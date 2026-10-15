using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GE.Art.Editor
{
    /// <summary>
    /// 엄마 캐릭터의 Humanoid 애니메이션(Mixamo FBX)을 임포트하고 Animator Controller를 구성하는 Editor 전용 도구.
    /// - Assets/_Art/Animations/Mother/Mother_Idle.fbx, Mother_Run.fbx를 Humanoid로 임포트하고 반복 재생·루트 굽기를 설정한다.
    /// - Mother.controller(Speed 파라미터로 Idle ↔ Run 전환)를 없을 때만 만든다.
    /// - MotherRealistic.prefab의 Animator에 Controller가 비어 있으면 연결하고 Apply Root Motion을 끈다.
    /// 엄마의 실제 이동·속도 결정은 MainGame 쪽 스크립트가 Animator의 Speed 값을 바꿔서 연결한다 (여기서는 다루지 않는다).
    /// </summary>
    public static class MotherAnimationSetup
    {
        private const string AnimationDir = "Assets/_Art/Animations/Mother";
        private const string IdleFbx = AnimationDir + "/Mother_Idle.fbx";
        private const string RunFbx = AnimationDir + "/Mother_Run.fbx";
        private const string ControllerPath = AnimationDir + "/Mother.controller";
        private const string PrefabPath = "Assets/_Art/Prefabs/MotherRealistic.prefab";

        /// <summary>Animator 파라미터 이름. 0이면 Idle, RunThreshold 이상이면 Run.</summary>
        public const string SpeedParameter = "Speed";
        private const float RunThreshold = 0.1f;

        [MenuItem("Tools/Art/Setup Mother Animations")]
        public static void Setup()
        {
            AnimationClip idle = ConfigureClip(IdleFbx, "Idle", bakeHorizontalMotion: true);
            AnimationClip run = ConfigureClip(RunFbx, "Run", bakeHorizontalMotion: false);
            AnimatorController controller = EnsureController(idle, run);
            AssignToPrefab(controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Mixamo FBX를 Humanoid 애니메이션 소스로 임포트한다. 파일에 메시가 들어 있어도 머티리얼은 만들지 않는다.
        /// 회전·높이는 포즈에 굽고(캐릭터가 기울거나 뜨지 않게), 수평 이동은 bakeHorizontalMotion에 따라 정한다.
        /// Run은 수평 이동을 루트 모션으로 남겨 두지만, 프리팹은 Apply Root Motion을 꺼서 제자리에서 달린다.
        /// </summary>
        private static AnimationClip ConfigureClip(string path, string clipName, bool bakeHorizontalMotion)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                throw new FileNotFoundException($"애니메이션 FBX가 없습니다: {path}");
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;
            // 파일을 교체하면 이전 파일 기준으로 저장된 Humanoid 매핑이 맞지 않아 아바타 생성이 실패하고,
            // 그러면 애니메이션 테이크도 읽히지 않는다. 매핑을 비워 자동 매핑을 새로 하게 한 뒤 먼저 임포트한다.
            HumanDescription cleared = importer.humanDescription;
            cleared.human = new HumanBone[0];
            cleared.skeleton = new SkeletonBone[0];
            importer.humanDescription = cleared;
            importer.clipAnimations = new ModelImporterClipAnimation[0];
            importer.SaveAndReimport();

            // 테이크 이름도 파일마다 다를 수 있으므로 항상 현재 파일의 기본 테이크에서 클립 설정을 만든다.
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
            {
                throw new System.InvalidOperationException($"애니메이션 클립이 없습니다: {path}");
            }

            ModelImporterClipAnimation clip = clips[0];
            clip.name = clipName;
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = bakeHorizontalMotion;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            AnimationClip result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == clipName);
            if (result == null || !result.isHumanMotion)
            {
                Debug.LogError($"[MotherAnimationSetup] Humanoid 클립을 만들지 못했습니다: {path}");
            }
            return result;
        }

        /// <summary>
        /// Controller는 없을 때만 만든다. 이미 있으면 사용자가 추가한 상태·전환을 보존하고,
        /// FBX 교체로 클립 연결이 끊긴(null) Idle·Run 상태만 다시 연결한다.
        /// </summary>
        private static AnimatorController EnsureController(AnimationClip idle, AnimationClip run)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                foreach (ChildAnimatorState child in existing.layers[0].stateMachine.states)
                {
                    if (child.state.motion != null) continue;
                    if (child.state.name == "Idle") child.state.motion = idle;
                    else if (child.state.name == "Run") child.state.motion = run;
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("Idle", new Vector3(300f, 100f, 0f));
            idleState.motion = idle;
            AnimatorState runState = machine.AddState("Run", new Vector3(300f, 220f, 0f));
            runState.motion = run;
            machine.defaultState = idleState;

            AnimatorStateTransition toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;
            toRun.AddCondition(AnimatorConditionMode.Greater, RunThreshold, SpeedParameter);

            AnimatorStateTransition toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;
            toIdle.AddCondition(AnimatorConditionMode.Less, RunThreshold, SpeedParameter);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>프리팹 Animator에 Controller가 비어 있을 때만 연결한다 (직접 바꾼 연결은 덮어쓰지 않는다).</summary>
        private static void AssignToPrefab(AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = root.GetComponent<Animator>();
                if (animator == null || animator.runtimeAnimatorController != null) return;
                animator.runtimeAnimatorController = controller;
                // 이동은 게임 스크립트가 담당하므로 애니메이션의 루트 이동을 적용하지 않는다 (제자리 달리기).
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
