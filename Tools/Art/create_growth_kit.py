"""Original Evolution Shop growth kit (GR-16): the new game's look, approved by the user on 2026-10-25 (GR.48). Blender 5.0,
no external assets or add-ons; everything is generated from the numbers below.

Run: blender --background --factory-startup --python Tools/Art/create_growth_kit.py -- --repo C:/dev/DumbVideoGameStudio [--only A,B] [--no-render]

Wave 1 (GR.45's slice): the back room and shop unit A dressed in the look (floors, wall panelling, rails, beams, door and window
frames, lamps, a sign, a shelf, a rug, plants, and the street outside unit A's window and door), and the stations there with
the parts each level adds: the grower (and its tap and lid), the incubator (its screen, tint port and tray, auto-doser port),
the Starter tank (its cabinet and frame, pebbles, plants, light bar, filter and lid) and the counter (its bag dispenser, card
reader and second till). The look is the one in Tools/Art/gr16_look_ranch.py's sketches: juicy soft colours, glossy plastic,
cartoon glass, glowing cultures and screens, stations built from their parts.

Coordinates: the helpers take Unity coordinates (X right, Y up, Z forward) and reflect them into Blender as (-X, -Z, Y), the
mapping create_diorama_kit.py probed to import exactly (bake_space_transform on). Each asset's parts are joined into one mesh
directly under the asset's root, one material slot per GK_* name; the game maps those names onto its own materials
(Runtime/Growth/GrowthLook.cs). Each asset's origin is its station's root (the greybox's), or the room's origin for dressing.
"""
import argparse
import hashlib
import json
import math
import random
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser()
parser.add_argument('--repo', required=True)
parser.add_argument('--only', default='', help='Comma-separated asset names to build and export; the rest are left as they are.')
parser.add_argument('--no-render', action='store_true', help='Skip the review render.')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
REPO = Path(args.repo).resolve()
assert (REPO / 'EvolutionShop/ProjectSettings/ProjectVersion.txt').is_file()
SOURCE = REPO / 'ArtSource/GrowthKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/GrowthKit'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
ONLY = set(a for a in args.only.split(',') if a)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0
bpy.context.preferences.filepaths.save_version = 0

# ================================================================ the palette (the game owns the real materials; these name the slots)
PALETTE = {
    'GK_Cream': '#FFF3DF', 'GK_Plaster': '#FFE8C6', 'GK_PlasterLab': '#DDF5EC', 'GK_Teal': '#22BFB0', 'GK_TealDark': '#15867F',
    'GK_TealLight': '#7FE3D6', 'GK_Panel': '#8FDDD0', 'GK_Yellow': '#FFC93C', 'GK_Coral': '#FF7A7A', 'GK_Pink': '#FF9EC4',
    'GK_Lime': '#9BDB4D', 'GK_Green': '#56B947', 'GK_GreenDark': '#3E9A45', 'GK_Lilac': '#A98BFF', 'GK_Ink': '#2B3A55',
    'GK_Wood': '#E39A5A', 'GK_WoodLight': '#EFB277', 'GK_WoodMid': '#DB8F4F', 'GK_WoodDark': '#B9713D', 'GK_Beam': '#A8612F',
    'GK_Sand': '#F7DFAE', 'GK_Tile': '#FFF1DC', 'GK_Tile2': '#FFE4C4', 'GK_TileAccent': '#4FCDBE', 'GK_Gold': '#FFC93C',
    'GK_Pebble1': '#FFC1B0', 'GK_Pebble2': '#C9B6FF', 'GK_Pebble3': '#FFE08A', 'GK_Pebble4': '#BDEFE8', 'GK_Rock': '#B9A7E8',
    'GK_Soil': '#7A4E35', 'GK_Paper': '#FFFBEF', 'GK_Pavement': '#D9D2C6', 'GK_Grass': '#7CCF4E', 'GK_Sky': '#9ADBFF',
    # Glass and light: the game gives these its cartoon glass and glowing materials.
    'GK_Glass': '#EAFBFF', 'GK_GlowWarm': '#FFF4D6', 'GK_GlowLime': '#9BDB4D', 'GK_GlowTeal': '#7FE3D6', 'GK_GlowWhite': '#F5FCFF',
    'GK_GlowCoral': '#FF5E7E', 'GK_GlowBlue': '#3FA9FF', 'GK_GlowYellow': '#FFC93C', 'GK_Screen': '#0F3B4C', 'GK_GaugeFace': '#FFF8E6',
    # Takes the culture's colour at run time (a grower's label and gauge arc).
    'GK_Culture': '#FF5E7E',
    'GK_Cloud': '#FFFFFF',
}
MAT = {}


