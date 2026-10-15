# -*- coding: utf-8 -*-
"""
사실적인 40대 한국인 엄마 캐릭터 생성 스크립트 (MPFB2 + 직접 만든 의상·머리).

실행 (프로젝트 루트에서, --factory-startup을 쓰면 MPFB 확장이 꺼지므로 붙이지 않는다):
  blender -b --python Tools/Blender/build_mother_realistic.py -- \
      Assets/_Art/Models/MotherRealistic.fbx Assets/_Art/Textures/MotherRealistic [미리보기.blend]

필요 조건:
  - Blender 5.2, MPFB 2.0.18 확장 (user_default 저장소)
  - MakeHuman system assets 팩(CC0)이 MPFB 데이터 폴더에 설치되어 있어야 한다.

구성:
  - 몸: MPFB 기본 메시(40대 동아시아 여성 체형, young_asian_female 피부). 옷에 가려지는 피부는 삭제해 폴리곤을 줄인다.
  - 뼈대: MPFB의 mixamo_unity 리그(mixamorig:* 본, 손가락 포함). T-포즈를 rest pose로 굳혀 Unity Humanoid에 맞춘다.
  - 상의: 몸 메시를 복제·부풀려 만든 긴소매 크루넥 티셔츠. 크림색 바탕 검은 가로 줄무늬(소매는 팔 둘레 방향).
  - 하의: female_casualsuit01의 청바지 부분만 남기고 종아리를 넓혀 일자 핏으로 만든다.
  - 양말: 발 메시를 복제해 만든 흰 양말.
  - 머리: 적갈색 볼륨 곱슬 펌(턱선 길이, 곱슬 앞머리). mother_hair.py에서 볼륨 덩어리 + 나선 컬로 직접 생성한다.
  - 안경은 없다.
텍스처는 두 번째 인자 폴더에 복사·생성하고, Unity 쪽 MotherRealisticPrefabBuilder.cs가 URP Lit 머티리얼로 연결한다.
"""

import importlib
import json
import math
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import lowpoly_builder as lp  # noqa: E402
import mother_hair  # noqa: E402

# ---- 체형 (MakeHuman 매크로 값, 0~1) ----
# age 0.5 = 25세, 1.0 = 90세 → 0.63 ≈ 42세. height는 키 약 1.60m가 되도록 맞춘 값.
PHENOTYPE = {
    "gender": 0.0, "age": 0.63, "muscle": 0.45, "weight": 0.42, "proportions": 0.65,
    "height": 0.585, "cupsize": 0.45, "firmness": 0.5,
    "race": {"asian": 1.0, "caucasian": 0.0, "african": 0.0},
}

# ---- 머티리얼 이름: Unity MotherRealisticPrefabBuilder.cs와 같아야 리맵된다 ----
MAT_SKIN = "M_MotherR_Skin"
MAT_EYES = "M_MotherR_Eyes"
MAT_BROWS = "M_MotherR_Eyebrows"
MAT_LASHES = "M_MotherR_Eyelashes"
MAT_SHIRT = "M_MotherR_Shirt"
MAT_JEANS = "M_MotherR_Jeans"
MAT_SOCKS = "M_MotherR_Socks"
# 머리 머티리얼(M_MotherR_Hair, M_MotherR_HairMass)은 mother_hair.py에 있다.

# ---- 색 (sRGB) ----
STRIPE_DARK = (0.07, 0.07, 0.08)
STRIPE_LIGHT = (0.94, 0.92, 0.85)

STRIPE_PERIOD = 0.021   # 줄무늬 한 주기(검정+크림) 길이 (m)
STRIPE_DARK_RATIO = 0.36
ARM_BONES = ("LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand")


# ======================================================================
# MPFB
# ======================================================================
def mpfb(module):
    """설치 저장소 이름에 관계없이 MPFB 서비스 모듈을 불러온다."""
    for repo in ("user_default", "blender_org"):
        try:
            return importlib.import_module("bl_ext.%s.mpfb.services.%s" % (repo, module))
        except ImportError:
            continue
    raise RuntimeError("MPFB 확장을 찾을 수 없습니다. Blender에 MPFB를 설치·활성화하고 --factory-startup 없이 실행하세요.")


