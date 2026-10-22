# Claude 협업 작업 지침

이 문서는 협업자의 Claude가 프로젝트를 수정할 때 지켜야 할 규칙과 기존 시스템의 연결 계약을 정리한다. **2026-10-22에 확인한 코드 기준**이다. 구현이 바뀌면 관련 설명도 함께 갱신한다.

사용자의 최신 요청과 [AGENTS.md](AGENTS.md)를 먼저 확인한다. 작업 전 현재 코드와 씬의 실제 연결을 읽고, 이전 대화나 과거 문서만으로 구현 상태를 추정하지 않는다.

## 1. 작업 범위와 변경 보존

- 메인 시스템은 프로젝트 소유자가 담당한다. 미니게임 작업을 요청받았다면 개별 게임 구현을 중심으로 수정하고, 공통 매니저·씬 전환·목숨·입력 정책을 임의로 재설계하지 않는다.
- 공통 코드 수정이 필요한 경우 변경 이유와 다른 게임에 미치는 영향을 설명하면서 요청 범위 안에서 최소한으로 수정한다.
- 시작 전에 `git status --short`와 대상 파일의 diff를 확인한다. 기존 미커밋 변경을 자신의 작업으로 오인하거나 되돌리지 않는다.
- 요청과 무관한 리팩터링, 이름 변경, 일괄 포맷 변경, 패키지 업그레이드, 프로젝트 설정 변경을 섞지 않는다.
- `.unity`, `.prefab`, `.asset`을 통째로 다시 생성하거나 기존 `.meta`를 삭제하지 않는다. 파일을 이동할 때는 `.meta`도 함께 보존해 GUID와 Inspector 참조가 유지되게 한다.
- `Library`, `Temp`, `Logs`, `obj`, 빌드 출력물은 소스와 구분한다. 테스트·캡처·검증용 임시 파일을 납품 소스에 섞지 않는다.
- 문서나 주석만 요청받았다면 실행 로직, Inspector 설정값, 씬, 프리팹을 변경하지 않는다.

## 2. 한국어 주석 규칙

[AGENTS.md](AGENTS.md)의 규칙을 모든 코드 작성·수정에 적용한다.

- 작성하거나 수정하는 코드에는 개발자 간 공유를 위한 **한국어 주석**을 포함한다.
- 주요 함수의 역할, 기존 시스템과의 연결 지점, 입력·상태 전환의 주의점을 해당 코드 가까이에 설명한다.
- 단순 대입을 반복 설명하지 말고 구현 이유와 호출 책임을 짧게 적는다.
- 동작을 수정하면 관련 주석도 함께 갱신한다. 구현과 다른 과거 설명을 그대로 두지 않는다.

## 3. 게임 구조와 현재 구현 범위

게임은 같은 화면에서 동시에 표시되는 두 영역으로 구성된다.

- **MainGame**: 주인공이 밤늦게 엄마 몰래 게임하는 상황. 엄마의 말이나 접근에 대응하고 자는 척해야 하는 메인 게임이다.
- **MiniGame**: 주인공이 게임기로 플레이하는 게임. MainGame 위에 Additive 씬으로 로드된다.

기본 흐름은 다음과 같다.

```text
Lobby
→ PageBase.OnActionButtonClick()
→ MenuManager.NextScene()
→ MainGame 로드
→ MiniGameManager.Start()
→ 미니게임 무작위 선택 및 Additive 로드
→ 성공: 클리어 수 증가 후 다음 미니게임
→ 실패: 목숨 1개 차감, 목숨이 남으면 같은 미니게임 재시도
→ 목숨 소진 또는 다른 패배 조건: 게임오버
→ Retry? 클릭: MainGame 전체 재생성
```

- 기획상 미니게임 목표는 **18개**다. 현재 등록된 씬은 `MiniGame01`(스네이크), `MiniGame02`(Flappy), `MiniGame03`(Sorting)이며 각각 공통 컨트롤러가 연결되어 있다.
- 씬 목록은 계속 늘어날 수 있다. 과거의 두 씬 구성이나 MiniGame02가 빈 테스트 씬이라는 가정을 새 구현·테스트에 사용하지 않는다.
- 현재 목표 판정은 **누적 18회 클리어**다. 선택 풀이 소진되면 다시 채우므로, 서로 다른 18개를 한 번씩 완료했는지 검사하는 구현은 아니다.
- `OnGameEnding()`은 승리 로그와 `GameFinished` 알림을 보낸다. 엔딩 화면은 아직 없으며, 엄마의 문 방문·발각 이벤트는 구현되어 있다.
- `PageBase.FakeLoad()`는 임시 로딩 표시다. 게임 성공·실패나 게임오버를 판단하는 함수가 아니다.

