# MiniGame02 — 플래피버드

`Assets/Scenes/MiniGame02.unity`를 열고 Play → Space로 시작한다.
시작 입력은 한 번의 초기 상승으로 연결되며, 이후 Space를 놓고 다시 누를 때마다 상승한다.
Tab은 로컬 일시정지/재개, 실패 후 Space를 놓고 다시 누르면 재시도한다.

## 확정 규칙

- 파이프 15쌍을 통과하면 성공한다.
- 파이프 또는 플레이 영역 위·아래 경계에 접촉하면 실패한다.
- 새의 전체 판정 영역이 파이프 뒤쪽을 벗어난 뒤 해당 쌍을 한 번만 집계한다.
- Space를 길게 눌러도 연속 상승하지 않는다. 결과는 실행당 한 번만 공통 `Completed` 이벤트로 전달한다.
- 날개 애니메이션, 회전 연출, 트윈, Animator, 오디오는 넣지 않았다. 새의 상하 이동과 파이프 이동은 게임 규칙에 따른 위치 갱신이다.

## 임시 밸런스와 판정

씬의 `MiniGame02 - Flappy Bird` → `FlappyGame > Rules`에서 조정한다.

| 설정 | 기본값 |
| --- | --- |
| 중력 | 14 |
| 상승 속도 | 4.8 |
| 파이프 이동 속도 | 2.8 |
| 파이프 간 거리 | 3.8 |
| 통로 높이 | 3.2 |
| 파이프 폭 | 0.9 |
| 첫 파이프 X | 6 |
| 난수 시드 | -1: 실행마다 무작위, 0 이상: 재현 가능 |

첫 통로는 중앙이고 이후 통로 중심은 이전보다 최대 0.9만큼 변한다.
위치는 16×9 게임 좌표를 사용하며, 새의 판정 크기는 0.6×0.48인 사각형이다.
시각 이미지는 판정보다 약간 크게 표시한다. 에셋 크기/투명 여백과 충돌 영역을 교체 후 확인한다.
해상도가 바뀌어도 고정 비율 플레이 영역과 판정이 함께 축소된다. 화면 위·아래 경계는 색이 밝은 플레이 영역의 위·아래 선이다.
검증한 기본값 외의 극단적인 조합은 수동 밸런싱이 필요하다.

## 아트 교체

- `FlappyView > Bird Sprite`: 팀원 제작 새 Sprite를 연결한다.
- `FlappyView > Pipe Sprite`: 팀원 제작 파이프 Sprite를 연결한다. 현재는 Unity UI 기본 사각형과 테두리를 사용한다.
- 파이프는 통로 밖 영역 길이에 맞춰 늘어나므로 늘려도 되는 이미지 또는 테두리 설정을 권장한다. 위쪽 전용 그림이 필요하면 전용 View에서 처리한다.
- 색상과 텍스트 배치는 씬의 `Content` 아래에서 편집한다.
- `Tools > Mini Games > 02 Flappy Bird > Open or Create Scene`은 이미 구성된 씬을 다시 만들지 않는다.
- 기존 MiniGame02의 테스트 오브젝트는 비활성 `Legacy placeholders - inactive` 자식 아래 보존하며 기존 씬 GUID를 유지한다. 씬 루트를 다시 켜도 이 보관 영역은 비활성 상태를 유지한다.

`Bird.png`는 내장 imagegen으로 생성한 임시 정적 아트다. 생성 프롬프트:

> One original static sprite for a Flappy Bird minigame: cute compact round yellow bird facing right, orange beak, single dark eye, small folded wing. Flat 2D cartoon, dark teal outline, crisp readable silhouette. Single bird fills 85% of square canvas, centered. Transparent background. No text, shadow, scenery, animation frames or accessories.

## 연결 및 검증 범위

- `FlappyGame : MiniGameController`, `GameId = MiniGame02`이며 기존 시작·정지·재개·결과 API를 사용한다.
- 전용 `FlappyView`가 시작/결과 UI를 담당한다. `ExternalPauseControl`이면 로컬 정지 패널만 숨긴다.
- 공통 코드, 전체 매니저, 메인게임, 엄마 시스템, 전체 씬 선택 목록과 빌드 설정은 변경하지 않는다. 전체 연동 검증은 담당 협업자가 수행한다.
- 테스트: EditMode `FlappyTests`, PlayMode `FlappySceneTests`. 다른 시스템 없이 MiniGame02 단독으로 실행한다.
- 2026-10-16: Unity 6000.3.23f1 격리 검증 프로젝트에서 EditMode 13/13, PlayMode 1/1 통과. URP 환경에서도 PlayMode 통과와 시작·플레이·정지·실패 화면 렌더를 확인했다. 실제 프로젝트의 전체 게임 연동 및 사람이 느끼는 난이도 평가는 별도다.
