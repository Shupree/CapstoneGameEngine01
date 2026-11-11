# MainGame 이벤트

`MainGameEvents`는 씬에 미리 배치한 이벤트 구성이다. 추가 UI Image는 사용하지 않으며 게임오버는 기존 `MiniGameManager`의 `Retry?` 화면으로 연결한다.

## 책임과 연결

- `MainGameEventDirector`: 최초 진입 및 이벤트 완료 뒤 30~120초를 무작위로 기다린다. Inspector의 `이벤트별 선택 가중치` 목록에서 실행 가능한 항목 하나를 가중치에 따라 선택하며 중복 실행하지 않는다.
- `MainGameEventBase`: `CanRun`, `TryBegin()`, `IsRunning`, `IsDetectionActive`, `Cancel()`의 공통 계약이다. 개별 연출은 `ExecuteEvent()`, 취소 정리는 `OnCancelled()`에서 구현한다.
- `MotherDoorVisitEvent`: 발걸음 재생, 문과 엄마의 방문 순서, 발각 구간을 관리한다.
- `MotherDoorRushEvent`: 같은 문 방문 경고 뒤 문 앞에서 뜸을 들이고, 빠르게 왕복하며 짧게 관찰하는 별도 이벤트다.
- `MotherWindowPeekEvent`: 노크 두 번, 무음 창문 개폐, 수평으로 들이미는 상체·얼굴과 발각 구간을 관리한다.
- `MotherClosetVisitEvent`: 옷장 소리 세 번, 두 문짝 개폐, 침대 앞 돌진과 방문 퇴장을 관리한다.
- `MotherFootstepsOnlyEvent`: 같은 발걸음을 끝까지 재생 → 1초 대기 → 같은 발걸음을 다시 끝까지 재생한다. 엄마와 문을 제어하지 않고 발각 판정은 항상 꺼져 있다.
- `MotherActorController`: 모델의 이동·회전과 Animator의 `Speed`를 제어한다. 일반 방문은 기존 Run을 0.45배로, 돌진은 이벤트가 전달한 속도로 재생한다. 관찰·취소 뒤 Idle은 1배로 복원하며 Root Motion은 사용하지 않는다.
- `DoorController`: 기존 문 회전 API를 재사용한다.
- `WindowController`: 창짝의 위치와 열림 정도를 관리한다. 개폐 시간은 이벤트의 Inspector 설정에서 전달한다.
- `MotherAppearance` / `MotherAppearanceProfile`: 모든 엄마의 공통 어두운 외형을 관리한다. 원본 머티리얼의 색·텍스처·알파는 보존하고 인스턴스의 색과 광택을 조절한다.
- `MiniGameManager`: Shift 숨김 상태를 제공하고 게임오버·전체 재시작을 담당한다. `GameOverTriggered`와 `GameFinished`를 받으면 Director가 진행 중 이벤트와 다음 타이머를 취소한다.
- `MainGameDeveloperTools`: Editor와 Development Build의 개발자 도구 표시·버튼 상태만 관리한다. 버튼의 실제 동작은 Director와 MiniGameManager의 공통 API에 위임한다.
- `MainGameCameraLookController`: MainGame 카메라의 시선만 제어한다. 미니게임이 숨겨지면 좌우 키와 마우스 드래그를 받는다.

## 문 방문 순서

```text
대기 30~120초
→ Step_Walk 실제 재생 종료
→ 1초 대기
→ Door_7 열기 시작
→ 완전히 열린 뒤 문 밖에서 플레이어 침대 옆까지 이동
→ 플레이어를 바라보며 4~7초 관찰
→ 경로를 따라 문 밖으로 완전히 퇴장
→ 문 완전히 닫기
→ 이벤트 완료, 다음 30~120초 대기 시작
```

발각 구간은 **문 열기 시작 + 1초부터 문이 완전히 닫힐 때까지**다. 문 애니메이션 완료 후 1초를 세지 않는다. 엄마 모델이 이미 밖으로 나갔어도 문이 닫히는 중에 게임기를 표시하면 발각된다. 현재 판정은 기획상 시간 구간으로 처리하며 시야 Raycast를 사용하지 않는다.

