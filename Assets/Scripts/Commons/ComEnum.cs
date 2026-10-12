public enum ESceneType
{
    NONE = -1,
    Lobby,
    MainGame,
    MiniGame,
    End
}

// 패배 원인과 실제 종료 처리를 분리합니다. 새 패배 조건도 매니저의 TriggerGameOver로 전달합니다.
public enum EGameOverReason
{
    None,
    LivesDepleted,
    CaughtByMother
}
