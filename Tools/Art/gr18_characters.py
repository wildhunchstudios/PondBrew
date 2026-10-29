# GR-18 look development (not game art yet): the new game's people in the approved look (GR.48): the four kinds of visitor
# and the exhibition's guests, the three helpers (Pip, Nori and Moss) and the player's hands. They are glossy cartoon
# figures of rounded rigid parts on a joint skeleton (root, hips, spine, head, shoulders, elbows, wrists, hips, knees,
# ankles), so the same joints and the same motions can drive them in the game. Original and local (Blender 5.0, EEVEE with
# ray tracing), built on GR-16's look script (its palette, materials, rooms, lights and render settings, executed up to
# its main()).
#   blender --background --factory-startup --python Tools/Art/gr18_characters.py -- --repo C:/dev/DumbVideoGameStudio --out <folder> [--quick] [--only visitors,staff]
# Stills: cast-visitors, cast-staff, face-<id> (one per character), variety, builds, shop, lab, hands-carry, hands-siphon.
# Clips (MP4): walk, staff-work, counter.
import bpy, math, random, sys, argparse
from pathlib import Path
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument('--repo', default='C:/dev/DumbVideoGameStudio')
ap.add_argument('--out', required=True)
ap.add_argument('--only', default='')
ap.add_argument('--quick', action='store_true')
ap.add_argument('--frames', type=int, default=0)  # clips: render only this many frames (a test)
ap.add_argument('--jpg', action='store_true')  # stills as JPEG (quality 90) for the board
ap.add_argument('--peek', type=int, default=0)  # clips: render this many evenly spaced frames as stills instead (a look)
args = ap.parse_args(argv)
OUT = Path(args.out); OUT.mkdir(parents=True, exist_ok=True)

# ---------------------------------------------------------------------------------------------------- the approved look
LOOK = Path(args.repo) / 'Tools' / 'Art' / 'gr16_look_ranch.py'
_src = LOOK.read_text(encoding='utf-8')
_src = _src[:_src.index('\ndef main():')]
_saved = sys.argv
sys.argv = ['gr16_look', '--', '--repo', args.repo, '--out', str(OUT)] + (['--quick'] if args.quick else [])
R = {'__name__': 'gr16_look'}
exec(compile(_src, str(LOOK), 'exec'), R)
sys.argv = _saved
B, C, lin = R['B'], R['C'], R['lin']
BLOOM, FLOW = R['BLOOM'], R['FLOW']

SKIN = ['#FFDCC2', '#F7C6A0', '#EFB088', '#D99467', '#B87448', '#93583A', '#6E412B']
HAIR = dict(black='#2E2430', brown='#6B4329', chestnut='#8E4F2A', auburn='#B5502F', blond='#F2C45A', ginger='#E5793B',
            grey='#C9CBD3', white='#F1F1F4', teal='#2FB9AE', pink='#FF8FB8', lilac='#A98BFF', moss='#7FB35A', navy='#33456F')
IRIS = ['#5A3A24', '#7A5230', '#3E6FB0', '#3F7F4F', '#6E7682', '#2E2430']
INK = C['ink']
LIP = '#9A3448'


