# -*- coding: utf-8 -*-
"""
build_mother_realistic.py에서 쓰는 적갈색 볼륨 곱슬 펌 머리 생성 모듈.

구성 (한 오브젝트 Mother_Hair, 머티리얼 2개):
  - M_MotherR_HairMass : 머리를 감싸는 볼륨 덩어리. 곱슬 무늬 텍스처 + 노멀맵으로 잔 컬을 표현한다.
                         얼굴 앞은 비우고, 이마 헤어라인 가장자리는 두피에 붙도록 접어 넣는다.
  - M_MotherR_Hair     : 덩어리 표면에서 늘어지는 나선 컬(링렛). 단면이 닫힌 삼각 튜브라 단면(양면) 렌더링이 필요 없다.
                         이마 쪽에는 짧은 곱슬 앞머리를 따로 배치한다.
모든 버텍스는 mixamorig:Head 본에 가중치 1.0으로 묶인다 (머리를 돌리면 통째로 따라간다).
좌표는 T-포즈 rest 기준, 정면 -Y (내보내기 직전에 180° 돌린다).
"""

import math
import os
import random

import bmesh
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

MAT_HAIR = "M_MotherR_Hair"
MAT_HAIR_MASS = "M_MotherR_HairMass"

# 적갈색 (sRGB)
HAIR_BASE = (0.40, 0.17, 0.09)
HAIR_LIGHT = (0.58, 0.28, 0.14)
HAIR_DARK = (0.22, 0.09, 0.05)

BANGS_COUNT = 40
CURL_COUNT = 160   # 늘리면 더 풍성해지지만 컬 하나당 약 100 삼각형이 늘어난다


def _smoothstep(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def _lerp3(a, b, t):
    return [a[i] + (b[i] - a[i]) * t for i in range(3)]


class HairShape:
    """머리 중심 기준 방위각(az, 0 = 정면 -Y)·고도(el)별 볼륨 외곽선."""

    def __init__(self, lm, body):
        verts = [v.co.copy() for v in body.data.vertices]
        self.bvh = BVHTree.FromPolygons(verts, [tuple(p.vertices) for p in body.data.polygons])
        head = [c for c in verts if c.z > lm.eye.z - 0.11 and abs(c.x) < 0.11]
        # 코를 빼고 이마 높이에서 가장 앞쪽을 이마 위치로 쓴다.
        forehead_y = min(c.y for c in head if c.z > lm.eye.z + 0.03)
        back_y = max(c.y for c in head)
        self.center = Vector((0.0, (forehead_y + back_y) * 0.5, lm.eye.z + 0.03))
        self.head_rx = max(abs(c.x) for c in head)
        self.top_z = max(c.z for c in head)
        self.eye_z = lm.eye.z
        self.ry_front = (self.center.y - forehead_y) + 0.022   # 이마 앞 볼륨은 얇게 (앞머리 컬이 덮는다)
        self.ry_back = (back_y - self.center.y) + 0.06
        self.rz_top = (self.top_z - self.center.z) + 0.035
        # 이마 헤어라인: 눈 위 약 5.5cm
        self.hairline_el = math.degrees(math.atan2(lm.eye.z + 0.055 - self.center.z, self.ry_front))
        self.bottom_z = lm.eye.z - 0.135   # 턱선 높이에서 끝나는 길이

    @staticmethod
    def direction(az, el):
        return Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))

    @staticmethod
    def signed_deg(az):
        return math.degrees((az + math.pi) % (2 * math.pi) - math.pi)

    def boundary_el(self, az):
        """얼굴 구멍의 위쪽 경계 고도(도). 관자놀이 쪽은 낮아지고, 노이즈로 가장자리를 들쭉날쭉하게 한다."""
        a = abs(self.signed_deg(az))
        return self.hairline_el - 0.25 * a + 3.0 * noise.noise(Vector((az * 2.5, 0.3, 0.7)))

    OPENING_HALF_AZ = 58.0

    def is_face_opening(self, az, el):
        return abs(self.signed_deg(az)) < self.OPENING_HALF_AZ and math.degrees(el) < self.boundary_el(az)

    def opening_distance(self, az, el):
        """얼굴 구멍 가장자리까지의 각도 거리(도). 구멍 위(이마)와 옆(볼·귀) 모두 고려한다."""
        d_el = math.degrees(el) - self.boundary_el(az)          # 양수 = 헤어라인 위
        d_az = abs(self.signed_deg(az)) - self.OPENING_HALF_AZ  # 양수 = 구멍 옆 바깥
        if d_el >= 0 and d_az >= 0:
            return math.hypot(d_el, d_az)
        return d_el if d_el >= 0 else d_az

    def head_radius(self, d):
        hit = self.bvh.ray_cast(self.center, d, 0.3)
        return (hit[0] - self.center).length if hit[0] is not None else None

    def outer_radius(self, az, el):
        d = self.direction(az, el)
        # 귀 아래(고도 -25° 부근)로 갈수록 옆으로 부푸는 펌 볼륨
        rx = self.head_rx + 0.05 + 0.03 * math.exp(-((math.degrees(el) + 25) / 28.0) ** 2)
        side = abs(d.x) / max(1e-4, math.hypot(d.x, d.y))
        ry = self.ry_back if d.y > 0 else self.ry_front + 0.03 * max(0.0, side - 0.3)
        r = 1.0 / math.sqrt((d.x / rx) ** 2 + (d.y / ry) ** 2 + (max(d.z, 0) / self.rz_top) ** 2 + (min(d.z, 0) / 0.2) ** 2)
        return r * (1.0 + 0.05 * noise.noise(d * 9.0))

    def surface_radius(self, az, el):
        """볼륨 표면 반지름. 얼굴 구멍 가장자리(이마·볼 옆)는 피부 쪽으로 접어 넣어,
        정면에서 볼륨 안쪽 벽(뒷면)이 들여다보이거나 가장자리가 떠 보이지 않게 한다."""
        d = self.direction(az, el)
        r = self.outer_radius(az, el)
        head_r = self.head_radius(d)
        if head_r is not None and head_r < 0.2:
            gap = self.opening_distance(az, el)
            if 0.0 <= gap < 16.0:
                tucked = head_r + 0.007
                r = tucked + (r - tucked) * _smoothstep(gap / 16.0)
            r = max(r, head_r + 0.006)
        return r

    def blocks_face(self, p):
        """컬이 얼굴(눈썹 아래~턱)을 가리는지 검사."""
        return (abs(p.x) < self.head_rx + 0.005 and p.y < self.center.y - 0.04
                and self.eye_z - 0.16 < p.z < self.eye_z + 0.032)


