using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GE.Art.Editor
{
    /// <summary>
    /// 방 안에서 텍스처 없이 단색(FBX 내장·URP 기본 머티리얼)으로 남아 있던 부분을 채우는 Editor 전용 도구.
    /// - Build: 천장 회벽·면 직물·의자 메시 원단 텍스처를 코드로 그리고, 방 디테일 머티리얼을 만든다(이미 있으면 유지).
    /// - Apply: 활성 씬의 Map 아래 렌더러에서 "머티리얼 이름 → 새 머티리얼" 규칙으로 해당 슬롯만 바꾼다(씬 오버라이드, Undo 가능).
    ///   PlayerInBed 이불은 프리팹 원본을 바꿔 침대 커버와 같은 머티리얼을 계속 쓰게 한다.
    /// FBX 임포터 리맵을 쓰지 않는 이유: 다른 오브젝트(의자·마우스 등)가 다른 FBX의 내장 머티리얼을 직접 참조하고 있어,
    /// 리맵하면 그 내장 머티리얼이 사라져 분홍색(누락)이 된다. 그래서 기존 나무·벽지와 같이 씬 렌더러에 직접 지정한다.
    /// 그대로 두는 것: 유리, 문·옷장 손잡이(Metal/Gold), 모니터 화면 슬롯(컴퓨터 등장 이벤트가 교체하는 슬롯).
    /// </summary>
    public static class RoomMaterialBuilder
    {
        private const string MaterialDir = "Assets/_Art/Materials/Room";
        private const string TextureDir = "Assets/_Art/Textures/Room";
        private const string TriplanarShaderName = "GE/Triplanar Lit";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string BedFbx = "Assets/_Art/Models/bedSingle.fbx";
        private const string PlayerInBedPrefab = "Assets/_Art/Prefabs/PlayerInBed.prefab";
        private const int TextureSize = 512;

        [MenuItem("Tools/Art/Build And Apply Room Detail Materials (Active Scene)")]
        public static string BuildAndApply()
        {
            Dictionary<string, Material> materials = Build();
            return Apply(materials);
        }

        // ---------------- 머티리얼 ----------------

        /// <summary>텍스처를 다시 그리고 머티리얼을 준비한다. 머티리얼이 이미 있으면 Inspector에서 바꾼 값을 존중해 그대로 쓴다.</summary>
        public static Dictionary<string, Material> Build()
        {
            Texture2D plasterAlbedo = SaveTexture(PaintPlaster(out float[] plasterHeight), "T_CeilingPlaster_Color", false);
            Texture2D plasterNormal = SaveTexture(HeightToNormal(plasterHeight, 1.5f), "T_CeilingPlaster_Normal", true);
            Texture2D weaveAlbedo = SaveTexture(PaintWeave(out float[] weaveHeight), "T_Cotton_Color", false);
            Texture2D weaveNormal = SaveTexture(HeightToNormal(weaveHeight, 3f), "T_Cotton_Normal", true);
            Texture2D meshAlbedo = SaveTexture(PaintChairMesh(out float[] meshHeight), "T_ChairMesh_Color", false);
            Texture2D meshNormal = SaveTexture(HeightToNormal(meshHeight, 4f), "T_ChairMesh_Normal", true);

            // 침대 커버의 원래 색(분홍·흰색)을 그대로 이어받아 질감만 더한다.
            Color blanketColor = FbxMaterialColor("carpet", new Color(0.98f, 0.64f, 0.62f));
            Color sheetColor = FbxMaterialColor("carpetWhite", new Color(0.95f, 0.95f, 0.97f));

            return new Dictionary<string, Material>
            {
                ["Ceiling"] = EnsureTriplanar("M_CeilingPaint", plasterAlbedo, plasterNormal, new Color(0.93f, 0.92f, 0.90f), 1.6f, 0.15f, 0.6f),
                ["BedSheet"] = EnsureTriplanar("M_BedSheet", weaveAlbedo, weaveNormal, sheetColor, 0.14f, 0.2f, 0.5f),
                ["BedBlanket"] = EnsureTriplanar("M_BedBlanket", weaveAlbedo, weaveNormal, blanketColor, 0.14f, 0.2f, 0.6f),
                ["ChairFabric"] = EnsureTriplanar("M_ChairFabric", meshAlbedo, meshNormal, new Color(0.30f, 0.31f, 0.34f), 0.08f, 0.25f, 0.7f),
                ["PlasticBlack"] = EnsureLit("M_PlasticBlack", new Color(0.09f, 0.09f, 0.10f), 0f, 0.55f),
                ["PlasticLight"] = EnsureLit("M_PlasticLight", new Color(0.78f, 0.78f, 0.80f), 0f, 0.45f),
                ["MetalSatin"] = EnsureLit("M_MetalSatin", new Color(0.72f, 0.73f, 0.75f), 0.9f, 0.55f),
                ["LampDiffuser"] = EnsureLit("M_LampDiffuser", new Color(0.95f, 0.94f, 0.90f), 0f, 0.7f),
                ["Wallpaper"] = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Art/Materials/M_Wallpaper.mat"),
            };
        }

        // ---------------- 씬 적용 ----------------

        /// <summary>
        /// 슬롯의 현재 머티리얼 이름을 보고 바꿀 머티리얼을 정한다. 오브젝트마다 예외가 있는 것은 path로 구분한다.
        /// null을 돌려주면 그 슬롯은 그대로 둔다.
        /// </summary>
        private static Material Choose(string path, string current, Dictionary<string, Material> m)
        {
            switch (current)
            {
                case "Lit": return path.EndsWith("Cube (1)") ? m["Ceiling"] : null;                       // 천장(URP 기본 회색)
                case "carpetWhite": return m["BedSheet"];                                                  // 매트리스·베개·커버 접힌 부분
                case "carpet": return m["BedBlanket"];                                                     // 침대 커버
                case "metal": return path.EndsWith("bedSingle") ? m["MetalSatin"] : null;                  // 침대 다리
                case "metalDark": return m["PlasticBlack"];                                                // 키보드 본체·모니터 테두리
                case "metalMedium":
                case "LightMetal":
                case "Grey": return m["PlasticLight"];                                                     // 키보드 키·마우스·천장등 몸체
                case "Light": return path.EndsWith("Light_Ceiling6") ? m["LampDiffuser"] : null;           // 천장등 갓
                case "60,59,59": return path.EndsWith("Office Chair/Chair") ? m["ChairFabric"] : m["PlasticBlack"]; // 의자 좌석·등받이 / 다리·레버
                case "_defaultMat":
                    if (path.Contains("Office Chair")) return m["MetalSatin"];                             // 의자 가스실린더
                    return path.Contains("/wall") ? m["Wallpaper"] : null;                                 // 벽 바깥면·모서리 마감
                default: return null;
            }
        }

        public static string Apply(Dictionary<string, Material> materials)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform map = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Map");
            if (map == null) throw new System.InvalidOperationException("활성 씬에서 Map을 찾을 수 없습니다.");

            var report = new List<string>();
            foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>(true))
            {
                string path = PathOf(renderer.transform);
                if (path.Contains("/PlayerInBed/")) continue; // 프리팹 원본에서 따로 바꾼다.
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    Material replacement = Choose(path, slots[i].name, materials);
                    if (replacement == null || replacement == slots[i]) continue;
                    report.Add($"{path}[{i}] {slots[i].name} → {replacement.name}");
                    slots[i] = replacement;
                    changed = true;
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Apply Room Detail Materials");
                renderer.sharedMaterials = slots;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            report.AddRange(ApplyToPlayerInBedPrefab(materials));
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            return string.Join("\n", report);
        }

        // PlayerInBed의 이불(Blanket)은 침대 커버와 같은 머티리얼을 써야 하므로 프리팹 원본에서 함께 바꾼다.
        private static IEnumerable<string> ApplyToPlayerInBedPrefab(Dictionary<string, Material> materials)
        {
            var report = new List<string>();
            if (!File.Exists(PlayerInBedPrefab)) return report;
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerInBedPrefab);
            try
            {
                Renderer blanket = contents.transform.Find("Blanket")?.GetComponent<Renderer>();
                if (blanket == null) return report;
                Material[] slots = blanket.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    Material replacement = slots[i].name == "carpet" ? materials["BedBlanket"] : slots[i].name == "carpetWhite" ? materials["BedSheet"] : null;
                    if (replacement == null) continue;
                    report.Add($"{PlayerInBedPrefab}:Blanket[{i}] {slots[i].name} → {replacement.name}");
                    slots[i] = replacement;
                    changed = true;
                }
                if (changed)
                {
                    blanket.sharedMaterials = slots;
                    PrefabUtility.SaveAsPrefabAsset(contents, PlayerInBedPrefab);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return report;
        }

        // ---------------- 텍스처 ----------------

        /// <summary>천장 회벽: 여러 크기의 노이즈를 겹친 아주 옅은 얼룩. 높이값은 노멀맵에 쓴다(타일 반복 가능하도록 원형 좌표 노이즈).</summary>
        private static Color[] PaintPlaster(out float[] height)
        {
            height = new float[TextureSize * TextureSize];
            var pixels = new Color[height.Length];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float h = TileableNoise(x, y, 8f, 0.0f) * 0.55f + TileableNoise(x, y, 32f, 3.1f) * 0.3f + TileableNoise(x, y, 96f, 7.7f) * 0.15f;
                height[y * TextureSize + x] = h;
                float v = 0.9f + (h - 0.5f) * 0.12f;
                pixels[y * TextureSize + x] = new Color(v, v, v, 1f);
            }
            return pixels;
        }

        /// <summary>면 직물(평직): 가로·세로 실이 번갈아 위아래로 지나가는 무늬. 색은 거의 흰색이며 실제 색은 머티리얼 Color로 입힌다.</summary>
        private static Color[] PaintWeave(out float[] height)
        {
            const int threads = 32; // 텍스처 한 장에 실 32가닥(타일 0.14m 기준 약 4mm 간격)
            height = new float[TextureSize * TextureSize];
            var pixels = new Color[height.Length];
            float cell = TextureSize / (float)threads;
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                int cx = Mathf.FloorToInt(x / cell), cy = Mathf.FloorToInt(y / cell);
                float fx = x / cell - cx, fy = y / cell - cy;
                bool warpOnTop = ((cx + cy) & 1) == 0;
                // 위에 놓인 실은 가운데가 볼록하고, 실 사이 틈은 낮다.
                float across = warpOnTop ? Mathf.Sin(fx * Mathf.PI) : Mathf.Sin(fy * Mathf.PI);
                float along = warpOnTop ? Mathf.Sin(fy * Mathf.PI) : Mathf.Sin(fx * Mathf.PI);
                float h = Mathf.Clamp01(across * 0.8f + along * 0.2f) + TileableNoise(x, y, 64f, 1.3f) * 0.15f;
                height[y * TextureSize + x] = h;
                float v = 0.86f + h * 0.12f;
                pixels[y * TextureSize + x] = new Color(v, v, v, 1f);
            }
            return pixels;
        }

        /// <summary>사무용 의자 메시 원단: 촘촘한 격자 구멍. 구멍은 어둡고 낮게, 실은 밝고 높게.</summary>
        private static Color[] PaintChairMesh(out float[] height)
        {
            const int holes = 48;
            height = new float[TextureSize * TextureSize];
            var pixels = new Color[height.Length];
            float cell = TextureSize / (float)holes;
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float fx = (x / cell) % 1f, fy = (y / cell) % 1f;
                float distance = Mathf.Max(Mathf.Abs(fx - 0.5f), Mathf.Abs(fy - 0.5f));
                float h = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.4f, distance));
                height[y * TextureSize + x] = h;
                float v = Mathf.Lerp(0.35f, 1f, h);
                pixels[y * TextureSize + x] = new Color(v, v, v, 1f);
            }
            return pixels;
        }

        // 높이맵 → OpenGL 방식 노멀맵(GE/Triplanar Lit의 Normal Map 슬롯 규약). 가장자리는 반대편과 이어 계산해 이음매가 없다.
        private static Color[] HeightToNormal(float[] height, float strength)
        {
            var pixels = new Color[height.Length];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float left = height[y * TextureSize + (x - 1 + TextureSize) % TextureSize];
                float right = height[y * TextureSize + (x + 1) % TextureSize];
                float down = height[((y - 1 + TextureSize) % TextureSize) * TextureSize + x];
                float up = height[((y + 1) % TextureSize) * TextureSize + x];
                var normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                pixels[y * TextureSize + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
            }
            return pixels;
        }

        // 원 위를 도는 좌표로 Perlin 노이즈를 읽어 가로·세로 모두 이어지는 노이즈를 만든다.
        private static float TileableNoise(int x, int y, float frequency, float seed)
        {
            float u = x / (float)TextureSize * Mathf.PI * 2f, v = y / (float)TextureSize * Mathf.PI * 2f;
            float r = frequency / (Mathf.PI * 2f);
            float a = Mathf.PerlinNoise(seed + r * Mathf.Cos(u), seed + r * Mathf.Sin(u) + r * Mathf.Cos(v) * 0.37f);
            float b = Mathf.PerlinNoise(seed + 11f + r * Mathf.Cos(v), seed + 5f + r * Mathf.Sin(v) + r * Mathf.Sin(u) * 0.37f);
            return (a + b) * 0.5f;
        }

        // ---------------- 저장 도우미 ----------------

        private static Texture2D SaveTexture(Color[] pixels, string name, bool normal)
        {
            EnsureFolder(TextureDir);
            string path = $"{TextureDir}/{name}.png";
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false, true);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = TextureSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>트라이플래너 머티리얼(UV가 없거나 크기가 제각각인 모델용). tileSize는 텍스처 한 장이 덮는 실제 크기(m).</summary>
        private static Material EnsureTriplanar(string name, Texture2D albedo, Texture2D normal, Color color, float tileSize, float smoothness, float normalScale)
        {
            Material material = LoadOrCreate(name, TriplanarShaderName, out bool created);
            if (!created) return material;
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_TileSize", tileSize);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_BumpScale", normalScale);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureLit(string name, Color color, float metallic, float smoothness)
        {
            Material material = LoadOrCreate(name, LitShaderName, out bool created);
            if (!created) return material;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name, string shaderName, out bool created)
        {
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = material == null;
            if (!created) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException($"셰이더를 찾을 수 없습니다: {shaderName}");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // bedSingle.fbx 내장 머티리얼의 원래 색(커버 분홍·흰색)을 읽는다. 없으면 기본값.
        private static Color FbxMaterialColor(string materialName, Color fallback)
        {
            Material material = AssetDatabase.LoadAllAssetsAtPath(BedFbx).OfType<Material>().FirstOrDefault(m => m.name == materialName);
            if (material == null) return fallback;
            return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
