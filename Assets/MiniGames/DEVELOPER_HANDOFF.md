# 미니게임 성공·실패·진행 함수 연결 정리

현재 기준: `557f1df`의 코드. 메인 개발자의 기존 함수 여부는 스네이크 추가 전
커밋 `6577f95`와 비교했습니다. 아래 줄 번호는 공유용 주석 추가 후 기준입니다.

## 적용 원칙과 점검 결론

- 전체 클리어 수, 다음 미니게임 선택·로딩, 전체 목표 달성, 씬 언로드는
  기존 `MiniGameSceneManager` 함수가 담당합니다.
- `MiniGameController`는 해당 미니게임의 입력·타이머·로컬 상태와 결과 통지만 담당합니다.
  클리어 수를 증가시키거나 `SceneManager.LoadScene*`을 호출하지 않습니다.
- 기존 메인 코드에는 실패·재시도 함수가 없습니다. 사용자 확인에 따라
  현재 제작한 `Finish(Failure, ...)`와 `StartGame()` 재시도를 유지합니다.
- 이번 점검에서는 이미 연결된 실행 코드를 변경하지 않았습니다. 함수 사용처를 문서화했습니다.

## 1. 기존 메인 개발자 함수

파일: `Assets/Scripts/Manager/MiniGameManager.cs`

이 파일의 클래스 이름은 **`MiniGameSceneManager`**입니다.

| 역할 | 기존 함수와 위치 | 현재 호출 위치·동작 |
| --- | --- | --- |
| 최초 미니게임 로드 | `Start()` 51행 → `LoadRandomMiniGame()` 130행 | MainGame 진입 시 기존 무작위 미니게임 선택을 실행합니다. |
| 클리어 반영 | `OnMiniGameCleared()` 295행 | `HandleMiniGameCompleted()` 243행에서 호출합니다. 클리어 수를 증가시키고 다음 게임/전체 종료를 결정합니다. |
| 다음 게임 진행 | `LoadRandomMiniGame()` 130행 | `OnMiniGameCleared()` 310행에서 호출합니다. 실제 로드는 `RoutineLoadMiniGame()` 171행의 Additive 전환으로 처리합니다. |
| 현재 미니게임 종료 | `CloseMiniGameImmediately()` 265행 | 전체 목표 도달 시 305행에서 호출합니다. `RoutineCloseMiniGame()` 272행이 씬을 언로드합니다. 외부에서도 호출할 수 있습니다. |
| 전체 목표 달성 | `OnGameEnding()` 315행 | 목표 횟수 도달 시 호출합니다. **현재 원래 구현은 완료 로그 출력이며 별도 엔딩 씬은 없습니다.** |
| Shift 숨김/복원 | `PauseAndHideMiniGame(bool)` 93행 | 기존 `HandleShiftHideInput()`에서 호출합니다. 루트 표시 상태와 전역 `Time.timeScale`을 변경하는 기존 기능입니다. |

기존 함수명과 진행 흐름을 유지하면서 이전 작업에서 이벤트 연결, 중복 클리어 방지,
빈 목록/씬 등록 검사, 후보가 여러 개일 때 연속 같은 씬 선택 방지를 추가했습니다.

## 2. 메인 코드에 추가한 연결 함수

파일: `Assets/Scripts/Manager/MiniGameManager.cs`

| 추가 함수 | 위치 | 역할 |
| --- | --- | --- |
| `BindMiniGameResult(Scene)` | 218행 | 로드된 씬의 컨트롤러를 찾아 `Completed`를 구독합니다. 로드 완료 후 214행에서 호출합니다. |
| `HandleMiniGameCompleted(MiniGameController, MiniGameResult)` | 235행 | 현재 씬·컨트롤러·실행 번호·성공 상태를 확인한 뒤 기존 `OnMiniGameCleared()`를 호출합니다. 실패 시 클리어 수나 씬을 바꾸지 않습니다. |
| `UnbindMiniGameResult(bool)` | 247행 | 전환/종료 전 이벤트 구독을 해제하고 `StopGame()`으로 해당 게임을 정리합니다. |
| `OnDestroy()` | 258행 | 남은 결과 구독을 해제하고 기존 싱글턴 정리를 호출합니다. |

연결 필드 `currentMiniGameController`, `currentResultHandler`, `currentClearHandled`도
추가한 항목입니다. 원래부터 있던 메인 API로 오해하지 않도록 구분합니다.

## 3. 우리가 만든 로컬 게임 함수