# ----------------------------------------------------------------------
def build_hair(body, rig, lm, tex_dir, make_material, attach, save_png):
    """make_material / attach / save_png는 build_mother_realistic.py의 도우미를 넘겨받는다."""
    textures = make_hair_textures(tex_dir, save_png)
    shape = HairShape(lm, body)
    rnd = random.Random(40)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    deform = bm.verts.layers.deform.new()

    # ---- 1) 볼륨 덩어리 (머티리얼 1) ----
    n_az, n_el = 72, 32
    el_min, el_max = math.radians(-60), math.radians(88)
    grid = {}
    for i in range(n_az):
        az = 2 * math.pi * i / n_az
        for j in range(n_el + 1):
            el = el_min + (el_max - el_min) * j / n_el
            p = shape.center + shape.direction(az, el) * shape.surface_radius(az, el)
            ok = p.z >= shape.bottom_z and not shape.is_face_opening(az, el)
            grid[(i, j)] = (bm.verts.new(p), ok, el)
    top = bm.verts.new(shape.center + Vector((0, 0, shape.surface_radius(0, math.pi / 2))))
    mass_faces = []
    for i in range(n_az):
        i2 = (i + 1) % n_az
        for j in range(n_el):
            corners = [grid[(i, j)], grid[(i2, j)], grid[(i2, j + 1)], grid[(i, j + 1)]]
            if all(c[1] for c in corners):
                face = bm.faces.new([c[0] for c in corners])
                # 방위각 이음매에서 UV가 되감기지 않도록 열 번호로 V를 계산한다 (둘레 2타일).
                for loop, c, col in zip(face.loops, corners, (i, i + 1, i + 1, i)):
                    loop[uv].uv = ((c[2] - el_min) / (el_max - el_min) * 0.9, col / n_az * 2.0)
                mass_faces.append(face)
        a, b = grid[(i, n_el)], grid[(i2, n_el)]
        face = bm.faces.new([a[0], b[0], top])
        for loop, c, col in zip(face.loops, (a, b, None), (i, i + 1, i + 0.5)):
            loop[uv].uv = (0.9, col / n_az * 2.0)
        mass_faces.append(face)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for f in mass_faces:
        f.material_index = 1
        f.smooth = True
        f.normal_update()
        if (f.calc_center_median() - shape.center).dot(f.normal) < 0:
            f.normal_flip()

    # ---- 2) 곱슬 앞머리: 헤어라인을 따라 짧은 컬을 고르게 배치 ----
    placed = 0
    for k in range(BANGS_COUNT * 3):
        if placed >= BANGS_COUNT:
            break
        az = math.radians(-50 + 100 * (k % BANGS_COUNT + rnd.random()) / BANGS_COUNT)
        el = math.radians(shape.boundary_el(az) + rnd.uniform(1.0, 14.0))
        if _place_curl(bm, uv, shape, rnd, az, el, bangs=True):
            placed += 1

    # ---- 3) 나머지 링렛 ----
    curls, attempts = 0, 0
    while curls < CURL_COUNT and attempts < 4000:
        attempts += 1
        az = rnd.uniform(0, 2 * math.pi)
        el = math.asin(rnd.uniform(math.sin(math.radians(-50)), math.sin(math.radians(85))))
        if shape.is_face_opening(az, el):
            continue
        if abs(shape.signed_deg(az)) < 50 and math.degrees(el) < shape.boundary_el(az) + 18:
            continue   # 이마 위는 앞머리 담당
        if _place_curl(bm, uv, shape, rnd, az, el, bangs=False):
            curls += 1

    for v in bm.verts:
        v[deform][0] = 1.0   # 유일한 버텍스 그룹(mixamorig:Head) → 인덱스 0
    import bpy  # Blender 안에서만 실행되므로 지연 import
    mesh = bpy.data.meshes.new("Mother_Hair")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Mother_Hair", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.vertex_groups.new(name="mixamorig:Head")
    mesh.materials.append(make_material(MAT_HAIR, image=textures["strands"], roughness=0.55))
    mesh.materials.append(make_material(MAT_HAIR_MASS, image=textures["curly"], roughness=0.7, normal_image=textures["curly_normal"]))
    attach(obj, rig)
    print("[hair] bangs:", placed, "curls:", curls, "tris:", sum(len(p.vertices) - 2 for p in mesh.polygons))
    return obj