def build_base_human():
    """MPFB로 몸·눈·눈썹·속눈썹·청바지(casualsuit01)·리그를 만들고 T-포즈를 rest pose로 굳힌다."""
    HumanService = mpfb("humanservice").HumanService
    info = HumanService._create_default_human_info_dict()
    info["phenotype"] = PHENOTYPE
    info["rig"] = "mixamo_unity"
    info["eyes"] = "low-poly.mhclo"
    info["eyebrows"] = "eyebrow001.mhclo"
    info["eyelashes"] = "eyelashes01.mhclo"
    info["clothes"] = ["female_casualsuit01.mhclo"]
    info["skin_mhmat"] = "young_asian_female.mhmat"
    info["skin_material_type"] = "MAKESKIN"
    settings = HumanService.get_default_deserialization_settings()
    settings["subdiv_levels"] = 0
    basemesh = HumanService.deserialize_from_dict(info, settings)
    rig = basemesh.parent

    RigService = mpfb("rigservice").RigService
    ObjectService = mpfb("objectservice").ObjectService
    LocationService = mpfb("locationservice").LocationService
    pose_file = os.path.join(LocationService.get_mpfb_data("poses"), "mixamo_unity_fk", "t-pose.json")
    with open(pose_file, "r", encoding="utf-8") as handle:
        pose = json.load(handle)
    ObjectService.deselect_and_deactivate_all()
    ObjectService.activate_blender_object(rig)
    bpy.ops.object.mode_set(mode='POSE')
    RigService.set_pose_from_dict(rig, pose)
    bpy.ops.object.mode_set(mode='OBJECT')
    # 메시에 포즈를 굽고 T-포즈를 새 rest pose로 만든다 (Unity Humanoid 아바타가 T-포즈를 기준으로 삼기 때문).
    RigService.apply_pose_as_rest_pose(rig)
    return basemesh, rig


def mpfb_data_dir():
    return mpfb("locationservice").LocationService.get_user_data()


def find_asset_file(subdir, filename):
    path = mpfb("assetservice").AssetService.find_asset_absolute_path(filename, asset_subdir=subdir)
    if path is None:
        raise FileNotFoundError("MPFB 에셋을 찾을 수 없습니다: %s/%s" % (subdir, filename))
    return path


# ======================================================================
# 공용 도우미
# ======================================================================
class Landmarks:
    """T-포즈 rest 상태의 본 위치로 옷·머리 재단 기준선을 계산한다 (체형 값을 바꿔도 따라가게)."""

    def __init__(self, rig):
        bones = rig.data.bones
        head = lambda name: bones["mixamorig:" + name].head_local.copy()  # noqa: E731
        self.hips = head("Hips")
        self.neck = head("Neck")
        self.head = head("Head")
        self.hand = head("LeftHand")
        self.foot = head("LeftFoot")
        self.knee = head("LeftLeg")
        self.eye = head("LeftEye")
        self.hem_z = self.hips.z + 0.005
        self.sleeve_end_x = self.hand.x - 0.022
        self.sock_top_z = self.foot.z + 0.05
        self.neck_half_x = 0.095

    def neckline_z(self, y):
        """크루넥 라인: 앞(쇄골 위)은 낮고 뒤(목덜미)는 높게 기울어진 면."""
        return self.neck.z - 0.028 + (y - (self.neck.y - 0.07)) * 0.45

    def in_shirt(self, co):
        if abs(co.x) > self.sleeve_end_x or co.z < self.hem_z:
            return False
        if abs(co.x) < self.neck_half_x and co.z > self.neckline_z(co.y):
            return False
        return True

    def deep_in_shirt(self, co, margin=0.03):
        """셔츠 경계에서 margin 이상 안쪽 → 몸 피부를 지워도 틈이 보이지 않는 영역."""
        if abs(co.x) > self.sleeve_end_x - margin or co.z < self.hem_z + margin:
            return False
        if abs(co.x) < self.neck_half_x + margin and co.z > self.neckline_z(co.y) - margin:
            return False
        return True


def deform_weight(vert, layer, index):
    return vert[layer].get(index, 0.0) if index is not None else 0.0


