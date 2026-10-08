# 미니게임 01 — 스네이크

첫 검토 대상인 스네이크만 구현했습니다. 나머지 6개는 스네이크 검토 후 진행합니다.

메인 개발자 전달용 기존/신규 함수 구분, 호출 위치 및 성공·실패 흐름은
[`DEVELOPER_HANDOFF.md`](DEVELOPER_HANDOFF.md)에 정리했습니다.

## 실행

1. Unity **6000.3.23f1**에서 프로젝트를 엽니다.
2. `Assets/Scenes/MiniGame01.unity`를 열고 Play를 누릅니다.
3. Game 창에 포커스를 주고 **Spacebar**로 시작합니다.
4. **WASD**로 방향을 바꾸고 **Tab**으로 일시정지·재개합니다.
5. 성공·실패 화면에서 Spacebar를 놓고 다시 누르면 새 게임을 시작합니다.

이미 구성된 씬은 바로 실행할 수 있습니다. 씬 구성 도구는
`Tools > Mini Games > 01 Snake > Open or Create Scene`입니다.
완성된 씬에서는 기존 Inspector 설정을 덮어쓰지 않고 씬을 열고 검증합니다.
`Validate Scene` 메뉴는 카메라·Canvas·컨트롤러·스크립트 참조를 검사합니다.

## 규칙과 임시 설정

- 10 × 10칸, 0.25초마다 한 칸 이동, 사과 5개 획득 시 즉시 성공.
- 벽 또는 몸과 충돌하면 즉시 실패. 이동할 때 비워지는 꼬리 칸에는 진입 가능.
- 사과는 빈칸에만 생성. 사과 한 개당 몸이 한 칸 성장.
- 정반대 방향으로 즉시 회전할 수 없으며 이동 한 번당 방향 전환 한 번만 예약.
- **자료에 없는 임시 설정**: 초기 길이 3칸, 머리 (5, 5), 오른쪽 출발,
  빈칸 중 균등 무작위 사과, 재시도마다 새로운 무작위 배치, 색상·화면 배치.
- `MiniGame01 - Snake` 오브젝트의 `SnakeGame` Inspector에서 보드 크기,
  이동 주기, 사과 목표, 초기 길이, 난수 시드를 조절합니다.
  `Random Seed = -1`은 매번 무작위, 0 이상은 같은 배치를 재현합니다.

별도 프로토타입·기획 문서나 `AGENTS.md`는 프로젝트에서 발견되지 않았습니다.
현재 구현은 사용자 메시지의 스네이크 규칙에 근거합니다.

## 구성

- `Assets/Scripts/MiniGames/Common`: 공통 상태·외부 API·결과 이벤트·테스트 UI.
- `Assets/Scripts/MiniGames/Snake`: 격자 규칙, 게임 제어, uGUI 표시.
- `Assets/Scripts/MiniGames/Editor`: 기존 테스트 씬을 구성하고 검사하는 도구.
- `Assets/MiniGames/Art/SnakeUI SDF.asset`: 기존 프로젝트의 한국어 TTF로 만든 전용 정적 UI 글꼴.
- `Assets/Tests/MiniGames`: EditMode 규칙/생명주기 테스트와 실제 씬 PlayMode 테스트.

기존 Input System의 `Keyboard.current`, uGUI/TMP, URP를 사용합니다.
렌더 파이프라인·패키지·Build Settings는 바꾸지 않습니다.
기존 MiniGame01의 카메라와 Canvas를 재사용하고 씬 GUID를 유지합니다.
임시 PageMiniGame 프리팹 인스턴스, EventSystem, TestObject, 조명은 해당 씬에서
비활성화합니다. 기존 프리팹·스크립트 원본은 그대로 보존합니다.

## MainGame 연결 계약

`SnakeGame`은 `IMiniGame`을 구현합니다.

```csharp
// 로드된 미니게임 씬 내부에서 찾은 컴포넌트를 보관합니다.
IMiniGame game = snakeGame;
game.Completed += OnCompleted;
game.StartGame();
game.PauseGame();
game.ResumeGame();
game.ResetGame(); // 시작 대기 상태와 초기 보드로 복원
game.StopGame();  // 상태/모델/예약 입력/타이머 정리; 결과 이벤트 없음

// 씬을 전환하기 전 호스트 측 구독도 해제합니다.
game.Completed -= OnCompleted;
```

`Completed`는 매 실행마다 성공 또는 실패 시 **한 번** 발생하며
`GameId`, `RunId`, `Outcome`, `Progress`, `Target`, `Reason`을 전달합니다.
성공·실패 판정은 `MiniGameTestUI`와 독립적입니다.
호스트 UI를 사용할 때는 `MiniGameTestUI.SetStandaloneControls(false)`와
`SetOverlaysVisible(false)`를 호출하거나 해당 컴포넌트를 비활성화합니다.
`SnakeGame.Model`은 진단/표시 용도로만 읽고 직접 Step/Reset을 호출하지 않습니다.