def smooth(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


def smoothstep(a, b, x):
    return smooth((x - a) / (b - a)) if b != a else (1.0 if x >= a else 0.0)


def M(b, kind, color, **kw):
    """The look's materials, and the people's own: skin, cloth, hair, gloss (shoes, bags, hats) and eye."""
    if kind not in ('skin', 'cloth', 'hair', 'gloss', 'eye'):
        return b.mat(kind, color, **kw)
    key = ('gr18 ' + kind, color, tuple(sorted(kw.items())))
    if key in b.mats: return b.mats[key]
    m = bpy.data.materials.new(kind + ' ' + color); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = lin(color)
    if kind == 'skin':
        p.inputs['Roughness'].default_value = 0.42
        p.inputs['Subsurface Weight'].default_value = 0.18
        p.inputs['Subsurface Radius'].default_value = (1.0, 0.45, 0.3)
        p.inputs['Subsurface Scale'].default_value = 0.02
        p.inputs['Coat Weight'].default_value = 0.22; p.inputs['Coat Roughness'].default_value = 0.25
    elif kind == 'cloth':
        p.inputs['Roughness'].default_value = 0.58; p.inputs['Sheen Weight'].default_value = 0.35
        p.inputs['Coat Weight'].default_value = 0.15; p.inputs['Coat Roughness'].default_value = 0.3
    elif kind == 'hair':
        p.inputs['Roughness'].default_value = 0.3; p.inputs['Coat Weight'].default_value = 0.65; p.inputs['Coat Roughness'].default_value = 0.1
    elif kind == 'gloss':
        p.inputs['Roughness'].default_value = 0.22; p.inputs['Coat Weight'].default_value = 0.85; p.inputs['Coat Roughness'].default_value = 0.05
    elif kind == 'eye':
        p.inputs['Roughness'].default_value = 0.1; p.inputs['Coat Weight'].default_value = 1.0; p.inputs['Coat Roughness'].default_value = 0.02
    b.mats[key] = m
    return m


# ---------------------------------------------------------------------------------------------------- shapes
def blob(b, name, center, radii, m, top=1.0, bottom=1.0, front=0.0, back=0.0, seg=40, rot=(0, 0, 0), shadow=True):
    """A rounded body: a sphere tapered from bottom to top, with a belly pushed forward (-Y) or a back pushed out."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=center, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.name = name
    for v in o.data.vertices:
        x, y, z = v.co
        s = bottom + (top - bottom) * (z + 1) / 2
        g = math.exp(-((z + 0.1) / 0.6) ** 2)
        yy = y * s - front * max(0.0, -y) * g + back * max(0.0, y) * g
        v.co = (x * s * radii[0], yy * radii[1], z * radii[2])
    b._finish(o, m, normals=False)
    o.visible_shadow = shadow
    return o


def curve(b, name, pts, radii, m, poly=False, caps=True, res=5):
    """A tube through points, its radius per point (limbs, straps, brows, mouths)."""
    if not isinstance(radii, (list, tuple)): radii = [radii] * len(pts)
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = 1.0; cu.bevel_resolution = res; cu.use_fill_caps = caps
    if poly:
        sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
        for p, pt, r in zip(sp.points, pts, radii): p.co = (pt[0], pt[1], pt[2], 1); p.radius = r
    else:
        sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts) - 1)
        for p, pt, r in zip(sp.bezier_points, pts, radii):
            p.co = pt; p.handle_left_type = 'AUTO'; p.handle_right_type = 'AUTO'; p.radius = r
    sp.use_smooth = True
    o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
    cu.materials.append(m)
    return o


def tri(b, name, pts, depth, m):
    """A flat shape (a V-neck, a sash end, a ribbon tail) from points in a plane, thickened along its normal."""
    me = bpy.data.meshes.new(name)
    n = len(pts)
    p = [Vector(q) for q in pts]
    nrm = (p[1] - p[0]).cross(p[2] - p[0]).normalized() * (depth / 2)
    verts = [tuple(q + nrm) for q in p] + [tuple(q - nrm) for q in p]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(m)
    mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = depth * 0.45; mod.segments = 3
    for f in o.data.polygons: f.use_smooth = True
    return o


def orient(o, n, roll=0.0):
    """Turns a part so its local -Y faces along n (a feature laid on a surface)."""
    q = Vector(n).to_track_quat('-Y', 'Z')
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = q
    if roll:
        from mathutils import Quaternion
        o.rotation_quaternion = q @ Quaternion((0, 1, 0), math.radians(roll))
    return o


# ---------------------------------------------------------------------------------------------------- the skeleton
class Rig:
    """Joints (empties) chained from the root at the feet; parts made at rest with the root at the origin, then parented."""

    def __init__(self, name):
        self.name = name; self.j = {}; self.w = {}

    def joint(self, key, parent, at):
        e = bpy.data.objects.new(self.name + ' ' + key, None); e.empty_display_size = 0.04
        bpy.context.scene.collection.objects.link(e)
        at = Vector(at)
        if parent is None: e.location = at
        else: e.parent = self.j[parent]; e.location = at - self.w[parent]
        e.rotation_mode = 'XYZ'
        self.j[key] = e; self.w[key] = at
        return e

    def put(self, o, key):
        o.parent = self.j[key]
        o.location = o.location - self.w[key]
        return o

    def place(self, at, yaw=0.0):
        self.j['root'].location = (at[0], at[1], at[2] if len(at) > 2 else 0.0)
        self.yaw = yaw
        self.j['root'].rotation_euler = (0, 0, math.radians(yaw))

    def pose(self, p):
        """p: joint -> (x, y, z) degrees; 'lift' raises the hips (metres); 'yaw' adds to the root's turn."""
        for k, e in self.j.items():
            if k == 'root': continue
            e.rotation_euler = [math.radians(a) for a in p.get(k, (0, 0, 0))]
        hz = self.w['hips'].z
        self.j['hips'].location = (p.get('sway', 0.0), p.get('surge', 0.0), hz + p.get('lift', 0.0))
        self.j['root'].rotation_euler = (0, 0, math.radians(self.yaw + p.get('yaw', 0.0)))

    def key(self, frame):
        for k, e in self.j.items():
            e.keyframe_insert('rotation_euler', frame=frame)
            if k in ('hips', 'root'): e.keyframe_insert('location', frame=frame)


# ---------------------------------------------------------------------------------------------------- a person
def person(b, spec, name):
    H = spec.get('height', 1.6); heads = spec.get('heads', 3.0); bw = spec.get('build', 1.0)
    hh = H / heads
    rig = Rig(name); rig.spec = spec
    neck = 0.04 * hh
    torso_h = spec.get('torso', 1.08) * hh
    top = H - hh - neck
    bot = top - torso_h
    hip_z = bot + 0.2 * hh
    ankle_z = 0.15 * hh
    thigh = (hip_z - ankle_z) * 0.52
    rig.d = dict(H=H, hh=hh, top=top, bot=bot, hip_z=hip_z, ankle_z=ankle_z, torso_h=torso_h, neck=neck, bw=bw,
                 trx=0.4 * hh * bw, try_=0.31 * hh * bw, ar=0.125 * hh * spec.get('arms', 1.0))
    rig.tz = (top + bot) / 2 + 0.03 * hh
    rig.trz = torso_h / 2 + 0.06 * hh
    rig.tt, rig.tb = 0.74, 1.06
    sh_z = top - 0.15 * hh
    sh_x = side_x(rig, sh_z) * 0.98
    rig.d.update(sh_x=sh_x, sh_z=sh_z)
    arm = spec.get('arm', 1.0)
    up_arm, fore = arm * 0.46 * hh, arm * 0.4 * hh
    splay = math.radians(9)
    hip_x = 0.19 * hh * bw
    rig.joint('root', None, (0, 0, 0))
    rig.joint('hips', 'root', (0, 0, hip_z))
    rig.joint('spine', 'hips', (0, 0, hip_z + 0.06 * hh))
    rig.joint('head', 'spine', (0, 0, top + neck * 0.6))
    for s, side in ((1, 'L'), (-1, 'R')):
        sh = Vector((s * sh_x, 0, sh_z))
        el = sh + Vector((s * math.sin(splay) * up_arm, 0, -math.cos(splay) * up_arm))
        wr = el + Vector((s * math.sin(splay * 0.5) * fore, 0, -math.cos(splay * 0.5) * fore))
        rig.joint('shoulder.' + side, 'spine', sh)
        rig.joint('elbow.' + side, 'shoulder.' + side, el)
        rig.joint('wrist.' + side, 'elbow.' + side, wr)
        hp = Vector((s * hip_x, 0, hip_z))
        rig.joint('hip.' + side, 'hips', hp)
        rig.joint('knee.' + side, 'hip.' + side, hp - Vector((0, 0, thigh)))
        rig.joint('ankle.' + side, 'knee.' + side, Vector((s * hip_x, 0, ankle_z)))
    rig.skin = M(b, 'skin', spec.get('skin', SKIN[2]))
    rig.held = []
    torso(b, rig, spec)
    for side in ('L', 'R'):
        legs(b, rig, side, spec)
        arms(b, rig, side, spec)
        hands(b, rig, side, spec.get('hands', {}).get(side, 'relaxed'), spec)
    head(b, rig, spec)
    for ex in spec.get('extras', []):
        if ex[0] == 'held': rig.held.append(ex[1]); continue
        EXTRAS[ex[0]](b, rig, spec, **(ex[1] if len(ex) > 1 else {}))
    return rig


def front_y(rig, z):
    """How far forward (-Y) the torso's front is at height z (its blob, at x = 0)."""
    d = rig.d
    zc, rz = rig.tz, rig.trz
    u = max(-1.0, min(1.0, (z - zc) / rz))
    s = rig.tb + (rig.tt - rig.tb) * (u + 1) / 2
    return -math.sqrt(max(0.0, 1 - u * u)) * s * d['try_']


def side_x(rig, z):
    d = rig.d
    u = max(-1.0, min(1.0, (z - rig.tz) / rig.trz))
    s = rig.tb + (rig.tt - rig.tb) * (u + 1) / 2
    return math.sqrt(max(0.0, 1 - u * u)) * s * d['trx']


def ring(b, rig, key, name, z, m, minor, grow=1.0):
    """A band around the torso at height z (a hem, a belt, a collar), following its oval."""
    rx, ry = side_x(rig, z) * grow, -front_y(rig, z) * grow
    o = b.torus(name, (0, 0, z), rx, minor, m)
    o.scale = (1, ry / rx, 1)
    return rig.put(o, key)


def panel(b, rig, name, z0, z1, span, grow, m, thick, rad=None, rows=10, cols=14):
    """A curved panel lying on the torso (or on any oval given by rad(z)), from z0 down to z1: span(u) gives its angles
    (radians round from the front) at u = 0 (top) to 1 (bottom): an apron's bib, a lapel, a V-neck, an apron's skirt."""
    rad = rad or (lambda z: (side_x(rig, z), -front_y(rig, z)))
    verts, faces = [], []
    for i in range(rows + 1):
        u = i / rows; z = z0 + (z1 - z0) * u
        rx, ry = rad(z); rx *= grow; ry *= grow
        a0, a1 = span(u)
        for j in range(cols + 1):
            a = a0 + (a1 - a0) * j / cols
            verts.append((rx * math.sin(a), -ry * math.cos(a), z))
    for i in range(rows):
        for j in range(cols):
            k = i * (cols + 1) + j
            faces.append((k, k + 1, k + cols + 2, k + cols + 1))
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(m)
    so = o.modifiers.new('Thick', 'SOLIDIFY'); so.thickness = thick; so.offset = 0.0; so.use_even_offset = True
    sub = o.modifiers.new('Soft', 'SUBSURF'); sub.levels = 1; sub.render_levels = 2
    for f in o.data.polygons: f.use_smooth = True
    return o


def torso(b, rig, spec):
    d = rig.d; hh = d['hh']; top = spec['top']; kind = top['kind']
    tm = M(b, 'cloth', top['color'])
    belly = spec.get('belly', 0.0)
    rig.put(blob(b, 'Torso', (0, 0, rig.tz), (d['trx'], d['try_'], rig.trz), tm, top=rig.tt, bottom=rig.tb, front=belly * 0.28), 'spine')
    bottom = spec['bottom']
    bm = M(b, 'cloth', bottom['color'])
    rig.put(blob(b, 'Pelvis', (0, 0, d['hip_z'] - 0.01 * hh), (0.37 * hh * d['bw'], 0.29 * hh * d['bw'], 0.19 * hh), bm, top=0.95, bottom=0.92), 'hips')
    rig.put(b.cyl('Neck', (0, 0, d['top'] + d['neck'] * 0.4), 0.13 * hh, d['neck'] + 0.16 * hh, rig.skin, r=0.01), 'head')
    acc = M(b, 'cloth', top.get('accent', top['color']))
    hem_z = rig.tz - rig.trz + 0.05 * hh
    if kind == 'hoodie':
        rig.put(b.box('Pocket', (0, front_y(rig, d['bot'] + 0.16 * hh) - 0.008, d['bot'] + 0.16 * hh), (0.38 * hh, 0.03 * hh, 0.17 * hh), acc, r=0.03), 'spine')
        rig.put(blob(b, 'Hood', (0, 0.17 * hh * d['bw'], d['top'] + 0.01 * hh), (0.27 * hh, 0.13 * hh, 0.12 * hh), acc), 'spine')
        rig.put(b.torus('Collar', (0, -0.01 * hh, d['top'] + 0.005 * hh), 0.14 * hh, 0.05 * hh, acc), 'spine')
        trim = M(b, 'cloth', top.get('trim', C['cream']))
        for s in (-1, 1):
            zt = d['top'] - 0.02 * hh
            rig.put(curve(b, 'Drawstring', [(s * 0.06 * hh, front_y(rig, zt) - 0.01, zt), (s * 0.07 * hh, front_y(rig, zt - 0.1 * hh) - 0.012, zt - 0.1 * hh),
                                            (s * 0.065 * hh, front_y(rig, zt - 0.19 * hh) - 0.012, zt - 0.19 * hh)], 0.008 * hh / 0.49 * 0.8 + 0.004, trim), 'spine')
            rig.put(b.ball('Aglet', (s * 0.065 * hh, front_y(rig, zt - 0.2 * hh) - 0.014, zt - 0.21 * hh), 0.016 * hh, trim, seg=12), 'spine')
        ring(b, rig, 'spine', 'Hem band', hem_z, acc, 0.035 * hh, 1.0)
    elif kind in ('cardigan', 'vest', 'polo', 'coverall', 'coat', 'labcoat'):
        shirt = M(b, 'cloth', top.get('shirt', C['cream']))
        zt = d['top'] - 0.005 * hh
        if kind in ('cardigan', 'vest', 'coat', 'labcoat'):
            # A V-neck showing what is under it.
            vz = d['top'] - (0.34 if kind in ('vest', 'cardigan') else 0.42) * hh
            z0 = d['top'] + 0.04 * hh
            rig.put(panel(b, rig, 'V-neck', z0, vz, lambda u: (-0.34 * (1 - u) - 0.02, 0.34 * (1 - u) + 0.02), 1.012, M(b, 'cloth', top.get('under', top.get('shirt', C['cream']))), 0.006 * hh), 'spine')
        if kind in ('cardigan', 'coat', 'labcoat', 'vest'):
            nb = 4 if kind != 'vest' else 3
            but = M(b, 'gloss', top.get('buttons', C['cream'])) if top.get('buttons') != 'gold' else b.mat('metal', '#F4C04E', rough=0.22)
            for k in range(nb):
                z = d['top'] - (0.36 + k * 0.17) * hh
                if z < hem_z + 0.05 * hh: break
                for s in ((-1, 1) if kind == 'coat' else (0,)):
                    o = b.cyl('Button', (s * 0.08 * hh, front_y(rig, z) - 0.006, z), 0.03 * hh, 0.02 * hh, but, r=0.004, rot=(90, 0, 0))
                    rig.put(o, 'spine')
        if kind == 'polo':
            for s in (-1, 1):
                o = blob(b, 'Polo collar', (s * 0.085 * hh, front_y(rig, zt) - 0.01, zt + 0.01 * hh), (0.085 * hh, 0.02 * hh, 0.05 * hh), M(b, 'cloth', top.get('accent', C['white'])),
                         rot=(10, s * 25, s * -30))
                rig.put(o, 'spine')
            for k in range(2):
                z = zt - (0.08 + 0.07 * k) * hh
                rig.put(b.cyl('Button', (0, front_y(rig, z) - 0.005, z), 0.018 * hh, 0.012 * hh, M(b, 'gloss', C['cream']), r=0.003, rot=(90, 0, 0)), 'spine')
        if kind == 'coverall':
            zz = d['top'] - 0.05 * hh
            rig.put(curve(b, 'Zip', [(0, front_y(rig, z) - 0.006, z) for z in (zz, zz - 0.3 * hh, d['bot'] + 0.05 * hh)], 0.01 * hh, b.mat('metal', '#D8DCE6')), 'spine')
            z = d['top'] - 0.2 * hh
            rig.put(b.box('Chest pocket', (0.13 * hh, front_y(rig, z) - 0.01, z), (0.13 * hh, 0.025 * hh, 0.12 * hh), acc, r=0.015, rot=(0, 0, -6)), 'spine')
            ring(b, rig, 'spine', 'Belt', d['bot'] + 0.08 * hh, M(b, 'gloss', '#7A4A2E'), 0.035 * hh, 1.02)
            rig.put(b.box('Buckle', (0, front_y(rig, d['bot'] + 0.08 * hh) - 0.03 * hh, d['bot'] + 0.08 * hh), (0.08 * hh, 0.02 * hh, 0.06 * hh), b.mat('metal', '#F4C04E'), r=0.008), 'spine')
            rig.put(b.torus('Collar', (0, -0.01 * hh, d['top'] + 0.005 * hh), 0.13 * hh, 0.04 * hh, acc), 'spine')
        if kind in ('coat', 'labcoat'):
            # The skirt of the coat, from the waist to the knees, open so the legs swing inside it.
            z0 = d['bot'] + 0.1 * hh; z1 = d['hip_z'] - (d['hip_z'] - d['ankle_z']) * (0.55 if kind == 'labcoat' or top.get('long') else 0.42)
            r0, q0 = side_x(rig, z0) * 1.03, -front_y(rig, z0) * 1.05
            tails = lambda z: (r0 + 0.32 * (z0 - z), q0 + 0.36 * (z0 - z))
            gap = 0.42
            rig.put(panel(b, rig, 'Coat tails', z0, z1, lambda u: (gap + 0.1 * u, 2 * math.pi - gap - 0.1 * u), 1.0, tm, 0.014 * hh, rad=tails, rows=8, cols=30), 'hips')
            lap = M(b, 'cloth', top.get('accent', top['color']))
            for s in (-1, 1):
                def span(u, s=s):
                    inner = 0.34 * (1 - u) + 0.02
                    outer = inner + 0.3 * math.sin(math.pi * min(1.0, u * 1.2)) + 0.05
                    return (inner, outer) if s > 0 else (-outer, -inner)
                rig.put(panel(b, rig, 'Lapel', d['top'] + 0.04 * hh, d['top'] - 0.42 * hh, span, 1.03, lap, 0.012 * hh), 'spine')
            rig.put(b.torus('Collar', (0, 0.01 * hh, d['top'] + 0.01 * hh), 0.15 * hh, 0.05 * hh, lap), 'spine')
            if kind == 'labcoat':
                for s in (-1, 1):
                    z = d['bot'] + 0.08 * hh
                    rig.put(b.box('Coat pocket', (s * 0.17 * hh, front_y(rig, z) - 0.012, z), (0.15 * hh, 0.02 * hh, 0.13 * hh), lap, r=0.012), 'spine')
                z = d['top'] - 0.24 * hh
                for k, pc in enumerate((C['coral'], C['teal'], C['yellow'])):
                    rig.put(b.cyl('Pen', (0.15 * hh + k * 0.025 * hh, front_y(rig, z) - 0.02, z + 0.04 * hh), 0.01 * hh, 0.14 * hh, b.mat('plastic', pc), r=0.003), 'spine')
                rig.put(b.box('Chest pocket', (0.17 * hh, front_y(rig, z) - 0.014, z), (0.13 * hh, 0.02 * hh, 0.1 * hh), lap, r=0.01), 'spine')
        if kind not in ('coat', 'labcoat', 'coverall'):
            ring(b, rig, 'spine', 'Hem band', hem_z, acc, 0.03 * hh, 1.0)
    rig.sleeve = M(b, 'cloth', top.get('sleeve', top.get('shirt', top['color']) if kind == 'vest' else top['color']))
    rig.cuff = M(b, 'cloth', top.get('accent', top['color']) if kind != 'vest' else top.get('shirt', C['cream']))


def legs(b, rig, side, spec):
    d = rig.d; hh = d['hh']; bw = d['bw']
    hp, kn, an = rig.w['hip.' + side], rig.w['knee.' + side], rig.w['ankle.' + side]
    bottom = spec['bottom']; bm = M(b, 'cloth', bottom['color'])
    r0, r1, r2 = 0.17 * hh * bw, 0.145 * hh * min(bw, 1.1), 0.125 * hh * min(bw, 1.1)
    if bottom['kind'] == 'shorts':
        mid = hp + (kn - hp) * 0.62
        rig.put(curve(b, 'Shorts leg', [hp + Vector((0, 0, 0.05 * hh)), mid], [r0, r0 * 1.06], bm, poly=True), 'hip.' + side)
        rig.put(b.torus('Shorts hem', mid, r0 * 1.05, 0.02 * hh, M(b, 'cloth', bottom.get('accent', bottom['color']))), 'hip.' + side)
        rig.put(curve(b, 'Thigh', [mid + Vector((0, 0, 0.02 * hh)), kn], [r1 * 0.95, r1 * 0.88], rig.skin, poly=True), 'hip.' + side)
        rig.put(b.ball('Knee', kn, r1 * 0.88, rig.skin, seg=24), 'knee.' + side)
        rig.put(curve(b, 'Shin', [kn, an + Vector((0, 0, 0.03 * hh))], [r1 * 0.86, r2 * 0.8], rig.skin, poly=True), 'knee.' + side)
        socks = spec.get('socks')
        if socks:
            sm = M(b, 'cloth', socks)
            rig.put(curve(b, 'Sock', [an + Vector((0, 0, 0.02 * hh)), an + Vector((0, 0, 0.3 * hh))], [r2 * 0.86, r2 * 0.92], sm, poly=True), 'knee.' + side)
            if spec.get('sock_stripe'):
                for k in range(2):
                    rig.put(b.torus('Sock stripe', an + Vector((0, 0, (0.24 - k * 0.06) * hh)), r2 * 0.92, 0.012 * hh, M(b, 'cloth', spec['sock_stripe'])), 'knee.' + side)
    else:
        rig.put(curve(b, 'Thigh', [hp + Vector((0, 0, 0.06 * hh)), kn], [r0, r1], bm, poly=True), 'hip.' + side)
        rig.put(b.ball('Knee', kn, r1, bm, seg=24), 'knee.' + side)
        rig.put(curve(b, 'Shin', [kn, an + Vector((0, 0, 0.04 * hh))], [r1, r2], bm, poly=True), 'knee.' + side)
        if spec['shoes']['kind'] != 'boot':
            rig.put(b.torus('Cuff', an + Vector((0, 0, 0.06 * hh)), r2 * 1.04, 0.022 * hh, M(b, 'cloth', bottom.get('accent', bottom['color']))), 'knee.' + side)
    shoes(b, rig, side, spec)


def shoes(b, rig, side, spec):
    d = rig.d; hh = d['hh']; an = rig.w['ankle.' + side]
    sh = spec['shoes']; kind = sh['kind']
    up = M(b, 'gloss', sh['color']); so = M(b, 'gloss', sh.get('sole', C['cream']))
    fy = an.y - 0.08 * hh
    hh = hh * 1.2
    if kind == 'boot':
        rig.put(curve(b, 'Boot leg', [Vector((an.x, an.y, 0.08 * hh)), Vector((an.x, an.y, 0.5 * hh))], [0.125 * hh, 0.135 * hh], up, poly=True), 'ankle.' + side)
        rig.put(b.torus('Boot rim', Vector((an.x, an.y, 0.5 * hh)), 0.135 * hh, 0.022 * hh, up), 'ankle.' + side)
    length = 0.22 * hh * (1.12 if kind == 'dress' else 1.0)
    rig.put(blob(b, 'Shoe', (an.x, fy, 0.085 * hh), (0.105 * hh, length, 0.09 * hh), up if kind != 'sandal' else rig.skin, top=0.82, bottom=1.0), 'ankle.' + side)
    rig.put(b.box('Sole', (an.x, fy - 0.005 * hh, 0.022 * hh), (0.215 * hh, 2 * length + 0.01 * hh, 0.045 * hh), so, r=0.012), 'ankle.' + side)
    if kind == 'sneaker':
        ac = M(b, 'gloss', sh.get('accent', C['teal']))
        for k in range(3):
            rig.put(b.box('Lace', (an.x, fy - (0.03 + k * 0.045) * hh, (0.155 - k * 0.012) * hh), (0.1 * hh, 0.02 * hh, 0.014 * hh), M(b, 'cloth', C['cream']), r=0.004, rot=(-25, 0, 0)), 'ankle.' + side)
        s = 1 if side == 'L' else -1
        rig.put(blob(b, 'Shoe stripe', (an.x + s * 0.1 * hh, fy, 0.08 * hh), (0.012 * hh, 0.12 * hh, 0.03 * hh), ac), 'ankle.' + side)
        rig.put(blob(b, 'Toe cap', (an.x, fy - length * 0.82, 0.06 * hh), (0.085 * hh, 0.05 * hh, 0.05 * hh), so), 'ankle.' + side)
    elif kind == 'sandal':
        for k in range(2):
            rig.put(b.box('Sandal strap', (an.x, fy - (0.02 + k * 0.1) * hh, (0.12 - k * 0.03) * hh), (0.23 * hh, 0.05 * hh, 0.04 * hh), up, r=0.01, rot=(-20, 0, 0)), 'ankle.' + side)


def arms(b, rig, side, spec):
    d = rig.d; hh = d['hh']
    sh, el, wr = rig.w['shoulder.' + side], rig.w['elbow.' + side], rig.w['wrist.' + side]
    r0, r1, r2 = d['ar'] * 1.05, d['ar'] * 0.93, d['ar'] * 0.84
    sleeves = spec['top'].get('sleeves', 'long')
    rig.put(b.ball('Shoulder', sh, r0 * 1.12, rig.sleeve, seg=28), 'shoulder.' + side)
    if sleeves == 'short':
        mid = sh + (el - sh) * 0.5
        rig.put(curve(b, 'Sleeve', [sh, mid], [r0 * 1.08, r0 * 1.12], rig.sleeve, poly=True), 'shoulder.' + side)
        rig.put(b.torus('Sleeve hem', mid, r0 * 1.1, 0.018 * hh, rig.cuff), 'shoulder.' + side)
        rig.put(curve(b, 'Upper arm', [mid, el], [r1 * 0.85, r1 * 0.82], rig.skin, poly=True), 'shoulder.' + side)
        fore_m = rig.skin
    else:
        rig.put(curve(b, 'Upper arm', [sh, el], [r0, r1], rig.sleeve, poly=True), 'shoulder.' + side)
        fore_m = rig.sleeve if sleeves == 'long' else rig.skin
    rig.put(b.ball('Elbow', el, r1 if fore_m == rig.sleeve else r1 * 0.85, fore_m, seg=24), 'elbow.' + side)
    if sleeves == 'rolled':
        rig.put(b.torus('Rolled cuff', el + Vector((0, 0, 0.01 * hh)), r1 * 1.05, 0.04 * hh, rig.cuff), 'elbow.' + side)
    rr = 0.85 if fore_m != rig.sleeve else 1.0
    rig.put(curve(b, 'Forearm', [el, wr + Vector((0, 0, 0.02 * hh))], [r1 * rr, r2 * rr], fore_m, poly=True), 'elbow.' + side)
    if sleeves == 'long':
        rig.put(b.torus('Cuff', wr + Vector((0, 0, 0.035 * hh)), r2 * 1.02, 0.025 * hh, rig.cuff), 'elbow.' + side)


HAND_SHAPES = {
    # curl of each finger's two joints (index first, at the front) and the thumb's, in degrees
    'relaxed': ([(18, 28), (24, 32), (28, 36), (32, 40)], 20),
    'grip': ([(70, 80), (75, 85), (78, 88), (80, 90)], 55),
    'point': ([(2, 4), (82, 88), (86, 92), (90, 96)], 40),
    'open': ([(4, 6), (2, 4), (4, 6), (8, 10)], 5),
    'cup': ([(35, 30), (38, 32), (40, 34), (42, 36)], 30),
}


def hands(b, rig, side, shape, spec, joint=None, at=None, m=None):
    """A cartoon hand of four fingers and a thumb, hanging down from the wrist with its palm toward the body."""
    d = rig.d if rig else None
    hh = d['hh'] if d else 0.49
    s = 1 if side == 'L' else -1
    w = at if at is not None else rig.w['wrist.' + side]
    hm = m or (M(b, 'gloss', spec['gloves']) if spec.get('gloves') else rig.skin)
    k = spec.get('hand', 1.0) * 1.28
    c = w + Vector((0, 0, -0.08 * hh * k))
    parts = [b.ball('Palm', c, (0.055 * hh * k, 0.1 * hh * k, 0.095 * hh * k), hm, seg=28)]
    curls, thumb = HAND_SHAPES[shape]
    ys = [-0.062, -0.021, 0.021, 0.062]
    lengths = [0.082, 0.09, 0.085, 0.07]
    fr = 0.031 * hh * k
    for i, (y, L, (c1, c2)) in enumerate(zip(ys, lengths, curls)):
        base = Vector((c.x - s * 0.006 * hh, c.y + y * hh * k, c.z - 0.075 * hh * k))
        L *= hh * k
        a1, a2 = math.radians(c1), math.radians(c1 + c2)
        mid = base + Vector((-s * math.sin(a1), 0, -math.cos(a1))) * L * 0.55
        tip = mid + Vector((-s * math.sin(a2), 0, -math.cos(a2))) * L * 0.45
        parts.append(curve(b, 'Finger', [base, mid, tip], [fr, fr * 0.96, fr * 0.92], hm, poly=True))
        parts.append(b.ball('Fingertip', tip, fr * 0.92, hm, seg=16))
        parts.append(b.ball('Knuckle', mid, fr * 0.97, hm, seg=16))
    ta = math.radians(thumb)
    tb = c + Vector((-s * 0.035 * hh * k, -0.085 * hh * k, 0.0))
    td = Vector((-s * (0.3 + 0.5 * math.sin(ta)), -0.6 * math.cos(ta), -0.7)).normalized()
    tt = tb + td * 0.085 * hh * k
    parts.append(curve(b, 'Thumb', [tb, tt], [fr * 1.1, fr], hm, poly=True))
    parts.append(b.ball('Thumb tip', tt, fr, hm, seg=16))
    key = joint or ('wrist.' + side)
    if rig:
        for p in parts: rig.put(p, key)
    return parts


# ---------------------------------------------------------------------------------------------------- the head
def head(b, rig, spec):
    d = rig.d; hh = d['hh']
    face = spec.get('face', {})
    hc = Vector((0, 0, d['H'] - 0.5 * hh))
    rx, ry, rz = 0.45 * hh * face.get('wide', 1.0), 0.42 * hh, 0.5 * hh * face.get('tall', 1.0)
    jaw = face.get('jaw', 0.16)
    rig.hc, rig.hr, rig.jaw = hc, (rx, ry, rz), jaw
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=1, location=hc)
    o = bpy.context.object; o.name = 'Head'
    for v in o.data.vertices:
        x, y, z = v.co
        s = 1 - jaw * z * z if z < 0 else 1.0
        v.co = (x * s * rx, y * s * ry, z * rz)
    b._finish(o, rig.skin, normals=False)
    rig.put(o, 'head')

    def surf(dx, dy, dz, out=0.0):
        dv = Vector((dx, dy, dz)).normalized()
        s = 1 - jaw * dv.z * dv.z if dv.z < 0 else 1.0
        p = Vector((dv.x * s * rx, dv.y * s * ry, dv.z * rz))
        n = Vector((p.x / rx ** 2, p.y / ry ** 2, p.z / rz ** 2)).normalized()
        return hc + p + n * out, n
    rig.surf = surf

    def put(o): return rig.put(o, 'head')
    eye_kind = face.get('eyes', 'round')
    ex, ey = 0.36 * face.get('apart', 1.0), 0.06 + face.get('eye_up', 0.0)
    es = face.get('eye_size', 1.0)
    white = M(b, 'eye', '#FFFFFF'); dark = M(b, 'eye', INK)
    iris = M(b, 'eye', face.get('iris', IRIS[0]))
    look = face.get('look', (0.0, 0.0))
    for s in (-1, 1):
        if eye_kind == 'sun':
            continue
        p, n = surf(s * ex, -1, ey, -0.006 * hh)
        put(orient(b.ball('Eye white', p, (0.088 * hh * es, 0.035 * hh, 0.108 * hh * es), white, seg=28), n))
        pi, _ = surf(s * ex + look[0] * 0.08, -1, ey - 0.012 + look[1] * 0.08, 0.016 * hh)
        put(orient(b.ball('Iris', pi, (0.06 * hh * es, 0.02 * hh, 0.074 * hh * es), iris, seg=24), n))
        pp, _ = surf(s * ex + look[0] * 0.08, -1, ey - 0.012 + look[1] * 0.08, 0.026 * hh)
        put(orient(b.ball('Pupil', pp, (0.032 * hh * es, 0.012 * hh, 0.042 * hh * es), dark, seg=20), n))
        g1, _ = surf(s * ex + 0.035 + look[0] * 0.08, -1, ey + 0.04 + look[1] * 0.08, 0.036 * hh)
        put(b.ball('Glint', g1, 0.017 * hh * es, white, seg=12, shadow=False))
        g2, _ = surf(s * ex - 0.03 + look[0] * 0.08, -1, ey - 0.05 + look[1] * 0.08, 0.034 * hh)
        put(b.ball('Glint', g2, 0.008 * hh * es, white, seg=10, shadow=False))
        if eye_kind == 'lidded':
            pl, nl = surf(s * ex, -1, ey + 0.085, 0.004 * hh)
            put(orient(b.ball('Eyelid', pl, (0.098 * hh * es, 0.045 * hh, 0.062 * hh * es), rig.skin, seg=24), nl))
            lash = [surf(s * ex + t * 0.11, -1, ey + 0.035 - 0.03 * t * t, 0.03 * hh)[0] for t in (-1, -0.5, 0, 0.5, 1)]
            put(curve(b, 'Lash line', lash, 0.011 * hh, dark))
        if face.get('lashes'):
            for t in (0.75, 1.05):
                q, nq = surf(s * (ex + 0.07 * t), -1, ey + 0.07 + 0.05 * t, 0.02 * hh)
                put(curve(b, 'Lash', [q, q + Vector((s * 0.03 * hh, -0.01 * hh, 0.02 * hh))], [0.008 * hh, 0.004 * hh], dark))
    # Brows, in the hair's colour.
    bm = M(b, 'hair', face.get('brow_color', spec.get('hair', {}).get('color', HAIR['brown'])))
    bt = face.get('brows', 1.0)
    if bt > 0:
        for s in (-1, 1):
            tilt = face.get('brow_tilt', 0.0)
            pts = [surf(s * (ex + dx), -1, ey + 0.26 + dy + s * 0 + tilt * dx * s * 0, 0.012 * hh)[0]
                   for dx, dy in ((-0.13, -0.01 + tilt), (0.0, 0.035), (0.13, 0.0 - tilt))]
            put(curve(b, 'Brow', pts, [0.018 * hh * bt, 0.024 * hh * bt, 0.016 * hh * bt], bm))
    # The nose.
    nose = face.get('nose', 'button')
    if nose == 'button':
        p, n = surf(0, -1, -0.12, 0.02 * hh); put(orient(b.ball('Nose', p, (0.07 * hh, 0.055 * hh, 0.06 * hh), rig.skin, seg=24), n))
    elif nose == 'bulb':
        p, n = surf(0, -1, -0.14, 0.04 * hh); put(orient(b.ball('Nose', p, (0.1 * hh, 0.08 * hh, 0.085 * hh), rig.skin, seg=28), n))
    elif nose == 'long':
        p, n = surf(0, -1, -0.1, 0.05 * hh); put(orient(b.ball('Nose', p + Vector((0, 0, -0.01 * hh)), (0.06 * hh, 0.1 * hh, 0.07 * hh), rig.skin, seg=28), n))
    # The mouth.
    mouth = face.get('mouth', 'smile')
    lip = M(b, 'eye', LIP)
    if mouth in ('smile', 'small', 'smirk'):
        w = 0.2 if mouth == 'smile' else 0.12
        pts = []
        for t in (-1, -0.5, 0, 0.5, 1):
            lift = 0.09 * t * t if mouth != 'smirk' else (0.1 * t * t if t > 0 else 0.02 * t * t)
            pts.append(surf(t * w, -1, -0.43 + lift, 0.006 * hh)[0])
        put(curve(b, 'Mouth', pts, [0.012 * hh, 0.016 * hh, 0.017 * hh, 0.016 * hh, 0.012 * hh], lip))
    elif mouth == 'grin':
        p, n = surf(0, -1, -0.4, -0.002 * hh)
        g = blob(b, 'Grin', (0, 0, 0), (0.16 * hh, 0.035 * hh, 0.11 * hh), M(b, 'eye', '#5B1F2B'), seg=32)
        for v in g.data.vertices:
            if v.co.z > 0: v.co.z *= 0.12
        g.location = p; put(orient(g, n))
        pt, _ = surf(0, -1, -0.385, 0.014 * hh)
        put(orient(b.box('Teeth', pt, (0.2 * hh, 0.012 * hh, 0.032 * hh), M(b, 'eye', '#FFFFFF'), r=0.012 * hh), n))
        pg, _ = surf(0, -1, -0.5, 0.012 * hh)
        put(orient(b.ball('Tongue', pg, (0.075 * hh, 0.02 * hh, 0.035 * hh), M(b, 'eye', '#FF7A8A'), seg=20), n))
    elif mouth == 'o':
        p, n = surf(0.02, -1, -0.43, 0.004 * hh); put(orient(b.ball('Mouth', p, (0.04 * hh, 0.02 * hh, 0.045 * hh), M(b, 'eye', '#5B1F2B'), seg=20), n))
    if face.get('mustache'):
        mm = M(b, 'hair', face['mustache'])
        for s in (-1, 1):
            p, n = surf(s * 0.11, -1, -0.27, 0.03 * hh)
            put(orient(blob(b, 'Mustache', p, (0.11 * hh, 0.04 * hh, 0.05 * hh), mm, top=0.8, bottom=1.0, seg=28), n, roll=s * -14))
    if face.get('blush', True):
        for s in (-1, 1):
            p, n = surf(s * 0.58, -1, -0.24, -0.003 * hh)
            put(orient(b.ball('Blush', p, (0.075 * hh, 0.012 * hh, 0.045 * hh), M(b, 'skin', face.get('blush_color', '#FF8FA3')), seg=20, shadow=False), n))
    if face.get('freckles'):
        fm = M(b, 'skin', '#B5603A')
        for s in (-1, 1):
            for fx, fz in ((0.45, -0.12), (0.55, -0.16), (0.5, -0.06), (0.62, -0.1)):
                p, n = surf(s * fx, -1, fz, 0.0)
                put(b.ball('Freckle', p, 0.011 * hh, fm, seg=10, shadow=False))
    if face.get('mole'):
        p, n = surf(0.42, -1, -0.38, 0.0); put(b.ball('Beauty mark', p, 0.012 * hh, M(b, 'skin', '#4A2A1C'), seg=10))
    for s in (-1, 1):
        p, n = surf(s * 1.0, 0.05, -0.02, -0.01 * hh)
        put(orient(b.ball('Ear', p, (0.06 * hh, 0.04 * hh, 0.095 * hh), rig.skin, seg=24), n))
    hair(b, rig, spec.get('hair', {}))
    if spec.get('glasses'): glasses(b, rig, spec['glasses'])
    if spec.get('hat'): hat(b, rig, spec['hat'])
    if eye_kind == 'sun': sunglasses(b, rig)