def new_object_from_bmesh(name, bm, template_obj, rig, material, decimate_ratio=None):
    """template_obj의 버텍스 그룹 순서를 그대로 복제해 bmesh의 deform 인덱스가 같은 본을 가리키게 한다.
    decimate_ratio를 주면 리그에 붙이기 전에 폴리곤을 줄인다 (버텍스 가중치는 보간되어 유지된다)."""
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if template_obj is not None:
        for group in template_obj.vertex_groups:
            obj.vertex_groups.new(name=group.name)
    mesh.materials.append(material)
    if decimate_ratio is not None:
        modifier = obj.modifiers.new("Decimate", 'DECIMATE')
        modifier.ratio = decimate_ratio
        with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    attach_to_rig(obj, rig)
    return obj


def attach_to_rig(obj, rig):
    obj.parent = rig
    obj.matrix_parent_inverse.identity()
    for modifier in list(obj.modifiers):
        if modifier.type == 'ARMATURE':
            obj.modifiers.remove(modifier)
    modifier = obj.modifiers.new("Armature", 'ARMATURE')
    modifier.object = rig


def save_png(path, width, height, pixel_fn):
    """pixel_fn(x, y) → (r, g, b, a) sRGB 0~1. bpy 이미지로 PNG를 저장한다."""
    image = bpy.data.images.new(os.path.basename(path), width, height, alpha=True)
    pixels = []
    for y in range(height):
        for x in range(width):
            pixels.extend(pixel_fn(x, y))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = path
    image.file_format = 'PNG'
    image.save()
    return image


