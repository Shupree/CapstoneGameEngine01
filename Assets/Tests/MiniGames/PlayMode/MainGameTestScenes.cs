using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GE.MiniGames.PlayModeTests
{
    // 제품 씬 목록은 보존하고 테스트 실행 인스턴스만 제한합니다. 새 미니게임 추가로 무작위 선택이 흔들리지 않게 합니다.
    internal static class MainGameTestScenes
    {
        public static void UseSnakeAndSecondGame(MonoBehaviour manager)
        {
            Set(manager, "miniGameSceneNames", new List<string> { "MiniGame01", "MiniGame02" });
            Set(manager, "remainingMiniGames", new List<string> { "MiniGame01", "MiniGame02" });
        }

        public static void Load(MonoBehaviour manager, string sceneName)
        {
            Set(manager, "remainingMiniGames", new List<string> { sceneName });
            manager.GetType().GetMethod("LoadRandomMiniGame").Invoke(manager, null);
        }

        private static void Set(MonoBehaviour manager, string name, List<string> value) =>
            manager.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, value);
    }
}