## 4. 코드의 책임과 어셈블리 경계

| 위치 | 책임과 주의사항 |
| --- | --- |
| `Assets/Scripts/Manager/MenuManager.cs` | Lobby → MainGame의 상위 씬 전환. 미니게임 재시도나 다음 미니게임 선택을 이곳에 연결하지 않는다. |
| `Assets/Scripts/Manager/MiniGameManager.cs` | Additive 씬 관리, 클리어 집계, 목숨, MainGame의 Tab·Shift 입력, 게임오버·전체 재시작. |
| `Assets/Scripts/Utilities/SingletonMono.cs` | 싱글턴 생성·중복 방지·수명 관리. 상속 클래스의 `Awake`와 `OnDestroy`에서 기존 기본 처리를 보존한다. |
| `Assets/Scripts/Commons/ComEnum.cs` | `ESceneType`, 확장 가능한 패배 원인 `EGameOverReason`. |
| `Assets/Scripts/MainGame/MainGameEventDirector.cs` | 이벤트 완료 뒤 30~120초 대기, 등록된 이벤트 선택, 동시 실행 방지, 숨김 상태 기반 공통 발각 판정. 씬 수명에 종속된다. |
| `Assets/Scripts/MainGame/MainGameEventBase.cs` | 개별 이벤트의 실행·완료·취소·발각 구간 계약. 새로운 이벤트는 이 타입을 상속해 Director 목록에 등록한다. |
| `Assets/Scripts/MainGame/MotherDoorVisitEvent.cs` | 발걸음 → 문 열기 → 침대 관찰 → 퇴장 → 문 닫기의 순서와 발각 시간. |
| `Assets/Scripts/MainGame/MotherActorController.cs` | Inspector 이동 지점을 따라 이동·회전하고 기존 Run을 감속한다. 이벤트 주기나 게임오버를 관리하지 않는다. |
| `Assets/Scripts/MainGame/DoorController.cs` | 기존 경첩 회전과 Open/Close/IsOpen/IsClosed API. 입력과 발각 판정은 외부 책임이다. |
| `Assets/Scripts/MiniGames/Common` | `IMiniGame`, `MiniGameController`, 결과·상태 계약, 단독 실행용 UI. 변경 시 모든 미니게임에 미칠 영향을 확인한다. |
| `Assets/Scripts/MiniGames/Snake` | 스네이크 규칙·로컬 실행·화면 표시. 전체 목숨이나 다음 씬을 직접 관리하지 않는다. |
| `Assets/Scripts/MiniGames/Editor` | Editor 전용 씬 구성·검증 도구. 런타임 코드와 분리한다. |

**어셈블리 의존성 방향을 유지한다.**

- 메인 매니저는 `Assembly-CSharp`, 미니게임 코드는 `GE.MiniGames.asmdef`에 속한다.
- `GE.MiniGames`에서 `Assembly-CSharp`의 `MiniGameManager`를 직접 참조할 수 없다. 미니게임 코드에 싱글턴 직접 호출을 넣거나, 이를 우회하려고 리플렉션을 런타임 연결 방식으로 추가하지 않는다.
- 미니게임은 공통 결과 이벤트로 알리고 매니저가 수신한다. 메인 참조를 가능하게 만들기 위해 `.asmdef`를 삭제하거나 스크립트를 다른 어셈블리로 대량 이동하지 않는다.
- 통합 테스트가 메인 타입에 리플렉션으로 접근하는 것은 테스트 어셈블리의 경계 처리를 위한 것이다. 이를 게임 구현 패턴으로 복사하지 않는다.
- `SingletonMono.Singleton` 접근은 인스턴스가 없으면 새 오브젝트를 생성한다. 미니게임 단독 실행에서 Inspector 참조가 없는 메인 매니저를 실수로 생성하지 않도록 주의한다.

## 5. 미니게임 성공·실패·재시도 계약

새 미니게임도 현재 공통 컨트롤러 계약에 맞춰 연결한다. 로드된 씬에는 매니저가 식별할 대표 `MiniGameController`를 하나 둔다.

