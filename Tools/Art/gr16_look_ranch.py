# GR-16 look development (not game art): one look for the new game's world in the feel the user asked for after turning down
# the three first looks ("shapes too basic, colours too pale and flat, none is the style"; reference for the feel only:
# Slime Rancher). Juicy soft colours (turquoise, sunny yellow, coral, fresh green, warm wood), glossy surfaces, sunlight
# through real windows onto a 3D landscape, glowing cultures and screens, and stations modelled with their parts.
# Original and local (Blender 5.0, EEVEE with ray tracing):
#   blender --background --factory-startup --python Tools/Art/gr16_look_ranch.py -- --repo C:/dev/DumbVideoGameStudio --out <folder> [--quick] [--only shop,lab,stations]
# It renders <out>/ranch-shop.png, ranch-lab.png (from the player's eye height, 1.65 m, as the first sketches) and
# ranch-stations.png (the stations on a backdrop), with the game's own creatures appended from ArtSource/DioramaKit.
import bpy, math, random, sys, argparse, json, os
from pathlib import Path
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument('--repo', default='C:/dev/DumbVideoGameStudio')
ap.add_argument('--out', required=True)
ap.add_argument('--only', default='')
ap.add_argument('--quick', action='store_true')
ap.add_argument('--sun-only', action='store_true')  # debugging: only the sun and the sky
args = ap.parse_args(argv)
OUT = Path(args.out); OUT.mkdir(parents=True, exist_ok=True)
KIT = Path(args.repo) / 'ArtSource' / 'DioramaKit' / 'EvolutionShop_DioramaKit.blend'
KIT_COLOURS = {k: v['srgb_hex'] for k, v in json.loads((Path(args.repo) / 'ArtSource' / 'DioramaKit' / 'manifest.json').read_text()).get('materials', {}).items()}
FONT = 'C:/Windows/Fonts/seguibl.ttf'  # only for the sketch's sign; the game's sign would be modelled

# The palette: soft but juicy, never washed out.
C = dict(
    teal='#22BFB0', teal_light='#7FE3D6', teal_dark='#15867F', yellow='#FFC93C', coral='#FF7A7A', pink='#FF9EC4',
    lime='#9BDB4D', green='#56B947', green_dark='#3E9A45', cream='#FFF3DF', plaster='#FFE8C6', plaster_lab='#DDF5EC',
    wood='#E39A5A', wood_light='#EFB277', wood_dark='#B9713D', beam='#A8612F', lilac='#A98BFF', sky='#6FC8FF',
    horizon='#DDF5FF', stone='#CDBBA7', ink='#2B3A55', screen='#0F3B4C', white='#FFFFFF', sand='#F7DFAE',
)
BLOOM, FLOW = '#FF5E7E', '#3FA9FF'


def lin(h, a=1.0):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [a])


def scale_col(col, k):
    return (min(col[0] * k, 1), min(col[1] * k, 1), min(col[2] * k, 1), 1)