def shell(b, rig, name, m, front=0.35, side=-0.15, back=-0.55, bulk=0.07, flare=0.0, inner=0.95, seg=72):
    """A cap over the head (hair, a bandana, a beanie, a cap's crown), its edge a line from the brow round to the nape."""
    hc, (rx, ry, rz), jaw = rig.hc, rig.hr, rig.jaw
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=hc)
    o = bpy.context.object; o.name = name
    for v in o.data.vertices:
        x, y, z = v.co
        t = abs(math.atan2(x, -y)) / math.pi
        line = front + (side - front) * smooth(t / 0.5) if t < 0.5 else side + (back - side) * smooth((t - 0.5) / 0.5)
        k = smoothstep(line - 0.07, line + 0.07, z)
        s = 1 - jaw * z * z if z < 0 else 1.0
        f = inner + (1.0 + bulk + flare * max(0.0, -z) - inner) * k
        v.co = (x * s * rx * f, y * s * ry * f, z * rz * f)
    b._finish(o, m, normals=False)
    return rig.put(o, 'head')


def hair(b, rig, h):
    style = h.get('style', 'crop')
    if style == 'none': return
    hm = M(b, 'hair', h.get('color', HAIR['brown']))
    hc, (rx, ry, rz) = rig.hc, rig.hr
    hh = rig.d['hh']

    def put(o): return rig.put(o, 'head')
    if style == 'bald':
        for s in (-1, 1):
            p, n = rig.surf(s * 0.9, 0.35, 0.05, 0.01 * hh)
            put(orient(blob(b, 'Tuft', p, (0.07 * hh, 0.05 * hh, 0.11 * hh), hm, seg=24), n))
        p, n = rig.surf(0, 0.95, -0.1, 0.01 * hh)
        put(orient(blob(b, 'Tuft', p, (0.22 * hh, 0.05 * hh, 0.08 * hh), hm, seg=24), n))
        return
    cfg = dict(crop=(0.45, -0.05, -0.45, 0.06, 0.0), bob=(0.32, -0.5, -0.62, 0.1, 0.18), long=(0.3, -0.6, -0.9, 0.1, 0.2),
               bun=(0.42, -0.08, -0.4, 0.05, 0.0), curly=(0.5, 0.0, -0.35, 0.05, 0.0), pompadour=(0.55, 0.0, -0.4, 0.05, 0.0),
               puffs=(0.42, -0.05, -0.42, 0.06, 0.0), pigtails=(0.42, -0.1, -0.45, 0.06, 0.0), spiky=(0.5, 0.0, -0.4, 0.05, 0.0), ponytail=(0.4, -0.05, -0.45, 0.05, 0.0))[style]
    shell(b, rig, 'Hair', hm, *cfg)
    if h.get('fringe', style in ('bob', 'long')):
        for t in (-0.42, -0.21, 0.0, 0.21, 0.42):
            p, n = rig.surf(t * 0.85, -1, cfg[0] - 0.04 + 0.06 * t * t, 0.03 * hh)
            put(orient(blob(b, 'Fringe', p, (0.1 * hh, 0.045 * hh, 0.11 * hh), hm, top=1.0, bottom=0.6, seg=24), n, roll=-t * 30))
    if style == 'bun':
        put(b.ball('Bun', hc + Vector((0, 0.32 * ry, 0.95 * rz)), 0.2 * hh, hm, seg=32))
        put(b.torus('Bun band', hc + Vector((0, 0.3 * ry, 0.82 * rz)), 0.12 * hh, 0.025 * hh, M(b, 'gloss', h.get('band', C['coral'])), rot=(-20, 0, 0)))
    elif style == 'pigtails':
        for s in (-1, 1):
            a = hc + Vector((s * 0.8 * rx, 0.45 * ry, -0.15 * rz))
            put(curve(b, 'Pigtail', [a, a + Vector((s * 0.12 * hh, 0.04 * hh, -0.12 * hh)), a + Vector((s * 0.14 * hh, 0.02 * hh, -0.3 * hh))], [0.08 * hh, 0.085 * hh, 0.045 * hh], hm))
            put(b.torus('Pigtail band', a + Vector((s * 0.04 * hh, 0.01 * hh, -0.02 * hh)), 0.065 * hh, 0.022 * hh, M(b, 'gloss', h.get('band', C['yellow'])), rot=(0, s * 60, 0)))
    elif style == 'puffs':
        for s in (-1, 1):
            put(b.ball('Puff', hc + Vector((s * 0.82 * rx, 0.25 * ry, 0.55 * rz)), 0.17 * hh, hm, seg=32))
            put(b.torus('Puff band', hc + Vector((s * 0.7 * rx, 0.2 * ry, 0.48 * rz)), 0.08 * hh, 0.022 * hh, M(b, 'gloss', h.get('band', C['yellow'])), rot=(0, s * 50, 0)))
    elif style == 'curly':
        rng = random.Random(7)
        n = 90
        for i in range(n):
            u = 1 - 2 * (i + 0.5) / n; a = math.pi * (3 - math.sqrt(5)) * i
            dv = Vector((math.cos(a) * math.sqrt(1 - u * u), math.sin(a) * math.sqrt(1 - u * u), u))
            if dv.z < -0.25 or (dv.y < -0.45 and dv.z < 0.6): continue
            p, nn = rig.surf(dv.x, dv.y, dv.z, 0.04 * hh)
            put(b.ball('Curl', p, (0.075 + 0.025 * rng.random()) * hh, hm, seg=18))
    elif style == 'pompadour':
        put(blob(b, 'Pompadour', hc + Vector((0, -0.42 * ry, 0.88 * rz)), (0.36 * hh, 0.3 * hh, 0.17 * hh), hm, top=0.75, bottom=1.0, rot=(-28, 0, 0)))
        put(blob(b, 'Pompadour back', hc + Vector((0, 0.1 * ry, 0.98 * rz)), (0.32 * hh, 0.36 * hh, 0.12 * hh), hm))
    elif style == 'spiky':
        for k, (dx, dy) in enumerate(((0, -0.4), (-0.4, -0.2), (0.4, -0.2), (-0.25, 0.25), (0.25, 0.25), (0, 0.5), (-0.6, 0.3), (0.6, 0.3))):
            p, n = rig.surf(dx, dy, 0.85, 0.0)
            o = R['cone'](b, 'Spike', p + n * 0.06 * hh, 0.07 * hh, 0.0, 0.18 * hh, hm)
            put(orient(o, n))
            o.rotation_quaternion = n.to_track_quat('Z', 'Y')
    elif style == 'ponytail':
        put(curve(b, 'Ponytail', [hc + Vector((0, 0.9 * ry, 0.35 * rz)), hc + Vector((0, 1.25 * ry, 0.0)), hc + Vector((0, 1.2 * ry, -0.55 * rz))],
                  [0.09 * hh, 0.1 * hh, 0.05 * hh], hm))
        put(b.torus('Tie', hc + Vector((0, 0.95 * ry, 0.3 * rz)), 0.07 * hh, 0.022 * hh, M(b, 'gloss', h.get('band', C['coral'])), rot=(70, 0, 0)))
    elif style == 'long':
        put(blob(b, 'Long hair', hc + Vector((0, 0.55 * ry, -0.75 * rz)), (0.8 * rx, 0.45 * ry, 0.55 * rz), hm, top=1.0, bottom=0.85))