Director의 `LateUpdate`는 매니저가 `Update`에서 반영한 `IsMiniGameHidden`을 확인한다. Tab으로 정지한 화면도 보이면 발각된다. Shift를 누른 채 다음 미니게임으로 넘어가도 숨김 상태를 유지한다.

## 이벤트 선택 확률

MainGame의 `MainGameEvents`를 선택하고 `MainGameEventDirector → 이벤트별 선택 가중치`에서 이벤트 참조와 가중치를 함께 설정한다.

| 이벤트 | 가중치 | 현재 선택 확률 |
| --- | ---: | ---: |
| MotherDoorVisit | 38 | 38% |
| MotherDoorRush | 7 | 7% |
| MotherWindowPeek | 10 | 10% |
| MotherClosetVisit | 10 | 10% |
| MotherComputerVisit | 10 | 10% |
| MotherFootstepsOnly | 25 | 25% |

- 가중치 합이 100이 아니어도 유효 항목의 비율로 계산한다. 변경은 다음 추첨부터 적용한다.
- 가중치 0, 비활성, 필수 참조가 없는 이벤트는 선택에서 제외한다. 확률은 남은 유효 항목의 합을 기준으로 계산한다.
- 기존 단순 `Events` 목록을 가진 씬은 참조를 기본 가중치 1로 한 번만 이전한다. 이미 설정된 새 목록의 가중치는 보존한다.
- 자동 이벤트와 개발자 도구의 `다음 이벤트 즉시 실행`은 같은 확률 설정으로 독립적으로 추첨한다.

## 문 앞에서 뜸을 들인 뒤 돌진하는 방문

`MainGameEvents/MotherDoorRush`의 `MotherDoorRushEvent`는 별도 추첨 항목이며, 일반 방문의 문·엄마 모델·발소리 AudioSource·진입/퇴장 경로를 공유한다. UI나 엄마 모델을 추가로 생성하지 않는다.

```text
Step_Walk 실제 재생 종료 → 1초 대기 → 기존 속도로 Door_7 열기
→ 문이 완전히 열린 뒤 1초 뜸 들이기
→ 빠르게 침대 옆까지 접근 → 플레이어를 바라보며 1초 관찰
→ 같은 속도로 문 밖까지 퇴장 → 문 완전히 닫기 → 다음 이벤트 대기
```

Inspector의 `돌진 전 대기 시간 (초)`와 `관찰 시간 (초)`는 각각 기본 1초다. `돌진 이동 속도`는 기본 8.25이며 진입·퇴장 모두에 적용한다. `Run 재생 속도`는 기본 1배다. 이동 속도와 애니메이션 속도는 `FollowPath`의 이번 호출에만 전달하므로, 일반 방문의 기본 1.2 및 Run 0.45배를 변경하지 않는다.

발각은 일반 문 방문과 동일하게 **문이 열리기 시작한 뒤 1초부터 완전히 닫힐 때까지**다. 문 앞에서 뜸을 들이는 동안도 위험하며 공통 `CaughtByMother`와 기존 `Retry?` 화면을 사용한다. 개발자 즉시 실행·게임오버·목표 달성·씬 종료는 현재 코루틴을 취소하고 소리·배우·문·애니메이션 속도를 정리한다.

## 옷장 등장

`MainGameEvents/MotherClosetVisit`에 옷장 위치의 3D AudioSource와 `Closet_Sound`를 연결했다. 앞의 두 소리는 AudioSource 음량의 75%로 재생하며, 첫 소리가 끝난 뒤 `Quiet Sound Interval`(기본 0.5초)을 기다린다. 두 번째 소리가 끝나면 `Reveal Delay`(1초) 뒤 세 번째 소리를 원래 음량으로 재생하면서 옷장을 열고 엄마를 표시한다.

