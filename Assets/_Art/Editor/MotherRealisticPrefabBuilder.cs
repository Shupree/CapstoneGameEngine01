using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GE.Art.Editor
{
    /// <summary>
    /// Tools/Blender/build_mother_realistic.py로 만든 사실적인 엄마 캐릭터(MotherRealistic.fbx)를 Unity에 연결하는 Editor 전용 도구.
    /// - 텍스처 임포트 설정(노멀맵, AO 선형, 줄무늬 반복 등)을 맞춘다.
    /// - URP Lit 머티리얼을 없을 때만 만들어 FBX 머티리얼 이름에 리맵한다 (Inspector에서 조정한 값 보존).
    /// - Humanoid Avatar(mixamorig 본 자동 매핑)로 임포트하고 FBX 프리팹 변형(MotherRealistic.prefab)을 만든다.
    /// 씬은 열거나 수정하지 않는다. 공용 임포트·프리팹 함수는 LowPolyModelPrefabBuilder에 있다.
    /// </summary>
    public static class MotherRealisticPrefabBuilder
    {
        private const string Fbx = "Assets/_Art/Models/MotherRealistic.fbx";
        private const string TextureDir = "Assets/_Art/Textures/MotherRealistic";
        private const string MaterialDir = "Assets/_Art/Materials/MotherRealistic";
        private const string PrefabPath = "Assets/_Art/Prefabs/MotherRealistic.prefab";
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>머티리얼 한 개의 설정. 이름은 Blender 스크립트의 MAT_* 상수와 같아야 리맵된다.</summary>
        private sealed class Spec
        {
            public string Name;
            public string BaseMap;
            public Color Color = Color.white;
            public float Smoothness = 0.3f;
            public string NormalMap;
            public string OcclusionMap;
            public bool DoubleSided;
            public float AlphaClip = -1f;   // 0 이상이면 알파 클리핑(눈썹·속눈썹)
            public bool CastShadows = true;
        }

        private static readonly Spec[] Specs =
        {
            new Spec { Name = "M_MotherR_Skin", BaseMap = "T_MotherR_Skin.png", Smoothness = 0.42f },
            new Spec { Name = "M_MotherR_Eyes", BaseMap = "T_MotherR_Eye.png", Smoothness = 0.85f },
            new Spec { Name = "M_MotherR_Eyebrows", BaseMap = "T_MotherR_Eyebrow.png", Smoothness = 0.1f, DoubleSided = true, AlphaClip = 0.35f, CastShadows = false },
            new Spec { Name = "M_MotherR_Eyelashes", BaseMap = "T_MotherR_Eyelashes.png", Smoothness = 0.1f, DoubleSided = true, AlphaClip = 0.35f, CastShadows = false },
            // 옷은 한 겹 메시라 소매·밑단 안쪽이 보이도록 양면 렌더링한다.
            new Spec { Name = "M_MotherR_Shirt", BaseMap = "T_MotherR_ShirtStripe.png", Smoothness = 0.15f, DoubleSided = true },
            new Spec { Name = "M_MotherR_Jeans", BaseMap = "T_MotherR_Jeans_Diffuse.png", NormalMap = "T_MotherR_Jeans_Normal.png", OcclusionMap = "T_MotherR_Jeans_AO.png", Smoothness = 0.2f, DoubleSided = true },
            new Spec { Name = "M_MotherR_Socks", Color = new Color(0.93f, 0.93f, 0.91f), Smoothness = 0.1f },
            // 컬은 닫힌 튜브라 한쪽 면만 그린다. 볼륨 덩어리는 아래에서 안쪽이 보일 수 있어 양면으로 둔다.
            new Spec { Name = "M_MotherR_Hair", BaseMap = "T_MotherR_HairStrands.png", Smoothness = 0.4f },
            new Spec { Name = "M_MotherR_HairMass", BaseMap = "T_MotherR_HairCurly.png", NormalMap = "T_MotherR_HairCurly_Normal.png", Smoothness = 0.3f, DoubleSided = true },
        };

        [MenuItem("Tools/Art/Setup Mother Realistic Prefab")]
        public static void Setup()
        {
            ConfigureTextures();
            Material[] materials = EnsureMaterials();
            LowPolyModelPrefabBuilder.ConfigureImporter(Fbx, materials, humanoid: true);

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"[MotherRealisticPrefabBuilder] {Fbx}의 Humanoid Avatar가 유효하지 않습니다. mixamorig 본 매핑을 확인하세요.");
            }

            if (!File.Exists(PrefabPath))
            {
                LowPolyModelPrefabBuilder.BuildPrefabInPreviewScene(PrefabPath, scene =>
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(LowPolyModelPrefabBuilder.LoadModel(Fbx), scene);
                    instance.name = "MotherRealistic";
                    return instance;
                });
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>텍스처 용도별 임포트 설정. 바뀐 경우에만 다시 임포트한다.</summary>
        private static void ConfigureTextures()
        {
            foreach (string path in Directory.GetFiles(TextureDir, "*.png").Select(p => p.Replace('\\', '/')))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                string file = Path.GetFileName(path);
                bool changed = false;

                bool isNormal = file.Contains("_Normal");
                if (isNormal && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
                // AO는 색이 아니라 데이터이므로 선형으로 읽는다.
                bool linear = file.Contains("_AO");
                if (linear && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    changed = true;
                }
                // 줄무늬·머리 텍스처는 UV가 0~1을 넘어 반복된다.
                if (importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    changed = true;
                }
                bool alpha = file.Contains("Eyebrow") || file.Contains("Eyelashes");
                if (alpha && !importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        /// <summary>없는 머티리얼만 URP Lit으로 만든다. 이미 있으면 그대로 쓴다.</summary>
        private static Material[] EnsureMaterials()
        {
            LowPolyModelPrefabBuilder.EnsureFolder(MaterialDir);
            Shader lit = Shader.Find(LitShaderName);
            if (lit == null)
            {
                throw new System.InvalidOperationException($"셰이더를 찾을 수 없습니다: {LitShaderName}");
            }

            var result = new Material[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                Spec spec = Specs[i];
                string path = $"{MaterialDir}/{spec.Name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = CreateMaterial(lit, spec);
                    AssetDatabase.CreateAsset(material, path);
                }
                result[i] = material;
            }
            return result;
        }

        private static Material CreateMaterial(Shader lit, Spec spec)
        {
            var material = new Material(lit) { name = spec.Name };
            material.SetColor("_BaseColor", spec.Color);
            material.SetFloat("_Smoothness", spec.Smoothness);
            material.SetFloat("_Metallic", 0f);
            if (spec.BaseMap != null)
            {
                material.SetTexture("_BaseMap", LoadTexture(spec.BaseMap));
            }
            if (spec.NormalMap != null)
            {
                material.SetTexture("_BumpMap", LoadTexture(spec.NormalMap));
                material.EnableKeyword("_NORMALMAP");
            }
            if (spec.OcclusionMap != null)
            {
                material.SetTexture("_OcclusionMap", LoadTexture(spec.OcclusionMap));
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            if (spec.DoubleSided)
            {
                // URP Lit의 Render Face = Both
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
            if (spec.AlphaClip >= 0f)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", spec.AlphaClip);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (!spec.CastShadows)
            {
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            return material;
        }

        private static Texture2D LoadTexture(string file)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{file}");
            if (texture == null)
            {
                Debug.LogWarning($"[MotherRealisticPrefabBuilder] 텍스처가 없습니다: {TextureDir}/{file}");
            }
            return texture;
        }
    }
}
