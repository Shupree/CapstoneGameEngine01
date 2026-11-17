using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 목숨은 매니저의 실제 값을 읽기만 합니다. 차감·게임오버·재시작 책임은 기존 매니저에 남깁니다.
[DefaultExecutionOrder(300)]
public sealed class MainGameArtworkHUD : MonoBehaviour
{
    [SerializeField] private MiniGameManager manager;
    [SerializeField] private RectTransform area;
    [SerializeField] private GameObject lifeRoot;
    [SerializeField] private RawImage[] digits;
    [SerializeField] private Texture[] numbers;
    [SerializeField] private TMP_Text gameOverReason;
    public void Configure(MiniGameManager owner, RectTransform viewport, GameObject lives, RawImage[] images, Texture[] textures, TMP_Text reason)
    { manager=owner; area=viewport; lifeRoot=lives; digits=images; numbers=textures; gameOverReason=reason; }
    private void LateUpdate()
    {
        if(manager==null) return;
        var rect=manager.MiniGameViewportRect; area.anchorMin=rect.min; area.anchorMax=rect.max;
        area.offsetMin=area.offsetMax=Vector2.zero;
        // Shift 숨김 중에는 목숨도 숨깁니다. 게임오버 화면에서는 0을 포함한 최종 값을 표시합니다.
        lifeRoot.SetActive(!manager.IsMiniGameHidden && (manager.IsMiniGameActive || manager.IsGameOver));
        string count=manager.CurrentLives.ToString();
        for(int i=0;i<digits.Length;i++)
        {
            bool active=i<count.Length; digits[i].gameObject.SetActive(active);
            if(active) digits[i].texture=numbers[count[i]-'0'];
        }
        if(manager.IsGameOver) gameOverReason.text=manager.GameOverReason==EGameOverReason.LivesDepleted?"NO LIVES LEFT":"CAUGHT!";
    }
}
