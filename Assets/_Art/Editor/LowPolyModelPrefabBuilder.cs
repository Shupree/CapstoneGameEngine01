using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GE.Art.Editor
{
    /// <summary>
    /// Blender 스크립트(Tools/Blender)로 만든 엄마 캐릭터·휴대용 게임기 FBX를 Unity에서 쓸 수 있게 정리하는 Editor 전용 도구.
    /// - URP Lit 머티리얼을 만들고 FBX 내장 머티리얼 이름에 리맵한다.
    /// - ModelImporter를 설정한다 (1유닛=1m, 축 변환 베이크, 엄마는 Humanoid Avatar).
    /// - 프리팹을 만든다. 이미 있는 머티리얼·프리팹은 덮어쓰지 않으므로 Inspector에서 조정한 값이 보존된다.
    /// FBX를 다시 생성해도 프리팹은 FBX를 참조하므로 자동으로 갱신된다. 이 메뉴는 리맵·임포트 설정만 다시 맞춘다.
    /// 씬은 열거나 수정하지 않는다 (프리팹 조립은 프리뷰 씬에서 한다).
    /// </summary>
    public static class LowPolyModelPrefabBuilder
    {
        private const string MotherFbx = "Assets/_Art/Models/Mother.fbx";
        private const string ConsoleFbx = "Assets/_Art/Models/HandheldConsole.fbx";
        private const string MotherMaterialDir = "Assets/_Art/Materials/Mother";
        private const string ConsoleMaterialDir = "Assets/_Art/Materials/HandheldConsole";
        private const string MotherPrefab = "Assets/_Art/Prefabs/Mother.prefab";
        private const string ConsolePrefab = "Assets/_Art/Prefabs/HandheldConsole.prefab";
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        // 이름은 Blender 스크립트의 MATERIALS 키와 같아야 리맵된다. 색은 sRGB 기준.
        private static readonly MaterialSpec[] MotherMaterials =
        {
            new MaterialSpec("M_Mother_Skin", new Color(0.93f, 0.78f, 0.68f), 0.30f),
            new MaterialSpec("M_Mother_Hair", new Color(0.24f, 0.16f, 0.12f), 0.35f),
            new MaterialSpec("M_Mother_Cardigan", new Color(0.72f, 0.52f, 0.56f), 0.15f),
            new MaterialSpec("M_Mother_Pants", new Color(0.30f, 0.32f, 0.38f), 0.20f),
            new MaterialSpec("M_Mother_Slipper", new Color(0.88f, 0.83f, 0.74f), 0.20f),
            new MaterialSpec("M_Mother_Eye", new Color(0.08f, 0.07f, 0.07f), 0.70f),
        };

        private static readonly MaterialSpec[] ConsoleMaterials =
        {
            new MaterialSpec("M_Console_Body", new Color(0.16f, 0.16f, 0.18f), 0.45f),
            new MaterialSpec("M_Console_Grip", new Color(0.27f, 0.36f, 0.50f), 0.35f),
            new MaterialSpec("M_Console_Bezel", new Color(0.03f, 0.03f, 0.035f), 0.80f),
            new MaterialSpec("M_Console_Button", new Color(0.80f, 0.80f, 0.83f), 0.50f),
            new MaterialSpec("M_Console_Stick", new Color(0.10f, 0.10f, 0.10f), 0.25f),
            // 화면: 꺼진 화면처럼 어둡고 반질한 기본값. RenderTexture는 _BaseMap(밝게 보이려면 _EmissionMap도)에 연결하거나
            // CRT 등 화면 전용 머티리얼로 Screen 렌더러의 머티리얼을 교체한다.
            new MaterialSpec("M_Console_Screen", new Color(0.02f, 0.025f, 0.03f), 0.90f),
        };

        private readonly struct MaterialSpec
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly float Smoothness;

            public MaterialSpec(string name, Color color, float smoothness)
            {
                Name = name;
                Color = color;
                Smoothness = smoothness;
            }
        }

        [MenuItem("Tools/Art/Setup Mother and Handheld Console Prefabs")]
        public static void SetupAll()
        {
            SetupHandheldConsole();
            SetupMother();
            AssetDatabase.SaveAssets();
        }

        // ---------------- 휴대용 게임기 ----------------

        /// <summary>
        /// 게임기 프리팹 구조: HandheldConsole(루트) / Model(FBX, Y 180° 회전) / HandheldConsole_Body·Screen·LeftStick·RightStick.
        /// 루트의 +Z가 플레이어가 바라보는 방향이고 화면은 루트의 -Z(플레이어 쪽)를 향한다.
        /// 따라서 카메라 자식으로 넣고 회전 0, 앞쪽(+Z)에 두면 화면이 카메라를 향한다.
        /// </summary>
        public static void SetupHandheldConsole()
        {
            Material[] materials = EnsureMaterials(ConsoleMaterialDir, ConsoleMaterials);
            ConfigureImporter(ConsoleFbx, materials, humanoid: false);
            if (File.Exists(ConsolePrefab)) return;

            BuildPrefabInPreviewScene(ConsolePrefab, scene =>
            {
                var root = new GameObject("HandheldConsole");
                SceneManager.MoveGameObjectToScene(root, scene);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(ConsoleFbx), scene);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                return root;
            });
        }

        // ---------------- 엄마 캐릭터 ----------------

        /// <summary>
        /// 엄마 프리팹은 FBX의 프리팹 변형(Variant)이다. 루트에 Humanoid Avatar가 연결된 Animator가 있고
        /// Controller는 비워 둔다 (애니메이션 작업 시 연결). 정면은 +Z, 원점은 발바닥.
        /// </summary>
        public static void SetupMother()
        {
            Material[] materials = EnsureMaterials(MotherMaterialDir, MotherMaterials);
            ConfigureImporter(MotherFbx, materials, humanoid: true);

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(MotherFbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"[LowPolyModelPrefabBuilder] {MotherFbx}의 Humanoid Avatar가 유효하지 않습니다. 본 이름을 확인하세요.");
            }

            if (File.Exists(MotherPrefab)) return;
            BuildPrefabInPreviewScene(MotherPrefab, scene =>
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(MotherFbx), scene);
                instance.name = "Mother";
                return instance;
            });
        }

        // ---------------- 공용 ----------------

        /// <summary>
        /// FBX 임포트 설정. Blender 스크립트(lowpoly_builder.export_fbx)로 미터 단위로 내보낸 파일 기준이다 (모델 정면 = +Z).
        /// MotherRealisticPrefabBuilder도 이 함수를 공유한다.
        /// </summary>
        internal static void ConfigureImporter(string path, Material[] materials, bool humanoid)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                throw new FileNotFoundException($"FBX가 없습니다. Tools/Blender 스크립트로 먼저 생성하세요: {path}");
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            // 루트에 -90° X 회전이 남지 않도록 축 변환을 메시·본에 굽는다.
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (Material material in materials)
            {
                // 재실행 시 기존 리맵을 지우고 다시 연결해 머티리얼 교체가 반영되게 한다.
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name);
                importer.RemoveRemap(id);
                importer.AddRemap(id, material);
            }

            if (humanoid)
            {
                // 본 이름이 Unity 표준 이름(Hips, LeftUpperArm …)이고 T-포즈로 만들어 둔 상태다.
                // 매핑을 비워 Unity 자동 매핑을 새 FBX 기준으로 다시 만들게 한 뒤, 빠진 선택 본을 보완한다.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false;
                HumanDescription cleared = importer.humanDescription;
                cleared.human = new HumanBone[0];
                cleared.skeleton = new SkeletonBone[0];
                importer.humanDescription = cleared;
                importer.SaveAndReimport();
                CompleteHumanMapping(importer, path);
                return;
            }

            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Unity 자동 매핑은 Chest·Shoulder 같은 선택 본을 빠뜨릴 수 있다.
        /// 자동 생성된 Avatar의 설명(스켈레톤 포함)을 바탕으로, 이름이 HumanBodyBones와 같은 본 중 빠진 것을 추가해 다시 임포트한다.
        /// </summary>
        private static void CompleteHumanMapping(ModelImporter importer, string path)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman) return;

            HumanDescription description = avatar.humanDescription;
            var human = description.human.ToList();
            var skeletonNames = description.skeleton.Select(bone => bone.name).ToHashSet();
            for (int i = 0; i < HumanTrait.BoneCount; i++)
            {
                string humanName = HumanTrait.BoneName[i];
                string boneName = ((HumanBodyBones)i).ToString();
                if (human.Any(h => h.humanName == humanName) || !skeletonNames.Contains(boneName)) continue;
                human.Add(new HumanBone { humanName = humanName, boneName = boneName, limit = new HumanLimit { useDefaultValues = true } });
            }

            if (human.Count == description.human.Length) return;
            description.human = human.ToArray();
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }

        /// <summary>없는 머티리얼만 URP Lit으로 만든다. 이미 있으면 Inspector에서 바꾼 값을 존중해 그대로 쓴다.</summary>
        private static Material[] EnsureMaterials(string folder, MaterialSpec[] specs)
        {
            EnsureFolder(folder);
            Shader lit = Shader.Find(LitShaderName);
            if (lit == null)
            {
                throw new System.InvalidOperationException($"셰이더를 찾을 수 없습니다: {LitShaderName}");
            }

            var result = new Material[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                string path = $"{folder}/{specs[i].Name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = specs[i].Name };
                    material.SetColor("_BaseColor", specs[i].Color);
                    material.SetFloat("_Smoothness", specs[i].Smoothness);
                    material.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(material, path);
                }
                result[i] = material;
            }
            return result;
        }

        /// <summary>
        /// 열린 씬을 건드리지 않도록 프리뷰 씬에서 프리팹을 조립·저장한다.
        /// build 콜백은 저장할 루트 오브젝트를 반환한다.
        /// </summary>
        internal static void BuildPrefabInPreviewScene(string prefabPath, System.Func<Scene, GameObject> build)
        {
            EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = build(preview);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success)
                {
                    Debug.LogError($"[LowPolyModelPrefabBuilder] 프리팹 저장 실패: {prefabPath}");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        internal static GameObject LoadModel(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                throw new FileNotFoundException($"모델을 불러올 수 없습니다: {path}");
            }
            return model;
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