def glasses(b, rig, g):
    hh = rig.d['hh']; fm = M(b, 'gloss', g.get('color', INK))
    face = rig.spec.get('face', {})
    ex, ey = 0.36 * face.get('apart', 1.0), 0.06 + face.get('eye_up', 0.0)
    centres = []
    for s in (-1, 1):
        p, n = rig.surf(s * ex, -1, ey, 0.07 * hh)
        centres.append(p)
        if g.get('kind') == 'square':
            # A rim of four bars round a clear lens, so the eyes show through (GR-18 wave 1: the board's solid plate hid them).
            q = Vector(n).to_track_quat('-Y', 'Z')
            w, h, t = 0.2 * hh, 0.16 * hh, 0.03 * hh
            for name, off, size in (('Frame top', (0, 0, (h - t) / 2), (w, 0.02 * hh, t)), ('Frame bottom', (0, 0, -(h - t) / 2), (w, 0.02 * hh, t)),
                                    ('Frame side', ((w - t) / 2, 0, 0), (t, 0.02 * hh, h - 2 * t + 0.01 * hh)), ('Frame side', (-(w - t) / 2, 0, 0), (t, 0.02 * hh, h - 2 * t + 0.01 * hh))):
                rig.put(orient(b.box(name, p + q @ Vector(off), size, fm, r=0.012 * hh), n), 'head')
            rig.put(orient(b.box('Lens', p + n * 0.004, (0.165 * hh, 0.01 * hh, 0.125 * hh), b.mat('glass', '#EAFBFF', alpha=0.12), r=0.02 * hh), n), 'head')
        else:
            o = b.torus('Frame', p, 0.1 * hh, 0.014 * hh, fm)
            o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = n.to_track_quat('Z', 'Y')
            rig.put(o, 'head')
    rig.put(curve(b, 'Bridge', [centres[0] + Vector((0.09 * hh, 0, 0.01 * hh)), (centres[0] + centres[1]) / 2 + Vector((0, -0.01 * hh, 0.03 * hh)),
                                centres[1] - Vector((0.09 * hh, 0, -0.01 * hh))], 0.012 * hh, fm), 'head')
    for s, cpt in zip((-1, 1), centres):
        ear, _ = rig.surf(s * 0.98, 0.1, 0.06, 0.02 * hh)
        rig.put(curve(b, 'Temple', [cpt + Vector((s * 0.1 * hh, 0, 0)), ear], 0.01 * hh, fm, poly=True), 'head')


def sunglasses(b, rig):
    hh = rig.d['hh']; lens = M(b, 'eye', '#2A2448'); fm = b.mat('metal', '#F4C04E', rough=0.2)
    pts = []
    for s in (-1, 1):
        p, n = rig.surf(s * 0.36, -1, 0.05, 0.03 * hh)
        rig.put(orient(blob(b, 'Shade', p, (0.15 * hh, 0.03 * hh, 0.1 * hh), lens, top=1.15, bottom=0.8, seg=28), n, roll=s * 4), 'head')
        g, _ = rig.surf(s * 0.3, -1, 0.1, 0.06 * hh)
        rig.put(b.ball('Shade glint', g, (0.03 * hh, 0.008 * hh, 0.015 * hh), M(b, 'eye', '#FFFFFF'), seg=12, shadow=False), 'head')
        pts.append(rig.surf(s * 0.6, -1, 0.15, 0.04 * hh)[0])
    rig.put(curve(b, 'Shade bar', [pts[0], rig.surf(0, -1, 0.15, 0.06 * hh)[0], pts[1]], 0.012 * hh, fm), 'head')
    for s in (-1, 1):
        rig.put(curve(b, 'Temple', [rig.surf(s * 0.62, -1, 0.12, 0.04 * hh)[0], rig.surf(s * 0.98, 0.1, 0.08, 0.02 * hh)[0]], 0.01 * hh, fm, poly=True), 'head')


def hat(b, rig, h):
    hh = rig.d['hh']; hc, (rx, ry, rz) = rig.hc, rig.hr
    kind = h['kind']; hm = M(b, 'gloss' if kind in ('cap', 'tophat', 'bucket', 'goggles') else 'cloth', h.get('color', C['teal']))
    top = hc + Vector((0, 0, rz))

    def put(o): return rig.put(o, 'head')
    if kind == 'cap':
        shell(b, rig, 'Cap crown', hm, front=0.48, side=0.12, back=-0.05, bulk=0.14, inner=0.9)
        p, n = rig.surf(0, -1, 0.5, 0.0)
        put(blob(b, 'Cap brim', hc + Vector((0, -1.0 * ry, 0.5 * rz)), (0.34 * hh, 0.3 * hh, 0.028 * hh), hm, rot=(10, 0, 0)))
        put(b.ball('Cap button', top + Vector((0, 0, 0.12 * hh)), 0.04 * hh, hm, seg=16))
        if h.get('logo'):
            p, n = rig.surf(0, -1, 0.72, 0.12 * hh)
            put(orient(b.cyl('Logo', p, 0.075 * hh, 0.012 * hh, M(b, 'gloss', C['cream']), r=0.004, rot=(0, 0, 0)), n))
            put(orient(b.ball('Logo fish', p + n * 0.012 + Vector((0.008 * hh, 0, 0)), (0.045 * hh, 0.012 * hh, 0.033 * hh), M(b, 'gloss', C['teal']), seg=16), n))
            put(orient(b.ball('Logo tail', p + n * 0.012 + Vector((-0.045 * hh, 0, 0)), (0.02 * hh, 0.012 * hh, 0.028 * hh), M(b, 'gloss', C['teal']), seg=12), n))
    elif kind == 'bucket':
        z = hc.z + 0.62 * rz
        o = R['cone'](b, 'Bucket hat', (0, 0, z + 0.12 * hh), 0.5 * hh, 0.4 * hh, 0.3 * hh, hm); put(o)
        put(b.cyl('Bucket top', (0, 0, z + 0.27 * hh), 0.4 * hh, 0.02 * hh, hm, r=0.02))
        put(blob(b, 'Bucket brim', (0, 0, z - 0.01 * hh), (0.7 * hh, 0.66 * hh, 0.035 * hh), hm, top=0.9, bottom=1.0, rot=(4, 0, 0)))
        put(b.torus('Bucket band', (0, 0, z + 0.04 * hh), 0.49 * hh, 0.03 * hh, M(b, 'cloth', h.get('band', C['coral']))))
        if h.get('flower'):
            put(b.ball('Flower', (0.4 * hh, -0.22 * hh, z + 0.08 * hh), 0.06 * hh, M(b, 'gloss', h['flower']), seg=16))
    elif kind == 'beret':
        put(blob(b, 'Beret', top + Vector((0.05 * hh, 0.02 * hh, -0.02 * hh)), (0.52 * hh, 0.48 * hh, 0.14 * hh), hm, rot=(0, -14, 0)))
        put(b.cyl('Beret nub', top + Vector((0.03 * hh, 0.02 * hh, 0.12 * hh)), 0.025 * hh, 0.06 * hh, hm, r=0.01))
    elif kind == 'beanie':
        shell(b, rig, 'Beanie', hm, front=0.4, side=-0.05, back=-0.35, bulk=0.13, inner=0.9)
        put(b.torus('Beanie cuff', hc + Vector((0, 0.04 * ry, 0.28 * rz)), 0.96 * rx, 0.06 * hh, M(b, 'cloth', h.get('band', C['cream'])), rot=(-9, 0, 0)))
        put(b.ball('Pompom', top + Vector((0, 0, 0.12 * hh)), 0.1 * hh, M(b, 'cloth', h.get('band', C['cream'])), seg=24))
    elif kind == 'tophat':
        put(b.cyl('Top hat', top + Vector((0, 0, 0.2 * hh)), 0.29 * hh, 0.48 * hh, hm, r=0.02))
        put(blob(b, 'Top hat brim', top + Vector((0, 0, -0.04 * hh)), (0.5 * hh, 0.45 * hh, 0.03 * hh), hm, top=0.9, bottom=1.0, rot=(5, 0, 0)))
        put(b.cyl('Hat band', top + Vector((0, 0, 0.03 * hh)), 0.296 * hh, 0.1 * hh, b.mat('metal', '#F4C04E', rough=0.25), r=0.01))
        for k, s in enumerate((-1, 1)):
            put(b.ball('Hat feather', top + Vector((0.27 * hh, 0.05 * hh * s, 0.16 * hh + 0.04 * hh * k)), (0.03 * hh, 0.04 * hh, 0.14 * hh), M(b, 'gloss', (C['coral'], C['cream'])[k]), seg=16))
    elif kind == 'bandana':
        shell(b, rig, 'Bandana', hm, front=0.68, side=0.25, back=-0.15, bulk=0.11, inner=0.92)
        dots = M(b, 'cloth', h.get('dots', C['cream']))
        for k in range(7):
            a = k * 2.4; u = 0.78 + 0.18 * ((k * 0.37) % 1)
            p, n = rig.surf(math.cos(a) * math.sqrt(1 - u * u), math.sin(a) * math.sqrt(1 - u * u), u, 0.1 * hh * 1.0 + 0.0)
            put(orient(b.ball('Bandana dot', p, (0.03 * hh, 0.006 * hh, 0.03 * hh), dots, seg=12), n))
        kn = hc + Vector((0, 0.95 * ry, 0.3 * rz))
        put(b.ball('Knot', kn, 0.06 * hh, hm, seg=20))
        for s in (-1, 1):
            put(blob(b, 'Bandana tail', kn + Vector((s * 0.05 * hh, 0.05 * hh, -0.12 * hh)), (0.04 * hh, 0.02 * hh, 0.12 * hh), hm, rot=(15, s * 20, 0)))
    elif kind == 'goggles':
        strap = M(b, 'cloth', h.get('strap', INK))
        o = b.torus('Goggle strap', hc + Vector((0, 0.02 * ry, 0.5 * rz)), 1.12 * rx, 0.03 * hh, strap, rot=(-10, 0, 0))
        o.scale = (1, ry / rx, 1); put(o)
        for s in (-1, 1):
            p, n = rig.surf(s * 0.3, -1, 0.58, 0.13 * hh)
            o = b.cyl('Goggle', p, 0.115 * hh, 0.09 * hh, hm, r=0.02)
            o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = n.to_track_quat('Z', 'Y'); put(o)
            o = b.cyl('Goggle lens', p + n * 0.046 * hh, 0.09 * hh, 0.01 * hh, M(b, 'eye', h.get('lens', C['teal_light'])), r=0.003)
            o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = n.to_track_quat('Z', 'Y'); put(o)
            put(b.ball('Goggle glint', p + n * 0.056 * hh + Vector((0.025 * hh, 0, 0.035 * hh)), 0.018 * hh, M(b, 'eye', '#FFFFFF'), seg=10, shadow=False))


# ---------------------------------------------------------------------------------------------------- what they carry and wear
def ex_tote(b, rig, spec, color=C['cream'], print_=C['teal'], side='R'):
    d = rig.d; hh = d['hh']; s = 1 if side == 'L' else -1
    bm = M(b, 'cloth', color)
    z = d['hip_z'] + 0.02 * hh
    x = s * (side_x(rig, z) + 0.05 * hh)
    bag = b.box('Tote', (x, 0.03 * hh, z), (0.06 * hh, 0.42 * hh, 0.44 * hh), bm, r=0.03)
    rig.put(bag, 'spine')
    rig.put(orient(b.ball('Tote print', (x + s * 0.035 * hh, 0.03 * hh, z), (0.13 * hh, 0.01 * hh, 0.09 * hh), M(b, 'cloth', print_), seg=24), Vector((s, 0, 0))), 'spine')
    rig.put(orient(b.ball('Tote eye', (x + s * 0.045 * hh, -0.02 * hh, z + 0.02 * hh), (0.03 * hh, 0.006 * hh, 0.03 * hh), M(b, 'cloth', '#FFFFFF'), seg=12), Vector((s, 0, 0))), 'spine')
    shx = rig.w['shoulder.' + side]
    rig.put(curve(b, 'Tote strap', [Vector((x, -0.12 * hh, z + 0.2 * hh)), Vector((shx.x * 0.75, -0.16 * hh, shx.z + 0.1 * hh)), Vector((shx.x * 0.75, 0.16 * hh, shx.z + 0.1 * hh)),
                                     Vector((x, 0.16 * hh, z + 0.2 * hh))], 0.018 * hh, bm), 'spine')


def ex_beltbag(b, rig, spec, color=C['coral']):
    d = rig.d; hh = d['hh']; z = d['bot'] + 0.16 * hh
    bm = M(b, 'gloss', color)
    a = math.radians(-38)
    x, y = side_x(rig, z) * 1.04 * math.sin(a), front_y(rig, z) * 1.04 * math.cos(a)
    rig.put(blob(b, 'Belt bag', (x, y - 0.03 * hh, z), (0.15 * hh, 0.07 * hh, 0.1 * hh), bm, top=0.9, rot=(0, 0, -38)), 'spine')
    rig.put(b.box('Belt bag zip', (x * 1.02, y - 0.09 * hh, z + 0.035 * hh), (0.22 * hh, 0.012 * hh, 0.012 * hh), b.mat('metal', '#F4C04E'), r=0.004, rot=(0, 0, -38)), 'spine')
    ring(b, rig, 'spine', 'Belt strap', z + 0.02 * hh, M(b, 'cloth', INK), 0.016 * hh, 1.03)


def ex_satchel(b, rig, spec, color=C['wood_dark']):
    d = rig.d; hh = d['hh']
    bm = M(b, 'gloss', color)
    z = d['hip_z'] + 0.02 * hh; x = side_x(rig, z) + 0.06 * hh
    rig.put(b.box('Satchel', (x, -0.02 * hh, z), (0.1 * hh, 0.42 * hh, 0.34 * hh), bm, r=0.04), 'spine')
    rig.put(b.box('Satchel flap', (x + 0.055 * hh, -0.02 * hh, z + 0.06 * hh), (0.02 * hh, 0.43 * hh, 0.24 * hh), bm, r=0.02), 'spine')
    rig.put(b.box('Buckle', (x + 0.07 * hh, -0.02 * hh, z - 0.02 * hh), (0.02 * hh, 0.07 * hh, 0.06 * hh), b.mat('metal', '#F4C04E'), r=0.006), 'spine')
    shR = rig.w['shoulder.R']
    zt = d['top'] - 0.02 * hh
    pts = [Vector((shR.x * 0.6, -0.04 * hh, zt + 0.06 * hh))]
    for k in range(1, 5):
        t = k / 4
        zz = zt + 0.06 * hh + (z + 0.17 * hh - zt - 0.06 * hh) * t
        xx = shR.x * 0.6 + (x - 0.02 * hh - shR.x * 0.6) * t
        pts.append(Vector((xx, front_y(rig, zz) - 0.012, zz)))
    rig.put(curve(b, 'Satchel strap', pts, 0.02 * hh, bm), 'spine')
    rig.put(curve(b, 'Satchel strap back', [pts[0], Vector((pts[0].x, 0.22 * hh, zt)), Vector((x - 0.02 * hh, 0.15 * hh, z + 0.17 * hh))], 0.02 * hh, bm), 'spine')


