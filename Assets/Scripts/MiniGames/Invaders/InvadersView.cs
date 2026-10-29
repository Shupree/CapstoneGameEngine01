using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Invaders
{
    // 시작·정지·재시도 화면은 기존 MiniGameTestUI가 담당합니다. 여기서는 저장된 게임 화면만 갱신합니다.
    [DefaultExecutionOrder(100)]
    public sealed class InvadersView : MonoBehaviour
    {
        [SerializeField] private InvadersGame game;
        [SerializeField] private Image player, bulletTemplate;
        [SerializeField] private Image[] enemies, shields;
        [SerializeField] private RectTransform bulletLayer, progressFill;
        [SerializeField] private TMP_Text progress, lives;
        private readonly List<Image> bulletImages = new List<Image>();

        public void Configure(InvadersGame controller, Image ship, Image[] fleet, Image[] bunkers,
            Image bullet, RectTransform layer, TMP_Text counter, TMP_Text health, RectTransform fill)
        {
            game = controller; player = ship; enemies = fleet; shields = bunkers; bulletTemplate = bullet;
            bulletLayer = layer; progress = counter; lives = health; progressFill = fill;
        }
        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (game == null || game.Model == null || player == null) return;
            var model = game.Model;
            Place(player, new Vector2(model.PlayerX, InvadersRules.PlayerY), new Vector2(InvadersRules.PlayerWidth, InvadersRules.PlayerHeight));
            // 기본 내부 목숨은 1개라 피격 즉시 실패합니다. 생존한 경우에만 모델 시간으로 무적 점멸을 표시합니다.
            player.enabled = !model.Invulnerable || model.Result != InvadersStep.Running || (int)(model.Elapsed * 10) % 2 == 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].enabled = model.Enemies[i].Alive;
                Place(enemies[i], model.Enemies[i].Position, new Vector2(InvadersRules.EnemyWidth, InvadersRules.EnemyHeight));
            }
            for (int i = 0; i < shields.Length; i++)
            {
                // 기존 씬에 저장된 여분의 조각은 숨겨, 씬 수정 없이 방어막당 6개만 표시합니다.
                if (i >= model.Shields.Count) { shields[i].enabled = false; continue; }
                shields[i].enabled = model.Shields[i].Alive;
                // 원래 크기로 복구한 충돌 조각과 표시 크기를 같은 상수로 맞춥니다.
                Place(shields[i], model.Shields[i].Position, Vector2.one * InvadersRules.ShieldPieceSize);
            }
            // 표시 탄환만 재사용하며 충돌·발사 주기는 모델에서 처리합니다. 공통 상태 패널 아래 전용 계층에 둡니다.
            for (int i = 0; i < model.Bullets.Count; i++)
            {
                if (i == bulletImages.Count)
                {
                    var image = Instantiate(bulletTemplate, bulletLayer);
                    image.name = "Bullet " + i; image.gameObject.SetActive(true); bulletImages.Add(image);
                }
                var bullet = model.Bullets[i]; var visual = bulletImages[i]; visual.enabled = true;
                visual.color = bullet.FromPlayer ? new Color32(153, 246, 228, 255) : new Color32(251, 113, 133, 255);
                Place(visual, bullet.Position, new Vector2(InvadersRules.BulletWidth, InvadersRules.BulletHeight));
            }
            for (int i = model.Bullets.Count; i < bulletImages.Count; i++) bulletImages[i].enabled = false;
            progress.text = $"처치 {game.Progress} / {game.Target}"; lives.text = $"목숨 {model.Lives}";
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1);
        }

        private static void Place(Image image, Vector2 center, Vector2 size)
        {
            image.rectTransform.anchoredPosition = new Vector2(center.x - InvadersRules.Width / 2, InvadersRules.Height / 2 - center.y);
            image.rectTransform.sizeDelta = size;
        }
    }
}
