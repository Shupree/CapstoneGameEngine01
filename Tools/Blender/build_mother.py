# -*- coding: utf-8 -*-
"""
엄마 캐릭터 로우폴리 모델 + 휴머노이드 뼈대 생성 스크립트.

실행 (Blender 5.2 기준, 프로젝트 루트에서):
  blender -b --factory-startup --python Tools/Blender/build_mother.py -- Assets/_Art/Models/Mother.fbx

구성:
  - Armature  : Unity Humanoid 자동 매핑이 되도록 Unity 표준 이름의 본(Hips, Spine, Chest, Neck, Head,
                Left/Right Shoulder·UpperArm·LowerArm·Hand, Left/Right UpperLeg·LowerLeg·Foot·Toes).
                T-포즈로 만들어 Unity Avatar 생성 시 포즈 보정이 필요 없게 했다. 손가락 본은 없다.
  - Mother_Body : 하나의 스킨드 메시. 파츠별로 한 본에 가중치 1.0(강체 스키닝)으로 묶여 있다.
                  관절에 구체를 겹쳐 굽혔을 때 틈이 덜 보이게 했다.
스크립트 좌표에서는 -Y를 정면(왼쪽 = +X)으로 만들고, 내보내기 직전 180° 돌려 Unity에서는 +Z를 바라본다.
키 약 1.65m, 원점은 발바닥 중앙.
의상: 카디건 상의, 긴 바지, 슬리퍼, 단발머리 (실내에서 밤에 돌아다니는 엄마).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import lowpoly_builder as lp  # noqa: E402

# Unity 쪽 .mat 이름과 같아야 ModelImporter 리맵이 연결된다 (LowPolyModelPrefabBuilder.cs 참고).
MATERIALS = {
    "M_Mother_Skin": (0.87, 0.68, 0.56),
    "M_Mother_Hair": (0.12, 0.08, 0.06),
    "M_Mother_Cardigan": (0.62, 0.42, 0.45),
    "M_Mother_Pants": (0.20, 0.22, 0.28),
    "M_Mother_Slipper": (0.80, 0.74, 0.62),
    "M_Mother_Eye": (0.03, 0.03, 0.03),
}

SHOULDER_Z = 1.37
HIP_Z = 0.93
KNEE_Z = 0.50
ANKLE_Z = 0.09
LEG_X = 0.09

# (이름, head, tail, 부모). 좌우는 모델 기준이며 왼쪽 = +X.
BONES = [
    ("Hips", (0, 0, HIP_Z), (0, 0, 1.05), None),
    ("Spine", (0, 0, 1.05), (0, 0, 1.19), "Hips"),
    ("Chest", (0, 0, 1.19), (0, 0, 1.40), "Spine"),
    ("Neck", (0, 0, 1.40), (0, 0, 1.48), "Chest"),
    ("Head", (0, 0, 1.48), (0, 0, 1.68), "Neck"),
]
for _side, _sx in (("Left", 1.0), ("Right", -1.0)):
    BONES += [
        (_side + "Shoulder", (0.03 * _sx, 0, SHOULDER_Z), (0.15 * _sx, 0, SHOULDER_Z), "Chest"),
        (_side + "UpperArm", (0.16 * _sx, 0, SHOULDER_Z), (0.43 * _sx, 0, SHOULDER_Z), _side + "Shoulder"),
        (_side + "LowerArm", (0.43 * _sx, 0, SHOULDER_Z), (0.67 * _sx, 0, SHOULDER_Z), _side + "UpperArm"),
        (_side + "Hand", (0.67 * _sx, 0, SHOULDER_Z), (0.79 * _sx, 0, SHOULDER_Z), _side + "LowerArm"),
        (_side + "UpperLeg", (LEG_X * _sx, 0, HIP_Z - 0.02), (LEG_X * _sx, 0, KNEE_Z), "Hips"),
        (_side + "LowerLeg", (LEG_X * _sx, 0, KNEE_Z), (LEG_X * _sx, 0, ANKLE_Z), _side + "UpperLeg"),
        (_side + "Foot", (LEG_X * _sx, 0, ANKLE_Z), (LEG_X * _sx, -0.10, 0.025), _side + "LowerLeg"),
        (_side + "Toes", (LEG_X * _sx, -0.10, 0.025), (LEG_X * _sx, -0.16, 0.025), _side + "Foot"),
    ]


def build_body():
    b = lp.MeshBuilder()

    # ---- 몸통 ----
    b.ellipsoid((0, 0, HIP_Z), (0.165, 0.115, 0.11), "M_Mother_Pants", "Hips")
    b.cylinder((0, 0, 0.84), (0, 0, 1.00), 0.195, 0.160, "M_Mother_Cardigan", "Hips", segments=12, squash=(1.0, 0.78))
    b.cylinder((0, 0, 0.98), (0, 0, 1.20), 0.150, 0.150, "M_Mother_Cardigan", "Spine", segments=12, squash=(1.0, 0.76))
    b.cylinder((0, 0, 1.17), (0, 0, 1.36), 0.158, 0.150, "M_Mother_Cardigan", "Chest", segments=12, squash=(1.0, 0.74))
    b.ellipsoid((0, 0, 1.355), (0.185, 0.105, 0.055), "M_Mother_Cardigan", "Chest")

    # ---- 목·머리 ----
    b.cylinder((0, 0, 1.35), (0, 0, 1.50), 0.048, 0.044, "M_Mother_Skin", "Neck", segments=8)
    b.ellipsoid((0, -0.01, 1.57), (0.095, 0.11, 0.12), "M_Mother_Skin", "Head", u_segments=12, v_segments=8)
    b.ellipsoid((0, -0.117, 1.553), (0.014, 0.018, 0.022), "M_Mother_Skin", "Head", u_segments=6, v_segments=4)
    for sx in (1, -1):
        b.ellipsoid((0.035 * sx, -0.108, 1.585), (0.014, 0.008, 0.016), "M_Mother_Eye", "Head", u_segments=8, v_segments=4)
        b.ellipsoid((0.094 * sx, -0.005, 1.565), (0.012, 0.02, 0.026), "M_Mother_Skin", "Head", u_segments=6, v_segments=4)
    # 단발머리: 정수리 캡 + 옆·뒤로 떨어지는 볼륨. 얼굴 앞면은 가리지 않는다.
    b.ellipsoid((0, 0.015, 1.607), (0.104, 0.116, 0.108), "M_Mother_Hair", "Head", u_segments=12, v_segments=8)
    b.ellipsoid((0, 0.035, 1.535), (0.112, 0.098, 0.095), "M_Mother_Hair", "Head", u_segments=12, v_segments=6)

    # ---- 팔 (T-포즈, 손바닥이 아래) ----
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        b.ellipsoid((0.165 * sx, 0, SHOULDER_Z - 0.005), (0.068, 0.064, 0.064), "M_Mother_Cardigan", side + "UpperArm")
        b.cylinder((0.16 * sx, 0, SHOULDER_Z), (0.44 * sx, 0, SHOULDER_Z), 0.056, 0.046, "M_Mother_Cardigan", side + "UpperArm")
        b.ellipsoid((0.435 * sx, 0, SHOULDER_Z), (0.047, 0.047, 0.047), "M_Mother_Cardigan", side + "LowerArm", u_segments=8)
        b.cylinder((0.43 * sx, 0, SHOULDER_Z), (0.66 * sx, 0, SHOULDER_Z), 0.046, 0.040, "M_Mother_Cardigan", side + "LowerArm")
        b.cylinder((0.62 * sx, 0, SHOULDER_Z), (0.665 * sx, 0, SHOULDER_Z), 0.047, 0.047, "M_Mother_Cardigan", side + "LowerArm")
        b.cylinder((0.64 * sx, 0, SHOULDER_Z), (0.69 * sx, 0, SHOULDER_Z), 0.030, 0.030, "M_Mother_Skin", side + "Hand", segments=8)
        b.box((0.735 * sx, 0, SHOULDER_Z - 0.003), (0.10, 0.075, 0.03), "M_Mother_Skin", side + "Hand", bevel=0.01, segments=2)
        b.cylinder((0.69 * sx, -0.035, SHOULDER_Z - 0.005), (0.735 * sx, -0.065, SHOULDER_Z - 0.005),
                   0.013, 0.011, "M_Mother_Skin", side + "Hand", segments=6)

    # ---- 다리 ----
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        x = LEG_X * sx
        b.cylinder((x, 0, HIP_Z + 0.01), (x, 0, KNEE_Z), 0.088, 0.060, "M_Mother_Pants", side + "UpperLeg", segments=10)
        b.ellipsoid((x, 0, KNEE_Z), (0.060, 0.060, 0.060), "M_Mother_Pants", side + "LowerLeg", u_segments=10)
        b.cylinder((x, 0, KNEE_Z), (x, 0, ANKLE_Z + 0.01), 0.060, 0.048, "M_Mother_Pants", side + "LowerLeg", segments=10)
        b.cylinder((x, 0, ANKLE_Z + 0.03), (x, 0, ANKLE_Z - 0.01), 0.035, 0.035, "M_Mother_Skin", side + "Foot", segments=8)
        # 슬리퍼: 뒤꿈치~발등은 Foot, 앞코는 Toes 본에 묶는다.
        b.box((x, -0.035, 0.035), (0.088, 0.15, 0.07), "M_Mother_Slipper", side + "Foot", bevel=0.018, segments=2)
        b.box((x, -0.125, 0.025), (0.084, 0.07, 0.05), "M_Mother_Slipper", side + "Toes", bevel=0.018, segments=2)
    return b


def build_armature():
    """T-포즈 뼈대. 편집 모드 진입이 필요해 view_layer의 활성 오브젝트로 지정한다."""
    arm_data = bpy.data.armatures.new("MotherArmature")
    arm_obj = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = arm_data.edit_bones
    for name, head, tail, parent in BONES:
        bone = edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.roll = 0.0
        if parent:
            bone.parent = edit_bones[parent]
            bone.use_connect = (tuple(edit_bones[parent].tail) == tuple(head))
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm_obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_path = os.path.abspath(argv[0] if argv else "Mother.fbx")

    lp.reset_scene()
    mats = lp.make_materials(MATERIALS)

    arm_obj = build_armature()
    body = build_body().to_object("Mother_Body", mats)
    missing = [g.name for g in body.vertex_groups if g.name not in arm_obj.data.bones]
    if missing:
        raise RuntimeError("뼈대에 없는 버텍스 그룹: %s" % missing)
    body.parent = arm_obj
    modifier = body.modifiers.new("Armature", 'ARMATURE')
    modifier.object = arm_obj

    bpy.context.view_layer.update()
    lp.face_unity_forward()
    lp.export_fbx(out_path, armature=True)
    print("[build_mother] exported:", out_path, "verts:", len(body.data.vertices), "tris:",
          sum(len(p.vertices) - 2 for p in body.data.polygons))


if __name__ == "__main__":
    main()