```text
개별 게임의 규칙 판정
→ MiniGameController.Finish(Success 또는 Failure, 사유)
→ Completed(MiniGameResult)
→ MiniGameManager.HandleMiniGameCompleted()
   ├─ 성공: OnMiniGameCleared()
   └─ 실패: OnMiniGameFailed()
```

- `Completed`는 **실행당 한 번** 전달한다. `GameId`, `RunId`, `Outcome`과 상태의 일치를 유지한다.
- 같은 결과를 이벤트와 싱글턴 호출 양쪽으로 중복 전달하지 않는다. 성공을 UI 표시 여부로 판단하거나, 실패를 매 프레임 보고하지 않는다.
- 현재 매니저는 다른 씬·이전 컨트롤러·과거 실행의 결과를 거르고, 같은 실패 실행의 목숨 중복 차감을 막는다. 이 검증을 제거하지 않는다.
- 성공 후 다음 씬 선택·언로드는 매니저 책임이다. 개별 게임에서 `SceneManager.LoadScene*`을 호출하거나 전체 클리어 수를 직접 증가시키지 않는다.
- 로컬 재시도는 `StartGame()`을 사용한다. 몸·점수·타이머 등 해당 게임만 초기화하고 `RunId`를 증가시킨다. 전체 목숨과 누적 클리어 수를 초기화하지 않는다.
- `ResetGame()`은 시작 대기 상태로 복원한다. 즉시 플레이 시작과 같은 의미로 취급하지 않는다.
- `StopGame()`은 로컬 자원 정리이며 새로운 성공·실패 결과를 보내지 않는다.
- 성공·실패 UI를 바꿔도 게임 결과 이벤트 자체는 독립적으로 동작해야 한다.
- 전환·언로드 시 이벤트 구독과 게임 자원을 정리한다. 비활성화·재활성화 뒤 입력이 몰리거나 이벤트가 중복 구독되지 않게 한다.

## 6. 입력·일시정지·숨김과 시간 처리

기존 **New Input System**을 사용한다. 입력 문제를 해결한다는 이유로 Legacy Input이나 프로젝트의 입력 처리 설정을 임의로 변경하지 않는다.

| 상황 | 현재 동작과 보존할 조건 |
| --- | --- |
| MainGame에서 Tab | `MiniGameManager`가 눌림 순간을 처리하고 컨트롤러의 정지·재개 API를 호출한다. 키를 길게 눌러도 한 번만 전환한다. |
| Tab 정지 | 미니게임만 멈춘다. `Time.timeScale`을 0으로 만들어 MainGame까지 정지시키지 않는다. |
| Shift 누름 | 미니게임의 Camera·Canvas만 숨긴다. 루트·로직·물리·전역 시간은 계속 진행된다. |
| Shift 해제 | 게임기를 다시 표시한다. 숨기기 전에 Tab으로 정지했다면 정지 상태를 유지한다. |
| Shift로 숨긴 동안 | Tab으로 정지 상태를 바꾸지 않는다. MainGame에 배치한 정지 오버레이도 함께 숨긴다. |
| MainGame 게임오버 | 현재 전체 진행을 `Time.timeScale = 0`으로 정지한다. 클릭 UI는 계속 작동해야 한다. |
| MiniGame01 단독 실행 | 메인 매니저를 만들지 않고 기존 Space 시작·재시도와 자체 Tab 정지·재개를 유지한다. |

- MainGame에 연결되면 `ExternalPauseControl = true`로 컨트롤러 자체 Tab 처리를 막는다. 한 입력을 매니저와 컨트롤러가 동시에 처리하지 않게 한다.
- `MiniGameTestUI.SetExternalPauseOverlay(true)`는 기존 **정지 화면만** 숨긴다. 이를 시작·실패 화면 전체를 끄는 처리로 바꾸지 않는다.
- 정지 중 들어온 입력은 재개 시 누적 적용하지 않는다. 기존 `FlushInput()`과 첫 틱 건너뛰기 처리를 보존한다.
- 매니저의 `[DefaultExecutionOrder(-100)]`은 컨트롤러보다 먼저 입력을 처리하기 위한 것이다. 실행 순서를 바꿀 때는 정지 프레임의 추가 진행 여부도 검사한다.