문틈으로 엄마가 보이면 `Before Rush Delay`(0.5초)를 세기 시작한다. 이 시간이 지나고 문짝이 열린 뒤 엄마가 침대 앞으로 달려가 `Watch Seconds`(1초) 동안 멈춘다. 엄마가 침대 앞을 떠날 때 옷장 문짝이 닫히기 시작한다. 엄마는 방문 앞에서 방문을 열고 나간다. 발각은 옷장 문틈으로 엄마가 보일 때부터 방문을 완전히 닫을 때까지다.

`Map/Wardrobe`의 `ClosetDoors`에서 문짝의 열림 각도와 개폐 시간을 조절한다. `MainGameEvents/ClosetVisitWaypoints`의 `InsideCloset`은 등장 지점이고 `ClosetThreshold`는 옷장에서 방으로 나오는 지점이다. 개발자 도구의 `옷장 등장 즉시 실행`으로 재현할 수 있다.

## 컴퓨터 등장

`MainGameEvents/MotherComputerVisit`에서 `Noise Seconds`(1.5초) 동안 모니터 위치의 3D AudioSource로 `Computer_Noise`를 재생하고, 모니터의 화면 재질 슬롯만 회색으로 바꾼다. `M_ComputerWarningGray`는 MainGame의 RetroScreen 색조를 통과한 최종 화면 기준으로 보정한다. 경고가 끝나면 소리와 화면을 복원하고 엄마가 작은 크기(`Start Scale`)에서 공용 모델의 원래 크기까지 자라면서 책상 끝으로 기어 나온다. `Crawl Seconds`와 `Stand Seconds`로 기어 나오는 시간과 책상 앞 바닥에서 일어서는 시간을 조절한다. 침대 앞에서는 `Watch Seconds`(1초) 동안 관찰한 뒤 `Door_7`을 열어 나간다.

`MainGameEvents/ComputerVisitWaypoints`의 `MonitorOpening`, `DeskEdge`, `StandInFrontOfDesk`가 각각 등장, 기어 나오는 도착점, 바닥에 서는 위치와 회전을 결정한다. 침대 앞 위치는 다른 방문과 같은 `DoorVisitWaypoints/Bedside`를 사용한다. 발각 구간은 모델이 모니터에서 보이기 시작하는 순간부터 방문이 완전히 닫힐 때까지다. 취소 시 소리·모니터 화면·공용 배우의 크기와 관절·방문을 정리한다. 개발자 도구의 `컴퓨터 등장 즉시 실행`으로 재현할 수 있다.

## 발소리만 들리는 연출

`MainGameEvents/MotherFootstepsOnly`에는 문 방문과 같은 `Step_Walk` 클립과 AudioSource 설정을 연결했다. 접근과 돌아가는 소리를 각각 원본 속도로 한 번 재생하며, 사이에 `Turnaround Delay` 1초를 기다린다. 실제 재생 종료를 기준으로 순서를 진행한다. 돌아가는 소리까지 끝난 뒤에야 다음 이벤트 대기를 예약한다.

돌아가는 소리는 Unity의 `AudioSource.volume`과 `Retreat Volume Curve`로 점차 작아진다. 커브의 X는 재생 진행률(0~1), Y는 원래 음량에 곱하는 배율이다. 기본값은 1→0이며 Inspector에서 감쇠 모양을 조절할 수 있다. 오디오 DSP 시계를 사용하고 클립 속도와 재생 길이는 유지한다. 접근 소리는 원래 크기로 재생하고, 정상 종료·취소 뒤에는 Inspector에서 설정한 음량을 복원한다.

이 이벤트는 게임기가 보이더라도 발각을 발생시키지 않는다. 엄마 모델과 Door_7의 활성 상태·위치·회전은 변경하지 않는다. 개발자 도구로 취소하거나 전체 게임이 종료되면 대기와 소리를 함께 정리한다.

## 창문 등장 순서와 조절 항목

대상은 침대 옆 `Map/wallWindowSlide`이며 책상 쪽 `wallWindowSlide (1)`은 제어하지 않는다. 아래 창짝 `windowBottom`을 위로 밀어 연다.