class B:
    def __init__(self):
        self.mats = {}
        self.rng = random.Random(11)

    # ------------------------------------------------------------------------------------------------ materials
    def mat(self, kind, color, **kw):
        key = (kind, color, tuple(sorted(kw.items())))
        if key in self.mats: return self.mats[key]
        m = bpy.data.materials.new(kind + ' ' + color)
        m.use_nodes = True
        nt = m.node_tree
        p = nt.nodes['Principled BSDF']
        base = lin(color)
        p.inputs['Base Color'].default_value = base
        def vary(amount, scale, stretch=(1, 1, 1), detail=3.0):
            tc = nt.nodes.new('ShaderNodeTexCoord')
            mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = stretch
            nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = scale; nz.inputs['Detail'].default_value = detail
            ramp = nt.nodes.new('ShaderNodeValToRGB')
            ramp.color_ramp.elements[0].color = scale_col(base, 1 - amount)
            ramp.color_ramp.elements[1].color = scale_col(base, 1 + amount * 0.6)
            ramp.color_ramp.elements[0].position = 0.3; ramp.color_ramp.elements[1].position = 0.7
            nt.links.new(tc.outputs['Object'], mp.inputs['Vector']); nt.links.new(mp.outputs[0], nz.inputs['Vector'])
            nt.links.new(nz.outputs['Fac'], ramp.inputs['Fac']); nt.links.new(ramp.outputs['Color'], p.inputs['Base Color'])
        if kind == 'plastic':
            p.inputs['Roughness'].default_value = kw.get('rough', 0.32)
            p.inputs['Coat Weight'].default_value = 0.55; p.inputs['Coat Roughness'].default_value = 0.08
        elif kind == 'paint':
            p.inputs['Roughness'].default_value = 0.75; p.inputs['Specular IOR Level'].default_value = 0.3
            if kw.get('noise'): vary(kw['noise'], 2.5)
        elif kind == 'wood':
            p.inputs['Roughness'].default_value = 0.5; p.inputs['Coat Weight'].default_value = 0.25; p.inputs['Coat Roughness'].default_value = 0.2
            g = kw.get('grain', 'y')
            vary(0.16, 3.0, {'x': (1.0, 14, 14), 'y': (14, 1.0, 14), 'z': (14, 14, 1.0)}[g], 6.0)
        elif kind == 'glass':
            p.inputs['Base Color'].default_value = lin(color)
            p.inputs['Roughness'].default_value = 0.02
            lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = 0.35
            ma = nt.nodes.new('ShaderNodeMath'); ma.operation = 'MULTIPLY_ADD'; ma.use_clamp = True
            ma.inputs[1].default_value = 0.55; ma.inputs[2].default_value = kw.get('alpha', 0.14)
            nt.links.new(lw.outputs['Facing'], ma.inputs[0]); nt.links.new(ma.outputs[0], p.inputs['Alpha'])
            p.inputs['Coat Weight'].default_value = 1.0; p.inputs['Coat Roughness'].default_value = 0.02
            p.inputs['Specular IOR Level'].default_value = 1.0
            m.surface_render_method = 'BLENDED'; m.use_backface_culling = False
        elif kind == 'water':
            p.inputs['Roughness'].default_value = 0.05; p.inputs['Alpha'].default_value = kw.get('alpha', 0.28)
            p.inputs['Emission Color'].default_value = base; p.inputs['Emission Strength'].default_value = 0.12
            m.surface_render_method = 'BLENDED'
        elif kind == 'liquid':
            p.inputs['Roughness'].default_value = 0.12; p.inputs['Coat Weight'].default_value = 0.6
            p.inputs['Emission Color'].default_value = base; p.inputs['Emission Strength'].default_value = kw.get('glow', 0.9)
        elif kind == 'emit':
            p.inputs['Base Color'].default_value = (0, 0, 0, 1)
            p.inputs['Emission Color'].default_value = base; p.inputs['Emission Strength'].default_value = kw.get('glow', 4.0)
        elif kind == 'metal':
            p.inputs['Metallic'].default_value = 1.0; p.inputs['Roughness'].default_value = kw.get('rough', 0.22)
        elif kind == 'fabric':
            p.inputs['Roughness'].default_value = 0.9; p.inputs['Sheen Weight'].default_value = 0.6
        elif kind == 'foliage':
            p.inputs['Roughness'].default_value = 0.5; p.inputs['Coat Weight'].default_value = 0.2
            vary(0.12, 4.0)
        elif kind == 'creature':
            p.inputs['Roughness'].default_value = 0.28; p.inputs['Coat Weight'].default_value = 0.8; p.inputs['Coat Roughness'].default_value = 0.05
            p.inputs['Subsurface Weight'].default_value = 0.15
        elif kind == 'ground':
            p.inputs['Roughness'].default_value = 0.8
            vary(0.18, 0.08)
        self.mats[key] = m
        return m

    # ------------------------------------------------------------------------------------------------ shapes
    def _finish(self, o, m, bevel=0.0, seg=5, normals=True):
        o.data.materials.append(m)
        if bevel > 0:
            mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = bevel; mod.segments = seg
            mod.limit_method = 'ANGLE'; mod.angle_limit = math.radians(35); mod.use_clamp_overlap = True
        for p in o.data.polygons: p.use_smooth = True
        if normals:
            wn = o.modifiers.new('Normals', 'WEIGHTED_NORMAL'); wn.keep_sharp = True; wn.mode = 'FACE_AREA'
        return o

    def box(self, name, center, size, m, r=0.04, seg=5, rot=(0, 0, 0), shadow=True):
        bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=[math.radians(a) for a in rot])
        o = bpy.context.object; o.name = name; o.scale = size
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        self._finish(o, m, min(r, 0.48 * min(size)) if r > 0 else 0, seg)
        o.visible_shadow = shadow
        return o

    def cyl(self, name, center, radius, depth, m, r=0.03, rot=(0, 0, 0), verts=48, seg=4):
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=center, rotation=[math.radians(a) for a in rot])
        o = bpy.context.object; o.name = name
        return self._finish(o, m, min(r, 0.45 * radius, 0.45 * depth) if r > 0 else 0, seg)

    def ball(self, name, center, scale, m, seg=40, shadow=True):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=center)
        o = bpy.context.object; o.name = name; o.scale = scale if isinstance(scale, (tuple, list)) else (scale,) * 3
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        self._finish(o, m, normals=False)
        o.visible_shadow = shadow
        return o

    def torus(self, name, center, major, minor, m, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=center, rotation=[math.radians(a) for a in rot],
                                         major_segments=64, minor_segments=16)
        o = bpy.context.object; o.name = name
        return self._finish(o, m, normals=False)

    def tube(self, name, points, radius, m):
        cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
        cu.bevel_depth = radius; cu.bevel_resolution = 6; cu.use_fill_caps = True
        sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(points) - 1)
        for bp, pt in zip(sp.bezier_points, points):
            bp.co = pt; bp.handle_left_type = 'AUTO'; bp.handle_right_type = 'AUTO'
        sp.use_smooth = True
        o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
        cu.materials.append(m)
        return o

    def pipe(self, name, points, radius, m, joint):
        """A pipe of straight runs with a round joint at every bend, like plumbing a child could draw."""
        cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
        cu.bevel_depth = radius; cu.bevel_resolution = 6; cu.use_fill_caps = True
        sp = cu.splines.new('POLY'); sp.points.add(len(points) - 1)
        for p, pt in zip(sp.points, points): p.co = (pt[0], pt[1], pt[2], 1)
        sp.use_smooth = True
        o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
        cu.materials.append(m)
        for pt in points[1:-1]: self.ball(name + ' joint', pt, radius * 1.55, joint, seg=24)
        return o

    def text(self, body, center, size, m, rot=(90, 0, 0), extrude=0.025):
        bpy.ops.object.text_add(location=center, rotation=[math.radians(a) for a in rot])
        o = bpy.context.object; o.data.body = body; o.data.size = size; o.data.extrude = extrude
        if os.path.exists(FONT): o.data.font = bpy.data.fonts.load(FONT)
        o.data.align_x = 'CENTER'; o.data.align_y = 'CENTER'; o.data.bevel_depth = 0.008; o.data.bevel_resolution = 3
        o.data.materials.append(m)
        return o

    def light(self, kind, name, at, energy, color='#FFFFFF', size=0.2, aim=None, rot=(0, 0, 0)):
        if args.sun_only: energy = 0
        L = bpy.data.lights.new(name, kind); L.energy = energy; L.color = lin(color)[:3]
        if kind == 'AREA': L.size = size if not isinstance(size, tuple) else size[0]
        if kind == 'AREA' and isinstance(size, tuple): L.shape = 'RECTANGLE'; L.size_y = size[1]
        if kind == 'POINT': L.shadow_soft_size = size
        o = bpy.data.objects.new(name, L); o.location = at; bpy.context.scene.collection.objects.link(o)
        if aim is not None:
            o.rotation_euler = (Vector(aim) - Vector(at)).to_track_quat('-Z', 'Y').to_euler()
        else:
            o.rotation_euler = [math.radians(a) for a in rot]
        return o

    # ------------------------------------------------------------------------------------------------ the game's creatures
    def creature(self, species, at, length, yaw):
        with bpy.data.libraries.load(str(KIT)) as (src, dst):
            dst.objects = [n for n in src.objects if n == 'Diorama' + species or n.startswith('Diorama' + species + ' / ')]
        objs = [o for o in dst.objects if o]
        root = next(o for o in objs if ' / ' not in o.name)
        for o in objs: bpy.context.scene.collection.objects.link(o)
        bpy.context.view_layer.update()
        size = max((max(c.dimensions) for c in objs if c.type == 'MESH'), default=1)
        root.scale = (length / size,) * 3; root.location = at; root.rotation_euler = (0, 0, math.radians(yaw))
        for o in objs:
            if o.type != 'MESH': continue
            for i, slot in enumerate(o.material_slots):
                if slot.material is None: continue
                code = KIT_COLOURS.get(slot.material.name.split('.')[0])
                if code: o.material_slots[i].material = self.mat('creature', '#' + code.lstrip('#'))
        return root


