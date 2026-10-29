"""Original Evolution Shop growth kit (GR-16): the new game's look, approved by the user on 2026-10-25 (GR.48). Blender 5.0,
no external assets or add-ons; everything is generated from the numbers below.

Run: blender --background --factory-startup --python Tools/Art/create_growth_kit.py -- --repo C:/dev/DumbVideoGameStudio [--only A,B] [--no-render]

GR-19 (GR.60, GR.61): nine pieces of decor in three themes (Riverbank: reeds with a dragonfly, a lily-pad lamp, a
watermill; Hot springs: a little volcano, a stone lantern, a rubber-duck hot pool; Seaside: a lighthouse, a treasure chest, a
coral arch), each standing on a shop tank's top (Runtime/Growth/GrowthWorldDecor.cs).

Wave 4 (GR.45): shop units B and C and the exhibition hall: their floors and dressing (boards, rails, beams, frames, lamps,
rugs), the street outside unit B, GR-10's set pieces (the bay window bench and its hanging bowls, unit A's window display,
unit C's feature wall and built-in feature tank), the hall's banners and spotlights, GR-11's exhibition sign, award and
display tags, the staff board and the hall's plans, and the four rooms' partitions as painted hoardings (COMING SOON).

Wave 3 (GR.45): the machines: the generator (the hand crank and its wheel, the bubble turbine, the twin turbine, the tide
engine), the pumps (the hand pump and its wheel, the electric motor), buffers and their numbered tags, the power board, the
sorter and the incubator's outlet; the lab annex and the workshop dressed, with GR-10's pipe gallery and dome light. (The
pipes and the chute are drawn by the game: Runtime/Growth/GrowthWorldLookPipes.cs.)

Wave 2 (GR.45): the other stations and tanks: the conditioning vat (its second batch and stirrer), the breeding tank (its
tint port and second pair), the research bench (its microscope, analyzer and planner) and the siphon (its bigger bulb, twin
chamber and vacuum), the vial shelf and its bottles, the supplier catalog, the pinboard, and the Large tank, the Show tank
(its light hood and spotlight) and the lab rack, each with its pebbles and plants, filter and lid.

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


def text(body, center, size, mat, euler=(0, 0, 0), extrude=0.02, res=None):
    """Letters reading left to right along Unity +X, facing Unity -Z (rot turns them), extruded toward +Z. res, when given,
    is a coarser curve resolution (wave 4's signs and boards: their letters are small, so fewer triangles do)."""
    bpy.ops.object.text_add()
    o = bpy.context.object
    o.data.body = body; o.data.size = 1.0; o.data.extrude = extrude / size; o.data.align_x = 'CENTER'; o.data.align_y = 'CENTER'
    o.data.bevel_depth = 0.25 * extrude / size; o.data.bevel_resolution = 2
    if res is not None:
        o.data.resolution_u = res; o.data.bevel_resolution = 1
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


# ================================================================ wave 2: the other stations and the other tanks (GR-16, GR.45's wave 2)
def heart(center, s, mat, depth=0.3):
    """A puffy heart facing +Z: a square turned 45 degrees and two round lobes on its upper sides."""
    x, y, z = center
    box((x, y - s * 0.12, z), (s * 0.72, s * 0.72, s * depth), mat, r=s * 0.1, rot=(0, 0, 45))
    for sx in (-1, 1):
        ball((x + sx * s * 0.26, y + s * 0.14, z), (s * 0.37, s * 0.37, s * depth * 0.5), mat, seg=20)


def vat():
    """GrowthWorld.BuildSource's conditioning vat (Mist): a base 0.8 x 0.3 x 0.7 and a glass column 0.62 wide from 0.3 to
    1.3, front +Z; the culture's level is the greybox's own fill (kept, inside the glass). A cream base with its label in the
    culture's colour, a turquoise collar and cap, a dome, two rods at the back, a spout on the back where a pipe meets the vat
    (its tap point, 1.3 m) and a stub on top."""
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl((sx * 0.3, 0.03, sz * 0.25), 0.055, 0.06, 'GK_TealDark', r=0.02)
    box((0, 0.18, 0), (0.8, 0.3, 0.7), 'GK_Cream', r=0.09)
    box((0, 0.31, 0), (0.82, 0.05, 0.72), 'GK_Teal', r=0.025)
    box((0, 0.17, 0.352), (0.34, 0.11, 0.02), 'GK_Culture', r=0.03)
    for sx in (-1, 1):
        ball((sx * 0.27, 0.17, 0.36), 0.028, 'GK_Yellow', seg=14)
    cyl((0, 0.36, 0), 0.345, 0.08, 'GK_Teal', r=0.03)
    cyl((0, 0.84, 0), 0.31, 0.9, 'GK_Glass', r=0, verts=40)
    for a in (210, 330):
        rad = math.radians(a)
        cyl((0.33 * math.cos(rad), 0.84, 0.33 * math.sin(rad)), 0.018, 0.9, 'GK_TealDark', r=0.006, verts=12)
    cyl((0, 1.32, 0), 0.345, 0.07, 'GK_Teal', r=0.03)
    ball((0, 1.355, 0), (0.29, 0.1, 0.29), 'GK_Cream')
    cyl((0, 1.3, -0.335), 0.034, 0.07, 'GK_Teal', r=0.01, rot=(90, 0, 0))
    ring((0, 1.3, -0.365), 0.042, 0.011, 'GK_TealDark', rot=(90, 0, 0))
    cyl((0.18, 1.47, -0.06), 0.04, 0.16, 'GK_TealDark', r=0.012)
    ring((0.18, 1.55, -0.06), 0.045, 0.012, 'GK_Teal')
    return join('Vat')


def vat_batch():
    """Level 2: the second batch beside the column (the greybox's at 0.48, 0.45, -0.18): its own base, glass, Mist inside
    (the culture's own colour) and a pipe into the column."""
    x, z = 0.48, -0.18
    cyl((x, 0.07, z), 0.16, 0.14, 'GK_Cream', r=0.05)
    cyl((x, 0.15, z), 0.15, 0.03, 'GK_Teal', r=0.012)
    cyl((x, 0.455, z), 0.13, 0.58, 'GK_Glass', r=0, verts=32)
    cyl((x, 0.36, z), 0.11, 0.38, 'GK_Culture', r=0.03)
    cyl((x, 0.765, z), 0.145, 0.06, 'GK_Teal', r=0.02)
    tube([(x, 0.79, z), (x, 0.98, z), (0.27, 0.98, -0.1)], 0.025, 'GK_Teal')
    return join('VatBatch')


def vat_stirrer():
    """Level 3: a yellow stirrer motor on the dome with vents and a light, its rod down into the Mist with a paddle."""
    box((0, 1.52, 0), (0.24, 0.17, 0.22), 'GK_Yellow', r=0.06)
    for k in range(3):
        box((-0.06 + k * 0.06, 1.53, 0.112), (0.025, 0.09, 0.008), 'GK_TealDark', r=0.006)
    ball((0.085, 1.575, 0.105), 0.02, 'GK_GlowLime', seg=12)
    cyl((0, 1.625, 0), 0.04, 0.04, 'GK_TealDark', r=0.012)
    cyl((0, 0.98, 0), 0.016, 0.9, 'GK_TealDark', r=0, verts=12)
    box((0, 0.6, 0), (0.22, 0.02, 0.08), 'GK_Cream', r=0.01, rot=(0, 35, 0))
    return join('VatStirrer')


def breeding_tank():
    """GrowthWorld.BuildBreeding (front +Z): a cabinet 1.1 x 0.78 x 0.5 and the glass from 0.78 to 1.22 (the greybox's glass
    and the pair's water kept), the divider at x 0.22 making the nursery on the right. A cream cabinet with a pink heart
    plate and the nursery's yellow sign, a turquoise frame open at the top, the divider a pane in a frame, a sand bed with
    pebbles, moss in the nursery and a plant."""
    box((0, 0.05, 0), (1.06, 0.1, 0.46), 'GK_TealDark', r=0.03)
    box((0, 0.435, 0), (1.1, 0.69, 0.5), 'GK_Cream', r=0.08)
    box((-0.2, 0.46, 0.255), (0.32, 0.3, 0.02), 'GK_Pink', r=0.07)
    heart((-0.2, 0.46, 0.27), 0.14, 'GK_Coral')
    box((0.34, 0.6, 0.255), (0.24, 0.1, 0.02), 'GK_Yellow', r=0.035)
    ball((0.34, 0.6, 0.268), (0.028, 0.036, 0.012), 'GK_Cream', seg=14)
    box((0, 0.79, 0), (1.12, 0.04, 0.52), 'GK_Teal', r=0.018)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl((sx * 0.535, 1.0, sz * 0.235), 0.024, 0.44, 'GK_Teal', r=0.008, verts=16)
    for sz in (-1, 1):
        box((0, 1.235, sz * 0.235), (1.1, 0.03, 0.035), 'GK_Teal', r=0.012)
    for sx in (-1, 1):
        box((sx * 0.535, 1.235, 0), (0.035, 0.03, 0.5), 'GK_Teal', r=0.012)
    box((0.22, 1.0, 0), (0.01, 0.4, 0.43), 'GK_Glass', r=0)
    for sz in (-1, 1):
        box((0.22, 1.0, sz * 0.212), (0.022, 0.42, 0.022), 'GK_TealDark', r=0.008)
    box((0.22, 1.205, 0), (0.024, 0.02, 0.45), 'GK_TealDark', r=0.008)
    box((0, 0.815, 0), (1.0, 0.03, 0.42), 'GK_Sand', r=0.01)
    pebbles = ['GK_Pebble1', 'GK_Pebble2', 'GK_Pebble3', 'GK_Pebble4', 'GK_Sand']
    for k in range(48):
        ball((RNG.uniform(-0.47, 0.47), 0.835, RNG.uniform(-0.18, 0.18)), (0.022, 0.012, 0.019), pebbles[k % 5], seg=10,
             rot=(0, RNG.uniform(0, 180), 0))
    ball((0.36, 0.84, 0.02), (0.11, 0.03, 0.12), 'GK_Lime', seg=20)
    ball((-0.44, 0.9, -0.15), (0.032, 0.07, 0.026), 'GK_Green', seg=14)
    ball((-0.39, 0.88, -0.16), (0.028, 0.05, 0.022), 'GK_Lime', seg=14)
    return join('BreedingTank')


def breeding_port():
    """Level 2: the tint port, a yellow funnel on the rim's front left corner (the greybox's at -0.48, 1.29, 0.2)."""
    box((-0.48, 1.235, 0.2), (0.11, 0.035, 0.11), 'GK_Teal', r=0.015)
    cyl((-0.48, 1.27, 0.2), 0.026, 0.05, 'GK_TealDark', r=0.008)
    cone((-0.48, 1.34, 0.2), 0.03, 0.085, 0.1, 'GK_Yellow')
    ring((-0.48, 1.39, 0.2), 0.083, 0.01, 'GK_Yellow')
    return join('BreedingPort')


def breeding_pair():
    """Level 3: the second pair's compartment, the greybox's glass divider at x -0.145 (kept) in a turquoise frame, and a
    second heart on the cabinet."""
    x = -0.145
    for sz in (-1, 1):
        box((x, 1.0, sz * 0.212), (0.02, 0.42, 0.02), 'GK_TealDark', r=0.008)
    box((x, 1.205, 0), (0.022, 0.02, 0.45), 'GK_TealDark', r=0.008)
    box((0.06, 0.46, 0.255), (0.16, 0.16, 0.02), 'GK_Pink', r=0.06)
    heart((0.06, 0.46, 0.27), 0.08, 'GK_Coral')
    return join('BreedingPair')


def bench():
    """GrowthWorld.Stations' research bench (1.3 x 0.6, top at 0.91, front +Z; it stands turned to face the back room): two
    wooden pedestals with turquoise drawers, a back panel, a cream top with a turquoise edge, and the notes level 1 keeps."""
    for s in (-1, 1):
        box((s * 0.45, 0.04, 0), (0.36, 0.08, 0.56), 'GK_TealDark', r=0.025)
        box((s * 0.45, 0.46, 0), (0.34, 0.78, 0.54), 'GK_Wood', r=0.04)
        for k in range(2):
            box((s * 0.45, 0.26 + k * 0.34, 0.272), (0.28, 0.26, 0.02), 'GK_Teal', r=0.03)
            ball((s * 0.45, 0.26 + k * 0.34, 0.29), 0.024, 'GK_Yellow', seg=14)
    box((0, 0.52, -0.22), (0.58, 0.56, 0.03), 'GK_WoodDark', r=0.012)
    box((0, 0.88, 0), (1.36, 0.06, 0.64), 'GK_Cream', r=0.03)
    box((0, 0.875, 0.322), (1.34, 0.035, 0.012), 'GK_Teal', r=0.006)
    papers = ('GK_Paper', 'GK_Pebble3', 'GK_Pebble4')
    for k in range(3):
        box((-0.17 + k * 0.045, 0.912 + k * 0.003, 0.1 - k * 0.02), (0.16, 0.004, 0.2), papers[k], r=0, rot=(0, -8 + k * 9, 0))
    cyl((-0.02, 0.918, 0.17), 0.008, 0.16, 'GK_Yellow', r=0.002, rot=(0, 20, 90))
    return join('Bench')


def bench_microscope():
    """Level 2: a microscope at the bench's left end (the greybox's at -0.42), its eyepiece toward the reader."""
    x = -0.42
    box((x, 0.925, 0.0), (0.16, 0.03, 0.2), 'GK_Teal', r=0.012)
    box((x, 1.04, -0.07), (0.05, 0.22, 0.05), 'GK_Teal', r=0.018, rot=(12, 0, 0))
    cyl((x, 0.99, 0.03), 0.055, 0.015, 'GK_TealDark', r=0.005)
    cyl((x, 1.13, 0.0), 0.03, 0.2, 'GK_Cream', r=0.01, rot=(20, 0, 0))
    cyl((x, 1.25, 0.045), 0.024, 0.06, 'GK_Ink', r=0.008, rot=(20, 0, 0))
    cyl((x + 0.035, 1.05, -0.07), 0.022, 0.03, 'GK_Coral', r=0.006, rot=(0, 0, 90))
    return join('BenchMicroscope')


def bench_analyzer():
    """Level 3: the analyzer (the greybox's at 0.12, 1.0, -0.12): a rounded cream box, its glowing screen with a trace, two
    lights and a lit vial."""
    box((0.12, 1.0, -0.12), (0.3, 0.18, 0.2), 'GK_Cream', r=0.05)
    box((0.1, 1.005, -0.018), (0.2, 0.11, 0.008), 'GK_Screen', r=0.012)
    for k, h in enumerate((0.03, 0.06, 0.045, 0.075, 0.035)):
        box((0.035 + k * 0.032, 0.975 + h / 2, -0.013), (0.016, h, 0.004), 'GK_GlowLime', r=0.003)
    for k, m in enumerate(('GK_GlowCoral', 'GK_GlowYellow')):
        ball((0.245, 1.03 - k * 0.04, -0.016), 0.012, m, seg=10)
    cyl((0.2, 1.1, -0.17), 0.03, 0.02, 'GK_TealDark', r=0.006)
    cyl((0.2, 1.16, -0.17), 0.02, 0.1, 'GK_GlowCoral', r=0.008)
    return join('BenchAnalyzer')


def bench_planner():
    """Level 4: the planner board upright at the back (the greybox's at 0.12, 1.38, -0.2): a coral frame on two posts, cards
    pinned to it and joined by a string."""
    for sx in (-1, 1):
        cyl((0.12 + sx * 0.4, 1.2, -0.25), 0.018, 0.58, 'GK_TealDark', r=0.006, verts=12)
    box((0.12, 1.4, -0.25), (0.84, 0.42, 0.03), 'GK_Coral', r=0.03)
    box((0.12, 1.4, -0.232), (0.76, 0.34, 0.008), 'GK_Paper', r=0.008)
    for cx, cy, m in ((-0.18, 1.48, 'GK_Pebble3'), (0.04, 1.44, 'GK_Pebble4'), (0.27, 1.49, 'GK_Pebble1'), (0.2, 1.31, 'GK_Pebble2'), (-0.08, 1.3, 'GK_Lime')):
        box((0.12 + cx, cy, -0.226), (0.14, 0.1, 0.004), m, r=0.006)
        ball((0.12 + cx, cy + 0.04, -0.222), 0.01, 'GK_Coral', seg=8)
    tube([(-0.06, 1.52, -0.221), (0.05, 1.46, -0.221), (0.16, 1.48, -0.221), (0.28, 1.47, -0.221), (0.39, 1.53, -0.221)], 0.003, 'GK_Coral', sides=6)
    return join('BenchPlanner')


def siphon_bulb():
    """The siphon at level 1 (the greybox's bulb at 0.2, 1.0, 0; set a little forward, clear of the analyzer): a glass bulb of
    Flow on a little stand, a coral squeeze bulb."""
    x, z = 0.22, 0.08
    cyl((x, 0.92, z), 0.055, 0.02, 'GK_TealDark', r=0.006)
    cyl((x, 0.95, z), 0.012, 0.05, 'GK_TealDark', r=0, verts=12)
    ball((x, 1.03, z), (0.05, 0.065, 0.05), 'GK_Glass', seg=24)
    ball((x, 1.015, z), (0.036, 0.042, 0.036), 'GK_GlowBlue', seg=16)
    cyl((x, 1.1, z), 0.018, 0.03, 'GK_Teal', r=0.006)
    ball((x, 1.135, z), (0.032, 0.036, 0.032), 'GK_Coral', seg=18)
    return join('SiphonBulb')


def siphon_big():
    """Level 2: a bigger bulb of Flow (the greybox's at 0.38, 0.98, 0.12) on its own stand."""
    x, z = 0.38, 0.12
    cyl((x, 0.92, z), 0.065, 0.02, 'GK_TealDark', r=0.006)
    ball((x, 1.0, z), (0.065, 0.075, 0.065), 'GK_Glass', seg=24)
    ball((x, 0.985, z), (0.05, 0.05, 0.05), 'GK_GlowBlue', seg=16)
    cyl((x, 1.085, z), 0.02, 0.03, 'GK_Teal', r=0.006)
    ball((x, 1.12, z), (0.034, 0.04, 0.034), 'GK_Coral', seg=18)
    return join('SiphonBig')


def siphon_twin():
    """Level 3: the twin chamber of Bloom (the greybox's at 0.38, 0.98, -0.12), joined to the big bulb by a glass bridge."""
    x, z = 0.38, -0.12
    cyl((x, 0.92, z), 0.065, 0.02, 'GK_TealDark', r=0.006)
    ball((x, 1.0, z), (0.065, 0.075, 0.065), 'GK_Glass', seg=24)
    ball((x, 0.985, z), (0.05, 0.05, 0.05), 'GK_GlowCoral', seg=16)
    cyl((x, 1.0, 0.0), 0.016, 0.12, 'GK_Glass', r=0, rot=(90, 0, 0), verts=16)
    ring((x, 1.0, 0.0), 0.022, 0.006, 'GK_Teal', rot=(90, 0, 0))
    return join('SiphonTwin')


def siphon_vacuum():
    """Level 4: the vacuum's yellow canister (the greybox's at 0.52, 1.02, 0) with a gauge, its hose to the bulbs."""
    x = 0.54
    cyl((x, 1.02, 0), 0.07, 0.2, 'GK_Yellow', r=0.025)
    cyl((x, 1.13, 0), 0.06, 0.03, 'GK_Teal', r=0.01)
    cyl((x, 1.03, 0.072), 0.028, 0.012, 'GK_GaugeFace', r=0.004, rot=(90, 0, 0))
    box((x + 0.008, 1.035, 0.08), (0.004, 0.02, 0.003), 'GK_Ink', r=0.001, rot=(0, 0, -30))
    tube([(x, 1.145, 0), (x - 0.04, 1.2, 0.04), (0.4, 1.13, 0.1)], 0.011, 'GK_Teal')
    return join('SiphonVacuum')


def vial_shelf():
    """GR-04's vial shelf on the back room's south wall (front +Z): a wooden shelf with a turquoise lip, yellow brackets on
    the wall, and a paper tag under each bottle's place (x -0.5 to 0.5, 0.25 apart)."""
    box((0, 0.95, 0.05), (1.44, 0.05, 0.3), 'GK_Wood', r=0.015)
    box((0, 0.955, 0.205), (1.44, 0.06, 0.02), 'GK_Teal', r=0.008)
    for sx in (-1, 1):
        box((sx * 0.6, 0.84, -0.02), (0.05, 0.18, 0.2), 'GK_Yellow', r=0.03)
        box((sx * 0.6, 0.84, -0.09), (0.09, 0.26, 0.02), 'GK_Yellow', r=0.012)
    for k in range(5):
        box((-0.5 + 0.25 * k, 0.952, 0.216), (0.12, 0.03, 0.004), 'GK_Paper', r=0.003)
    return join('VialShelf')


def vial_bottle():
    """A starter vial (origin: its bottle's place on the shelf): a round glass bottle (the greybox's culture fill kept inside
    it), a neck, a cork and a little tag."""
    cyl((0, 0.095, 0), 0.055, 0.19, 'GK_Glass', r=0, verts=24)
    ball((0, 0.19, 0), (0.055, 0.028, 0.055), 'GK_Glass', seg=20)
    cyl((0, 0.22, 0), 0.024, 0.045, 'GK_Glass', r=0, verts=16)
    ring((0, 0.205, 0), 0.026, 0.006, 'GK_Teal')
    cyl((0, 0.252, 0), 0.027, 0.04, 'GK_WoodLight', r=0.01)
    box((0.04, 0.17, 0.045), (0.05, 0.035, 0.004), 'GK_Paper', r=0.004, rot=(0, 40, -10))
    return join('VialBottle')


CAT_TILT = 25


def tilted(o, dy, dz, a=CAT_TILT):
    """A point on the catalog's lectern: (dy, dz) in its frame, tilted about X through o."""
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    return (o[0], o[1] + dy * c - dz * s, o[2] + dy * s + dz * c)


def catalog():
    """GR-04's supplier catalog by the counter (front +Z, the reader's side): a round foot and a post, a wooden lectern tilted
    toward the reader with a ledge, a big coral book open on it (a creature on one page, lines on the other), a ribbon."""
    rot = (CAT_TILT, 0, 0)
    cyl((0, 0.03, 0), 0.2, 0.06, 'GK_TealDark', r=0.02)
    cyl((0, 0.5, -0.02), 0.045, 0.88, 'GK_Teal', r=0.015)
    cyl((0, 0.93, -0.02), 0.065, 0.05, 'GK_TealDark', r=0.015)
    o = (0, 1.0, 0)
    box(o, (0.5, 0.05, 0.38), 'GK_Wood', r=0.02, rot=rot)
    box(tilted(o, 0.04, 0.178), (0.5, 0.05, 0.025), 'GK_WoodDark', r=0.01, rot=rot)
    box(tilted(o, 0.035, -0.01), (0.46, 0.022, 0.34), 'GK_Coral', r=0.01, rot=rot)
    for s in (-1, 1):
        box(tilted((s * 0.112, 1.0, 0), 0.058, -0.01), (0.212, 0.026, 0.31), 'GK_Paper', r=0.01, rot=rot)
    box(tilted(o, 0.062, -0.01), (0.016, 0.028, 0.31), 'GK_Coral', r=0.005, rot=rot)
    box(tilted((-0.112, 1.0, 0), 0.073, -0.03), (0.15, 0.004, 0.12), 'GK_TealLight', r=0.004, rot=rot)
    ball(tilted((-0.112, 1.0, 0), 0.078, -0.03), (0.04, 0.012, 0.03), 'GK_Yellow', seg=14, rot=rot)
    for k in range(3):
        box(tilted((0.112, 1.0, 0), 0.073, -0.07 + k * 0.045), (0.15, 0.004, 0.014), 'GK_Sand', r=0.002, rot=rot)
    p = tilted(o, 0.05, 0.16)
    box((0.03, p[1] - 0.07, p[2] + 0.012), (0.028, 0.14, 0.006), 'GK_Yellow', r=0.002)
    return join('Catalog')


def pinboard():
    """The pinboard on the back room's east wall (origin: its centre, 1.55 m up; front -Z, the room's side): a cork board in
    a wooden frame, three papers pinned where the greybox's notes are (now, next and the long goal), pins, a string and a
    sticky note."""
    def at(x, y, dx, dy, a):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        return x + dx * c - dy * s, y + dx * s + dy * c
    box((0, 0, 0.005), (1.34, 0.8, 0.04), 'GK_Sand', r=0.01)
    for sy in (-1, 1):
        box((0, sy * 0.42, 0), (1.46, 0.06, 0.07), 'GK_WoodMid', r=0.02)
    for sx in (-1, 1):
        box((sx * 0.7, 0, 0), (0.06, 0.84, 0.07), 'GK_WoodMid', r=0.02)
    heads = ('GK_Yellow', 'GK_TealLight', 'GK_Pink')
    pins = ('GK_Coral', 'GK_Teal', 'GK_Lilac')
    tops = []
    for i in range(3):
        x, y, a = -0.4 + 0.4 * i, 0.05 * (i % 2) - 0.02, (-3, 2, -2)[i]
        box((x, y, -0.022), (0.3, 0.34, 0.006), 'GK_Paper', r=0.006, rot=(0, 0, a))
        hx, hy = at(x, y, 0, 0.125, a)
        box((hx, hy, -0.027), (0.28, 0.06, 0.004), heads[i], r=0.004, rot=(0, 0, a))
        for k in range(3):
            lx, ly = at(x, y, -0.02, 0.03 - k * 0.06, a)
            box((lx, ly, -0.027), (0.2, 0.014, 0.003), 'GK_TealLight', r=0.002, rot=(0, 0, a))
        px, py = at(x, y, 0, 0.15, a)
        ball((px, py, -0.035), 0.018, pins[i], seg=12)
        tops.append((px, py))
    pts = []
    for i, (px, py) in enumerate(tops):
        pts.append((px, py, -0.04))
        if i < 2:
            nx, ny = tops[i + 1]
            pts.append(((px + nx) / 2, min(py, ny) - 0.035, -0.04))
    tube(pts, 0.004, 'GK_Coral', sides=6)
    box((0.52, -0.3, -0.022), (0.12, 0.12, 0.004), 'GK_Pebble3', r=0.004, rot=(0, 0, 8))
    return join('Pinboard')


def tank_cabinet(W, D, S, G, doors):
    """A tank's cabinet and frame (front +Z), as the Starter tank's: a kick, a cream cabinet with turquoise doors and yellow
    knobs, the frame's rails and posts round the glass, and a light bar on top at the back."""
    top = S + G
    box((0, 0.05, 0), (W - 0.04, 0.1, D - 0.04), 'GK_TealDark', r=0.03)
    box((0, S / 2 + 0.03, 0), (W, S - 0.06, D), 'GK_Cream', r=0.08)
    dw = (W - 0.12) / doors
    for i in range(doors):
        cx = -W / 2 + 0.06 + dw * (i + 0.5)
        box((cx, S * 0.48, D / 2 + 0.01), (dw - 0.06, S * 0.62, 0.03), 'GK_Teal', r=0.025)
        ball((cx + (dw / 2 - 0.1) * (-1 if i == doors - 1 else 1), S * 0.52, D / 2 + 0.035), 0.03, 'GK_Yellow', seg=16)
    box((0, S - 0.005, 0), (W + 0.06, 0.06, D + 0.06), 'GK_Teal', r=0.025)
    box((0, top + 0.02, 0), (W + 0.06, 0.05, D + 0.06), 'GK_Teal', r=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl((sx * (W / 2 + 0.005), S + G / 2, sz * (D / 2 + 0.005)), 0.03, G, 'GK_Teal', r=0.01, verts=16)
    box((0, top + 0.075, -D * 0.15), (W * 0.8, 0.05, 0.14), 'GK_Cream', r=0.02)
    box((0, top + 0.05, -D * 0.15), (W * 0.74, 0.006, 0.09), 'GK_GlowWhite', r=0)


def pebble_bed(W, D, y, n):
    pebbles = ['GK_Sand', 'GK_Pebble1', 'GK_Pebble2', 'GK_Pebble3', 'GK_Pebble4']
    for k in range(n):
        ball((RNG.uniform(-W / 2 + 0.07, W / 2 - 0.07), y, RNG.uniform(-D / 2 + 0.07, D / 2 - 0.07)), (0.03, 0.017, 0.026),
             pebbles[k % len(pebbles)], seg=10, rot=(0, RNG.uniform(0, 180), 0))


def water_plants(y, plants):
    for k, (px, pz, h, m) in enumerate(plants):
        ball((px, y + h / 2, pz), (0.05, h / 2, 0.035), m, seg=16, rot=(0, 0, (k % 5 - 2) * 6))


LARGE = (2.5, 1.0, 0.8, 0.75)
SHOW = (3.0, 1.5, 0.55, 1.0)
RACK = (1.5, 1.0, 0.45, 0.75)


def tank_large():
    """GrowthWorld.BuildTank for a Large tank (2.5 x 1.0; stand 0.8, glass 0.75): the Starter tank's cabinet and frame, longer,
    with three doors."""
    tank_cabinet(*LARGE, 3)
    return join('TankLarge')


def tank_bed_large():
    """The Large tank's pebbles, two clumps of plants and one in the middle at the back, a rock and an arch."""
    W, D, S, G = LARGE
    y = S + 0.06
    pebble_bed(W, D, y, 150)
    water_plants(y, ((1.0, -0.3, 0.5, 'GK_Green'), (1.1, -0.18, 0.36, 'GK_Lime'), (0.9, -0.38, 0.3, 'GK_GreenDark'),
                     (-1.05, -0.3, 0.44, 'GK_Lime'), (-0.95, -0.38, 0.3, 'GK_Green'), (-0.1, -0.38, 0.32, 'GK_GreenDark')))
    ball((-0.55, y + 0.05, -0.25), (0.2, 0.1, 0.14), 'GK_Rock', seg=20)
    ring((0.45, y, -0.28), 0.15, 0.05, 'GK_Rock', rot=(90, 0, 0))
    return join('TankBedLarge')


def tank_lid_large():
    """Large tank level 3: a cream lid with two handles (the greybox's at top + 0.05)."""
    W, D, S, G = LARGE
    top = S + G
    box((0, top + 0.06, 0), (W + 0.04, 0.04, D + 0.04), 'GK_Cream', r=0.018)
    for sx in (-1, 1):
        box((sx * W / 4, top + 0.1, 0.15), (0.3, 0.04, 0.06), 'GK_Yellow', r=0.02)
    return join('TankLidLarge')


def tank_show():
    """GrowthWorld.BuildTank for a Show tank (3.0 x 1.5; stand 0.55, glass 1.0): a low wooden plinth planked on its front,
    a cream band with gold trim, a coral plaque with a gold medal, the frame round the glass with gold corner caps, and a
    light hood along the top front (in place of the greybox's light bar; the game's lamp inside stays)."""
    W, D, S, G = SHOW
    top = S + G
    box((0, 0.05, 0), (W - 0.04, 0.1, D - 0.04), 'GK_TealDark', r=0.03)
    box((0, S / 2 + 0.01, 0), (W, S - 0.1, D), 'GK_WoodDark', r=0.05)
    woods = ['GK_Wood', 'GK_WoodLight', 'GK_WoodMid']
    for k in range(12):
        box((-W / 2 + 0.125 + k * 0.25, S / 2 - 0.01, D / 2 + 0.008), (0.235, S - 0.22, 0.02), woods[k % 3], r=0.01, seg=2)
    box((0, S - 0.04, 0), (W + 0.04, 0.07, D + 0.04), 'GK_Cream', r=0.03)
    box((0, S - 0.002, 0), (W + 0.07, 0.025, D + 0.07), 'GK_Gold', r=0.008)
    box((0, S * 0.46, D / 2 + 0.03), (0.36, 0.2, 0.02), 'GK_Coral', r=0.05)
    ball((0, S * 0.46, D / 2 + 0.045), (0.055, 0.055, 0.014), 'GK_Gold', seg=20)
    ring((0, S * 0.46, D / 2 + 0.045), 0.06, 0.008, 'GK_Yellow', rot=(90, 0, 0))
    box((0, top + 0.02, 0), (W + 0.06, 0.05, D + 0.06), 'GK_Teal', r=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl((sx * (W / 2 + 0.005), S + G / 2, sz * (D / 2 + 0.005)), 0.035, G, 'GK_Teal', r=0.012, verts=16)
            ball((sx * (W / 2 + 0.005), top + 0.06, sz * (D / 2 + 0.005)), 0.04, 'GK_Gold', seg=14)
    box((0, top + 0.09, D / 2 - 0.14), (W * 0.92, 0.08, 0.24), 'GK_Cream', r=0.03)
    box((0, top + 0.047, D / 2 - 0.14), (W * 0.86, 0.006, 0.14), 'GK_GlowWarm', r=0)
    box((0, top + 0.12, D / 2 - 0.01), (W * 0.92, 0.035, 0.03), 'GK_Teal', r=0.012)
    return join('TankShow')


def tank_bed_show():
    """The Show tank's scene: pebbles, a sand dune, tall plants at the back corners, an arch, rocks, coral branches and a
    bubble stone, all clear of the middle where the creatures swim."""
    W, D, S, G = SHOW
    y = S + 0.06
    pebble_bed(W, D, y, 220)
    ball((-0.7, y + 0.01, -0.25), (0.65, 0.07, 0.4), 'GK_Sand', seg=28)
    water_plants(y, ((1.25, -0.5, 0.7, 'GK_Green'), (1.35, -0.38, 0.5, 'GK_Lime'), (1.15, -0.62, 0.42, 'GK_GreenDark'), (1.3, -0.15, 0.32, 'GK_Lime'),
                     (-1.3, -0.5, 0.64, 'GK_Lime'), (-1.2, -0.62, 0.46, 'GK_Green'), (-1.38, -0.3, 0.36, 'GK_GreenDark'),
                     (0.1, -0.62, 0.4, 'GK_Green'), (0.25, -0.6, 0.28, 'GK_Lime')))
    ring((0.62, y, -0.45), 0.22, 0.07, 'GK_Rock', rot=(90, 0, 0))
    ball((-0.3, y + 0.06, -0.5), (0.24, 0.12, 0.16), 'GK_Rock', seg=20)
    ball((0.95, y + 0.04, 0.35), (0.14, 0.07, 0.1), 'GK_Rock', seg=18)
    for k, (cx, cz, a, h, m) in enumerate(((-0.95, 0.2, -18, 0.24, 'GK_Coral'), (-0.88, 0.22, 14, 0.3, 'GK_Coral'), (-0.82, 0.16, 32, 0.2, 'GK_Pink'))):
        cyl((cx, y + h / 2, cz), 0.025, h, m, r=0.01, rot=(0, 0, a), verts=12)
        tip = (cx - math.sin(math.radians(a)) * h / 2, y + h / 2 + math.cos(math.radians(a)) * h / 2, cz)
        ball(tip, 0.035, m, seg=12)
    ball((-0.5, y + 0.02, 0.45), (0.06, 0.03, 0.06), 'GK_TealLight', seg=16)
    return join('TankBedShow')


def tank_spotlight():
    """Show tank level 2: the spotlight above its front (the greybox's at 0, 2.7, D/2 + 0.2): a rod from the 3.2 m ceiling,
    a yellow shade turned toward the tank and its warm glowing face."""
    W, D, S, G = SHOW
    z = D / 2 + 0.2
    cyl((0, 3.0, z), 0.014, 0.4, 'GK_TealDark', r=0, verts=12)
    cyl((0, 3.185, z), 0.05, 0.02, 'GK_TealDark', r=0.006)
    ball((0, 2.8, z), 0.03, 'GK_TealDark', seg=12)
    o = (0, 2.71, z - 0.02)
    cone(o, 0.12, 0.05, 0.16, 'GK_Yellow', rot=(25, 0, 0))
    face = tilted(o, -0.078, 0, 25)
    cyl(face, 0.105, 0.006, 'GK_GlowWarm', r=0, rot=(25, 0, 0))
    return join('TankSpotlight')


def tank_rack():
    """GrowthWorld.BuildTank for a lab rack (1.5 x 1.0; stand 0.45, glass 0.75; GR-08): turquoise steel shelving round a plain
    tank: four posts with cross rails, a lower shelf with crates, the shelf the tank stands on with its label holder, a top
    shelf with jars and a box, and the glass's rails. No plants: a rack is never on show."""
    W, D, S, G = RACK
    top = S + G
    H = top + 0.42
    for sx in (-1, 1):
        for sz in (-1, 1):
            box((sx * (W / 2 - 0.03), H / 2, sz * (D / 2 - 0.03)), (0.05, H, 0.05), 'GK_TealDark', r=0.012)
        box((sx * (W / 2 - 0.03), 0.26, 0), (0.03, 0.03, D - 0.06), 'GK_Teal', r=0.008)
    for sz in (-1, 1):
        box((0, 0.26, sz * (D / 2 - 0.03)), (W - 0.06, 0.03, 0.03), 'GK_Teal', r=0.008)
    box((0, 0.08, 0), (W - 0.02, 0.03, D - 0.02), 'GK_WoodLight', r=0.01)
    box((0, S - 0.02, 0), (W, 0.04, D), 'GK_Wood', r=0.012)
    box((0, top + 0.4, 0), (W, 0.04, D), 'GK_Wood', r=0.012)
    for k, x in enumerate((-0.38, 0.32)):
        box((x, 0.21, 0.02), (0.5, 0.22, 0.6), 'GK_Cream', r=0.03)
        box((x, 0.25, 0.322), (0.16, 0.04, 0.008), 'GK_TealDark', r=0.01)
        box((x, 0.18, 0.322), (0.22, 0.07, 0.004), ('GK_Pebble3', 'GK_Pebble4')[k], r=0.004)
    box((0, S - 0.02, D / 2 + 0.012), (0.34, 0.07, 0.012), 'GK_Yellow', r=0.01)
    box((0, S - 0.02, D / 2 + 0.02), (0.28, 0.045, 0.004), 'GK_Paper', r=0.003)
    box((0, top + 0.02, 0), (W + 0.04, 0.04, D + 0.04), 'GK_Teal', r=0.015)
    box((0, S + 0.015, 0), (W + 0.02, 0.03, D + 0.02), 'GK_Teal', r=0.01)
    for k, x in enumerate((-0.45, -0.28, -0.11)):
        cyl((x, top + 0.42 + 0.08, -0.15), 0.06, 0.16, 'GK_Glass', r=0, verts=20)
        cyl((x, top + 0.42 + 0.06, -0.15), 0.048, 0.1, ('GK_GlowCoral', 'GK_GlowBlue', 'GK_GlowLime')[k], r=0.01)
        cyl((x, top + 0.42 + 0.17, -0.15), 0.062, 0.025, 'GK_Teal', r=0.008)
    box((0.35, top + 0.42 + 0.07, -0.1), (0.36, 0.14, 0.26), 'GK_Coral', r=0.03)
    return join('TankRack')


def tank_lid_rack():
    """Lab rack level 3: a cream lid with a handle (the greybox's at top + 0.05), under the top shelf."""
    W, D, S, G = RACK
    top = S + G
    box((0, top + 0.06, 0), (W + 0.04, 0.04, D + 0.04), 'GK_Cream', r=0.018)
    box((0, top + 0.1, 0.15), (0.3, 0.04, 0.06), 'GK_Yellow', r=0.02)
    return join('TankLidRack')


WAVE2 = [vat, vat_batch, vat_stirrer, breeding_tank, breeding_port, breeding_pair, bench, bench_microscope, bench_analyzer,
         bench_planner, siphon_bulb, siphon_big, siphon_twin, siphon_vacuum, vial_shelf, vial_bottle, catalog, pinboard,
         tank_large, tank_bed_large, tank_lid_large, tank_show, tank_bed_show, tank_spotlight, tank_rack, tank_lid_rack]


# ================================================================ wave 3: the machines, the annex and the workshop (GR-16, GR.45's wave 3)
def gen_crank():
    """GrowthWorld.BuildGenerator at level 1, the hand crank (origin: the generator's root; front +Z, into the room): a
    platform, a yellow housing with vents, a gauge, a plate and a lamp, and the axle the wheel turns on. The wheel is a
    piece of its own (GenCrankWheel), placed on the greybox's crank wheel, so it turns while it is cranked."""
    box((0, 0.05, 0), (1.0, 0.1, 0.8), 'GK_TealDark', r=0.04)
    box((0, 0.5, -0.1), (0.72, 0.8, 0.5), 'GK_Yellow', r=0.12)
    box((0, 0.16, -0.1), (0.74, 0.05, 0.52), 'GK_Teal', r=0.02)
    for k in range(4):
        box((0.362, 0.7 - k * 0.08, -0.1), (0.008, 0.035, 0.3), 'GK_TealDark', r=0.006)
    cyl((0.22, 0.32, 0.16), 0.07, 0.03, 'GK_Teal', r=0.01, rot=(90, 0, 0))
    cyl((0.22, 0.32, 0.176), 0.055, 0.005, 'GK_GaugeFace', r=0, rot=(90, 0, 0))
    box((0.23, 0.335, 0.181), (0.008, 0.045, 0.004), 'GK_Coral', r=0.002, rot=(0, 0, -40))
    box((-0.2, 0.32, 0.157), (0.2, 0.12, 0.012), 'GK_Cream', r=0.025)
    box((-0.2, 0.32, 0.165), (0.04, 0.08, 0.004), 'GK_Yellow', r=0.004, rot=(0, 0, 20))
    cyl((0.2, 0.92, -0.1), 0.05, 0.04, 'GK_Teal', r=0.01)
    ball((0.2, 0.97, -0.1), 0.05, 'GK_GlowLime', seg=20)
    cyl((0, 0.85, 0.24), 0.035, 0.18, 'GK_TealDark', r=0.01, rot=(90, 0, 0))
    return join('GenCrank')


def gen_crank_wheel():
    """The hand crank's wheel (origin: its centre, facing +Z): a coral rim with three spokes, a yellow hub and a cream handle."""
    ring((0, 0, 0), 0.27, 0.035, 'GK_Coral', rot=(90, 0, 0))
    for a in (0, 60, 120):
        box((0, 0, 0), (0.52, 0.025, 0.02), 'GK_Coral', r=0.008, rot=(0, 0, a))
    cyl((0, 0, 0.01), 0.055, 0.05, 'GK_Yellow', r=0.015, rot=(90, 0, 0))
    cyl((0.24, 0, 0.08), 0.022, 0.13, 'GK_Cream', r=0.008, rot=(90, 0, 0))
    ball((0.24, 0, 0.15), 0.032, 'GK_Cream', seg=16)
    return join('GenCrankWheel')


def gen_turbine(name, columns):
    """GrowthWorld.BuildGenerator at levels 2 to 4 (front +Z): the bubble turbine (one glass column), the twin turbine (two)
    and the tide engine (three, and a ribbed drum behind). A platform, a cream base with a turquoise band, the yellow turbine
    housing with vents and a lamp, and each column's glass between a collar and a cap, joined by a yellow header; the
    greybox's water and its rising bubbles stay inside each column."""
    w = 0.7 + 0.45 * (columns - 1)
    W = max(1.0, w)
    box((0, 0.05, 0), (W + 0.08, 0.1, 0.84), 'GK_TealDark', r=0.04)
    box((0, 0.26, 0), (W, 0.32, 0.8), 'GK_Cream', r=0.07)
    box((0, 0.42, 0), (W + 0.02, 0.04, 0.82), 'GK_Teal', r=0.015)
    box((0, 0.61, 0), (w, 0.36, 0.6), 'GK_Yellow', r=0.08)
    for k in range(3):
        box((-w * 0.12, 0.68 - k * 0.07, 0.302), (w * 0.5, 0.025, 0.008), 'GK_TealDark', r=0.006)
    lamp_x = 0.28 + 0.22 * (columns - 1)
    cyl((lamp_x, 0.72, 0.305), 0.045, 0.02, 'GK_Teal', r=0.006, rot=(90, 0, 0))
    ball((lamp_x, 0.72, 0.318), 0.032, 'GK_GlowLime', seg=16)
    xs = [(k - (columns - 1) * 0.5) * 0.5 for k in range(columns)]
    for x in xs:
        cyl((x, 0.82, 0), 0.245, 0.06, 'GK_Teal', r=0.02)
        cyl((x, 1.3, 0), 0.22, 0.92, 'GK_Glass', r=0, verts=40)
        cyl((x, 1.79, 0), 0.245, 0.07, 'GK_Teal', r=0.025)
    if columns >= 2:
        cyl((0, 1.9, 0), 0.045, (columns - 1) * 0.5 + 0.1, 'GK_Yellow', r=0.02, rot=(0, 0, 90))
        for x in xs:
            cyl((x, 1.85, 0), 0.035, 0.06, 'GK_TealDark', r=0.01)
    else:
        ball((0, 1.85, 0), (0.12, 0.06, 0.12), 'GK_Cream', seg=20)
    if columns >= 3:
        cyl((0, 0.6, -0.32), 0.21, 1.0, 'GK_Yellow', r=0.05, rot=(0, 0, 90))
        for k in range(5):
            ring((-0.4 + k * 0.2, 0.6, -0.32), 0.212, 0.016, 'GK_Coral', rot=(0, 0, 90))
        for s in (-1, 1):
            box((s * 0.55, 0.43, -0.32), (0.08, 0.3, 0.16), 'GK_Teal', r=0.03)
    return join(name)


def pump_hand():
    """A grower's hand pump (pumps' level 1; origin: the greybox's pump on the grower's front, front +Z): a turquoise housing
    with a foot and a hub; its wheel is a piece of its own (PumpHandWheel), so it turns while it pumps."""
    box((0, 0, 0), (0.2, 0.26, 0.14), 'GK_Teal', r=0.05)
    box((0, -0.14, -0.01), (0.16, 0.04, 0.12), 'GK_TealDark', r=0.015)
    cyl((0, 0, 0.08), 0.035, 0.04, 'GK_TealDark', r=0.01, rot=(90, 0, 0))
    return join('PumpHand')


def pump_hand_wheel():
    """The hand pump's wheel (origin: its centre, facing +Z): a yellow rim with spokes and a coral knob on its handle."""
    ring((0, 0, 0), 0.09, 0.014, 'GK_Yellow', rot=(90, 0, 0))
    for a in (0, 90):
        box((0, 0, 0), (0.17, 0.016, 0.012), 'GK_Yellow', r=0.005, rot=(0, 0, a))
    cyl((0.08, 0, 0.035), 0.012, 0.06, 'GK_TealDark', r=0.004, rot=(90, 0, 0))
    ball((0.08, 0, 0.07), 0.024, 'GK_Coral', seg=14)
    return join('PumpHandWheel')


def pump_motor():
    """An electric pump (levels 2 and 3): the housing with a cream motor can, its cooling fins and its shaft cap (the
    greybox's light, green or amber by level, stays)."""
    box((0, 0, 0), (0.2, 0.26, 0.14), 'GK_Teal', r=0.05)
    cyl((0, 0.02, 0.1), 0.075, 0.08, 'GK_Cream', r=0.02, rot=(90, 0, 0))
    for k in range(4):
        ring((0, 0.02, 0.068 + k * 0.018), 0.077, 0.006, 'GK_TealLight', rot=(90, 0, 0))
    cyl((0, 0.02, 0.145), 0.03, 0.012, 'GK_TealDark', r=0.004, rot=(90, 0, 0))
    return join('PumpMotor')


def buffer_column():
    """A buffer (origin: its place by the annex's south wall; its tag faces +Z, into the annex): a stand, a collar, a tall
    glass column (the greybox's culture fill kept inside, rising as it fills), a band, a lid and the inlet the pipe drops to
    (1.5 m)."""
    cyl((0, 0.1, 0), 0.2, 0.2, 'GK_TealDark', r=0.05)
    cyl((0, 0.23, 0), 0.195, 0.06, 'GK_Teal', r=0.02)
    cyl((0, 0.84, 0), 0.185, 1.2, 'GK_Glass', r=0, verts=40)
    ring((0, 0.5, 0), 0.19, 0.01, 'GK_Teal')
    cyl((0, 1.465, 0), 0.2, 0.05, 'GK_Teal', r=0.02)
    cyl((0, 1.505, 0), 0.045, 0.03, 'GK_TealDark', r=0.01)
    return join('Buffer')


def buffer_tag(n):
    """Buffer n's tag on its band, its number facing into the annex."""
    def build():
        box((0, 0.42, 0.205), (0.15, 0.12, 0.014), 'GK_Cream', r=0.03)
        text(str(n), (0, 0.42, 0.217), 0.08, 'GK_Ink', euler=(0, 180, 0), extrude=0.004)
        return join('BufferTag' + str(n))
    build.__name__ = 'buffer_tag_' + str(n)
    return build


def power_board():
    """The power board on the annex's east wall (origin: its centre; front -Z, into the annex): a turquoise frame, a cream
    face with POWER on it, dark slots behind the greybox's supply and draw bars (kept, they move), a green and an amber dot
    beside them, and a bezel round the greybox's lamp (kept, its colour says whether supply meets draw)."""
    box((0, 0, 0), (1.16, 0.86, 0.06), 'GK_Teal', r=0.05)
    box((0, 0, -0.033), (1.04, 0.74, 0.006), 'GK_Cream', r=0.03)
    for y in (0.16, -0.04):
        box((0, y, -0.0375), (0.94, 0.11, 0.003), 'GK_Ink', r=0.02)
    for y, m in ((0.16, 'GK_GlowLime'), (-0.04, 'GK_GlowYellow')):
        ball((-0.495, y, -0.04), 0.018, m, seg=10)
    text('POWER', (-0.05, 0.3, -0.041), 0.07, 'GK_Ink', extrude=0.004)
    ring((0.42, -0.26, -0.037), 0.05, 0.01, 'GK_TealDark', rot=(90, 0, 0))
    return join('PowerBoard')


def sorter():
    """GrowthWorld.BuildSorter (origin: the sorter's root; front +Z, into the workshop): a cream cabinet with turquoise panels,
    its label and lamp; a belt of slats between two rollers; the glass hopper on its arm where the chute arrives (local z
    0.65, its rim at 1.6 m) with a spout down to the belt; the two gates (honey for the shop, coral for the racks) and their
    trays."""
    hz = 0.65
    box((0, 0.05, 0), (1.56, 0.1, 0.76), 'GK_TealDark', r=0.03)
    box((0, 0.48, 0), (1.6, 0.76, 0.8), 'GK_Cream', r=0.08)
    for s in (-1, 1):
        box((s * 0.4, 0.42, 0.405), (0.66, 0.46, 0.02), 'GK_Teal', r=0.04)
    box((-0.3, 0.73, 0.412), (0.42, 0.08, 0.012), 'GK_Yellow', r=0.02)
    cyl((0.5, 0.75, 0.41), 0.045, 0.02, 'GK_Teal', r=0.006, rot=(90, 0, 0))
    ball((0.5, 0.75, 0.422), 0.03, 'GK_GlowLime', seg=14)
    box((0, 0.89, 0), (1.4, 0.05, 0.42), 'GK_Ink', r=0.02)
    for k in range(14):
        box((-0.62 + k * 0.095, 0.922, 0), (0.07, 0.015, 0.4), 'GK_TealLight', r=0.006)
    for s in (-1, 1):
        cyl((s * 0.7, 0.9, 0), 0.05, 0.44, 'GK_TealDark', r=0.015, rot=(90, 0, 0))
    box((0, 1.08, 0.3), (0.07, 0.36, 0.07), 'GK_Teal', r=0.025)
    box((0, 1.28, (0.3 + hz) / 2), (0.07, 0.07, hz - 0.3 + 0.07), 'GK_Teal', r=0.025)
    cyl((0, 1.32, hz), 0.055, 0.06, 'GK_TealDark', r=0.015)
    cone((0, 1.47, hz), 0.06, 0.19, 0.26, 'GK_Glass')
    ring((0, 1.6, hz), 0.19, 0.02, 'GK_Teal')
    tube([(0, 1.32, hz), (0, 1.12, 0.42), (0, 0.98, 0.18)], 0.035, 'GK_Glass')
    for s, m in ((-1, 'GK_Yellow'), (1, 'GK_Coral')):
        for z in (-0.22, 0.22):
            box((s * 0.55, 1.1, z), (0.04, 0.36, 0.04), 'GK_TealDark', r=0.012)
        box((s * 0.55, 1.29, 0), (0.05, 0.04, 0.48), 'GK_TealDark', r=0.012)
        box((s * 0.55, 1.08, 0), (0.035, 0.24, 0.38), m, r=0.02)
        box((s * 0.7, 0.62, 0.49), (0.3, 0.06, 0.18), m, r=0.02)
        box((s * 0.7, 0.575, 0.43), (0.08, 0.05, 0.08), 'GK_TealDark', r=0.015)
    return join('Sorter')


def incubator_outlet():
    """The incubator's outlet beside it (GR-08, shown once the sorter is bought; origin: the outlet's place): a turquoise box
    and the yellow mouth the chute leaves from (0.95 m)."""
    box((0, 0.82, 0), (0.16, 0.28, 0.24), 'GK_Teal', r=0.05)
    cyl((0, 0.91, 0), 0.06, 0.06, 'GK_TealDark', r=0.015)
    cone((0, 0.975, 0), 0.06, 0.1, 0.06, 'GK_Yellow')
    return join('IncubatorOutlet')


def pipe_gallery():
    """GR-10's pipe gallery on the annex's west wall (origin: its place, x -3.84, z 12.7; facing +X, into the annex): four
    glass risers glowing with the cultures' colours on turquoise brackets, coral valve wheels, gauges, two headers and a
    placard."""
    cores = ('GK_GlowCoral', 'GK_GlowBlue', 'GK_GlowYellow', 'GK_GlowLime')
    for i in range(4):
        z = -1.2 + 0.8 * i
        cyl((0, 1.8, z), 0.055, 3.2, 'GK_Glass', r=0, verts=24)
        cyl((0, 1.8, z), 0.026, 3.16, cores[i], r=0, verts=12)
        for y in (0.9, 2.7):
            box((-0.03, y, z), (0.07, 0.05, 0.14), 'GK_Teal', r=0.02)
        cyl((0.07, 1.35, z), 0.015, 0.1, 'GK_TealDark', r=0.004, rot=(0, 0, 90))
        ring((0.12, 1.35, z), 0.1, 0.018, 'GK_Coral', rot=(0, 0, 90))
        for a in (0, 90):
            box((0.12, 1.35, z), (0.012, 0.18, 0.016), 'GK_Coral', r=0.005, rot=(a, 0, 0))
        cyl((0.07, 2.1, z + 0.12), 0.012, 0.07, 'GK_TealDark', r=0.004)
        cyl((0.09, 2.1, z + 0.18), 0.07, 0.03, 'GK_Teal', r=0.01, rot=(0, 0, 90))
        cyl((0.106, 2.1, z + 0.18), 0.055, 0.005, 'GK_GaugeFace', r=0, rot=(0, 0, 90))
    for y in (0.5, 3.1):
        cyl((0, y, 0), 0.075, 2.9, 'GK_Teal', r=0.03, rot=(90, 0, 0))
    box((0.05, 2.6, 1.55), (0.02, 0.25, 0.5), 'GK_Cream', r=0.02)
    text('LINES', (0.065, 2.6, 1.55), 0.08, 'GK_Ink', euler=(0, -90, 0), extrude=0.004)
    return join('PipeGallery')


def workshop_dome():
    """GR-10's dome light under the workshop's ceiling (origin: its centre, at the 3.6 m ceiling): a glass dome, a turquoise
    ring, a ceiling plate and the warm glow inside."""
    ball((0, 0, 0), (1.2, 0.45, 1.2), 'GK_Glass', seg=40)
    ring((0, -0.01, 0), 1.2, 0.045, 'GK_Teal')
    cyl((0, -0.005, 0), 1.26, 0.02, 'GK_TealDark', r=0)
    ball((0, -0.03, 0), (0.75, 0.14, 0.75), 'GK_GlowWarm', seg=32)
    return join('WorkshopDome')


def lab_panels(a, b, inner, gaps, h=0.95):
    """The back room's rounded turquoise panels along a wall's inside face, leaving its gaps (distances along it from a)."""
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


def lab_walls(walls):
    for a, b, n, gaps, rail_gaps in walls:
        lab_panels(a, b, n, gaps)
        wall_strip(a, b, n, 1.07, 0.07, 0.07, 'GK_Teal', rail_gaps)
        wall_strip(a, b, n, 0.04, 0.08, 0.06, 'GK_TealDark', gaps, r=0.015)


def jar_shelf(x, z, along_x, inner):
    """A wooden shelf on a wall with five jars of glowing culture."""
    n = Vector(inner)
    cores = ('GK_GlowCoral', 'GK_GlowBlue', 'GK_GlowYellow', 'GK_GlowLime', 'GK_GlowTeal')
    off = 0.1 + 0.15
    cx, cz = x + n.x * off, z + n.y * off
    box((cx, 1.6, cz), (1.4, 0.05, 0.3) if along_x else (0.3, 0.05, 1.4), 'GK_Wood', r=0.015)
    for k in range(5):
        t = -0.5 + 0.25 * k
        jx, jz = (cx + t, cz) if along_x else (cx, cz + t)
        cyl((jx, 1.72, jz), 0.06, 0.18, 'GK_Glass', r=0, verts=20)
        cyl((jx, 1.69, jz), 0.048, 0.11, cores[k], r=0.01)
        cyl((jx, 1.82, jz), 0.064, 0.03, 'GK_Teal', r=0.008)


def annex_floor():
    """The lab annex's floor (x -4..4, z 9..17): the back room's glossy tiles."""
    for i in range(16):
        for j in range(16):
            m = 'GK_TileAccent' if (i + j) % 4 == 0 else ('GK_Tile' if (i + j) % 2 else 'GK_Tile2')
            box((-4 + 0.25 + i * 0.5, 0.006 - 0.015, 9 + 0.25 + j * 0.5), (0.49, 0.03, 0.49), m, r=0.012, seg=2)
    # A base under the boards or tiles, so their joints show it rather than the sky behind the hidden greybox floor.
    box((0, -0.034, 13), (8, 0.02, 8), 'GK_Pavement', r=0)
    return join('AnnexFloor')


def annex_dressing():
    """The lab annex (x -4..4, z 9..17, a 3.6 m ceiling; GrowthWorldLab.AnnexRoom): the back room's panels, rail and skirting
    round its walls, clear of the doorway from the back room (x 0.45..1.85), the doorway into the workshop (z 9.6..11), the
    pipe gallery (z 11.25..14.15 on the west wall) and, for the rail, the power board (z 11.9..13.1 on the east wall); frames
    round both doorways; two light strips down the ceiling; a shelf of glowing jars; a plant."""
    back, workshop, gallery, board = (4.45, 5.85), (0.6, 2.0), (2.25, 5.15), (2.9, 4.1)
    lab_walls((((-4, 9), (4, 9), (0, 1), [back], [back]),
               ((-4, 9), (-4, 17), (1, 0), [gallery], [gallery]),
               ((-4, 17), (4, 17), (0, -1), [], []),
               ((4, 9), (4, 17), (-1, 0), [workshop], [workshop, board])))
    door_frame(-4 + (back[0] + back[1]) / 2, 9, True, back[1] - back[0], 2.8, 'GK_Teal', (0, 1))
    door_frame(4, 9 + (workshop[0] + workshop[1]) / 2, False, workshop[1] - workshop[0], 2.8, 'GK_Teal', (-1, 0))
    for x in (-1.5, 1.5):
        box((x, 3.56, 13), (0.5, 0.08, 6.0), 'GK_Cream', r=0.03)
        box((x, 3.515, 13), (0.36, 0.01, 5.8), 'GK_GlowWhite', r=0)
    jar_shelf(-1.6, 17, True, (0, -1))
    potted_plant(-3.45, 16.45, 0.85)
    return join('AnnexDressing')


def workshop_floor():
    """The workshop's floor (x 4..12, z 9..15): big sandy tiles with a turquoise one now and then."""
    for i in range(8):
        for j in range(6):
            m = 'GK_TileAccent' if (i * 3 + j) % 7 == 0 else ('GK_Tile2' if (i + j) % 2 else 'GK_Sand')
            box((4 + 0.5 + i, 0.006 - 0.015, 9 + 0.5 + j), (0.98, 0.03, 0.98), m, r=0.02, seg=2)
    # A base under the boards or tiles, so their joints show it rather than the sky behind the hidden greybox floor.
    box((8, -0.034, 12), (8, 0.02, 6), 'GK_Pavement', r=0)
    return join('WorkshopFloor')


def workshop_dressing():
    """The workshop (x 4..12, z 9..15, a 3.6 m ceiling; GrowthWorldLine.WorkshopRoom): the panels, rail and skirting round
    its walls, clear of the doorway from the annex (z 9.6..11 on its west wall); a frame round it; two light strips either
    side of the dome; a pegboard of tools; a stack of crates in the south-east corner; a plant."""
    workshop = (0.6, 2.0)
    lab_walls((((4, 9), (12, 9), (0, 1), [], []),
               ((12, 9), (12, 15), (-1, 0), [], []),
               ((4, 15), (12, 15), (0, -1), [], []),
               ((4, 9), (4, 15), (1, 0), [workshop], [workshop])))
    door_frame(4, 9 + (workshop[0] + workshop[1]) / 2, False, workshop[1] - workshop[0], 2.8, 'GK_Teal', (1, 0))
    for z in (10.3, 13.7):
        box((8, 3.56, z), (6.0, 0.08, 0.5), 'GK_Cream', r=0.03)
        box((8, 3.515, z), (5.8, 0.01, 0.36), 'GK_GlowWhite', r=0)
    # The pegboard on the west wall (z 12..14), with a wrench, a hammer and a screwdriver.
    px = 4 + 0.1 + 0.02
    box((px, 1.75, 13.0), (0.04, 0.9, 1.6), 'GK_Sand', r=0.015)
    for y in (1.3, 2.2):
        box((px + 0.01, y, 13.0), (0.05, 0.04, 1.64), 'GK_WoodMid', r=0.01)
    box((px + 0.04, 1.85, 12.55), (0.02, 0.36, 0.06), 'GK_Teal', r=0.012, rot=(0, 0, 0))
    ring((px + 0.04, 2.05, 12.55), 0.05, 0.014, 'GK_Teal', rot=(0, 0, 90))
    box((px + 0.04, 1.8, 13.0), (0.02, 0.4, 0.04), 'GK_WoodDark', r=0.01)
    box((px + 0.05, 2.0, 13.0), (0.04, 0.08, 0.18), 'GK_Coral', r=0.02)
    box((px + 0.04, 1.75, 13.45), (0.02, 0.26, 0.05), 'GK_Yellow', r=0.015)
    box((px + 0.04, 1.95, 13.45), (0.02, 0.14, 0.012), 'GK_TealDark', r=0.004)
    # Crates in the south-east corner, clear of the racks (r2 to x 9.5, r3 from z 10.85).
    for (cx, cy, cz, s, m) in ((11.35, 0.22, 9.6, 0.44, 'GK_WoodMid'), (10.85, 0.22, 9.55, 0.42, 'GK_Wood'), (11.3, 0.64, 9.6, 0.4, 'GK_WoodLight')):
        box((cx, cy, cz), (s, s, s), m, r=0.03)
        box((cx, cy, cz + s / 2 + 0.002), (s * 0.8, 0.04, 0.004), 'GK_WoodDark', r=0.002)
    potted_plant(4.5, 14.5, 0.85)
    return join('WorkshopDressing')


WAVE3 = [gen_crank, gen_crank_wheel, lambda: gen_turbine('GenTurbine', 1), lambda: gen_turbine('GenTwin', 2),
         lambda: gen_turbine('GenTide', 3), pump_hand, pump_hand_wheel, pump_motor, buffer_column] + \
        [buffer_tag(n) for n in range(1, 7)] + \
        [power_board, sorter, incubator_outlet, pipe_gallery, workshop_dome, annex_floor, annex_dressing, workshop_floor, workshop_dressing]


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
    # A base under the boards or tiles, so their joints show it rather than the sky behind the hidden greybox floor.
    box((0, -0.034, 0), (8, 0.02, 8), 'GK_WoodDark', r=0)
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
    # A base under the boards or tiles, so their joints show it rather than the sky behind the hidden greybox floor.
    box((-1, -0.034, 6.5), (6, 0.02, 5), 'GK_Pavement', r=0)
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
    annex = (4.45, 5.85)  # along the back room's north wall from x -4: the doorway at x 0.45..1.85 (GrowthWorld.AnnexDoorFrom, AnnexDoorTo)
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
    potted_plant(1.6, 4.55, 0.75)  # the south-east corner, clear of both doorways
    return join('BackRoomDressing')


# ================================================================ wave 4: units B and C, the hall, the boards and the signs (GR-16, GR.45's
# wave 4). Rooms and partitions in world coordinates; the set pieces, boards, banners and tags at their greybox roots.
def shop_walls(walls, rail_gaps=None):
    """The shop's turquoise boards to 0.9 with a rail and skirting along each wall's inside face, broken at its gaps."""
    boards = ['GK_Panel', 'GK_TealLight']
    for k, (a, b, n, gaps) in enumerate(walls):
        wall_boards(a, b, n, 0.9, gaps, boards)
        wall_strip(a, b, n, 0.93, 0.07, 0.07, 'GK_Teal', (rail_gaps or {}).get(k, gaps))
        wall_strip(a, b, n, 0.05, 0.1, 0.06, 'GK_TealDark', gaps, r=0.015)


def arch_frame_east_corner(x0, x1, z, inner, height=2.4):
    """A frame round the arch from unit B into unit C (x 9.9..11.85), whose east side is the room's corner: one post on its
    west side and a head across to the east wall's face (x 11.9)."""
    off = inner * (0.1 + 0.04)
    box((x0 - 0.05, height / 2, z + off), (0.12, height, 0.08), 'GK_Teal', r=0.04)
    box(((x0 - 0.11 + 11.9) / 2, height + 0.06, z + off), (11.9 - (x0 - 0.11), 0.12, 0.08), 'GK_Teal', r=0.04)


def poster(x, y, z, inner_x):
    """A framed poster on an east or west wall: a coral frame, a paper sheet with a teal creature and bubbles."""
    s = -1 if inner_x < 0 else 1
    box((x + s * 0.02, y, z), (0.04, 0.8, 0.62), 'GK_Coral', r=0.03)
    box((x + s * 0.045, y, z), (0.02, 0.7, 0.52), 'GK_Paper', r=0.01)
    ball((x + s * 0.06, y - 0.04, z), (0.02, 0.14, 0.17), 'GK_TealLight', seg=18)
    ball((x + s * 0.075, y, z - s * 0.06), (0.01, 0.04, 0.04), 'GK_Paper', seg=12)
    ball((x + s * 0.08, y, z - s * 0.06), (0.01, 0.018, 0.018), 'GK_Ink', seg=10)
    for k, (dy, dz, r) in enumerate(((0.16, 0.1, 0.03), (0.24, 0.05, 0.022), (0.3, 0.11, 0.016))):
        ball((x + s * 0.06, y + dy, z + s * dz), (0.008, r, r), 'GK_Sky', seg=10)


def wall_clock(x, y, z, inner_x):
    s = -1 if inner_x < 0 else 1
    cyl((x + s * 0.03, y, z), 0.24, 0.06, 'GK_Teal', r=0.025, rot=(0, 0, 90))
    cyl((x + s * 0.06, y, z), 0.19, 0.01, 'GK_Cream', r=0, rot=(0, 0, 90))
    box((x + s * 0.07, y + 0.05, z), (0.01, 0.12, 0.02), 'GK_Ink', r=0.004)
    box((x + s * 0.07, y, z + 0.04), (0.01, 0.02, 0.09), 'GK_Ink', r=0.004)


def unit_b_floor():
    """Unit B's floor (x 4..12, z -4..4): planks a shade lighter than unit A's, in three tones."""
    planks_x(4, 12, -4, 4, 0.006, 0.296, ['GK_WoodLight', 'GK_Sand', 'GK_Wood'])
    box((8, -0.034, 0), (8, 0.02, 8), 'GK_WoodDark', r=0)
    return join('UnitBFloor')


def unit_b_dressing():
    """Unit B (x 4..12, z -4..4, a 3.2 m ceiling; GrowthWorld.Rooms): unit A's boards, rail and skirting round its walls
    (clear of the arch from unit A, the arch into unit C, and for the rail the bay window), chunky beams, frames round both
    arches, two pendant lamps (the game lights them), a striped rug, a poster and a clock on the east wall and a plant in the
    south-east corner, all clear of its places (b1 to b4, and the hall's e1 and e2 along the front wall)."""
    shop_walls((((4, -4), (4, 4), (1, 0), [(2.4, 5.6)]),
                ((4, 4), (12, 4), (0, -1), [(5.9, 7.85)]),
                ((12, -4), (12, 4), (-1, 0), []),
                ((4, -4), (12, -4), (0, 1), [])), rail_gaps={3: [(3.0, 5.0)]})
    for z in (-3.0, -1.0, 1.0, 3.0):
        box((8, 3.1, z), (7.8, 0.2, 0.22), 'GK_Beam', r=0.05)
    door_frame(4, 0, False, 3.2, 2.4, 'GK_Teal', (1, 0))
    arch_frame_east_corner(9.9, 11.85, 4, -1)
    pendant(6.0, 2.5, 2.0, 3.2)
    pendant(9.8, 2.5, -2.0, 3.2)
    for w, d, y, m in ((3.0, 1.3, 0.004, 'GK_Coral'), (2.8, 1.1, 0.008, 'GK_Cream'), (2.5, 0.8, 0.012, 'GK_Pink')):
        box((8.0, y, 1.55), (w, 0.008, d), m, r=0)
    for k in range(5):
        box((6.95 + k * 0.52, 0.016, 1.55), (0.12, 0.008, 0.6), 'GK_Yellow', r=0)
    poster(11.9, 2.15, -2.75, -1)
    wall_clock(11.9, 2.35, 2.65, -1)
    potted_plant(11.65, -3.6, 0.8)
    return join('UnitBDressing')


def street_east():
    """Outside unit B (x 6..16, south of z -4): the pavement and the fence go on past unit A's, with a lamp post, two bushes
    and a tree, so the bay window looks out on the street too."""
    box((11, -0.03, -5.4), (10, 0.06, 2.6), 'GK_Pavement', r=0.01)
    for k in range(6):
        box((7.5 + k * 1.5, 0.45, -7.0), (0.12, 0.9, 0.12), 'GK_WoodLight', r=0.04)
    for y in (0.62, 0.32):
        box((10.58, y, -7.0), (8.96, 0.1, 0.06), 'GK_WoodLight', r=0.03)
    cyl((10.4, 1.35, -6.4), 0.05, 2.7, 'GK_TealDark', r=0.02)
    cyl((10.4, 2.72, -6.4), 0.16, 0.05, 'GK_Teal', r=0.02)
    ball((10.4, 2.86, -6.4), (0.15, 0.17, 0.15), 'GK_GlowWarm', seg=20)
    cyl((10.4, 3.04, -6.4), 0.19, 0.06, 'GK_Teal', r=0.025)
    cyl((10.4, 0.04, -6.4), 0.14, 0.08, 'GK_TealDark', r=0.02)
    for x, z, r, m in ((8.2, -7.8, 0.55, 'GK_Lime'), (14.4, -7.9, 0.62, 'GK_Green')):
        ball((x, r * 0.5, z), (r, r * 0.8, r * 0.9), m, seg=20)
    cyl((12.6, 0.9 * 1.2, -10.5), 0.16 * 1.2, 1.8 * 1.2, 'GK_Beam', r=0.05)
    for dx, dz, dy, r, m in ((0, 0, 2.4, 1.15, 'GK_Green'), (-0.7, 0.3, 1.9, 0.8, 'GK_Lime'), (0.7, -0.2, 2.0, 0.85, 'GK_GreenDark'), (0.1, -0.5, 2.9, 0.7, 'GK_Lime')):
        ball((12.6 + dx * 1.2, dy * 1.2, -10.5 + dz * 1.2), r * 1.2, m, seg=20)
    return join('StreetEast')


def bay_window():
    """GR-10's bay window bench in unit B's front wall (origin: the bench's place, x 8, z -3.675; front +Z, into the shop;
    the wall's inside face at local z -0.225): the window's frame (shallow at its head, so the hall's banner hangs clear in
    front of it), mullions and sill; a wooden window seat with three drawers, a coral cushion, two pillows and a book; and two
    glass bowls with water, pebbles and a plant hanging either side of the banner."""
    wz = -0.225
    box((0, 2.46, wz + 0.0275), (2.04, 0.1, 0.055), 'GK_Teal', r=0.025)
    for s in (-1, 1):
        box((s * 0.96, 1.65, wz + 0.035), (0.12, 1.62, 0.07), 'GK_Teal', r=0.03)
    box((0, 1.65, wz - 0.05), (0.05, 1.5, 0.04), 'GK_TealDark', r=0.015)
    box((0, 1.95, wz - 0.05), (1.8, 0.04, 0.04), 'GK_TealDark', r=0.015)
    box((0, 0.875, wz + 0.09), (1.98, 0.05, 0.18), 'GK_Teal', r=0.02)
    # The window seat.
    box((0, 0.03, 0), (1.76, 0.06, 0.42), 'GK_TealDark', r=0.02)
    box((0, 0.23, 0), (1.8, 0.34, 0.45), 'GK_Cream', r=0.05)
    for k in range(3):
        x = -0.58 + k * 0.58
        box((x, 0.23, 0.228), (0.5, 0.24, 0.02), 'GK_Teal', r=0.02)
        ball((x, 0.25, 0.245), 0.025, 'GK_Yellow', seg=14)
    box((0, 0.425, 0), (1.84, 0.05, 0.47), 'GK_Wood', r=0.015)
    box((0, 0.49, 0.01), (1.72, 0.08, 0.4), 'GK_Coral', r=0.035)
    for s, m in ((-1, 'GK_Yellow'), (1, 'GK_TealLight')):
        box((s * 0.6, 0.64, -0.12), (0.34, 0.24, 0.1), m, r=0.045, rot=(-14, 0, s * 6))
    box((0.18, 0.545, 0.06), (0.2, 0.03, 0.14), 'GK_Lilac', r=0.008, rot=(0, 12, 0))
    box((0.18, 0.565, 0.06), (0.18, 0.012, 0.12), 'GK_Paper', r=0.003, rot=(0, 12, 0))
    # The hanging bowls.
    for s in (-1, 1):
        x, z, y = s * 0.62, 0.12, 2.25
        cyl((x, 3.185, z), 0.04, 0.03, 'GK_Teal', r=0.01)
        cyl((x, (3.17 + 2.55) / 2, z), 0.005, 3.17 - 2.55, 'GK_Ink', r=0)
        for a in (0, 120, 240):
            ra = math.radians(a)
            tube([(x, 2.55, z), (x + 0.15 * math.cos(ra), 2.31, z + 0.15 * math.sin(ra))], 0.004, 'GK_Ink', sides=6)
        ball((x, 2.55, z), 0.02, 'GK_Wood', seg=10)
        ring((x, 2.31, z), 0.15, 0.012, 'GK_Teal')
        ball((x, y, z), (0.17, 0.15, 0.17), 'GK_Glass', seg=28)
        ball((x, y - 0.035, z), (0.145, 0.09, 0.145), 'GK_Pebble4', seg=24)
        for k, m in enumerate(('GK_Pebble1', 'GK_Pebble3', 'GK_Pebble2')):
            ball((x - 0.05 + k * 0.05, y - 0.11, z + (k % 2) * 0.03), (0.025, 0.014, 0.022), m, seg=10)
        ball((x + s * 0.02, y - 0.02, z - 0.02), (0.03, 0.08, 0.025), 'GK_Green', seg=14, rot=(0, 0, s * 10))
        ball((x - s * 0.03, y - 0.04, z + 0.02), (0.025, 0.06, 0.02), 'GK_Lime', seg=14, rot=(0, 0, -s * 14))
    return join('BayWindow')


def window_display():
    """GR-10's window display on unit A's sill (origin: its place, x -2.6, y 0.9, z -3.93; front +Z, the shop's side): a
    turquoise base, a light hood over the greybox's water and glass (they stay), pebbles, plants and a rock arch inside, and a
    price card."""
    box((0, 0.03, 0), (0.72, 0.06, 0.18), 'GK_Teal', r=0.02)
    box((0, 0.065, 0.085), (0.6, 0.02, 0.01), 'GK_Yellow', r=0.004)
    box((0, 0.46, 0), (0.66, 0.04, 0.16), 'GK_Cream', r=0.015)
    box((0, 0.438, 0), (0.58, 0.004, 0.1), 'GK_GlowWarm', r=0)
    pebbles = ['GK_Sand', 'GK_Pebble1', 'GK_Pebble2', 'GK_Pebble3', 'GK_Pebble4']
    for k in range(26):
        ball((-0.27 + (k % 13) * 0.045, 0.1, -0.035 + (k // 13) * 0.07), (0.02, 0.011, 0.017), pebbles[k % 5], seg=10)
    water_plants(0.1, ((-0.22, -0.02, 0.24, 'GK_Green'), (-0.17, 0.01, 0.17, 'GK_Lime'), (0.2, -0.02, 0.2, 'GK_GreenDark')))
    ring((0.04, 0.1, -0.01), 0.07, 0.022, 'GK_Rock', rot=(90, 0, 0))
    box((0.42, 0.05, 0.05), (0.1, 0.07, 0.006), 'GK_Paper', r=0.004, rot=(-18, 0, 0))
    box((0.42, 0.055, 0.054), (0.06, 0.012, 0.002), 'GK_Coral', r=0.002, rot=(-18, 0, 0))
    return join('WindowDisplay')


def unit_c_floor():
    """Unit C's floor (x 2..12, z 4..9): a basketweave of wood blocks, a metre square each, four planks turned each way."""
    woods = ['GK_WoodLight', 'GK_Wood', 'GK_WoodMid']
    for i in range(10):
        for j in range(5):
            x0, z0 = 2 + i, 4 + j
            for k in range(4):
                c = 0.125 + 0.25 * k
                m = woods[(i * 3 + j * 5 + k) % 3]
                if (i + j) % 2 == 0:
                    box((x0 + 0.5, 0.006 - 0.015, z0 + c), (0.988, 0.03, 0.238), m, r=0.008, seg=2)
                else:
                    box((x0 + c, 0.006 - 0.015, z0 + 0.5), (0.238, 0.03, 0.988), m, r=0.008, seg=2)
    box((7, -0.034, 6.5), (10, 0.02, 5), 'GK_WoodDark', r=0)
    return join('UnitCFloor')


def unit_c_dressing():
    """Unit C (x 2..12, z 4..9, a 3.2 m ceiling; GrowthWorldSpaces.UnitCRoom): the boards, rail and skirting round its walls
    (the north wall's only east of the feature wall, x 9.8..12; clear of the arch from unit B), beams, a frame round the arch,
    two pendant lamps (the game lights them) and a rug along its aisle, clear of its places (c1 to c6)."""
    shop_walls((((2, 4), (12, 4), (0, 1), [(7.9, 9.85)]),
                ((2, 4), (2, 9), (1, 0), []),
                ((12, 4), (12, 9), (-1, 0), []),
                ((2, 9), (12, 9), (0, -1), [(0, 7.8)])))
    for z in (5.25, 7.75):
        box((7, 3.1, z), (9.8, 0.2, 0.22), 'GK_Beam', r=0.05)
    arch_frame_east_corner(9.9, 11.85, 4, 1)
    pendant(5.0, 2.5, 6.5, 3.2)
    pendant(9.0, 2.5, 6.5, 3.2)
    for w, d, y, m in ((4.6, 1.2, 0.004, 'GK_Teal'), (4.4, 1.0, 0.008, 'GK_Cream'), (4.1, 0.7, 0.012, 'GK_TealLight')):
        box((6.5, y, 6.25), (w, 0.008, d), m, r=0)
    for k in range(7):
        ball((4.7 + k * 0.6, 0.012, 6.25), (0.08, 0.006, 0.08), 'GK_Yellow', seg=14)
    return join('UnitCDressing')


def feature_wall():
    """GR-10's feature wall along unit C's north wall (x 2.2..9.8, on its inside face at z 8.9; 4 cm deep at most, since the
    Show tanks' backs reach the wall): a light turquoise lower wall with a wavy top, the deeper teal above it with painted
    bubbles, starfish and shells, and a cove of light along its top."""
    zf = 8.9
    box((6, 1.6, zf - 0.01), (7.6, 3.2, 0.02), 'GK_Teal', r=0)
    box((6, 1.07, zf - 0.024), (7.6, 2.14, 0.004), 'GK_TealLight', r=0)
    for k in range(19):
        x = 2.4 + k * 0.4
        cyl((x, 2.14, zf - 0.024), 0.21, 0.004, 'GK_TealLight', r=0, rot=(90, 0, 0), verts=24)
    for k in range(18):
        x = 2.6 + k * 0.4
        cyl((x, 2.3, zf - 0.028), 0.12, 0.004, 'GK_Teal', r=0, rot=(90, 0, 0), verts=24)
    box((6, 0.42, zf - 0.028), (7.6, 0.06, 0.004), 'GK_Panel', r=0)
    rng = random.Random(41)
    for k in range(34):
        x, y, r = rng.uniform(2.35, 9.65), rng.uniform(2.55, 3.0), rng.choice((0.025, 0.035, 0.05, 0.07))
        cyl((x, y, zf - 0.032), r, 0.004, 'GK_Paper', r=0, rot=(90, 0, 0), verts=16)
        cyl((x, y, zf - 0.036), r * 0.7, 0.004, 'GK_Pebble4', r=0, rot=(90, 0, 0), verts=16)
    for cx, cy, s, m in ((2.75, 2.75, 0.2, 'GK_Coral'), (9.3, 2.7, 0.24, 'GK_Yellow'), (5.98, 2.78, 0.16, 'GK_Pink')):
        for a in range(0, 360, 72):
            box((cx, cy, zf - 0.04), (0.06, s, 0.006), m, r=0.004, rot=(0, 0, a))
        cyl((cx, cy, zf - 0.044), 0.05, 0.006, m, r=0, rot=(90, 0, 0), verts=16)
    box((6, 3.12, zf - 0.05), (7.6, 0.06, 0.1), 'GK_Cream', r=0.02)
    box((6, 3.088, zf - 0.04), (7.5, 0.004, 0.06), 'GK_GlowWhite', r=0)
    return join('FeatureWall')


def feature_tank():
    """GR-10's built-in feature tank between unit C's two Show tank places (origin: x 5.975, z 8.75; front -Z, into the
    shop): a cream cabinet with turquoise doors, a turquoise bezel round the window (the greybox's water and glass stay in
    it), a light hood with gold trim, and inside, a painted sea at the back, pebbles, plants, a rock arch, coral and bubbles."""
    box((0, 0.05, -0.005), (1.26, 0.1, 0.28), 'GK_TealDark', r=0.03)
    box((0, 0.36, 0), (1.3, 0.52, 0.3), 'GK_Cream', r=0.05)
    for s in (-1, 1):
        box((s * 0.3, 0.36, -0.155), (0.52, 0.36, 0.02), 'GK_Teal', r=0.025)
        ball((s * 0.08, 0.38, -0.17), 0.025, 'GK_Yellow', seg=14)
    box((0, 0.625, -0.02), (1.3, 0.05, 0.34), 'GK_Gold', r=0.01)
    # The bezel round the window (x -0.525..0.525, y 0.65..1.85), its front just in front of the glass.
    for s in (-1, 1):
        box((s * 0.585, 1.25, -0.02), (0.13, 1.3, 0.34), 'GK_Teal', r=0.03)
    box((0, 0.67, -0.02), (1.3, 0.06, 0.34), 'GK_Teal', r=0.02)
    box((0, 1.89, -0.02), (1.3, 0.08, 0.34), 'GK_Teal', r=0.02)
    box((0, 2.0, -0.02), (1.34, 0.16, 0.34), 'GK_Cream', r=0.04)
    box((0, 1.925, -0.18), (1.2, 0.012, 0.02), 'GK_Gold', r=0.004)
    box((0, 1.916, -0.06), (1.0, 0.004, 0.18), 'GK_GlowWarm', r=0)
    ball((0, 2.12, -0.12), (0.1, 0.07, 0.03), 'GK_Gold', seg=18)
    # Inside, behind the greybox's water (z -0.16..-0.04): the back painted as the sea, the scene standing in front of it.
    box((0, 1.25, 0.1), (1.05, 1.2, 0.02), 'GK_Sky', r=0)
    for k in range(6):
        cyl((-0.45 + k * 0.18, 1.55 + 0.08 * (k % 2), 0.088), 0.06, 0.004, 'GK_Pebble4', r=0, rot=(90, 0, 0), verts=16)
    box((0, 0.69, -0.03), (1.05, 0.04, 0.26), 'GK_Sand', r=0.01)
    pebbles = ['GK_Pebble1', 'GK_Pebble2', 'GK_Pebble3', 'GK_Pebble4']
    for k in range(30):
        ball((-0.48 + (k % 15) * 0.068, 0.72, -0.12 + (k // 15) * 0.09), (0.024, 0.013, 0.02), pebbles[k % 4], seg=10)
    water_plants(0.72, ((-0.42, 0.02, 0.62, 'GK_Green'), (-0.33, 0.05, 0.44, 'GK_Lime'), (0.4, 0.03, 0.56, 'GK_GreenDark'), (0.32, 0.06, 0.36, 'GK_Lime')))
    ring((0.05, 0.72, 0.03), 0.16, 0.05, 'GK_Rock', rot=(90, 0, 0))
    for k, (cx, a, h, m) in enumerate(((-0.12, -16, 0.2, 'GK_Coral'), (-0.07, 12, 0.26, 'GK_Pink'), (0.2, 22, 0.18, 'GK_Coral'))):
        cyl((cx, 0.72 + h / 2, 0.05), 0.02, h, m, r=0.008, rot=(0, 0, a), verts=12)
        ball((cx - math.sin(math.radians(a)) * h / 2, 0.72 + h / 2 + math.cos(math.radians(a)) * h / 2, 0.05), 0.03, m, seg=12)
    for k in range(7):
        ball((0.28 + 0.015 * (k % 2), 0.85 + k * 0.12, 0.0), 0.012 + 0.004 * (k % 3), 'GK_Paper', seg=10)
    return join('FeatureTank')


def hoarding(name, a0, a1, line, along_x, height, label):
    """A room's partition as a painted hoarding, on its doorway's line from a0 to a1 (0.12 thick, the greybox's), its face to
    the open side (-Z or -X): upright planks in three pale tones, a coral oval painted COMING SOON, the room's name under
    it, and yellow and teal stripes along its foot."""
    width = a1 - a0
    n = max(4, int(round(width / 0.22)))
    w = width / n
    tones = ('GK_Cream', 'GK_Sand', 'GK_WoodLight')
    for k in range(n):
        c = a0 + w * (k + 0.5)
        if along_x: box((c, height / 2, line), (w - 0.012, height, 0.12), tones[k % 3], r=0.012, seg=2)
        else: box((line, height / 2, c), (0.12, height, w - 0.012), tones[k % 3], r=0.012, seg=2)
    mid = (a0 + a1) / 2
    f = line - 0.062
    rx, ry = min(0.75, width * 0.38), min(0.42, width * 0.22)
    y = min(1.55, height * 0.6)
    if along_x:
        cyl((mid, y, f), rx, 0.006, 'GK_Coral', r=0, rot=(90, 0, 0), rz=ry, verts=40)
        cyl((mid, y, f - 0.004), rx * 0.92, 0.006, 'GK_Coral', r=0, rot=(90, 0, 0), rz=ry * 0.86, verts=40)
        text('COMING', (mid, y + ry * 0.3, f - 0.01), ry * 0.42, 'GK_Cream', extrude=0.006, res=3)
        text('SOON', (mid, y - ry * 0.3, f - 0.01), ry * 0.42, 'GK_Cream', extrude=0.006, res=3)
        text(label, (mid, y - ry - 0.18, f - 0.004), 0.13, 'GK_Ink', extrude=0.005, res=3)
        for k in range(int(width / 0.16)):
            box((a0 + 0.08 + k * 0.16, 0.2, f), (0.1, 0.16, 0.006), ('GK_Yellow', 'GK_Teal')[k % 2], r=0.004, rot=(0, 0, 30))
    else:
        cyl((f, y, mid), ry, 0.006, 'GK_Coral', r=0, rot=(0, 0, 90), rz=rx, verts=40)
        text('COMING', (f - 0.01, y + ry * 0.3, mid), ry * 0.42, 'GK_Cream', euler=(0, 90, 0), extrude=0.006, res=3)
        text('SOON', (f - 0.01, y - ry * 0.3, mid), ry * 0.42, 'GK_Cream', euler=(0, 90, 0), extrude=0.006, res=3)
        text(label, (f - 0.004, y - ry - 0.18, mid), 0.13, 'GK_Ink', euler=(0, 90, 0), extrude=0.005, res=3)
        for k in range(int(width / 0.16)):
            box((f, 0.2, a1 - 0.08 - k * 0.16), (0.006, 0.16, 0.1), ('GK_Yellow', 'GK_Teal')[k % 2], r=0.004, rot=(30, 0, 0))
    return join(name)


def banner(name, cloth, trim):
    """A hall banner (origin: its rod, on the ceiling's line; it unrolls downward as the game scales it): a gold rod with
    finials, the cloth with a trim band and a pointed foot with a tassel, and a cream disc with a gold star on both faces."""
    cyl((0, 0, 0), 0.025, 1.0, 'GK_Gold', r=0.008, rot=(0, 0, 90))
    for s in (-1, 1):
        ball((s * 0.52, 0, 0), 0.04, 'GK_Gold', seg=14)
    box((0, -0.47, 0), (0.8, 0.9, 0.03), cloth, r=0.012)
    box((0, -0.92, 0), (0.4, 0.4, 0.03), cloth, r=0.012, rot=(0, 0, 45))
    box((0, -0.09, 0), (0.82, 0.08, 0.036), trim, r=0.012)
    box((0, -0.86, 0), (0.82, 0.04, 0.036), trim, r=0.01)
    ball((0, -1.22, 0), 0.035, 'GK_Gold', seg=12)
    cyl((0, -0.45, 0), 0.19, 0.044, 'GK_Cream', r=0.01, rot=(90, 0, 0), verts=32)
    for side in (-1, 1):
        for a in range(0, 360, 72):
            box((0, -0.45, side * 0.025), (0.05, 0.24, 0.006), 'GK_Gold', r=0.006, rot=(0, 0, a))
    return join(name)


def hall_spot():
    """A hall spotlight's fixture (origin: the game's spotlight, 0.15 under the 3.2 m ceiling): a ceiling plate, a turquoise
    can, a gold ring and its warm lens."""
    cyl((0, 0.135, 0), 0.09, 0.03, 'GK_TealDark', r=0.008)
    cyl((0, 0.07, 0), 0.075, 0.11, 'GK_Teal', r=0.02)
    ring((0, 0.012, 0), 0.075, 0.012, 'GK_Gold')
    cyl((0, 0.008, 0), 0.06, 0.006, 'GK_GlowWarm', r=0)
    return join('HallSpot')


def exhibition_sign():
    """GR-11's sign over the shop door, inside (origin: its centre, 2.78 m up; front +Z, the shop's side): a turquoise
    board and frame round the greybox's gold face (it stays: pale while prepared, deep and lit on the day), GRAND
    EXHIBITION in ink letters and a coral rosette."""
    box((0, 0, -0.02), (1.5, 0.42, 0.04), 'GK_TealDark', r=0.02)
    for s in (-1, 1):
        box((0, s * 0.18, 0.02), (1.5, 0.06, 0.06), 'GK_Teal', r=0.02)
        box((s * 0.72, 0, 0.02), (0.06, 0.42, 0.06), 'GK_Teal', r=0.02)
    text('GRAND EXHIBITION', (0.08, -0.005, 0.038), 0.085, 'GK_Ink', euler=(0, 180, 0), extrude=0.008, res=3)
    cyl((-0.56, 0, 0.04), 0.085, 0.014, 'GK_Coral', r=0.004, rot=(90, 0, 0), verts=24)
    cyl((-0.56, 0, 0.049), 0.05, 0.008, 'GK_Gold', r=0.002, rot=(90, 0, 0), verts=24)
    for s in (-1, 1):
        box((-0.56 + s * 0.035, -0.12, 0.036), (0.04, 0.12, 0.006), 'GK_Coral', r=0.004, rot=(0, 0, s * 12))
    return join('ExhibitionSign')


def award():
    """GR-11's award, on unit A's front wall east of the door (origin: its centre; front +Z, the shop's side): a wooden
    plaque with a gold rim, a gold medal with a star and its coral ribbon, and a cream card engraved GRAND EXHIBITION."""
    box((0, 0, -0.005), (0.6, 0.75, 0.05), 'GK_WoodDark', r=0.04)
    box((0, 0, 0.02), (0.52, 0.67, 0.012), 'GK_Wood', r=0.02)
    for s in (-1, 1):
        box((0, s * 0.345, 0.024), (0.54, 0.012, 0.006), 'GK_Gold', r=0.003)
        box((s * 0.27, 0, 0.024), (0.012, 0.69, 0.006), 'GK_Gold', r=0.003)
    for s in (-1, 1):
        box((s * 0.05, 0.27, 0.03), (0.07, 0.16, 0.006), 'GK_Coral', r=0.004, rot=(0, 0, -s * 14))
    cyl((0, 0.1, 0.035), 0.12, 0.016, 'GK_Gold', r=0.005, rot=(90, 0, 0), verts=32)
    ring((0, 0.1, 0.045), 0.1, 0.008, 'GK_Yellow', rot=(90, 0, 0))
    for a in range(0, 360, 72):
        box((0, 0.1, 0.046), (0.025, 0.11, 0.006), 'GK_Yellow', r=0.004, rot=(0, 0, a))
    box((0, -0.2, 0.03), (0.42, 0.16, 0.01), 'GK_Cream', r=0.01)
    text('GRAND', (0, -0.165, 0.037), 0.05, 'GK_Ink', euler=(0, 180, 0), extrude=0.003, res=3)
    text('EXHIBITION', (0, -0.23, 0.037), 0.05, 'GK_Ink', euler=(0, 180, 0), extrude=0.003, res=3)
    return join('Award')


def display_tag():
    """GR-11's tag over a tank on display (origin: its place, 2.2 m up, 0.2 in front of the tank's centre; front +Z): a coral
    ribbon with notched ends, ON SHOW in cream letters and a gold rosette at its middle."""
    box((0, 0, 0), (0.5, 0.15, 0.02), 'GK_Coral', r=0.02)
    for s in (-1, 1):
        box((s * 0.29, -0.015, -0.004), (0.12, 0.12, 0.016), 'GK_Coral', r=0.01, rot=(0, 0, s * 8))
        box((s * 0.36, -0.015, -0.004), (0.06, 0.06, 0.016), 'GK_Coral', r=0.01, rot=(0, 0, 45))
    text('ON SHOW', (0, 0, 0.012), 0.06, 'GK_Cream', euler=(0, 180, 0), extrude=0.004, res=3)
    cyl((0, 0.13, 0.012), 0.07, 0.016, 'GK_Gold', r=0.004, rot=(90, 0, 0), verts=24)
    for a in range(0, 360, 72):
        box((0, 0.13, 0.022), (0.016, 0.07, 0.004), 'GK_Yellow', r=0.003, rot=(0, 0, a))
    return join('DisplayTag')


def staff_board():
    """GR-09's staff board on unit A's north wall (origin: its centre, 1.6 m up; front -Z, the shop's side): a cork board in
    a wooden frame, STAFF on a turquoise plaque, and a card pinned up for each helper (Pip, Nori, Moss) with their colour,
    a round portrait and their name."""
    box((0, 0, 0.005), (0.94, 0.64, 0.04), 'GK_Sand', r=0.01)
    for sy in (-1, 1):
        box((0, sy * 0.34, 0), (1.04, 0.06, 0.07), 'GK_WoodMid', r=0.02)
    for sx in (-1, 1):
        box((sx * 0.49, 0, 0), (0.06, 0.74, 0.07), 'GK_WoodMid', r=0.02)
    box((0, 0.25, -0.03), (0.4, 0.1, 0.02), 'GK_Teal', r=0.02)
    text('STAFF', (0, 0.25, -0.042), 0.07, 'GK_Cream', extrude=0.005, res=3)
    for i, (who, col, cap) in enumerate((('PIP', 'GK_Pink', 'GK_TealDark'), ('NORI', 'GK_Green', 'GK_TealDark'), ('MOSS', 'GK_Paper', 'GK_Ink'))):
        x = -0.3 + 0.3 * i
        a = (-3, 2, -2)[i]
        box((x, -0.06, -0.022), (0.22, 0.3, 0.006), 'GK_Paper', r=0.008, rot=(0, 0, a))
        box((x, 0.06, -0.027), (0.2, 0.05, 0.004), col, r=0.004, rot=(0, 0, a))
        cyl((x, -0.04, -0.028), 0.055, 0.004, col if col != 'GK_Paper' else 'GK_Cream', r=0, rot=(90, 0, 0), verts=24)
        cyl((x, -0.035, -0.031), 0.035, 0.004, 'GK_Pebble1', r=0, rot=(90, 0, 0), verts=20)
        box((x, 0.0, -0.032), (0.07, 0.016, 0.004), cap, r=0.003)
        text(who, (x, -0.15, -0.031), 0.04, 'GK_Ink', extrude=0.003, res=3)
        ball((x, 0.08, -0.035), 0.016, ('GK_Coral', 'GK_Yellow', 'GK_Teal')[i], seg=12)
    return join('StaffBoard')


def hall_board():
    """GR-10's exhibition hall plans in unit C's north-east corner (origin: its centre, 1.6 m up; front +Z in its own frame,
    the room's side; the wall at local z -0.04): a wooden frame round a blueprint: the shop's units drawn in white lines, the
    banners as coral flags, the spotlit places as gold stars, and HALL PLANS across its top."""
    box((0, 0, -0.01), (0.84, 0.64, 0.04), 'GK_Ink', r=0.01)
    for sy in (-1, 1):
        box((0, sy * 0.34, 0), (0.94, 0.06, 0.07), 'GK_WoodMid', r=0.02)
    for sx in (-1, 1):
        box((sx * 0.44, 0, 0), (0.06, 0.74, 0.07), 'GK_WoodMid', r=0.02)
    text('HALL PLANS', (0, 0.24, 0.016), 0.06, 'GK_Paper', euler=(0, 180, 0), extrude=0.003, res=3)
    # The plan (the units seen from above, the street at the bottom): unit A and unit B side by side, unit C behind them.
    def line(x0, y0, x1, y1):
        if abs(y1 - y0) < 1e-6: box(((x0 + x1) / 2, y0, 0.013), (abs(x1 - x0) + 0.008, 0.008, 0.004), 'GK_Paper', r=0)
        else: box((x0, (y0 + y1) / 2, 0.013), (0.008, abs(y1 - y0) + 0.008, 0.004), 'GK_Paper', r=0)
    # Local x runs right to left as the room sees it (front +Z), so the plan's east is local -x.
    for (x0, y0, x1, y1) in ((0.3, -0.24, -0.3, -0.24), (0.3, 0.16, -0.3, 0.16), (0.3, -0.24, 0.3, 0.16), (-0.3, -0.24, -0.3, 0.16),
                             (0.0, -0.24, 0.0, -0.04), (0.3, -0.04, -0.3, -0.04)):
        line(x0, y0, x1, y1)
    for (x, y) in ((0.15, -0.14), (-0.15, -0.14), (0.15, 0.06), (-0.15, 0.06), (-0.22, -0.19)):
        for a in range(0, 360, 72):
            box((x, y, 0.016), (0.008, 0.036, 0.003), 'GK_Gold', r=0.002, rot=(0, 0, a))
    for (x, y) in ((0.24, 0.12), (-0.08, 0.12), (-0.24, -0.08)):
        box((x, y, 0.016), (0.03, 0.04, 0.003), 'GK_Coral', r=0.003)
    ball((0.36, -0.27, 0.03), 0.02, 'GK_Coral', seg=12)
    ball((-0.36, 0.27, 0.03), 0.02, 'GK_Yellow', seg=12)
    return join('HallBoard')


WAVE4 = [unit_b_floor, unit_b_dressing, street_east, bay_window, window_display, unit_c_floor, unit_c_dressing, feature_wall,
         feature_tank,
         lambda: hoarding('PartitionB', -1.6, 1.6, 4.0, False, 2.4, 'UNIT B'),
         lambda: hoarding('PartitionC', 9.9, 11.85, 4.0, True, 2.4, 'UNIT C'),
         lambda: hoarding('PartitionAnnex', 0.45, 1.85, 9.0, True, 2.8, 'LAB ANNEX'),
         lambda: hoarding('PartitionWorkshop', 9.6, 11.0, 4.0, False, 2.8, 'WORKSHOP'),
         lambda: banner('BannerCoral', 'GK_Coral', 'GK_Yellow'), lambda: banner('BannerYellow', 'GK_Yellow', 'GK_Teal'),
         lambda: banner('BannerBlue', 'GK_Sky', 'GK_Coral'), hall_spot, exhibition_sign, award, display_tag, staff_board, hall_board]


# ================================================================ GR-19: decor (GR.60, GR.61): nine pieces, three themes. Each stands
# on a shop tank's top at its back corner (GrowthWorldDecor.cs), so its origin is the middle of its base, its front +Z as the
# tank's, and it stays within about 0.36 m across and 0.52 m tall. Riverbank suits fresh-water creatures, Hot springs warm-water
# ones and Seaside brine ones (GrowthContent.Decor).

DECOR_BASE = 0.035


def smooth(points, steps=5):
    """Catmull-Rom points through the given ones (the decor's curling stems), for tube()."""
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(steps):
            t = s / steps
            out.append(tuple(0.5 * (2 * p1[k] + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t * t +
                                    (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t * t * t) for k in range(3)))
    out.append(tuple(points[-1]))
    return out


def decor_base(mat):
    """The round base every piece stands on, in its theme's ground: grass, rock or sand."""
    cyl((0, DECOR_BASE / 2, 0), 0.17, DECOR_BASE, mat, r=0.014)


def decor_reeds():
    """Riverbank: a clump of reeds, three with cattails, by a little puddle, and a dragonfly resting on the second tallest."""
    decor_base('GK_Grass')
    cyl((0.07, DECOR_BASE + 0.003, 0.07), 0.065, 0.006, 'GK_GlowTeal', r=0, rz=0.045)
    reeds = ((-0.06, -0.05, 0.42, 'GK_Green', 6), (0.0, -0.08, 0.36, 'GK_GreenDark', -5), (-0.11, 0.02, 0.3, 'GK_Lime', 11),
             (0.05, -0.02, 0.28, 'GK_Green', -10), (-0.02, 0.05, 0.22, 'GK_Lime', 4), (0.09, -0.07, 0.2, 'GK_GreenDark', -14))
    tips = []
    for k, (x, z, h, m, lean) in enumerate(reeds):
        tx = x + math.sin(math.radians(lean)) * h
        tip = (tx, DECOR_BASE + h, z)
        tube([(x, DECOR_BASE - 0.005, z), ((x + tx) / 2 + 0.004, DECOR_BASE + h * 0.5, z), tip], 0.012, m)
        tips.append(tip)
        if k < 3:
            head = (x + math.sin(math.radians(lean)) * (h - 0.07), DECOR_BASE + h - 0.07, z)
            ball(head, (0.026, 0.062, 0.026), 'GK_Beam', seg=16, rot=(0, 0, -lean))
    for k in range(4):
        a = math.radians(30 + 90 * k)
        ball((0.04 * math.cos(a), DECOR_BASE + 0.012, 0.04 * math.sin(a) - 0.01), (0.03, 0.012, 0.03), 'GK_GreenDark', seg=14)
    # The dragonfly on reed 1, just under its tip: a long blue body, a head, four glassy wings.
    x, y, z = tips[1][0] + 0.005, tips[1][1] - 0.03, tips[1][2] + 0.025
    tube([(x - 0.06, y, z), (x + 0.05, y + 0.004, z)], 0.011, 'GK_GlowBlue')
    ball((x + 0.06, y + 0.005, z), 0.018, 'GK_GlowBlue', seg=14)
    for sx in (-1, 1):
        for dx, ang in ((0.02, 12), (-0.005, -12)):
            ball((x + dx, y + 0.014, z + sx * 0.05), (0.016, 0.004, 0.05), 'GK_Glass', seg=14, rot=(0, sx * ang, 0))
    return join('DecorReeds')


def decor_lily_lamp():
    """Riverbank: a lily pad with a curling stem holding up a glowing water-lily lamp, a frog sitting on the pad."""
    decor_base('GK_Grass')
    cyl((0, DECOR_BASE + 0.01, 0.02), 0.155, 0.02, 'GK_Green', r=0.008, rz=0.135)
    ring((0, DECOR_BASE + 0.02, 0.02), 0.14, 0.006, 'GK_GreenDark', sz=0.125)
    for k in range(5):
        a = math.radians(200 + k * 30)
        tube([(0.02, DECOR_BASE + 0.021, 0.02), (0.02 + 0.12 * math.cos(a), DECOR_BASE + 0.021, 0.02 + 0.11 * math.sin(a))], 0.003, 'GK_Lime')
    c = (-0.03, DECOR_BASE + 0.37, 0.0)
    tube(smooth([(0.04, DECOR_BASE + 0.015, -0.04), (0.08, DECOR_BASE + 0.14, -0.06), (0.04, DECOR_BASE + 0.27, -0.04), (c[0], c[1] - 0.03, c[2])]), 0.013, 'GK_GreenDark')
    tube(smooth([(0.068, DECOR_BASE + 0.11, -0.055), (0.11, DECOR_BASE + 0.16, -0.02), (0.13, DECOR_BASE + 0.15, 0.02)]), 0.007, 'GK_Green')
    ball((0.135, DECOR_BASE + 0.15, 0.03), (0.035, 0.008, 0.03), 'GK_Lime', seg=14)
    for ring_k, (count, rad, lift, mat) in enumerate(((8, 0.05, -0.005, 'GK_Pink'), (6, 0.032, 0.012, 'GK_Coral'))):
        for i in range(count):
            a = 360.0 * i / count + ring_k * 30
            ar = math.radians(a)
            ball((c[0] + rad * math.cos(ar), c[1] + lift, c[2] + rad * math.sin(ar)), (0.045 if ring_k == 0 else 0.03, 0.018, 0.02), mat, seg=14, rot=(0, -a, 18))
    ball((c[0], c[1] + 0.012, c[2]), (0.03, 0.028, 0.03), 'GK_GlowWarm', seg=18)
    # The frog: a body, two eye bumps with eyes, a smile line of a darker green.
    fx, fz = -0.06, 0.08
    ball((fx, DECOR_BASE + 0.045, fz), (0.04, 0.03, 0.035), 'GK_Lime', seg=18)
    for sx in (-1, 1):
        ball((fx + sx * 0.02, DECOR_BASE + 0.075, fz + 0.012), 0.014, 'GK_Lime', seg=12)
        ball((fx + sx * 0.02, DECOR_BASE + 0.078, fz + 0.024), 0.007, 'GK_Ink', seg=10)
    tube([(fx - 0.018, DECOR_BASE + 0.05, fz + 0.034), (fx, DECOR_BASE + 0.045, fz + 0.037), (fx + 0.018, DECOR_BASE + 0.05, fz + 0.034)], 0.003, 'GK_GreenDark')
    return join('DecorLilyLamp')


def decor_watermill():
    """Riverbank: a little wooden watermill with a red roof and a lit window, its wheel facing front in a stream."""
    decor_base('GK_Grass')
    hx, hz = -0.02, -0.06
    box((hx, DECOR_BASE + 0.08, hz), (0.2, 0.16, 0.12), 'GK_WoodLight', r=0.012)
    for k in range(4):
        box((hx, DECOR_BASE + 0.025 + k * 0.04, hz + 0.061), (0.202, 0.006, 0.004), 'GK_WoodMid', r=0)
    for s in (-1, 1):
        box((hx, DECOR_BASE + 0.205, hz + s * 0.04), (0.24, 0.014, 0.105), 'GK_Coral', r=0.006, rot=(s * -38, 0, 0))
    box((hx, DECOR_BASE + 0.245, hz), (0.24, 0.018, 0.02), 'GK_Coral', r=0.006)
    box((hx - 0.06, DECOR_BASE + 0.055, hz + 0.063), (0.04, 0.07, 0.006), 'GK_WoodDark', r=0.004)
    box((hx - 0.06, DECOR_BASE + 0.125, hz + 0.063), (0.04, 0.035, 0.006), 'GK_GlowWarm', r=0.004)
    box((hx - 0.06, DECOR_BASE + 0.125, hz + 0.066), (0.044, 0.005, 0.005), 'GK_WoodDark', r=0)
    box((hx - 0.06, DECOR_BASE + 0.125, hz + 0.066), (0.005, 0.039, 0.005), 'GK_WoodDark', r=0)
    # The stream across the front and the wheel standing in it before the hut's right half, its axle into the wall.
    wx, wy, wz = 0.05, DECOR_BASE + 0.1, 0.035
    box((0.0, DECOR_BASE + 0.006, 0.105), (0.32, 0.012, 0.07), 'GK_GlowTeal', r=0.004)
    cyl((wx, wy, wz - 0.03), 0.011, 0.06, 'GK_WoodDark', r=0.003, rot=(90, 0, 0))
    for s in (-1, 1):
        ring((wx, wy, wz + s * 0.014), 0.075, 0.008, 'GK_WoodDark', rot=(90, 0, 0))
    for i in range(8):
        a = 45 * i
        ar = math.radians(a)
        box((wx + 0.068 * math.cos(ar), wy + 0.068 * math.sin(ar), wz), (0.03, 0.008, 0.045), 'GK_Wood', r=0.003, rot=(0, 0, a))
    for i in range(4):
        a = math.radians(45 * i)
        box((wx, wy, wz), (0.15, 0.008, 0.008), 'GK_WoodDark', r=0.002, rot=(0, 0, 45 * i))
    cyl((wx, wy, wz), 0.018, 0.04, 'GK_Wood', r=0.004, rot=(90, 0, 0))
    return join('DecorWatermill')


def decor_volcano():
    """Hot springs: a little volcano, lava glowing in its crater and running down its front, a puff of steam above."""
    decor_base('GK_Rock')
    h = 0.27
    cone((0, DECOR_BASE + h / 2, -0.01), 0.16, 0.055, h, 'GK_Rock', verts=24)
    cone((0, DECOR_BASE + 0.03, -0.01), 0.17, 0.14, 0.06, 'GK_Lilac', verts=24)
    top = DECOR_BASE + h
    cyl((0, top - 0.004, -0.01), 0.047, 0.012, 'GK_GlowCoral', r=0)
    ball((0, top + 0.004, -0.01), (0.042, 0.02, 0.042), 'GK_GlowCoral', seg=18)
    tube([(0.01, top - 0.005, 0.035), (0.03, top - 0.07, 0.06), (0.015, top - 0.15, 0.1), (0.04, DECOR_BASE + 0.04, 0.15)], 0.013, 'GK_GlowYellow')
    tube([(-0.02, top - 0.01, 0.03), (-0.05, top - 0.09, 0.07), (-0.07, DECOR_BASE + 0.07, 0.12)], 0.009, 'GK_GlowCoral')
    for x, y, z, r in ((0.0, top + 0.06, -0.02, 0.04), (0.035, top + 0.115, -0.03, 0.032), (-0.012, top + 0.165, -0.035, 0.025), (0.03, top + 0.2, -0.04, 0.017)):
        ball((x, y, z), r, 'GK_Cloud', seg=16)
    for x, z, r, m in ((0.13, 0.07, 0.03, 'GK_Rock'), (-0.12, 0.09, 0.025, 'GK_Pebble2'), (0.1, -0.12, 0.022, 'GK_Pebble2')):
        ball((x, DECOR_BASE + r * 0.6, z), (r, r * 0.75, r), m, seg=14)
    return join('DecorVolcano')


def decor_lantern():
    """Hot springs: a stone lantern, warm light behind its paper windows, under a coral roof with a gold finial."""
    decor_base('GK_Rock')
    cyl((0, DECOR_BASE + 0.02, 0), 0.075, 0.04, 'GK_Sand', r=0.012, verts=6)
    cyl((0, DECOR_BASE + 0.1, 0), 0.034, 0.12, 'GK_Sand', r=0.008, verts=12)
    box((0, DECOR_BASE + 0.175, 0), (0.14, 0.03, 0.14), 'GK_Sand', r=0.01)
    box((0, DECOR_BASE + 0.245, 0), (0.095, 0.105, 0.095), 'GK_GlowWarm', r=0.01)
    for sx in (-1, 1):
        for sz in (-1, 1):
            box((sx * 0.052, DECOR_BASE + 0.245, sz * 0.052), (0.024, 0.11, 0.024), 'GK_Sand', r=0.006)
    for s in (-1, 1):
        box((0, DECOR_BASE + 0.245, s * 0.049), (0.006, 0.1, 0.004), 'GK_WoodDark', r=0)
        box((s * 0.049, DECOR_BASE + 0.245, 0), (0.004, 0.1, 0.006), 'GK_WoodDark', r=0)
    cone((0, DECOR_BASE + 0.33, 0), 0.12, 0.018, 0.075, 'GK_Coral', rot=(0, 45, 0), verts=4)
    box((0, DECOR_BASE + 0.296, 0), (0.15, 0.012, 0.15), 'GK_Coral', r=0.005)
    ball((0, DECOR_BASE + 0.385, 0), 0.02, 'GK_Gold', seg=14)
    for x, z, r in ((0.11, 0.08, 0.028), (-0.1, 0.1, 0.022), (-0.12, -0.07, 0.026)):
        ball((x, DECOR_BASE + r * 0.6, z), (r, r * 0.7, r), 'GK_Pebble2', seg=14)
    return join('DecorLantern')


def decor_duck_spa():
    """Hot springs: a round rock pool of steaming warm water with a yellow rubber duck afloat in it."""
    decor_base('GK_Rock')
    cyl((0, DECOR_BASE + 0.025, 0), 0.135, 0.05, 'GK_Rock', r=0.02)
    cyl((0, DECOR_BASE + 0.048, 0), 0.105, 0.01, 'GK_GlowTeal', r=0)
    for i in range(10):
        a = math.radians(i * 36 + 8)
        ball((0.125 * math.cos(a), DECOR_BASE + 0.055, 0.125 * math.sin(a)), (0.035, 0.026, 0.03), 'GK_Rock' if i % 3 else 'GK_Pebble2', seg=14, rot=(0, -i * 36, 0))
    dx, dz = 0.01, 0.01
    y = DECOR_BASE + 0.07
    ball((dx, y, dz), (0.045, 0.032, 0.058), 'GK_Yellow', seg=20)
    ball((dx, y + 0.01, dz - 0.052), (0.02, 0.022, 0.02), 'GK_Yellow', seg=14)
    ball((dx, y + 0.052, dz + 0.035), 0.03, 'GK_Yellow', seg=18)
    ball((dx, y + 0.046, dz + 0.068), (0.02, 0.008, 0.018), 'GK_Coral', seg=14)
    for sx in (-1, 1):
        ball((dx + sx * 0.016, y + 0.062, dz + 0.056), 0.006, 'GK_Ink', seg=10)
        ball((dx + sx * 0.04, y + 0.008, dz), (0.012, 0.016, 0.03), 'GK_Gold', seg=12)
    for x, yy, z, r in ((-0.06, 0.12, -0.03, 0.03), (-0.04, 0.17, -0.05, 0.025), (0.07, 0.15, -0.06, 0.024), (-0.07, 0.21, -0.06, 0.017)):
        ball((x, DECOR_BASE + yy, z), r, 'GK_Cloud', seg=14)
    return join('DecorDuckSpa')


def decor_lighthouse():
    """Seaside: a lighthouse in coral and cream stripes on its rocks, its lamp room glowing under a coral cap."""
    decor_base('GK_Sand')
    for x, z, r, m in ((0.09, 0.06, 0.045, 'GK_Rock'), (-0.08, 0.08, 0.038, 'GK_Rock'), (0.06, -0.09, 0.035, 'GK_Pebble2'), (-0.1, -0.05, 0.03, 'GK_Rock')):
        ball((x, DECOR_BASE + r * 0.55, z), (r, r * 0.75, r), m, seg=14)
    y0, h, r0, r1 = DECOR_BASE + 0.02, 0.34, 0.068, 0.048
    cyl((0, DECOR_BASE + 0.01, 0), 0.08, 0.025, 'GK_Rock', r=0.008)
    cone((0, y0 + h / 2, 0), r0, r1, h, 'GK_Cream', verts=28)
    for k in range(3):
        yy = y0 + 0.045 + k * 0.11
        rr = r0 - (r0 - r1) * (yy - y0) / h + 0.004
        cyl((0, yy, 0), rr, 0.045, 'GK_Coral', r=0.006, verts=28)
    box((0, y0 + 0.04, r0 - 0.004), (0.028, 0.05, 0.012), 'GK_Teal', r=0.006)
    box((0, y0 + 0.2, r0 - 0.014), (0.02, 0.024, 0.01), 'GK_GlowWarm', r=0.004)
    top = y0 + h
    cyl((0, top + 0.006, 0), 0.068, 0.014, 'GK_Teal', r=0.005)
    ring((0, top + 0.035, 0), 0.062, 0.004, 'GK_Teal')
    for i in range(8):
        a = math.radians(i * 45)
        cyl((0.062 * math.cos(a), top + 0.022, 0.062 * math.sin(a)), 0.003, 0.03, 'GK_Teal', r=0, verts=8)
    cyl((0, top + 0.045, 0), 0.04, 0.065, 'GK_GlowYellow', r=0.006)
    cone((0, top + 0.1, 0), 0.052, 0.006, 0.045, 'GK_Coral', verts=24)
    ball((0, top + 0.128, 0), 0.012, 'GK_Gold', seg=12)
    return join('DecorLighthouse')


def decor_chest():
    """Seaside: a treasure chest thrown open, a heap of gold inside, pearls and coins spilling onto the sand."""
    decor_base('GK_Sand')
    cz = -0.015
    box((0, DECOR_BASE + 0.05, cz), (0.2, 0.1, 0.13), 'GK_Wood', r=0.012)
    for s in (-1, 1):
        box((s * 0.07, DECOR_BASE + 0.05, cz), (0.018, 0.104, 0.134), 'GK_Gold', r=0.004)
    box((0, DECOR_BASE + 0.07, cz + 0.066), (0.03, 0.035, 0.008), 'GK_Gold', r=0.004)
    box((0, DECOR_BASE + 0.16, cz - 0.085), (0.2, 0.12, 0.024), 'GK_WoodMid', r=0.01, rot=(-18, 0, 0))
    for s in (-1, 1):
        box((s * 0.07, DECOR_BASE + 0.16, cz - 0.085), (0.018, 0.122, 0.028), 'GK_Gold', r=0.004, rot=(-18, 0, 0))
    ball((0, DECOR_BASE + 0.1, cz), (0.088, 0.032, 0.055), 'GK_Gold', seg=20)
    for x, y, z in ((-0.04, 0.125, -0.01), (0.03, 0.128, -0.02), (0.0, 0.132, 0.01), (0.055, 0.12, 0.015), (-0.06, 0.115, 0.02)):
        ball((x, DECOR_BASE + y, cz + z), 0.016, 'GK_Paper', seg=12)
    for x, z, a in ((0.06, 0.09, 12), (0.09, 0.07, -20), (-0.05, 0.095, 30), (0.02, 0.11, -8)):
        cyl((x, DECOR_BASE + 0.006, z), 0.017, 0.005, 'GK_Gold', r=0, rot=(a, 0, 0), verts=16)
    for x, z in ((-0.09, 0.08), (0.12, 0.03)):
        ball((x, DECOR_BASE + 0.014, z), 0.014, 'GK_Paper', seg=12)
    return join('DecorChest')


def decor_coral_arch():
    """Seaside: an arch of pink coral with branching corals beside it, a starfish and two shells on the sand."""
    decor_base('GK_Sand')
    pts = [(0.12 * math.cos(math.radians(t)), DECOR_BASE + 0.215 * math.sin(math.radians(t)), -0.02) for t in range(0, 181, 15)]
    tube(pts, 0.03, 'GK_Pink')
    for k, t in enumerate(range(10, 180, 28)):
        ball((0.12 * math.cos(math.radians(t)), DECOR_BASE + 0.215 * math.sin(math.radians(t)) + 0.012, -0.0 + (k % 2) * -0.04), 0.017, 'GK_Coral', seg=12)
    # Branching coral at the back left, a fan at the back right.
    trunk = [(-0.08, DECOR_BASE, -0.1), (-0.085, DECOR_BASE + 0.12, -0.1)]
    tube(trunk, 0.016, 'GK_Coral')
    for dx, dy, dz in ((-0.05, 0.1, 0.0), (0.04, 0.12, 0.01), (0.0, 0.16, -0.02)):
        tip = (-0.085 + dx, DECOR_BASE + 0.1 + dy, -0.1 + dz)
        tube([(-0.085, DECOR_BASE + 0.09, -0.1), tip], 0.011, 'GK_Coral')
        ball(tip, 0.016, 'GK_Pink', seg=12)
    cyl((0.09, DECOR_BASE + 0.11, -0.1), 0.07, 0.008, 'GK_Lilac', r=0.002, rot=(90, 0, 0), rz=0.1)
    # The starfish and the shells.
    sx, sz = 0.06, 0.09
    for i in range(5):
        a = i * 72
        ar = math.radians(a)
        ball((sx + 0.025 * math.cos(ar), DECOR_BASE + 0.008, sz + 0.025 * math.sin(ar)), (0.028, 0.008, 0.011), 'GK_Yellow', seg=12, rot=(0, -a, 0))
    ball((sx, DECOR_BASE + 0.01, sz), (0.016, 0.009, 0.016), 'GK_Yellow', seg=12)
    ball((-0.07, DECOR_BASE + 0.012, 0.09), (0.03, 0.014, 0.024), 'GK_Pebble1', seg=14)
    ball((-0.03, DECOR_BASE + 0.01, 0.12), (0.02, 0.01, 0.017), 'GK_Pebble3', seg=12)
    return join('DecorCoralArch')


WAVE_DECOR = [decor_reeds, decor_lily_lamp, decor_watermill, decor_volcano, decor_lantern, decor_duck_spa,
              decor_lighthouse, decor_chest, decor_coral_arch]


BUILDS = [grower, grower_ring, grower_tap, grower_lid, incubator, incubator_screen, incubator_port, incubator_doser,
          tank_starter, tank_bed, tank_filter, tank_lid, counter, counter_bags, counter_reader, counter_till,
          unit_a_floor, unit_a_dressing, street, back_room_floor, back_room_dressing] + WAVE2 + WAVE3 + WAVE4 + WAVE_DECOR  # later waves last, so earlier ones draw their random numbers as before


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

# With --only, the manifest keeps the other assets' records and takes the rebuilt ones'.
kept = []
if ONLY and (SOURCE / 'manifest.json').is_file():
    kept = [r for r in json.loads((SOURCE / 'manifest.json').read_text(encoding='utf-8'))['assets'] if r['id'] not in ONLY]
names = list(built)
records = sorted(kept + records, key=lambda r: names.index(r['id']) if r['id'] in names else len(names))
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
