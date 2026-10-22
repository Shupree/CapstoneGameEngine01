# MainGame 이벤트

`MainGameEvents`는 씬에 미리 배치한 이벤트 구성이다. 추가 UI Image는 사용하지 않으며 게임오버는 기존 `MiniGameManager`의 `Retry?` 화면으로 연결한다.

## 책임과 연결

- `MainGameEventDirector`: 최초 진입 및 이벤트 완료 뒤 30~120초를 무작위로 기다린다. Inspector의 `Events` 목록에서 실행 가능한 항목 하나를 선택하며 중복 실행하지 않는다.
- `MainGameEventBase`: `CanRun`, `TryBegin()`, `IsRunning`, `IsDetectionActive`, `Cancel()`의 공통 계약이다. 개별 연출은 `ExecuteEvent()`, 취소 정리는 `OnCancelled()`에서 구현한다.
- `MotherDoorVisitEvent`: 발걸음 재생, 문과 엄마의 방문 순서, 발각 구간을 관리한다.
- `MotherActorController`: 모델의 이동·회전과 Animator의 `Speed`를 제어한다. 이동 중 기존 Run을 0.45배로 재생하고 관찰 중 Idle은 1배로 복원한다. Root Motion은 사용하지 않는다.
- `DoorController`: 기존 문 회전 API를 재사용한다.
- `MiniGameManager`: Shift 숨김 상태를 제공하고 게임오버·전체 재시작을 담당한다. `GameOverTriggered`와 `GameFinished`를 받으면 Director가 진행 중 이벤트와 다음 타이머를 취소한다.

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

## 씬에서 조절할 항목

- `MainGameEvents`: 매니저 참조, 이벤트 목록, 대기 범위.
- `MotherDoorVisit`: 문·배우·Step_Walk AudioSource/Clip, 진입·퇴장 경로, 관찰 대상, 소리 뒤 대기와 발각 유예, 관찰 범위.
- `MotherActor`: 모델/Animator 참조, 이동 속도, 회전 속도, Run 재생 속도.
- `DoorVisitWaypoints`: 문 밖, 문턱, 문 안, 방 통로, 침대 옆, 플레이어 시선 목표. 방 배치를 바꾸면 경로가 가구와 겹치지 않도록 함께 조정한다. 지점의 높이는 발이 닿을 바닥 높이로 맞춘다.

엄마 프리팹 인스턴스는 편집 시 비활성으로 저장하고 이벤트가 시작할 때 표시한다. 원본 프리팹과 Animator Controller를 수정하지 않고 씬 인스턴스로 구성한다.

## 이벤트 추가

1. `MainGameEventBase`를 상속한 새 컴포넌트를 만든다. 한국어 개발자 주석을 포함한다.
2. `CanRun`에서 Inspector 필수 참조를 검사하고, `ExecuteEvent`에서 해당 연출이 완전히 끝날 때까지 기다린다.
3. `IsDetectionActive`에 그 이벤트만의 발각 구간을 반환한다. 문이나 엄마가 없는 이벤트도 가능하며 항상 `false`인 연출도 등록할 수 있다.
4. `OnCancelled`에서 소리·이동·문 등 해당 이벤트가 사용한 자원을 정리한다. 취소 시 성공·실패 결과를 새로 보내지 않는다.
5. 씬에 배치하고 Director의 `Events` 목록에 연결한다. Director에 이벤트 타입별 분기문을 추가하지 않는다.

창문 방문은 별도 이벤트에서 다른 이동 경로와 발각 구간을 정하고 `MotherActorController`를 재사용하면 된다. 같은 배우를 쓰는 이벤트도 Director가 하나씩 실행하므로 겹치지 않는다.

## 입력

| 입력 | 미니게임 화면 | 미니게임 진행 | 엄마 이벤트 |
| --- | --- | --- | --- |
| 없음 | 표시 | 진행 | 진행 |
| Tab | 표시 | 정지 | 진행 |
| Shift만 유지 | 숨김 | 진행 | 진행 |
| Tab으로 정지 후 Shift 유지 | 숨김 | 정지 | 진행 |

Shift를 놓으면 화면이 돌아오고 기존 Tab 상태를 유지한다. 숨긴 동안에는 Tab 전환을 받지 않는다. 전역 `Time.timeScale = 0`은 기존 MainGame 게임오버에서만 사용한다.

PlayMode의 `MainGameMotherEventIntegrationTests`와 `MiniGamePauseIntegrationTests`가 시간·발각·재시작·숨김 조합을 검증한다. 목표 18회 달성 시 이벤트는 중단되지만 엔딩 화면은 별도 구현 영역이다.
