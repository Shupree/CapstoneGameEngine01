using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PageBase : MonoBehaviour
{
    [SerializeField] protected GameObject _loadingGauge;
    [SerializeField] protected Image _loadingFill;
    [SerializeField] protected TMP_Text _promptText;
    [SerializeField] protected TextMeshProUGUI _sceneText;
    [SerializeField] protected TextMeshProUGUI _buttonCaption;
    [SerializeField] protected ESceneType _currentScene;
    [SerializeField] protected float _fakeLoadDuration = 1.5f;

    protected bool isLoading;

    protected virtual void Start()
    {
        MenuManager.Singleton.currentScene = _currentScene;
        SetSceneName();
        SetPrompt(_currentScene == ESceneType.MainGame ? "플레이 중" : $"{_currentScene} 신에서 프로세스 진행 중입니다");
        SetButtonCaption($"{MenuManager.Singleton.GetCurrentScene().ToString()}");

        StartCoroutine(FakeLoad());
    }

    void SetButtonCaption(string caption) => _buttonCaption.text = MenuManager.Singleton.GetNextScene().ToString();

    void SetPrompt(string desc)
    {
        if (_promptText != null)
            _promptText.text = desc;
    }

    void SetSceneName() => _sceneText.text = $"Scene: {_currentScene.ToString()}";

    protected virtual IEnumerator FakeLoad()
    {
        isLoading = true;

        if (_loadingGauge != null)
            _loadingGauge.SetActive(true);

        float elapsed = 0f;
        while (elapsed < _fakeLoadDuration)
        {
            elapsed += Time.deltaTime;

            if (_loadingFill != null)
                _loadingFill.fillAmount = Mathf.Clamp01(elapsed / _fakeLoadDuration);

            yield return null;
        }

        if (_loadingGauge != null)
            _loadingGauge.SetActive(false);

        SetPrompt(_currentScene == ESceneType.MainGame ? "플레이가 종료되었습니다" : "진행 프로세스가 완료되었습니다");
        isLoading = false;
    }

    public virtual void OnActionButtonClick()
    {
        Debug.Log("dd");
        if (isLoading)
            return;

        MenuManager.Singleton.NextScene();
    }
}