def preview_material(name, image=None, color=(0.8, 0.8, 0.8), roughness=0.6, alpha_image=False, normal_image=None):
    """Blender 미리보기용 Principled 머티리얼. Unity에서는 이름으로 URP Lit 머티리얼에 리맵된다."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = roughness
    if image is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if alpha_image:
            links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    else:
        bsdf.inputs["Base Color"].default_value = (color[0] ** 2.2, color[1] ** 2.2, color[2] ** 2.2, 1.0)
    if normal_image is not None:
        ntex = nodes.new("ShaderNodeTexImage")
        ntex.image = normal_image
        ntex.image.colorspace_settings.name = 'Non-Color'
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def rename_material(obj, name):
    if obj.material_slots and obj.material_slots[0].material is not None:
        obj.material_slots[0].material.name = name
        return obj.material_slots[0].material
    return None


# ======================================================================
# 몸 정리 + 셔츠 + 양말
# ======================================================================
def build_shirt(source_bm, layer, groups, lm, rig, body, stripe_image):
    """몸 메시(T-포즈)에서 상체·팔 면을 복제해 루즈핏 긴소매 티셔츠를 만든다."""
    bm = source_bm.copy()
    bm.verts.ensure_lookup_table()
    inside = {v for v in bm.verts if lm.in_shirt(v.co)}
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(v in inside for v in f.verts)], context='FACES_ONLY')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    layer = bm.verts.layers.deform.active
    arm_ids = [groups.get("mixamorig:" + n) for n in ARM_BONES]
    is_arm = {v: sum(deform_weight(v, layer, i) for i in arm_ids) > 0.5 for v in bm.verts}

    # 1) 몸의 굴곡(배꼽·쇄골·팔꿈치)을 지우도록 내부 버텍스를 여러 번 평탄화한다.
    interior = [v for v in bm.verts if not v.is_boundary]
    for _ in range(6):
        bmesh.ops.smooth_vert(bm, verts=interior, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()

    # 2) 몸통: 법선 방향으로 띄운다.
    for v in bm.verts:
        if not is_arm[v]:
            v.co += v.normal * 0.011

    # 3) 몸통: 가슴 높이 둘레를 기준으로 그 아래가 일자로 떨어지는 박시한 실루엣.
    center_y = lm.neck.y - 0.005
    bust_lo, bust_hi = lm.hips.z + 0.27, lm.hips.z + 0.33
    sectors = 48
    ring = [0.0] * sectors
    sector_of = lambda co: int((math.atan2(co.y - center_y, co.x) + math.pi) / (2 * math.pi) * sectors) % sectors  # noqa: E731
    for v in bm.verts:
        if not is_arm[v] and bust_lo < v.co.z < bust_hi:
            s = sector_of(v.co)
            ring[s] = max(ring[s], math.hypot(v.co.x, v.co.y - center_y))
    ring = [max(ring[i - 1], ring[i], ring[(i + 1) % sectors]) for i in range(sectors)]
    for v in bm.verts:
        if is_arm[v] or v.co.z > bust_hi:
            continue
        s = sector_of(v.co)
        radius = math.hypot(v.co.x, v.co.y - center_y)
        target = ring[s] * 0.97 if ring[s] > 0 else radius
        t = min(1.0, max(0.0, (bust_hi - v.co.z) / 0.08))
        hem_flare = 1.0 + max(0.0, (lm.hem_z + 0.06) - v.co.z) * 0.25
        new_radius = max(radius, radius + (target - radius) * t) * hem_flare
        if radius > 1e-5:
            scale = new_radius / radius
            v.co.x *= scale
            v.co.y = center_y + (v.co.y - center_y) * scale

    # 4) 소매: 팔 축(어깨→손목)에서 일정 반지름 이상으로 넉넉하게, 손목 근처는 주름(뭉침)을 준다.
    for v in bm.verts:
        if not is_arm[v]:
            continue
        axis_y = lm.hand.y * (abs(v.co.x) / lm.hand.x) + (lm.neck.y - 0.02) * (1 - abs(v.co.x) / lm.hand.x)
        axis_z = lm.hand.z + 0.006
        dy, dz = v.co.y - axis_y, v.co.z - axis_z
        radius = math.hypot(dy, dz)
        ax = abs(v.co.x)
        base = 0.047 - 0.012 * min(1.0, max(0.0, (ax - 0.2) / 0.3))
        bunch = 0.0
        if ax > lm.sleeve_end_x - 0.11:
            ramp = min(1.0, (ax - (lm.sleeve_end_x - 0.11)) / 0.05)
            bunch = 0.0045 * ramp * (0.5 + 0.5 * math.sin(ax / 0.016 * 2 * math.pi + math.atan2(dz, dy) * 2))
        new_radius = max(radius + 0.008, base) + bunch
        if radius > 1e-5:
            v.co.y = axis_y + dy * new_radius / radius
            v.co.z = axis_z + dz * new_radius / radius

    # 5) 줄무늬 UV: 몸통은 높이(z), 소매는 팔 길이 방향(|x|)을 V로 써서 줄무늬가 가로·팔 둘레로 돈다.
    uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        arm_face = sum(1 for v in face.verts if is_arm[v]) * 2 > len(face.verts)
        for loop in face.loops:
            coord = abs(loop.vert.co.x) if arm_face else loop.vert.co.z
            loop[uv].uv = (0.5, coord / STRIPE_PERIOD)

    # 6) 목 립(크림색 단): 목 둘레 경계를 위·안쪽으로 1cm 돌출. UV를 크림색 구간에 고정한다.
    neck_edges = [e for e in bm.edges if e.is_boundary and all(abs(v.co.x) < 0.16 and v.co.z > lm.neck.z - 0.08 for v in e.verts)]
    if neck_edges:
        ext = bmesh.ops.extrude_edge_only(bm, edges=neck_edges)
        new_verts = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]
        for v in new_verts:
            inward = Vector((-v.co.x, center_y - v.co.y, 0.0))
            v.co += inward.normalized() * 0.004 + Vector((0, 0, 0.011))
        cream_v = (STRIPE_DARK_RATIO + (1 - STRIPE_DARK_RATIO) * 0.5)
        new_faces = {f for v in new_verts for f in v.link_faces}
        for face in new_faces:
            for loop in face.loops:
                loop[uv].uv = (0.5, cream_v)

    mat = preview_material(MAT_SHIRT, image=stripe_image, roughness=0.85)
    return new_object_from_bmesh("Mother_Shirt", bm, body, rig, mat)


def build_socks(source_bm, lm, rig, body):
    bm = source_bm.copy()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(v.co.z < lm.sock_top_z for v in f.verts)], context='FACES_ONLY')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    interior = [v for v in bm.verts if not v.is_boundary]
    for _ in range(25):  # 발가락 사이 굴곡을 메워 양말처럼 감싼다 (줄어든 만큼 아래에서 더 띄운다)
        bmesh.ops.smooth_vert(bm, verts=interior, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.006
        if v.co.z < 0.002:
            v.co.z = 0.002
    mat = preview_material(MAT_SOCKS, color=(0.93, 0.93, 0.91), roughness=0.95)
    # 발가락까지 촘촘한 메시라 절반 이하로 줄인다.
    return new_object_from_bmesh("Mother_Socks", bm, body, rig, mat, decimate_ratio=0.35)


def process_body(body, rig, lm, stripe_image):
    """헬퍼 형상 제거 → 셔츠·양말 생성 → 옷 안쪽 피부 삭제. MPFB 마스크 모디파이어를 실제 삭제로 대신한다."""
    groups = {g.name: g.index for g in body.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(body.data)
    layer = bm.verts.layers.deform.active
    body_group = groups["body"]
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if deform_weight(v, layer, body_group) <= 0.0], context='VERTS')

    shirt = build_shirt(bm, layer, groups, lm, rig, body, stripe_image)
    socks = build_socks(bm, lm, rig, body)

    jeans_group = groups.get("Delete.female_casualsuit01")
    layer = bm.verts.layers.deform.active
    hidden = [v for v in bm.verts
              if deform_weight(v, layer, jeans_group) > 0.0 and v.co.z < lm.hem_z + 0.05
              or lm.deep_in_shirt(v.co)
              or v.co.z < lm.sock_top_z - 0.025]
    bmesh.ops.delete(bm, geom=hidden, context='VERTS')
    bm.to_mesh(body.data)
    bm.free()
    for modifier in list(body.modifiers):
        if modifier.type == 'MASK':
            body.modifiers.remove(modifier)
    return shirt, socks


# ======================================================================
# 청바지
# ======================================================================
def build_jeans(suit, lm):
    """casualsuit01에서 티셔츠 덩어리를 지우고, 무릎 아래를 넓혀 일자 청바지로 만든다."""
    bm = bmesh.new()
    bm.from_mesh(suit.data)
    bm.verts.ensure_lookup_table()
    seen, top_parts = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        stack, comp = [v], []
        seen.add(v)
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for e in cur.link_edges:
                other = e.other_vert(cur)
                if other not in seen:
                    seen.add(other)
                    stack.append(other)
        if max(c.co.z for c in comp) > lm.hips.z + 0.15:
            top_parts.extend(comp)
    bmesh.ops.delete(bm, geom=top_parts, context='VERTS')

    for side in (1.0, -1.0):
        legs = [v for v in bm.verts if v.co.x * side > 0]
        knee_ring = [v for v in legs if abs(v.co.z - lm.knee.z) < 0.02]
        if not knee_ring:
            continue
        knee_x = sum(v.co.x for v in knee_ring) / len(knee_ring)
        knee_y = sum(v.co.y for v in knee_ring) / len(knee_ring)
        knee_r = sum(math.hypot(v.co.x - knee_x, v.co.y - knee_y) for v in knee_ring) / len(knee_ring)
        foot_x, foot_y = lm.foot.x * side, lm.foot.y
        straight_r = knee_r * 1.12
        for v in legs:
            if v.co.z > lm.knee.z + 0.06:
                continue
            t = (v.co.z - lm.foot.z) / max(1e-4, lm.knee.z - lm.foot.z)
            cx = foot_x + (knee_x - foot_x) * min(1.0, max(0.0, t))
            cy = foot_y + (knee_y - foot_y) * min(1.0, max(0.0, t))
            radius = math.hypot(v.co.x - cx, v.co.y - cy)
            blend = min(1.0, max(0.0, (lm.knee.z + 0.06 - v.co.z) / 0.08))
            new_radius = radius + (max(radius, straight_r) - radius) * blend
            if radius > 1e-5:
                v.co.x = cx + (v.co.x - cx) * new_radius / radius
                v.co.y = cy + (v.co.y - cy) * new_radius / radius
    bm.to_mesh(suit.data)
    bm.free()
    suit.name = "Mother_Jeans"


# ======================================================================
# 텍스처
# ======================================================================
def make_textures(tex_dir):
    os.makedirs(tex_dir, exist_ok=True)

    def stripe(_x, y):
        # 세로 128px = 한 주기. 아래 36%가 검정, 경계는 1px 부드럽게.
        t = (y + 0.5) / 128.0
        edge = 1.5 / 128.0
        dark = min(1.0, max(0.0, (STRIPE_DARK_RATIO - t) / edge + 0.5)) * min(1.0, max(0.0, t / edge + 0.5))
        c = [STRIPE_LIGHT[i] + (STRIPE_DARK[i] - STRIPE_LIGHT[i]) * dark for i in range(3)]
        return (c[0], c[1], c[2], 1.0)

    return save_png(os.path.join(tex_dir, "T_MotherR_ShirtStripe.png"), 8, 128, stripe)


def copy_mpfb_textures(tex_dir):
    """CC0 MakeHuman 텍스처를 Unity 폴더로 복사 (이름은 Unity 빌더와 맞춘다)."""
    copies = {
        ("skins", "young_asian_female.mhmat", "young_lightskinned_female_diffuse3.png"): "T_MotherR_Skin.png",
        ("eyes", "brown.mhmat", "brown_eye.png"): "T_MotherR_Eye.png",
        ("eyebrows", "eyebrow001.mhclo", "eyebrow001.png"): "T_MotherR_Eyebrow.png",
        ("eyelashes", "eyelashes01.mhclo", "eyelashes01.png"): "T_MotherR_Eyelashes.png",
        ("clothes", "female_casualsuit01.mhclo", "female_casualsuit01_diffuse.png"): "T_MotherR_Jeans_Diffuse.png",
        ("clothes", "female_casualsuit01.mhclo", "female_casualsuit01_normal.png"): "T_MotherR_Jeans_Normal.png",
        ("clothes", "female_casualsuit01.mhclo", "female_casualsuit01_ao.png"): "T_MotherR_Jeans_AO.png",
    }
    for (subdir, anchor, filename), target in copies.items():
        src = os.path.join(os.path.dirname(find_asset_file(subdir, anchor)), filename)
        shutil.copyfile(src, os.path.join(tex_dir, target))


# ======================================================================
def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("사용법: -- <출력 FBX> <텍스처 폴더> [미리보기 .blend]")
    out_fbx, tex_dir = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    preview_blend = os.path.abspath(argv[2]) if len(argv) > 2 else None

    bpy.ops.wm.read_homefile(use_empty=True)
    body, rig = build_base_human()
    lm = Landmarks(rig)
    print("[landmarks] hips", tuple(lm.hips), "neck", tuple(lm.neck), "hand", tuple(lm.hand), "eye", tuple(lm.eye))

    stripe_img = make_textures(tex_dir)
    copy_mpfb_textures(tex_dir)

    children = {o.name: o for o in rig.children}
    eyes = next(o for n, o in children.items() if n.endswith("low-poly"))
    brows = next(o for n, o in children.items() if "eyebrow" in n)
    lashes = next(o for n, o in children.items() if "eyelashes" in n)
    suit = next(o for n, o in children.items() if "casualsuit" in n)

    shirt, socks = process_body(body, rig, lm, stripe_img)
    build_jeans(suit, lm)
    hair = mother_hair.build_hair(body, rig, lm, tex_dir, preview_material, attach_to_rig, save_png)

    body.name, eyes.name, brows.name, lashes.name = "Mother_Body", "Mother_Eyes", "Mother_Eyebrows", "Mother_Eyelashes"
    rename_material(body, MAT_SKIN)
    rename_material(eyes, MAT_EYES)
    rename_material(brows, MAT_BROWS)
    rename_material(lashes, MAT_LASHES)
    rename_material(suit, MAT_JEANS)
    rig.name = "Armature"

    # 미리보기·FBX에 방해되는 남은 오브젝트(빈 헬퍼 등) 제거
    keep = {rig, body, eyes, brows, lashes, suit, shirt, socks, hair}
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)

    total = 0
    for obj in sorted(keep - {rig}, key=lambda o: o.name):
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        total += tris
        print("[mesh] %-18s verts=%6d tris=%6d" % (obj.name, len(obj.data.vertices), tris))
    print("[mesh] total tris:", total, "bones:", len(rig.data.bones))

    if preview_blend:
        bpy.ops.wm.save_as_mainfile(filepath=preview_blend)
    lp.face_unity_forward()
    lp.export_fbx(out_fbx, armature=True)
    print("[build_mother_realistic] exported:", out_fbx)


if __name__ == "__main__":
    main()
