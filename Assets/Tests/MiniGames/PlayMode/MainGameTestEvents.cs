using System.Collections;
using System.Reflection;
using UnityEngine;

namespace GE.MiniGames.PlayModeTests
{
    // 연출 종류를 검증하는 테스트에서만 실행 인스턴스의 가중치를 고정합니다. 제품 씬의 확률은 수정하지 않습니다.
    internal static class MainGameTestEvents
    {
        public static IList Entries(MonoBehaviour director) => (IList)director.GetType()
            .GetField("eventEntries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);

        public static Object Event(object entry) => entry.GetType().GetProperty("Event").GetValue(entry) as Object;

        public static void SetWeight(object entry, int weight) => entry.GetType()
            .GetField("weight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry, weight);

        public static void UseOnly(MonoBehaviour director, Object selected)
        {
            foreach (var entry in Entries(director))
                if (entry != null) SetWeight(entry, Event(entry) == selected ? 1 : 0);
        }
    }
}
