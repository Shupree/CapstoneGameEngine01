using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GE.Art.Editor
{
    /// <summary>
    /// 창밖 밤 풍경을 만드는 Editor 전용 도구.
    /// 방 카메라 위치에서 각 창(Map/wallWindow*)을 통해 보이는 방향에 두 장의 배경판을 세운다.
    /// - Sky: 멀리(35m) 있는 밤하늘 + 산 능선 + 아파트 실루엣(일부 창에 불). 달은 달빛(Base/Directional Light)이 오는 쪽에 그린다.
    /// - Trees: 가까이(14m) 있는 나무·전봇대·전선 실루엣(알파 컷아웃).
    /// 배경판은 조명을 받지 않는 Unlit이고 그림자를 드리우지 않는다(달빛 그림자를 가리지 않게).
    /// 카메라는 회전만 하고 위치는 고정이므로, 창을 통해 보이는 범위는 카메라 위치와 창 크기로 정해진다.
    /// 카메라 위치나 창 위치를 바꾸면 이 메뉴를 다시 실행한다. 엄마 창문 등장 지점(창밖 약 2m)보다 훨씬 멀리 둔다.
    /// 텍스처·머티리얼은 실행할 때마다 다시 만들고, 씬 오브젝트는 Map/NightView 아래에서 재사용한다(저장은 하지 않음).
    /// </summary>
    public static class NightViewBuilder
    {
        private const string TextureDir = "Assets/_Art/Textures/NightView";
        private const string MaterialDir = "Assets/_Art/Materials/NightView";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string RootName = "NightView";

        // 배경판이 덮는 각도 범위(카메라 기준). 창 너머 시야가 이 안에 들어오는지 실행 시 검사한다.
        private const float SkyDistance = 35f;
        private const float SkyHalfAngle = 35f;
        private const float SkyMinElevation = -10f;
        private const float SkyMaxElevation = 72f;
        private const float TreesDistance = 14f;
        private const float TreesHalfAngle = 40f;
        private const float TreesMinElevation = -12f;
        private const float TreesMaxElevation = 32f;

        private const int SkyWidth = 1024;
        private const int SkyHeight = 2048;
        private const int TreesWidth = 1024;
        private const int TreesHeight = 640;

        [MenuItem("Tools/Art/Build Night Window Views (Active Scene)")]
        public static string BuildInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            Camera camera = all.Select(t => t.GetComponent<Camera>()).FirstOrDefault(c => c != null && c.name == "Main Camera");
            if (camera == null) throw new System.InvalidOperationException("활성 씬에서 Main Camera를 찾을 수 없습니다.");
            Transform map = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Map");
            if (map == null) throw new System.InvalidOperationException("활성 씬에서 Map을 찾을 수 없습니다.");
            Light moonLight = all.Select(t => t.GetComponent<Light>()).FirstOrDefault(l => l != null && l.type == LightType.Directional);

            // 창: Map 바로 아래의 wallWindow* 중 유리(windowBottom/windowTop)가 있는 것.
            var windows = map.Cast<Transform>().Where(t => t.name.StartsWith("wallWindow") && t.Find("windowBottom") != null).ToList();
            if (windows.Count == 0) throw new System.InvalidOperationException("Map 아래에서 유리가 있는 창(wallWindow*)을 찾을 수 없습니다.");

            Texture2D treesTexture = SaveTexture(PaintTrees(), $"{TextureDir}/T_NightTrees.png", true);
            Material treesMaterial = EnsureUnlitMaterial("M_NightTrees", treesTexture, true);

            Transform root = map.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Night Window Views");
                root.SetParent(map, false);
            }

            Vector3 eye = camera.transform.position;
            var report = new List<string>();
            foreach (Transform window in windows)
            {
                Bounds aperture = GlassBounds(window);
                Vector3 toWindow = aperture.center - eye;
                toWindow.y = 0f;
                Vector3 forward = toWindow.normalized;
                Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

                // 달이 이 창 방향에서 좌우·위아래 몇 도에 있는지. 범위 밖이면 가장자리 쪽 하늘만 밝아진다.
                Vector2 moon = moonLight != null ? AngleOf(-moonLight.transform.forward, facing) : new Vector2(60f, 20f);
                string key = Sanitize(window.name);
                // 창마다 건물 배치가 다르되 다시 실행해도 같은 그림이 나오도록 이름에서 고정 시드를 만든다.
                int seed = key.Aggregate(17, (hash, ch) => hash * 31 + ch);
                Texture2D skyTexture = SaveTexture(PaintSky(moon, seed), $"{TextureDir}/T_NightSky_{key}.png", false);
                Material skyMaterial = EnsureUnlitMaterial($"M_NightSky_{key}", skyTexture, false);

                PlaceBoard(root, $"{key}_Sky", eye, facing, SkyDistance, SkyHalfAngle, SkyMinElevation, SkyMaxElevation, skyMaterial);
                PlaceBoard(root, $"{key}_Trees", eye, facing, TreesDistance, TreesHalfAngle, TreesMinElevation, TreesMaxElevation, treesMaterial);
                report.Add($"{window.name}: {CheckCoverage(aperture, eye, facing)}, moon at ({moon.x:F0}°, {moon.y:F0}°)");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            return string.Join("\n", report);
        }

        // ---------------- 배치 ----------------

        /// <summary>
        /// 카메라에서 forward 방향으로 distance만큼 떨어진 수직 평면에 배경판(Quad)을 세운다.
        /// 판의 가로는 ±halfAngle, 세로는 minElevation~maxElevation(카메라 눈높이 기준) 범위를 덮는다.
        /// 텍스처도 같은 각도 대응으로 그리므로, 창을 통해 보이는 하늘·지평선 높이가 실제 각도와 맞는다.
        /// </summary>
        private static void PlaceBoard(Transform root, string name, Vector3 eye, Quaternion facing, float distance,
            float halfAngle, float minElevation, float maxElevation, Material material)
        {
            Transform board = root.Find(name);
            if (board == null)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(go, "Build Night Window Views");
                board = go.transform;
                board.SetParent(root, false);
            }

            float bottom = distance * Mathf.Tan(minElevation * Mathf.Deg2Rad);
            float top = distance * Mathf.Tan(maxElevation * Mathf.Deg2Rad);
            float width = 2f * distance * Mathf.Tan(halfAngle * Mathf.Deg2Rad);
            Vector3 forward = facing * Vector3.forward;

            Undo.RecordObject(board, "Build Night Window Views");
            board.SetPositionAndRotation(eye + forward * distance + Vector3.up * ((top + bottom) * 0.5f), facing);
            board.localScale = new Vector3(width, top - bottom, 1f);

            board.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = board.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // 창의 유리 두 장을 합친 범위가 창 너머로 보이는 구멍이다.
        private static Bounds GlassBounds(Transform window)
        {
            var renderers = new[] { window.Find("windowBottom"), window.Find("windowTop") }
                .Where(t => t != null).Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToList();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        // 창 구멍의 모서리를 하늘 판의 각도로 옮겨 판 안에 들어오는지 확인한다.
        private static string CheckCoverage(Bounds aperture, Vector3 eye, Quaternion facing)
        {
            float minA = float.MaxValue, maxA = float.MinValue, minE = float.MaxValue, maxE = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? aperture.min.x : aperture.max.x, (i & 2) == 0 ? aperture.min.y : aperture.max.y, (i & 4) == 0 ? aperture.min.z : aperture.max.z);
                Vector2 angle = AngleOf(corner - eye, facing);
                minA = Mathf.Min(minA, angle.x); maxA = Mathf.Max(maxA, angle.x);
                minE = Mathf.Min(minE, angle.y); maxE = Mathf.Max(maxE, angle.y);
            }
            bool covered = minA > -SkyHalfAngle && maxA < SkyHalfAngle && minE > SkyMinElevation && maxE < SkyMaxElevation;
            if (!covered) Debug.LogWarning($"[NightViewBuilder] 창 너머 시야({minA:F0}~{maxA:F0}°, {minE:F0}~{maxE:F0}°)가 하늘 판 범위를 벗어납니다. 각도 상수를 늘리세요.");
            return $"view {minA:F0}~{maxA:F0}° / elevation {minE:F0}~{maxE:F0}° covered={covered}";
        }

        /// <summary>direction을 facing 기준 (좌우 각, 위아래 각)으로 바꾼다. 오른쪽·위가 +.</summary>
        private static Vector2 AngleOf(Vector3 direction, Quaternion facing)
        {
            Vector3 local = Quaternion.Inverse(facing) * direction;
            float horizontal = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Atan2(local.y, Mathf.Sqrt(local.x * local.x + local.z * local.z)) * Mathf.Rad2Deg;
            return new Vector2(horizontal, elevation);
        }

        // ---------------- 하늘 텍스처 ----------------

        /// <summary>
        /// 밤하늘 판. 픽셀 위치를 판 위의 각도로 바꿔 그린다(가로 ±SkyHalfAngle, 세로 SkyMinElevation~SkyMaxElevation).
        /// 위에서부터: 짙은 남색 하늘과 별 → 지평선 쪽 도시 불빛 번짐 → 먼 산 능선 → 아파트 실루엣과 드문드문 켜진 창 → 어두운 땅.
        /// 색은 RetroScreen(색 수 제한·디더링)을 거친 뒤에도 구분되도록 조금 밝게 잡았다.
        /// </summary>
        private static Color[] PaintSky(Vector2 moon, int seed)
        {
            var random = new System.Random(seed);
            var pixels = new Color[SkyWidth * SkyHeight];
            float tanMin = Mathf.Tan(SkyMinElevation * Mathf.Deg2Rad), tanMax = Mathf.Tan(SkyMaxElevation * Mathf.Deg2Rad);
            float tanHalf = Mathf.Tan(SkyHalfAngle * Mathf.Deg2Rad);

            // 반투명 유리(알파 0.5)가 배경을 절반 정도로 어둡게 하므로, 방보다 살짝 밝은 푸른 창으로 보이게 두 배 정도 밝게 잡았다.
            var zenith = new Color(0.050f, 0.070f, 0.160f);
            var upper = new Color(0.085f, 0.115f, 0.240f);
            var horizon = new Color(0.180f, 0.190f, 0.310f);
            var cityGlow = new Color(0.340f, 0.230f, 0.220f);
            var moonGlow = new Color(0.220f, 0.260f, 0.380f);
            var mountainColor = new Color(0.045f, 0.055f, 0.100f);
            var groundColor = new Color(0.015f, 0.016f, 0.026f);

            // 아파트·주택 실루엣: 좌우 각도 구간마다 높이(지평선 위 각도)가 다른 건물.
            List<Building> buildings = MakeBuildings(random);
            // 별: 각도 공간에서 균일하게 흩뿌린 점(대부분 1px, 일부 밝은 별은 십자).
            var stars = Enumerable.Range(0, 900).Select(_ => new Vector3(
                (float)random.NextDouble() * SkyWidth, (float)random.NextDouble() * SkyHeight, (float)random.NextDouble())).ToList();

            for (int py = 0; py < SkyHeight; py++)
            {
                float elevation = Mathf.Atan(Mathf.Lerp(tanMin, tanMax, (py + 0.5f) / SkyHeight)) * Mathf.Rad2Deg;
                for (int px = 0; px < SkyWidth; px++)
                {
                    float horizontal = Mathf.Atan(Mathf.Lerp(-tanHalf, tanHalf, (px + 0.5f) / SkyWidth)) * Mathf.Rad2Deg;
                    Color c;
                    if (elevation < -0.6f)
                    {
                        c = groundColor;
                    }
                    else
                    {
                        float h = Mathf.Clamp01(elevation / 60f);
                        c = h < 0.25f ? Color.Lerp(horizon, upper, h / 0.25f) : Color.Lerp(upper, zenith, (h - 0.25f) / 0.75f);
                        c += cityGlow * Mathf.Exp(-Mathf.Max(0f, elevation) / 5f) * 0.55f;
                        float moonDistance = Vector2.Distance(new Vector2(horizontal, elevation), moon);
                        c += moonGlow * Mathf.Exp(-moonDistance / 22f) * 0.8f;
                        if (moonDistance < 1.6f) c = Color.Lerp(c, new Color(0.85f, 0.88f, 0.95f), Mathf.Clamp01((1.6f - moonDistance) / 0.25f));

                        float ridge = 1.6f + 1.8f * Mathf.PerlinNoise(horizontal * 0.05f + seed * 0.001f, 0.3f) + 1.2f * Mathf.PerlinNoise(horizontal * 0.21f, 1.7f);
                        if (elevation < ridge) c = mountainColor;
                        foreach (Building b in buildings)
                        {
                            if (horizontal < b.Left || horizontal > b.Right || elevation > b.Top) continue;
                            c = b.Color;
                            if (b.IsLit(horizontal, elevation, out Color light)) c = light;
                            break;
                        }
                    }
                    pixels[py * SkyWidth + px] = c;
                }
            }

            foreach (Vector3 star in stars)
            {
                int x = (int)star.x, y = (int)star.y;
                float elevation = Mathf.Atan(Mathf.Lerp(tanMin, tanMax, (y + 0.5f) / SkyHeight)) * Mathf.Rad2Deg;
                // 가장 높은 건물(11°)·산 능선(약 5°) 위에서만 별을 그리고, 지평선 쪽은 도시 불빛 때문에 흐리게 한다.
                if (elevation < 13f) continue;
                float brightness = Mathf.Lerp(0.35f, 1f, star.z * star.z) * Mathf.Clamp01((elevation - 13f) / 10f);
                var starColor = Color.Lerp(new Color(0.75f, 0.82f, 1f), new Color(1f, 0.92f, 0.8f), star.z) * brightness;
                Blend(pixels, x, y, starColor, 1f);
                if (star.z > 0.93f)
                {
                    Blend(pixels, x + 1, y, starColor, 0.5f); Blend(pixels, x - 1, y, starColor, 0.5f);
                    Blend(pixels, x, y + 1, starColor, 0.5f); Blend(pixels, x, y - 1, starColor, 0.5f);
                }
            }
            return pixels;
        }

        private static void Blend(Color[] pixels, int x, int y, Color color, float amount)
        {
            if (x < 0 || y < 0 || x >= SkyWidth || y >= SkyHeight) return;
            int i = y * SkyWidth + x;
            pixels[i] = pixels[i] + color * amount;
        }

        /// <summary>먼 아파트 단지와 주택 실루엣. 좌우 각도 구간·높이·창 격자를 무작위로 정한다(밤늦은 시간이라 불 켜진 창은 일부).</summary>
        private static List<Building> MakeBuildings(System.Random random)
        {
            var list = new List<Building>();
            float x = -SkyHalfAngle - 2f;
            while (x < SkyHalfAngle + 2f)
            {
                bool apartment = random.NextDouble() < 0.55;
                float width = apartment ? Mathf.Lerp(4f, 9f, (float)random.NextDouble()) : Mathf.Lerp(1.5f, 3.5f, (float)random.NextDouble());
                float top = apartment ? Mathf.Lerp(5f, 11f, (float)random.NextDouble()) : Mathf.Lerp(2.2f, 4f, (float)random.NextDouble());
                float shade = Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());
                list.Add(new Building
                {
                    Left = x, Right = x + width, Top = top,
                    Color = new Color(0.030f, 0.036f, 0.062f) * shade,
                    Columns = apartment ? Mathf.Max(3, Mathf.RoundToInt(width / 0.9f)) : 2,
                    RowHeight = apartment ? 0.55f : 0.8f,
                    LitChance = apartment ? 0.13f : 0.2f,
                    Seed = random.Next(),
                });
                x += width + (float)random.NextDouble() * (apartment ? 2.5f : 1f);
            }
            // 높은 건물이 앞을 가리지 않게 낮은 건물부터 검사한다(그림 순서 = 앞쪽).
            return list.OrderBy(b => b.Top).ToList();
        }

        private sealed class Building
        {
            public float Left, Right, Top, RowHeight, LitChance;
            public int Columns, Seed;
            public Color Color;

            /// <summary>창 격자에서 이 위치가 불 켜진 창이면 색을 돌려준다. 따뜻한 전등빛이 대부분이고 일부는 푸른 TV·형광등 빛.</summary>
            public bool IsLit(float horizontal, float elevation, out Color light)
            {
                light = default;
                float u = (horizontal - Left) / (Right - Left) * Columns;
                float v = elevation / RowHeight;
                int column = Mathf.FloorToInt(u), row = Mathf.FloorToInt(v);
                if (row < 1 || elevation > Top - RowHeight * 0.6f) return false;
                float fu = u - column, fv = v - row;
                if (fu < 0.3f || fu > 0.75f || fv < 0.3f || fv > 0.75f) return false;
                int hash = (column * 73856093) ^ (row * 19349663) ^ Seed;
                float roll = (hash & 0xffff) / 65535f;
                if (roll > LitChance) return false;
                light = roll < LitChance * 0.75f ? new Color(1f, 0.78f, 0.42f) * 0.85f : new Color(0.6f, 0.78f, 1f) * 0.7f;
                return true;
            }
        }

        // ---------------- 나무·전봇대 텍스처 ----------------

        /// <summary>
        /// 가까운 실루엣 판(알파 컷아웃). 판 위 좌표(거리 1 기준의 tan 값)로 그린다.
        /// 땅 높이는 카메라 눈높이(약 1.1m) 아래이므로 거리 14m에서 약 -4.6°다. 나무는 좌우 가장자리, 전봇대와 전선은 가운데 오른쪽.
        /// </summary>
        private static Color[] PaintTrees()
        {
            var pixels = new Color[TreesWidth * TreesHeight];
            float tanMin = Mathf.Tan(TreesMinElevation * Mathf.Deg2Rad), tanMax = Mathf.Tan(TreesMaxElevation * Mathf.Deg2Rad);
            float tanHalf = Mathf.Tan(TreesHalfAngle * Mathf.Deg2Rad);
            float ground = -1.1f / TreesDistance;
            var silhouette = new Color(0.010f, 0.012f, 0.022f, 1f);
            var random = new System.Random(4242);

            // 나무 수관: 여러 원을 겹친 덩어리.
            var blobs = new List<Vector3>();
            foreach (float cx in new[] { -0.72f, -0.55f, 0.62f, 0.78f })
            {
                float height = Mathf.Lerp(0.28f, 0.42f, (float)random.NextDouble());
                for (int i = 0; i < 14; i++)
                {
                    blobs.Add(new Vector3(
                        cx + ((float)random.NextDouble() - 0.5f) * 0.18f,
                        ground + height * Mathf.Lerp(0.45f, 1f, (float)random.NextDouble()),
                        Mathf.Lerp(0.035f, 0.075f, (float)random.NextDouble())));
                }
            }
            const float poleX = 0.30f, poleTop = 0.62f;

            for (int py = 0; py < TreesHeight; py++)
            {
                float Y = Mathf.Lerp(tanMin, tanMax, (py + 0.5f) / TreesHeight);
                for (int px = 0; px < TreesWidth; px++)
                {
                    float X = Mathf.Lerp(-tanHalf, tanHalf, (px + 0.5f) / TreesWidth);
                    bool solid = Y < ground + 0.015f * Mathf.PerlinNoise(X * 30f, 0.5f);
                    if (!solid) solid = blobs.Any(b => (new Vector2(X, Y) - new Vector2(b.x, b.y)).sqrMagnitude < b.z * b.z);
                    // 나무 줄기
                    if (!solid) solid = Y < ground + 0.2f && new[] { -0.72f, -0.55f, 0.62f, 0.78f }.Any(cx => Mathf.Abs(X - cx) < 0.006f);
                    // 전봇대·가로대
                    if (!solid) solid = (Mathf.Abs(X - poleX) < 0.005f && Y < poleTop) || (Mathf.Abs(Y - (poleTop - 0.03f)) < 0.004f && Mathf.Abs(X - poleX) < 0.05f);
                    // 전선 두 가닥(처진 곡선)
                    if (!solid)
                    {
                        for (int w = 0; w < 2; w++)
                        {
                            float anchor = poleTop - 0.03f - w * 0.012f;
                            float span = X < poleX ? (X - poleX) / (-tanHalf - poleX) : (X - poleX) / (tanHalf - poleX);
                            float sag = anchor - 0.09f * Mathf.Sin(Mathf.Clamp01(span) * Mathf.PI) + (X < poleX ? -0.04f : 0.02f) * span;
                            if (Mathf.Abs(Y - sag) < 0.0016f) { solid = true; break; }
                        }
                    }
                    pixels[py * TreesWidth + px] = solid ? silhouette : new Color(0f, 0f, 0f, 0f);
                }
            }
            return pixels;
        }

        // ---------------- 에셋 저장 ----------------

        private static Texture2D SaveTexture(Color[] pixels, string path, bool alpha)
        {
            int width = alpha ? TreesWidth : SkyWidth, height = alpha ? TreesHeight : SkyHeight;
            var texture = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, true);
            // 위 색들은 화면에 보일 최종(sRGB) 값으로 잡았으므로 그대로 PNG에 쓴다.
            texture.SetPixels(pixels.Select(c => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), alpha ? c.a : 1f)).ToArray());
            texture.Apply();
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = alpha;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>URP Unlit 머티리얼. 없으면 만들고, 있으면 텍스처만 다시 연결한다(Inspector에서 바꾼 색은 유지).</summary>
        private static Material EnsureUnlitMaterial(string name, Texture2D texture, bool cutout)
        {
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader unlit = Shader.Find(UnlitShaderName);
                if (unlit == null) throw new System.InvalidOperationException($"셰이더를 찾을 수 없습니다: {UnlitShaderName}");
                material = new Material(unlit) { name = name };
                material.SetColor("_BaseColor", Color.white);
                if (cutout)
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                }
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string Sanitize(string name)
        {
            string cleaned = new string(name.Where(char.IsLetterOrDigit).ToArray());
            return string.IsNullOrEmpty(cleaned) ? "Window" : char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1);
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