- 숨기기 전에 Tab을 눌러야 숨긴 동안에도 미니게임이 정지한다. Shift만 누르면 보이지 않는 미니게임도 실패할 수 있다.
- 외부 숨김 조회는 `IsMiniGameHidden`이다. 과거 `IsHidingAndPaused`·`PauseAndHideMiniGame` 이름으로 되돌리지 않는다.
- 숨김 해제 시 각 Camera·Canvas의 기존 enabled 상태를 복원한다. 새 Additive 씬도 첫 렌더링 전에 숨기고, 로딩 중에도 Shift 상태를 갱신한다.

## 7. 목숨·게임오버·전체 재시작

- 기본 목숨은 `startingLives = 3`, 현재 목숨은 매니저의 `currentLives` 정수로 관리한다. 외부 조회는 `CurrentLives`를 사용한다.
- 유효한 실패 한 번에 `OnMiniGameFailed()`에서 1개만 차감한다. 미니게임이 별도로 목숨을 차감하지 않는다.
- 목숨이 남으면 같은 씬에서 기존 Space 재시도를 허용한다. 다음 미니게임으로 자동 넘어가거나 전체 MainGame을 리셋하지 않는다.
- 목숨이 0개면 `TriggerGameOver(EGameOverReason.LivesDepleted)`로 진입한다.
- 엄마 발각 등 다른 패배 원인도 같은 `TriggerGameOver`를 사용한다. 별도의 게임오버 플래그·화면·재시작 경로를 중복 구현하지 않는다.

현재 `MainGameEventDirector`는 활성 이벤트의 발각 구간에 게임기가 보이면 아래 공통 게임오버를 요청한다. Inspector의 매니저 참조를 사용하며 없는 싱글턴을 생성하지 않는다.

```csharp
// MainGame에서 엄마에게 발각됐다고 판정한 시점에 공통 게임오버를 요청합니다.
MiniGameManager.Singleton.TriggerGameOver(EGameOverReason.CaughtByMother);
```

- 새 원인은 `EGameOverReason`에 추가한다. 현재 값은 `None`, `LivesDepleted`, `CaughtByMother`다.
- 이미 게임오버가 됐거나 재시작 중이면 결과·입력·추가 종료 요청을 중복 처리하지 않는다. 먼저 확정된 패배 원인을 덮어쓰지 않는다.
- `LivesChanged`, `GameOverTriggered`를 연동할 수 있다. 구독한 쪽은 자신의 수명에 맞춰 구독을 해제한다.
- `Retry?`는 `RestartMainGame()`을 호출한다. Additive 씬 정리 → MainGame 전체 재로드 → 새 매니저의 `Awake()`에서 목숨 3개와 클리어 수 0으로 초기화하는 흐름을 유지한다.
- 미니게임 로딩 중 발각된 경우와 Retry 연속 클릭도 고려한다. 기존 로드 대기·중복 재시작 차단을 제거하지 않는다.
- 재시작 시 기존 싱글턴이 남아 새 매니저를 중복 인스턴스로 제거하는 문제가 생기지 않게 한다. `RoutineRestartMainGame()`의 씬 이동·정리 순서를 임의로 생략하지 않는다.
- 재시작·종료 뒤 0배 시간이 남지 않아야 한다. 미니게임만 초기화하고 MainGame 전체 재시작이 됐다고 보고하지 않는다.

## 8. UI·씬·카메라 수정 규칙

**사용자가 요청한 것 외의 Image 오브젝트는 되도록 생성하지 않는다. 추가가 필요하면 생성 목적을 사용자에게 알리면서 진행한다.**

- 현재 정지·게임오버 UI는 런타임 스크립트로 생성하지 않고 **MainGame 하이어라키에 미리 배치**한다.
- 같은 기능의 Canvas·Image·Text를 새로 만들기 전에 기존 오브젝트와 Inspector 참조를 확인한다.
- uGUI와 TextMeshPro를 사용한다. `Retry?`에는 Text 자체의 Button을 사용하므로 버튼 배경용 Image를 추가할 필요가 없다.
- Inspector 참조, Button의 저장된 `OnClick`, `.meta` GUID를 보존한다. 필드 이름 변경 시 직렬화 데이터의 이전도 함께 고려한다.

현재 하이어라키는 다음과 같다.

