using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Bomb
{
    // 저장된 그림만 갱신합니다. 절단은 중앙 조각 숨김으로 표시하며 별도 애니메이션이나 타이머를 사용하지 않습니다.
    [DefaultExecutionOrder(100)]
    public sealed class BombView : MonoBehaviour
    {
        [SerializeField] private BombGame game;
        [SerializeField] private Image[] markers, wireTops, wireMiddles, wireBottoms;
        [SerializeField] private TMP_Text[] markerLetters, wireLetters;
        [SerializeField] private RectTransform cursor, progressFill;
        [SerializeField] private TMP_Text timer, progress;
        [SerializeField] private Image failureTint;
        private static readonly Color[] Colors = {
            new Color32(231, 76, 60, 255), new Color32(52, 152, 219, 255),
            new Color32(241, 196, 15, 255), new Color32(46, 204, 113, 255) };
        private static readonly string[] Letters = { "R", "B", "Y", "G" };

        // 교체 가능한 Image/TMP 참조를 씬 빌더에서 연결합니다. 색상 이외에도 문자를 함께 제공해 순서를 구분합니다.
        public void Configure(BombGame controller, Image[] targets, TMP_Text[] targetLetters, Image[] tops, Image[] middles,
            Image[] bottoms, TMP_Text[] letters, RectTransform selection, TMP_Text clock, TMP_Text counter, RectTransform fill, Image tint)
        {
            game = controller; markers = targets; markerLetters = targetLetters; wireTops = tops; wireMiddles = middles;
            wireBottoms = bottoms; wireLetters = letters; cursor = selection; timer = clock; progress = counter; progressFill = fill; failureTint = tint;
        }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (game == null || game.Model == null) return;
            var model = game.Model;
            for (int i = 0; i < BombModel.WireCount; i++)
            {
                int target = (int)model.Sequence[i], wire = (int)model.Wires[i];
                markers[i].color = i < model.CutCount ? new Color32(149, 165, 166, 255) : Colors[target];
                markerLetters[i].text = i < model.CutCount ? "-" : Letters[target];
                wireTops[i].color = wireMiddles[i].color = wireBottoms[i].color = Colors[wire];
                wireMiddles[i].enabled = !model.CutWires[i];
                wireLetters[i].text = Letters[wire];
            }
            cursor.anchoredPosition = new Vector2(-100 + model.SelectedIndex * 70, -50);
            cursor.gameObject.SetActive(game.State == MiniGameState.Playing);
            // 표시할 때만 소수를 올려 남은 시간이 있는데 00:00으로 보이는 것을 막습니다.
            int centiseconds = (int)Math.Ceiling(model.TimeLeft * 100);
            timer.text = $"{centiseconds / 100:00}:{centiseconds % 100:00}";
            progress.text = $"절단 {game.Progress} / {game.Target}";
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1);
            failureTint.enabled = game.State == MiniGameState.Failed;
        }
    }
}
