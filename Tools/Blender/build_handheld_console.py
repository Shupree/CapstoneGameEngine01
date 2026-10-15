# -*- coding: utf-8 -*-
"""
휴대용 게임기(스위치·스팀덱 형태) 로우폴리 모델 생성 스크립트.

실행 (Blender 5.2 기준, 프로젝트 루트에서):
  blender -b --factory-startup --python Tools/Blender/build_handheld_console.py -- Assets/_Art/Models/HandheldConsole.fbx

출력 FBX의 오브젝트 구성 (Unity에서 모델 루트 아래 자식이 된다):
  - HandheldConsole_Body : 본체·베젤·그립·버튼·D패드·숄더버튼 (머티리얼별 서브메시)
  - Screen               : 화면 사각형 1장. 전용 머티리얼(M_Console_Screen), UV 0~1 전체 사용
                           → RenderTexture를 이 머티리얼(또는 교체 머티리얼)에 붙이면 된다.
  - LeftStick/RightStick : 아날로그 스틱. 피벗이 스틱 밑동이라 나중에 기울이기 애니메이션이 가능하다.
스크립트 좌표에서는 화면이 -Y를 향하게 만들고, 내보내기 직전 180° 돌려 Unity에서는 FBX 루트의 +Z가 화면 쪽이 된다.
LeftStick/D패드는 화면을 바라보는 플레이어 기준 왼쪽 그립에 있다.
크기: 폭 약 0.30m × 높이 0.11m × 두께 약 0.03m (1 유닛 = 1m).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import lowpoly_builder as lp  # noqa: E402

# Unity 쪽 .mat 이름과 같아야 ModelImporter 리맵이 연결된다 (LowPolyModelPrefabBuilder.cs 참고).
MATERIALS = {
    "M_Console_Body": (0.10, 0.10, 0.11),
    "M_Console_Grip": (0.16, 0.22, 0.35),
    "M_Console_Bezel": (0.01, 0.01, 0.01),
    "M_Console_Button": (0.75, 0.75, 0.78),
    "M_Console_Stick": (0.05, 0.05, 0.05),
    "M_Console_Screen": (0.02, 0.02, 0.03),
}

# ---- 치수 (미터) ----
BODY_HALF_W = 0.095   # 본체(화면부) 반폭
BODY_HALF_H = 0.052   # 본체 반높이
BODY_DEPTH = 0.016    # 본체 두께
GRIP_X = 0.118        # 그립 중심 X (좌우 대칭)
GRIP_SIZE = (0.060, 0.024, 0.110)
GRIP_Y = 0.002        # 그립을 뒤쪽으로 살짝 밀어 손에 쥐는 두께를 만든다
GRIP_FRONT = GRIP_Y - GRIP_SIZE[1] * 0.5   # 그립 앞면 Y (버튼이 붙는 면)
SCREEN_W, SCREEN_H = 0.150, 0.150 * 9.0 / 16.0  # 16:9 화면
CONTROL_TOP_Z = 0.022
CONTROL_BOTTOM_Z = -0.025


def build_body():
    b = lp.MeshBuilder()
    # 본체와 화면 베젤
    b.box((0, 0, 0), (BODY_HALF_W * 2, BODY_DEPTH, BODY_HALF_H * 2), "M_Console_Body", bevel=0.004, segments=2)
    b.box((0, -BODY_DEPTH * 0.5 - 0.0008, 0.0), (0.172, 0.0016, 0.098), "M_Console_Bezel", bevel=0.0006, segments=1)

    for side in (-1, 1):
        x = GRIP_X * side
        # 좌우 그립 + 뒷면 손잡이 볼록부
        b.box((x, GRIP_Y, 0), GRIP_SIZE, "M_Console_Grip", bevel=0.008, segments=3)
        b.ellipsoid((x * 0.98, 0.016, -0.012), (0.028, 0.016, 0.050), "M_Console_Grip", u_segments=10, v_segments=6)
        # 숄더 버튼(L/R)과 뒤쪽 트리거
        b.box((x, GRIP_Y - 0.001, 0.0565), (0.046, 0.016, 0.006), "M_Console_Button", bevel=0.002, segments=1)
        b.box((x, 0.012, 0.050), (0.036, 0.010, 0.014), "M_Console_Button", bevel=0.003, segments=1)
        # 스틱 받침(링). 스틱 자체는 별도 오브젝트.
        stick_z = CONTROL_TOP_Z if side < 0 else CONTROL_BOTTOM_Z
        b.cylinder((x, GRIP_FRONT + 0.0005, stick_z), (x, GRIP_FRONT - 0.0015, stick_z),
                   0.0115, 0.0110, "M_Console_Stick", segments=16)

    # 왼쪽 아래: D패드 (십자 두 개)
    dx, dz = -GRIP_X, CONTROL_BOTTOM_Z
    b.box((dx, GRIP_FRONT - 0.0012, dz), (0.020, 0.003, 0.0065), "M_Console_Button", bevel=0.0008, segments=1)
    b.box((dx, GRIP_FRONT - 0.0012, dz), (0.0065, 0.003, 0.020), "M_Console_Button", bevel=0.0008, segments=1)

    # 오른쪽 위: ABXY 다이아몬드 배치
    ax, az = GRIP_X, CONTROL_TOP_Z
    for ox, oz in ((0.0, 0.0115), (0.0115, 0.0), (0.0, -0.0115), (-0.0115, 0.0)):
        b.cylinder((ax + ox, GRIP_FRONT + 0.0005, az + oz), (ax + ox, GRIP_FRONT - 0.0025, az + oz),
                   0.0048, 0.0045, "M_Console_Button", segments=12)

    # +/− 버튼, 홈·캡처 버튼 (그립 안쪽 위/아래 모서리)
    for side in (-1, 1):
        x = (GRIP_X - 0.020) * side
        b.box((x, GRIP_FRONT - 0.0008, 0.045), (0.007, 0.0016, 0.0018), "M_Console_Button")
        if side > 0:
            b.box((x, GRIP_FRONT - 0.0008, 0.045), (0.0018, 0.0016, 0.007), "M_Console_Button")
        b.cylinder((x, GRIP_FRONT + 0.0005, -0.046), (x, GRIP_FRONT - 0.0012, -0.046),
                   0.0035, 0.0035, "M_Console_Button", segments=10)
    return b


def build_screen():
    b = lp.MeshBuilder()
    # 베젤 앞면(-0.0096)보다 아주 조금 앞에 둬서 Z-파이팅을 피한다.
    b.quad((0.0, -BODY_DEPTH * 0.5 - 0.0018, 0.002), SCREEN_W, SCREEN_H, "M_Console_Screen")
    return b


def build_stick():
    """원점 = 스틱 밑동, -Y 방향으로 솟은 스틱. 오브젝트 위치로 배치한다."""
    b = lp.MeshBuilder()
    b.cylinder((0, 0.0005, 0), (0, -0.0060, 0), 0.0042, 0.0040, "M_Console_Stick", segments=10)
    b.cylinder((0, -0.0055, 0), (0, -0.0095, 0), 0.0098, 0.0088, "M_Console_Stick", segments=16)
    return b


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_path = os.path.abspath(argv[0] if argv else "HandheldConsole.fbx")

    lp.reset_scene()
    mats = lp.make_materials(MATERIALS)

    build_body().to_object("HandheldConsole_Body", mats)
    build_screen().to_object("Screen", mats)
    for name, side, z in (("LeftStick", -1, CONTROL_TOP_Z), ("RightStick", 1, CONTROL_BOTTOM_Z)):
        stick = build_stick().to_object(name, mats)
        stick.location = (GRIP_X * side, GRIP_FRONT - 0.0015, z)

    bpy.context.view_layer.update()
    lp.face_unity_forward()
    lp.export_fbx(out_path, armature=False)
    print("[build_handheld_console] exported:", out_path)


if __name__ == "__main__":
    main()