def ex_bowtie(b, rig, spec, color=C['teal']):
    d = rig.d; hh = d['hh']; z = d['top'] + 0.01 * hh
    bm = M(b, 'gloss', color); y = front_y(rig, d['top'] - 0.03 * hh) - 0.03 * hh
    for s in (-1, 1):
        o = R['cone'](b, 'Bow', (s * 0.055 * hh, y, z), 0.05 * hh, 0.012 * hh, 0.1 * hh, bm, rot=(0, s * 90, 0))
        rig.put(o, 'spine')
    rig.put(b.ball('Bow knot', (0, y - 0.005, z), 0.025 * hh, bm, seg=16), 'spine')


def ex_loupe(b, rig, spec):
    d = rig.d; hh = d['hh']; z = d['top'] - 0.42 * hh
    cord = M(b, 'cloth', INK)
    yy = front_y(rig, z) - 0.03 * hh
    rig.put(curve(b, 'Loupe cord', [Vector((-0.1 * hh, front_y(rig, d['top']) - 0.005, d['top'] + 0.02 * hh)), Vector((-0.08 * hh, yy, z + 0.2 * hh)), Vector((0, yy, z + 0.06 * hh)),
                                     Vector((0.08 * hh, yy, z + 0.2 * hh)), Vector((0.1 * hh, front_y(rig, d['top']) - 0.005, d['top'] + 0.02 * hh))], 0.007 * hh, cord), 'spine')
    o = b.torus('Loupe', (0, yy - 0.01 * hh, z), 0.065 * hh, 0.016 * hh, b.mat('metal', '#F4C04E', rough=0.2), rot=(90, 0, 0)); rig.put(o, 'spine')
    rig.put(b.cyl('Loupe lens', (0, yy - 0.01 * hh, z), 0.058 * hh, 0.012 * hh, b.mat('glass', '#EAFBFF', alpha=0.2), r=0, rot=(90, 0, 0)), 'spine')


def ex_chain(b, rig, spec):
    d = rig.d; hh = d['hh']
    gold = b.mat('metal', '#F4C04E', rough=0.18)
    z0 = d['top'] + 0.0
    for k in range(13):
        t = (k / 12) * 2 - 1
        z = z0 - 0.26 * hh * (1 - t * t)
        x = t * 0.15 * hh
        rig.put(b.ball('Chain link', (x, front_y(rig, z) - 0.018 * hh, z), 0.018 * hh, gold, seg=12), 'spine')
    z = z0 - 0.33 * hh
    rig.put(b.cyl('Medallion', (0, front_y(rig, z) - 0.025 * hh, z), 0.065 * hh, 0.02 * hh, gold, r=0.006, rot=(90, 0, 0)), 'spine')
    rig.put(b.ball('Medallion gem', (0, front_y(rig, z) - 0.04 * hh, z), (0.03 * hh, 0.012 * hh, 0.03 * hh), M(b, 'eye', C['lilac']), seg=16), 'spine')


def ex_sash(b, rig, spec, color=C['cream']):
    d = rig.d; hh = d['hh']; sm = M(b, 'cloth', color)
    o = b.torus('Sash', (0, 0, (d['top'] + d['bot']) / 2 + 0.06 * hh), d['trx'] * 1.07, 0.035 * hh, sm, rot=(0, 38, 0))
    o.scale = (1.0, d['try_'] / d['trx'] * 1.1, 1.0)
    rig.put(o, 'spine')


def ex_rosette(b, rig, spec, at=0.25):
    d = rig.d; hh = d['hh']; z = d['top'] - at * hh
    x = 0.13 * hh; y = front_y(rig, z) - 0.03 * hh
    gold = b.mat('metal', '#F4C04E', rough=0.25)
    for k in range(14):
        a = k / 14 * 2 * math.pi
        rig.put(b.ball('Ruffle', (x + math.cos(a) * 0.075 * hh, y, z + math.sin(a) * 0.075 * hh), (0.035 * hh, 0.015 * hh, 0.035 * hh), M(b, 'gloss', '#E8B53A'), seg=12), 'spine')
    rig.put(b.cyl('Rosette', (x, y - 0.01 * hh, z), 0.07 * hh, 0.02 * hh, M(b, 'gloss', C['cream']), r=0.006, rot=(90, 0, 0)), 'spine')
    rig.put(b.cyl('Rosette centre', (x, y - 0.022 * hh, z), 0.04 * hh, 0.012 * hh, gold, r=0.004, rot=(90, 0, 0)), 'spine')
    for s in (-1, 1):
        rig.put(tri(b, 'Ribbon', [(x + s * 0.01 * hh, y + 0.005, z - 0.06 * hh), (x + s * 0.07 * hh, y + 0.005, z - 0.06 * hh), (x + s * 0.07 * hh, y + 0.005, z - 0.26 * hh),
                                  (x + s * 0.04 * hh, y + 0.005, z - 0.21 * hh), (x + s * 0.01 * hh, y + 0.005, z - 0.26 * hh)], 0.01 * hh, M(b, 'gloss', '#E8B53A' if s < 0 else C['cream'])), 'spine')


def ex_mark(b, rig, spec, at=None, x=0.0, fish=C['teal'], grow=1.0):
    """The shop's mark: a little creature in a cream circle, worn on the chest."""
    d = rig.d; hh = d['hh']
    z = at if at is not None else d['top'] - 0.22 * hh
    y = front_y(rig, z) * grow - 0.018 * hh
    rig.put(b.cyl('Shop mark', (x, y, z), 0.08 * hh, 0.01 * hh, M(b, 'gloss', C['cream']), r=0.003, rot=(90, 0, 0)), 'spine')
    rig.put(b.ball('Mark fish', (x + 0.01 * hh, y - 0.008, z), (0.045 * hh, 0.01 * hh, 0.034 * hh), M(b, 'gloss', fish), seg=16), 'spine')
    rig.put(b.ball('Mark tail', (x - 0.04 * hh, y - 0.008, z), (0.02 * hh, 0.01 * hh, 0.028 * hh), M(b, 'gloss', fish), seg=12), 'spine')
    rig.put(b.ball('Mark eye', (x + 0.03 * hh, y - 0.016, z + 0.008 * hh), 0.008 * hh, M(b, 'eye', INK), seg=8), 'spine')


def ex_apron(b, rig, spec, color=C['teal'], pocket=C['yellow'], name=None):
    d = rig.d; hh = d['hh']; am = M(b, 'cloth', color)
    zt, zb = d['top'] - 0.12 * hh, d['bot'] + 0.08 * hh
    # The bib on the chest, and the skirt from the waist down to the thighs, each curved round the body.
    rig.put(panel(b, rig, 'Apron bib', zt, zb, lambda u: (-0.5 - 0.15 * u, 0.5 + 0.15 * u), 1.03, am, 0.012 * hh), 'spine')
    z0 = d['bot'] + 0.1 * hh; z1 = d['hip_z'] - (d['hip_z'] - d['ankle_z']) * 0.5
    prx, pry = 0.37 * hh * d['bw'], 0.29 * hh * d['bw']
    skirt = lambda z: (prx * 1.06 + 0.25 * (z0 - z), pry * 1.08 + 0.4 * (z0 - z))
    rig.put(panel(b, rig, 'Apron skirt', z0, z1, lambda u: (-0.95, 0.95), 1.0, am, 0.014 * hh, rad=skirt), 'hips')
    zp = z0 - (z0 - z1) * 0.45
    rig.put(panel(b, rig, 'Apron pocket', zp + 0.1 * hh, zp - 0.08 * hh, lambda u: (-0.45, 0.45), 1.0, M(b, 'cloth', pocket), 0.012 * hh,
                  rad=lambda z: (skirt(z)[0] + 0.012 * hh, skirt(z)[1] + 0.012 * hh), rows=4, cols=8), 'hips')
    ring(b, rig, 'spine', 'Apron tie', d['bot'] + 0.06 * hh, am, 0.02 * hh, 1.03)
    rig.put(curve(b, 'Apron strap', [(-0.18 * hh, front_y(rig, zt) - 0.01, zt + 0.01 * hh), (-0.13 * hh, 0.0, d['top'] + 0.07 * hh), (0.13 * hh, 0.0, d['top'] + 0.07 * hh),
                                     (0.18 * hh, front_y(rig, zt) - 0.01, zt + 0.01 * hh)], 0.016 * hh, am), 'spine')
    # The shop's mark on the bib: a little creature in a cream circle.
    ex_mark(b, rig, spec, at=zt - 0.14 * hh, x=0.0, fish=C['yellow'] if color == C['teal'] else C['teal'], grow=1.03)
    if name:
        zb2 = d['top'] - 0.26 * hh
        x = -0.2 * hh; y = front_y(rig, zb2) * 1.03 - 0.03 * hh
        rig.put(b.box('Name badge', (x, y, zb2), (0.16 * hh, 0.015 * hh, 0.07 * hh), M(b, 'gloss', C['white']), r=0.008), 'spine')
        t = b.text(name, (x, y - 0.009 * hh, zb2), 0.045 * hh, M(b, 'gloss', C['coral']), extrude=0.002)
        rig.put(t, 'spine')


def adopt(rig, objs, key):
    """Parts made in the person's own space (root at the feet, facing -Y), handed to a joint where they are now."""
    root = rig.j['root']
    for o in objs: o.parent = root
    bpy.context.view_layer.update()
    for o in objs:
        mw = o.matrix_world.copy(); o.parent = rig.j[key]; o.matrix_world = mw


def grip_point(rig, side):
    """Where the palm is now (after the pose), in the person's own space."""
    bpy.context.view_layer.update()
    inv = rig.j['root'].matrix_world.inverted()
    W = inv @ rig.j['wrist.' + side].matrix_world.translation
    E = inv @ rig.j['elbow.' + side].matrix_world.translation
    return W + (W - E).normalized() * 0.15 * rig.d['hh']


def held(b, rig, item='notebook', side='L', color=None):
    """Something held in a hand, made after the pose where that hand is now, upright in the person's space, then parented
    to the wrist (or to the spine, for the jar carried in both arms)."""
    hh = rig.d['hh']; s = 1 if side == 'L' else -1
    g = grip_point(rig, side)
    key = 'wrist.' + side
    o = []
    V = Vector
    if item == 'notebook':
        o.append(b.box('Notebook', g + V((0, -0.06 * hh, 0.1 * hh)), (0.3 * hh, 0.06 * hh, 0.38 * hh), M(b, 'gloss', color or C['teal_dark']), r=0.015, rot=(-12, 0, s * 8)))
        o.append(b.box('Pages', g + V((0.01 * s * hh, -0.06 * hh, 0.1 * hh)), (0.28 * hh, 0.05 * hh, 0.35 * hh), M(b, 'cloth', C['cream']), r=0.006, rot=(-12, 0, s * 8)))
        o.append(b.box('Book label', g + V((0, -0.095 * hh, 0.14 * hh)), (0.16 * hh, 0.006 * hh, 0.08 * hh), M(b, 'gloss', C['yellow']), r=0.003, rot=(-12, 0, s * 8)))
    elif item == 'coupons':
        for k in range(3):
            o.append(b.box('Coupon', g + V((0, -0.05 * hh - k * 0.005, 0.12 * hh)), (0.22 * hh, 0.008 * hh, 0.12 * hh),
                           M(b, 'cloth', (C['lime'], C['pink'], C['cream'])[k]), r=0.003, rot=(0, (k - 1) * 18, 0)))
        o.append(b.box('Coupon star', g + V((0, -0.07 * hh, 0.14 * hh)), (0.05 * hh, 0.004 * hh, 0.05 * hh), M(b, 'gloss', C['coral']), r=0.002, rot=(0, 45, 0)))
    elif item == 'program':
        o.append(b.box('Programme', g + V((0, -0.05 * hh, 0.1 * hh)), (0.26 * hh, 0.025 * hh, 0.36 * hh), M(b, 'gloss', color or '#2B3A55'), r=0.01, rot=(-10, 0, 0)))
        o.append(b.box('Programme star', g + V((0, -0.068 * hh, 0.14 * hh)), (0.1 * hh, 0.006 * hh, 0.1 * hh), b.mat('metal', '#F4C04E'), r=0.004, rot=(-10, 45, 0)))
    elif item == 'clipboard':
        o.append(b.box('Clipboard', g + V((0, -0.06 * hh, 0.12 * hh)), (0.34 * hh, 0.025 * hh, 0.44 * hh), M(b, 'gloss', C['wood']), r=0.012, rot=(-22, 0, 0)))
        o.append(b.box('Paper', g + V((0, -0.075 * hh, 0.1 * hh)), (0.3 * hh, 0.006 * hh, 0.36 * hh), M(b, 'cloth', '#FFFFFF'), r=0.003, rot=(-22, 0, 0)))
        o.append(b.box('Clip', g + V((0, -0.03 * hh, 0.33 * hh)), (0.12 * hh, 0.03 * hh, 0.05 * hh), b.mat('metal', '#D8DCE6'), r=0.006, rot=(-22, 0, 0)))
        for k in range(3):
            o.append(b.box('Line', g + V((0, -0.08 * hh - k * 0.01 * hh, 0.18 * hh - k * 0.07 * hh)), (0.22 * hh, 0.004 * hh, 0.016 * hh), M(b, 'gloss', (C['teal'], C['coral'], C['teal'])[k]), r=0.002, rot=(-22, 0, 0)))
    elif item == 'pipette':
        o.append(b.cyl('Pipette', g + V((0, -0.02 * hh, -0.08 * hh)), 0.024 * hh, 0.34 * hh, b.mat('glass', '#F2FCFF', alpha=0.25), r=0))
        o.append(b.cyl('Pipette fill', g + V((0, -0.02 * hh, -0.17 * hh)), 0.018 * hh, 0.13 * hh, b.mat('liquid', BLOOM, glow=1.2), r=0.004))
        o.append(b.ball('Pipette bulb', g + V((0, -0.02 * hh, 0.13 * hh)), (0.05 * hh, 0.05 * hh, 0.08 * hh), M(b, 'gloss', C['coral']), seg=20))
        o.append(b.ball('Drop', g + V((0, -0.02 * hh, -0.3 * hh)), (0.02 * hh, 0.02 * hh, 0.028 * hh), b.mat('liquid', BLOOM, glow=1.6), seg=12))
    elif item == 'bags':
        for k, col in enumerate((color or C['lilac'], '#F4C04E')):
            bc = g + V((s * 0.04 * hh * k, (k - 0.5) * 0.18 * hh, -0.36 * hh - k * 0.05 * hh))
            o.append(b.box('Shopping bag', bc, (0.12 * hh, 0.38 * hh, 0.44 * hh), M(b, 'gloss', col), r=0.015, rot=(0, 0, s * 30)))
            o.append(b.box('Bag star', bc + V((s * 0.07 * hh, 0, 0.04 * hh)), (0.006 * hh, 0.13 * hh, 0.13 * hh), M(b, 'gloss', C['cream']), r=0.004, rot=(45, 0, s * 30)))
            o.append(curve(b, 'Bag handle', [bc + V((0, -0.1 * hh, 0.21 * hh)), g + V((0, (k - 0.5) * 0.04 * hh, 0.02 * hh)), bc + V((0, 0.1 * hh, 0.21 * hh))],
                           0.012 * hh, b.mat('metal', '#F4C04E')))
    elif item == 'netbag':
        nm = M(b, 'cloth', color or C['lime'])
        bc = g + V((0, 0, -0.26 * hh))
        for k in range(5):
            o.append(b.torus('Net', bc + V((0, 0, (k - 2) * 0.06 * hh)), (0.15 - abs(k - 2) * 0.03) * hh, 0.009 * hh, nm))
        for k, col in enumerate((C['coral'], C['yellow'], C['lime'])):
            o.append(b.ball('Grocery', bc + V(((k - 1) * 0.06 * hh, (k % 2) * 0.04 * hh, (k - 1) * 0.04 * hh)), 0.07 * hh, M(b, 'gloss', col), seg=16))
        o.append(curve(b, 'Net handle', [bc + V((0, -0.08 * hh, 0.12 * hh)), g + V((0, 0, 0.02 * hh)), bc + V((0, 0.08 * hh, 0.12 * hh))], 0.01 * hh, nm))
    elif item == 'coin':
        o.append(b.cyl('Coin', g + V((0, -0.05 * hh, 0.04 * hh)), 0.05 * hh, 0.014 * hh, b.mat('metal', '#F4C04E', rough=0.2), r=0.004, rot=(80, 0, 0)))
        o.append(b.cyl('Coin', g + V((0.01 * hh, -0.06 * hh, 0.07 * hh)), 0.05 * hh, 0.014 * hh, b.mat('metal', '#F4C04E', rough=0.2), r=0.004, rot=(70, 0, 20)))
    elif item == 'creature_bag':
        o.extend(bag_(b, g + V((0, 0, -0.03 * hh)), hh, color=color, species='Pondlet'))
    elif item == 'jar':
        gc = (grip_point(rig, 'L') + grip_point(rig, 'R')) / 2
        jc = gc + V((0, 0.02 * hh, 0.05 * hh))
        o.append(b.cyl('Jar', jc, 0.2 * hh, 0.44 * hh, b.mat('glass', '#EAFBFF', alpha=0.12), r=0))
        o.append(b.cyl('Jar water', jc + V((0, 0, -0.03 * hh)), 0.185 * hh, 0.34 * hh, b.mat('water', '#47C6E8', alpha=0.3), r=0.01))
        o.append(b.cyl('Jar lid', jc + V((0, 0, 0.235 * hh)), 0.205 * hh, 0.06 * hh, M(b, 'gloss', C['teal']), r=0.014))
        o.append(b.ball('Lid knob', jc + V((0, 0, 0.28 * hh)), 0.04 * hh, M(b, 'gloss', C['yellow']), seg=16))
        o.append(b.creature('Pondlet', jc + V((0, 0, -0.02 * hh)), 0.15, 200))
        key = 'wrist.R'
    adopt(rig, o, key)
    return o