```text
Knock_Window 재생 종료
→ After First Knock Delay (기본 1초)
→ Knock_Window 재생 종료
→ After Second Knock Delay (기본 1초)
→ 효과음 없이 창문 열기 (기본 0.8초)
→ 완전히 열린 뒤 엄마 등장, 방 안으로 상체를 수평으로 들이미는 자세로 이동
→ Watch Seconds (기본 1초)
→ 창밖 자세로 물러나 숨김
→ 효과음 없이 창문 닫기 (기본 0.8초)
→ 완전히 닫힌 뒤 이벤트 완료, 다음 이벤트 대기
```

`MainGameEvents/MotherWindowPeek`의 `MotherWindowPeekEvent`에서 두 노크 뒤 대기 시간과 `Watch Seconds`를 각각 조절한다. `Window Open Seconds`와 `Window Close Seconds`는 소리와 독립된 개폐 시간이며 각각 기본 0.8초다. `Peek Transition Seconds`는 들이밀기·물러나기의 보간 시간이며 기본 0.35초다.

`WindowPeekWaypoints`의 `OutsideWindow`와 `PeekInside`가 위치·기울기를 결정한다. 상체 축을 수평으로 눕혀 얼굴이 화면에서 상체 왼쪽의 방 안 공간으로 나오게 배치했다. 현재 PeekInside 위치는 `(-0.65, 1.52, -3.2)`, 회전은 `(18, -57, -90)`도이며 시선은 침대의 플레이어 쪽이다. 하체는 창밖에 두고 기존 카메라 위치·시야각을 사용한다. 문 방문과 같은 `MotherActorController`를 사용하고 `BlendPose()`로 Idle 자세를 보간한다. 다음 문 방문의 ShowAt은 문 밖 지점의 정상 서 있는 자세로 복원한다.

발각은 **엄마가 표시되기 시작하는 Entering부터 창문이 완전히 닫힐 때까지**다. 경고·노크·여는 동안은 안전하고, 엄마가 사라진 뒤 닫히는 동안은 위험하다. 카메라 방향과 관계없이 기존 Shift 숨김으로 판정하며 공통 `CaughtByMother` 및 `Retry?`를 사용한다.

이 이벤트는 0.864초의 `Knock_Window`만 두 번 재생한다. `Open_Window`는 열기·닫기 어느 구간에서도 재생하거나 길이를 참조하지 않는다. 원본 오디오 파일은 프로젝트에 보존했다. 모든 시간은 MainGame 시간에 따르며 Tab 정지는 영향을 주지 않는다. 취소·게임오버·목표 달성 시 소리와 엄마를 숨기고 창문을 즉시 닫는다.

## 엄마의 공통 어두운 외형

`Assets/_Art/Settings/MotherDarkAppearance.asset`의 `Tint`(기본 RGB 0.15)와 `Maximum Smoothness`(기본 0.08)를 조절한다. `MotherRealistic`과 `Mother` 프리팹의 `MotherAppearance`가 같은 프로필을 참조하므로 모든 등장에 적용된다. MainGame의 공유 배우 인스턴스도 이 설정을 따른다.

색은 원래 피부·옷·머리 색에 곱하고, 눈·피부 등의 광택을 제한하며 발광 색을 검정으로 만든다. `MaterialPropertyBlock`을 사용하므로 원본 공유 머티리얼이나 텍스처를 변형하지 않는다. 알파 값과 눈썹·속눈썹의 클리핑은 유지한다. 편집 화면에서도 적용되며 비활성화·컴포넌트 제거 시 기존 PropertyBlock을 복원한다.

## 씬에서 조절할 항목