| 역할 | 파일·위치 | 사용 함수와 동작 |
| --- | --- | --- |
| 스네이크 판정 | `Assets/Scripts/MiniGames/Snake/SnakeGame.cs:47` | `OnGameTick()` → `SnakeModel.Step()`. 55행은 성공, 56~57행은 벽/몸 충돌 실패를 전달합니다. |
| 성공/실패의 로컬 정지와 통지 | `Assets/Scripts/MiniGames/Common/MiniGameController.cs:133` | `Finish(MiniGameOutcome, string)`가 이동을 멈추고 상태/사유를 저장한 뒤 `Completed`를 실행당 한 번 전달합니다. **메인 클리어 수나 씬을 직접 변경하지 않습니다.** |
| 시작 및 재시도 입력 | `Assets/Scripts/MiniGames/Common/MiniGameTestUI.cs:60` | `ProcessSpace()`가 Space를 놓은 뒤 새로 눌렀는지 확인하고 71행에서 `game.StartGame()`을 호출합니다. |
| 해당 게임 시작/재시도 | `Assets/Scripts/MiniGames/Common/MiniGameController.cs:82` | `StartGame()`이 `OnResetGame()`으로 몸·사과·점수·타이머를 초기화하고 입력/결과/일시정지 상태를 초기화한 후 실행합니다. 실행 식별용 `RunId`는 증가합니다. |
| Tab 일시정지/재개 | `Assets/Scripts/MiniGames/Common/MiniGameController.cs:95`, `:102` | `PauseGame()` / `ResumeGame()`. 이 게임만 멈추고 전역 시간은 변경하지 않습니다. |
| 시작 대기 상태로 초기화 | `Assets/Scripts/MiniGames/Common/MiniGameController.cs:110` | `ResetGame()`. 초기 보드와 시작 화면 상태로 돌립니다. |
| 해당 게임 정리 | `Assets/Scripts/MiniGames/Common/MiniGameController.cs:123` | `StopGame()`. 예약 실행·입력과 게임 모델/타이머를 정리합니다. 씬 언로드 자체는 메인의 기존 함수가 담당합니다. |

## 4. 호출 순서

성공(MainGame이 로드한 씬):

```text
SnakeGame.OnGameTick()
→ Finish(Success, 사유)
→ Completed(result)                         [새 결과 통지]
→ MiniGameSceneManager.HandleMiniGameCompleted()
→ OnMiniGameCleared()                       [기존 메인 함수]
   ├─ 목표 미달: LoadRandomMiniGame()        [기존 메인 함수]
   │             → RoutineLoadMiniGame()
   └─ 전체 목표 달성: CloseMiniGameImmediately() + OnGameEnding()
```

실패·재시도:

```text
SnakeGame.OnGameTick()
→ Finish(Failure, 사유)
→ 실패 상태/화면 표시 + Completed(result)
→ 메인 연결부는 클리어 수/씬을 유지
→ Space를 놓고 다시 누름
→ MiniGameTestUI.ProcessSpace()
→ MiniGameController.StartGame()
→ SnakeGame.OnResetGame() → 같은 게임을 새로 실행
```

## 5. 구분해야 하는 동작

- `Finish()`는 로컬 게임 종료/결과 보고이고, `OnMiniGameCleared()`는 메인 진행 반영입니다.
- `StartGame()`은 로드된 게임의 실제 플레이 시작/재시도이고,
  `LoadRandomMiniGame()`은 다른 미니게임 씬 선택/로딩입니다.
- Tab은 로컬 일시정지, Shift는 기존 메인의 화면 숨김 기능입니다.
- `MenuManager.NextScene()`은 로비에서 MainGame으로 이동하는 함수입니다.
  미니게임 실패/재시도/다음 미니게임에 연결하지 않았습니다.
- `PageBase.FakeLoad()`는 기존 화면의 임시 로딩 표시입니다. 성공/실패 판정 함수가 아닙니다.
- `MiniGame01` 단독 실행에서는 메인 매니저를 자동 생성하지 않습니다.
  성공·실패 화면과 Space 재시도를 유지합니다.
- `MiniGame02`는 아직 플레이 가능한 플래피버드가 없는 기존 테스트 씬입니다.

## 기존 검증 기록

앞선 연결 작업에서 동일 소스의 검증용 복사본으로 컴파일 및 PlayMode 6/6 통과를 확인했습니다.
`Assets/Tests/MiniGames/PlayMode/MiniGameClearIntegrationTests.cs`가 실제 MainGame을 로드해
성공 전환, 실패 후 재시도, 중복/지난 결과 차단, 최종 목표 종료를 검사합니다.
결과는 `Logs/Snake-ClearIntegration.xml`입니다.
이번 작업은 코드·이력 점검과 문서 정리이며 실행 로직을 바꾸지 않아 테스트를 재실행하지 않았습니다.