```text
MainGame
└─ Manager
   └─ MiniGameManager
      └─ MiniGamePauseCanvas
         ├─ PauseOverlay       : 검은 Image, 불투명도 50%, 미니게임 영역
         │  └─ PauseText       : 중앙의 -PAUSED-
         └─ GameOverOverlay    : 전체 화면을 덮는 검은 Image
            └─ RetryText       : 중앙의 Retry? + Button
```

- `miniGamePauseOverlay`, `gameOverOverlay`, `gameOverRetryButton`의 참조와 `RestartMainGame` 클릭 연결을 유지한다.
- 오버레이의 기본 저장 상태는 비활성이다. 검증을 위해 켠 상태를 그대로 저장하지 않는다.
- 현재 오버레이 Canvas는 Screen Space - Overlay, 정렬 순서 `32767`이다. 정지·게임오버 UI가 미니게임 뒤에 가리지 않게 한다.
- 정지 Image는 미니게임 viewport만, 게임오버 Image는 MainGame 전체를 덮어야 한다. 해상도가 바뀌어도 텍스트가 중앙에 있어야 한다.
- 정지 Image·Text는 불필요한 클릭을 가로채지 않아야 한다. 게임오버 배경은 아래 화면 클릭을 막고 `Retry?` Text는 클릭을 받아야 한다.
- MainGame과 Additive 미니게임을 합쳐 **활성 EventSystem은 하나**로 유지한다. 매니저가 미니게임 씬의 EventSystem과 입력 모듈을 런타임에 비활성화하는 처리를 보존한다.
- 미니게임 카메라는 매니저가 지정하는 viewport를 따른다. 현재 기본값은 `(0.15, 0.15, 0.7, 0.7)`, depth는 `1`이다. 전체 화면으로 덮어쓰지 않는다.
- 새 미니게임 카메라가 `Camera.main` 선택을 방해하지 않도록 태그를 확인한다. 카메라·Canvas·조명·오디오 등 Additive 씬 간 간섭을 함께 확인한다.
- 씬 구성 도구를 실행하기 전에 기존 씬의 저장 상태를 확인한다. 기존 씬을 파괴하고 다시 만들어 UI 문제를 해결하지 않는다.

## 9. Unity 환경과 연결 문제

- Editor 버전은 `ProjectSettings/ProjectVersion.txt`를 따른다. 현재 `6000.3.23f1`이다.
- URP, New Input System, uGUI/TMP를 유지한다. 버전과 의존성은 `Packages/manifest.json` 및 `packages-lock.json`을 확인한다.
- Editor가 열려 있고 Unity CLI/MCP를 사용할 수 있으면 실제 Editor를 통해 씬·프리팹을 편집하고 저장한다. 저장되지 않은 Editor 상태와 디스크 YAML을 서로 덮어쓰지 않는다.
- 여러 프로젝트가 열려 있을 수 있으므로 CLI 호출에는 작업 중인 **실제 프로젝트 경로**를 지정한다. 다른 개발자의 절대 경로나 다른 프로젝트의 MCP 설정을 복사하지 않는다.
- 연결 실패만으로 Editor가 꺼졌다고 단정하지 않는다. Pipeline 패키지 상태, 컴파일 오류, Safe Mode, 저장 대화상자, 패키지 로딩, 로컬 연결 권한을 확인한다.
- Pipeline 서버가 시작돼 있어도 `Library/Pipeline/.unity-pipeline-port`가 없으면 CLI가 찾지 못할 수 있다. 이 파일은 서버가 생성하는 연결·인증 정보이므로 직접 조작하거나 저장소에 추가하지 않는다. 서버 재시작 후 연결 상태를 다시 확인한다.
- 저장 대화상자가 떠 있으면 자동 명령과 테스트가 멈출 수 있다. 미저장 변경을 임의로 버리거나 Editor를 강제 종료하지 말고 상황을 알린다.
- 새 미니게임 씬은 매니저의 씬 목록과 Build Profiles/Build Settings에 등록해야 한다. 파일이 존재한다고 로드 가능한 상태라고 단정하지 않는다.

## 10. 검증과 완료 보고

변경 범위에 맞춰 컴파일과 기존 테스트를 실행한다. 문서만 수정한 작업 때문에 불필요하게 씬을 열거나 테스트 환경을 변경하지 않는다.

Unity Test Runner의 주요 테스트는 다음과 같다.

