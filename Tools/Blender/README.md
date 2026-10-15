# Blender 모델 생성 스크립트

엄마 캐릭터와 휴대용 게임기를 코드로 생성한다. Blender 5.2에서 확인했다.

| 스크립트 | 결과물 | Unity 메뉴 |
| --- | --- | --- |
| `build_mother.py` | `Assets/_Art/Models/Mother.fbx` (로우폴리 엄마) | `Tools > Art > Setup Mother and Handheld Console Prefabs` |
| `build_handheld_console.py` | `Assets/_Art/Models/HandheldConsole.fbx` | 위와 같음 |
| `build_mother_realistic.py` (+ `mother_hair.py`) | `Assets/_Art/Models/MotherRealistic.fbx`와 텍스처 | `Tools > Art > Setup Mother Realistic Prefab` |

## 로우폴리 모델 다시 생성하기 (프로젝트 루트에서)

```bash
blender -b --factory-startup --python Tools/Blender/build_mother.py -- Assets/_Art/Models/Mother.fbx
blender -b --factory-startup --python Tools/Blender/build_handheld_console.py -- Assets/_Art/Models/HandheldConsole.fbx
```

## 사실적인 엄마(MotherRealistic) 다시 생성하기

필요 조건:
- [MPFB 2](https://extensions.blender.org/add-ons/mpfb/) 확장을 Blender `user_default` 저장소에 설치하고 켠다. 2.0.18에서 확인했다.
- [MakeHuman system assets 팩(CC0)](https://static.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html)을 MPFB 데이터 폴더에 설치한다. MPFB의 `Library settings > Load pack from zip file`로 설치하거나, `%APPDATA%\Blender Foundation\Blender\5.2\extensions\.user\user_default\mpfb\data`에 직접 압축을 푼다.
- `--factory-startup`을 쓰지 않는다. 이 옵션을 쓰면 확장이 꺼져 MPFB를 찾지 못한다.

```bash
blender -b --python Tools/Blender/build_mother_realistic.py -- Assets/_Art/Models/MotherRealistic.fbx Assets/_Art/Textures/MotherRealistic
```

세 번째 인자로 `.blend` 경로를 주면 Blender 미리보기 파일도 저장한다.

## 엄마 애니메이션 (Mixamo → Humanoid)

1. Mixamo에서 FBX를 받는다. 형식은 FBX for Unity, 30fps이고, 이동 클립은 In Place를 권장한다.
2. Mixamo 캐릭터 메시를 공개 저장소에 올리지 않도록 메시를 지우고 뼈대와 애니메이션만 남긴다.
   ```bash
   blender -b --factory-startup --python Tools/Blender/strip_mixamo_mesh.py -- <받은.fbx> Assets/_Art/Animations/Mother/Mother_Run.fbx
   ```
   이 스크립트는 바인드 포즈를 남기려고 1mm 삼각형 하나를 함께 넣는다. 이 삼각형이 없으면 rest pose가 첫 프레임 자세로 바뀌어 리타게팅 결과가 틀어진다.
3. Unity에서 `Tools > Art > Setup Mother Animations`를 실행한다.
   - Humanoid 임포트, 반복 재생, 루트 회전·높이 굽기를 설정한다.
   - `Mother.controller`를 만든다. Speed가 0.1 이상이면 Run, 미만이면 Idle이다.
   - `MotherRealistic.prefab`에 Controller를 연결하고 Apply Root Motion을 끈다.
   - 이동과 Speed 값은 게임 스크립트가 정한다.

## 공통 규칙

- 머티리얼(`Assets/_Art/Materials/...`)과 프리팹(`Assets/_Art/Prefabs`)은 없을 때만 만든다. 이미 있는 것은 덮어쓰지 않으므로 Inspector에서 조정한 값이 유지된다.
- FBX 임포트 설정, 머티리얼 리맵, Humanoid 매핑은 메뉴를 실행할 때마다 다시 맞춘다.
- 프리팹은 FBX를 참조한다. FBX를 다시 생성하면 형태 변경이 자동으로 반영된다.
- Blender 머티리얼 이름과 Unity `.mat` 이름이 같아야 리맵된다. 이름을 바꿀 때는 양쪽을 함께 바꾼다.

## 구조 요약

| 모델 | 구조 |
| --- | --- |
| Mother (로우폴리) | 메시 1개와 머티리얼 6개, 본 21개(손가락 없음). 강체 스키닝, T-포즈. 정면은 +Z, 원점은 발바닥이다. |
| MotherRealistic | MPFB 몸(40대 동아시아 여성, 키 약 1.60m)에 직접 만든 옷과 머리를 더했다. 상의는 긴소매 크림색 바탕 검정 줄무늬 티셔츠, 하의는 일자 청바지, 흰 양말에 적갈색 곱슬 펌이다. 안경은 없다. `mixamorig:*` 본 64개(손가락·눈·턱 포함)를 Humanoid 55개 슬롯에 매핑했다. 메시 8개, 삼각형 약 4.9만 개(머리 약 2.4만), T-포즈, 정면 +Z. |
| HandheldConsole | 프리팹 루트 아래 `Model`(Y축 180° 회전)이 있다. 그 아래에 `HandheldConsole_Body`, `Screen`(전용 머티리얼, UV 0~1), `LeftStick`과 `RightStick`(피벗이 스틱 밑동)이 있다. 화면은 루트의 -Z를 향한다. |

MotherRealistic 머리 폴리곤을 줄이려면 `mother_hair.py`의 `CURL_COUNT`를 낮춘다(컬 1개당 약 100 삼각형).

## 라이선스

MotherRealistic의 몸, 피부, 눈, 눈썹, 속눈썹 텍스처와 청바지 원본(female_casualsuit01)은 MakeHuman 커뮤니티의 **CC0** 에셋이다. 출처 표기 의무는 없다. 줄무늬 티셔츠, 양말, 곱슬머리와 그 텍스처는 이 스크립트가 절차적으로 생성한다.
