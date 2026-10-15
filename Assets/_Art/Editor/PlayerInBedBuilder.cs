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
    /// 침대에 누운 주인공(1인칭 시점에서 보이는 몸)을 코드로 생성하는 Editor 전용 도구.
    /// - Blanket: 몸 형태(가슴·배·두 다리·발끝)대로 볼록하게 덮인 이불. 침대 커버(bedSingle.fbx의 carpet)와 같은 머티리얼을 쓴다.
    /// - Body: 이불 밖으로 나온 상체와 팔. 파자마(민트 바탕에 빨강 삼각형·노랑 사각형·초록 원) 텍스처와 피부 머티리얼을 쓴다.
    /// 메시·텍스처는 실행할 때마다 다시 만들지만, 머티리얼과 프리팹은 이미 있으면 덮어쓰지 않는다(Inspector 조정값 보존).
    /// 프리팹 조립은 프리뷰 씬에서 하므로 Build 메뉴는 열린 씬을 수정하지 않는다. 씬 배치는 Place 메뉴로 따로 한다.
    /// </summary>
    public static class PlayerInBedBuilder
    {
        private const string BedFbx = "Assets/_Art/Models/bedSingle.fbx";
        private const string MeshAssetPath = "Assets/_Art/Models/PlayerInBed/PlayerInBed_Meshes.asset";
        private const string PajamaTexturePath = "Assets/_Art/Textures/Pajama/T_Pajama_Pattern.png";
        private const string MaterialDir = "Assets/_Art/Materials/Player";
        private const string PrefabPath = "Assets/_Art/Prefabs/PlayerInBed.prefab";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string InstanceName = "PlayerInBed";

        // MainGame의 bedSingle(스케일 0.3) 기준 치수. 프리팹 원점 = 침대 머리 쪽 끝의 가운데 바닥.
        // 로컬 +Z = 머리→발 방향, Y = 바닥 기준 높이. 침대 모델이나 스케일을 바꾸면 이 값도 같이 맞춘다.
        private const float BedLength = 3.38f;
        private const float BedHalfWidth = 0.85f;
        private const float MattressTop = 0.79f;

        // 이불이 시작되는 위치(가슴 중간)와 끝(침대 발치). 이불 위쪽 끝은 흰 띠로 접힌 것처럼 보이게 한다.
        private const float BlanketStart = 1.28f;
        private const float BlanketEnd = 3.36f;
        private const float BlanketLift = 0.012f;

        // 파자마 무늬 한 장이 덮는 실제 크기(m). 씬 전체가 사람 크기보다 약 1.6배 크게 잡혀 있어 그에 맞췄다.
        private const float PajamaTileSize = 0.42f;

        [MenuItem("Tools/Art/Build Player In Bed")]
        public static void Build()
        {
            Texture2D pajamaTexture = BuildPajamaTexture();
            Material pajama = EnsureMaterial("M_Player_Pajama", Color.white, 0.35f, pajamaTexture);
            Material skin = EnsureMaterial("M_Player_Skin", new Color(0.95f, 0.80f, 0.68f), 0.25f, null);
            GetBedCoverMaterials(out Material blanketMain, out Material blanketFold);

            var blanketHeights = new BlanketHeightField();
            Mesh blanketMesh = SaveMesh("PlayerInBed_Blanket", BuildBlanketMesh(blanketHeights));
            Mesh bodyMesh = SaveMesh("PlayerInBed_Body", BuildBodyMesh(blanketHeights));
            AssetDatabase.SaveAssets();

            if (File.Exists(PrefabPath)) return;
            BuildPrefabInPreviewScene(scene =>
            {
                var root = new GameObject(InstanceName);
                SceneManager.MoveGameObjectToScene(root, scene);
                AddRenderer(root.transform, "Blanket", blanketMesh, blanketMain, blanketFold);
                AddRenderer(root.transform, "Body", bodyMesh, pajama, skin);
                return root;
            });
        }

        /// <summary>
        /// 열린 씬의 Map/bedSingle을 찾아 PlayerInBed 프리팹을 침대 위에 배치한다. 이미 있으면 위치만 다시 맞춘다.
        /// 씬은 Dirty로만 표시하고 저장하지 않는다(저장 여부는 작업자가 결정).
        /// </summary>
        [MenuItem("Tools/Art/Place Player In Bed (Active Scene)")]
        public static string PlaceInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform bed = scene.GetRootGameObjects().Select(go => go.transform.Find("bedSingle")).FirstOrDefault(t => t != null);
            if (bed == null) throw new System.InvalidOperationException("활성 씬에서 Map/bedSingle을 찾을 수 없습니다.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new FileNotFoundException($"프리팹이 없습니다. 먼저 Build Player In Bed를 실행하세요: {PrefabPath}");

            // 침대 본체 렌더러의 바운드에서 긴 축을 찾고, 베개가 있는 쪽을 머리로 본다.
            Bounds bounds = bed.GetComponent<Renderer>().bounds;
            Transform pillow = bed.Find("pillow");
            bool alongX = bounds.size.x >= bounds.size.z;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            float headSign = pillow != null && Vector3.Dot(pillow.position - bounds.center, axis) < 0f ? -1f : 1f;
            float length = alongX ? bounds.size.x : bounds.size.z;
            if (Mathf.Abs(length - BedLength) > 0.05f)
            {
                Debug.LogWarning($"[PlayerInBedBuilder] 침대 길이({length:F2})가 생성 기준({BedLength:F2})과 다릅니다. 치수 상수를 확인하세요.");
            }

            Vector3 head = bounds.center + axis * (headSign * length * 0.5f);
            head.y = bed.position.y;
            Quaternion rotation = Quaternion.LookRotation(-axis * headSign, Vector3.up);

            Transform parent = bed.parent;
            Transform existing = parent.Find(InstanceName);
            GameObject instance;
            if (existing != null)
            {
                instance = existing.gameObject;
                Undo.RecordObject(instance.transform, "Place Player In Bed");
            }
            else
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Undo.RegisterCreatedObjectUndo(instance, "Place Player In Bed");
            }
            instance.name = InstanceName;
            instance.transform.SetPositionAndRotation(head, rotation);
            instance.transform.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{InstanceName} placed at {head} yaw {rotation.eulerAngles.y:F1} under {parent.name}";
        }

        // ---------------- 이불 ----------------

        /// <summary>
        /// 이불 높이맵. 이불 아래 몸의 높이(BodyHeight)를 구한 뒤, 천이 몸에서 침대로 비스듬히 늘어지도록
        /// 거리 기반 텐트(경사 DrapeSlope)를 씌우고 살짝 블러한다. 팔을 이불 위에 올릴 때도 이 값을 쓴다.
        /// </summary>
        private sealed class BlanketHeightField
        {
            public const int CountF = 72;
            public const int CountS = 56;
            private const float DrapeSlope = 0.8f;
            private readonly float[,] heights = new float[CountF, CountS];

            public BlanketHeightField()
            {
                var body = new float[CountF, CountS];
                for (int i = 0; i < CountF; i++)
                for (int j = 0; j < CountS; j++)
                {
                    body[i, j] = BodyHeight(F(i), S(j));
                }

                // 텐트: 각 점의 높이 = 주변 몸 높이에서 거리 × 경사를 뺀 값의 최댓값. 다리 사이도 천이 걸쳐진 모양이 된다.
                for (int i = 0; i < CountF; i++)
                for (int j = 0; j < CountS; j++)
                {
                    float best = 0f;
                    for (int k = 0; k < CountF; k++)
                    for (int l = 0; l < CountS; l++)
                    {
                        float b = body[k, l];
                        if (b <= best) continue;
                        float df = F(i) - F(k), ds = S(j) - S(l);
                        best = Mathf.Max(best, b - DrapeSlope * Mathf.Sqrt(df * df + ds * ds));
                    }
                    heights[i, j] = best;
                }

                Blur();
            }

            public static float F(int i) => Mathf.Lerp(BlanketStart, BlanketEnd, i / (CountF - 1f));
            public static float S(int j) => Mathf.Lerp(-BedHalfWidth - 0.01f, BedHalfWidth + 0.01f, j / (CountS - 1f));
            public float At(int i, int j) => heights[i, j];

            /// <summary>로컬 (f, s)에서 이불 윗면의 실제 높이(바닥 기준). 범위 밖이면 가장자리 값을 쓴다.</summary>
            public float SurfaceY(float f, float s)
            {
                float fi = Mathf.Clamp(Mathf.InverseLerp(BlanketStart, BlanketEnd, f) * (CountF - 1), 0f, CountF - 1.001f);
                float sj = Mathf.Clamp(Mathf.InverseLerp(-BedHalfWidth - 0.01f, BedHalfWidth + 0.01f, s) * (CountS - 1), 0f, CountS - 1.001f);
                int i = (int)fi, j = (int)sj;
                float tf = fi - i, ts = sj - j;
                float h = Mathf.Lerp(Mathf.Lerp(heights[i, j], heights[i, j + 1], ts), Mathf.Lerp(heights[i + 1, j], heights[i + 1, j + 1], ts), tf);
                return MattressTop + BlanketLift + h;
            }

            private void Blur()
            {
                var copy = (float[,])heights.Clone();
                for (int i = 0; i < CountF; i++)
                for (int j = 0; j < CountS; j++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= CountF || b >= CountS) continue;
                        sum += copy[a, b];
                        n++;
                    }
                    // 몸보다 낮아지지 않게 해 이불이 상체·다리를 파고들지 않도록 한다.
                    heights[i, j] = Mathf.Max(sum / n, BodyHeight(F(i), S(j)));
                }
            }
        }

        /// <summary>이불 아래 몸의 높이(매트리스 윗면 기준). 상체 + 두 다리 + 위로 선 발끝.</summary>
        private static float BodyHeight(float f, float s)
        {
            float h = TorsoHeight(f) * Bump(s / TorsoWidth(f)) * Window(f, 0.8f, 2.3f, 0.15f);

            // 1인칭 시점에서 가슴 너머로 다리·발끝이 보이도록 실제 체형보다 약간 과장한 높이다.
            float t = Mathf.InverseLerp(2.0f, 2.95f, f);
            float legCenter = Mathf.Lerp(0.15f, 0.11f, t);
            float legRadius = Mathf.Lerp(0.15f, 0.09f, t);
            float legHeight = Mathf.Lerp(0.17f, 0.11f, t);
            float legWindow = Window(f, 1.85f, 3.02f, 0.12f);
            for (int side = -1; side <= 1; side += 2)
            {
                h = Mathf.Max(h, legHeight * Bump((s - side * legCenter) / legRadius) * legWindow);

                float df = (f - 3.02f) / 0.11f, ds = (s - side * 0.12f) / 0.09f;
                float e = df * df + ds * ds;
                if (e < 1f) h = Mathf.Max(h, 0.24f * Mathf.Sqrt(1f - e));
            }
            return h;
        }

        // 상체 높이·반폭 곡선. 노출된 상체(파자마)와 이불 속 상체가 같은 곡선을 써서 이불 가장자리에서 틈이 생기지 않는다.
        private static float TorsoHeight(float f) => Curve(f, (0.80f, 0.08f), (0.95f, 0.13f), (1.2f, 0.14f), (1.6f, 0.16f), (1.95f, 0.17f), (2.3f, 0.12f));
        private static float TorsoWidth(float f) => Curve(f, (0.80f, 0.20f), (0.93f, 0.32f), (1.1f, 0.34f), (1.6f, 0.30f), (2.0f, 0.31f), (2.3f, 0.27f));

        private static MeshData BuildBlanketMesh(BlanketHeightField field)
        {
            var data = new MeshData(2);
            int nf = BlanketHeightField.CountF, ns = BlanketHeightField.CountS;

            // 윗면 격자. UV는 실제 길이(m) 기준이라 텍스처를 넣어도 늘어나지 않는다.
            int start = data.Vertices.Count;
            for (int i = 0; i < nf; i++)
            for (int j = 0; j < ns; j++)
            {
                float f = BlanketHeightField.F(i), s = BlanketHeightField.S(j);
                data.Add(new Vector3(s, MattressTop + BlanketLift + field.At(i, j), f), new Vector2(s, f));
            }
            for (int i = 0; i < nf - 1; i++)
            for (int j = 0; j < ns - 1; j++)
            {
                int a = start + i * ns + j;
                data.Quad(0, a, a + ns, a + ns + 1, a + 1);
            }

            // 옆·발치·위쪽 끝에 아래로 내려오는 치마를 붙여 이불 두께 틈이 보이지 않게 한다.
            const float skirtBottom = MattressTop - 0.05f;
            AddSkirt(data, field, Enumerable.Range(0, nf).Select(i => (i, 0)).ToList());
            AddSkirt(data, field, Enumerable.Range(0, nf).Select(i => (i, ns - 1)).Reverse().ToList());
            AddSkirt(data, field, Enumerable.Range(0, ns).Select(j => (nf - 1, j)).ToList());
            AddSkirt(data, field, Enumerable.Range(0, ns).Select(j => (0, j)).Reverse().ToList());

            void AddSkirt(MeshData d, BlanketHeightField hf, List<(int i, int j)> edge)
            {
                int baseIndex = d.Vertices.Count;
                foreach ((int i, int j) in edge)
                {
                    float f = BlanketHeightField.F(i), s = BlanketHeightField.S(j);
                    float y = MattressTop + BlanketLift + hf.At(i, j);
                    d.Add(new Vector3(s, y, f), new Vector2(s, y));
                    d.Add(new Vector3(s, skirtBottom, f), new Vector2(s, skirtBottom));
                }
                for (int k = 0; k < edge.Count - 1; k++)
                {
                    int a = baseIndex + k * 2;
                    d.Quad(0, a, a + 1, a + 3, a + 2);
                }
            }

            // 이불 위쪽 끝을 따라 흰 띠(커버의 carpetWhite)를 말아 올린 접힌 이불처럼 보이게 한다.
            var edgePath = new List<Vector3>();
            for (int j = 0; j < ns; j++)
            {
                float s = BlanketHeightField.S(j);
                edgePath.Add(new Vector3(s, MattressTop + BlanketLift + field.At(0, j) + 0.012f, BlanketStart + 0.025f));
            }
            AddTube(data, 1, edgePath, _ => 0.034f, 10, PajamaTileSize);
            return data;
        }

        // ---------------- 몸(파자마 상체·팔) ----------------

        private static MeshData BuildBodyMesh(BlanketHeightField field)
        {
            var data = new MeshData(2);
            AddTorso(data);

            for (int side = -1; side <= 1; side += 2)
            {
                // 어깨 → 팔꿈치(이불 위, 몸 옆) → 손목(배 위). 높이는 이불 윗면에 팔 반지름을 더해 얹는다.
                var shoulder = new Vector3(side * 0.29f, MattressTop + 0.09f, 0.98f);
                var elbow = new Vector3(side * 0.42f, field.SurfaceY(1.45f, side * 0.42f) + 0.055f, 1.45f);
                var wrist = new Vector3(side * 0.18f, field.SurfaceY(1.78f, side * 0.18f) + 0.042f, 1.78f);
                List<Vector3> armPath = CatmullRom(new[] { shoulder, elbow, wrist }, 28);

                // 반팔 파자마: 소매(파자마)는 위팔 앞부분만, 나머지 팔은 피부. 소매를 조금 굵게 해 겹치는 부분을 가린다.
                int sleeveEnd = Mathf.RoundToInt(armPath.Count * 0.40f);
                AddTube(data, 0, armPath.Take(sleeveEnd + 1).ToList(), t => 0.068f, 14, PajamaTileSize);
                AddTube(data, 1, armPath.Skip(sleeveEnd - 3).ToList(), t => Mathf.Lerp(0.056f, 0.042f, t), 12, 1f);

                // 손: 손목 끝에서 배 쪽으로 납작한 타원체.
                Vector3 forward = (wrist - elbow).normalized;
                var handCenter = wrist + forward * 0.075f;
                handCenter.y = field.SurfaceY(handCenter.z, handCenter.x) + 0.03f;
                AddEllipsoid(data, 1, handCenter, Quaternion.LookRotation(forward, Vector3.up), new Vector3(0.058f, 0.032f, 0.085f));
            }
            return data;
        }

        /// <summary>
        /// 이불 밖 상체: 목 아래(f 0.80)부터 이불 속(f 1.55)까지 단면이 납작한 슈퍼타원인 몸통을 잇는다.
        /// 아랫면은 매트리스 속으로 살짝 묻어 바닥이 보이지 않게 하고, 목 쪽 끝은 파자마 면으로 막는다.
        /// </summary>
        private static void AddTorso(MeshData data)
        {
            const int rings = 18, segments = 22;
            const float startF = 0.80f, endF = 1.55f, sink = 0.03f, power = 2.4f;
            int start = data.Vertices.Count;
            for (int r = 0; r < rings; r++)
            {
                float f = Mathf.Lerp(startF, endF, r / (rings - 1f));
                float w = TorsoWidth(f), h = TorsoHeight(f) + sink;
                float arc = 0f;
                Vector3 previous = Vector3.zero;
                for (int k = 0; k <= segments; k++)
                {
                    float theta = Mathf.PI * k / segments;
                    float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                    var p = new Vector3(
                        w * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / power),
                        MattressTop - sink + h * Mathf.Pow(s, 2f / power),
                        f);
                    if (k > 0) arc += Vector3.Distance(p, previous);
                    previous = p;
                    data.Add(p, new Vector2(arc, f) / PajamaTileSize);
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings - 1; r++)
            for (int k = 0; k < segments; k++)
            {
                int a = start + r * stride + k;
                data.Quad(0, a, a + 1, a + stride + 1, a + stride);
            }

            // 목 쪽 마개: 첫 단면 둘레를 중심점으로 부채꼴 연결.
            int center = data.Vertices.Count;
            data.Add(new Vector3(0f, MattressTop + TorsoHeight(startF) * 0.45f, startF), new Vector2(0f, startF) / PajamaTileSize);
            for (int k = 0; k < segments; k++)
            {
                data.Triangle(0, center, start + k + 1, start + k);
            }
        }

        // ---------------- 파자마 텍스처 ----------------

        /// <summary>
        /// 민트 바탕에 둥근 빨강 삼각형, 노랑 사각형, 초록 원을 흩뿌린 반복(타일) 텍스처를 그려 PNG로 저장한다.
        /// 지터 격자 + 고정 시드라 매번 같은 무늬가 나오며, 도형은 가장자리를 감싸 그려 이음매가 없다.
        /// </summary>
        private static Texture2D BuildPajamaTexture()
        {
            const int size = 512, cells = 4;
            var background = new Color32(172, 228, 220, 255);
            Color[] palette =
            {
                new Color32(238, 92, 96, 255),   // 빨강 삼각형
                new Color32(248, 210, 70, 255),  // 노랑 사각형
                new Color32(126, 204, 108, 255), // 초록 원
            };

            var pixels = Enumerable.Repeat((Color)background, size * size).ToArray();
            var random = new System.Random(20261015);
            float cell = size / (float)cells;
            int shapeIndex = 0;
            for (int cy = 0; cy < cells; cy++)
            for (int cx = 0; cx < cells; cx++)
            {
                // 한 칸에 도형 두 개(반 칸 어긋나게)를 둬 사진처럼 촘촘하고 고르게 흩어지게 한다.
                for (int n = 0; n < 2; n++)
                {
                    int type = (shapeIndex++ + random.Next(2)) % 3;
                    float x = (cx + 0.25f + 0.5f * n + (float)(random.NextDouble() - 0.5) * 0.30f) * cell;
                    float y = (cy + 0.25f + 0.5f * ((n + cx) % 2) + (float)(random.NextDouble() - 0.5) * 0.30f) * cell;
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float radius = cell * (type == 2 ? 0.12f : 0.14f);
                    DrawShape(pixels, size, type, new Vector2(x, y), radius, angle, palette[type]);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            EnsureFolder(Path.GetDirectoryName(PajamaTexturePath).Replace('\\', '/'));
            File.WriteAllBytes(PajamaTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PajamaTexturePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PajamaTexturePath);
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PajamaTexturePath);
        }

        /// <summary>부호 거리 함수로 도형 하나를 안티에일리어싱해 그린다. 텍스처 경계를 넘으면 반대편에 이어 그린다.</summary>
        private static void DrawShape(Color[] pixels, int size, int type, Vector2 center, float radius, float angle, Color color)
        {
            int extent = Mathf.CeilToInt(radius * 1.6f) + 2;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            for (int py = -extent; py <= extent; py++)
            for (int px = -extent; px <= extent; px++)
            {
                int x = Mathf.FloorToInt(center.x) + px, y = Mathf.FloorToInt(center.y) + py;
                var local = new Vector2(x + 0.5f - center.x, y + 0.5f - center.y);
                var p = new Vector2(cos * local.x + sin * local.y, -sin * local.x + cos * local.y);
                float distance = type switch
                {
                    0 => RoundedTriangle(p, radius),
                    1 => RoundedSquare(p, radius * 0.78f, radius * 0.25f),
                    _ => p.magnitude - radius,
                };
                float coverage = Mathf.Clamp01(0.5f - distance);
                if (coverage <= 0f) continue;
                int index = ((y % size + size) % size) * size + ((x % size + size) % size);
                pixels[index] = Color.Lerp(pixels[index], color, coverage);
            }
        }

        private static float RoundedSquare(Vector2 p, float halfSize, float round)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * (halfSize - round);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
        }

        // 정삼각형 SDF(Inigo Quilez)에 반지름만큼 둥글림을 더한다.
        private static float RoundedTriangle(Vector2 p, float radius)
        {
            float round = radius * 0.22f, r = radius - round;
            const float k = 1.7320508f;
            p.x = Mathf.Abs(p.x) - r;
            p.y += r / k;
            if (p.x + k * p.y > 0f) p = new Vector2(p.x - k * p.y, -k * p.x - p.y) * 0.5f;
            p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
            return -p.magnitude * Mathf.Sign(p.y) - round;
        }

        // ---------------- 메시 도우미 ----------------

        /// <summary>서브메시별 삼각형을 모으는 간단한 메시 버퍼. 서브메시 순서 = 렌더러 머티리얼 순서.</summary>
        private sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int>[] Triangles;

            public MeshData(int subMeshCount)
            {
                Triangles = Enumerable.Range(0, subMeshCount).Select(_ => new List<int>()).ToArray();
            }

            public void Add(Vector3 position, Vector2 uv)
            {
                Vertices.Add(position);
                Uvs.Add(uv);
            }

            public void Triangle(int subMesh, int a, int b, int c) => Triangles[subMesh].AddRange(new[] { a, b, c });

            // 시계 방향(a→b→c→d)으로 넘기면 Unity 기준 앞면이 된다.
            public void Quad(int subMesh, int a, int b, int c, int d)
            {
                Triangle(subMesh, a, b, c);
                Triangle(subMesh, a, c, d);
            }
        }

        /// <summary>
        /// 경로를 따라 원형 단면 튜브를 만든다(평행 이동 프레임으로 비틀림 방지). 양 끝은 막는다.
        /// UV는 둘레·길이를 실제 길이로 재고 tileSize로 나눠 파자마 무늬 크기가 일정하게 한다.
        /// </summary>
        private static void AddTube(MeshData data, int subMesh, List<Vector3> path, System.Func<float, float> radiusAt, int segments, float tileSize)
        {
            int start = data.Vertices.Count;
            Vector3 tangent = (path[1] - path[0]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(tangent, Vector3.up).normalized : Vector3.right;
            float length = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 next = i < path.Count - 1 ? (path[i + 1] - path[i]).normalized : tangent;
                Vector3 newTangent = i == 0 ? tangent : ((path[i] - path[i - 1]).normalized + next).normalized;
                normal = Vector3.ProjectOnPlane(normal, newTangent).normalized;
                tangent = newTangent;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                if (i > 0) length += Vector3.Distance(path[i], path[i - 1]);

                float radius = radiusAt(i / (path.Count - 1f));
                for (int k = 0; k <= segments; k++)
                {
                    float a = Mathf.PI * 2f * k / segments;
                    Vector3 offset = (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * radius;
                    data.Add(path[i] + offset, new Vector2(Mathf.PI * 2f * radius * k / segments, length) / tileSize);
                }
            }

            int stride = segments + 1;
            for (int i = 0; i < path.Count - 1; i++)
            for (int k = 0; k < segments; k++)
            {
                int a = start + i * stride + k;
                data.Quad(subMesh, a, a + 1, a + stride + 1, a + stride);
            }

            // 양 끝 마개.
            for (int end = 0; end < 2; end++)
            {
                int ring = end == 0 ? start : start + (path.Count - 1) * stride;
                int center = data.Vertices.Count;
                data.Add(path[end == 0 ? 0 : path.Count - 1], Vector2.zero);
                for (int k = 0; k < segments; k++)
                {
                    if (end == 0) data.Triangle(subMesh, center, ring + k + 1, ring + k);
                    else data.Triangle(subMesh, center, ring + k, ring + k + 1);
                }
            }
        }

        private static void AddEllipsoid(MeshData data, int subMesh, Vector3 center, Quaternion rotation, Vector3 radii)
        {
            const int u = 14, v = 8;
            int start = data.Vertices.Count;
            for (int iv = 0; iv <= v; iv++)
            for (int iu = 0; iu <= u; iu++)
            {
                float phi = Mathf.PI * iv / v, theta = Mathf.PI * 2f * iu / u;
                var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                data.Add(center + rotation * Vector3.Scale(unit, radii), new Vector2(iu / (float)u, iv / (float)v));
            }
            for (int iv = 0; iv < v; iv++)
            for (int iu = 0; iu < u; iu++)
            {
                int a = start + iv * (u + 1) + iu;
                data.Quad(subMesh, a, a + 1, a + u + 2, a + u + 1);
            }
        }

        private static List<Vector3> CatmullRom(Vector3[] points, int samples)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < samples; i++)
            {
                float t = i / (samples - 1f) * (points.Length - 1);
                int seg = Mathf.Min((int)t, points.Length - 2);
                float u = t - seg;
                Vector3 p0 = points[Mathf.Max(seg - 1, 0)], p1 = points[seg], p2 = points[seg + 1], p3 = points[Mathf.Min(seg + 2, points.Length - 1)];
                result.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u));
            }
            return result;
        }

        private static float Bump(float u)
        {
            u = Mathf.Abs(u);
            if (u >= 1f) return 0f;
            float t = 1f - u * u;
            return t * t;
        }

        private static float Window(float f, float a, float b, float fade) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - a) / fade)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((b - f) / fade));

        private static float Curve(float x, params (float x, float y)[] keys)
        {
            if (x <= keys[0].x) return keys[0].y;
            for (int i = 1; i < keys.Length; i++)
            {
                if (x <= keys[i].x) return Mathf.SmoothStep(keys[i - 1].y, keys[i].y, Mathf.InverseLerp(keys[i - 1].x, keys[i].x, x));
            }
            return keys[keys.Length - 1].y;
        }

        // ---------------- 에셋 저장 ----------------

        /// <summary>
        /// 메시를 하나의 .asset 파일 안에 이름별 서브 에셋으로 저장한다.
        /// 이미 있으면 같은 Mesh 객체의 내용만 바꿔 프리팹·씬의 참조(GUID/fileID)가 유지되게 한다.
        /// </summary>
        private static Mesh SaveMesh(string name, MeshData data)
        {
            EnsureFolder(Path.GetDirectoryName(MeshAssetPath).Replace('\\', '/'));
            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(MeshAssetPath).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };

            mesh.Clear();
            mesh.SetVertices(data.Vertices);
            mesh.SetUVs(0, data.Uvs);
            mesh.subMeshCount = data.Triangles.Length;
            for (int i = 0; i < data.Triangles.Length; i++) mesh.SetTriangles(data.Triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            if (isNew)
            {
                if (File.Exists(MeshAssetPath)) AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
                else AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            }
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>없을 때만 URP Lit 머티리얼을 만든다. 이미 있으면 Inspector에서 바꾼 값을 그대로 쓴다.</summary>
        private static Material EnsureMaterial(string name, Color color, float smoothness, Texture texture)
        {
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader lit = Shader.Find(LitShaderName);
            if (lit == null) throw new System.InvalidOperationException($"셰이더를 찾을 수 없습니다: {LitShaderName}");
            material = new Material(lit) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 이불 색을 침대 커버와 똑같이 맞추려고 bedSingle.fbx의 cover 렌더러가 쓰는 머티리얼(carpet, carpetWhite)을 그대로 참조한다.
        /// FBX 리맵으로 커버 머티리얼을 바꾸면 이불도 같이 바뀐다.
        /// </summary>
        private static void GetBedCoverMaterials(out Material main, out Material fold)
        {
            var bed = AssetDatabase.LoadAssetAtPath<GameObject>(BedFbx);
            Renderer cover = bed != null ? bed.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "cover") : null;
            Material[] materials = cover != null ? cover.sharedMaterials : new Material[0];
            main = materials.FirstOrDefault(m => m != null && m.name == "carpet");
            fold = materials.FirstOrDefault(m => m != null && m.name == "carpetWhite");
            if (main == null || fold == null)
            {
                throw new System.InvalidOperationException($"{BedFbx}의 cover 머티리얼(carpet, carpetWhite)을 찾을 수 없습니다.");
            }
        }

        private static void AddRenderer(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        /// <summary>열린 씬을 건드리지 않도록 프리뷰 씬에서 프리팹을 조립·저장한다.</summary>
        private static void BuildPrefabInPreviewScene(System.Func<Scene, GameObject> build)
        {
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = build(preview);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success) Debug.LogError($"[PlayerInBedBuilder] 프리팹 저장 실패: {PrefabPath}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
