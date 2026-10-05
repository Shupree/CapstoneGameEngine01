using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : SingletonMono<MenuManager>
{
    public ESceneType currentScene { get; set; } = ESceneType.NONE;

    public string GetCurrentSceneName() => SceneManager.GetActiveScene().name;

    public ESceneType GetCurrentScene() => currentScene;

    public ESceneType GetNextScene()
    {
        return currentScene switch
        {
            ESceneType.Lobby => ESceneType.MainGame,
            _ => ESceneType.NONE            // 게임 화면 -> 로비 화면으로 돌아가는 경우 일시 제거
        };
    }

    // 다음 씬 전환
    public void NextScene()
    {
        Debug.Log(GetNextScene());  //

        if (GetNextScene() == ESceneType.NONE)
        {
            return;
        }

        SceneManager.LoadScene(GetNextScene().ToString());
    }
}