def bag_(b, top, hh, rig=None, key=None, color=None, species='Pondlet'):
    """The shop's carrier for a creature: a clear water bag knotted at the top with a twist tie."""
    parts = []
    c = top + Vector((0, 0, -0.17 * hh))
    parts.append(blob(b, 'Bag', c, (0.13 * hh, 0.12 * hh, 0.17 * hh), b.mat('glass', '#F2FCFF', alpha=0.1), top=0.55, bottom=1.0, shadow=False))
    parts.append(blob(b, 'Bag water', c + Vector((0, 0, -0.03 * hh)), (0.115 * hh, 0.105 * hh, 0.13 * hh), b.mat('water', '#47C6E8', alpha=0.28), top=0.7, bottom=1.0, shadow=False))
    parts.append(R['cone'](b, 'Bag neck', top + Vector((0, 0, 0.0)), 0.06 * hh, 0.012 * hh, 0.1 * hh, b.mat('glass', '#F2FCFF', alpha=0.15)))
    parts.append(b.torus('Twist tie', top + Vector((0, 0, 0.02 * hh)), 0.02 * hh, 0.01 * hh, M(b, 'gloss', color or C['teal'])))
    parts.append(b.ball('Bag knot', top + Vector((0, 0, 0.06 * hh)), 0.025 * hh, b.mat('glass', '#F2FCFF', alpha=0.2), seg=12))
    cr = b.creature(species, c + Vector((0, 0, -0.03 * hh)), 0.12 * hh / 0.49, 30)
    parts.append(cr)
    if rig:
        for p in parts: rig.put(p, key)
    return parts


EXTRAS = dict(tote=ex_tote, beltbag=ex_beltbag, satchel=ex_satchel, bowtie=ex_bowtie, loupe=ex_loupe, chain=ex_chain, sash=ex_sash,
              rosette=ex_rosette, apron=ex_apron, mark=ex_mark)


# ---------------------------------------------------------------------------------------------------- the cast
def cast():
    """The hero look of each kind of visitor and each helper (visitors get their variety from visitor())."""
    return dict(
        casual=dict(height=1.62, build=1.0, skin=SKIN[2], hair=dict(style='crop', color=HAIR['brown'], fringe=True),
                    face=dict(iris=IRIS[0], nose='button', mouth='smile'),
                    top=dict(kind='hoodie', color=C['teal'], accent=C['teal_dark'], trim=C['cream']),
                    bottom=dict(kind='trousers', color='#5C7FD6', accent='#4A69B8'),
                    shoes=dict(kind='sneaker', color='#FFFFFF', sole=C['coral'], accent=C['teal']),
                    hands=dict(R='grip'), extras=[('tote', dict(color=C['cream'], print_=C['coral'], side='R'))]),
        bargain=dict(height=1.46, build=1.2, belly=0.7, skin=SKIN[1], hair=dict(style='bald', color=HAIR['grey']),
                     face=dict(iris=IRIS[2], nose='bulb', mouth='smile', mustache=HAIR['grey'], brows=1.4, brow_color=HAIR['grey'], eye_size=0.85),
                     glasses=dict(kind='round', color=C['ink']), hat=dict(kind='bucket', color=C['yellow'], band=C['coral'], flower=C['pink']),
                     top=dict(kind='cardigan', color=C['yellow'], accent='#E8A21E', buttons=C['coral'], shirt=C['cream']),
                     bottom=dict(kind='shorts', color='#C9A46A', accent='#B08A52'), socks='#FFFFFF', sock_stripe=C['coral'],
                     shoes=dict(kind='sandal', color='#8A5A3B', sole='#5C3A24'),
                     hands=dict(R='grip', L='grip'),
                     extras=[('beltbag', dict(color=C['coral'])), ('held', dict(item='coupons', side='R')), ('held', dict(item='netbag', side='L'))]),
        collector=dict(height=1.76, build=0.86, skin=SKIN[5], hair=dict(style='bun', color=HAIR['black'], band=C['teal']),
                       face=dict(eyes='lidded', iris=IRIS[0], nose='long', mouth='small', brows=0.8, tall=1.04),
                       glasses=dict(kind='square', color=C['coral']),
                       top=dict(kind='vest', color=C['coral'], accent='#E35D5D', shirt=C['cream']),
                       bottom=dict(kind='trousers', color='#4A4F6E', accent='#3B3F5A'),
                       shoes=dict(kind='dress', color='#7A3E22', sole='#3A2418'),
                       hands=dict(L='grip', R='point'),
                       extras=[('bowtie', dict(color=C['teal'])), ('loupe', {}), ('satchel', dict(color=C['wood_dark'])), ('held', dict(item='notebook', side='L'))]),
        premium=dict(height=1.72, build=1.22, belly=0.8, skin=SKIN[3], hair=dict(style='pompadour', color=HAIR['black']),
                     face=dict(eyes='sun', nose='bulb', mouth='grin', brows=1.1),
                     top=dict(kind='coat', color=C['lilac'], accent='#C9B6FF', buttons='gold', shirt='#3D2E6B', under='#3D2E6B'),
                     bottom=dict(kind='trousers', color='#3D2E6B', accent='#2E2252'),
                     shoes=dict(kind='dress', color='#FFFFFF', sole='#F4C04E'),
                     hands=dict(R='grip', L='grip'),
                     extras=[('chain', {}), ('held', dict(item='bags', side='R', color=C['lilac'])), ('held', dict(item='bags', side='L', color=C['pink']))]),
        guest=dict(height=1.68, build=0.95, skin=SKIN[6], hair=dict(style='bun', color=HAIR['black'], band='#E8B53A'),
                   face=dict(eyes='lidded', iris=IRIS[1], nose='button', mouth='smile', lashes=True, mole=True),
                   hat=dict(kind='tophat', color='#2B3A55'),
                   top=dict(kind='coat', color='#F2C14E', accent='#FFE6A6', buttons='gold', long=True, under='#FFF3DF'),
                   bottom=dict(kind='trousers', color='#2B3A55', accent='#1F2A40'),
                   shoes=dict(kind='dress', color='#2B3A55', sole='#F4C04E'),
                   hands=dict(L='grip'),
                   extras=[('sash', dict(color=C['cream'])), ('rosette', dict(at=0.3)), ('held', dict(item='program', side='L'))]),
        pip=dict(height=1.52, build=0.95, skin=SKIN[0], hair=dict(style='pigtails', color=HAIR['auburn'], band=C['yellow']), hands=dict(R='open'),
                 face=dict(iris=IRIS[3], nose='button', mouth='grin', freckles=True, eye_size=1.08),
                 hat=dict(kind='cap', color=C['pink'], logo=True),
                 top=dict(kind='polo', color=C['pink'], accent=C['white'], sleeves='short'),
                 bottom=dict(kind='trousers', color='#2B3A55', accent='#1F2A40'),
                 shoes=dict(kind='sneaker', color=C['pink'], sole=C['white'], accent=C['teal']),
                 extras=[('apron', dict(color=C['teal'], pocket=C['yellow'], name='PIP'))]),
        nori=dict(height=1.74, build=1.24, skin=SKIN[4], hair=dict(style='crop', color=HAIR['black']),
                  face=dict(iris=IRIS[0], nose='bulb', mouth='smile', brows=1.3, jaw=0.08, wide=1.04),
                  hat=dict(kind='bandana', color=C['coral'], dots=C['cream']),
                  top=dict(kind='coverall', color=C['green'], accent=C['green_dark'], sleeves='rolled'),
                  bottom=dict(kind='trousers', color=C['green'], accent=C['green_dark']),
                  shoes=dict(kind='boot', color=C['yellow'], sole='#E8A21E'),
                  hands=dict(R='grip', L='grip'), extras=[('mark', dict(x=-0.14, fish=C['teal'])), ('held', dict(item='jar', side='R'))]),
        moss=dict(height=1.6, build=0.9, skin=SKIN[3], hair=dict(style='curly', color=HAIR['moss']), gloves=C['lilac'],
                  face=dict(iris=IRIS[2], nose='long', mouth='o', brows=0.9, eye_size=1.05),
                  hat=dict(kind='goggles', color=C['teal_dark'], lens=C['teal_light']),
                  top=dict(kind='labcoat', color='#FBFDFF', accent='#E3F4EE', under=C['teal_light'], buttons=C['teal']),
                  bottom=dict(kind='trousers', color='#4A4F6E', accent='#3B3F5A'),
                  shoes=dict(kind='sneaker', color=C['lilac'], sole=C['white'], accent=C['yellow']),
                  hands=dict(R='grip', L='grip'), extras=[('held', dict(item='pipette', side='R')), ('held', dict(item='clipboard', side='L'))]),
    )


TYPE_COLOURS = dict(casual=[C['teal'], '#2BB4C9', '#3FCB9A', '#4FB8A8'], bargain=[C['yellow'], '#FFB238', '#F7D154'],
                    collector=[C['coral'], '#F2685E', '#FF8F70'], premium=[C['lilac'], '#B79CFF', '#9575F0'])


def visitor(kind, seed, heads=None):
    """A visitor of a kind, varied by seed: skin, hair, colours, height and build; the kind's mark always kept."""
    rng = random.Random(seed)
    spec = dict(cast()[kind])
    spec = {k: (dict(v) if isinstance(v, dict) else v) for k, v in spec.items()}
    spec['skin'] = rng.choice(SKIN)
    style = rng.choice(['crop', 'bob', 'bun', 'curly', 'ponytail', 'puffs', 'spiky', 'long', 'crop'])
    hair_col = rng.choice([HAIR['black'], HAIR['brown'], HAIR['chestnut'], HAIR['auburn'], HAIR['blond'], HAIR['ginger'], HAIR['black'], HAIR['pink'], HAIR['teal'], HAIR['lilac']])
    spec['hair'] = dict(style=style, color=hair_col, fringe=rng.random() < 0.5, band=rng.choice([C['coral'], C['yellow'], C['lime']]))
    spec['face'] = dict(spec.get('face', {}))
    spec['face'].update(iris=rng.choice(IRIS), nose=rng.choice(['button', 'button', 'bulb', 'long']), mouth=rng.choice(['smile', 'smile', 'small', 'grin', 'smirk']),
                        freckles=rng.random() < 0.25, wide=rng.uniform(0.95, 1.06), eye_size=rng.uniform(0.9, 1.08), brows=rng.uniform(0.8, 1.2))
    spec['height'] = spec.get('height', 1.6) * rng.uniform(0.93, 1.06)
    spec['build'] = spec.get('build', 1.0) * rng.uniform(0.92, 1.1)
    spec['belly'] = rng.choice([0.0, 0.0, 0.4, 0.7])
    if heads: spec['heads'] = heads
    top = dict(spec['top']); top['color'] = rng.choice(TYPE_COLOURS.get(kind, [top['color']])); spec['top'] = top
    if kind == 'casual':
        spec['bottom'] = dict(kind=rng.choice(['trousers', 'trousers', 'shorts']), color=rng.choice(['#5C7FD6', C['cream'], '#2B3A55', '#C9A46A', '#7D6BC4']))
        spec['socks'] = rng.choice(['#FFFFFF', C['yellow'], C['pink']])
        spec['shoes'] = dict(kind='sneaker', color=rng.choice(['#FFFFFF', C['yellow'], C['coral'], '#2B3A55']), sole=rng.choice([C['coral'], C['cream'], C['teal']]), accent=rng.choice([C['teal'], C['pink'], C['lime']]))
        spec['hat'] = None if style not in ('crop', 'bob', 'long', 'ponytail') else rng.choice([None, None, dict(kind='beanie', color=rng.choice([C['coral'], C['yellow'], C['lilac']]), band=C['cream']), dict(kind='beret', color=rng.choice([C['coral'], '#2B3A55']))])
        spec['glasses'] = dict(kind=rng.choice(['round', 'square']), color=rng.choice([C['ink'], C['coral'], C['teal_dark']])) if rng.random() < 0.3 else None
        bagc = rng.choice([C['cream'], C['yellow'], C['pink'], C['lime']])
        spec['extras'] = [('tote', dict(color=bagc, print_=rng.choice([C['coral'], C['teal'], C['lilac']]), side='R'))]
    return spec


# ---------------------------------------------------------------------------------------------------- poses and motion
def idle(style=0.0, arms=None):
    """A relaxed stand: the weight on one leg, the arms a little out and bent, the head tipped."""
    p = {'hips': (0, 0, 4), 'spine': (2, 0, -3), 'head': (-3, 0, 4 + style * 6),
         'hip.L': (-2, 0, 0), 'hip.R': (3, 0, 0), 'knee.R': (6, 0, 0), 'ankle.R': (-3, 0, 0),
         'shoulder.L': (-4, -8, 0), 'shoulder.R': (-2, 8, 0), 'elbow.L': (-22, 0, 0), 'elbow.R': (-24, 0, 0), 'sway': 0.0}
    if arms: p.update(arms)
    return p


def hero_pose(key):
    """Each character's pose on the cast sheets, in their personality."""
    return dict(
        casual=idle(0.4, {'shoulder.R': (-10, 6, 0), 'elbow.R': (-95, 0, 12), 'head': (-4, 0, 10), 'shoulder.L': (2, -10, 0)}),
        bargain=idle(0.0, {'shoulder.R': (-52, 10, 0), 'elbow.R': (-70, 0, 0), 'shoulder.L': (-6, -14, 0), 'elbow.L': (-16, 0, 0),
                           'head': (-8, 0, -6), 'spine': (6, 0, 0)}),
        collector=idle(-0.5, {'shoulder.L': (-30, -6, 0), 'elbow.L': (-92, 0, -20), 'shoulder.R': (-48, 12, 0), 'elbow.R': (-118, 0, 20),
                              'head': (6, 0, -8), 'spine': (-2, 0, 4)}),
        premium=idle(0.2, {'shoulder.R': (-4, 16, 0), 'shoulder.L': (-4, -16, 0), 'elbow.L': (-14, 0, 0), 'elbow.R': (-14, 0, 0),
                           'spine': (-6, 0, 0), 'head': (-10, 0, 4)}),
        guest=idle(-0.2, {'shoulder.L': (-28, -6, 0), 'elbow.L': (-80, 0, -10), 'shoulder.R': (-24, 10, 0), 'elbow.R': (-96, 0, 28),
                          'spine': (-4, 0, 0), 'head': (-8, 0, -4)}),
        pip=idle(0.5, {'shoulder.R': (-10, 112, 0), 'elbow.R': (0, 46, 0), 'wrist.R': (0, 0, -80), 'shoulder.L': (-6, -12, 0),
                       'head': (-2, 0, 12), 'spine': (0, 0, 6)}),
        nori=idle(0.0, {'shoulder.L': (-48, -2, 0), 'shoulder.R': (-48, 2, 0), 'elbow.L': (-56, 0, -14), 'elbow.R': (-56, 0, 14),
                        'spine': (-3, 0, 0), 'head': (-2, 0, 6)}),
        moss=idle(-0.3, {'shoulder.R': (-36, 8, 0), 'elbow.R': (-86, 0, 18), 'shoulder.L': (-22, -6, 0), 'elbow.L': (-82, 0, -24),
                         'head': (10, 0, 8), 'spine': (5, 0, 0)}),
    )[key]