- `MainGameEvents`: 매니저 참조, 이벤트 목록, 대기 범위.
- `MotherDoorVisit`: 문·배우·Step_Walk AudioSource/Clip, 진입·퇴장 경로, 관찰 대상, 소리 뒤 대기와 발각 유예, 관찰 범위.
- `MotherDoorRush`: 공유 문·배우·경로 참조, 돌진 전 대기·관찰 시간, 왕복 이동 속도와 Run 재생 속도.
- `MotherActor`: 모델/Animator 참조, 이동 속도, 회전 속도, Run 재생 속도.
- `DoorVisitWaypoints`: 문 밖, 문턱, 문 안, 방 통로, 침대 옆, 플레이어 시선 목표. 방 배치를 바꾸면 경로가 가구와 겹치지 않도록 함께 조정한다. 지점의 높이는 발이 닿을 바닥 높이로 맞춘다.
- 침대 앞 정지 위치는 `MainGameEvents/DoorVisitWaypoints/Bedside`의 `Transform → Position`이다. 일반 방문·돌진 방문·옷장 등장·컴퓨터 등장이 이 지점을 공유한다. 특정 이벤트만 옮기려면 별도 지점을 만들어 해당 이벤트의 `Entry Path` 또는 `Approach Bed Path` 마지막 항목에 연결한다.

엄마 프리팹 인스턴스는 편집 시 비활성으로 저장하고 이벤트가 시작할 때 표시한다. 두 엄마 프리팹에는 공통 외형 컴포넌트를 연결했으며 이동·방문 자세는 씬의 지점으로 구성한다. 기존 Animator Controller는 유지한다.

Inspector의 `Interval Seconds`와 **문 방문 이벤트의** `Watch Seconds`는 **X=최소, Y=최대(초)**다. 두 값을 같게 설정하면 고정 시간이다. 창문 이벤트의 `Watch Seconds`는 단일 시간이다. 이벤트 간격 변경은 다음 대기 예약부터, 관찰 시간 변경은 다음 관찰 구간부터 반영된다. 음수와 역전된 범위는 Inspector에서 보정한다.

## 개발자 도구

MainGame의 `DeveloperToolsCanvas`에 미리 배치한 텍스트 버튼 UI다. 사용자가 추가한 배경 스타일을 유지하며, `[개발자 도구]`를 누르면 열한 개 버튼을 펼치거나 접는다. UI를 열어도 게임은 계속 진행된다. 새 버튼도 기존 텍스트 버튼을 복제하며 별도 Image는 사용하지 않는다.

MainGame의 기존 `EventSystem`은 **씬 최상위**에 둔다. 숨길 수 있는 테스트 Canvas `UIsRoot` 아래에 두면 Canvas 비활성화와 함께 입력 모듈도 꺼져, 다른 Canvas의 개발자 도구·Retry 버튼까지 클릭되지 않는다. Canvas의 표시 상태와 입력 시스템 수명을 분리하고, Additive 미니게임의 EventSystem은 기존 매니저 정책대로 비활성화해 활성 시스템을 하나로 유지한다.

| 버튼 | 동작 |
| --- | --- |
| 다음 이벤트 즉시 실행 | 진행 중인 이벤트의 소리·이동·문 연출을 취소하고, 등록된 이벤트 중 하나를 즉시 무작위 실행한다. 현재 이벤트와 같은 종류가 다시 선택될 수도 있다. |
| 문 방문 즉시 실행 | 현재 연출을 취소한 뒤 일반 문 방문을 확정 실행한다. |
| 돌진 방문 즉시 실행 | 현재 연출을 취소한 뒤 문 앞에서 뜸을 들이고 달려오는 방문을 확정 실행한다. |
| 창문 등장 즉시 실행 | 현재 연출을 취소한 뒤 침대 옆 창문 등장 연출을 확정 실행한다. |
| 옷장 등장 즉시 실행 | 현재 연출을 취소한 뒤 옷장 등장과 방문 퇴장을 확정 실행한다. |
| 컴퓨터 등장 즉시 실행 | 현재 연출을 취소한 뒤 컴퓨터 모니터 등장과 방문 퇴장을 확정 실행한다. |
| 발소리만 즉시 실행 | 현재 연출을 취소한 뒤 발소리만 들리는 안전한 연출을 확정 실행한다. |
| 플래피버드 미니게임 시작 | 현재 Additive 미니게임을 정리하고 MiniGame02를 새로 로드한다. Space 입력 전에는 Ready 상태다. |
| 스네이크 미니게임 시작 | 현재 Additive 미니게임을 정리하고 MiniGame01을 새로 로드한다. 같은 스네이크를 선택해도 새 인스턴스로 교체한다. |
| 공룡 미니게임 시작 | 현재 Additive 미니게임을 정리하고 MiniGame04를 새로 로드한다. Space 입력 전에는 Ready 상태다. |
| 좌우 분류 미니게임 시작 | 현재 Additive 미니게임을 정리하고 MiniGame03을 새로 로드한다. Space 입력 전에는 Ready 상태다. |

