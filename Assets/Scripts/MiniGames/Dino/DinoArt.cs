using System;
using System.Collections.Generic;
using UnityEngine;

namespace GE.MiniGames.Dino
{
    [Serializable] public sealed class DinoPolygon { public Vector2[] points; }
    [Serializable] public sealed class DinoFrame
    {
        public Sprite sprite;
        [Tooltip("0~1 좌표의 실루엣. 비우면 교체한 Sprite의 Physics Shape를 사용합니다.")]
        public DinoPolygon[] collisionPolygons;

        // 임시 그림과 판정은 같은 도형을 사용합니다. 새 에셋은 Sprite Editor의 Custom Physics Shape로 교체할 수 있습니다.
        public DinoPolygon[] Shapes()
        {
            if (collisionPolygons != null && collisionPolygons.Length > 0) return collisionPolygons;
            if (sprite == null) return Array.Empty<DinoPolygon>();
            var shapes = new DinoPolygon[sprite.GetPhysicsShapeCount()];
            var points = new List<Vector2>();
            for (int i = 0; i < shapes.Length; i++)
            {
                sprite.GetPhysicsShape(i, points);
                var normalized = new Vector2[points.Count];
                for (int j = 0; j < points.Count; j++)
                    normalized[j] = (points[j] * sprite.pixelsPerUnit + sprite.pivot) / sprite.rect.size;
                shapes[i] = new DinoPolygon { points = normalized };
            }
            return shapes;
        }
    }

    // 씬의 이 에셋 참조만 교체하면 표시와 판정 실루엣이 함께 바뀝니다. 크기·속도는 DinoRules에서 조정합니다.
    [CreateAssetMenu(menuName = "Mini Games/Dino Art")]
    public sealed class DinoArt : ScriptableObject
    {
        public DinoFrame[] dinosaurRun, bird;
        public DinoFrame dinosaurJump;
        public DinoFrame[] cactus, rocks;
        public Sprite ground, flag;
        public DinoFrame[] Frames(DinoObstacleKind kind, int variant) => kind == DinoObstacleKind.Cactus
            ? new[] { cactus[variant % cactus.Length] } : kind == DinoObstacleKind.Rock
            ? new[] { rocks[variant % rocks.Length] } : bird;
    }
}
