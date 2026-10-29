# MiniGame07 — Bomb

GE_BOMB.html의 폭탄 해체 규칙을 기존 미니게임 계약으로 옮긴 씬입니다.

- 4.7초 안에 상단 색상 순서대로 네 전선을 자릅니다.
- A/D는 새 눌림당 한 칸 이동하며 끝에서 멈춥니다. Space는 선택한 전선을 한 번 자릅니다.
- 전선 배치와 목표 순서를 독립적으로 섞습니다. 이미 절단한 전선을 다시 자르면 무시합니다.
- 순서 오류/시간 초과는 실패, 네 번째 올바른 절단은 즉시 성공입니다.
- 시작 Space가 절단으로 사용되지 않으며, 실패 후 키 해제와 새 Space로 재시도합니다.
- Tab 일시정지·재개와 결과 UI는 기존 MiniGameController/MiniGameTestUI를 사용합니다.
- Z+X+C는 에디터/개발 빌드 성공 치트입니다. 출시 시 BombGame.cs의 DEV_CLEAR_CHEAT #if~#endif 블록을 제거할 수 있습니다.

구조: BombModel(규칙·시간), BombGame(공통 계약 연결), BombView(정적 UI 갱신).
400×300 화면을 CanvasScaler와 ScreenSpaceCamera로 표시합니다. 별도 애니메이션은 없고, 절단된 전선은 중앙 조각을 숨깁니다. 색상 마커의 R/B/Y/G 문자도 함께 제공됩니다. Image 스프라이트는 팀원 에셋으로 교체할 수 있고 전용 정적 폰트를 사용합니다.

씬은 Tools > Mini Games > 07 Bomb > Open or Create Scene에서 열 수 있습니다. 빌더는 이미 있는 씬을 덮어쓰지 않습니다.