- 미니게임 교체는 목숨·누적 클리어 수·엄마 이벤트를 유지한다. Tab 정지는 이전 컨트롤러와 함께 정리하고 새 게임은 시작 대기로 들어간다.
- Shift를 누른 상태라면 새 미니게임도 기존 숨김 정책을 따른다.
- 전환 중 중복 미니게임 요청을 받지 않는다. 게임오버·목표 달성 후에는 도구를 사용할 수 없다.
- `UNITY_EDITOR || DEVELOPMENT_BUILD` 조건에서만 표시·실행한다. 일반 빌드에서는 UI 루트를 숨기고 개발용 미니게임 전환 API도 요청을 거부한다.
- 이벤트 즉시 실행 API는 `MainGameEventDirector.TryRunNextEventImmediately()`, 개발용 지정 미니게임 전환 API는 `MiniGameManager.TryLoadMiniGameForDevelopment(string)`이다. UI에서 직접 씬을 로드하거나 목숨을 초기화하지 않는다.
- 이벤트별 지정 실행은 `MainGameDeveloperTools.RunEventImmediately(MainGameEventBase)` → `MainGameEventDirector.TryRunEventForDevelopment(MainGameEventBase)`로 연결한다. 확률이 0이어도 실행하며 가중치·미니게임·목숨·클리어 수는 변경하지 않는다. 같은 이벤트를 다시 누르면 취소 후 처음부터 시작한다. 완료 뒤의 다음 대기는 기존 무작위 확률을 사용한다.
- 지정 대상은 Director 목록에 등록되어 있고 활성 상태이며 필수 참조가 유효해야 한다. 잘못된 대상은 현재 이벤트를 취소하지 않는다. `CanRunEventForDevelopment`로 버튼 활성 상태도 같은 조건에 맞추며 일반 배포 빌드·게임오버·목표 달성 이후에는 지정 실행을 거부한다.
- 새 지정 버튼을 추가할 때는 `ToolsPanel`에 텍스트 버튼을 배치하고 `Event Shortcuts`에 버튼·이벤트를 등록한다. Button OnClick은 `RunEventImmediately`에 해당 이벤트를 Object 인자로 저장한다. 이벤트 타입별 메서드나 분기문을 추가하지 않는다.
- 클릭 검증은 Button.onClick이나 ExecuteEvents 직접 호출만으로 끝내지 않는다. `MainGameDeveloperToolsTests`의 Mouse 테스트는 실제 입력 액션·InputSystemUIInputModule·EventSystem을 거쳐 메뉴, 기존 테스트 UI, Retry와 재시작 후 클릭까지 확인한다.

## 이벤트 추가

1. `MainGameEventBase`를 상속한 새 컴포넌트를 만든다. 한국어 개발자 주석을 포함한다.
2. `CanRun`에서 Inspector 필수 참조를 검사하고, `ExecuteEvent`에서 해당 연출이 완전히 끝날 때까지 기다린다.
3. `IsDetectionActive`에 그 이벤트만의 발각 구간을 반환한다. 문이나 엄마가 없는 이벤트도 가능하며 항상 `false`인 연출도 등록할 수 있다.
4. `OnCancelled`에서 소리·이동·문 등 해당 이벤트가 사용한 자원을 정리한다. 취소 시 성공·실패 결과를 새로 보내지 않는다.
5. 씬에 배치하고 Director의 `이벤트별 선택 가중치` 목록에 연결하고 비중을 설정한다. Director에 이벤트 타입별 분기문을 추가하지 않는다.

