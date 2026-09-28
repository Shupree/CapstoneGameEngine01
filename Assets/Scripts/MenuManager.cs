using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // 싱글턴 인스턴스
    public static MenuManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 현재 씬 이름 가져오기
    public string GetCurrentSceneName() => SceneManager.GetActiveScene().name;

    // 씬 전환
    public void NextScene()
    {
        SceneManager.LoadScene(GetCurrentSceneName() switch
        {
            "Lobby" => "MainGame",
            "MainGame" => "Lobby",
            _ => "Lobby"
        });
    }
}

