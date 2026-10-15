# -*- coding: utf-8 -*-
"""
Mixamo에서 "With Skin"으로 받은 FBX에서 캐릭터 메시를 지우고 뼈대 + 애니메이션만 남겨 다시 내보낸다.

용도: 공개 저장소에 Mixamo 캐릭터 메시를 올리지 않고 용량도 줄이기 위해.
Unity에서는 Humanoid로 임포트하므로 메시가 없어도 엄마 캐릭터에 리타게팅된다.

실행 (입력과 출력이 같아도 된다):
  blender -b --factory-startup --python Tools/Blender/strip_mixamo_mesh.py -- <입력.fbx> <출력.fbx> [fps=30]

주의: Unity가 이미 임포트한 파일을 덮어쓸 때는 .meta를 지우지 않는다 (임포트 설정·GUID 유지).
"""

import os
import sys

import bpy


def add_bind_pose_anchor(arm):
    """Hips 본 위치에 아주 작은 스킨 삼각형을 만든다 (렌더링용이 아니라 바인드 포즈 기록용)."""
    hips = arm.data.bones["mixamorig:Hips"]
    c = hips.head_local
    size = 0.1   # 아마추어 로컬 단위(cm 스케일 0.01 적용 전) → 실제 약 1mm
    mesh = bpy.data.meshes.new("BindPoseAnchor")
    mesh.from_pydata([(c.x, c.y, c.z), (c.x + size, c.y, c.z), (c.x, c.y, c.z + size)], [], [(0, 1, 2)])
    obj = bpy.data.objects.new("BindPoseAnchor", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = arm
    group = obj.vertex_groups.new(name="mixamorig:Hips")
    group.add([0, 1, 2], 1.0, 'REPLACE')
    modifier = obj.modifiers.new("Armature", 'ARMATURE')
    modifier.object = arm


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("사용법: -- <입력.fbx> <출력.fbx> [fps]")
    src, dst = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    fps = int(argv[2]) if len(argv) > 2 else 30

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    # Mixamo 기본 30fps. 씬 fps를 먼저 맞춰야 키프레임이 원래 시간 간격대로 다시 구워진다.
    scene.render.fps = fps
    scene.render.fps_base = 1.0
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False)

    removed = 0
    for obj in list(bpy.data.objects):
        if obj.type != 'ARMATURE':
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1
    for data in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.textures):
        for block in list(data):
            if block.users == 0:
                data.remove(block)

    armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    if len(armatures) != 1 or armatures[0].animation_data is None or armatures[0].animation_data.action is None:
        raise SystemExit("애니메이션이 있는 아마추어를 하나 찾지 못했습니다: %s" % src)
    arm = armatures[0]
    action = arm.animation_data.action
    start, end = action.frame_range
    scene.frame_start, scene.frame_end = int(round(start)), int(round(end))

    # 스킨 메시가 하나도 없으면 FBX에 바인드 포즈가 기록되지 않아, 다시 읽을 때 rest pose가
    # 첫 프레임 자세로 바뀐다(→ Unity Humanoid 기준 자세가 틀어져 리타게팅 결과가 달라진다).
    # 1mm 삼각형 하나를 Hips에 스키닝해 T-포즈 바인드 포즈가 함께 저장되게 한다.
    add_bind_pose_anchor(arm)

    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=False,
        object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        # 메시가 함께 있으면 씬 단위 테이크("Scene")로 묶여 Unity가 테이크를 못 찾는 경우가 있어
        # 액션 단위 테이크(아마추어|액션 이름)로 내보낸다. 액션은 하나뿐이다.
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,   # 키 감소 없이 원본 프레임 그대로
        path_mode='STRIP',
    )
    print("[strip_mixamo_mesh] %s -> %s removed=%d frames=%d..%d fps=%d" % (src, dst, removed, scene.frame_start, scene.frame_end, fps))


if __name__ == "__main__":
    main()
