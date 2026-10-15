# -*- coding: utf-8 -*-
"""
로우폴리 모델 생성용 공용 도우미 모듈.

build_mother.py, build_handheld_console.py가 Blender 백그라운드 모드에서 import해 사용한다.
bpy.ops 의존을 줄이기 위해 메시는 bmesh로 직접 만들고, 파츠마다 머티리얼 슬롯과
(필요하면) 본 이름의 버텍스 그룹을 지정한다. 좌표계는 Blender 기준이다.
  - Z: 위, -Y: 모델의 정면, +X: 모델 기준 왼쪽(정면에서 볼 때 화면 오른쪽)
  - 단위: 미터
내보내기 직전 face_unity_forward()로 Z축 180° 회전을 적용해, Unity에서 모델 정면이 +Z가 되게 한다.
"""

import bpy
import bmesh
from mathutils import Matrix, Vector


def reset_scene():
    """팩토리 설정의 빈 씬으로 초기화하고 단위를 미터로 맞춘다."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0


def _scale(sx, sy, sz):
    return Matrix.Diagonal((sx, sy, sz, 1.0))


def align_z_to(direction):
    """bmesh 원기둥(로컬 Z축)을 direction 방향으로 세우는 회전 행렬."""
    return Vector((0.0, 0.0, 1.0)).rotation_difference(Vector(direction).normalized()).to_matrix().to_4x4()


class MeshBuilder:
    """하나의 메시에 여러 파츠를 누적한다.

    파츠마다 머티리얼 이름과 본 이름을 받아 face.material_index와 deform 가중치(강체 1.0)를 기록한다.
    한 버텍스는 한 본에만 묶이므로 관절 부위는 구체를 겹쳐 굽힘 시 틈을 가린다.
    """

    def __init__(self):
        self.bm = bmesh.new()
        self.uv_layer = self.bm.loops.layers.uv.new("UVMap")
        self.deform_layer = self.bm.verts.layers.deform.verify()
        self.materials = []
        self.groups = []

    # ---- 내부 기록 ----
    def _index(self, items, name):
        if name not in items:
            items.append(name)
        return items.index(name)

    def _snapshot(self):
        return set(self.bm.faces)

    def _tag_new(self, before, material, bone, smooth, smooth_faces=None):
        """before 이후 생긴 면에 머티리얼·스무딩·본 가중치를 지정한다."""
        new_faces = [f for f in self.bm.faces if f not in before]
        mat_index = self._index(self.materials, material)
        smooth_set = set(smooth_faces) if smooth_faces is not None else None
        verts = set()
        for face in new_faces:
            face.material_index = mat_index
            face.smooth = smooth if smooth_set is None else (face in smooth_set)
            verts.update(face.verts)
        if bone:
            group_index = self._index(self.groups, bone)
            for vert in verts:
                vert[self.deform_layer][group_index] = 1.0
        return new_faces

    # ---- 기본 도형 ----
    def box(self, center, size, material, bone=None, bevel=0.0, segments=2, rotation=None):
        """center에 size 크기의 상자를 만든다. bevel>0이면 모서리를 둥글게 깎고 깎인 면만 스무딩한다."""
        before = self._snapshot()
        rot = rotation if rotation is not None else Matrix.Identity(4)
        matrix = Matrix.Translation(center) @ rot @ _scale(*size)
        result = bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix, calc_uvs=True)
        smooth_faces = []
        if bevel > 0.0:
            verts = result['verts']
            edges = list({e for v in verts for e in v.link_edges})
            bev = bmesh.ops.bevel(
                self.bm, geom=edges, offset=bevel, offset_type='OFFSET',
                profile_type='SUPERELLIPSE', segments=segments, profile=0.5,
                affect='EDGES', clamp_overlap=True)
            smooth_faces = bev['faces']
        return self._tag_new(before, material, bone, False, smooth_faces)

    def cylinder(self, p0, p1, r0, r1, material, bone=None, segments=8, squash=(1.0, 1.0), smooth=True):
        """p0→p1 축을 따라 반지름 r0→r1의 원기둥(원뿔대)을 만든다. squash로 단면을 타원으로 만든다."""
        before = self._snapshot()
        p0, p1 = Vector(p0), Vector(p1)
        axis = p1 - p0
        matrix = Matrix.Translation((p0 + p1) * 0.5) @ align_z_to(axis) @ _scale(squash[0], squash[1], 1.0)
        bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=segments,
            radius1=r0, radius2=r1, depth=axis.length, matrix=matrix, calc_uvs=True)
        # 옆면만 스무딩하고 뚜껑(축과 평행한 법선)은 평평하게 둬서 버튼 윗면 음영 얼룩을 막는다.
        smooth_faces = []
        if smooth:
            direction = axis.normalized()
            for face in self.bm.faces:
                if face not in before:
                    face.normal_update()
                    if abs(face.normal.dot(direction)) < 0.9:
                        smooth_faces.append(face)
        return self._tag_new(before, material, bone, False, smooth_faces)

    def ellipsoid(self, center, radii, material, bone=None, u_segments=10, v_segments=6, smooth=True):
        """center에 반지름 radii(x, y, z)의 타원체를 만든다."""
        before = self._snapshot()
        matrix = Matrix.Translation(center) @ _scale(*radii)
        bmesh.ops.create_uvsphere(
            self.bm, u_segments=u_segments, v_segments=v_segments, radius=1.0,
            matrix=matrix, calc_uvs=True)
        return self._tag_new(before, material, bone, smooth)

    def quad(self, center, width, height, material, uv=True):
        """-Y를 향하는 XZ 평면 사각형. UV는 정면에서 볼 때 왼쪽 아래 (0,0), 오른쪽 위 (1,1)."""
        before = self._snapshot()
        cx, cy, cz = center
        hw, hh = width * 0.5, height * 0.5
        # 반시계 순서(+X 다음 +Z)라서 면 법선이 -Y(정면)를 향한다.
        coords = [(cx - hw, cy, cz - hh), (cx + hw, cy, cz - hh), (cx + hw, cy, cz + hh), (cx - hw, cy, cz + hh)]
        uvs = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]
        verts = [self.bm.verts.new(c) for c in coords]
        face = self.bm.faces.new(verts)
        if uv:
            for loop, coord in zip(face.loops, uvs):
                loop[self.uv_layer].uv = coord
        return self._tag_new(before, material, None, False)

    # ---- Blender 오브젝트로 출력 ----
    def to_object(self, name, material_lookup, collection=None):
        """누적한 bmesh를 메시 오브젝트로 만든다. 버텍스 그룹 순서는 deform 인덱스와 같아야 한다."""
        mesh = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        (collection or bpy.context.scene.collection).objects.link(obj)
        for mat_name in self.materials:
            mesh.materials.append(material_lookup[mat_name])
        for group_name in self.groups:
            obj.vertex_groups.new(name=group_name)
        return obj


def make_materials(spec):
    """{이름: (r, g, b)} → Blender 머티리얼. 색은 미리보기용이며 Unity에서는 URP Lit 머티리얼로 리맵한다."""
    result = {}
    for name, rgb in spec.items():
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (rgb[0], rgb[1], rgb[2], 1.0)
        # FBX에 Diffuse 색이 기록되도록 Principled BSDF의 Base Color도 같이 맞춘다.
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        result[name] = mat
    return result


def face_unity_forward():
    """스크립트에서 -Y를 정면으로 만든 모델을 Z축 180° 돌려 +Y가 정면이 되게 하고 회전을 데이터에 적용한다.

    Blender +Y가 Unity +Z로 가져와지므로 Unity에서 모델 정면이 +Z(왼쪽 = -X)가 된다.
    부모가 없는 루트 오브젝트만 돌리며, 아마추어에 적용하면 본과 자식 메시가 함께 갱신된다.
    """
    flip = Matrix.Rotation(3.141592653589793, 4, 'Z')
    roots = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    for obj in bpy.context.scene.objects:
        obj.select_set(obj in roots)
    for obj in roots:
        obj.matrix_world = flip @ obj.matrix_world
    bpy.context.view_layer.objects.active = roots[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bpy.context.view_layer.update()


def export_fbx(filepath, armature=False):
    """Unity용 FBX 내보내기. 스케일은 1 유닛 = 1m가 되도록 적용하고 리프 본은 만들지 않는다."""
    object_types = {'MESH', 'ARMATURE'} if armature else {'MESH', 'EMPTY'}
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=False,
        object_types=object_types,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        # Unity(bakeAxisConversion)는 FBX 축 정보를 읽어 다시 변환하므로 이 값과 무관하게
        # Blender (x, y, z) → Unity (x, z, y)가 된다. 정면 방향은 face_unity_forward()에서 맞춘다.
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=False,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode='AUTO',
    )