엄마가 등장하는 이벤트는 `MotherActorController`를 공유한다. 다른 등장 방법도 개별 이동 지점과 발각 구간을 가진 이벤트로 추가하면 된다. Director가 한 번에 한 이벤트를 실행한다.

## 입력

| 입력 | 미니게임 화면 | 미니게임 진행 | 엄마 이벤트 |
| --- | --- | --- | --- |
| 없음 | 표시 | 진행 | 진행 |
| Tab | 표시 | 정지 | 진행 |
| Shift만 유지 | 숨김 | 진행 | 진행 |
| Tab으로 정지 후 Shift 유지 | 숨김 | 정지 | 진행 |

Shift를 놓으면 화면이 돌아오고 기존 Tab 상태를 유지한다. 숨긴 동안에는 Tab 전환을 받지 않는다. 전역 `Time.timeScale = 0`은 기존 MainGame 게임오버에서만 사용한다.

## 좌우 시선 조작

`Base/Main Camera`에 `MainGameCameraLookController`를 미리 배치하고 같은 씬의 MiniGameManager를 연결한다. **Shift로 미니게임 화면을 숨긴 동안** 좌우 화살표를 누르거나, 방 화면에서 마우스 왼쪽 버튼을 누른 채 가로로 드래그한다. 왼쪽은 책상·모니터 방향, 오른쪽은 창문 방향이다. 드래그는 마우스가 가로로 움직이는 프레임에 화살표와 같은 각속도와 좌우 한계를 적용한다. UI에서 시작한 드래그는 UI 입력으로 처리하고, 두 입력이 겹치면 화살표를 우선한다. 화면을 다시 올리면 마지막 시선을 유지하며, 전체 MainGame을 Retry할 때 최초 시선으로 돌아온다.

Inspector에서 다음 항목을 조절한다.

- `Left Yaw Offset`: 최초 카메라 회전으로부터 왼쪽 끝까지의 수평 각도(도). 현재 약 `-50.17`이며 책상·모니터 방향이다.
- `Right Yaw Offset`: 오른쪽 끝까지의 수평 각도(도). 현재 약 `30.52`이며 창문 방향이다.
- `Rotation Speed Degrees Per Second`: 좌우 공통 각속도(도/초). 현재 약 `66.89`로, 기존 책상 방향의 `50.17° / 0.75초` 속도를 유지한다. 왼쪽 끝까지 약 0.75초, 오른쪽 끝까지 약 0.46초가 걸리며 양끝 사이에서도 같은 각속도로 회전한다. 한계 각도를 바꿔도 속도는 유지된다.

카메라 위치·최초 상하 각도·기울기·시야각과 Additive 미니게임 카메라는 유지한다. 창틀 전체를 보이게 하기 위한 자동 상향 회전과 FOV 보정은 제거했다. Tab 정지, 엄마 이벤트, 목숨과 누적 클리어 수도 이 컴포넌트의 책임이 아니다. 매니저의 Update와 Director의 발각 판정 뒤 LateUpdate에서 회전하며, 게임오버·목표 달성 뒤에는 시선 입력을 받지 않는다. 미니게임 교체 중에도 숨김 상태라면 조작할 수 있다. Inspector 각도는 현재 방 배치와 가로 화면을 기준으로 설정했다.

PlayMode `MainGameCameraLookTests`는 좌우 키와 마우스 드래그의 방향·각속도·양끝 제한, 수평 회전과 FOV, Tab 정지와 메인 시간, 미니게임 교체 및 전체 재시작을 검증한다. `MainGameDeveloperToolsTests`는 UI에서 시작한 드래그가 시선을 돌리지 않는지도 검증한다.

PlayMode의 `MainGameEventIntegration` 카테고리는 38:7:10:10:10:25 가중치 경계, 문 방문과 돌진, 창문 개폐, 옷장과 컴퓨터의 등장·방문 퇴장, 발각과 취소, 공통 외형, 개발자 버튼을 검증한다. `MiniGamePauseIntegrationTests`는 입력·숨김 조합을 검증한다. 목표 18회 달성 시 이벤트는 중단되지만 엔딩 화면은 별도 구현 영역이다.