| 테스트 | 확인 대상 |
| --- | --- |
| EditMode `SnakeModelTests` | 스네이크 규칙·충돌·사과·성공 판정 |
| EditMode `SnakeLifecycleTests`, `SnakeTestUITests` | 상태 전환·결과 1회 통지·입력·로컬 재시도 |
| PlayMode `SnakeScenePlayModeTests` | 저장된 미니게임 씬의 입력·화면·viewport·언로드 |
| PlayMode `MiniGameClearIntegrationTests` | MainGame의 클리어 집계·다음 씬·중복/과거 결과 차단 |
| PlayMode `MiniGamePauseIntegrationTests` | Tab 정지·재개, Shift 숨김과의 조합, 정지 UI 수명 |
| PlayMode `MiniGameLivesIntegrationTests` | 목숨 차감·로컬 재시도·게임오버·전체 재시작·다른 패배 원인 |
| PlayMode `MainGameMotherEventIntegrationTests` | 실제 소리 종료·문 시작 기준 시간, 침대 관찰·퇴장, 닫힘 중 발각, Tab 상태의 발각, 재시작·목표 달성 시 이벤트 수명 |

관련 기능을 수정한 경우 다음을 확인한다.

1. Lobby → MainGame → Additive 미니게임 진입이 정상이다.
2. Tab을 길게 눌러도 한 번만 정지하며, MainGame 시간은 계속 흐른다.
3. Tab 정지 후 Shift로 숨겼다 돌아와도 정지 상태와 진행도가 유지된다.
4. 실패 한 번에 목숨이 정확히 하나 줄고, 중복·지난 결과는 반영되지 않는다.
5. 목숨이 남으면 Space로 같은 게임을 재시도하며 목숨·전체 진행도는 유지된다.
6. 세 번째 실패 시 전체 화면 게임오버가 표시되고 `Retry?` 클릭이 작동한다.
7. Retry 후 기존 MainGame 오브젝트와 Additive 씬이 제거되고, 새 실행은 목숨 3개·클리어 수 0으로 시작한다.
8. 엄마 발각에 해당하는 공통 진입점과 로딩 중 게임오버도 같은 종료·재시작 경로를 따른다.
9. 미니게임 단독 실행과 다른 게임으로의 전환도 유지된다.

- UI 변경은 실제 Game View에서 확인한다. Play 진입이나 스크린샷 한 장만으로 게임이 진행 중이라고 판단하지 말고 프레임·시간·상태 변화도 확인한다.
- UI 클릭 검증은 저장된 Button 연결, Raycaster, EventSystem, 입력 액션과 Game View 포커스를 함께 확인한다. 컨트롤러의 키 입력이 된다고 마우스 UI도 된다고 가정하지 않는다.
- 테스트용 입력 장치·액션 필터·포커스 정책·백그라운드 실행·`Time.timeScale`을 원래 상태로 복원한다. 테스트 통과를 위해 제품의 입력 설정을 바꾸지 않는다.
- TMP 폰트·아틀라스 등 의도하지 않은 `.asset` 변경이 생겼는지 diff를 확인한다. 기존 변경은 보존하고 새로 생긴 부수 변경은 원인을 구분한다.
- 완료 시 수정 파일, 동작 변화, 실제 실행한 검증과 미검증 범위를 보고한다. 과거 문서의 테스트 결과를 이번 실행 결과처럼 사용하지 않는다.

## 11. 기존 문서를 읽을 때의 주의점

추가 배경은 [미니게임 README](Assets/MiniGames/README.md)와 [기존 전달 문서](Assets/MiniGames/DEVELOPER_HANDOFF.md)에 있다. 다만 두 문서에는 이전 구현 기준의 설명이 남아 있다.

- 현재 클래스 이름은 **`MiniGameManager`**다. 과거의 `MiniGameSceneManager`라는 이름으로 되돌리지 않는다.
- 현재는 실패 목숨 차감, MainGame의 Tab 정지 UI, 공통 게임오버와 전체 재시작이 구현돼 있다. 과거의 “메인 실패 함수 없음” 또는 “성공만 전달” 설명을 현재 계약으로 적용하지 않는다.
- 전체 목표는 사용자가 정한 **18개**다. 이전 README의 “나머지 6개”를 전체 기획으로 해석하지 않는다.
- 이전 문서의 줄 번호, 커밋 기준, 테스트 개수는 과거 기록이다. 현재 코드·씬·테스트를 직접 확인한다.