def _place_curl(bm, uv, shape, rnd, az, el, bangs):
    d = shape.direction(az, el)
    root = shape.center + d * shape.surface_radius(az, el) * rnd.uniform(0.93, 1.0)
    if root.z < shape.bottom_z + 0.015:
        return False
    down = Vector((0, 0, -1))
    tangent = down - d * down.dot(d)
    if tangent.length < 0.2:
        tangent = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0))
    tangent.normalize()
    axis = (tangent * 0.85 + d * (0.45 if bangs else 0.25)).normalized()
    if axis.z > 0.05:   # 정수리 컬이 위로 솟지 않게 거의 수평으로 눕힌다
        axis.z = 0.05
        axis.normalize()
    length = rnd.uniform(0.03, 0.05) if bangs else rnd.uniform(0.055, 0.095)
    floor_z = shape.eye_z + 0.035 if bangs else shape.bottom_z - 0.03
    if (root + axis * length).z < floor_z:
        length = (root.z - floor_z) / max(0.2, -axis.z)
    if length < 0.02:
        return False
    if any(shape.blocks_face(root + axis * (length * k / 4.0)) for k in range(5)):
        return False
    _add_curl(bm, uv, rnd, root, axis, length, rnd.uniform(0.008, 0.012) * (0.85 if bangs else 1.0))
    return True