def lin(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


def material(name):
    if name not in MAT:
        m = bpy.data.materials.new(name)
        m.diffuse_color = lin(PALETTE[name])
        m.use_nodes = True
        m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = lin(PALETTE[name])
        MAT[name] = m
    return MAT[name]


# ================================================================ Unity space -> Blender space
P = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # Unity point -> Blender point, a reflection


def u(p):
    return Vector((-p[0], -p[2], p[1]))


def rot_unity(euler):
    """Unity's rotation for Euler degrees (applied Z, then X, then Y), as a 4x4 in Unity space."""
    x, y, z = (math.radians(a) for a in euler)
    return Matrix.Rotation(y, 4, 'Y') @ Matrix.Rotation(x, 4, 'X') @ Matrix.Rotation(z, 4, 'Z')


def place(obj, center, size, euler=(0, 0, 0)):
    """Puts a unit primitive built about Blender's origin (its axis along Blender Z, which is Unity Y) where a Unity transform
    with this position, rotation and scale would: the matrix is conjugated by the reflection, so the mesh stays proper."""
    A = Matrix.Translation(Vector(center)) @ rot_unity(euler) @ Matrix.Diagonal(Vector((size[0], size[1], size[2], 1)))
    obj.matrix_world = P @ A @ P.inverted()
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


PARTS = []


def finish(obj, mat, bevel=0.0, segments=4, smooth=True, weighted=True):
    obj.data.materials.clear(); obj.data.materials.append(material(mat))
    bpy.context.view_layer.objects.active = obj
    if bevel > 0:
        mod = obj.modifiers.new('Bevel', 'BEVEL'); mod.width = bevel; mod.segments = segments
        mod.limit_method = 'ANGLE'; mod.angle_limit = math.radians(35); mod.use_clamp_overlap = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if smooth:
        for p in obj.data.polygons: p.use_smooth = True
    if weighted:
        wn = obj.modifiers.new('Normals', 'WEIGHTED_NORMAL'); wn.keep_sharp = True; wn.weight = 50; wn.mode = 'FACE_AREA'
        bpy.ops.object.modifier_apply(modifier=wn.name)
    PARTS.append(obj)
    return obj


def box(center, size, mat, r=0.03, rot=(0, 0, 0), seg=4):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.object
    place(o, center, size, rot)
    return finish(o, mat, min(r, 0.45 * min(size)) if r > 0 else 0, seg)


def cyl(center, radius, height, mat, r=0.02, rot=(0, 0, 0), verts=32, rz=None):
    """A cylinder along Unity Y (rot turns it); rz gives an oval (radius along Z)."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=1, depth=1)
    o = bpy.context.object
    place(o, center, (radius, height, radius if rz is None else rz), rot)
    return finish(o, mat, min(r, 0.45 * radius, 0.45 * height) if r > 0 else 0, 3)


def ball(center, radii, mat, seg=24, rot=(0, 0, 0)):
    radii = radii if isinstance(radii, (tuple, list)) else (radii,) * 3
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=max(8, seg // 2), radius=1)
    o = bpy.context.object
    place(o, center, radii, rot)
    return finish(o, mat, weighted=False)


def ring(center, major, minor, mat, rot=(0, 0, 0), sz=None):
    """A torus lying in Unity's XZ plane (rot turns it); sz stretches it along Z for an oval ring."""
    bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=minor / major, major_segments=48, minor_segments=12)
    o = bpy.context.object
    place(o, center, (major, major, major if sz is None else sz), rot)
    return finish(o, mat, weighted=False)


def cone(center, r1, r2, height, mat, rot=(0, 0, 0), verts=32):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=1)
    o = bpy.context.object
    place(o, center, (1, height, 1), rot)
    return finish(o, mat, 0.01, 2)


