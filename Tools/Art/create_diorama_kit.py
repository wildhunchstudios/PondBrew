"""Original Evolution Shop Pocket Aquarium Diorama kit (AD-A). Blender 5.0, no external assets or add-ons.

Run: blender --background --factory-startup --python create_diorama_kit.py -- --repo C:/dev/DumbVideoGameStudio

Builds the redesigned Pondlet and Casual visitor as rigid parts on named pivots (so the game's existing
pose code drives them) with face shape keys, plus the six hand-drawn effect shapes as one flipbook sheet.
Everything is generated from the numbers below; nothing is imported.

Authoring helpers take Unity coordinates (X right, Y up, Z forward, left-handed) and reflect them into
Blender's right-handed space as (-X, -Z, Y); the FBX export bakes the Y-up conversion. Import probes showed
that this is the mapping Unity reads back exactly, and that only parts directly under the asset root keep
their transforms, so every part is a direct child of the root and is its own pivot (a fin mesh is the
"Left fin pivot"). The visitor's face parts are reparented onto its "Head pivot" by the game at build time.
The Pondlet faces local -Z (creature convention); the visitor faces +Z (character convention).
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Vector

parser = argparse.ArgumentParser()
parser.add_argument('--repo', required=True)
parser.add_argument('--no-render', action='store_true', help='Skip the review renders.')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
REPO = Path(args.repo).resolve()
assert (REPO / 'EvolutionShop/ProjectSettings/ProjectVersion.txt').is_file()
SOURCE = REPO / 'ArtSource/DioramaKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/DioramaKit'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for item in list(block):
        block.remove(item)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
bpy.context.preferences.filepaths.save_version = 0
COLLECTION = scene.collection

# Slot names only. Unity maps each DK_* slot to its own toon material (ShopDioramaPalette); the hex values
# here are the same sRGB colours, for the review renders and the manifest.
PALETTE = {
    'DK_PondletCoral': 'FF6F57', 'DK_PondletFin': 'FF9E6E', 'DK_PondletBelly': 'FFD9A0', 'DK_PondletLip': 'FF8FA0',
    'DK_Blush': 'FF9EA0', 'DK_EyeWhite': 'FFFFFF', 'DK_Pupil': '1B413E', 'DK_Glint': 'FFFFFF',
    'DK_CasualKnit': 'F08A3C', 'DK_Collar': 'FFF6DF', 'DK_Trousers': '2B3B4F', 'DK_Shoes': '3B2A22',
    'DK_Skin': 'B9774E', 'DK_Beret': '1B2B33', 'DK_Nub': 'D66145', 'DK_Tote': 'E9D6A8', 'DK_Hair': '3B2A22',
    'DK_Mouth': '5A1F22', 'DK_Button': 'D66145',
}
MAT = {}
def srgb_linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4
for name, code in PALETTE.items():
    mat = bpy.data.materials.new(name)
    rgb = [int(code[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    mat.diffuse_color = (*[srgb_linear(v) for v in rgb], 1)
    MAT[name] = mat

def u(p):
    """Unity point -> Blender point. A reflection, since Unity is left-handed and Blender right-handed."""
    return Vector((-p[0], -p[2], p[1]))

def un(p):
    """Blender point -> Unity point (the inverse of u)."""
    return (-p[0], p[2], -p[1])

def rot(p, euler_deg):
    """Rotate a Unity-space offset by Unity Euler degrees (applied Z, then X, then Y, as Unity does)."""
    x, y, z = (math.radians(a) for a in euler_deg)
    v = Vector(p)
    v = Vector((v.x * math.cos(z) - v.y * math.sin(z), v.x * math.sin(z) + v.y * math.cos(z), v.z))
    v = Vector((v.x, v.y * math.cos(x) - v.z * math.sin(x), v.y * math.sin(x) + v.z * math.cos(x)))
    v = Vector((v.x * math.cos(y) + v.z * math.sin(y), v.y, -v.x * math.sin(y) + v.z * math.cos(y)))
    return v

# ---------------------------------------------------------------- mesh building (Unity space, pure python)
class Part:
    """Accumulates one exported mesh: vertex list, faces and per-face slot index, in Unity space relative to a pivot."""
    def __init__(self, name):
        self.name, self.verts, self.faces, self.slots, self.mats = name, [], [], [], []
    def slot(self, material):
        if material not in self.mats:
            self.mats.append(material)
        return self.mats.index(material)
    def add(self, verts, faces, material):
        base, index = len(self.verts), self.slot(material)
        self.verts += [Vector(v) for v in verts]
        self.faces += [tuple(base + i for i in f) for f in faces]
        self.slots += [index] * len(faces)
        return range(base, base + len(verts))

def sphere_grid(seg, rings):
    """Unit sphere (Y poles) as (verts, faces, params) with params (phi, theta) for each vertex."""
    verts, params = [(0, 1, 0)], [(0, 0)]
    for r in range(1, rings):
        phi = math.pi * r / rings
        for s in range(seg):
            theta = math.tau * s / seg
            verts.append((math.sin(phi) * math.cos(theta), math.cos(phi), math.sin(phi) * math.sin(theta)))
            params.append((phi, theta))
    verts.append((0, -1, 0)); params.append((math.pi, 0))
    faces, bottom = [], len(verts) - 1
    for s in range(seg):
        faces.append((0, 1 + (s + 1) % seg, 1 + s))
    for r in range(rings - 2):
        a, b = 1 + r * seg, 1 + (r + 1) * seg
        for s in range(seg):
            t = (s + 1) % seg
            faces.append((a + s, a + t, b + t, b + s))
    last = 1 + (rings - 2) * seg
    for s in range(seg):
        faces.append((last + s, last + (s + 1) % seg, bottom))
    return verts, faces, params

def ellipsoid(part, center, radii, material, euler=(0, 0, 0), seg=28, rings=16, squarish=1.0):
    """Ellipsoid (squarish > 1 makes a rounded box) rotated by Unity Euler degrees and placed at center."""
    unit, faces, _ = sphere_grid(seg, rings)
    out = []
    for v in unit:
        x, y, z = v
        if squarish != 1.0:
            m = (abs(x) ** squarish + abs(y) ** squarish + abs(z) ** squarish) ** (1 / squarish)
            m = max(m, 1e-6)
            x, y, z = x / m, y / m, z / m
        p = rot((x * radii[0], y * radii[1], z * radii[2]), euler)
        out.append((p.x + center[0], p.y + center[1], p.z + center[2]))
    return part.add(out, faces, material)

def loft_z(part, z0, z1, rings, seg, profile, material, belly=None, yoff=lambda t: 0):
    """Closed surface along Z. profile(t) -> (rx, ry_top, ry_bottom); poles at both ends."""
    verts, faces, slot_override = [], [], []
    verts.append((0, yoff(0), z0))
    for r in range(1, rings):
        t = r / rings
        rx, ryt, ryb = profile(t)
        z = z0 + (z1 - z0) * t
        for s in range(seg):
            a = math.tau * s / seg
            c, sn = math.cos(a), math.sin(a)
            verts.append((rx * c, yoff(t) + (ryt if sn > 0 else ryb) * sn, z))
    verts.append((0, yoff(1), z1))
    end = len(verts) - 1
    for s in range(seg):
        faces.append((0, 1 + s, 1 + (s + 1) % seg))
    for r in range(rings - 2):
        a, b = 1 + r * seg, 1 + (r + 1) * seg
        for s in range(seg):
            t = (s + 1) % seg
            faces.append((a + s, b + s, b + t, a + t))
    last = 1 + (rings - 2) * seg
    for s in range(seg):
        faces.append((last + s, end, last + (s + 1) % seg))
    rng = part.add(verts, faces, material)
    if belly:
        # Faces whose ring angle points low enough (and away from the very ends) take the belly slot.
        bslot = part.slot(belly[0])
        first_face = len(part.faces) - len(faces)
        for fi, f in enumerate(faces):
            ys = [verts[i][1] - yoff(0) for i in f]
            zs = [verts[i][2] for i in f]
            tmid = (sum(zs) / len(zs) - z0) / (z1 - z0)
            if belly[1](sum(ys) / len(ys), tmid):
                part.slots[first_face + fi] = bslot
    return rng

def shell(part, center, radii, material, euler, phi_max, thickness=.012, seg=24, rows=8):
    """A cap of an ellipsoid from its top pole down to phi_max (radians), with thickness. Returns (indices, builder)."""
    def build(phi_limit):
        verts = []
        for layer, scale in ((0, 1.0), (1, 1.0 - thickness / max(radii))):
            verts.append((0, radii[1] * scale, 0))
            for r in range(1, rows + 1):
                phi = phi_limit * r / rows
                for s in range(seg):
                    th = math.tau * s / seg
                    verts.append((math.sin(phi) * math.cos(th) * radii[0] * scale, math.cos(phi) * radii[1] * scale,
                                  math.sin(phi) * math.sin(th) * radii[2] * scale))
        out = []
        for v in verts:
            p = rot(v, euler)
            out.append((p.x + center[0], p.y + center[1], p.z + center[2]))
        return out
    n = 1 + rows * seg
    faces = []
    for layer in (0, 1):
        o = layer * n
        for s in range(seg):
            f = (o, o + 1 + (s + 1) % seg, o + 1 + s)
            faces.append(f if layer == 0 else f[::-1])
        for r in range(rows - 1):
            a, b = o + 1 + r * seg, o + 1 + (r + 1) * seg
            for s in range(seg):
                t = (s + 1) % seg
                f = (a + s, a + t, b + t, b + s)
                faces.append(f if layer == 0 else f[::-1])
    rim_o, rim_i = 1 + (rows - 1) * seg, n + 1 + (rows - 1) * seg
    for s in range(seg):
        t = (s + 1) % seg
        faces.append((rim_o + s, rim_o + t, rim_i + t, rim_i + s))
    idx = part.add(build(phi_max), faces, material)
    return idx, build

def torus(part, center, major, minor, material, scale=(1, 1, 1), seg=36, sides=12, euler=(0, 0, 0)):
    """Ring around the Z axis (faces -Z/+Z)."""
    verts, faces = [], []
    for s in range(seg):
        a = math.tau * s / seg
        for j in range(sides):
            b = math.tau * j / sides
            r = major + minor * math.cos(b)
            p = rot((r * math.cos(a) * scale[0], r * math.sin(a) * scale[1], minor * math.sin(b) * scale[2]), euler)
            verts.append((p.x + center[0], p.y + center[1], p.z + center[2]))
    for s in range(seg):
        for j in range(sides):
            a, b = s * sides + j, s * sides + (j + 1) % sides
            c, d = ((s + 1) % seg) * sides + (j + 1) % sides, ((s + 1) % seg) * sides + j
            faces.append((a, b, c, d))
    return part.add(verts, faces, material)

def tube(part, points, radius, material, sides=10):
    verts = []
    for i, p in enumerate(points):
        c = Vector(p)
        tan = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
        side = tan.cross(Vector((0, 1, 0)))
        if side.length < .01:
            side = tan.cross(Vector((1, 0, 0)))
        side.normalize(); other = tan.cross(side).normalized()
        for k in range(sides):
            a = math.tau * k / sides
            verts.append(tuple(c + radius * (math.cos(a) * side + math.sin(a) * other)))
    faces = []
    for r in range(len(points) - 1):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append((r * sides + k, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + k))
    faces += [tuple(range(sides - 1, -1, -1)), tuple((len(points) - 1) * sides + k for k in range(sides))]
    return part.add(verts, faces, material)

# ---------------------------------------------------------------- objects, pivots and shape keys
RECORDS = []

def make_object(part, root, pivot=(0, 0, 0), name=None):
    """Mesh object directly under the root whose origin is the pivot (Unity space); vertices are in root space."""
    me = bpy.data.meshes.new(part.name)
    px = Vector(pivot)
    me.from_pydata([u(Vector(v) - px) for v in part.verts], [], part.faces)
    for m in part.mats:
        me.materials.append(MAT[m])
    for poly, s in zip(me.polygons, part.slots):
        poly.material_index = s
    me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    me.shade_smooth()
    obj = bpy.data.objects.new(name or part.name, me)
    COLLECTION.objects.link(obj)
    obj.parent = root
    obj.location = u(px)
    return obj

def empty(name, pivot=(0, 0, 0), root=None):
    """An empty (the asset root, or a joint with no mesh of its own directly under the root)."""
    obj = bpy.data.objects.new(name, None)
    COLLECTION.objects.link(obj)
    obj.empty_display_size = .05
    if root is not None:
        obj.parent = root
    obj.location = u(Vector(pivot))
    return obj

def shape_key(obj, part, name, fn, indices=None, pivot=(0, 0, 0)):
    """fn(index, unity_root_vertex) -> new unity_root_vertex for the given vertex indices."""
    if obj.data.shape_keys is None:
        obj.shape_key_add(name='Basis', from_mix=False)
    key = obj.shape_key_add(name=name, from_mix=False)
    key.value = 0.0  # Blender 5.0 adds new keys at full strength; the rest pose is the basis
    px = Vector(pivot)
    for i in (indices if indices is not None else range(len(part.verts))):
        key.data[i].co = u(Vector(fn(i, part.verts[i])) - px)
    return key

def stats(root, kind):
    tris, verts = 0, 0
    mins, maxs = [1e9] * 3, [-1e9] * 3
    for obj in [root] + list(root.children_recursive):
        if obj.type != 'MESH':
            continue
        tris += sum(len(p.vertices) - 2 for p in obj.data.polygons)
        verts += len(obj.data.vertices)
        for v in obj.data.vertices:
            w = un(obj.matrix_world @ v.co)
            for k in range(3):
                mins[k] = min(mins[k], w[k]); maxs[k] = max(maxs[k], w[k])
    return {'triangles': tris, 'vertices': verts, 'bounds_unity_m': {'min': mins, 'max': maxs, 'size': [maxs[k] - mins[k] for k in range(3)]}}

# ================================================================ Pondlet (front -Z)
def pondlet():
    root = empty('DioramaPondlet')
    root['front'] = 'creature local -Z'
    Z0, Z1 = -.37, .38
    def prof(t):
        if t < .42:
            f = math.sqrt(max(0, 1 - (1 - t / .42) ** 2))
        else:
            s = (t - .42) / .58
            f = 1 - .78 * (s * s * (3 - 2 * s))
        return .27 * f, .245 * f, .215 * f
    yoff = lambda t: .015 * t * t
    body = Part('Body')
    loft_z(body, Z0, Z1, 30, 36, prof, 'DK_PondletCoral',
           belly=('DK_PondletBelly', lambda y, t: y < -.105 and .06 < t < .86), yoff=yoff)
    body_obj = make_object(body, root)
    def puffed(i, v):
        t = (v[2] - Z0) / (Z1 - Z0)
        g = 1 + .16 * math.sin(math.pi * min(1, max(0, t)) ** .8)
        return (v[0] * g, yoff(t) + (v[1] - yoff(t)) * g, v[2])
    shape_key(body_obj, body, 'Puffed', puffed)

    # Big glossy eyes: a third of the head's height, set forward on the sides.
    eyes, pupils, glints, lids, blush = Part('Eyes'), Part('Pupils'), Part('Glints'), Part('Lids'), Part('Blush')
    lid_builders = []
    for side in (-1, 1):
        c = (side * .132, .072, -.262)
        yaw = -side * 32  # positive Unity yaw turns -Z toward -X, so the right eye (+X) takes a negative yaw
        ellipsoid(eyes, c, (.088, .104, .072), 'DK_EyeWhite', (0, yaw, 0), 24, 14)
        fwd = rot((0, 0, -1), (0, yaw, 0))
        pc = Vector(c) + fwd * .052 + Vector((0, -.004, 0))
        ellipsoid(pupils, pc, (.052, .066, .036), 'DK_Pupil', (0, yaw, 0), 20, 12)
        gc = Vector(c) + fwd * .083 + Vector((-side * .006 - .014, .03, 0))
        ellipsoid(glints, gc, (.019, .021, .012), 'DK_Glint', (0, yaw, 0), 12, 8)
        idx, build = shell(lids, c, (.094, .110, .078), 'DK_PondletCoral', (0, yaw, 0), math.radians(24), .010, 24, 8)
        lid_builders.append((idx, build))
        ellipsoid(blush, (side * .208, -.03, -.228), (.046, .028, .016), 'DK_Blush', (0, -side * 52, 0), 16, 8)
    for p in (eyes, pupils, glints, blush):
        make_object(p, root)
    lids_obj = make_object(lids, root)
    closed = {}
    for idx, build in lid_builders:
        for i, v in zip(idx, build(math.radians(104))):
            closed[i] = v
    shape_key(lids_obj, lids, 'Blink', lambda i, v: closed[i])
    happy_lid = {}
    for idx, build in lid_builders:
        for i, v in zip(idx, build(math.radians(58))):
            happy_lid[i] = v
    shape_key(lids_obj, lids, 'Happy', lambda i, v: happy_lid[i])

    # Pouty lips on the nose with a dark mouth inside; shape keys open them into a smile or an O.
    lips = Part('Lips')
    lc = Vector((0, -.032, -.382))
    torus(lips, lc, .046, .025, 'DK_PondletLip', (1.22, .82, 1.0), 32, 12)
    inside = ellipsoid(lips, lc + Vector((0, 0, .012)), (.038, .024, .012), 'DK_Mouth', (0, 0, 0), 16, 8)
    lips_obj = make_object(lips, root)
    def smile(i, v):
        d = Vector(v) - lc
        x = d.x * 1.18
        y = d.y * .8 + .03 * (d.x / .07) ** 2 - .006
        return lc + Vector((x, y, d.z))
    def surprised(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * 1.05, d.y * 1.55, d.z))
    shape_key(lips_obj, lips, 'Happy', smile)
    shape_key(lips_obj, lips, 'Surprised', surprised)

    # Chunky fins; each fin mesh is the pivot the swim code already rotates.
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .205, -.07, -.04)
        fin = Part(label + ' fin')
        ellipsoid(fin, (pivot[0] + side * .05, pivot[1] - .045, pivot[2] + .09), (.034, .078, .13), 'DK_PondletFin',
                  (0, side * 18, side * -38), 20, 12)
        make_object(fin, root, pivot, label + ' fin pivot')
    tail_pivot = (0, .012, .33)
    tail = Part('Tail')
    ellipsoid(tail, (0, tail_pivot[1], tail_pivot[2] + .03), (.05, .07, .08), 'DK_PondletFin', (0, 0, 0), 16, 10)
    for side in (-1, 1):
        ellipsoid(tail, (0, tail_pivot[1] + side * .105, tail_pivot[2] + .165), (.042, .155, .19), 'DK_PondletFin',
                  (side * 36, 0, 0), 22, 14)
    make_object(tail, root, tail_pivot, 'Tail pivot')
    crest_pivot = (0, .2, -.01)
    crest = Part('Crest')
    ellipsoid(crest, (0, crest_pivot[1] + .045, crest_pivot[2] + .07), (.034, .085, .155), 'DK_PondletFin', (24, 0, 0), 20, 12)
    make_object(crest, root, crest_pivot, 'Crest pivot')
    return root

# ================================================================ Casual visitor (front +Z)
def catmull(points, t):
    """Piecewise Catmull-Rom through (y, r) points, sampled at normalized t."""
    n = len(points) - 1
    f = min(n - 1e-6, max(0, t * n)); i = int(f); s = f - i
    p0 = points[max(i - 1, 0)][1]; p1 = points[i][1]; p2 = points[i + 1][1]; p3 = points[min(i + 2, n)][1]
    return .5 * ((2 * p1) + (-p0 + p2) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s + (-p0 + 3 * p1 - 3 * p2 + p3) * s ** 3)

def casual():
    root = empty('DioramaCasualVisitor')
    root['front'] = 'character local +Z'
    # Short trousered legs; each leg mesh is the hip joint the walk code swings.
    for side, label in ((-1, 'Left'), (1, 'Right')):
        hip = (side * .11, .40, 0)
        leg = Part(label + ' leg')
        ellipsoid(leg, (hip[0], hip[1] - .15, 0), (.095, .175, .10), 'DK_Trousers', (0, 0, 0), 20, 12)
        ellipsoid(leg, (hip[0], hip[1] - .33, .045), (.105, .072, .155), 'DK_Shoes', (0, 0, 0), 20, 10, 1.6)
        make_object(leg, root, hip, label + ' hip')
    # Round knit body: an egg with a cream collar, coral buttons and a patch pocket.
    body = Part('Body')
    radius = [(.30, .02), (.33, .2), (.40, .285), (.50, .32), (.62, .325), (.74, .30), (.85, .25), (.93, .17), (.97, .07)]
    y0, y1 = .30, .98
    seg, rings = 36, 28
    verts = [(0, y0, 0)]
    for r in range(1, rings):
        t = r / rings
        y = y0 + (y1 - y0) * t
        rr = catmull(radius, (y - radius[0][0]) / (radius[-1][0] - radius[0][0]))
        for s in range(seg):
            a = math.tau * s / seg
            verts.append((rr * math.cos(a), y, rr * .8 * math.sin(a)))
    verts.append((0, y1, 0))
    faces = []
    for s in range(seg):
        faces.append((0, 1 + (s + 1) % seg, 1 + s))
    for r in range(rings - 2):
        a, b = 1 + r * seg, 1 + (r + 1) * seg
        for s in range(seg):
            t = (s + 1) % seg
            faces.append((a + s, a + t, b + t, b + s))
    last, end = 1 + (rings - 2) * seg, len(verts) - 1
    for s in range(seg):
        faces.append((last + s, last + (s + 1) % seg, end))
    body.add(verts, faces, 'DK_CasualKnit')
    ellipsoid(body, (0, .925, 0), (.205, .07, .175), 'DK_Collar', (0, 0, 0), 28, 10)
    for i in range(3):
        by = .80 - i * .12
        front = catmull(radius, (by - radius[0][0]) / (radius[-1][0] - radius[0][0])) * .8
        ellipsoid(body, (0, by, front + .004), (.026, .026, .014), 'DK_Button', (0, 0, 0), 12, 8)
    ellipsoid(body, (-.165, .52, .205), (.06, .05, .012), 'DK_Collar', (0, -34, 0), 16, 8, 2.4)
    make_object(body, root)
    # Arms with mitten hands (thumb forward); the tote hangs from the right hand.
    for side, label in ((-1, 'Left'), (1, 'Right')):
        sh = (side * .27, .86, 0)
        arm = Part(label + ' arm')
        ellipsoid(arm, (sh[0] + side * .025, sh[1] - .14, 0), (.082, .175, .086), 'DK_CasualKnit', (0, 0, side * 9), 20, 12)
        torus(arm, (sh[0] + side * .046, sh[1] - .285, 0), .06, .022, 'DK_Collar', (1, 1, 1), 24, 8, (90, 0, 0))
        hand = (sh[0] + side * .05, sh[1] - .36, .012)
        ellipsoid(arm, hand, (.074, .084, .062), 'DK_Skin', (0, 0, 0), 18, 10)
        ellipsoid(arm, (hand[0] - side * .045, hand[1] + .012, hand[2] + .042), (.03, .046, .03), 'DK_Skin', (-30, 0, side * 20), 12, 8)
        if side > 0:
            bag = (hand[0] + .012, hand[1] - .23, hand[2] + .01)
            ellipsoid(arm, bag, (.065, .15, .15), 'DK_Tote', (0, 0, 0), 20, 12, 3.2)
            for dz in (-.05, .05):
                tube(arm, [(bag[0], bag[1] + .12, bag[2] + dz), (hand[0] + .01, hand[1] - .03, hand[2] + dz * .6),
                           (hand[0] + .004, hand[1] + .02, hand[2] + dz * .3)], .011, 'DK_Tote', 8)
        make_object(arm, root, sh, label + ' shoulder')
    # Head on its pivot: big face, eyes with lids, brows, button nose, open mouth, blush and a beret. Every face
    # part has its origin at the head pivot (the brows at their own pivots); the game reparents them onto it.
    hp = (0, .95, 0)
    empty('Head pivot', hp, root)
    hc = Vector((0, hp[1] + .33, .01))
    head = Part('Head')
    ellipsoid(head, hc, (.34, .32, .31), 'DK_Skin', (0, 0, 0), 36, 22)
    for side in (-1, 1):
        ellipsoid(head, hc + Vector((side * .335, -.02, -.01)), (.05, .075, .06), 'DK_Skin', (0, 0, 0), 14, 8)
    ellipsoid(head, hc + Vector((0, -.06, .305)), (.046, .04, .04), 'DK_Skin', (0, 0, 0), 14, 8)
    ellipsoid(head, hc + Vector((0, .14, -.05)), (.345, .25, .32), 'DK_Hair', (0, 0, 0), 32, 16)
    beret = hc + Vector((-.03, .29, -.02))
    ellipsoid(head, beret, (.335, .078, .305), 'DK_Beret', (0, 0, 8), 36, 14)
    ellipsoid(head, beret + Vector((-.012, .085, 0)), (.034, .034, .034), 'DK_Nub', (0, 0, 0), 12, 8)
    make_object(head, root, hp)
    eyes, pupils, glints, lids, blush = Part('Eyes'), Part('Pupils'), Part('Glints'), Part('Lids'), Part('Blush')
    lid_builders = []
    for side in (-1, 1):
        c = hc + Vector((side * .118, .035, .262))
        ellipsoid(eyes, c, (.082, .098, .05), 'DK_EyeWhite', (0, 0, 0), 22, 12)
        ellipsoid(pupils, c + Vector((side * .004, -.008, .034)), (.044, .058, .026), 'DK_Pupil', (0, 0, 0), 18, 10)
        ellipsoid(glints, c + Vector((side * .004 - .016, .016, .056)), (.014, .016, .008), 'DK_Glint', (0, 0, 0), 10, 6)
        idx, build = shell(lids, c, (.088, .104, .056), 'DK_Skin', (0, 0, 0), math.radians(18), .008, 22, 8)
        lid_builders.append((idx, build))
        ellipsoid(blush, hc + Vector((side * .2, -.08, .225)), (.05, .028, .016), 'DK_Blush', (0, side * 34, 0), 14, 8)
    for p in (eyes, pupils, glints, blush):
        make_object(p, root, hp)
    lids_obj = make_object(lids, root, hp)
    closed = {}
    for idx, build in lid_builders:
        for i, v in zip(idx, build(math.radians(104))):
            closed[i] = v
    shape_key(lids_obj, lids, 'Blink', lambda i, v: closed[i], pivot=hp)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        brow = Part(label + ' brow')
        bc = hc + Vector((side * .118, .16, .262))
        ellipsoid(brow, bc, (.06, .017, .02), 'DK_Hair', (0, 0, -side * 8), 14, 8)
        make_object(brow, root, tuple(bc), label + ' brow pivot')
    mouth = Part('Mouth')
    mc = hc + Vector((0, -.16, .272))
    ellipsoid(mouth, mc, (.072, .034, .02), 'DK_Mouth', (-12, 0, 0), 22, 12)
    mouth_obj = make_object(mouth, root, hp)
    def m(fn):
        return lambda i, v: tuple(mc + fn(Vector(v) - mc))
    shape_key(mouth_obj, mouth, 'Happy', m(lambda d: Vector((d.x * 1.25, (d.y * 1.35 - .012 if d.y < 0 else d.y * .6) + .016 * (d.x / .072) ** 2, d.z))), pivot=hp)
    shape_key(mouth_obj, mouth, 'Haggle', m(lambda d: Vector((d.x * .9 + .018, d.y * .28 + .012 * d.x / .072, d.z))), pivot=hp)
    shape_key(mouth_obj, mouth, 'Shocked', m(lambda d: Vector((d.x * .62, d.y * 1.75 - .01, d.z))), pivot=hp)
    shape_key(mouth_obj, mouth, 'Ouch', m(lambda d: Vector((d.x * 1.1, d.y * .42 + .009 * math.sin(d.x * 70), d.z))), pivot=hp)
    return root

# ================================================================ hand-drawn effect sheet
CELL, COLS = 128, 4
SHEET_ROWS = ['bubble', 'swirl', 'sparkle', 'puff', 'ripple', 'coin']
INK = np.array([0x1B, 0x41, 0x3E]) / 255

def flipbook():
    ss = 3
    n = CELL * ss
    g = (np.arange(n) + .5) / n - .5
    X, Y = np.meshgrid(g, -g)
    R, T = np.hypot(X, Y), np.arctan2(Y, X)
    def wob(seed, amp=.008):
        return amp * (np.sin(3 * T + seed) + .6 * np.sin(5 * T + 2.1 * seed) + .4 * np.sin(7 * T - seed))
    def ellipse(cx, cy, rx, ry):
        return (np.hypot((X - cx) / rx, (Y - cy) / ry) - 1) * min(rx, ry)
    def star(r_in, r_out, spin, cx=0.0, cy=0.0):
        # Four pointed tips with concave sides (an astroid), plus a small round core so the centre reads.
        c, s = math.cos(spin), math.sin(spin)
        x, y = (X - cx) * c - (Y - cy) * s, (X - cx) * s + (Y - cy) * c
        p = .55
        m = (np.abs(x) ** p + np.abs(y) ** p) ** (1 / p)
        tips = (m - r_out) * .45
        core = np.hypot(x, y) - r_in
        return np.minimum(tips, core)
    def cell(frame):
        rgb = np.ones((n, n, 3)); alpha = np.zeros((n, n))
        return rgb, alpha
    def paint(rgb, alpha, d, fill=(1, 1, 1), fill_alpha=1.0, line=.022):
        inside = np.clip(.5 - d / (1.2 / n), 0, 1)
        edge = np.clip(.5 - (np.abs(d) - line * .5) / (1.2 / n), 0, 1)
        f = np.array(fill)
        rgb[:] = rgb * (1 - inside[..., None] * fill_alpha) + f * inside[..., None] * fill_alpha
        alpha[:] = np.maximum(alpha, inside * fill_alpha)
        rgb[:] = rgb * (1 - edge[..., None]) + INK * edge[..., None]
        alpha[:] = np.maximum(alpha, edge)
    def highlight(rgb, alpha, d, a=.95):
        inside = np.clip(.5 - d / (1.2 / n), 0, 1) * a
        rgb[:] = rgb * (1 - inside[..., None]) + inside[..., None]
        alpha[:] = np.maximum(alpha, inside)
    frames = []
    for row, kind in enumerate(SHEET_ROWS):
        for f in range(COLS):
            rgb, alpha = cell(f)
            s = row * 11 + f * 3
            if kind == 'bubble':
                if f < 3:
                    r = (.2, .36, .37)[f]; sq = (1, 1, .9)[f]
                    d = ellipse(0, 0, r / sq ** .5, r * sq ** .5) + wob(s)
                    paint(rgb, alpha, d, (1, 1, 1), .28)
                    highlight(rgb, alpha, ellipse(-r * .38, r * .42, r * .22, r * .12) + wob(s + 1, .003))
                else:
                    ring = np.abs(R - .34) - .015 + wob(s, .01)
                    gaps = np.cos(5 * T + 1.3) > .35
                    ring = np.where(gaps, .5, ring)
                    paint(rgb, alpha, ring, (1, 1, 1), 1.0, .02)
                    for k in range(4):
                        a = k * math.tau / 4 + .6
                        paint(rgb, alpha, ellipse(.43 * math.cos(a), .43 * math.sin(a), .045, .045), (1, 1, 1), .9)
            elif kind == 'swirl':
                # A fat curled ribbon drawn as a band around an Archimedean path.
                turn = f * .7
                rr = .08 + (T + math.pi + turn) % math.tau / math.tau * .3
                band = np.abs(R - rr) - (.035 + .02 * (rr - .08) / .3)
                band = np.where(R > .44, .5, band)
                paint(rgb, alpha, band + wob(s, .004), (1, 1, 1), 1.0, .02)
            elif kind == 'sparkle':
                size = (.22, .42, .38, .2)[f]; spin = (0, 0, .25, .5)[f]
                paint(rgb, alpha, star(size * .2, size, spin) + wob(s, .003), (1, 1, 1), 1.0)
                if f in (1, 2):
                    paint(rgb, alpha, star(.025, .11, spin + .4, .3, .3), (1, 1, 1), 1.0, .018)
            elif kind == 'puff':
                grow = (.7, 1.0, 1.12, 1.2)[f]
                blobs = [(0, -.02, .2), (-.17, -.06, .14), (.17, -.07, .15), (-.08, .12, .15), (.1, .11, .13)]
                if f == 3:
                    blobs = [(-.24, -.04, .12), (.2, .02, .11), (-.02, .22, .1)]
                d = np.full_like(X, 9.0)
                for bx, by, br in blobs:
                    d = np.minimum(d, ellipse(bx * grow, by * grow, br * grow, br * grow))
                d += wob(s, .006)
                paint(rgb, alpha, d, (1, 1, 1), 1.0 if f < 3 else .85)
                # A soft shaded crescent along the lower edge only.
                inner = np.clip(.5 - d / (1.2 / n), 0, 1)
                crescent = inner * np.clip((d + .055) / .03, 0, 1) * np.clip(-Y / .08, 0, 1)
                rgb[:] = rgb * (1 - .2 * crescent[..., None])
            elif kind == 'ripple':
                r = (.14, .24, .34, .43)[f]
                a = (1, .95, .8, .55)[f]
                for k, rr in enumerate((r, r * .58)):
                    if f == 0 and k == 1:
                        continue
                    ring = np.abs(np.hypot(X / 1.0, Y / .45) - rr) - .012
                    paint(rgb, alpha, ring + wob(s + k, .003), (1, 1, 1), a, .016)
                alpha *= a
            elif kind == 'coin':
                w = (1, .62, .14, .62)[f]
                d = ellipse(0, 0, .3 * w + .02, .3)
                paint(rgb, alpha, d + wob(s, .003), (1, 1, 1), 1.0, .026)
                if f != 2:
                    paint(rgb, alpha, np.abs(ellipse(0, 0, .2 * w, .2)) - .006, (1, 1, 1), 0, .018)
                    highlight(rgb, alpha, ellipse(-.1 * w, .14, .05 * w + .01, .03), .8)
            # Downsample the supersampled cell (box filter).
            rgb_d = rgb.reshape(CELL, ss, CELL, ss, 3).mean(axis=(1, 3))
            a_d = alpha.reshape(CELL, ss, CELL, ss).mean(axis=(1, 3))
            frames.append((row, f, rgb_d, a_d))
    W, H = CELL * COLS, CELL * len(SHEET_ROWS)
    sheet = np.zeros((H, W, 4))
    for row, f, rgb_d, a_d in frames:
        sheet[row * CELL:(row + 1) * CELL, f * CELL:(f + 1) * CELL, :3] = rgb_d
        sheet[row * CELL:(row + 1) * CELL, f * CELL:(f + 1) * CELL, 3] = a_d
    img = bpy.data.images.new('DioramaFlipbook', W, H, alpha=True, float_buffer=False)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(np.flipud(sheet).astype(np.float32).ravel())
    path = EXPORT / 'DioramaFlipbook.png'
    img.filepath_raw = str(path); img.file_format = 'PNG'
    img.save()
    return path, W, H

# ================================================================ build, export, review renders, manifest
assets = {}
for name, build in (('DioramaPondlet', pondlet), ('DioramaCasualVisitor', casual)):
    # Blender object names are global, so each asset is built and exported before the next one is made; its parts
    # are then renamed with its id, freeing the shared names (Eyes, Lids, Body...) for the next asset.
    root = assets[name] = build()
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    dest = EXPORT / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(dest), use_selection=True, global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
                             bake_space_transform=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=False,
                             mesh_smooth_type='OFF', use_tspace=False, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False)
    rec = stats(root, name)
    rec.update({'id': name, 'export': str(dest.relative_to(REPO)).replace('\\', '/'),
                'sha256': hashlib.sha256(dest.read_bytes()).hexdigest(), 'bytes': dest.stat().st_size,
                'objects': sorted(o.name for o in root.children_recursive),
                'shape_keys': {o.name: [k.name for k in o.data.shape_keys.key_blocks[1:]] for o in root.children_recursive
                               if o.type == 'MESH' and o.data.shape_keys},
                'material_slots': sorted({m.name for o in root.children_recursive if o.type == 'MESH' for m in o.data.materials})})
    assert rec['triangles'] < 40000, rec
    assert not any('.' in o for o in rec['objects']), rec['objects']
    RECORDS.append(rec)
    for child in root.children_recursive:
        child.name = name + ' / ' + child.name

sheet_path, sheet_w, sheet_h = flipbook()

renders = []
if not args.no_render:
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_object_outline = True
    scene.display.shading.object_outline_color = (0.02, .06, .06)
    scene.display.shading.show_cavity = True
    scene.display.shading.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new('Review sky') if scene.world is None else scene.world
    scene.world.color = (.85, .78, .62)
    assets['DioramaCasualVisitor'].location = u((1.3, 0, 0))
    camdata = bpy.data.cameras.new('Diorama review camera'); cam = bpy.data.objects.new('Diorama review camera', camdata)
    COLLECTION.objects.link(cam); scene.camera = cam; camdata.lens = 50
    def shot(fname, pos, target):
        cam.location = u(pos)
        cam.rotation_euler = (u(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(SOURCE / fname)
        bpy.ops.render.render(write_still=True)
        renders.append(str((SOURCE / fname).relative_to(REPO)).replace('\\', '/'))
    shot('DioramaKit_Pondlet_Front.png', (-1.25, .35, -1.55), (0, .02, 0))
    shot('DioramaKit_Pondlet_Side.png', (-2.0, .1, .02), (0, .02, .06))
    shot('DioramaKit_Casual_Front.png', (1.3 + .9, 1.25, 3.4), (1.3, .86, 0))
    shot('DioramaKit_Casual_Face.png', (1.3 + .25, 1.35, 1.3), (1.3, 1.28, 0))
    assets['DioramaCasualVisitor'].location = u((0, 0, 0))

blend = SOURCE / 'EvolutionShop_DioramaKit.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
manifest = {
    'schema': 1, 'title': 'Evolution Shop Pocket Aquarium Diorama kit (AD-A slice)',
    'origin': {'author': 'Original local Blender authorship by Claude for Evolution Shop (generated from Tools/Art/create_diorama_kit.py)',
               'external_sources': [], 'license': 'Original project work; no third-party source or asset license',
               'paid_generation': False, 'external_uploads': False},
    'blender_version': bpy.app.version_string, 'source_script': 'Tools/Art/create_diorama_kit.py',
    'source_script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'blend_source': str(blend.relative_to(REPO)).replace('\\', '/'),
    'scale': '1 unit = 1 metre', 'up': 'Unity +Y',
    'front': {'DioramaPondlet': 'local -Z (creature)', 'DioramaCasualVisitor': 'local +Z (character)'},
    'export_settings': {'format': 'FBX', 'forward': '-Z', 'up': 'Y', 'global_scale': 1, 'bake_space_transform': True,
                        'animations': False, 'shape_keys': True, 'modifiers': 'none (shape keys require unmodified meshes)',
                        'axis_mapping': 'Unity (x, y, z) -> Blender (-x, -z, y), a reflection; probed to import exactly',
                        'hierarchy': 'every part directly under the asset root and its own pivot; the visitor face parts are reparented onto Head pivot in game'},
    'unity_import_expected': {'globalScale': 1, 'useFileScale': True, 'importAnimation': False, 'addCollider': False,
                              'importBlendShapes': True, 'material_mapping': 'DK_* slot names map to runtime toon materials'},
    'materials': {name: {'srgb_hex': '#' + code} for name, code in PALETTE.items()},
    'assets': RECORDS,
    'flipbook': {'export': str(sheet_path.relative_to(REPO)).replace('\\', '/'), 'size': [sheet_w, sheet_h],
                 'cell_px': CELL, 'columns': COLS, 'rows_top_to_bottom': SHEET_ROWS,
                 'sha256': hashlib.sha256(sheet_path.read_bytes()).hexdigest(),
                 'drawing': 'White fills and dark ink outline drawn from wobbled signed-distance shapes, 3x supersampled; particles tint the fill.'},
    'renders': renders,
    'validation_limits': ['Workbench review renders show shape and colour only, not the in-game toon shading, outline or bloom.',
                          'In-game scale, fit inside containers and posing are verified by the AD-A route, not here.'],
}
(SOURCE / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print('DIORAMA_KIT_COMPLETE ' + json.dumps([{'id': r['id'], 'triangles': r['triangles'], 'size': r['bounds_unity_m']['size']} for r in RECORDS]))
