using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames.Cross
{
    // 게임판만 세로로 스크롤합니다. 시작·재시도·정지 화면은 기존 MiniGameTestUI가 고정된 화면에서 담당합니다.
    [DefaultExecutionOrder(100)]
    public sealed class CrossView : MonoBehaviour
    {
        [SerializeField] private CrossGame game;
        [SerializeField] private Image player;
        [SerializeField] private Image[] cars;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private RectTransform field, courseRoot;

        // 기존 씬의 차량 이미지를 재사용하며, 늘어난 차선에 필요한 슬롯만 첫 표시/재시도 때 보충합니다.
        public void Configure(CrossGame controller, Image character, Image[] vehicles, TMP_Text counter, RectTransform fill)
        { game = controller; player = character; cars = vehicles; progress = counter; progressFill = fill; }

        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (game == null || game.Model == null || player == null) return;
            var model = game.Model;
            EnsureCourseView();
            EnsureCarCapacity(model.Cars.Count);
            // 플레이어가 화면 위로 진행하면 게임판만 따라 움직입니다. 좌표를 모델에서 계산하므로 정지 시 멈추고 재시도 시 복귀합니다.
            float viewportHeight = field.rect.height;
            float cameraTop = Mathf.Clamp(model.PlayerY + CrossModel.PlayerSize / 2 - viewportHeight * .65f,
                0, Mathf.Max(0, CrossModel.Height - viewportHeight));
            courseRoot.anchoredPosition = new Vector2(0, cameraTop + (viewportHeight - CrossModel.Height) / 2);
            Place(player, model.PlayerX, model.PlayerY, CrossModel.PlayerSize, CrossModel.PlayerSize);
            for (int i = 0; i < cars.Length; i++)
            {
                bool visible = i < model.Cars.Count;
                cars[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var car = model.Cars[i];
                Place(cars[i], car.X, car.Row * CrossModel.CellSize + (CrossModel.CellSize - CrossModel.CarHeight) / 2,
                    CrossModel.CarWidth, CrossModel.CarHeight);
            }
            progress.text = $"{game.Progress:00} / {game.Target:00}";
            progressFill.anchorMax = new Vector2((float)game.Progress / game.Target, 1);
        }

        // 씬 파일을 수정하지 않고 기존 안전지대·차선 그림을 복제해 긴 코스를 구성합니다.
        // 공통 상태 패널은 field 아래에 그대로 두고, 게임 오브젝트만 새 스크롤 계층으로 옮깁니다.
        private void EnsureCourseView()
        {
            if (courseRoot != null) return;
            field = (RectTransform)player.transform.parent;
            Image goalZone = null, startZone = null;
            RectTransform divider = null;
            TMP_Text zoneLabel = null;
            var oldScenery = new List<Transform>();
            foreach (Transform child in field)
            {
                if (child.name.StartsWith("Goal safe zone")) goalZone = child.GetComponent<Image>();
                else if (child.name.StartsWith("Start safe zone")) startZone = child.GetComponent<Image>();
                else if (child.name.StartsWith("Lane divider")) divider = (RectTransform)child;
                else if (child.name == "Goal label") zoneLabel = child.GetComponent<TMP_Text>();
                else continue;
                oldScenery.Add(child);
            }

            courseRoot = new GameObject("Scrolling course", typeof(RectTransform)).GetComponent<RectTransform>();
            courseRoot.gameObject.layer = field.gameObject.layer;
            courseRoot.SetParent(field, false);
            courseRoot.anchorMin = courseRoot.anchorMax = courseRoot.pivot = Vector2.one * .5f;
            courseRoot.sizeDelta = new Vector2(CrossModel.Width, CrossModel.Height);
            courseRoot.SetAsFirstSibling();
            for (int row = 0; row < CrossModel.Rows; row++)
            {
                if (CrossModel.IsSafeRow(row))
                {
                    var template = row == CrossModel.Rows - 1 ? startZone : goalZone;
                    if (template != null)
                    {
                        var zone = Instantiate(template, courseRoot);
                        zone.name = "Safe zone " + (CrossModel.Rows - 1 - row);
                        zone.gameObject.SetActive(true);
                        Place(zone, 0, row * CrossModel.CellSize, CrossModel.Width, CrossModel.CellSize);
                    }
                    if (zoneLabel != null && row != CrossModel.Rows - 1)
                    {
                        var label = Instantiate(zoneLabel, courseRoot);
                        label.gameObject.SetActive(true);
                        label.text = $"SAFE ZONE  {CrossModel.Rows - 1 - row} / {CrossModel.RoadRows}";
                        label.rectTransform.anchoredPosition = new Vector2(0, CrossModel.Height / 2f - (row + .5f) * CrossModel.CellSize);
                    }
                }
                else if (row > 0 && !CrossModel.IsSafeRow(row - 1) && divider != null)
                {
                    var line = Instantiate(divider, courseRoot);
                    line.name = "Lane divider " + row;
                    line.gameObject.SetActive(true);
                    line.anchoredPosition = new Vector2(0, CrossModel.Height / 2f - row * CrossModel.CellSize);
                }
            }
            foreach (var scenery in oldScenery) scenery.gameObject.SetActive(false);
            foreach (var car in cars) car.transform.SetParent(courseRoot, false);
            player.transform.SetParent(courseRoot, false);
            // 플레이 화면에 저장된 예전 목표 안내도 개별 View에서 갱신합니다.
            var goalText = field.parent.Find("Goal")?.GetComponent<TMP_Text>();
            if (goalText != null) goalText.text = $"{game.Target}개 차선 건너기   /   TAB  일시정지";
        }

        // 차량 수가 기존 씬의 20개 슬롯보다 많아도 보이지 않는 차량 판정이 생기지 않도록 표시 슬롯을 늘립니다.
        private void EnsureCarCapacity(int count)
        {
            if (cars.Length >= count) return;
            var expanded = new Image[count];
            cars.CopyTo(expanded, 0);
            for (int i = cars.Length; i < count; i++)
            {
                expanded[i] = Instantiate(cars[0], courseRoot);
                expanded[i].name = "Car " + (i + 1) + " - replaceable sprite";
                expanded[i].transform.SetSiblingIndex(player.transform.GetSiblingIndex());
            }
            cars = expanded;
        }

        // 전체 코스의 좌상단 좌표를 스크롤 계층 중심 좌표로 변환합니다. 충돌 좌표는 카메라 이동의 영향을 받지 않습니다.
        private static void Place(Image image, float x, float y, float width, float height)
        {
            image.rectTransform.anchoredPosition = new Vector2(x + width / 2 - CrossModel.Width / 2f,
                CrossModel.Height / 2f - y - height / 2);
            image.rectTransform.sizeDelta = new Vector2(width, height);
        }
    }
}