Tab은 이 컨트롤러만 정지시키며 `Time.timeScale`을 바꾸지 않습니다.
정지 중 타이머는 누적되지 않고, 예약 방향을 비우며, 정지 중 누른 키는
놓은 뒤 다시 눌러야 처리됩니다. 이동 주기의 남은 시간은 재개 시 유지됩니다.
포커스를 잃으면 일시정지하며 Tab으로 재개합니다.
씬 루트 비활성화는 현재 보드를 유지하고 재활성화 첫 프레임의 시간 누적을 건너뜁니다.
전역 싱글턴·정적 이벤트·물리 오브젝트·코루틴을 새로 만들지 않습니다.

**MainGame 클리어 연결:** `MiniGameSceneManager`는 자신이 Additive로 불러온
씬의 `MiniGameController.Completed`를 자동 구독합니다. 성공하면 기존
`OnMiniGameCleared()` → 클리어 수 증가 → `LoadRandomMiniGame()` 흐름으로
다음 씬을 불러옵니다. 전체 목표 횟수에 도달하면 기존 종료 흐름을 실행합니다.
실패 시 씬을 유지하고 Space 재시도를 허용합니다. 독립적으로 MiniGame01만
실행하면 매니저를 새로 생성하지 않으며 기존 테스트 화면과 재시도가 유지됩니다.

전환 전 구독을 해제하고 종료된 게임의 입력·타이머·모델을 정리합니다.
현재 씬/컨트롤러/실행 번호가 다른 결과와 중복 클리어는 무시합니다.
후보가 여러 개면 방금 끝낸 씬을 다시 선택하지 않습니다.
앞으로 추가할 게임도 `MiniGameController`를 사용하면 같은 연결을 재사용합니다.
공통 컨트롤러가 없는 기존 게임의 `OnMiniGameCleared()` 직접 호출도 유지합니다.

MainGame 씬에는 현재 MiniGame01/02가 등록되어 있습니다. **MiniGame02는 아직
실제 플래피버드가 없는 기존 테스트 씬**입니다. 현재는 씬 전환까지 구현한 것이며,
다음 게임 내용은 스네이크 검토 후 제작합니다. MainGame의 최초 선택은 기존처럼 무작위입니다.
기존 관리자의 Shift 숨김 기능은 전역 `Time.timeScale`을 변경합니다.
이 기존 동작은 이번 스네이크 작업 범위에서 수정하지 않았습니다.

## 검증 실행

Unity의 `Window > General > Test Runner`에서 `GE.MiniGames.Tests`(EditMode)와
`GE.MiniGames.PlayModeTests`(PlayMode)를 실행합니다.
PlayMode 테스트는 실제 저장된 씬과 Input System 키보드 이벤트를 사용합니다.
그래픽 장치가 있으면 시작/플레이/일시정지/결과 화면을 `Logs/Snake-*.png`로 저장합니다.

이번 검증 결과 (Unity 6000.3.23f1):

- 씬 실제 생성·저장 및 C# 컴파일 성공.
- EditMode **24/24 통과**: 사과 생성/성장, 몸·벽 충돌, 반전/연속 입력 방지,
  사과 5개 성공, 보드 채우기, 결과 1회 전달, 정지/재개/재시도/종료 정리.
- PlayMode **3/3 통과**: 저장된 씬에서 실제 Input System 키 입력,
  Space 시작/재시도, Tab 유지/재개, 정지 중 입력 차단, 숨김/복원, 씬 언로드/재로드.
  MainGame과 같은 70% viewport 및 16:9·3:4 영역에서 UI가 잘리지 않는지도 확인.
- 실제 렌더링으로 시작/플레이/정지/결과/축소 viewport 화면을 캡처해 확인.
- 실행 기록: `Logs/Snake-Build.log`, `Logs/Snake-EditMode.xml`, `Logs/Snake-PlayMode.xml`.

클리어 연결의 추가 테스트는 `MiniGameClearIntegrationTests`에서 실제 MainGame,
MiniGame01, MiniGame02를 사용해 성공 전환, 실패 후 재도전, 중복/이전 결과 차단,
최종 목표 달성 시 종료를 확인합니다. 결과 파일은 `Logs/Snake-ClearIntegration.xml`입니다.
**추가 검증 완료: PlayMode 6/6 통과** (클리어 통합 3개 + 기존 회귀 3개).
열려 있는 원본 Unity 작업을 보호하기 위해 같은 소스의 검증용 복사본에서 컴파일하고
실제 씬 로드·언로드를 실행했습니다. 이 추가 실행은 화면 캡처 없이 진행했습니다.
별도 실행 파일 빌드는 이번 범위에 포함되지 않습니다.