WALKS = dict(
    # stride (hip swing, deg), knee, arm swing, bounce (m), sway (m), twist, lean, head, arms out, elbow bend
    casual=dict(stride=24, knee=42, arm=26, bob=0.02, sway=0.012, twist=6, lean=3, head=-2, out=0, elbow=-14),
    bargain=dict(stride=20, knee=46, arm=34, bob=0.016, sway=0.018, twist=5, lean=8, head=4, out=6, elbow=-28),
    collector=dict(stride=22, knee=36, arm=4, bob=0.012, sway=0.008, twist=3, lean=-2, head=-6, out=0, elbow=-10, behind=True),
    premium=dict(stride=22, knee=38, arm=14, bob=0.018, sway=0.03, twist=11, lean=-5, head=-10, out=14, elbow=-16),
    guest=dict(stride=18, knee=34, arm=10, bob=0.012, sway=0.01, twist=4, lean=-4, head=-8, out=4, elbow=-30),
    pip=dict(stride=24, knee=50, arm=30, bob=0.035, sway=0.012, twist=6, lean=4, head=0, out=4, elbow=-24),
    nori=dict(stride=20, knee=34, arm=0, bob=0.014, sway=0.022, twist=4, lean=2, head=0, out=0, elbow=0, carry=True),
    moss=dict(stride=18, knee=36, arm=8, bob=0.012, sway=0.01, twist=3, lean=8, head=6, out=2, elbow=-40),
)


def walk(t, w):
    """A walking pose at phase t (0..1 is two steps)."""
    a = 2 * math.pi * t
    s, c = math.sin(a), math.cos(a)
    kl = w['knee'] * max(0.0, math.cos(a)) ** 1.5 + 6
    kr = w['knee'] * max(0.0, -math.cos(a)) ** 1.5 + 6
    p = {'lift': w['bob'] * c * c - w['bob'] * 0.5, 'sway': -w['sway'] * c,
         'hips': (0, 0, w['twist'] * s), 'spine': (w['lean'], 0, -w['twist'] * 0.7 * s),
         'head': (w['head'] - w['lean'] * 0.6, 0, w['twist'] * 0.3 * s),
         'hip.L': (-w['stride'] * s - kl * 0.3, 0, 0), 'hip.R': (w['stride'] * s - kr * 0.3, 0, 0),
         'knee.L': (kl, 0, 0), 'knee.R': (kr, 0, 0),
         'ankle.L': (-kl * 0.35 + 6 * s, 0, 0), 'ankle.R': (-kr * 0.35 - 6 * s, 0, 0)}
    if w.get('behind'):
        p.update({'shoulder.L': (28, 0, -10), 'shoulder.R': (28, 0, 10), 'elbow.L': (-70, 0, 20), 'elbow.R': (-70, 0, -20)})
    elif w.get('carry'):
        p.update({'shoulder.L': (-42, -4, 0), 'shoulder.R': (-42, 4, 0), 'elbow.L': (-62, 0, 0), 'elbow.R': (-62, 0, 0)})
    else:
        p.update({'shoulder.L': (w['arm'] * s, -w['out'], 0), 'shoulder.R': (-w['arm'] * s, w['out'], 0),
                  'elbow.L': (w['elbow'] - max(0.0, -s) * 12, 0, 0), 'elbow.R': (w['elbow'] - max(0.0, s) * 12, 0, 0)})
    return p


# ---------------------------------------------------------------------------------------------------- scenes
def sheet(b, ox, color, row, spacing, y=0.3):
    R['backdrop'](b, ox, color); R['sheet_lights'](b, ox)
    rigs = []
    n = len(row)
    for i, (spec, pose, yaw, name) in enumerate(row):
        x = ox + (i - (n - 1) / 2) * spacing
        rig = person(b, spec, name)
        rig.place((x, y), yaw)
        rig.pose(pose)
        for h in rig.held: held(b, rig, **h)
        rigs.append(rig)
    return rigs


def frame_sheet(cam, ox, width, z=0.9, dist=None, lens=50):
    cam.data.sensor_fit = 'HORIZONTAL'; cam.data.lens = lens
    half = math.atan(18 / lens)
    dist = dist or (width / 2) / math.tan(half)
    cam.location = (ox, 0.3 - dist, z + 0.25)
    aim(cam, (ox, 0.3, z))


