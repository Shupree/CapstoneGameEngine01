using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TouchPrompt : MonoBehaviour
{
    [SerializeField] private Text promptText;
    [SerializeField] private GameObject loadingGauge;
    [SerializeField] private Image loadingFill;
    [SerializeField] private float fakeLoadDuration = 1.5f;

    void Start()
    {
        if (promptText != null)
            promptText.text = MenuManager.Instance.GetCurrentSceneName() switch
            {
                "MainGame" => "플레이 중",
                _ => "프로세싱..."
            };

        StartCoroutine(FakeLoad());
    }

    void Update()
    {
        if (loadingGauge != null && loadingGauge.activeSelf)
            return;

        // 씬 전환 신호 발송
        if (Input.GetMouseButtonDown(0))
            MenuManager.Instance.NextScene();
    }

    // 로딩 게이지 표시
    IEnumerator FakeLoad()
    {
        if (loadingGauge != null)
            loadingGauge.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fakeLoadDuration)
        {
            elapsed += Time.deltaTime;

            if (loadingFill != null)
                loadingFill.fillAmount = Mathf.Clamp01(elapsed / fakeLoadDuration);

            yield return null;
        }

        if (loadingGauge != null)
            loadingGauge.SetActive(false);

        if (promptText != null)
            promptText.text = MenuManager.Instance.GetCurrentSceneName() switch
            {
                "MainGame" => "플레이가 종료되었습니다\n터치하면 다음 신으로!",
                _ => "터치하면 다음 신으로!"
            };
    }
}
