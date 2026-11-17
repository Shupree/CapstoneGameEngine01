using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GE.MiniGames
{
    // 결과 판정에는 관여하지 않고 기존 상태를 그림으로 표시합니다. 기존 UI의 R 입력 경로는 유지합니다.
    [DefaultExecutionOrder(300)]
    public sealed class MiniGameArtworkUI : MonoBehaviour
    {
        [SerializeField] private MiniGameController game;
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup legacy;
        [SerializeField] private RawImage artwork;
        [SerializeField] private Texture pass, fail, pause;
        [SerializeField] private Button retry;
        [SerializeField] private TMP_Text reason, hint;
        public void Configure(MiniGameController owner, Canvas surface, GameObject root, CanvasGroup oldOverlay,
            RawImage image, Texture success, Texture failure, Texture paused, Button retryButton, TMP_Text detail, TMP_Text prompt)
        {
            game=owner; canvas=surface; panel=root; legacy=oldOverlay; artwork=image;
            pass=success; fail=failure; pause=paused; retry=retryButton; reason=detail; hint=prompt;
        }
        private void OnEnable() { if(retry!=null) retry.onClick.AddListener(Retry); }
        private void OnDisable()
        {
            if(retry!=null) retry.onClick.RemoveListener(Retry);
            if(legacy!=null) { legacy.alpha=1; legacy.blocksRaycasts=true; }
        }
        // 버튼 클릭도 기존 R 재시도와 같은 컨트롤러 진입점을 사용합니다. 숨김·전환 중 입력은 받지 않습니다.
        private void Retry()
        {
            if(game!=null && canvas.enabled && game.KeyboardInputEnabled &&
                (game.State==MiniGameState.Failed || game.State==MiniGameState.Succeeded)) game.StartGame();
        }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if(game==null || panel==null) return;
            var state=game.State;
            bool result=state==MiniGameState.Failed || state==MiniGameState.Succeeded;
            bool localPause=state==MiniGameState.Paused && !game.ExternalPauseControl;
            bool visible=result || localPause;
            panel.SetActive(visible);
            if(legacy!=null) { legacy.alpha=visible?0:1; legacy.blocksRaycasts=!visible; }
            if(!visible) return;
            artwork.texture=state==MiniGameState.Succeeded?pass:state==MiniGameState.Failed?fail:pause;
            // 원본 종횡비를 유지하고 고정 표시 영역 안에 맞춥니다.
            var texture=artwork.texture;
            if(texture!=null) artwork.rectTransform.sizeDelta=new Vector2(280,280f*texture.height/texture.width);
            reason.text=result?game.ResultReason:"";
            bool canRetry=result && game.KeyboardInputEnabled;
            retry.gameObject.SetActive(canRetry); retry.interactable=canRetry;
            hint.text=localPause?"TAB":canRetry?"R / CLICK":"";
        }
    }
}