def aim(cam, target):
    cam.rotation_euler = (Vector(target) - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()


SHOTS = {}
EXPOSURE = -1.15


def shot(name):
    def reg(f): SHOTS[name] = f; return f
    return reg


@shot('visitors')
def s_visitors(b, cam, scene):
    c = cast()
    row = [(c[k], hero_pose(k), yaw, k) for k, yaw in (('casual', -10), ('bargain', 6), ('collector', -6), ('premium', 4), ('guest', -8))]
    rigs = sheet(b, 1000, '#FFE0C8', row, 1.1)
    def go():
        frame_sheet(cam, 1000, 6.3, z=0.9)
        render(scene, 'cast-visitors', 1920, 1080, exposure=EXPOSURE)
    return go


@shot('staff')
def s_staff(b, cam, scene):
    c = cast()
    row = [(c[k], hero_pose(k), yaw, k) for k, yaw in (('pip', -8), ('nori', 0), ('moss', 8))]
    sheet(b, 1200, '#C6EEDD', row, 1.2)
    def go():
        frame_sheet(cam, 1200, 4.2, z=0.9)
        render(scene, 'cast-staff', 1600, 1080, exposure=EXPOSURE)
    return go


@shot('faces')
def s_faces(b, cam, scene):
    c = cast()
    keys = ['casual', 'bargain', 'collector', 'premium', 'guest', 'pip', 'nori', 'moss']
    row = [(c[k], hero_pose(k), 0, k) for k in keys]
    rigs = sheet(b, 1400, '#FFF1E0', row, 1.6)
    def go():
        for k, rig in zip(keys, rigs):
            bpy.context.view_layer.update()
            hcw = rig.j['head'].matrix_world @ (rig.hc - rig.w['head'])
            cam.data.sensor_fit = 'HORIZONTAL'; cam.data.lens = 80
            cam.location = hcw + Vector((0.32, -1.55, 0.05))
            aim(cam, hcw + Vector((0, 0, -0.03)))
            render(scene, 'face-' + k, 640, 640, exposure=EXPOSURE)
    return go


@shot('variety')
def s_variety(b, cam, scene):
    row = []
    for i in range(7):
        spec = visitor('casual', 100 + i)
        row.append((spec, idle(((i * 37) % 10) / 10 - 0.5, {'shoulder.R': (-6, 0, 0), 'elbow.R': (-60, 0, 10)}), ((i * 53) % 21) - 10, 'v%d' % i))
    sheet(b, 1700, '#E3F1FF', row, 0.9)
    def go():
        frame_sheet(cam, 1700, 6.6, z=0.9)
        render(scene, 'variety', 1920, 1000, exposure=EXPOSURE)
    return go


@shot('builds')
def s_builds(b, cam, scene):
    c = cast()
    row = []
    for g, heads in enumerate((2.6, 3.0, 3.8)):
        for k in ('casual', 'bargain', 'collector'):
            spec = dict(c[k]); spec['heads'] = heads
            row.append((spec, idle(0.0), 0, '%s-%s' % (k, heads)))
    xs = []
    R['backdrop'](b, 2000, '#F3E8FF'); R['sheet_lights'](b, 2000)
    for i, (spec, pose, yaw, name) in enumerate(row):
        g, k = divmod(i, 3)
        x = 2000 + (g - 1) * 2.75 + (k - 1) * 0.78
        rig = person(b, spec, name); rig.place((x, 0.3), yaw); rig.pose(pose)
        for h in rig.held: held(b, rig, **h)
    def go():
        frame_sheet(cam, 2000, 8.4, z=0.9)
        render(scene, 'builds', 1920, 900, exposure=EXPOSURE)
    return go


BUILT = set()
ROOM_OBJECTS = set()


def room(b, name):
    """GR-16's rooms (and the landscape outside), each built once however many shots use them, and shown in every shot."""
    if name in BUILT: return
    BUILT.add(name)
    before = set(bpy.data.objects.keys())
    R[name](b)
    ROOM_OBJECTS.update(set(bpy.data.objects.keys()) - before)


def placed(b, spec, name, at, yaw, pose):
    rig = person(b, spec, name)
    rig.place(at, yaw)
    rig.pose(pose)
    for h in rig.held: held(b, rig, **h)
    return rig


def eye_camera(cam, at, target, fov=62):
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(fov)
    cam.location = at
    aim(cam, target)


@shot('shop')
def s_shop(b, cam, scene):
    """Shop unit A by day from the player's eye: a collector and a bargain hunter at the Starter tank, a visitor paying at the
    counter where Pip hands over a creature, a big spender waiting in the queue, a visitor leaving with a bag."""
    room(b, 'outdoors'); room(b, 'shop')
    c = cast()
    col = dict(c['collector']); col['hands'] = dict(L='grip', R='point')
    placed(b, col, 'collector', (-0.9, 2.28), 186, idle(0.0, {'spine': (16, 0, 0), 'head': (4, 0, -6), 'hips': (0, 0, 0),
                                                              'shoulder.R': (-78, 10, 0), 'elbow.R': (-16, 0, 0), 'shoulder.L': (-30, -6, 0), 'elbow.L': (-92, 0, -20)}))
    bar = visitor('bargain', 7); bar['hands'] = dict(R='relaxed', L='grip')
    bar['extras'] = [e for e in bar['extras'] if e[0] != 'held'] + [('held', dict(item='netbag', side='L'))]
    placed(b, bar, 'bargain', (-1.75, 1.95), 168, idle(0.0, {'spine': (24, 0, 0), 'head': (-6, 0, 4), 'shoulder.R': (-36, 4, 0), 'elbow.R': (-60, 0, 0),
                                                             'hip.L': (-14, 0, 0), 'knee.L': (16, 0, 0), 'hip.R': (-14, 0, 0), 'knee.R': (16, 0, 0)}))
    cas = visitor('casual', 103); cas['hands'] = dict(R='grip', L='relaxed')
    cas['extras'] = cas['extras'] + [('held', dict(item='coin', side='R'))]
    placed(b, cas, 'payer', (1.14, 2.08), 98, idle(0.2, {'shoulder.R': (-74, 6, 0), 'elbow.R': (-24, 0, 0), 'head': (-6, 0, 0), 'spine': (4, 0, 0)}))
    pre = dict(c['premium']); pre['hands'] = dict(R='grip', L='relaxed')
    pre['extras'] = [e for e in pre['extras'] if not (e[0] == 'held' and e[1]['side'] == 'L')]
    placed(b, pre, 'premium', (0.4, 1.3), 108, idle(0.0, {'shoulder.L': (-62, -30, 0), 'elbow.L': (-96, 0, 30), 'head': (14, 0, 0)}))
    lv = visitor('casual', 111); lv['hands'] = dict(R='grip', L='relaxed')
    lv['extras'] = lv['extras'] + [('held', dict(item='creature_bag', side='R', color=C['coral']))]
    placed(b, lv, 'leaving', (0.1, 0.35), 150, walk(0.25, WALKS['casual']))

    # The player behind the counter, holding out a bag to the visitor paying.
    eye_camera(cam, (2.78, 1.5, 1.6), (0.2, 1.75, 1.15))
    o, rt, up, fw = cam_axes(cam)
    w = o + fw * 0.5 + rt * 0.22 - up * 0.08
    fp_arm(b, cam, 'R', 'grip', w, fw * 0.5 + up * 0.5 - rt * 0.3, None, across=Vector((0, 0, -1)))
    bag_(b, w + fw * 0.03 - rt * 0.02 - up * 0.07, 0.5, color=C['coral'], species='Bubblebelly')
    counter_cam = (Vector(cam.location), cam.rotation_euler.copy())

    def go():
        cam.location, cam.rotation_euler = counter_cam
        render(scene, 'shop-counter', 1920, 1080, exposure=-0.15)
        eye_camera(cam, (0.55, 2.0, 1.55), (-1.4, 2.55, 1.15))
        render(scene, 'shop-tank', 1920, 1080, exposure=-0.15)
    return go


@shot('lab')
def s_lab(b, cam, scene):
    """The back room: Moss doses the incubator with a pipette; Nori carries a jar to the tanks."""
    room(b, 'outdoors'); room(b, 'lab')
    c = cast()
    placed(b, c['moss'], 'moss', (200.92, 4.42), 8, idle(0.0, {'spine': (14, 0, 0), 'head': (16, 0, 0), 'shoulder.R': (-104, 4, 0), 'elbow.R': (-20, 0, 0),
                                                                 'shoulder.L': (-22, -6, 0), 'elbow.L': (-82, 0, -24)}))
    placed(b, c['nori'], 'nori', (199.75, 2.35), 52, walk(0.3, WALKS['nori']))

    def go():
        eye_camera(cam, (200.55, 0.85, 1.6), (200.6, 4.0, 1.12))
        render(scene, 'lab', 1920, 1080, exposure=-0.3)
    return go


GLOVE, SLEEVE, CUFF = C['yellow'], C['teal'], C['cream']


def fp_arm(b, cam, side, shape, wrist, fingers, palm, across=None):
    """The player's arm in view: a sleeve and a glove, built hanging at the origin, then turned so the fingers point along
    `fingers` and the palm faces `palm` (world directions), the wrist at `wrist`."""
    s = 1 if side == 'L' else -1
    spec = dict(gloves=GLOVE, hand=0.62)
    parts = hands(b, None, side, shape, spec, at=Vector((0, 0, 0)), m=M(b, 'gloss', GLOVE))
    parts.append(b.torus('Glove cuff', (0, 0, 0.03), 0.038, 0.016, M(b, 'gloss', GLOVE)))
    parts.append(curve(b, 'Forearm', [Vector((0, 0, 0.02)), Vector((0, 0, 0.42))], [0.036, 0.048], M(b, 'cloth', SLEEVE), poly=True))
    parts.append(b.torus('Rolled sleeve', (0, 0, 0.14), 0.045, 0.02, M(b, 'cloth', CUFF)))
    e = bpy.data.objects.new('Arm ' + side, None); bpy.context.scene.collection.objects.link(e)
    for p in parts: p.parent = e
    z = -Vector(fingers).normalized()
    if across is not None:
        y = Vector(across).normalized(); y = (y - z * y.dot(z)).normalized()
        x = y.cross(z)
    else:
        pn = Vector(palm).normalized()
        x = (pn if s < 0 else -pn)
        x = (x - z * x.dot(z)).normalized()
        y = z.cross(x)
    from mathutils import Matrix
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(wrist)
    e.matrix_world = m
    return e


def cam_axes(cam):
    bpy.context.view_layer.update()
    r = cam.matrix_world.to_3x3()
    return Vector(cam.location), r @ Vector((1, 0, 0)), r @ Vector((0, 1, 0)), r @ Vector((0, 0, -1))


@shot('hands-carry')
def s_hands_carry(b, cam, scene):
    """The player's hands: three creatures carried in the shop's bags, through the back room."""
    room(b, 'outdoors'); room(b, 'lab')
    eye_camera(cam, (201.75, 1.35, 1.62), (200.8, 3.9, 1.0), fov=62)
    o, rt, up, fw = cam_axes(cam)
    w = o + fw * 0.46 - rt * 0.15 - up * 0.02
    fp_arm(b, cam, 'L', 'grip', w, fw * 0.45 + up * 0.55 + rt * 0.35, None, across=Vector((0, 0, -1)))
    knot = w + fw * 0.02 + rt * 0.03 - up * 0.04
    for k in range(3):
        side = rt * (k - 1) * 0.07 + fw * (0.02 * (1 - abs(k - 1)))
        bag_(b, knot + side - up * 0.07, 0.5, color=(C['teal'], C['coral'], C['yellow'])[k], species=('Pondlet', 'Bubblebelly', 'Lanternfin')[k])
    carry_cam = (Vector(cam.location), cam.rotation_euler.copy())

    def go():
        cam.location, cam.rotation_euler = carry_cam
        render(scene, 'hands-carry', 1600, 900, exposure=-0.3)
    return go


@shot('hands-siphon')
def s_hands_siphon(b, cam, scene):
    """The player's hand siphoning culture from a grower."""
    room(b, 'outdoors'); room(b, 'lab')
    eye_camera(cam, (199.15, 2.3, 1.62), (198.2, 4.25, 0.95), fov=62)
    o, rt, up, fw = cam_axes(cam)
    w = o + fw * 0.34 + rt * 0.15 - up * 0.17
    aimv = (Vector((198.1, 4.25 - 0.26, 0.95)) - w).normalized()
    fp_arm(b, cam, 'R', 'cup', w, aimv, -rt * 0.8 + up * 0.2)
    g = w + aimv * 0.06
    tip = g + aimv * 0.3
    back = g + aimv * 0.04
    b.cyl('Siphon tube', (tip + back) / 2, 0.022, (tip - back).length, b.mat('glass', '#F2FCFF', alpha=0.25), r=0).rotation_euler = (tip - back).to_track_quat('Z', 'Y').to_euler()
    b.cyl('Siphon fill', (tip * 0.4 + back * 0.6), 0.016, (tip - back).length * 0.55, b.mat('liquid', BLOOM, glow=1.4), r=0.004).rotation_euler = (tip - back).to_track_quat('Z', 'Y').to_euler()
    b.ball('Siphon bulb', g - aimv * 0.005, (0.042, 0.042, 0.042), M(b, 'gloss', C['coral']), seg=24)
    b.ball('Siphon nozzle', tip, 0.014, M(b, 'gloss', C['teal_dark']), seg=12)
    for k in range(3):
        b.ball('Siphon drop', tip + fw * (0.05 + k * 0.04) - up * 0.01 * k, 0.008 + 0.003 * k, b.mat('liquid', BLOOM, glow=1.6), seg=10)
    lab_cam = (Vector(cam.location), cam.rotation_euler.copy())

    def go():
        cam.location, cam.rotation_euler = lab_cam
        render(scene, 'hands-siphon', 1600, 900, exposure=-0.3)
    return go


def clip(scene, name, frames, w=1280, h=720, exposure=None, samples=24):
    """Renders the keyed frames 1..frames as an MP4 (H.264) the board plays in a loop."""
    scene.frame_start, scene.frame_end = 1, (args.frames or frames)
    scene.render.fps = 24
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.resolution_percentage = 50 if args.quick else 100
    scene.eevee.taa_render_samples = 8 if args.quick else samples
    scene.view_settings.exposure = EXPOSURE if exposure is None else exposure
    im = scene.render.image_settings
    im.media_type = 'VIDEO'; im.file_format = 'FFMPEG'
    ff = scene.render.ffmpeg
    ff.format = 'MPEG4'; ff.codec = 'H264'; ff.constant_rate_factor = 'HIGH'; ff.ffmpeg_preset = 'GOOD'
    ff.gopsize = 12
    scene.render.use_file_extension = False
    if args.peek:
        im.media_type = 'IMAGE'; im.file_format = 'PNG'; scene.render.use_file_extension = True
        for k in range(args.peek):
            fr = 1 + k * frames // args.peek
            scene.frame_set(fr)
            scene.render.filepath = str(OUT / ('%s-f%02d.png' % (name, fr)))
            bpy.ops.render.render(write_still=True)
        return
    scene.render.filepath = str(OUT / (name + '.mp4'))
    bpy.ops.render.render(animation=True)
    print('RENDERED', scene.render.filepath)
    im.media_type = 'IMAGE'


def animate(rigs, fns, frames):
    """Keys every rig on every frame from its pose function of t (0..1 over the loop)."""
    for f in range(1, frames + 1):
        t = (f - 1) / frames
        for rig, fn in zip(rigs, fns):
            rig.pose(fn(t))
            rig.key(f)
    for rig in rigs:
        for e in rig.j.values():
            if e.animation_data and e.animation_data.action:
                for fc in getattr(e.animation_data.action, 'fcurves', []):
                    for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'


def merge(*poses):
    out = {}
    for p in poses:
        for k, v in p.items():
            if k in out and isinstance(v, tuple):
                out[k] = tuple(a + b for a, b in zip(out[k], v))
            elif k in out and isinstance(v, float):
                out[k] = out[k] + v
            else:
                out[k] = v
    return out


def wave(t, n=1, phase=0.0):
    return math.sin(2 * math.pi * (n * t + phase))


def pulse(t, n=1, phase=0.0, sharp=3):
    """0..1, a soft beat n times a loop."""
    return max(0.0, math.sin(2 * math.pi * (n * t + phase))) ** sharp


# ---- the visitors' moments
def m_happy(t):
    """Hugging the bag they bought, two little hops."""
    h = pulse(t, 2, 0.0, 2)
    crouch = pulse(t, 2, 0.6, 2)
    return merge(idle(0.3), {'lift': 0.13 * h - 0.04 * crouch, 'knee.L': (28 * crouch, 0, 0), 'knee.R': (28 * crouch - 6, 0, 0),
                             'hip.L': (-14 * crouch, 0, 0), 'hip.R': (-14 * crouch, 0, 0), 'ankle.L': (-12 * crouch + 18 * h, 0, 0), 'ankle.R': (-12 * crouch + 18 * h, 0, 0),
                             'shoulder.L': (-58, -12, 0), 'shoulder.R': (-58, 12, 0), 'elbow.L': (-92, 0, -40), 'elbow.R': (-92, 0, 40),
                             'head': (-14 * h, 0, 10 * wave(t, 2)), 'spine': (-6 * h, 0, 0)})


def m_wait(t):
    """Waiting in the queue: a foot taps, a look at the wrist, a sigh."""
    tap = pulse(t, 6, 0.0, 1)
    look = smooth(min(1.0, max(0.0, (math.sin(2 * math.pi * t) + 0.2) * 1.5)))
    return merge(idle(0.0), {'ankle.R': (-30 * tap, 0, 0), 'knee.R': (12 * tap, 0, 0), 'hip.R': (-6 * tap, 0, 0),
                             'shoulder.L': (-62 * look, -14, 0), 'elbow.L': (-110 * look, 0, -60 * look), 'head': (22 * look, 0, -24 * look),
                             'spine': (4 + 4 * wave(t, 1, 0.5), 0, 0), 'lift': -0.01 * pulse(t, 1, 0.75)})


def m_browse(t):
    """At a tank: leaning in, pointing at something swimming past, a nod."""
    lean = 0.5 + 0.5 * wave(t, 1)
    point = smooth(min(1.0, max(0.0, wave(t, 1, 0.1) * 2)))
    return merge(idle(-0.4), {'spine': (10 + 12 * lean, 0, 0), 'head': (2 + 10 * pulse(t, 2, 0.3), 0, -6 + 10 * wave(t, 1, 0.3)),
                              'shoulder.R': (-20 - 56 * point, 10, 0), 'elbow.R': (-30 + 22 * point, 0, 0),
                              'hip.L': (-6 * lean, 0, 0), 'hip.R': (-6 * lean, 0, 0), 'knee.L': (6 * lean, 0, 0), 'knee.R': (6 + 6 * lean, 0, 0)})


def m_unhappy(t):
    """Nothing they could afford: a shrug with the palms up, the head shakes, the shoulders sag."""
    shrug = pulse(t, 1, 0.1, 1)
    shake = wave(t, 4) * (1 - shrug) * 0.6
    return merge(idle(0.0), {'shoulder.L': (-30 * shrug, -50 * shrug - 8, 0), 'shoulder.R': (-30 * shrug, 50 * shrug + 8, 0),
                             'elbow.L': (-80 * shrug - 20, 0, -50 * shrug), 'elbow.R': (-80 * shrug - 20, 0, 50 * shrug),
                             'head': (6 + 8 * (1 - shrug), 0, 22 * shake), 'spine': (8 * (1 - shrug), 0, 0), 'lift': 0.02 * shrug})


def m_clap(t):
    """An exhibition guest applauding."""
    c = pulse(t, 8, 0.0, 1)
    return merge(idle(-0.2), {'shoulder.L': (-58, -6, 0), 'shoulder.R': (-58, 6, 0), 'elbow.L': (-70, 0, -50 + 14 * c), 'elbow.R': (-70, 0, 50 - 14 * c),
                              'head': (-10, 0, 6 * wave(t, 2)), 'spine': (-4, 0, 0), 'lift': 0.01 * pulse(t, 4)})


# ---- the helpers at work
def w_pip(t):
    """Pip at the till: a sweep of the scanner, a nod to the visitor, a wave goodbye."""
    sweep = wave(t, 2)
    bye = smooth(min(1.0, max(0.0, (wave(t, 1, 0.75) - 0.3) * 3)))
    return merge(idle(0.4), {'shoulder.L': (-56, -10, 26 * sweep), 'elbow.L': (-60, 0, -20), 'head': (8 - 10 * pulse(t, 2, 0.25), 0, 10 * sweep),
                             'shoulder.R': (-10 - 10 * bye, 20 + 92 * bye, 0), 'elbow.R': (-20 * (1 - bye), 46 * bye, 0), 'wrist.R': (0, 0, -80 * bye + 18 * bye * wave(t, 6)),
                             'spine': (4, 0, 6 * sweep), 'lift': 0.015 * pulse(t, 4)})


def w_nori(t):
    """Nori reaches the jar forward over a tank's rim to let the creature out, and brings it back, bending the knees."""
    up = 0.5 - 0.5 * math.cos(2 * math.pi * t)
    dip = pulse(t, 1, 0.75, 2)
    return merge(idle(0.0), {'shoulder.L': (-48 - 26 * up, -2, 0), 'shoulder.R': (-48 - 26 * up, 2, 0), 'elbow.L': (-56 + 40 * up, 0, -14), 'elbow.R': (-56 + 40 * up, 0, 14),
                             'wrist.L': (0, 0, 0), 'wrist.R': (34 * pulse(t, 1, 0.1, 2), 0, 0),
                             'knee.L': (24 * dip, 0, 0), 'knee.R': (24 * dip, 0, 0), 'hip.L': (-12 * dip, 0, 0), 'hip.R': (-12 * dip, 0, 0), 'ankle.L': (-12 * dip, 0, 0), 'ankle.R': (-12 * dip, 0, 0),
                             'lift': -0.04 * dip + 0.02 * up, 'head': (-8 * up, 0, 0), 'spine': (-4 * up, 0, 0)})


def w_moss(t):
    """Moss squeezes three drops from the pipette, checks the clipboard, nods."""
    drop = pulse(t, 3, 0.0, 4)
    check = smooth(min(1.0, max(0.0, (wave(t, 1, 0.6) - 0.2) * 3)))
    return merge(idle(-0.3), {'shoulder.R': (-36 - 8 * drop, 8, 0), 'elbow.R': (-86 + 10 * drop, 0, 18), 'shoulder.L': (-22 - 10 * check, -6, 0), 'elbow.L': (-82 - 10 * check, 0, -24),
                              'head': (10 + 10 * check, 0, 8 - 26 * check), 'spine': (5 + 3 * drop, 0, 0), 'lift': -0.01 * drop})


@shot('walk')
def s_walk(b, cam, scene):
    """The five kinds of visitor walking, each in their own way (a loop of 3 seconds: 2, 3 or 4 strides)."""
    c = cast()
    keys = ['casual', 'bargain', 'collector', 'premium', 'guest']
    row = [(c[k], idle(), -28, k) for k in keys]
    rigs = sheet(b, 2400, '#FFE0C8', row, 1.1)
    cycles = dict(casual=3, bargain=4, collector=2, premium=2, guest=3)
    animate(rigs, [(lambda t, k=k: walk(t * cycles[k], WALKS[k])) for k in keys], 72)

    def go():
        frame_sheet(cam, 2400, 6.3, z=0.9)
        clip(scene, 'walk', 72)
    return go


@shot('moments')
def s_moments(b, cam, scene):
    """What visitors do: hug what they bought, wait in the queue, browse a tank, shrug at prices, applaud at the exhibition."""
    c = cast()
    cas = dict(c['casual']); cas['hands'] = dict(L='grip', R='grip'); cas['extras'] = cas['extras'] + [('held', dict(item='creature_bag', side='R', color=C['coral']))]
    bar = dict(c['bargain']); bar['hands'] = dict(L='relaxed', R='grip'); bar['extras'] = [e for e in bar['extras'] if not (e[0] == 'held' and e[1]['side'] == 'L')]
    col = dict(c['collector']); col['hands'] = dict(L='grip', R='point')
    pre = dict(c['premium']); pre['hands'] = dict(L='open', R='open'); pre['extras'] = [e for e in pre['extras'] if e[0] != 'held']
    gue = dict(c['guest']); gue['hands'] = dict(L='open', R='open'); gue['extras'] = [e for e in gue['extras'] if e[0] != 'held']
    fns = [m_happy, m_wait, m_browse, m_unhappy, m_clap]
    row = [(s, fn(0.0), yaw, k) for s, fn, yaw, k in zip((cas, bar, col, pre, gue), fns, (-8, 10, -24, 4, -6), ('happy', 'wait', 'browse', 'unhappy', 'clap'))]
    rigs = sheet(b, 2600, '#D6E9FF', row, 1.1)
    animate(rigs, fns, 96)

    def go():
        frame_sheet(cam, 2600, 6.3, z=0.9)
        clip(scene, 'moments', 96)
    return go


@shot('staff-work')
def s_staff_work(b, cam, scene):
    """The helpers at work: Pip at the till, Nori lifting a jar, Moss dosing."""
    c = cast()
    keys = ['pip', 'nori', 'moss']
    pip = dict(c['pip']); pip['hands'] = dict(L='grip', R='open')
    specs = [pip, c['nori'], c['moss']]
    fns = [w_pip, w_nori, w_moss]
    row = [(s, fn(0.0), yaw, k + '-work') for s, fn, yaw, k in zip(specs, fns, (-8, 0, 8), keys)]
    rigs = sheet(b, 2800, '#C6EEDD', row, 1.2)
    animate(rigs, fns, 96)

    def go():
        frame_sheet(cam, 2800, 4.2, z=0.9)
        clip(scene, 'staff-work', 96)
    return go


def render(scene, name, w, h, exposure=0.0, samples=None):
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.resolution_percentage = 50 if args.quick else 100
    scene.eevee.taa_render_samples = 16 if args.quick else (samples or 96)
    scene.view_settings.exposure = exposure
    scene.render.image_settings.media_type = 'IMAGE'
    scene.render.image_settings.file_format = 'JPEG' if args.jpg else 'PNG'
    if args.jpg: scene.render.image_settings.quality = 90
    scene.render.use_file_extension = False
    scene.render.filepath = str(OUT / (name + ('.jpg' if args.jpg else '.png')))
    bpy.ops.render.render(write_still=True)
    print('RENDERED', scene.render.filepath)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    R['setup'](scene)
    b = B()
    cam = bpy.data.objects.new('Eye', bpy.data.cameras.new('Eye')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.clip_start = 0.05
    names = [n for n in SHOTS if not args.only or n in args.only.split(',')]
    gos, cols = [], []
    for n in names:
        before = set(bpy.data.objects.keys())
        gos.append(SHOTS[n](b, cam, scene))
        col = bpy.data.collections.new('Shot ' + n); scene.collection.children.link(col)
        for k in set(bpy.data.objects.keys()) - before - ROOM_OBJECTS:
            o = bpy.data.objects[k]
            if o == cam: continue
            for c in list(o.users_collection): c.objects.unlink(o)
            col.objects.link(o)
        cols.append(col)
    for go, col in zip(gos, cols):
        for c in cols: c.hide_render = c is not col
        go()


if __name__ == '__main__':
    main()