# ==================================================================================================== the stations
def grower(b, at, culture, pipe_to=None):
    x, y = at
    plastic, teal, teal_dark = b.mat('plastic', C['cream']), b.mat('plastic', C['teal']), b.mat('plastic', C['teal_dark'])
    for k in range(3):
        a = math.radians(90 + k * 120)
        b.cyl('Grower foot', (x + 0.22 * math.cos(a), y + 0.22 * math.sin(a), 0.035), 0.065, 0.07, teal_dark, r=0.02)
    b.cyl('Grower base', (x, y, 0.33), 0.3, 0.52, plastic, r=0.09)
    b.torus('Grower band', (x, y, 0.47), 0.302, 0.03, teal)
    b.torus('Grower band', (x, y, 0.19), 0.296, 0.02, teal)
    # The gauge on its front: a dial with its culture's colour and a needle.
    b.cyl('Gauge', (x, y - 0.3, 0.32), 0.085, 0.04, teal, r=0.012, rot=(90, 0, 0))
    b.cyl('Gauge face', (x, y - 0.322, 0.32), 0.065, 0.006, b.mat('emit', '#FFF8E6', glow=1.2), r=0, rot=(90, 0, 0))
    b.cyl('Gauge arc', (x, y - 0.326, 0.32), 0.05, 0.004, b.mat('emit', culture, glow=2.5), r=0, rot=(90, 0, 0))
    b.box('Needle', (x + 0.015, y - 0.332, 0.335), (0.012, 0.004, 0.05), b.mat('plastic', C['ink']), r=0.003, rot=(0, -35, 0))
    b.cyl('Grower collar', (x, y, 0.625), 0.27, 0.09, teal, r=0.035)
    # The glass with its glowing culture and bubbles.
    b.cyl('Grower glass', (x, y, 1.01), 0.24, 0.72, b.mat('glass', '#EAFBFF'), r=0)
    b.cyl('Culture', (x, y, 0.9), 0.212, 0.48, b.mat('liquid', culture, glow=0.9), r=0.03)
    b.cyl('Culture top', (x, y, 1.142), 0.2, 0.012, b.mat('liquid', '#FFFFFF', glow=0.25), r=0)
    for k in range(6):
        bx = x + b.rng.uniform(-0.12, 0.12); by = y + b.rng.uniform(-0.12, 0.05)
        b.ball('Bubble', (bx, by, 0.72 + k * 0.07), 0.018 + 0.008 * (k % 3), b.mat('liquid', '#FFFFFF', glow=0.6), seg=16, shadow=False)
    # The cap: a collar, a dome and a little light; a pipe out of its side.
    b.cyl('Grower cap ring', (x, y, 1.405), 0.265, 0.08, teal, r=0.035)
    b.ball('Grower cap', (x, y, 1.44), (0.235, 0.235, 0.15), plastic)
    b.cyl('Cap stalk', (x, y, 1.62), 0.02, 0.06, teal_dark, r=0.008)
    b.ball('Cap light', (x, y, 1.67), 0.045, b.mat('emit', C['yellow'], glow=6))
    if pipe_to:
        b.pipe('Culture pipe', [(x + 0.2, y, 1.46)] + pipe_to, 0.034, b.mat('liquid', culture, glow=0.5), teal)
        b.cyl('Pipe collar', (x + 0.24, y, 1.46), 0.05, 0.05, teal_dark, r=0.015, rot=(0, 90, 0))


def incubator(b, at):
    x, y = at
    plastic, yellow, teal = b.mat('plastic', C['cream']), b.mat('plastic', C['yellow']), b.mat('plastic', C['teal'])
    for k in range(4):
        a = math.radians(45 + k * 90)
        b.ball('Incubator foot', (x + 0.36 * math.cos(a), y + 0.36 * math.sin(a), 0.05), (0.07, 0.07, 0.05), b.mat('plastic', C['teal_dark']))
    b.cyl('Incubator base', (x, y, 0.33), 0.5, 0.56, yellow, r=0.12)
    b.box('Incubator panel', (x, y - 0.47, 0.3), (0.42, 0.06, 0.22), plastic, r=0.04)
    for k, col in enumerate((BLOOM, C['yellow'], FLOW)):
        b.cyl('Panel light', (x - 0.12 + k * 0.12, y - 0.505, 0.3), 0.032, 0.02, b.mat('emit', col, glow=4), r=0.006, rot=(90, 0, 0))
    b.cyl('Incubator plate', (x, y, 0.63), 0.47, 0.07, plastic, r=0.03)
    b.torus('Glow ring', (x, y, 0.665), 0.44, 0.022, b.mat('emit', C['teal_light'], glow=5))
    b.torus('Nest', (x, y, 0.71), 0.15, 0.055, b.mat('fabric', C['pink']))
    b.ball('Dose', (x - 0.05, y, 0.74), 0.055, b.mat('liquid', BLOOM, glow=1.6), shadow=False)
    b.ball('Dose', (x + 0.06, y + 0.02, 0.74), 0.055, b.mat('liquid', FLOW, glow=1.6), shadow=False)
    b.ball('Incubator dome', (x, y, 0.66), (0.43, 0.43, 0.43), b.mat('glass', '#F2FCFF', alpha=0.12), shadow=False)
    b.torus('Dome top ring', (x, y, 1.07), 0.08, 0.025, teal)
    b.ball('Dome knob', (x, y, 1.1), 0.05, teal)
    # The console on its stalk: a tilted screen with the mix's bars, a big round button.
    sx, sy = x + 0.62, y - 0.18
    b.cyl('Console stalk', (sx, sy, 0.45), 0.04, 0.9, b.mat('plastic', C['teal_dark']), r=0.015)
    b.cyl('Console foot', (sx, sy, 0.03), 0.14, 0.06, b.mat('plastic', C['teal_dark']), r=0.025)
    b.box('Console', (sx, sy, 0.98), (0.36, 0.08, 0.27), plastic, r=0.04, rot=(-28, 0, 0))
    b.box('Screen', (sx, sy - 0.035, 0.995), (0.29, 0.02, 0.19), b.mat('emit', C['screen'], glow=1.0), r=0.02, rot=(-28, 0, 0))
    for k, (col, h) in enumerate(((BLOOM, 0.11), (FLOW, 0.07), (C['yellow'], 0.13))):
        b.box('Bar', (sx - 0.08 + k * 0.08, sy - 0.05, 0.95 + h / 2 * 0.88), (0.045, 0.01, h * 0.88), b.mat('emit', col, glow=3.5), r=0.006, rot=(-28, 0, 0))
    b.cyl('Big button', (sx + 0.13, sy - 0.03, 0.86), 0.045, 0.04, b.mat('plastic', C['coral']), r=0.015, rot=(62, 0, 0))


