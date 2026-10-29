#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GE.MiniGames.Dino;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GE.MiniGames.PlayModeTests
{
    // 실제 Unity 2D 다각형 충돌과 저장된 MiniGame04를 사용합니다. 모델 대체 판정이나 충돌 목은 사용하지 않습니다.
    public sealed class DinoSceneTests : InputTestFixture
    {
        private Keyboard keyboard;
        private MonoBehaviour manager;
        private bool previousBackground;
        private DinoArt Art => AssetDatabase.LoadAssetAtPath<DinoArt>("Assets/MiniGames/Dino/DinoArt.asset");
        public override void Setup()
        {
            base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>();
            previousBackground = Application.runInBackground; Application.runInBackground = true;
        }
        public override void TearDown() { Application.runInBackground = previousBackground; base.TearDown(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (manager != null) Object.Destroy(manager.gameObject);
            var scene = SceneManager.GetSceneByName("MiniGame04");
            // 모델 전용 테스트도 연속 실행되므로 정리용 씬은 기존 인스턴스를 재사용합니다.
            var cleanup = SceneManager.GetSceneByName("Dino test cleanup");
            if (!cleanup.IsValid()) cleanup = SceneManager.CreateScene("Dino test cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MiniGame04.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Keys();
        }
        private IEnumerator Keys(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null; }
        private static void Frame(DinoGame game, float dt, int buttons = 0)
        {
            var method = typeof(MiniGameController).GetMethod("ProcessFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(game, new[] { (object)dt, Enum.ToObject(method.GetParameters()[1].ParameterType, buttons) });
        }
        private static List<DinoObstacle> Variants()
        {
            var all = new List<DinoObstacle>();
            for (int i = 0; i < 3; i++) all.Add(new DinoObstacle { Kind = DinoObstacleKind.Cactus, Variant = i });
            for (int i = 0; i < 2; i++) all.Add(new DinoObstacle { Kind = DinoObstacleKind.Rock, Variant = i });
            all.Add(new DinoObstacle { Kind = DinoObstacleKind.LowBird }); all.Add(new DinoObstacle { Kind = DinoObstacleKind.HighBird });
            return all;
        }
        private static void AutoJump(DinoModel model, HashSet<int> jumped)
        {
            for (int i = 0; i < model.Course.Count; i++)
            {
                var obstacle = model.Course[i];
                if (!obstacle.NeedsJump || jumped.Contains(i)) continue;
                if (model.ObstacleX(i) <= model.Rules.DinosaurFront + model.Rules.speed * DinoCourse.JumpLead(model.Rules, obstacle))
                { Assert.That(model.Grounded, Is.True, "다음 점프 전 착지 필요: " + i); model.Jump(); jumped.Add(i); }
                break;
            }
        }

        [UnityTest] public IEnumerator MinimumIntervals_All49PairsUseUnityPolygonCollisions()
        {
            var rules = new DinoRules(); int checkedPairs = 0;
            foreach (var first in Variants()) foreach (var second in Variants())
            {
                first.CourseX = rules.DinosaurFront + rules.speed * .9f;
                second.CourseX = first.CourseX + rules.speed * .85f;
                // 같은 객체 조합도 서로 다른 코스 엔트리로 복사합니다.
                var pair = new[] { new DinoObstacle { Kind=first.Kind, Variant=first.Variant, CourseX=first.CourseX },
                    new DinoObstacle { Kind=second.Kind, Variant=second.Variant, CourseX=second.CourseX } };
                using (var model = new DinoModel(rules, Art, pair))
                {
                    var jumped = new HashSet<int>();
                    for (int frame = 0; frame < 360 && model.Result == DinoStep.Running; frame++)
                    { AutoJump(model, jumped); model.Step(1f/120); }
                    Assert.That(model.Result, Is.EqualTo(DinoStep.Running), first.Kind + "/" + first.Variant + " → " + second.Kind + "/" + second.Variant);
                    Assert.That(model.Grounded, Is.True); Assert.That(model.Physics.SimulationCount, Is.GreaterThan(360));
                }
                checkedPairs++; yield return null;
            }
            Assert.That(checkedPairs, Is.EqualTo(49));
        }

        [UnityTest] public IEnumerator RealSilhouettes_LeaveCactusBranchAndBirdWingGapsEmpty()
        {
            using (var physics = new DinoPhysics())
            {
                var probeFrame = new DinoFrame { collisionPolygons = new[] { new DinoPolygon { points = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right } } } };
                var probe = physics.Create("Probe", new[] { probeFrame }, new Vector2(2,2), 0);
                var cactus = physics.Create("Cactus", new[] { Art.cactus[2] }, new Vector2(38,56), 1);
                cactus.Place(Vector2.zero); probe.Place(new Vector2(11,35)); physics.Simulate(1f/240);
                Assert.That(probe.Touches(cactus), Is.False, "가지와 줄기 사이 빈 공간");
                probe.Place(new Vector2(19,35)); physics.Simulate(1f/240); Assert.That(probe.Touches(cactus), Is.True);
                cactus.SetActive(false);
                var bird = physics.Create("Bird", Art.bird, new Vector2(40,28), 1); bird.Place(Vector2.zero);
                probe.Place(new Vector2(1,2)); physics.Simulate(1f/240); Assert.That(probe.Touches(bird), Is.False);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ShortStreaks_HighBirdBetweenLargestGroundObstacles()
        {
            // 높은 새 전후에 큰 장애물이 붙어도 같은 점프 기준으로 세 장애물을 모두 피할 수 있어야 합니다.
            var rules = new DinoRules();
            var demanding = new[] { new DinoObstacle { Kind=DinoObstacleKind.Cactus, Variant=2 },
                new DinoObstacle { Kind=DinoObstacleKind.Rock, Variant=1 }, new DinoObstacle { Kind=DinoObstacleKind.LowBird } };
            foreach (var first in demanding) foreach (var last in demanding)
            {
                var course = new[] { new DinoObstacle { Kind=first.Kind, Variant=first.Variant, CourseX=rules.DinosaurFront+rules.speed },
                    new DinoObstacle { Kind=DinoObstacleKind.HighBird, CourseX=rules.DinosaurFront+rules.speed*1.85f },
                    new DinoObstacle { Kind=last.Kind, Variant=last.Variant, CourseX=rules.DinosaurFront+rules.speed*2.7f } };
                using (var model = new DinoModel(rules, Art, course))
                {
                    var jumped = new HashSet<int>();
                    for (int frame=0;frame<240 && model.Result==DinoStep.Running;frame++) { AutoJump(model,jumped); model.Step(1f/60); }
                    Assert.That(model.Result, Is.EqualTo(DinoStep.Running), first.Kind+" → 높은 새 → "+last.Kind);
                    Assert.That(jumped.Count, Is.EqualTo(2)); Assert.That(model.Grounded, Is.True);
                }
                yield return null;
            }
        }

        [UnityTest] public IEnumerator Viewport_KeepsWholeFieldAndRelativeSizes()
        {
            yield return Load(); var game=Object.FindFirstObjectByType<DinoGame>();
            game.KeyboardInputEnabled=false; game.enabled=false; game.StartGame(); Frame(game,0);
            for(int i=0;i<20;i++) Frame(game,.1f);
            var view=game.GetComponent<DinoView>();
            var field=(RectTransform)typeof(DinoView).GetField("field",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            var dinosaur=(Image)typeof(DinoView).GetField("dinosaur",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            var camera=game.GetComponentInChildren<Camera>(); var oldRect=camera.rect;
            var sizes=new[]{new Vector2Int(720,330),new Vector2Int(1280,720),new Vector2Int(720,1280),new Vector2Int(1280,720)};
            for(int index=0;index<sizes.Length;index++)
            {
                var size=sizes[index];
                var target=new RenderTexture(size.x,size.y,24); camera.targetTexture=target;
                camera.rect=index==3 ? new Rect(.15f,.15f,.7f,.7f) : oldRect;
                yield return null; view.Refresh(); Canvas.ForceUpdateCanvases();
                var corners=new Vector3[4]; field.GetWorldCorners(corners);
                var lower=camera.WorldToScreenPoint(corners[0]); var upper=camera.WorldToScreenPoint(corners[2]);
                Assert.That(lower.x,Is.GreaterThanOrEqualTo(camera.pixelRect.xMin-.1f));
                Assert.That(lower.y,Is.GreaterThanOrEqualTo(camera.pixelRect.yMin-.1f));
                Assert.That(upper.x,Is.LessThanOrEqualTo(camera.pixelRect.xMax+.1f));
                Assert.That(upper.y,Is.LessThanOrEqualTo(camera.pixelRect.yMax+.1f));
                Assert.That((upper.x-lower.x)/(upper.y-lower.y),Is.EqualTo(720f/330).Within(.001));
                Assert.That(dinosaur.rectTransform.rect.size,Is.EqualTo(new Vector2(48,56)));
                camera.targetTexture=null; Object.Destroy(target);
            }
            camera.rect=oldRect; Capture(game,"Gameplay");
        }

        [UnityTest] public IEnumerator JumpPhysics_BirdHeightsAndTerminalFreeze()
        {
            using (var model = new DinoModel(new DinoRules(), Art, Array.Empty<DinoObstacle>()))
            {
                model.Jump(); model.Step(1f/3); Assert.That(model.Height, Is.EqualTo(100).Within(.01));
                model.Jump(); model.Step(1f/3 + .001f); Assert.That(model.Height, Is.Zero); Assert.That(model.Velocity, Is.Zero);
                model.Step(.1f); Assert.That(model.Height, Is.Zero, "공중 점프 예약 금지");
            }
            foreach (var kind in new[] { DinoObstacleKind.LowBird, DinoObstacleKind.HighBird })
            {
                using (var model = new DinoModel(new DinoRules(), Art, new[] { new DinoObstacle { Kind = kind, CourseX = 400 } }))
                {
                    model.Step(2);
                    Assert.That(model.Result, Is.EqualTo(kind == DinoObstacleKind.LowBird ? DinoStep.Collision : DinoStep.Running));
                    if (model.Result == DinoStep.Collision)
                    {
                        float distance = model.Distance; int steps = model.Physics.SimulationCount;
                        model.Step(10); Assert.That(model.Distance, Is.EqualTo(distance)); Assert.That(model.Physics.SimulationCount, Is.EqualTo(steps));
                    }
                }
                yield return null;
            }
        }

        [UnityTest] public IEnumerator SavedScene_KeyboardStartJumpPauseHoldAndRetry()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DinoGame>();
            Capture(game, "Ready");
            yield return Keys(Key.Tab); Assert.That(game.State, Is.EqualTo(MiniGameState.Ready));
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Model.Height, Is.Zero, "시작 Space는 점프하지 않습니다.");
            yield return Keys(); yield return Keys(Key.Space); Assert.That(game.Model.Height, Is.GreaterThan(0));
            yield return new WaitForSeconds(.15f); Capture(game, "Jump");
            yield return Keys(Key.Tab); Assert.That(game.State, Is.EqualTo(MiniGameState.Paused));
            float height = game.Model.Height, distance = game.Model.Distance; int steps = game.Model.Physics.SimulationCount;
            yield return Keys(Key.Tab, Key.Space); yield return new WaitForSeconds(.15f);
            Assert.That(game.Model.Height, Is.EqualTo(height)); Assert.That(game.Model.Distance, Is.EqualTo(distance));
            Assert.That(game.Model.Physics.SimulationCount, Is.EqualTo(steps)); Capture(game, "Paused");
            yield return Keys(Key.Space); yield return Keys(Key.Tab, Key.Space); yield return Keys(Key.Space);
            yield return new WaitForSeconds(.75f); Assert.That(game.Model.Grounded, Is.True, "유지한 Space로 착지 후 자동 점프하지 않습니다.");
            game.enabled = false;
            for (int i=0; i<50 && game.State == MiniGameState.Playing; i++) Frame(game,.1f,16);
            Assert.That(game.State, Is.EqualTo(MiniGameState.Failed)); Capture(game, "Failed");
            yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Failed));
            yield return Keys(); yield return Keys(Key.Space); Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
            Assert.That(game.Model.Distance, Is.Zero); Assert.That(game.Model.Height, Is.Zero); Assert.That(game.Model.Velocity, Is.Zero);
        }

        [UnityTest] public IEnumerator SavedCourse_Reaches8000AndFlag_AtDifferentFrameRates()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DinoGame>(); game.KeyboardInputEnabled = false; game.enabled = false;
            foreach (float dt in new[] { 1f/30, 1f/60, 1f/144 })
            {
                game.Settings.randomSeed = 4; game.StartGame(); Frame(game,0);
                var model = game.Model; var jumped = new HashSet<int>(); int resultCount = 0;
                Action<MiniGameResult> handler = _ => resultCount++; game.Completed += handler;
                bool capturedFlag = false;
                for (int i = 0; i < 4500 && game.State == MiniGameState.Playing; i++)
                {
                    AutoJump(model,jumped); Frame(game,dt);
                    if (!capturedFlag && model.FlagX < 650) { Capture(game,"Finish-Approach"); capturedFlag = true; }
                }
                Assert.That(game.State, Is.EqualTo(MiniGameState.Succeeded)); Assert.That(model.Distance, Is.EqualTo(8000));
                Assert.That(model.Progress01, Is.EqualTo(1)); Assert.That(model.Elapsed, Is.EqualTo(8000d/299).Within(.001));
                int simulations = model.Physics.SimulationCount; Frame(game,1,16);
                Assert.That(model.Physics.SimulationCount, Is.EqualTo(simulations)); Assert.That(resultCount, Is.EqualTo(1));
                Capture(game,"Success"); game.Completed -= handler; game.ResetGame(); yield return null;
            }
        }

        [UnityTest] public IEnumerator ExistingManager_ReceivesOneFailureAndFinalSuccess()
        {
            yield return Load(); var game = Object.FindFirstObjectByType<DinoGame>(); game.KeyboardInputEnabled = false; game.enabled = false;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Dino manager host"));
            var type = Type.GetType("MiniGameManager, Assembly-CSharp", true);
            manager = (MonoBehaviour)new GameObject("Dino integration manager").AddComponent(type); manager.enabled = false;
            type.GetField("currentLoadedMiniGame", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,"MiniGame04");
            type.GetField("totalMiniGamesToClear", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,1);
            type.GetMethod("BindMiniGameResult", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{game.gameObject.scene});
            game.StartGame(); Frame(game,0);
            for(int i=0;i<50 && game.State==MiniGameState.Playing;i++) Frame(game,.1f);
            Assert.That((int)type.GetProperty("CurrentLives").GetValue(manager),Is.EqualTo(2));
            type.GetMethod("OnMiniGameFailed").Invoke(manager,null);
            Assert.That((int)type.GetProperty("CurrentLives").GetValue(manager),Is.EqualTo(2));
            game.Settings.randomSeed=4; game.StartGame(); Frame(game,0);
            var jumped=new HashSet<int>();
            for(int i=0;i<2000 && game.State==MiniGameState.Playing;i++){AutoJump(game.Model,jumped);Frame(game,1f/60);}
            Assert.That((int)type.GetProperty("CurrentClearedCount").GetValue(manager),Is.EqualTo(1));
            for(int i=0;i<120 && SceneManager.GetSceneByName("MiniGame04").isLoaded;i++) yield return null;
            Assert.That(SceneManager.GetSceneByName("MiniGame04").isLoaded,Is.False);
        }

        // 화면은 동일한 카메라에서 실제 URP Canvas 렌더로 저장하며 씬/카메라 설정은 복원합니다.
        private static void Capture(DinoGame game, string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            game.GetComponent<DinoView>().Refresh();
            var camera=game.GetComponentInChildren<Camera>(); var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active;
            var target=new RenderTexture(1440,660,24); var pixels=new Texture2D(1440,660,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1440,660),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Dino-"+name+".png",pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.Destroy(target);Object.Destroy(pixels);}
        }
    }
}
#endif