def _add_curl(bm, uv, rnd, root, axis, length, curl_radius):
    """root에서 axis 방향으로 감기는 나선 컬. 단면은 닫힌 삼각형(바깥쪽이 볼록)이라 어느 방향에서도 앞면만 보인다.
    URP Lit은 양면 렌더링 시 뒷면 법선을 뒤집지 않아 조명이 틀어지므로, 리본+양면 대신 닫힌 단면을 쓴다."""
    pitch = rnd.uniform(0.016, 0.022)
    turns = max(1.5, length / pitch)
    steps = max(8, int(turns * 5))
    helper = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
    b1 = axis.cross(helper).normalized()
    b2 = axis.cross(b1).normalized()
    phase0 = rnd.uniform(0, 2 * math.pi)
    width = pitch * 0.7
    v_offset = rnd.random()
    prev = None
    prev_center = None
    for s in range(steps + 1):
        t = s / steps
        phase = phase0 + t * turns * 2 * math.pi
        radial = b1 * math.cos(phase) + b2 * math.sin(phase)
        taper = 1.0 - 0.3 * t
        center = root + axis * (length * t) + radial * (curl_radius * (1.0 - 0.25 * t))
        half = axis * (width * 0.5 * taper)
        bulge = radial * (width * 0.3 * taper)
        row = (bm.verts.new(center - half), bm.verts.new(center + bulge), bm.verts.new(center + half))
        if prev is not None:
            u0, u1 = (s - 1) / steps * turns, s / steps * turns
            mid = (prev_center + center) * 0.5
            for k in range(3):
                k2 = (k + 1) % 3
                face = bm.faces.new([prev[k], row[k], row[k2], prev[k2]])
                face.smooth = True
                face.material_index = 0
                face.normal_update()
                if face.normal.dot(face.calc_center_median() - mid) < 0:
                    face.normal_flip()   # 단면 중심에서 바깥을 향하게
                v0, v1 = k / 3.0, (k + 1) / 3.0
                for loop, (uu, vv) in zip(face.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                    loop[uv].uv = (uu, v_offset + vv * 0.25)
        prev = row
        prev_center = center


# ----------------------------------------------------------------------
def make_hair_textures(tex_dir, save_png):
    """컬 리본용 가닥 텍스처와 볼륨 덩어리용 곱슬 무늬(알베도·노멀)를 절차적으로 만든다."""
    rnd = random.Random(7)
    fine = [rnd.random() for _ in range(256)]
    coarse = [rnd.random() for _ in range(32)]
    coarse_band = [coarse[y // 8] * (1 - (y % 8) / 8) + coarse[(y // 8 + 1) % 32] * ((y % 8) / 8) for y in range(256)]

    def strands(x, y):
        # U(가로) = 가닥 방향, V(세로) = 가닥마다 다른 밝기. 대비를 낮게 해 나뭇결처럼 보이지 않게 한다.
        yy = (y + int(2 * math.sin(x * 0.07 + y * 0.05))) % 256
        k = 0.55 * fine[yy] + 0.25 * coarse_band[yy] + 0.2 * (0.5 + 0.5 * math.sin(x * 0.045))
        c = _lerp3([v * 0.8 for v in HAIR_BASE], [v * 0.95 for v in HAIR_LIGHT], k)
        return (c[0], c[1], c[2], 1.0)

    strands_img = save_png(os.path.join(tex_dir, "T_MotherR_HairStrands.png"), 256, 256, strands)

    # 곱슬 무늬: 작은 나선 획을 겹쳐 그린 높이맵 → 알베도와 노멀맵. 가장자리는 이음매 없이 반복된다.
    n = 512
    height = [0.0] * (n * n)
    shade = [0.5] * (n * n)
    rnd = random.Random(11)
    for _ in range(2600):
        cx, cy = rnd.uniform(0, n), rnd.uniform(0, n)
        r0 = rnd.uniform(3.5, 9.0)
        a0 = rnd.uniform(0, 2 * math.pi)
        span = rnd.uniform(1.3, 2.4) * math.pi * rnd.choice((-1.0, 1.0))
        brightness = rnd.random()
        level = rnd.uniform(0.6, 1.0)
        steps = int(abs(span) * r0 / 0.7)
        for k in range(steps):
            t = k / steps
            ang = a0 + span * t
            rr = r0 * (1.0 - 0.35 * t)
            ix, iy = int(cx + rr * math.cos(ang)), int(cy + rr * math.sin(ang))
            for oy in (-1, 0, 1):
                for ox in (-1, 0, 1):
                    value = level * (1.0 if ox == 0 and oy == 0 else 0.55)
                    idx = ((iy + oy) % n) * n + (ix + ox) % n
                    if value > height[idx]:
                        height[idx] = value
                        shade[idx] = brightness
    blurred = [0.0] * (n * n)
    for y in range(n):
        for x in range(n):
            s = 0.0
            for oy in (-1, 0, 1):
                row = ((y + oy) % n) * n
                s += height[row + (x - 1) % n] + height[row + x] + height[row + (x + 1) % n]
            blurred[y * n + x] = s / 9.0

    def curly(x, y):
        idx = y * n + x
        h = blurred[idx]
        occlusion = 0.65 + 0.35 * min(1.0, h / 0.55)
        c = _lerp3(HAIR_DARK, HAIR_LIGHT, 0.25 + shade[idx] * 0.35 + h * 0.3)
        return (c[0] * occlusion, c[1] * occlusion, c[2] * occlusion, 1.0)

    def curly_normal(x, y):
        dx = blurred[y * n + (x + 1) % n] - blurred[y * n + (x - 1) % n]
        dy = blurred[((y + 1) % n) * n + x] - blurred[((y - 1) % n) * n + x]
        nv = Vector((-dx * 1.8, -dy * 1.8, 1.0)).normalized()
        return (nv.x * 0.5 + 0.5, nv.y * 0.5 + 0.5, nv.z * 0.5 + 0.5, 1.0)

    curly_img = save_png(os.path.join(tex_dir, "T_MotherR_HairCurly.png"), n, n, curly)
    normal_img = save_png(os.path.join(tex_dir, "T_MotherR_HairCurly_Normal.png"), n, n, curly_normal)
    return {"strands": strands_img, "curly": curly_img, "curly_normal": normal_img}
