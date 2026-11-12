using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace GE.Art.Editor
{
    /// <summary>
    /// 메인 UI 한글 폰트(SpoqaHanSansNeo-BoldBlack SDF)를 Static 아틀라스로 굽는 Editor 전용 도구.
    ///
    /// 왜 필요한가: 이 폰트가 Dynamic이면 화면에 새 글자가 나올 때마다 에셋에 글리프가 추가되고,
    /// 빌드·테스트 때는 Clear Dynamic Data on Build로 다시 지워져 커밋마다 의미 없는 변경이 생겼다.
    /// Static으로 미리 구워 두면 실행 중에 에셋이 바뀌지 않는다(미니게임 폰트들과 같은 방식).
    ///
    /// 글자 범위: MainUIFontCharset.txt(ASCII, 호환 자모, 자주 쓰는 기호, KS X 1001 완성형 2,350자)
    /// + 씬·프리팹의 텍스트와 Assets/Scripts 문자열 리터럴에서 찾은 비ASCII 글자.
    /// 목록에 없는 글자는 화면에 네모로 나오므로, 새 UI 문구에 희귀 글자를 쓰면 이 메뉴를 다시 실행한다.
    ///
    /// 기존 에셋을 제자리에서 다시 채우므로 GUID와 머티리얼 서브 에셋(fileID)이 유지되어
    /// 프리팹·씬의 m_fontAsset / m_sharedMaterial 참조가 끊기지 않는다.
    /// </summary>
    public static class MainUIFontBaker
    {
        private const string FontAssetPath = "Assets/TextMesh Pro/Resources/SpoqaHanSansNeo-BoldBlack SDF.asset";
        private const string CharsetPath = "Assets/_Art/Editor/MainUIFontCharset.txt";

        // 아틀라스 크기와 패딩. 패딩은 기존 머티리얼의 Gradient Scale(9 = 패딩 8 + 1)과 맞춘다.
        // 포인트 크기는 MaxPointSize부터 낮춰 가며 모든 글자가 아틀라스 한 장에 들어가는 가장 큰 값을 고른다.
        private const int AtlasWidth = 4096;
        private const int AtlasHeight = 2048;
        private const int Padding = 8;
        private const int MaxPointSize = 64;
        private const int MinPointSize = 30;
        private const int PointSizeStep = 2;

        // 메인 UI 문구가 있는 곳. 미니게임은 각자 전용 Static 폰트를 쓰므로 스크립트는 MiniGames를 제외한다.
        private static readonly string[] TextAssetRoots = { "Assets/Scenes", "Assets/Resources" };
        private static readonly string[] ScriptRoots = { "Assets/Scripts" };

        [MenuItem("Tools/Art/Bake Main UI Font (Static)")]
        public static void BakeFromMenu()
        {
            Debug.Log(Bake());
        }

        /// <summary>batch 모드 진입점: Unity.exe -batchmode -executeMethod GE.Art.Editor.MainUIFontBaker.BakeFromCommandLine</summary>
        public static void BakeFromCommandLine()
        {
            try
            {
                Debug.Log(Bake());
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string Bake()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null) throw new FileNotFoundException($"폰트 에셋이 없습니다: {FontAssetPath}");
            Font source = LoadSourceFont(font);

            List<char> wanted = CollectCharacters(out int scannedCount);
            if (FontEngine.LoadFontFace(source, MaxPointSize) != FontEngineError.Success)
            {
                throw new InvalidOperationException($"원본 폰트를 읽을 수 없습니다: {AssetDatabase.GetAssetPath(source)}");
            }

            // 원본 폰트에 아예 없는 글자는 공간 부족과 구분해 경고로만 남긴다(Dynamic일 때도 표시할 수 없던 글자).
            var available = new List<char>();
            var notInFont = new StringBuilder();
            foreach (char c in wanted)
            {
                if (FontEngine.TryGetGlyphIndex(c, out uint glyphIndex) && glyphIndex != 0) available.Add(c);
                else notInFont.Append(c);
            }
            string characters = new string(available.ToArray());

            for (int pointSize = MaxPointSize; pointSize >= MinPointSize; pointSize -= PointSizeStep)
            {
                if (!TryBakeAt(font, source, pointSize, characters)) continue;

                FinishAsStatic(font, pointSize, characters);
                string warning = notInFont.Length > 0 ? $"\n원본 폰트에 없어 제외한 글자: {notInFont}" : string.Empty;
                return $"[MainUIFontBaker] {FontAssetPath} Static 굽기 완료: 글자 {font.characterTable.Count}개 " +
                       $"(목록 {wanted.Count}개, 프로젝트 문자열에서 추가 {scannedCount}개), 포인트 크기 {pointSize}, " +
                       $"아틀라스 {AtlasWidth}x{AtlasHeight}, 패딩 {Padding}{warning}";
            }
            throw new InvalidOperationException($"포인트 크기 {MinPointSize}에서도 글자 {characters.Length}개가 {AtlasWidth}x{AtlasHeight}에 들어가지 않습니다.");
        }

        /// <summary>Static 에셋은 sourceFontFile이 비어 있으므로 Editor 전용 참조(m_SourceFontFile_EditorRef)에서 원본 TTF를 찾는다.</summary>
        private static Font LoadSourceFont(TMP_FontAsset font)
        {
            var serialized = new SerializedObject(font);
            var source = serialized.FindProperty("m_SourceFontFile_EditorRef")?.objectReferenceValue as Font;
            if (source == null)
            {
                string guid = serialized.FindProperty("m_SourceFontFileGUID")?.stringValue;
                source = string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            }
            if (source == null) throw new InvalidOperationException("폰트 에셋의 원본 TTF 참조를 찾을 수 없습니다.");
            return source;
        }

        /// <summary>
        /// 지정한 포인트 크기로 아틀라스를 비우고 다시 채운다. 모든 글자가 한 장에 들어가면 true.
        /// 글리프 추가 API(TryAddCharacters)가 Dynamic에서만 동작하므로 굽는 동안만 Dynamic으로 둔다.
        /// </summary>
        private static bool TryBakeAt(TMP_FontAsset font, Font source, int pointSize, string characters)
        {
            // 빈 영역(free rect)을 새 아틀라스 크기로 잡도록 Clear 전에 크기와 패딩을 먼저 바꾼다.
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_AtlasWidth").intValue = AtlasWidth;
            serialized.FindProperty("m_AtlasHeight").intValue = AtlasHeight;
            serialized.FindProperty("m_AtlasPadding").intValue = Padding;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = false;
            font.ClearFontAssetData(true);

            // 줄 높이·기준선 등 글꼴 지표는 포인트 크기에 비례하므로 해당 크기의 FaceInfo로 통째로 교체한다.
            FontEngine.LoadFontFace(source, pointSize);
            FaceInfo face = FontEngine.GetFaceInfo();
            font.faceInfo = face;

            bool added = font.TryAddCharacters(characters, out string missing, false);
            return added && string.IsNullOrEmpty(missing) && font.characterTable.Count >= characters.Length;
        }

        /// <summary>Static으로 고정하고 머티리얼·생성 설정을 새 아틀라스에 맞춘 뒤 저장한다.</summary>
        private static void FinishAsStatic(TMP_FontAsset font, int pointSize, string characters)
        {
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            Material material = font.material;
            if (material != null)
            {
                material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasWidth);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasHeight);
                material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
                EditorUtility.SetDirty(material);
            }

            // Font Asset Creator로 열었을 때도 같은 설정이 보이도록 생성 설정을 기록한다(필드가 없으면 건너뜀).
            var serialized = new SerializedObject(font);
            SetInt(serialized, "m_CreationSettings.pointSize", pointSize);
            SetInt(serialized, "m_CreationSettings.padding", Padding);
            SetInt(serialized, "m_CreationSettings.atlasWidth", AtlasWidth);
            SetInt(serialized, "m_CreationSettings.atlasHeight", AtlasHeight);
            SerializedProperty sequence = serialized.FindProperty("m_CreationSettings.characterSequence");
            if (sequence != null) sequence.stringValue = characters;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(font);
            foreach (Texture2D atlas in font.atlasTextures.Where(t => t != null)) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.intValue = value;
        }

        /// <summary>기본 목록 파일과 프로젝트 문자열의 글자를 합친다. scannedCount는 기본 목록에 없던 추가 글자 수.</summary>
        private static List<char> CollectCharacters(out int scannedCount)
        {
            if (!File.Exists(CharsetPath)) throw new FileNotFoundException($"글자 목록 파일이 없습니다: {CharsetPath}");
            var set = new SortedSet<char>();
            foreach (string line in File.ReadAllLines(CharsetPath, Encoding.UTF8))
            {
                if (line.StartsWith("#")) continue;
                foreach (char c in line) AddIfPrintable(set, c);
            }

            int baseCount = set.Count;
            foreach (string root in TextAssetRoots.Where(Directory.Exists))
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                             .Where(p => p.EndsWith(".unity") || p.EndsWith(".prefab")))
                {
                    // YAML은 비ASCII 문자열을 \uXXXX로 저장하는 경우가 있어 먼저 풀어서 읽는다.
                    string text = Regex.Replace(File.ReadAllText(path, Encoding.UTF8), @"\\u([0-9A-Fa-f]{4})",
                        m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                    foreach (char c in text) if (c > 0x7F) AddIfPrintable(set, c);
                }
            }

            // 스크립트는 주석이 아닌 문자열 리터럴 안의 글자만 쓴다.
            var literal = new Regex("\"(?:[^\"\\\\\\n]|\\\\.)*\"");
            foreach (string root in ScriptRoots.Where(Directory.Exists))
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                             .Where(p => !p.Replace('\\', '/').Contains("/MiniGames/")))
                {
                    foreach (Match match in literal.Matches(File.ReadAllText(path, Encoding.UTF8)))
                    {
                        foreach (char c in match.Value) if (c > 0x7F) AddIfPrintable(set, c);
                    }
                }
            }

            scannedCount = set.Count - baseCount;
            return set.ToList();
        }

        private static void AddIfPrintable(SortedSet<char> set, char c)
        {
            if (char.IsControl(c) || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.PrivateUse) return;
            if (c == '﻿') return;
            set.Add(c);
        }
    }
}