def tube(points, radius, mat, sides=12):
    """A smooth pipe through Unity points."""
    cu = bpy.data.curves.new('tube', 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = max(2, sides // 4); cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(points) - 1)
    for p, pt in zip(sp.points, points): p.co = (*u(pt), 1)
    o = bpy.data.objects.new('tube', cu); bpy.context.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.object
    return finish(o, mat, weighted=False)


def text(body, center, size, mat, euler=(0, 0, 0), extrude=0.02):
    """Letters reading left to right along Unity +X, facing Unity -Z (rot turns them), extruded toward +Z."""
    bpy.ops.object.text_add()
    o = bpy.context.object
    o.data.body = body; o.data.size = 1.0; o.data.extrude = extrude / size; o.data.align_x = 'CENTER'; o.data.align_y = 'CENTER'
    o.data.bevel_depth = 0.25 * extrude / size; o.data.bevel_resolution = 2
    font = Path('C:/Windows/Fonts/seguibl.ttf')
    if font.exists(): o.data.font = bpy.data.fonts.load(str(font))
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.object
    # Text local (x right, y up, z out of the page) -> Unity (x, y, -z) facing -Z, then the transform, then into Blender.
    A = Matrix.Translation(Vector(center)) @ rot_unity(euler) @ Matrix.Diagonal(Vector((size, size, -size, 1)))
    for v in o.data.vertices:
        v.co = u(A @ v.co)
    o.data.update()
    return finish(o, mat, weighted=False)


def join(name):
    """Joins the parts made since the last join into one mesh under an empty root named for the asset."""
    global PARTS
    root = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(root)
    parts = [p for p in PARTS if p.name in bpy.data.objects]
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = name + ' mesh'
    mesh.parent = root
    PARTS = []
    return root


# ================================================================ the stations (origin: the greybox station's root; front is local +Z
# except the incubator, whose front is -Z; units in metres, as GrowthWorld.cs builds them)
RNG = random.Random(16)


def grower():
    """GrowthWorld.BuildSource: a cabinet 0.8 x 1.0 x 0.7 and a jar 0.42 wide from 1.0 to 1.6, its culture label on the
    front (+Z) and its taps on the back (-Z)."""
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl((sx * 0.3, 0.035, sz * 0.25), 0.055, 0.07, 'GK_TealDark', r=0.02)
    box((0, 0.52, 0), (0.8, 0.96, 0.7), 'GK_Cream', r=0.1)
    for y in (0.16, 0.86):
        box((0, y, 0), (0.82, 0.05, 0.72), 'GK_Teal', r=0.025)
    # The culture's label and the gauge on the front panel.
    box((0, 0.36, 0.352), (0.42, 0.14, 0.02), 'GK_Culture', r=0.03)
    cyl((0, 0.64, 0.355), 0.09, 0.03, 'GK_Teal', r=0.012, rot=(90, 0, 0))
    cyl((0, 0.64, 0.371), 0.07, 0.006, 'GK_GaugeFace', r=0, rot=(90, 0, 0))
    cyl((0, 0.64, 0.375), 0.055, 0.004, 'GK_Culture', r=0, rot=(90, 0, 0))
    cyl((0, 0.64, 0.378), 0.035, 0.005, 'GK_GaugeFace', r=0, rot=(90, 0, 0))
    box((0.012, 0.66, 0.382), (0.012, 0.055, 0.004), 'GK_Ink', r=0.002, rot=(0, 0, -35))
    for sx in (-1, 1):
        box((sx * 0.405, 0.52, 0.12), (0.02, 0.5, 0.16), 'GK_TealLight', r=0.008)
    # The jar: its collar on the cabinet, the glass, and a rim at the top.
    cyl((0, 1.02, 0), 0.25, 0.08, 'GK_Teal', r=0.03)
    cyl((0, 1.31, 0), 0.215, 0.58, 'GK_Glass', r=0, verts=40)
    ring((0, 1.6, 0), 0.215, 0.022, 'GK_Teal')
    return join('Grower')


def grower_ring():
    """Level 2: a second shelf ring around the jar, with little clips."""
    ring((0, 1.12, 0), 0.235, 0.018, 'GK_Teal')
    for k in range(3):
        a = math.radians(30 + k * 120)
        box((0.24 * math.cos(a), 1.12, 0.24 * math.sin(a)), (0.05, 0.05, 0.05), 'GK_Yellow', r=0.015)
    return join('GrowerRing')


def grower_tap():
    """A tap on the back (-Z), where the culture's pipe rises (GR-07): a short spout from the cabinet, a riser, a coral valve
    wheel. Placed at the greybox Tap's x (0, or 0.16 for the second)."""
    cyl((0, 1.18, -0.325), 0.036, 0.25, 'GK_Teal', r=0.012, rot=(90, 0, 0))
    ring((0, 1.18, -0.21), 0.045, 0.012, 'GK_TealDark', rot=(90, 0, 0))
    cyl((0, 1.26, -0.42), 0.034, 0.16, 'GK_TealDark', r=0.01)
    ring((0.0, 1.18, -0.445), 0.04, 0.012, 'GK_Coral', rot=(90, 0, 0))
    box((0, 1.18, -0.445), (0.07, 0.012, 0.012), 'GK_Coral', r=0.004)
    return join('GrowerTap')


def grower_lid():
    """Level 4: a domed lid on the jar with a little light on a stalk."""
    cyl((0, 1.62, 0), 0.235, 0.05, 'GK_Teal', r=0.02)
    ball((0, 1.645, 0), (0.21, 0.11, 0.21), 'GK_Cream')
    cyl((0, 1.78, 0), 0.016, 0.05, 'GK_TealDark', r=0.006)
    ball((0, 1.82, 0), 0.04, 'GK_GlowYellow', seg=16)
    return join('GrowerLid')


def incubator():
    """GrowthWorld.Stations' incubator: a base 0.9 x 0.9 x 0.7 with its front to -Z, the greybox's dome (0.6 x 0.45 x 0.55 at
    1.05) and tray (-0.22) kept; this is a yellow base with a panel of lights, a glowing ring and a nest, and the dome's ring."""
    for sx in (-1, 1):
        for sz in (-1, 1):
            ball((sx * 0.34, 0.04, sz * 0.25), (0.07, 0.045, 0.07), 'GK_TealDark', seg=16)
    box((0, 0.46, 0), (0.9, 0.84, 0.7), 'GK_Yellow', r=0.14)
    box((0, 0.42, -0.352), (0.46, 0.24, 0.03), 'GK_Cream', r=0.05)
    for k, m in enumerate(('GK_GlowCoral', 'GK_GlowYellow', 'GK_GlowBlue')):
        cyl((-0.12 + k * 0.12, 0.42, -0.37), 0.035, 0.016, m, r=0.005, rot=(90, 0, 0))
    box((0, 0.875, 0), (0.86, 0.05, 0.66), 'GK_Cream', r=0.025)
    ring((0, 0.905, 0), 0.39, 0.018, 'GK_GlowTeal', sz=0.29)
    ring((0, 0.85, 0), 0.31, 0.03, 'GK_Teal', sz=0.285)
    ball((0, 0.915, -0.22), (0.16, 0.035, 0.1), 'GK_Pink', seg=20)
    ball((0, 1.29, 0), 0.045, 'GK_Teal', seg=16)
    ring((0, 1.27, 0), 0.06, 0.015, 'GK_Teal')
    return join('Incubator')


def incubator_screen():
    """Level 2: the mix log screen on the front, right of the panel (the greybox's at 0.3, 0.66, -0.36)."""
    box((0.3, 0.66, -0.36), (0.26, 0.19, 0.04), 'GK_Cream', r=0.035)
    box((0.3, 0.665, -0.382), (0.21, 0.14, 0.008), 'GK_Screen', r=0.012)
    for k, (m, h) in enumerate((('GK_GlowCoral', 0.08), ('GK_GlowBlue', 0.05), ('GK_GlowYellow', 0.1))):
        box((0.245 + k * 0.055, 0.615 + h / 2, -0.388), (0.032, h, 0.006), m, r=0.004)
    return join('IncubatorScreen')


def incubator_port():
    """Level 3: the tint port on top (a yellow funnel) and a wider clutch tray."""
    cyl((0.22, 1.27, 0.1), 0.03, 0.08, 'GK_TealDark', r=0.008)
    cone((0.22, 1.35, 0.1), 0.035, 0.085, 0.1, 'GK_Yellow')
    ball((0, 0.905, -0.22), (0.24, 0.03, 0.13), 'GK_Pink', seg=24)
    return join('IncubatorPort')


def incubator_doser():
    """Level 4: the auto-doser's port on the back (+Z), where the buffers' pipes drop in (1.17 m)."""
    box((0, 0.95, 0.38), (0.32, 0.3, 0.1), 'GK_Teal', r=0.05)
    box((0, 0.95, 0.432), (0.2, 0.16, 0.01), 'GK_Screen', r=0.012)
    cyl((0, 1.17, 0.42), 0.055, 0.08, 'GK_TealDark', r=0.015)
    ring((0, 1.21, 0.42), 0.055, 0.012, 'GK_GlowTeal')
    return join('IncubatorDoser')


TANK_W, TANK_D, TANK_STAND, TANK_GLASS = 1.5, 1.0, 0.9, 0.62


def tank_starter():
    """GrowthWorld.BuildTank for a Starter tank (1.5 x 1.0; stand 0.9, glass 0.62, front +Z): a cream cabinet with turquoise
    doors and a kick, the frame's rails and posts round the glass, and a light bar on top."""
    W, D, S, G = TANK_W, TANK_D, TANK_STAND, TANK_GLASS
    top = S + G
    box((0, 0.05, 0), (W - 0.04, 0.1, D - 0.04), 'GK_TealDark', r=0.03)
    box((0, S / 2 + 0.03, 0), (W, S - 0.06, D), 'GK_Cream', r=0.08)
    for s in (-1, 1):
        box((s * W * 0.24, S * 0.48, D / 2 + 0.01), (W * 0.44, S * 0.62, 0.03), 'GK_Teal', r=0.025)
        ball((s * 0.09, S * 0.52, D / 2 + 0.035), 0.03, 'GK_Yellow', seg=16)
    box((0, S - 0.005, 0), (W + 0.06, 0.06, D + 0.06), 'GK_Teal', r=0.025)
    box((0, top + 0.02, 0), (W + 0.06, 0.05, D + 0.06), 'GK_Teal', r=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl((sx * (W / 2 + 0.005), S + G / 2, sz * (D / 2 + 0.005)), 0.03, G, 'GK_Teal', r=0.01, verts=16)
    box((0, top + 0.075, -D * 0.15), (W * 0.8, 0.05, 0.14), 'GK_Cream', r=0.02)
    box((0, top + 0.05, -D * 0.15), (W * 0.74, 0.006, 0.09), 'GK_GlowWhite', r=0)
    return join('TankStarter')


def tank_bed():
    """The Starter tank's pebbles on its gravel, its plants and a rock (the greybox gravel's top is at stand + 0.055)."""
    W, D, S = TANK_W, TANK_D, TANK_STAND
    y = S + 0.06
    pebbles = ['GK_Sand', 'GK_Pebble1', 'GK_Pebble2', 'GK_Pebble3', 'GK_Pebble4']
    for k in range(90):
        ball((RNG.uniform(-W / 2 + 0.07, W / 2 - 0.07), y, RNG.uniform(-D / 2 + 0.07, D / 2 - 0.07)), (0.03, 0.017, 0.026),
             pebbles[k % len(pebbles)], seg=10, rot=(0, RNG.uniform(0, 180), 0))
    for k, (px, pz, h, m) in enumerate(((0.52, -0.3, 0.42, 'GK_Green'), (0.6, -0.18, 0.3, 'GK_Lime'), (0.44, -0.36, 0.26, 'GK_GreenDark'),
                                         (-0.58, -0.3, 0.34, 'GK_Lime'), (-0.5, -0.38, 0.22, 'GK_Green'))):
        ball((px, y + h / 2, pz), (0.05, h / 2, 0.035), m, seg=16, rot=(0, 0, (k - 2) * 6))
    ball((-0.3, y + 0.04, -0.25), (0.16, 0.08, 0.12), 'GK_Rock', seg=20)
    return join('TankBed')


def tank_filter():
    """Level 2: a filter box on the back (-Z), at the greybox's (W/2 - 0.3, top - 0.12, -D/2 + 0.07)."""
    W, D, top = TANK_W, TANK_D, TANK_STAND + TANK_GLASS
    x, z = W / 2 - 0.3, -D / 2 + 0.07
    box((x, top - 0.12, z), (0.26, 0.24, 0.12), 'GK_Teal', r=0.04)
    for k in range(3):
        box((x - 0.06 + k * 0.06, top - 0.12, z + 0.062), (0.03, 0.14, 0.006), 'GK_TealDark', r=0.006)
    cyl((x, top + 0.02, z), 0.025, 0.1, 'GK_TealDark', r=0.008)
    return join('TankFilter')


def tank_lid():
    """Level 3: a cream lid with a handle, over the rim (the greybox's at top + 0.05)."""
    W, D, top = TANK_W, TANK_D, TANK_STAND + TANK_GLASS
    box((0, top + 0.06, 0), (W + 0.04, 0.04, D + 0.04), 'GK_Cream', r=0.018)
    box((0, top + 0.1, 0.15), (0.3, 0.04, 0.06), 'GK_Yellow', r=0.02)
    return join('TankLid')


def counter():
    """GrowthWorld.Stations' counter (2.1 x 0.7, top at 1.045): a wooden body planked on both long sides, a kick, a turquoise
    trim, a cream top, the bell (0.6, 1.08, 0.1), a jar of treats and a little plant."""
    box((0, 0.05, 0), (1.96, 0.1, 0.56), 'GK_TealDark', r=0.03)
    box((0, 0.52, 0), (2.0, 0.86, 0.6), 'GK_WoodDark', r=0.04)
    woods = ['GK_Wood', 'GK_WoodLight', 'GK_WoodMid']
    for side in (-1, 1):
        for k in range(10):
            box((-0.9 + k * 0.2, 0.52, side * 0.31), (0.19, 0.8, 0.03), woods[k % 3], r=0.012, seg=2)
    box((0, 0.96, 0), (2.06, 0.04, 0.66), 'GK_Teal', r=0.018)
    box((0, 1.0, 0), (2.12, 0.07, 0.72), 'GK_Cream', r=0.03)
    cyl((0.6, 1.047, 0.1), 0.065, 0.02, 'GK_Ink', r=0.006)
    ball((0.6, 1.07, 0.1), (0.06, 0.05, 0.06), 'GK_Gold', seg=20)
    ball((0.6, 1.125, 0.1), 0.013, 'GK_Gold', seg=10)
    cyl((0.85, 1.155, -0.15), 0.08, 0.22, 'GK_Glass', r=0)
    cyl((0.85, 1.28, -0.15), 0.085, 0.03, 'GK_Coral', r=0.01)
    for k in range(12):
        ball((0.85 + RNG.uniform(-0.045, 0.045), 1.065 + k * 0.012, -0.15 + RNG.uniform(-0.045, 0.045)), 0.024,
             ('GK_Yellow', 'GK_Pink', 'GK_Lime', 'GK_Lilac')[k % 4], seg=10)
    cyl((-0.85, 1.09, -0.18), 0.055, 0.11, 'GK_Coral', r=0.018)
    for k in range(3):
        ball((-0.85 + (k - 1) * 0.028, 1.17 + (k % 2) * 0.02, -0.18 + (k % 2) * 0.02), (0.032, 0.045, 0.032), 'GK_Lime', seg=14)
    return join('Counter')


def counter_bags():
    """Level 2: a bag dispenser (the greybox's at -0.7, 1.16, 0.1), a roll of paper bags in a cream holder."""
    box((-0.7, 1.08, 0.1), (0.28, 0.06, 0.22), 'GK_Teal', r=0.02)
    for s in (-1, 1):
        box((-0.7 + s * 0.12, 1.17, 0.1), (0.03, 0.18, 0.18), 'GK_Cream', r=0.012)
    cyl((-0.7, 1.19, 0.1), 0.07, 0.2, 'GK_Coral', r=0.012, rot=(0, 0, 90))
    box((-0.7, 1.13, 0.17), (0.18, 0.1, 0.004), 'GK_Paper', r=0.002, rot=(-20, 0, 0))
    return join('CounterBags')


def counter_reader():
    """Level 3: a card reader tilted toward the customer, with its green light (the greybox's at 0.15, 1.1, 0.12)."""
    box((0.15, 1.1, 0.12), (0.12, 0.05, 0.18), 'GK_Teal', r=0.02, rot=(-20, 0, 0))
    box((0.15, 1.13, 0.1), (0.08, 0.008, 0.06), 'GK_Screen', r=0.004, rot=(-20, 0, 0))
    ball((0.15, 1.13, 0.17), 0.012, 'GK_GlowLime', seg=10)
    return join('CounterReader')


def counter_till():
    """Level 4: a second till with its screen (the greybox's at -0.25, 1.14, -0.05)."""
    box((-0.25, 1.11, -0.05), (0.36, 0.14, 0.32), 'GK_Teal', r=0.05)
    box((-0.25, 1.1, 0.115), (0.28, 0.06, 0.02), 'GK_Cream', r=0.01)
    box((-0.25, 1.28, -0.12), (0.26, 0.17, 0.05), 'GK_Cream', r=0.03, rot=(-15, 0, 0))
    box((-0.25, 1.285, -0.1), (0.21, 0.12, 0.008), 'GK_Screen', r=0.01, rot=(-15, 0, 0))
    box((-0.25, 1.29, -0.097), (0.13, 0.03, 0.004), 'GK_GlowLime', r=0.003, rot=(-15, 0, 0))
    for i in range(2):
        for j in range(3):
            cyl((-0.33 + j * 0.08, 1.19, -0.02 + i * 0.07), 0.022, 0.02, ('GK_Coral', 'GK_Yellow', 'GK_Paper')[(i + j) % 3], r=0.006)
    return join('CounterTill')


# ================================================================ the rooms (origin: the world's origin; GrowthWorld.Rooms' walls are 0.2
# thick on their lines, so their inside faces are 0.1 in)
def planks_x(x0, x1, z0, z1, y, width, mats, thick=0.03):
    """Floor planks running along Z, side by side across X, their joints staggered."""
    n = int(round((x1 - x0) / width))
    w = (x1 - x0) / n
    for i in range(n):
        x = x0 + (i + 0.5) * w
        z = z0 - RNG.uniform(0, 1.2)
        while z < z1:
            L = RNG.choice((1.4, 1.8, 2.2, 2.6))
            a, b = max(z, z0), min(z + L, z1)
            if b - a > 0.05:
                box((x, y - thick / 2, (a + b) / 2), (w - 0.012, thick, b - a - 0.012), mats[(i * 7 + int(z * 3)) % len(mats)], r=0.008, seg=2)
            z += L


def wall_boards(a, b, inner, height, gaps, mats, width=0.166, depth=0.035):
    """Upright boards along a wall's inside face from a to b (floor-plane points), leaving out its gaps (distances along it)."""
    a, b = Vector((a[0], a[1])), Vector((b[0], b[1]))
    d = (b - a).normalized(); length = (b - a).length
    n = Vector(inner)
    k = 0
    count = int(length // width)
    t = (length - count * width) / 2
    while t + width <= length + 1e-6:
        c = t + width * 0.5
        if not any(g0 - 0.02 < c < g1 + 0.02 for g0, g1 in gaps):
            p = a + d * c + n * (0.1 + depth / 2)
            size = (width - 0.008, height, depth) if abs(d.x) > 0.5 else (depth, height, width - 0.008)
            box((p.x, height / 2, p.y), size, mats[k % len(mats)], r=0.01, seg=2)
        t += width; k += 1


def wall_strip(a, b, inner, y, h, depth, mat, gaps=(), r=0.02):
    """A rail or skirting along a wall's inside face, broken at its gaps."""
    a, b = Vector((a[0], a[1])), Vector((b[0], b[1]))
    d = (b - a).normalized(); length = (b - a).length
    spans = [(0.0, length)]
    for g0, g1 in gaps:
        nxt = []
        for s0, s1 in spans:
            if g1 <= s0 or g0 >= s1: nxt.append((s0, s1)); continue
            if g0 > s0: nxt.append((s0, g0))
            if g1 < s1: nxt.append((g1, s1))
        spans = nxt
    n = Vector(inner)
    for s0, s1 in spans:
        if s1 - s0 < 0.05: continue
        p = a + d * ((s0 + s1) / 2) + n * (0.1 + depth / 2)
        size = (s1 - s0, h, depth) if abs(d.x) > 0.5 else (depth, h, s1 - s0)
        box((p.x, y, p.y), size, mat, r=r)


def door_frame(cx, cz, along_x, width, height, mat, inner):
    """A turquoise frame round a doorway's inside face: two posts and a head."""
    n = Vector(inner)
    off = 0.1 + 0.04
    for s in (-1, 1):
        if along_x: box((cx + s * (width / 2 + 0.05), height / 2, cz + n.y * off), (0.12, height, 0.08), mat, r=0.04)
        else: box((cx + n.x * off, height / 2, cz + s * (width / 2 + 0.05)), (0.08, height, 0.12), mat, r=0.04)
    if along_x: box((cx, height + 0.06, cz + n.y * off), (width + 0.22, 0.12, 0.08), mat, r=0.04)
    else: box((cx + n.x * off, height + 0.06, cz), (0.08, 0.12, width + 0.22), mat, r=0.04)


def pendant(x, y, z, ceiling):
    cyl((x, (ceiling + y) / 2 + 0.1, z), 0.008, ceiling - y - 0.2, 'GK_Ink', r=0)
    cyl((x, ceiling - 0.03, z), 0.07, 0.05, 'GK_Teal', r=0.02)
    cyl((x, y + 0.22, z), 0.045, 0.06, 'GK_Teal', r=0.02)
    ball((x, y + 0.08, z), (0.3, 0.17, 0.3), 'GK_Yellow')
    ball((x, y - 0.03, z), 0.08, 'GK_GlowWarm', seg=16)


def potted_plant(x, z, s=1.0):
    cyl((x, 0.22 * s, z), 0.22 * s, 0.44 * s, 'GK_Coral', r=0.06 * s)
    ring((x, 0.44 * s, z), 0.215 * s, 0.035 * s, 'GK_Pink')
    cyl((x, 0.43 * s, z), 0.19 * s, 0.02, 'GK_Soil', r=0)
    for dx, dz, dy, r, m in ((0, 0, 0.78, 0.3, 'GK_Green'), (-0.14, 0.05, 0.66, 0.22, 'GK_Lime'), (0.15, -0.04, 0.68, 0.23, 'GK_GreenDark'),
                             (0.04, 0.12, 0.92, 0.2, 'GK_Lime'), (0.02, -0.12, 0.86, 0.19, 'GK_Green')):
        ball((x + dx * s, dy * s, z + dz * s), (r * s, r * 0.9 * s, r * s), m, seg=20)


def unit_a_floor():
    """Unit A's floor (8 x 8 at the origin, its top at y 0): warm planks in three tones."""
    planks_x(-4, 4, -4, 4, 0.006, 0.296, ['GK_Wood', 'GK_WoodLight', 'GK_WoodMid'])
    return join('UnitAFloor')


def unit_a_dressing():
    """Unit A's walls and ceiling: turquoise boards to 0.9 with a rail and skirting (around the window, the entrance, the back
    room door and the arch), chunky beams, frames for the doorways and the window, two pendant lamps, the sign, a shelf of jars,
    a poster, a clock, plants in two corners and a round rug."""
    boards = ['GK_Panel', 'GK_TealLight']
    # Front wall (z -4): window x -3.6..-1.6 (boards run under it), entrance x -1.2..0.2.
    wall_boards((-4, -4), (4, -4), (0, 1), 0.9, [(2.8, 4.2)], boards)
    wall_boards((-4, -4), (-4, 4), (1, 0), 0.9, [], boards)
    wall_boards((-4, 4), (4, 4), (0, -1), 0.9, [(4.2, 5.4)], boards)
    wall_boards((4, -4), (4, 4), (-1, 0), 0.9, [(2.4, 5.6)], boards)
    for a, b, n, gaps in (((-4, -4), (4, -4), (0, 1), [(2.8, 4.2)]), ((-4, -4), (-4, 4), (1, 0), []),
                          ((-4, 4), (4, 4), (0, -1), [(4.2, 5.4)]), ((4, -4), (4, 4), (-1, 0), [(2.4, 5.6)])):
        wall_strip(a, b, n, 0.93, 0.07, 0.07, 'GK_Teal', gaps)
        wall_strip(a, b, n, 0.05, 0.1, 0.06, 'GK_TealDark', gaps, r=0.015)
    for z in (-3.0, -1.0, 1.0, 3.0):
        box((0, 3.1, z), (7.8, 0.2, 0.22), 'GK_Beam', r=0.05)
    door_frame(-0.5, -4, True, 1.4, 2.4, 'GK_Teal', (0, 1))
    door_frame(0.8, 4, True, 1.2, 2.4, 'GK_Teal', (0, -1))
    door_frame(4, 0, False, 3.2, 2.4, 'GK_Teal', (-1, 0))
    # The window (x -3.6..-1.6, glass 0.9..2.4): a chunky frame, a mullion and a transom, a sill with two little pots.
    wx, wz = -2.6, -4 + 0.1
    box((wx, 2.46, wz + 0.04), (2.24, 0.14, 0.12), 'GK_Teal', r=0.05)
    box((wx, 0.86, wz + 0.1), (2.3, 0.08, 0.24), 'GK_Teal', r=0.035)
    for s in (-1, 1):
        box((wx + s * 1.06, 1.65, wz + 0.04), (0.12, 1.6, 0.12), 'GK_Teal', r=0.05)
    box((wx, 1.65, wz + 0.02), (0.06, 1.5, 0.05), 'GK_TealDark', r=0.02)
    box((wx, 1.85, wz + 0.02), (2.0, 0.05, 0.05), 'GK_TealDark', r=0.02)
    for k, m in enumerate(('GK_Yellow', 'GK_Pink')):
        cyl((wx - 0.6 + k * 1.2, 0.96, wz + 0.12), 0.07, 0.12, m, r=0.02)
        ball((wx - 0.6 + k * 1.2, 1.09, wz + 0.12), (0.09, 0.08, 0.09), 'GK_Lime', seg=18)
    # Lamps (the game lights them), the sign on the north wall over place a1, the shelf on the east wall, poster, clock.
    pendant(-1.4, 2.5, 1.2, 3.2)
    pendant(1.7, 2.5, -1.0, 3.2)
    sz = 4 - 0.1
    box((-2.0, 2.62, sz - 0.03), (2.5, 0.7, 0.06), 'GK_Teal', r=0.06)
    box((-2.0, 2.62, sz - 0.06), (2.34, 0.56, 0.06), 'GK_Beam', r=0.05)
    text('Evolution Shop', (-2.0, 2.6, sz - 0.1), 0.3, 'GK_Cream', extrude=0.025)
    for s in (-1, 1):
        ball((-2.0 + s * 1.1, 2.62, sz - 0.1), (0.05, 0.05, 0.03), 'GK_Gold', seg=14)
    ex = 4 - 0.1
    box((ex - 0.15, 1.62, 2.7), (0.3, 0.05, 1.6), 'GK_WoodDark', r=0.02)
    for s in (-1, 1):
        box((ex - 0.06, 1.54, 2.7 + s * 0.65), (0.12, 0.14, 0.05), 'GK_Teal', r=0.02)
    for k, m in enumerate(('GK_GlowCoral', 'GK_GlowBlue', 'GK_GlowYellow', 'GK_Lilac', 'GK_GlowLime')):
        jz = 2.1 + k * 0.3
        cyl((ex - 0.15, 1.75, jz), 0.08, 0.2, 'GK_Glass', r=0)
        cyl((ex - 0.15, 1.72, jz), 0.066, 0.12, m, r=0.02)
        cyl((ex - 0.15, 1.865, jz), 0.084, 0.035, ('GK_Teal', 'GK_Coral', 'GK_Teal', 'GK_Yellow', 'GK_Coral')[k], r=0.012)
    box((ex - 0.02, 2.2, 1.95), (0.04, 0.8, 0.62), 'GK_Coral', r=0.03)
    box((ex - 0.045, 2.2, 1.95), (0.02, 0.7, 0.52), 'GK_Paper', r=0.01)
    ball((ex - 0.06, 2.16, 1.95), (0.02, 0.14, 0.17), 'GK_Teal', seg=18)
    ball((ex - 0.075, 2.2, 1.89), (0.01, 0.04, 0.04), 'GK_Paper', seg=12)
    ball((ex - 0.08, 2.2, 1.88), (0.01, 0.018, 0.018), 'GK_Ink', seg=10)
    ball((ex - 0.06, 2.16, 2.14), (0.02, 0.09, 0.06), 'GK_TealDark', seg=12)
    cyl((-4 + 0.13, 2.35, -0.2), 0.24, 0.06, 'GK_Teal', r=0.025, rot=(0, 0, 90))
    cyl((-4 + 0.16, 2.35, -0.2), 0.19, 0.01, 'GK_Cream', r=0, rot=(0, 0, 90))
    box((-4 + 0.17, 2.4, -0.2), (0.01, 0.12, 0.02), 'GK_Ink', r=0.004)
    box((-4 + 0.17, 2.35, -0.16), (0.01, 0.02, 0.09), 'GK_Ink', r=0.004)
    potted_plant(3.5, 3.5, 1.0)
    potted_plant(-3.55, -3.55, 0.9)
    for r, y, m in ((1.0, 0.004, 'GK_TealLight'), (0.78, 0.008, 'GK_Cream'), (0.52, 0.012, 'GK_Yellow')):
        cyl((1.6, y, 0.6), r, 0.008, m, r=0)
    return join('UnitADressing')


def street():
    """Outside unit A (south of z -4): a pavement, a strip of grass with a fence, round trees, bushes and puffy clouds, so the
    window and the entrance look out on somewhere sunny."""
    box((0, -0.03, -5.4), (12, 0.06, 2.6), 'GK_Pavement', r=0.01)
    box((0, -0.04, -12), (40, 0.06, 11), 'GK_Grass', r=0)
    for k in range(9):
        box((-6 + k * 1.5, 0.45, -7.0), (0.12, 0.9, 0.12), 'GK_WoodLight', r=0.04)
    box((0, 0.62, -7.0), (12.2, 0.1, 0.06), 'GK_WoodLight', r=0.03)
    box((0, 0.32, -7.0), (12.2, 0.1, 0.06), 'GK_WoodLight', r=0.03)

    def tree(x, z, s):
        cyl((x, 0.9 * s, z), 0.16 * s, 1.8 * s, 'GK_Beam', r=0.05 * s)
        for dx, dz, dy, r, m in ((0, 0, 2.4, 1.15, 'GK_Green'), (-0.7, 0.3, 1.9, 0.8, 'GK_Lime'), (0.7, -0.2, 2.0, 0.85, 'GK_GreenDark'), (0.1, -0.5, 2.9, 0.7, 'GK_Lime')):
            ball((x + dx * s, dy * s, z + dz * s), r * s, m, seg=20)
    tree(-4.5, -9.5, 1.1); tree(1.5, -11, 1.3); tree(6.0, -9.0, 1.0); tree(-9, -13, 1.4)
    for x, z, r, m in ((-2.5, -7.8, 0.6, 'GK_Lime'), (3.5, -7.9, 0.55, 'GK_Green'), (-6.5, -8.2, 0.7, 'GK_Green')):
        ball((x, r * 0.5, z), (r, r * 0.8, r * 0.9), m, seg=20)
    for x, y, z, s in ((-6, 9, -30, 2.4), (5, 11, -34, 2.8), (14, 8, -28, 2.0)):
        for j in range(5):
            ball((x + (j - 2) * s * 0.7, y + RNG.uniform(-0.3, 0.5), z + RNG.uniform(-0.6, 0.6)), (s * 0.8, s * 0.55, s * 0.75), 'GK_Cloud', seg=16)
    return join('Street')


def back_room_floor():
    """The back room's floor (x -4..2, z 4..9): glossy cream tiles with turquoise ones in a pattern."""
    for i in range(12):
        for j in range(10):
            m = 'GK_TileAccent' if (i + j) % 4 == 0 else ('GK_Tile' if (i + j) % 2 else 'GK_Tile2')
            box((-4 + 0.25 + i * 0.5, 0.006 - 0.015, 4 + 0.25 + j * 0.5), (0.49, 0.03, 0.49), m, r=0.012, seg=2)
    return join('BackRoomFloor')


def back_room_dressing():
    """The back room's walls and ceiling: rounded turquoise panels to 1.0 with a rail (around the door to unit A and the
    annex doorway), frames for both doorways, and a light strip down the ceiling (the game lights it)."""
    def panels(a, b, inner, gaps, h=0.95):
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized(); length = (b - a).length; n = Vector(inner)
        t = (length - int(length // 0.9) * 0.9) / 2
        while t + 0.9 <= length + 1e-6:
            c = t + 0.45
            if not any(g0 - 0.45 < c < g1 + 0.45 for g0, g1 in gaps):
                p = a + d * c + n * (0.1 + 0.025)
                size = (0.84, h, 0.05) if abs(d.x) > 0.5 else (0.05, h, 0.84)
                box((p.x, 0.08 + h / 2, p.y), size, 'GK_Panel', r=0.05)
            t += 0.9
    door = (4.2, 5.4)  # along unit A's north wall from x -4: x 0.2..1.4
    annex = (0.45, 1.85)  # along the back room's north wall from x -4
    panels((-4, 4), (2, 4), (0, 1), [(door[0], door[1])])
    panels((-4, 4), (-4, 9), (1, 0), [])
    panels((-4, 9), (2, 9), (0, -1), [annex])
    panels((2, 4), (2, 9), (-1, 0), [])
    for a, b, n, gaps in (((-4, 4), (2, 4), (0, 1), [door]), ((-4, 4), (-4, 9), (1, 0), []), ((-4, 9), (2, 9), (0, -1), [annex]), ((2, 4), (2, 9), (-1, 0), [])):
        wall_strip(a, b, n, 1.07, 0.07, 0.07, 'GK_Teal', gaps)
        wall_strip(a, b, n, 0.04, 0.08, 0.06, 'GK_TealDark', gaps, r=0.015)
    # The wall the back room shares with unit A wears the back room's mint above its panels, around the door and over it.
    wall_strip((-4, 4), (2, 4), (0, 1), 1.775, 1.35, 0.02, 'GK_PlasterLab', [door], r=0)
    wall_strip((-4, 4), (2, 4), (0, 1), 3.025, 1.15, 0.02, 'GK_PlasterLab', [], r=0)
    door_frame(0.8, 4, True, 1.2, 2.4, 'GK_Teal', (0, 1))
    door_frame(-4 + (annex[0] + annex[1]) / 2, 9, True, annex[1] - annex[0], 2.8, 'GK_Teal', (0, -1))
    box((-1, 3.56, 6.5), (0.5, 0.08, 4.2), 'GK_Cream', r=0.03)
    box((-1, 3.515, 6.5), (0.36, 0.01, 4.0), 'GK_GlowWhite', r=0)
    potted_plant(1.6, 8.55, 0.85)
    return join('BackRoomDressing')


BUILDS = [grower, grower_ring, grower_tap, grower_lid, incubator, incubator_screen, incubator_port, incubator_doser,
          tank_starter, tank_bed, tank_filter, tank_lid, counter, counter_bags, counter_reader, counter_till,
          unit_a_floor, unit_a_dressing, street, back_room_floor, back_room_dressing]


def stats(root):
    mesh = root.children[0]
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    corners = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
    lo = [min(c[i] for c in corners) for i in range(3)]; hi = [max(c[i] for c in corners) for i in range(3)]
    # Blender -> Unity: (-x, z, -y).
    ulo = (-hi[0], lo[2], -hi[1]); uhi = (-lo[0], hi[2], -lo[1])
    return {'triangles': tris, 'materials': sorted(m.name for m in mesh.data.materials),
            'bounds_unity_m': {'min': [round(v, 3) for v in ulo], 'max': [round(v, 3) for v in uhi]}}


records = []
built = {}
for build in BUILDS:
    name = build.__doc__ and build.__name__
    root = build()
    built[root.name] = root
    if ONLY and root.name not in ONLY:
        continue
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for c in root.children: c.select_set(True)
    bpy.context.view_layer.objects.active = root
    dest = EXPORT / (root.name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(dest), use_selection=True, global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
                             bake_space_transform=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=False,
                             mesh_smooth_type='OFF', use_tspace=False, add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    rec = stats(root)
    rec.update({'id': root.name, 'export': str(dest.relative_to(REPO)).replace('\\', '/'),
                'sha256': hashlib.sha256(dest.read_bytes()).hexdigest(), 'bytes': dest.stat().st_size})
    assert rec['triangles'] < 120000, rec
    records.append(rec)
    print('GK_EXPORTED', root.name, rec['triangles'], rec['bounds_unity_m'])

if not ONLY:
    manifest = {
        'schema': 1, 'title': 'Evolution Shop growth kit (GR-16): the new game\'s look',
        'origin': {'author': 'Original local asset authorship for Evolution Shop (GR-16)', 'external_sources': [],
                   'paid_generation': False, 'external_uploads': False},
        'blender_version': bpy.app.version_string, 'source_script': 'Tools/Art/create_growth_kit.py',
        'source_script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'axis_mapping': 'Unity (x, y, z) -> Blender (-x, -z, y), a reflection; probed to import exactly (create_diorama_kit.py)',
        'export_settings': {'format': 'FBX', 'forward': '-Z', 'up': 'Y', 'bake_space_transform': True, 'mesh_smooth_type': 'OFF'},
        'materials': {k: {'srgb_hex': v} for k, v in PALETTE.items()},
        'assets': records,
    }
    (SOURCE / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print('GROWTH_KIT_COMPLETE', len(records))
