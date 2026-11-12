# 화면 셰이더(RetroScreen) 변경 가이드

MainGame의 **방 화면**과 **게임기(미니게임) 화면**에 걸리는 레트로/CRT 효과를 바꾸는 방법이다.
두 화면은 따로 조절되며, 한쪽을 바꿔도 다른 쪽에 영향이 없다. 구조 설명은 [README](README.md)에 있다.

---

## 1. 어디를 바꾸나 (한눈에 보기)

| 바꾸고 싶은 것 | MainGame 하이어라키에서 선택할 오브젝트 | Inspector에서 볼 곳 |
| --- | --- | --- |
| 방 화면의 색감·효과 | `Base/Global Volume` | `Volume` 컴포넌트 → Profile → **GE/Retro Screen** |
| 방 화면 효과 켜기/끄기·전체 강도 | `Base/Main Camera` | **Retro Screen Camera** (체크박스, `Intensity`) |
| 게임기 화면(모든 미니게임)의 값·켜기/끄기·강도 | `Base/Global Volume` | **Retro Screen MiniGame Applier** |
| 특정 미니게임 하나만 다르게 | 해당 미니게임 씬의 카메라 | **Retro Screen Camera**를 직접 추가 ([5장](#5-특정-미니게임만-다른-값-쓰기)) |
| 방의 어둡기·그림자·달빛, 게임기 화면 빛 | `Lighting` 창, `Base/Directional Light`, `Base/MiniGameScreenLight` | [9장](#9-방-조명그림자와-게임기-화면-빛) (셰이더가 아닌 조명) |

> 방 화면 값은 **프로필 에셋**(`Assets/Scenes/SampleMap/Global Volume Profile.asset`)에 저장된다.
> 게임기 화면 값은 **MainGame 씬**에 저장된다. 둘 다 바꾼 뒤 저장(Ctrl+S)을 잊지 않는다.

---

## 2. 방 화면 바꾸기

1. MainGame 씬을 연다.
2. Hierarchy에서 `Base/Global Volume`을 선택한다.
3. Inspector의 `Volume` 컴포넌트에서 **GE/Retro Screen** 항목을 펼친다.
4. 바꾸려는 항목의 **왼쪽 체크박스(Override)를 켜고** 값을 조절한다.
   - 체크박스가 꺼진 항목은 기본값(효과 없음)으로 처리된다.
5. Game 뷰에서 바로 확인할 수 있다. Play 하지 않아도 보인다.

방 효과만 잠시 끄거나 약하게 하려면 `Base/Main Camera`의 **Retro Screen Camera**에서

- 체크박스 해제 → 방 화면 효과 끔
- `Intensity`(0~1) → 전체 강도 조절

> `Retro Screen Camera`의 `Source`는 **Volume**으로 둔다.
> `Custom`으로 바꾸면 Global Volume 값 대신 컴포넌트 아래쪽 `Custom Settings` 값을 쓴다.

---

## 3. 게임기 화면(미니게임) 바꾸기

1. MainGame 씬에서 `Base/Global Volume`을 선택한다.
2. Inspector의 **Retro Screen MiniGame Applier**에서 조절한다.
   - `Apply To Mini Games`: 끄면 모든 미니게임 화면에서 효과가 빠진다.
   - `Intensity`: 전체 강도(0~1).
   - `Mini Game Settings`: 실제 효과 값(항목 설명은 [4장](#4-항목별-의미)).
3. **확인은 Play 모드에서 한다.** 미니게임은 Play 중에 로드되므로, 편집 화면에서는 게임기 화면이 보이지 않는다.

### Play 중에 조절할 때 주의

Play 중 Inspector에서 바꾼 값은 바로 화면에 반영되지만, **Play를 멈추면 원래대로 돌아간다**(Unity 기본 동작).
마음에 드는 값을 찾았으면 다음 순서로 옮긴다.

1. Play 중 `Retro Screen MiniGame Applier` 컴포넌트 제목 오른쪽 `⋮` → **Copy Component**
2. Play 정지
3. 같은 컴포넌트 `⋮` → **Paste Component Values**
4. 씬 저장(Ctrl+S)

> 방 화면의 Volume 프로필은 에셋이라 Play 중에 바꿔도 **멈춘 뒤에도 그대로 남는다.** 실험 후 되돌릴 값을 미리 적어 둔다.

---

## 4. 항목별 의미

| 항목 | 범위 | 효과 | 팁 |
| --- | --- | --- | --- |
| `Curvature` | 0 ~ 0.5 | 브라운관처럼 화면이 볼록하게 휜다 | 0.02~0.08이면 자연스럽다. 너무 크면 가장자리가 많이 늘어난다 |
| `Pixel Size` | 1 ~ 16 | 화면을 큰 픽셀로 깨뜨린다 | 1 = 픽셀화 없음. 3~5면 레트로 게임 느낌 |
| `Red / Green / Blue Levels` | 0 ~ 64 | 채널별 색 단계 수를 줄인다 | 0 = 제한 없음. 4~16이면 옛날 게임기 색감 |
| `Dither 4` / `Dither 8` | 0 ~ 1 | 색 단계 사이를 점 무늬(Bayer)로 섞는다 | 색 단계를 줄였을 때 계단 현상을 부드럽게 한다 |
| `Desaturation` | 0 ~ 1 | 채도를 뺀다 | 1 = 완전 흑백 |
| `Tint` | 색 | 화면 전체에 곱하는 색 | 흰색 = 변화 없음. 어두운 색일수록 화면이 어두워진다 |
| `Scanline Intensity` | 0 ~ 1 | 가로 줄무늬(스캔라인) 진하기 | 0.1~0.35가 무난하다 |
| `Scanline Size` | 1 ~ 16 | 스캔라인 한 줄의 높이(화면 픽셀) | 해상도가 높으면 2~3으로 키운다 |
| `Vignette` | 0 ~ 1 | 모서리를 어둡게 한다 | 공포 분위기에 효과적이다 |
| `Vignette Smoothness` | 0.01 ~ 1 | 비네트가 중앙으로 퍼지는 범위 | 작을수록 가장자리에 좁고 진하게 생긴다 |
| `Intensity`(카메라·적용기) | 0 ~ 1 | 위 값 전체를 한 번에 약하게 한다 | `Pixel Size`와 색 단계 수는 줄이지 않는다 |

> 모든 값이 기본값(곡률 0, 픽셀 1, 틴트 흰색, 스캔라인·비네트 0 등)이면 효과 패스를 아예 건너뛴다.

---

## 5. 특정 미니게임만 다른 값 쓰기

공통 값 대신 그 게임만의 화면을 쓰고 싶을 때만 사용한다. 미니게임 씬은 팀원 작업 영역이므로, 미리 담당자와 이야기한다.

1. 해당 미니게임 씬(예: `MiniGame06`)을 연다.
2. 그 씬의 카메라를 선택하고 `Add Component` → **GE/Rendering/Retro Screen Camera**를 추가한다.
3. `Source = Custom`으로 두고 `Custom Settings`를 조절한다.
   - 기존 Volume 값에서 시작하려면 컴포넌트 `⋮` → **Copy Values From Volume Profiles In Scene**.
4. 그 게임만 효과를 빼고 싶으면 컴포넌트를 추가한 뒤 **체크 해제**한다(삭제하면 공통 값이 다시 붙는다).
5. 씬 저장 후 MainGame에서 Play해 확인한다.

카메라에 이 컴포넌트가 있으면 MainGame의 공통 적용기는 그 카메라를 건드리지 않는다.

---

## 6. 값 예시 (출발점)

아래는 출발점으로 쓸 예시 값이다. 실제 화면을 보며 조절한다.

**방을 더 어둡고 으스스하게**
`Desaturation` 0.35 · `Tint` (R 0.55, G 0.25, B 0.35) · `Vignette` 0.55 · `Vignette Smoothness` 0.4 · `Scanline Intensity` 0.15

**레트로 픽셀 느낌**
`Pixel Size` 3~4 · `Red/Green/Blue Levels` 16 · `Dither 4` 0.5

**게임기 화면을 선명하게**
`Curvature` 0.03 · `Scanline Intensity` 0.15 · `Vignette` 0.25 · `Tint` 흰색

**흑백 휴대용 게임기 느낌**
`Desaturation` 1 · `Tint` (R 0.6, G 0.75, B 0.35) · `Red/Green/Blue Levels` 4 · `Pixel Size` 2

---

## 7. 스크립트로 바꾸기 (예: 엄마 등장 시 방 화면을 더 어둡게)

`Assembly-CSharp`(예: `Assets/Scripts/MainGame`)의 코드에서만 쓸 수 있다. 미니게임 코드(`GE.MiniGames` 어셈블리)에서는 이 타입을 참조할 수 없다.
`GameObject.Find` 대신 Inspector 참조를 연결해서 쓴다.

```csharp
using GE.Rendering;
using UnityEngine;

public class ExampleScreenControl : MonoBehaviour
{
    // Inspector에서 Base/Main Camera와 Base/Global Volume을 연결한다.
    [SerializeField] private RetroScreenCamera roomScreen;
    [SerializeField] private RetroScreenMiniGameApplier miniGameScreen;

    public void OnMotherAppears()
    {
        roomScreen.Intensity = 1f;            // 방 효과 최대
        miniGameScreen.Intensity = 0.5f;      // 게임기 화면 효과 절반

        // 게임기 화면 값 일부만 바꾸기: struct라서 꺼내서 수정한 뒤 다시 넣는다.
        var s = miniGameScreen.MiniGameSettings;
        s.scanlineIntensity = 0.5f;
        miniGameScreen.MiniGameSettings = s;   // 현재 미니게임에 바로 반영된다
    }

    public void TurnOffRoomEffect() => roomScreen.enabled = false;
}
```

- 방 화면은 `Source = Volume`이므로 색감 자체를 스크립트로 바꾸려면 `Source = Custom`으로 바꾼 뒤 `CustomSettings`를 설정한다.
- 스크립트로 바꾼 값은 저장되지 않는다(Play 중 값과 같다).

---

## 8. 효과가 안 보일 때

1. `Assets/Settings/PC_Renderer.asset`의 Renderer Features에서 **Retro Screen Renderer Feature**가 켜져 있는지 확인한다.
2. 방: `Base/Main Camera`의 `Retro Screen Camera` 체크·`Intensity`, Volume 프로필의 **Override 체크박스**를 확인한다.
3. 게임기: `Apply To Mini Games`·`Intensity`를 확인하고 **Play 모드**에서 본다.
4. Scene 뷰에는 기본적으로 효과가 보이지 않는다(피처의 `Show In Scene View`). Game 뷰에서 확인한다.
5. Shift로 게임기를 숨기면 미니게임 카메라가 꺼지므로 게임기 효과도 같이 사라진다(정상).
6. 정지·게임오버 UI(Screen Space - Overlay)에는 효과가 걸리지 않는다(정상).

---

## 9. 방 조명·그림자와 게임기 화면 빛

셰이더(색감)와 별개로, 방의 밝기와 그림자는 **조명**이 정한다. 모두 MainGame 씬에 저장된다.

| 바꾸고 싶은 것 | 위치 | 현재 값 |
| --- | --- | --- |
| 방 전체의 기본 밝기(그림자 진하기) | `Window › Rendering › Lighting` → Environment → **Environment Lighting** (Source = Gradient) | Sky (0.07, 0.085, 0.125), Equator (0.045, 0.05, 0.07), Ground (0.02, 0.02, 0.028) |
| 반사 밝기 | 같은 창 → Environment Reflections → `Intensity Multiplier` | 0.3 |
| 달빛(창문 그림자) | `Base/Directional Light` | 푸른빛 (0.55, 0.65, 1), Intensity 1.6, 회전 (14, 100, 0), Soft Shadows |
| 게임기 화면 빛 | `Base/MiniGameScreenLight` (Point Light + **Mini Game Screen Glow**) | 아래 표 |
| 창밖 밤 풍경 | `Map/NightView` (창마다 `_Sky`·`_Trees` 배경판, Unlit) | 아래 "창밖 밤 풍경" |

- **그림자를 더 진하게** 하려면 Environment Lighting 세 색을 더 어둡게 한다. 그림자 속은 이 색으로만 밝혀진다.
  너무 어두우면 숨은 상태(Shift)에서 문·엄마가 잘 안 보이므로 실제 화면에서 확인한다.
- **달빛 위치**는 Directional Light의 회전으로 옮긴다. 지금은 침대 뒤쪽(서쪽) 창으로 들어와 옷장·책장에 창틀 모양 빛을 만든다.
  X(첫 값)를 키우면 바닥 쪽으로, Y를 바꾸면 좌우로 움직인다.
- 천장 조명(`Map/Light_Ceiling6`)은 Baked 모드인데 라이트맵이 없어서 화면에 영향이 없다(밤이라 꺼진 상태로 둠).

### 창밖 밤 풍경

두 창(`Map/wallWindowSlide`, `Map/wallWindowSlide (1)`) 너머에 배경판을 두 장씩 세웠다. 조명을 받지 않는 Unlit이고 그림자를 드리우지 않아 달빛 그림자를 가리지 않는다.

- `<창>_Sky`(35m 거리): 별이 있는 남색 하늘, 지평선의 도시 불빛 번짐, 먼 산 능선, 창 일부에 불이 켜진 아파트 실루엣. 텍스처는 `Assets/_Art/Textures/NightView/T_NightSky_<창>.png`.
- `<창>_Trees`(14m 거리): 나무·전봇대·전선 실루엣(알파 컷아웃). 침대에서 올려다보는 각도라 지금 구도에서는 전선 정도만 보인다.
- 침대에서 보면 창이 눈높이보다 위에 있어 창으로는 주로 하늘이 보이고, 아파트 지붕과 불빛은 창 아래쪽에 걸친다. 달은 달빛이 오는 서쪽에 그리므로 두 창 모두 화면 밖이며, 그쪽 하늘이 조금 밝다.
- 유리(반투명, 알파 0.5)가 배경을 절반쯤 어둡게 하므로 텍스처를 실제보다 밝게 그렸다. 더 어둡게·밝게 하려면 `Assets/_Art/Materials/NightView/M_NightSky_<창>.mat`의 Base Color를 조절한다(흰색이 원래 밝기).
- 다시 만들기: `Tools > Art > Build Night Window Views (Active Scene)`(`Assets/_Art/Editor/NightViewBuilder.cs`). 카메라 위치나 창 위치·크기를 바꾸면 다시 실행한다. 창 너머 시야가 배경판 범위를 벗어나면 Console에 경고가 나온다. 엄마 창문 등장 지점(창밖 약 2m)보다 훨씬 멀리 있어 등장 연출을 가리지 않는다.

### 게임기 화면 빛 (Mini Game Screen Glow)

미니게임 화면의 평균 색을 0.1초마다 읽어 손 근처의 Point Light 색·밝기로 옮긴다. 화면이 빨가면 방도 붉게, 어두운 게임이면 빛도 약해진다.
실제 화면을 복사해 읽으므로 그 영역에 그려진 정지 오버레이 같은 UI도 색에 섞일 수 있다.

| 항목 | 효과 |
| --- | --- |
| `Max Intensity` | 화면이 가장 밝을 때 빛 세기(기본 2). 방이 너무 밝으면 줄인다 |
| `Min Brightness` | 검은 배경 게임도 최소한 이 비율만큼 빛난다(기본 0.35) |
| `Hidden Intensity Multiplier` | Shift로 숨겼을 때 남길 빛(기본 0 = 완전히 꺼짐). 0.1~0.2면 이불 속 화면이 새어 나오는 느낌 |
| `Snap Off When Hidden` | 켜면(기본) 숨기는 순간 빛이 바로 꺼진다. 끄면 `Fade Speed`로 서서히 줄어든다. 다시 꺼낼 때는 항상 서서히 켜진다 |
| `Follow Screen Color` / `Color Saturation` | 화면 색 따라가기 켜기·끄기 / 0이면 흰빛, 1이면 화면 색 그대로 |
| `Fallback Color` | 색을 따라가지 않을 때(또는 첫 샘플 전) 빛 색 |
| `Sample Interval` / `Fade Speed` | 색을 읽는 간격 / 바뀌는 속도. Fade Speed가 크면 깜빡이듯 빠르게 바뀐다 |

- Point Light 자체의 `Range`(기본 6)와 그림자 설정은 같은 오브젝트의 **Light** 컴포넌트에서 바꾼다. `Intensity`는 스크립트가 매 프레임 덮어쓰므로 Light에서 바꾸지 않는다.
- 빛은 Play 모드에서만 켜진다(편집 화면에서는 세기 0). 미니게임이 없거나 게임오버면 꺼진다.
- 빛 위치는 손 근처(카메라 앞 0.35m, 아래 0.15m)다. 좌우 시선 회전은 숨김 상태에서만 일어나고 그때는 빛이 꺼져 있으므로 카메라를 따라가지 않는다.

---

## 참고: 꺼 둔 CRT-Free 효과

`PC_Renderer`의 **Full Screen Pass Renderer Feature**(CRT-Free 머티리얼)는 꺼 두었다.
Built-in 파이프라인 전용 셰이더라 URP에서 화면 변화가 없었다(켜고 끈 화면이 픽셀 단위로 같음).
모든 카메라에 전체 화면 비용만 들었기 때문에 비활성화했다. 삭제하지 않았으므로 체크박스로 다시 켤 수 있다.
CRT 효과는 RetroScreen의 `Curvature`·`Scanline`·`Vignette`로 대신한다.
