# RetroScreen 카메라별 적용 방법

URP 전체 화면 레트로/CRT 효과(`Hidden/GE/RetroScreen`)를 카메라마다 따로 켜고 끄고 값을 다르게 주는 방법이다.

> 값을 직접 바꿔 보려는 경우에는 [셰이더 변경 가이드](SHADER_GUIDE.md)부터 본다. 이 문서는 구조 설명이다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `RetroScreenRendererFeature.cs` | `Assets/Settings/PC_Renderer.asset`에 등록된 렌더러 피처. 카메라마다 값을 정해 블릿한다. |
| `RetroScreenCamera.cs` | 카메라에 붙이는 설정 컴포넌트. **카메라별 켜기/끄기와 값은 여기서 조절한다.** |
| `RetroScreenSettings.cs` | 셰이더에 넘기는 값 묶음. Volume과 카메라 컴포넌트가 함께 쓴다. |
| `RetroScreenVolume.cs` | Volume Override(`GE/Retro Screen`). 컴포넌트가 없는 카메라의 기본 경로다. |
| `RetroScreenMiniGameApplier.cs` | MainGame에 두는 컴포넌트. Additive로 로드되는 **모든 미니게임 카메라**에 공통 "게임기 화면" 값을 자동으로 붙인다. |

## 카메라마다 값을 정하는 순서

1. 카메라에 `RetroScreenCamera`가 있으면 그 컴포넌트가 정한다.
   - 컴포넌트 체크 해제(`enabled = false`) 또는 `Intensity = 0`이면 **그 카메라만** 효과가 꺼진다. 이때 Volume 값은 무시한다.
   - `Source = Custom`이면 컴포넌트의 `Custom Settings` 값을 쓴다(카메라마다 독립).
   - `Source = Volume`이면 그 카메라의 Volume 스택(Volume Mask·Trigger 기준) 값을 쓴다.
   - `Intensity`(0~1)는 곡률·디더링·채도·틴트·스캔라인·비네트를 비례해서 줄인다. 픽셀 크기와 색 단계 수는 줄이지 않는다.
2. 컴포넌트가 없으면 기존처럼 Volume의 `GE/Retro Screen` 값을 쓴다(Lobby, MiniGame02 등).

## 현재 MainGame 설정 (방과 게임기 화면 분리)

| 화면 | 어디서 조절하나 | 현재 값 |
| --- | --- | --- |
| 방 (`Base/Main Camera`) | 카메라의 `RetroScreenCamera` — `Source = Volume` | 실제 값은 `Base/Global Volume`의 프로필(`Assets/Scenes/SampleMap/Global Volume Profile.asset`). 곡률 0.023, 디더링 4x4·8x8 각 1, 채도 감소 0.121, 핑크 틴트, 스캔라인 0.111, 비네트 0.333 |
| 게임기 화면 (모든 미니게임 카메라) | `Base/Global Volume`의 `RetroScreenMiniGameApplier` | 픽셀화 없음, 곡률 0.06, 스캔라인 0.3, 비네트 0.4, 아주 옅은 초록 틴트 |

- 방 효과만 끄기: `Base/Main Camera`의 `RetroScreenCamera` 체크 해제(또는 `Intensity` 0).
- 게임기 화면 효과만 끄기: `RetroScreenMiniGameApplier`의 `Apply To Mini Games` 해제(또는 `Intensity` 0).
- 미니게임 카메라는 방의 Global Volume 영향을 받지 않는다(적용기가 붙인 컴포넌트가 `Custom` 모드이므로).
- `PC_Renderer`의 CRT-Free `FullScreenPassRendererFeature`는 **비활성**으로 두었다. Built-in 전용 셰이더라 URP에서 화면 변화가 없으면서(켜고 끈 화면이 픽셀 단위로 같음) 모든 카메라에 전체 화면 패스 비용만 들었다. 삭제하지 않았으므로 필요하면 다시 켤 수 있다.

## 특정 미니게임만 다른 화면 값을 쓰기

미니게임 씬의 카메라에 `Add Component › GE/Rendering/Retro Screen Camera`를 직접 붙이면, 적용기는 그 카메라를 **건드리지 않고** 게임 전용 값으로 둔다.

1. 카메라에 컴포넌트를 추가하고 `Custom Settings`를 조절한다. 기존 Volume 값에서 시작하려면 우클릭 메뉴의 `Copy Values From Volume Profiles In Scene`을 쓴다.
2. 그 게임만 효과를 빼려면 컴포넌트를 붙인 뒤 체크 해제한다.
3. 미니게임 씬은 팀원 작업 영역이므로, 공통 값으로 충분하면 씬에 컴포넌트를 붙이지 않는 것을 기본으로 한다.

단독 실행(미니게임 씬만 Play)에는 MainGame의 적용기가 없으므로 미니게임 씬 자체 설정(Volume 등)만 적용된다.

## 스크립트에서 바꾸기

```csharp
// 메인 어셈블리(Assembly-CSharp) 코드에서 사용한다. GE.MiniGames 어셈블리에서는 이 타입을 직접 참조할 수 없다.
var retro = camera.GetComponent<GE.Rendering.RetroScreenCamera>();
retro.enabled = false;          // 이 카메라만 효과 해제
retro.Intensity = 0.5f;         // 전체 강도
var s = retro.CustomSettings;   // struct라서 꺼내서 수정한 뒤 다시 대입한다.
s.scanlineIntensity = 0.2f;
retro.CustomSettings = s;
```

## 주의

- 값은 렌더링할 때마다 읽으므로 Play 중 Inspector 변경이 바로 반영된다.
- viewport가 있는 카메라(미니게임 `0.15, 0.15, 0.7, 0.7`)는 그 영역 안에만 효과가 들어간다. 방 카메라 효과는 미니게임 영역 뒤에 깔려 미니게임 화면에 겹치지 않는다.
- Screen Space - Overlay Canvas(정지·게임오버 UI)에는 효과가 적용되지 않는다.
- Shift로 게임기를 숨기면 매니저가 미니게임 카메라 자체를 끄므로 효과도 함께 사라진다. 다시 보이면 적용기 값이 그대로 남아 있다.