def tank(b, at, creatures=True):
    x, y = at
    w, d = 1.5, 0.62
    plastic, teal, teal_dark = b.mat('plastic', C['cream']), b.mat('plastic', C['teal']), b.mat('plastic', C['teal_dark'])
    b.box('Tank cabinet', (x, y, 0.42), (w + 0.12, d + 0.12, 0.8), plastic, r=0.07)
    for s in (-1, 1):
        b.box('Cabinet door', (x + s * 0.39, y - d / 2 - 0.07, 0.42), (0.7, 0.03, 0.6), teal, r=0.02)
        b.ball('Door knob', (x + s * 0.12, y - d / 2 - 0.1, 0.48), 0.035, b.mat('plastic', C['yellow']))
    b.box('Tank kick', (x, y, 0.04), (w + 0.06, d + 0.06, 0.08), teal_dark, r=0.03)
    b.box('Frame bottom', (x, y, 0.86), (w + 0.1, d + 0.1, 0.08), teal, r=0.035)
    b.box('Frame top', (x, y, 1.68), (w + 0.1, d + 0.1, 0.07), teal, r=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.cyl('Frame post', (x + sx * w / 2, y + sy * d / 2, 1.27), 0.035, 0.8, teal, r=0.012)
    b.box('Tank glass', (x, y, 1.27), (w, d, 0.8), b.mat('glass', '#EAFBFF', alpha=0.1), r=0, shadow=False)
    b.box('Water', (x, y, 1.22), (w - 0.04, d - 0.04, 0.66), b.mat('water', '#47C6E8', alpha=0.25), r=0.01, shadow=False)
    b.box('Sand bed', (x, y, 0.92), (w - 0.06, d - 0.06, 0.05), b.mat('paint', C['sand']), r=0.015)
    pebble_cols = [C['sand'], '#FFC1B0', '#C9B6FF', '#FFE08A', '#BDEFE8']
    for k in range(70):
        b.ball('Pebble', (x + b.rng.uniform(-w / 2 + 0.06, w / 2 - 0.06), y + b.rng.uniform(-d / 2 + 0.06, d / 2 - 0.06), 0.955),
               (0.028, 0.024, 0.016), b.mat('plastic', pebble_cols[k % len(pebble_cols)], rough=0.5), seg=12)
    for k, (px, h, col) in enumerate(((0.52, 0.42, C['green']), (0.6, 0.3, C['lime']), (0.44, 0.26, C['green_dark']), (-0.58, 0.34, C['lime']))):
        b.ball('Water plant', (x + px, y + 0.14 - 0.06 * k, 0.95 + h / 2), (0.05, 0.035, h / 2), b.mat('foliage', col), seg=20)
    b.ball('Tank rock', (x - 0.42, y + 0.12, 0.99), (0.16, 0.12, 0.08), b.mat('plastic', '#B9A7E8', rough=0.6))
    b.box('Light bar', (x, y + 0.05, 1.75), (w * 0.82, 0.16, 0.06), plastic, r=0.025)
    b.box('Light strip', (x, y + 0.05, 1.716), (w * 0.76, 0.1, 0.01), b.mat('emit', '#F2FBFF', glow=3), r=0)
    if creatures:
        b.creature('Pondlet', (x - 0.38, y - 0.05, 1.22), 0.26, 200)
        b.creature('Pondlet', (x - 0.02, y - 0.1, 1.38), 0.26, 215)
        b.creature('Bubblebelly', (x + 0.3, y - 0.02, 1.14), 0.3, 160)


def register(b, at):
    x, y, z = at
    teal, plastic = b.mat('plastic', C['teal']), b.mat('plastic', C['cream'])
    b.box('Register body', (x, y, z + 0.08), (0.34, 0.38, 0.16), teal, r=0.06)
    b.box('Register drawer', (x - 0.17, y, z + 0.06), (0.03, 0.3, 0.08), plastic, r=0.012)
    b.box('Register head', (x + 0.04, y, z + 0.27), (0.08, 0.32, 0.22), plastic, r=0.04, rot=(0, -18, 0))
    b.box('Register screen', (x - 0.005, y, z + 0.28), (0.01, 0.26, 0.15), b.mat('emit', C['screen'], glow=1.0), r=0.008, rot=(0, -18, 0))
    b.box('Register price', (x - 0.012, y, z + 0.3), (0.006, 0.17, 0.04), b.mat('emit', C['lime'], glow=4), r=0.004, rot=(0, -18, 0))
    for i in range(3):
        for j in range(2):
            b.cyl('Key', (x - 0.07 + j * 0.07, y - 0.1 + i * 0.1, z + 0.17), 0.025, 0.02, b.mat('plastic', (C['coral'], C['yellow'], C['white'])[(i + j) % 3]), r=0.008)


def counter(b, at, length=2.0, along='y'):
    x, y = at
    wood = [b.mat('wood', c, grain='z') for c in (C['wood'], C['wood_light'], C['wood'])]
    sx, sy = (0.72, length) if along == 'y' else (length, 0.72)
    b.box('Counter body', (x, y, 0.46), (sx, sy, 0.86), b.mat('wood', C['wood_dark'], grain='z'), r=0.04)
    # Planks down the side facing the room.
    n = int(length / 0.2)
    for k in range(n):
        o = -length / 2 + 0.1 + k * 0.2
        if along == 'y': b.box('Counter plank', (x - 0.37, y + o, 0.46), (0.03, 0.19, 0.78), wood[k % 3], r=0.012, seg=2)
        else: b.box('Counter plank', (x + o, y - 0.37, 0.46), (0.19, 0.03, 0.78), wood[k % 3], r=0.012, seg=2)
    b.box('Counter kick', (x, y, 0.05), (sx - 0.04, sy - 0.04, 0.1), b.mat('plastic', C['teal_dark']), r=0.03)
    b.box('Counter trim', (x, y, 0.9), (sx + 0.06, sy + 0.06, 0.04), b.mat('plastic', C['teal']), r=0.018)
    b.box('Counter top', (x, y, 0.96), (sx + 0.14, sy + 0.14, 0.08), b.mat('plastic', C['cream']), r=0.035)
    top = 1.0
    if along == 'y':
        register(b, (x + 0.05, y - 0.35, top))
        rx, ry = x, y + 0.45
    else:
        register(b, (x - 0.3, y + 0.05, top))
        rx, ry = x + 0.35, y
    # A bell, a jar of treats, a small plant.
    b.cyl('Bell base', (rx - 0.1, ry, top + 0.012), 0.07, 0.024, b.mat('plastic', C['ink']), r=0.008)
    b.ball('Bell', (rx - 0.1, ry, top + 0.04), (0.07, 0.07, 0.06), b.mat('metal', C['yellow'], rough=0.18))
    b.ball('Bell button', (rx - 0.1, ry, top + 0.1), 0.015, b.mat('metal', C['yellow']))
    b.cyl('Treat jar', (rx + 0.12, ry + 0.2, top + 0.12), 0.09, 0.24, b.mat('glass', '#F2FCFF', alpha=0.15), r=0)
    b.cyl('Jar lid', (rx + 0.12, ry + 0.2, top + 0.255), 0.095, 0.035, b.mat('plastic', C['coral']), r=0.012)
    for k in range(14):
        b.ball('Treat', (rx + 0.12 + b.rng.uniform(-0.05, 0.05), ry + 0.2 + b.rng.uniform(-0.05, 0.05), top + 0.03 + k * 0.011),
               0.026, b.mat('plastic', (C['yellow'], C['pink'], C['lime'], C['lilac'])[k % 4]), seg=12, shadow=False)
    b.cyl('Little pot', (rx + 0.15, ry - 0.2 if along == 'y' else ry - 0.15, top + 0.06), 0.06, 0.12, b.mat('plastic', C['coral']), r=0.02)
    for k in range(3):
        b.ball('Succulent', (rx + 0.15 + (k - 1) * 0.03, (ry - 0.2 if along == 'y' else ry - 0.15) + (k % 2) * 0.02, top + 0.15 + (k % 2) * 0.02),
               (0.035, 0.035, 0.05), b.mat('foliage', C['lime']), seg=16)


def plant(b, at, size=1.0):
    x, y = at
    b.cyl('Plant pot', (x, y, 0.22 * size), 0.22 * size, 0.44 * size, b.mat('plastic', C['coral']), r=0.06 * size)
    b.torus('Pot rim', (x, y, 0.44 * size), 0.215 * size, 0.035 * size, b.mat('plastic', '#FF9A8F'))
    b.cyl('Soil', (x, y, 0.43 * size), 0.19 * size, 0.02, b.mat('paint', '#7A4E35'), r=0)
    for k, (dx, dy, dz, s, col) in enumerate(((0, 0, 0.78, 0.3, C['green']), (-0.14, 0.05, 0.66, 0.22, C['lime']), (0.15, -0.04, 0.68, 0.23, C['green_dark']),
                                              (0.04, 0.12, 0.92, 0.2, C['lime']), (0.02, -0.12, 0.86, 0.19, C['green']))):
        b.ball('Leaves', (x + dx * size, y + dy * size, dz * size), (s * size, s * size, s * 0.9 * size), b.mat('foliage', col), seg=28)


def pendant(b, at, ceiling):
    x, y, z = at
    b.cyl('Lamp cord', (x, y, (ceiling + z) / 2 + 0.12), 0.008, ceiling - z - 0.2, b.mat('plastic', C['ink']), r=0)
    b.cyl('Lamp cup', (x, y, z + 0.22), 0.05, 0.06, b.mat('plastic', C['teal']), r=0.02)
    b.ball('Lamp shade', (x, y, z + 0.08), (0.3, 0.3, 0.17), b.mat('plastic', C['yellow']))
    b.ball('Bulb', (x, y, z - 0.03), 0.08, b.mat('emit', '#FFF4D6', glow=12), shadow=False)
    b.light('POINT', 'Lamp', (x, y, z - 0.12), 45, '#FFD9A0', size=0.12)


def window(b, x_wall, yc, z0, z1, width, inward=1):
    teal, teal_dark = b.mat('plastic', C['teal']), b.mat('plastic', C['teal_dark'])
    fx = x_wall + inward * 0.04
    h = z1 - z0
    b.box('Window top', (fx, yc, z1 + 0.06), (0.2, width + 0.3, 0.14), teal, r=0.05)
    b.box('Window sill', (fx + inward * 0.08, yc, z0 - 0.05), (0.34, width + 0.4, 0.1), teal, r=0.04)
    for s in (-1, 1):
        b.box('Window side', (fx, yc + s * (width / 2 + 0.07), (z0 + z1) / 2), (0.2, 0.14, h + 0.1), teal, r=0.05)
    b.box('Mullion', (fx, yc, (z0 + z1) / 2), (0.08, 0.06, h), teal_dark, r=0.02)
    b.box('Transom', (fx, yc, z0 + h * 0.62), (0.08, width, 0.06), teal_dark, r=0.02)
    b.box('Window pane', (x_wall, yc, (z0 + z1) / 2), (0.01, width, h), b.mat('glass', '#EAFBFF', alpha=0.06), r=0, shadow=False)


# ==================================================================================================== rooms
def shell(b, ox, room, window_span, window_z, wall_mat, ceiling_h=3.3):
    """Walls, a cut window in the left wall, the ceiling, and a front wall the camera stands just behind (nearer than its clip distance)."""
    y0, y1 = -0.5, 5.2
    wy0, wy1 = window_span; wz0, wz1 = window_z
    lx = ox - 3.2
    # The left wall around the window opening, so the sun comes in only through it.
    b.box(room + ' left front', (lx, (y0 + wy0) / 2, ceiling_h / 2), (0.2, wy0 - y0, ceiling_h), wall_mat, r=0)
    b.box(room + ' left back', (lx, (wy1 + y1) / 2, ceiling_h / 2), (0.2, y1 - wy1, ceiling_h), wall_mat, r=0)
    b.box(room + ' left below', (lx, (wy0 + wy1) / 2, wz0 / 2), (0.2, wy1 - wy0, wz0), wall_mat, r=0)
    b.box(room + ' left above', (lx, (wy0 + wy1) / 2, (wz1 + ceiling_h) / 2), (0.2, wy1 - wy0, ceiling_h - wz1), wall_mat, r=0)
    b.box(room + ' back', (ox, y1 + 0.0, ceiling_h / 2), (6.6, 0.2, ceiling_h), wall_mat, r=0)
    b.box(room + ' right', (ox + 3.2, (y0 + y1) / 2, ceiling_h / 2), (0.2, y1 - y0, ceiling_h), wall_mat, r=0)
    b.box(room + ' ceiling', (ox, (y0 + y1) / 2, ceiling_h + 0.06), (6.6, y1 - y0, 0.12), wall_mat, r=0)
    b.box(room + ' front', (ox, -0.56, ceiling_h / 2), (6.6, 0.02, ceiling_h), wall_mat, r=0)
    return y0, y1


def shop(b):
    ox = 0
    H = 3.3
    # The floor: warm planks of three tones, joints staggered.
    woods = [b.mat('wood', c, grain='y') for c in (C['wood'], C['wood_light'], '#DB8F4F')]
    for i in range(21):
        x = ox - 3.1 + 0.15 + i * 0.295
        yy = -0.5 - b.rng.uniform(0, 1.2)
        while yy < 5.1:
            L = b.rng.choice((1.4, 1.8, 2.2, 2.6))
            a, e = max(yy, -0.5), min(yy + L, 5.1)
            if e - a > 0.05:
                b.box('Plank', (x, (a + e) / 2, -0.03), (0.285, e - a - 0.012, 0.06), woods[(i * 7 + int(yy * 3)) % 3], r=0.012, seg=2)
            yy += L
    y0, y1 = shell(b, ox, 'Shop', (1.0, 2.6), (1.05, 2.35), b.mat('paint', C['plaster'], noise=0.04))
    # A wainscot of upright boards with a turquoise rail, on three walls (the left one under and beside the window).
    wain = [b.mat('wood', c, grain='z') for c in ('#7BD3C7', '#86DACE')]
    for k in range(37):
        b.box('Wainscot', (ox - 3.05 + 0.09 + k * 0.166, y1 - 0.12, 0.5), (0.158, 0.04, 1.0), wain[k % 2], r=0.012, seg=2)
    for k in range(34):
        yy = y0 + 0.09 + k * 0.166
        b.box('Wainscot', (ox - 3.08, yy, 0.5), (0.04, 0.158, 1.0), wain[k % 2], r=0.012, seg=2)
        b.box('Wainscot', (ox + 3.08, yy, 0.5), (0.04, 0.158, 1.0), wain[k % 2], r=0.012, seg=2)
    rail = b.mat('plastic', C['teal'])
    b.box('Rail', (ox, y1 - 0.15, 1.02), (6.2, 0.07, 0.07), rail, r=0.03)
    b.box('Rail', (ox - 3.05, (y0 + y1) / 2, 1.02), (0.07, y1 - y0, 0.07), rail, r=0.03)
    b.box('Rail', (ox + 3.05, (y0 + y1) / 2, 1.02), (0.07, y1 - y0, 0.07), rail, r=0.03)
    b.box('Skirting', (ox, y1 - 0.12, 0.05), (6.2, 0.06, 0.1), b.mat('plastic', C['teal_dark']), r=0.02)
    # Chunky beams across the ceiling.
    for yy in (0.4, 1.9, 3.4, 4.9):
        b.box('Beam', (ox, yy, H - 0.1), (6.3, 0.22, 0.2), b.mat('wood', C['beam'], grain='x'), r=0.05)
    # The window in the left wall, its sill with two little pots.
    window(b, ox - 3.1, 1.8, 1.05, 2.35, 1.6)
    for k, col in enumerate((C['yellow'], C['pink'])):
        b.cyl('Sill pot', (ox - 2.9, 1.35 + k * 0.85, 1.07), 0.07, 0.12, b.mat('plastic', col), r=0.02)
        b.ball('Sill leaves', (ox - 2.9, 1.35 + k * 0.85, 1.2), (0.09, 0.09, 0.08), b.mat('foliage', C['lime']), seg=20)
    # The sign: a chunky board with its turquoise border and raised letters, a star on either side.
    b.box('Sign border', (ox - 0.9, y1 - 0.14, 2.62), (2.5, 0.06, 0.7), rail, r=0.06)
    b.box('Sign board', (ox - 0.9, y1 - 0.17, 2.62), (2.34, 0.06, 0.56), b.mat('wood', C['beam'], grain='x'), r=0.05)
    b.text('Evolution Shop', (ox - 0.9, y1 - 0.22, 2.6), 0.32, b.mat('plastic', C['cream']))
    for s in (-1, 1):
        b.ball('Sign stud', (ox - 0.9 + s * 1.1, y1 - 0.21, 2.62), (0.05, 0.03, 0.05), b.mat('metal', C['yellow']))
    # The shelf over the tank with jars and boxes.
    b.box('Shelf', (ox - 1.0, y1 - 0.3, 1.98), (1.7, 0.3, 0.05), b.mat('wood', C['wood_dark'], grain='x'), r=0.02)
    for s in (-1, 1):
        b.box('Bracket', (ox - 1.0 + s * 0.7, y1 - 0.2, 1.9), (0.05, 0.16, 0.14), rail, r=0.02)
    for k, col in enumerate((BLOOM, FLOW, C['yellow'], C['lilac'], C['lime'])):
        jx = ox - 1.65 + k * 0.28
        b.cyl('Shelf jar', (jx, y1 - 0.3, 2.11), 0.08, 0.2, b.mat('glass', '#F2FCFF', alpha=0.18), r=0)
        b.cyl('Jar fill', (jx, y1 - 0.3, 2.08), 0.068, 0.12, b.mat('liquid', col, glow=0.35), r=0.02)
        b.cyl('Jar lid', (jx, y1 - 0.3, 2.225), 0.084, 0.035, b.mat('plastic', (C['teal'], C['coral'], C['teal'], C['yellow'], C['coral'])[k]), r=0.012)
    # The Starter tank, the counter, a big plant, a rug, two lamps.
    tank(b, (ox - 1.25, 3.1))
    counter(b, (ox + 1.9, 2.0), 2.0, 'y')
    plant(b, (ox - 2.5, 0.55), 1.0)
    b.cyl('Rug', (ox + 0.9, 2.0, 0.006), 0.95, 0.012, b.mat('fabric', C['teal_light']), r=0)
    b.cyl('Rug ring', (ox + 0.9, 2.0, 0.01), 0.75, 0.012, b.mat('fabric', C['cream']), r=0)
    b.cyl('Rug heart', (ox + 0.9, 2.0, 0.014), 0.5, 0.012, b.mat('fabric', C['yellow']), r=0)
    pendant(b, (ox - 1.2, 2.2, 2.55), H)
    pendant(b, (ox + 1.0, 3.6, 2.6), H)
    # The door to the back room: a turquoise frame, a wooden door with a round window.
    dx = ox + 2.05
    b.box('Door frame', (dx, y1 - 0.12, 1.15), (1.2, 0.08, 2.4), rail, r=0.05)
    b.box('Door', (dx, y1 - 0.16, 1.1), (0.98, 0.06, 2.18), b.mat('wood', C['wood'], grain='z'), r=0.04)
    b.torus('Door porthole', (dx, y1 - 0.2, 1.6), 0.2, 0.04, b.mat('plastic', C['yellow']), rot=(90, 0, 0))
    b.cyl('Door glass', (dx, y1 - 0.195, 1.6), 0.2, 0.01, b.mat('emit', '#D9F6F2', glow=0.6), r=0, rot=(90, 0, 0))
    b.ball('Door knob', (dx - 0.36, y1 - 0.22, 1.05), 0.045, b.mat('metal', C['yellow']))
    # A poster of a creature between the shelf and the door, a clock on the right wall, crates by the door.
    px = ox + 0.75
    b.box('Poster frame', (px, y1 - 0.13, 1.85), (0.62, 0.04, 0.8), b.mat('plastic', C['coral']), r=0.03)
    b.box('Poster', (px, y1 - 0.155, 1.85), (0.52, 0.02, 0.7), b.mat('paint', '#FFF0B8'), r=0.01)
    b.ball('Poster body', (px, y1 - 0.17, 1.8), (0.17, 0.02, 0.14), b.mat('plastic', C['teal']))
    b.ball('Poster eye', (px + 0.06, y1 - 0.185, 1.84), (0.04, 0.01, 0.04), b.mat('plastic', C['white']))
    b.ball('Poster pupil', (px + 0.07, y1 - 0.19, 1.84), (0.018, 0.01, 0.018), b.mat('plastic', C['ink']))
    b.ball('Poster tail', (px - 0.19, y1 - 0.17, 1.8), (0.06, 0.02, 0.09), b.mat('plastic', C['teal_dark']))
    for k in range(3):
        b.ball('Poster bubble', (px + 0.15 - k * 0.03, y1 - 0.17, 1.98 + k * 0.06), (0.025 + k * 0.006, 0.01, 0.025 + k * 0.006), b.mat('plastic', '#BDEFE8'))
    b.cyl('Clock', (ox + 3.06, 3.6, 2.35), 0.24, 0.06, b.mat('plastic', C['teal']), r=0.025, rot=(0, 90, 0))
    b.cyl('Clock face', (ox + 3.03, 3.6, 2.35), 0.19, 0.01, b.mat('plastic', C['cream']), r=0, rot=(0, 90, 0))
    b.box('Clock hand', (ox + 3.02, 3.6, 2.4), (0.01, 0.02, 0.12), b.mat('plastic', C['ink']), r=0.004)
    b.box('Clock hand', (ox + 3.02, 3.64, 2.35), (0.01, 0.09, 0.02), b.mat('plastic', C['ink']), r=0.004)
    for k, (cz, s) in enumerate(((0.22, 0.44), (0.62, 0.36))):
        cx_, cy_ = ox + 2.7 - k * 0.05, 4.4
        b.box('Crate', (cx_, cy_, cz), (s, s, s), b.mat('wood', C['wood_light'], grain='x'), r=0.025)
        for sgn in (-1, 1):
            b.box('Crate slat', (cx_ - s / 2 - 0.005, cy_ + sgn * s * 0.32, cz), (0.02, 0.06, s * 0.96), b.mat('wood', C['wood_dark'], grain='z'), r=0.008)
        b.cyl('Crate label', (cx_ - s / 2 - 0.012, cy_, cz), s * 0.18, 0.01, b.mat('plastic', C['yellow']), r=0, rot=(0, 90, 0))
    # Light: the room's soft fill from the open front, a cool bounce from the window side.
    b.light('AREA', 'Shop fill', (ox, -0.3, 2.4), 150, '#FFF1DE', size=(5.0, 2.0), rot=(90, 0, 0))
    b.light('AREA', 'Shop bounce', (ox, 2.4, H - 0.3), 50, '#FFF4E6', size=(4.0, 4.0))


def lab(b):
    ox = 200
    H = 3.3
    # The floor: glossy cream tiles with turquoise ones in a pattern.
    for i in range(-6, 7):
        for j in range(0, 11):
            col = '#4FCDBE' if (i + j) % 4 == 0 else ('#FFF1DC' if (i + j) % 2 else '#FFE4C4')
            b.box('Tile', (ox + i * 0.5, j * 0.5 - 0.25, -0.03), (0.49, 0.49, 0.06), b.mat('plastic', col, rough=0.25), r=0.02, seg=2)
    y0, y1 = shell(b, ox, 'Lab', (1.2, 2.5), (1.15, 2.3), b.mat('paint', C['plaster_lab'], noise=0.03))
    # Rounded wall panels up to the rail.
    panel = b.mat('plastic', '#8FDDD0', rough=0.35)
    for k in range(7):
        b.box('Wall panel', (ox - 2.6 + k * 0.87, y1 - 0.12, 0.55), (0.82, 0.05, 0.9), panel, r=0.05)
    for k in range(6):
        yy = y0 + 0.45 + k * 0.9
        if not (1.0 < yy < 2.7): b.box('Wall panel', (ox - 3.08, yy, 0.55), (0.05, 0.84, 0.9), panel, r=0.05)
        b.box('Wall panel', (ox + 3.08, yy, 0.55), (0.05, 0.84, 0.9), panel, r=0.05)
    b.box('Wall panel', (ox - 3.08, 1.85, 0.57), (0.05, 1.4, 0.98), panel, r=0.05)
    rail = b.mat('plastic', C['teal'])
    b.box('Rail', (ox, y1 - 0.15, 1.05), (6.2, 0.07, 0.07), rail, r=0.03)
    b.box('Rail', (ox + 3.05, (y0 + y1) / 2, 1.05), (0.07, y1 - y0, 0.07), rail, r=0.03)
    window(b, ox - 3.1, 1.85, 1.15, 2.3, 1.3)
    # The ceiling's light strip.
    b.box('Light housing', (ox, 2.4, H - 0.04), (0.5, 4.2, 0.08), b.mat('plastic', C['cream']), r=0.03)
    b.box('Light strip', (ox, 2.4, H - 0.085), (0.36, 4.0, 0.01), b.mat('emit', '#F5FCFF', glow=3.5), r=0, shadow=False)
    # Two growers on the back wall, their pipes running up and along the wall to the incubator's dome.
    gy = 4.25
    for k, (gx, col) in enumerate(((ox - 1.9, BLOOM), (ox - 0.95, FLOW))):
        zz = 2.3 + k * 0.12
        px = ox + 0.95 + k * 0.11
        grower(b, (gx, gy), col, pipe_to=[(gx + 0.42, gy, 1.46), (gx + 0.42, gy, zz), (gx + 0.42, y1 - 0.26, zz), (px, y1 - 0.26, zz),
                                           (px, 3.6, zz), (px, 3.6, 1.12)])
        for bx in (gx + 0.85, ox + 0.3):
            b.box('Pipe clip', (bx, y1 - 0.2, zz), (0.06, 0.1, 0.1), b.mat('plastic', C['teal_dark']), r=0.02)
    incubator(b, (ox + 1.0, 3.6))
    # The pinboard, the vial rack on the left wall, a bench on the right with flasks, a plant.
    b.box('Pinboard frame', (ox + 2.25, y1 - 0.13, 1.85), (1.0, 0.05, 0.75), b.mat('plastic', C['coral']), r=0.04)
    b.box('Pinboard', (ox + 2.25, y1 - 0.16, 1.85), (0.88, 0.03, 0.63), b.mat('paint', '#D9A672', noise=0.1), r=0.01)
    for k, col in enumerate(('#FFF6C9', '#D7F5FF', '#FFD9E6')):
        nx = ox + 1.97 + k * 0.28
        b.box('Note', (nx, y1 - 0.18, 1.88 - (k % 2) * 0.08), (0.22, 0.01, 0.26), b.mat('paint', col), r=0.004, rot=(0, (k - 1) * 4, 0))
        b.ball('Pin', (nx, y1 - 0.195, 1.99 - (k % 2) * 0.08), 0.022, b.mat('plastic', (C['coral'], C['teal'], C['yellow'])[k]), seg=12)
    b.box('Vial rack', (ox - 3.0, 3.75, 1.45), (0.22, 1.1, 0.06), b.mat('plastic', C['cream']), r=0.02)
    b.box('Vial rack back', (ox - 3.07, 3.75, 1.6), (0.04, 1.1, 0.34), rail, r=0.02)
    for k, col in enumerate((BLOOM, FLOW, C['yellow'], C['lilac'], C['lime'], BLOOM)):
        vy = 3.3 + k * 0.18
        b.cyl('Vial', (ox - 2.98, vy, 1.6), 0.045, 0.26, b.mat('glass', '#F2FCFF', alpha=0.2), r=0)
        b.cyl('Vial fill', (ox - 2.98, vy, 1.56), 0.036, 0.16, b.mat('liquid', col, glow=0.7), r=0.012)
        b.cyl('Vial cap', (ox - 2.98, vy, 1.745), 0.048, 0.03, b.mat('plastic', C['teal_dark']), r=0.01)
    b.box('Bench', (ox + 2.75, 1.6, 0.85), (0.7, 1.6, 0.07), b.mat('plastic', C['cream']), r=0.03)
    b.box('Bench body', (ox + 2.75, 1.6, 0.42), (0.62, 1.5, 0.78), b.mat('wood', C['wood'], grain='z'), r=0.04)
    for k in range(2):
        b.box('Bench drawer', (ox + 2.43, 1.25 + k * 0.7, 0.62), (0.03, 0.6, 0.26), b.mat('plastic', C['teal']), r=0.02)
        b.ball('Drawer knob', (ox + 2.41, 1.25 + k * 0.7, 0.62), 0.03, b.mat('plastic', C['yellow']), seg=16)
    for k, col in enumerate((C['lime'], C['pink'], FLOW)):
        fy = 1.1 + k * 0.4
        b.ball('Flask', (ox + 2.75, fy, 1.0), 0.11, b.mat('glass', '#F2FCFF', alpha=0.18), shadow=False)
        b.ball('Flask fill', (ox + 2.75, fy, 0.97), (0.09, 0.09, 0.06), b.mat('liquid', col, glow=0.8))
        b.cyl('Flask neck', (ox + 2.75, fy, 1.15), 0.035, 0.12, b.mat('glass', '#F2FCFF', alpha=0.2), r=0)
    plant(b, (ox + 2.6, 4.55), 0.9)
    b.light('AREA', 'Lab strip light', (ox, 2.4, H - 0.12), 110, '#F2FAFF', size=(0.5, 4.0))
    b.light('AREA', 'Lab fill', (ox, -0.3, 2.4), 90, '#FFF6EA', size=(5.0, 2.0), rot=(90, 0, 0))


def outdoors(b):
    """A sunny landscape beyond the windows: rolling hills, round trees, puffy clouds."""
    def tree(x, y, s, trunk=True):
        if trunk: b.cyl('Trunk', (x, y, 0.9 * s), 0.16 * s, 1.8 * s, b.mat('wood', C['beam'], grain='z'), r=0.05 * s)
        for k, (dx, dy, dz, r, col) in enumerate(((0, 0, 2.4, 1.15, C['green']), (-0.7, 0.3, 1.9, 0.8, C['lime']),
                                                  (0.7, -0.2, 2.0, 0.85, C['green_dark']), (0.1, -0.5, 2.9, 0.7, C['lime']))):
            b.ball('Canopy', (x + dx * s, y + dy * s, dz * s), r * s, b.mat('foliage', col), seg=28)
    def hill(x, y, rx, ry, rz, col):
        b.ball('Hill', (x, y, -0.2), (rx, ry, rz), b.mat('foliage', col), seg=48)
    for ox in (0, 200):
        # Seen through each room's left window: toward -x, a little toward +y.
        base = ox - 3.4
        bpy.ops.mesh.primitive_plane_add(size=1, location=(ox - 50, 10, -0.06)); g = bpy.context.object; g.scale = (120, 160, 1)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        g.data.materials.append(b.mat('ground', '#7CCF4E'))
        tree(base - 4.0, 6.0, 1.0); tree(base - 7.5, 11.5, 1.3); tree(base - 5.5, 1.0, 0.8)
        b.ball('Bush', (base - 2.0, 3.2, 0.3), (0.7, 0.6, 0.5), b.mat('foliage', C['lime']), seg=28)
        b.ball('Bush', (base - 2.6, 0.2, 0.25), (0.6, 0.5, 0.45), b.mat('foliage', C['green']), seg=28)
        for k in range(6):
            b.box('Fence post', (base - 3.2, -1.0 + k * 1.3, 0.45), (0.12, 0.12, 0.9), b.mat('wood', C['wood_light'], grain='z'), r=0.04)
        b.box('Fence rail', (base - 3.2, 2.25, 0.62), (0.06, 6.6, 0.1), b.mat('wood', C['wood_light'], grain='y'), r=0.03)
        hill(base - 22, 16, 14, 10, 4.5, C['green']); hill(base - 30, 2, 16, 12, 6.0, '#6CC24A'); hill(base - 18, 30, 12, 9, 5.0, C['lime'])
        for k, (cx, cy, cz, s) in enumerate(((base - 40, 18, 16, 3.0), (base - 45, 2, 19, 2.4), (base - 38, 34, 14, 2.8))):
            for j in range(5):
                b.ball('Cloud', (cx + b.rng.uniform(-1, 1), cy + (j - 2) * s * 0.7, cz + b.rng.uniform(-0.4, 0.6)),
                       (s * 0.8, s * 0.75, s * 0.55), b.mat('liquid', '#FFFFFF', glow=0.7), seg=24, shadow=False)


def stations_sheet(b):
    ox = 400
    # A soft backdrop: a floor curving up into a back wall.
    me = bpy.data.meshes.new('Backdrop')
    prof = [(-12.0, 0.0), (2.0, 0.0), (2.0, 10.0)]
    verts = [(ox + sx, py, pz) for sx in (-16, 16) for (py, pz) in prof]
    faces = [(0, 1, 4, 3), (1, 2, 5, 4)]
    me.from_pydata(verts, [], faces); me.update()
    bd = bpy.data.objects.new('Backdrop', me); bpy.context.scene.collection.objects.link(bd)
    bd.data.materials.append(b.mat('paint', '#FFD7A6'))
    mod = bd.modifiers.new('Bevel', 'BEVEL'); mod.width = 1.6; mod.segments = 24; mod.limit_method = 'ANGLE'; mod.affect = 'EDGES'
    for p in bd.data.polygons: p.use_smooth = True
    grower(b, (ox - 3.05, 0.25), BLOOM)
    grower(b, (ox - 2.3, 0.4), FLOW)
    incubator(b, (ox - 1.15, 0.15))
    tank(b, (ox + 0.85, 0.5))
    counter(b, (ox + 3.15, 0.4), 0.95, 'x')
    b.light('AREA', 'Key', (ox - 4.0, -4.5, 4.5), 160, '#FFF1DC', size=3.0, aim=(ox, 0.3, 0.8))
    b.light('AREA', 'Rim', (ox + 1.5, 1.4, 2.6), 110, '#DDF6FF', size=1.5, aim=(ox + 1.5, -0.4, 0.8))
    b.light('AREA', 'Fill', (ox + 4.5, -5.0, 2.0), 40, '#FFFFFF', size=4.0, aim=(ox, 0.3, 0.8))


# ==================================================================================================== render
def setup(scene):
    scene.render.engine = 'BLENDER_EEVEE'
    e = scene.eevee
    e.taa_render_samples = 16 if args.quick else 96
    e.use_raytracing = True; e.ray_tracing_method = 'SCREEN'
    e.use_fast_gi = True; e.fast_gi_method = 'GLOBAL_ILLUMINATION'
    e.use_shadows = True; e.shadow_ray_count = 2; e.shadow_step_count = 8
    e.ray_tracing_options.use_denoise = True
    scene.view_settings.view_transform = 'AgX'; scene.view_settings.look = 'AgX - Punchy'
    scene.view_settings.exposure = 0.15
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.resolution_percentage = 50 if args.quick else 100
    # The sky: a gradient from a pale horizon to a clear blue.
    world = bpy.data.worlds.new('Sky'); scene.world = world; world.use_nodes = True
    nt = world.node_tree; bg = nt.nodes['Background']
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = lin(C['horizon'])
    ramp.color_ramp.elements[1].position = 0.45; ramp.color_ramp.elements[1].color = lin(C['sky'])
    nt.links.new(tc.outputs['Generated'], sep.inputs[0]); nt.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.0
    # The sun, low enough to pour through the left windows onto the floors.
    sun = bpy.data.lights.new('Sun', 'SUN'); sun.energy = 11.0; sun.color = lin('#FFE4BA')[:3]; sun.angle = math.radians(3)
    so = bpy.data.objects.new('Sun', sun); so.rotation_euler = (0, math.radians(-45.6), math.radians(29.4)); scene.collection.objects.link(so)
    # A soft bloom on what glows.
    ng = bpy.data.node_groups.new('Bloom', 'CompositorNodeTree')
    ng.interface.new_socket('Image', in_out='OUTPUT', socket_type='NodeSocketColor')
    rl = ng.nodes.new('CompositorNodeRLayers'); gl = ng.nodes.new('CompositorNodeGlare'); out = ng.nodes.new('NodeGroupOutput')
    gl.inputs['Type'].default_value = 'Bloom'; gl.inputs['Threshold'].default_value = 1.2; gl.inputs['Strength'].default_value = 0.35
    gl.inputs['Size'].default_value = 0.55
    ng.links.new(rl.outputs['Image'], gl.inputs['Image']); ng.links.new(gl.outputs[0], out.inputs[0])
    scene.compositing_node_group = ng


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    setup(scene)
    b = B()
    shots = [s for s in ('shop', 'lab', 'stations') if not args.only or s in args.only.split(',')]
    outdoors(b)
    if 'shop' in shots: shop(b)
    if 'lab' in shots: lab(b)
    if 'stations' in shots: stations_sheet(b)
    cam = bpy.data.objects.new('Eye', bpy.data.cameras.new('Eye')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.clip_start = 0.1
    for shot in shots:
        if shot == 'stations':
            cam.data.sensor_fit = 'AUTO'
            cam.data.lens = 40
            cam.location = (400.2, -8.0, 1.7); target = (400.2, 0.3, 0.8)
        else:
            ox = 0 if shot == 'shop' else 200
            cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(62)
            cam.location = (ox + 1.0, -0.6, 1.65)
            target = (ox - 0.4, 4.6, 1.15) if shot == 'shop' else (ox + 0.2, 4.6, 1.0)
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.view_settings.exposure = {'shop': -0.1, 'lab': -0.25, 'stations': -0.75}[shot]
        scene.render.filepath = str(OUT / ('ranch-' + shot + '.png'))
        bpy.ops.render.render(write_still=True)
        print('RENDERED', scene.render.filepath)


main()
