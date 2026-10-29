using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GE.MiniGames.Dino
{
    // 전역 Physics2D/Time 설정을 변경하지 않는 수동 로컬 물리 공간입니다. 미니게임의 틱에서만 Simulate합니다.
    public sealed class DinoPhysics : IDisposable
    {
        public const float UnitsPerPixel = 0.01f;
        private readonly Scene scene;
        private readonly PhysicsScene2D physics;
        private readonly List<Actor> actors = new List<Actor>();
        public int SimulationCount { get; private set; }
        public string SceneName => scene.name;

        public DinoPhysics()
        {
            scene = SceneManager.CreateScene("Dino physics " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
        }

        public Actor Create(string label, DinoFrame[] frames, Vector2 size, float inset)
        {
            var root = new GameObject(label);
            SceneManager.MoveGameObjectToScene(root, scene);
            var actor = new Actor(root, frames, size, inset);
            actors.Add(actor);
            return actor;
        }
        public void Simulate(float dt) { physics.Simulate(dt); SimulationCount++; }
        public void Dispose()
        {
            // 언로드 완료 전에도 기존 판정 오브젝트를 즉시 꺼 재시도와 중복되지 않도록 합니다.
            foreach (var actor in actors) actor.SetActive(false);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }

        public sealed class Actor
        {
            private readonly GameObject root;
            private readonly Rigidbody2D body;
            private readonly PolygonCollider2D[] colliders;
            private readonly Vector2[][][] paths;
            private int frame = -1;

            internal Actor(GameObject root, DinoFrame[] frames, Vector2 size, float inset)
            {
                this.root = root;
                body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0; body.interpolation = RigidbodyInterpolation2D.None;
                paths = new Vector2[frames.Length][][];
                int maximum = 0;
                for (int f = 0; f < frames.Length; f++)
                {
                    var shapes = frames[f].Shapes();
                    paths[f] = new Vector2[shapes.Length][];
                    maximum = Mathf.Max(maximum, shapes.Length);
                    for (int p = 0; p < shapes.Length; p++)
                    {
                        var source = shapes[p].points;
                        Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
                        foreach (var point in source) { min = Vector2.Min(min, point * size); max = Vector2.Max(max, point * size); }
                        Vector2 center = (min + max) * 0.5f;
                        Vector2 factor = new Vector2(Mathf.Max(0.1f, 1 - 2 * inset / (max.x - min.x)), Mathf.Max(0.1f, 1 - 2 * inset / (max.y - min.y)));
                        paths[f][p] = new Vector2[source.Length];
                        for (int i = 0; i < source.Length; i++) paths[f][p][i] = (center + (source[i] * size - center) * factor) * UnitsPerPixel;
                    }
                }
                if (maximum == 0) throw new ArgumentException(root.name + ": 충돌 실루엣이 없습니다. Sprite Physics Shape를 설정하세요.");
                colliders = new PolygonCollider2D[maximum];
                for (int p = 0; p < maximum; p++) { colliders[p] = root.AddComponent<PolygonCollider2D>(); colliders[p].isTrigger = true; }
                SetFrame(0);
            }
            public void Place(Vector2 bottomLeft) { body.position = bottomLeft * UnitsPerPixel; }
            public void SetActive(bool active) { if (root != null) root.SetActive(active); }
            public void SetFrame(int index)
            {
                index %= paths.Length;
                if (frame == index) return;
                frame = index;
                for (int p = 0; p < colliders.Length; p++)
                {
                    colliders[p].enabled = p < paths[index].Length;
                    if (colliders[p].enabled) colliders[p].SetPath(0, paths[index][p]);
                }
            }
            // AABB는 빠른 후보 검사에만 쓰고, 최종 판정은 Unity의 실제 다각형 거리 검사에 맡깁니다.
            public bool Touches(Actor other)
            {
                foreach (var a in colliders) foreach (var b in other.colliders)
                    if (a.enabled && b.enabled && a.bounds.Intersects(b.bounds) && a.Distance(b).isOverlapped) return true;
                return false;
            }
        }
    }
}
