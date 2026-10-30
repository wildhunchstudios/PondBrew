"""Original Evolution Shop people kit (GR-18 waves 1 to 4): the visitors, the exhibition's guests, the helpers (Pip, Nori
and Moss) and the player's hands in the look the user approved on 2026-10-29 (GR.55, "Looks good"). Built with the look board's own builder (Tools/Art/gr18_characters.py, executed as a
library), so the game's people are the board's. Blender 5.0, no external assets or add-ons.

Run: blender --background --factory-startup --python Tools/Art/create_people_kit.py -- --repo C:/dev/DumbVideoGameStudio

What it makes (Resources/Art/PeopleKit; its record in ArtSource/PeopleKit/manifest.json):
- PK_Body_<kind>.fbx for each kind of visitor (casual, bargain, collector, premium, guest) and each helper (pip, nori, moss,
  in their own colours, Moss in lilac gloves): a mesh per joint and variant,
  each named "<joint>__<variant>" directly under the asset's root, in the kind's rest pose (feet at the origin): the spine
  (the kind's top and what it wears on it), the hips, shoulders, elbows, hips, knees and ankles (with the shoes), the legs'
  variants (the casual visitor's trousers or shorts) and four hand shapes a side (relaxed, grip, point, open).
- PK_Head.fbx: the head's parts on a head one unit tall, its joint at the origin (the game scales it by each kind's head
  height): the skull with its neck, ears and cheeks; the eyes; eyelids, lashes; brows; three noses; five mouths; a mustache,
  freckles, a beauty mark; every hair style, the fringes; every hat; glasses and sunglasses.
- PK_Held.fbx: what visitors hold, in each wrist's frame of a person whose head is 0.5333 tall (1.6 m, three heads): the
  shop's water bag, coins, coupons, a net bag of shopping, a notebook, shopping bags, a programme; and the helpers' jar
  (carried in both arms, on the right wrist), pipette (its fill the DOSE role) and clipboard.
- PK_Hands.fbx (wave 3): the player's arms in first person, the shop's yellow gloves and turquoise sleeves rolled up in cream
  (arm__<side>, and hand-<shape>__<side> for relaxed, grip, open, point and cup), each with its wrist at the origin, the
  fingers down and the forearm up; and what they hold: the shop's water bag (bag__fp, its knot at the origin, its twist tie
  the TIE role), the siphon's bulb, tube, a fill band one metre long (DOSE) and the vacuum's canister, and a length of pipe.
- PK_Gags.fbx (wave 4, §6.7's comedy): faces' parts on the head kit's head (a wobbly grimace, eyes squeezed shut, a
  slap's hand mark on the left cheek) and props in metres (a ring of dizzy stars, a puff of a cloud, a burp's bubble, a note).
- PK_Wish.fbx (GR-20, visitors' wishes): a thought bubble (a cream cloud with an ink rim and a trail of two puffs to the head,
  its front facing the viewer) and what shows in it, in metres: a fish token in plain teal and in each colour (Sunny, Deep,
  Shimmer), the colours' signs (a sun, a drop, a sparkle), a size pip, and the heart a granted wish pops into.
- people.json (beside them, a TextAsset): each kind's joints (rest positions in Unity space), its head height, and each part's
  name, so the game assembles a person from these parts and turns their joints.

Materials: every slot is named PK_<kind>_<colour>: the look's material kind (cloth, skin, hair, gloss, eye, plastic, metal,
glass, water, liquid, ...) and a colour, either a fixed sRGB hex or a role the game fills in for each person (SKIN, HAIR,
IRIS, TOP, BOTTOM, SOCKS, SHOE, SOLE, SHOEACC, BAG, PRINT, HAT, HATBAND, BAND, GLASSES, DOSE, TIE).

Coordinates: built as the board is (Blender, Z up, facing -Y); Unity sees Blender (x, y, z) at (-x, z, -y) (the mapping
create_diorama_kit.py probed, bake_space_transform on), so people face Unity +Z with their left at Unity -X. Each part's
mesh keeps its rest position (its object at the asset's origin), as the growth kit's do.
"""
import argparse
import copy
import hashlib
import json
import math
import sys
import tempfile
from pathlib import Path

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument('--repo', required=True)
ap.add_argument('--only', default='', help='Comma-separated assets to build (Body_<kind>, Head, Held, Hands, Gags, Wish); the rest are left as they are.')
args = ap.parse_args(argv)
REPO = Path(args.repo).resolve()
assert (REPO / 'EvolutionShop/ProjectSettings/ProjectVersion.txt').is_file()
SOURCE = REPO / 'ArtSource' / 'PeopleKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/PeopleKit'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
ONLY = set(a for a in args.only.split(',') if a)

# ---------------------------------------------------------------------------------------------------- the board's builder
LIB = REPO / 'Tools' / 'Art' / 'gr18_characters.py'
_saved = sys.argv
sys.argv = ['gr18_lib', '--', '--repo', str(REPO), '--out', tempfile.gettempdir()]
G = {'__name__': 'gr18_lib'}
exec(compile(LIB.read_text(encoding='utf-8'), str(LIB), 'exec'), G)
sys.argv = _saved

# The roles a person's colours fill in, and how they preview in Blender.
PREVIEW = dict(SKIN='#EFB088', HAIR='#6B4329', IRIS='#5A3A24', TOP='#22BFB0', BOTTOM='#5C7FD6', SOCKS='#FFFFFF', SHOE='#FFFFFF',
               SOLE='#FF7A7A', SHOEACC='#22BFB0', BAG='#FFF3DF', PRINT='#FF7A7A', HAT='#FF7A7A', HATBAND='#FFF3DF', BAND='#FF7A7A',
               GLASSES='#2B3A55', DOSE='#FF5E7E', TIE='#22BFB0')
SLOTS = {}


def slot(kind, color):
    c = str(color)
    role = c.lstrip('#').upper() if c.startswith('#') else c.upper()
    name = 'PK_%s_%s' % (kind, role)
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = G['lin'](c if c.startswith('#') else PREVIEW.get(role, '#FF00FF'))
        SLOTS[name] = {'kind': kind, 'colour': ('#' + role) if c.startswith('#') else role}
    return m


class KitB(G['B']):
    """The builder's shapes with fewer segments (a game's budget) and the kit's material slots."""

    def mat(self, kind, color, **kw):
        return slot(kind, color)

    def ball(self, name, center, scale, m, seg=40, shadow=True):
        return super().ball(name, center, scale, m, seg=max(10, int(seg * 0.55)), shadow=shadow)

    def cyl(self, name, center, radius, depth, m, r=0.03, rot=(0, 0, 0), verts=48, seg=4):
        return super().cyl(name, center, radius, depth, m, r=r, rot=rot, verts=max(12, int(verts * 0.5)), seg=min(seg, 2))

    def box(self, name, center, size, m, r=0.04, seg=5, rot=(0, 0, 0), shadow=True):
        return super().box(name, center, size, m, r=r, seg=min(seg, 2), rot=rot, shadow=shadow)

    def torus(self, name, center, major, minor, m, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=center, rotation=[math.radians(a) for a in rot],
                                         major_segments=24, minor_segments=8)
        o = bpy.context.object; o.name = name
        return self._finish(o, m, normals=False)

    def creature(self, species, at, length, yaw):
        # The game puts its own creature in a bag or a jar (GrowthCreatureView): the kit carries none.
        e = bpy.data.objects.new('No creature', None); bpy.context.scene.collection.objects.link(e)
        e.location = at
        return e


G['M'] = lambda b, kind, color, **kw: slot(kind, color)
_blob, _curve, _shell = G['blob'], G['curve'], G['shell']
G['blob'] = lambda *a, **kw: _blob(*a, **dict(kw, seg=max(16, int(kw.get('seg', 40) * 0.6))))
G['curve'] = lambda *a, **kw: _curve(*a, **dict(kw, res=min(kw.get('res', 5), 3)))
G['shell'] = lambda *a, **kw: _shell(*a, **dict(kw, seg=40))
b = KitB()
person, hands, legs, head, held, Rig = G['person'], G['hands'], G['legs'], G['head'], G['held'], G['Rig']


def unity(v):
    return [round(-v[0], 5), round(v[2], 5), round(-v[1], 5)]


def stem(name):
    return name.split('.')[0]


def bake(objs, name):
    """Converts parts (curves, modifiers) to one plain mesh in world space, its object at the origin."""
    objs = [o for o in objs if o.type in ('MESH', 'CURVE', 'FONT')]
    if not objs: return None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target='MESH')
    sel = [o for o in bpy.context.selected_objects]
    for o in sel:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    bpy.context.view_layer.objects.active = sel[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(sel) > 1: bpy.ops.object.join()
    m = bpy.context.view_layer.objects.active
    m.name = name; m.data.name = name
    return m


def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.curves):
        for d in list(coll):
            if d.users == 0: coll.remove(d)


def export(root_name, meshes):
    root = bpy.data.objects.new(root_name, None); bpy.context.scene.collection.objects.link(root)
    for m in meshes: m.parent = root
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for m in meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = root
    dest = EXPORT / (root_name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(dest), use_selection=True, global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
                             bake_space_transform=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=False,
                             mesh_smooth_type='OFF', use_tspace=False, add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    tris = {m.name: sum(len(p.vertices) - 2 for p in m.data.polygons) for m in meshes}
    print('PK_EXPORTED', root_name, len(meshes), sum(tris.values()))
    return {'id': root_name, 'export': str(dest.relative_to(REPO)).replace('\\', '/'), 'sha256': hashlib.sha256(dest.read_bytes()).hexdigest(),
            'bytes': dest.stat().st_size, 'triangles': sum(tris.values()), 'parts': {k: tris[k] for k in sorted(tris)}}


def phase(fn):
    """Runs fn and returns the objects it made."""
    before = set(bpy.data.objects)
    fn()
    return [o for o in bpy.data.objects if o not in before]


# ---------------------------------------------------------------------------------------------------- the bodies
KINDS = ['casual', 'bargain', 'collector', 'premium', 'guest', 'pip', 'nori', 'moss']
# GR-18 wave 2: the helpers, each one person in their own colours (only their skin a role, for the head kit's face).
HELPERS = ['pip', 'nori', 'moss']
HAND_SHAPES = ['relaxed', 'grip', 'point', 'open']


def body_spec(kind):
    s = copy.deepcopy(G['cast']()[kind])
    s['skin'] = 'SKIN'
    s['hair'] = {'style': 'none'}; s['hat'] = None; s['glasses'] = None
    s['face'] = {'eyes': 'round'}
    if kind not in HELPERS: s['top']['color'] = 'TOP'
    s['hands'] = {}
    s['extras'] = [e for e in s.get('extras', []) if e[0] != 'held']
    if kind == 'casual':
        s['bottom'] = dict(kind='trousers', color='BOTTOM')
        s['socks'] = 'SOCKS'
        s['shoes'] = dict(kind='sneaker', color='SHOE', sole='SOLE', accent='SHOEACC')
        s['extras'] = [('tote', dict(color='BAG', print_='PRINT', side='R'))]
    return s


def build_body(kind):
    spec = body_spec(kind)
    rig = [None]

    def make(): rig[0] = person(b, spec, kind)
    made = phase(make)
    r = rig[0]
    joint_of = {e: k for k, e in r.j.items()}
    groups = {}

    def owner(o):
        p = o.parent
        while p is not None and p not in joint_of: p = p.parent
        return joint_of.get(p)
    hand_names = {'Palm', 'Finger', 'Fingertip', 'Knuckle', 'Thumb', 'Thumb tip'}
    for o in made:
        k = owner(o)
        if k is None or k == 'head' or o.type == 'EMPTY': continue
        if k.startswith('wrist.') and stem(o.name) in hand_names: tag = 'hand-relaxed'
        elif kind == 'casual' and k.split('.')[0] in ('hip', 'knee', 'ankle'): tag = 'trousers'
        else: tag = 'base'
        groups.setdefault((k, tag), []).append(o)
    for shape in HAND_SHAPES[1:]:
        for side in ('L', 'R'):
            for o in phase(lambda: hands(b, r, side, shape, spec)):
                groups.setdefault(('wrist.' + side, 'hand-' + shape), []).append(o)
    if kind == 'casual':
        shorts = dict(spec); shorts['bottom'] = dict(kind='shorts', color='BOTTOM')
        for side in ('L', 'R'):
            for o in phase(lambda: legs(b, r, side, shorts)):
                k = owner(o)
                if k: groups.setdefault((k, 'shorts'), []).append(o)
    meshes = [bake(objs, '%s__%s' % (k, tag)) for (k, tag), objs in sorted(groups.items())]
    meshes = [m for m in meshes if m]
    rest = {}
    parent_of = {k: (joint_of.get(e.parent) if e.parent else None) for k, e in r.j.items()}
    for k in r.j: rest[k] = {'parent': parent_of[k], 'rest': unity(r.w[k])}
    d = r.d
    info = {'height': d['H'], 'hh': d['hh'], 'build': spec.get('build', 1.0), 'shoulderWidth': d['sh_x'], 'joints': rest,
            'headCentre': unity(Vector((0, 0, d['H'] - 0.5 * d['hh']))), 'gripDrop': 0.15 * d['hh']}
    rec = export('PK_Body_' + kind, meshes)
    clear()
    return info, rec


# ---------------------------------------------------------------------------------------------------- the head kit
HEAD_BASE = {'Head': 'skull', 'Ear': 'skull', 'Blush': 'skull', 'Eye white': 'eyes', 'Iris': 'eyes', 'Pupil': 'eyes', 'Glint': 'eyes'}
HAIRS = ['crop', 'bob', 'long', 'bun', 'curly', 'pompadour', 'puffs', 'pigtails', 'spiky', 'ponytail', 'bald']
FRINGES = ['crop', 'bob', 'long']
HATS = ['cap', 'bucket', 'beret', 'beanie', 'tophat', 'bandana', 'goggles']


def head_variant(face=None, hair=None, hat=None, glasses=None, keep_base=False):
    """Builds a head one unit tall on its own joint at the origin, and returns its parts (the base's left out unless asked)."""
    r = Rig('headkit')
    r.joint('head', None, (0, 0, 0))
    r.d = {'H': 1.016, 'hh': 1.0}
    spec = {'face': dict({'nose': 'none', 'mouth': 'none', 'brows': 0, 'blush': True}, **(face or {})),
            'hair': hair or {'style': 'none', 'color': 'HAIR'}, 'hat': hat, 'glasses': glasses, 'skin': 'SKIN'}
    spec['face'].setdefault('iris', 'IRIS'); spec['face'].setdefault('brow_color', 'HAIR')
    r.spec = spec; r.skin = slot('skin', 'SKIN')
    made = phase(lambda: head(b, r, spec))
    made = [o for o in made if o.type != 'EMPTY']
    bpy.data.objects.remove(r.j['head'], do_unlink=True)
    out = {}
    for o in made:
        if o.type == 'EMPTY': continue
        base = HEAD_BASE.get(stem(o.name))
        if base and not keep_base:
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        out.setdefault(base or 'variant', []).append(o)
    return out


def build_head():
    meshes = []

    def add(name, objs):
        m = bake(objs, name)
        if m: meshes.append(m)
    base = head_variant(keep_base=True)
    neck = b.cyl('Neck', (0, 0, -0.008), 0.13, 0.2, slot('skin', 'SKIN'), r=0.01)
    add('head__skull', base['skull'] + [neck])
    add('head__eyes', base['eyes'])
    add('head__lids', head_variant(face={'eyes': 'lidded'}).get('variant', []))
    add('head__lashes', head_variant(face={'lashes': True}).get('variant', []))
    add('head__brows', head_variant(face={'brows': 1.0}).get('variant', []))
    add('head__brows-bushy', head_variant(face={'brows': 1.4}).get('variant', []))
    for n in ('button', 'bulb', 'long'):
        add('head__nose-' + n, head_variant(face={'nose': n}).get('variant', []))
    for m in ('smile', 'small', 'smirk', 'grin', 'o'):
        add('head__mouth-' + m, head_variant(face={'mouth': m}).get('variant', []))
    add('head__mustache', head_variant(face={'mustache': 'HAIR'}).get('variant', []))
    add('head__freckles', head_variant(face={'freckles': True}).get('variant', []))
    add('head__mole', head_variant(face={'mole': True}).get('variant', []))
    for h in HAIRS:
        add('head__hair-' + h, head_variant(hair={'style': h, 'color': 'HAIR', 'fringe': False, 'band': 'BAND'}).get('variant', []))
    for h in FRINGES:
        with_f = head_variant(hair={'style': h, 'color': 'HAIR', 'fringe': True, 'band': 'BAND'}).get('variant', [])
        fringe = [o for o in with_f if stem(o.name) == 'Fringe']
        for o in with_f:
            if o not in fringe: bpy.data.objects.remove(o, do_unlink=True)
        add('head__fringe-' + h, fringe)
    for h in HATS:
        hat = dict(kind=h, color='HAT', band='HATBAND')
        if h == 'cap': hat['logo'] = True
        if h == 'bucket': hat['flower'] = '#FF9EC4'
        if h == 'bandana': hat['dots'] = '#FFF3DF'
        if h == 'goggles': hat['lens'] = '#7FE3D6'; hat['strap'] = '#2B3A55'
        add('head__hat-' + h, head_variant(hat=hat).get('variant', []))
    for g in ('round', 'square'):
        add('head__glasses-' + g, head_variant(glasses={'kind': g, 'color': 'GLASSES'}).get('variant', []))
    add('head__sunglasses', [o for o in head_variant(face={'eyes': 'sun'}).get('variant', [])])
    # The base parts each variant made again, and the empties, go.
    keep = set(meshes)
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    rec = export('PK_Head', meshes)
    clear()
    return {'hh': 1.0, 'jointToCentre': 0.516}, rec


# ---------------------------------------------------------------------------------------------------- what visitors hold
HELD = ['creature_bag', 'coin', 'coupons', 'netbag', 'notebook', 'bags', 'program', 'jar', 'pipette', 'clipboard']
# The jar is carried in both arms on the right wrist (the builder hangs it there whichever side); the pipette's fill is a
# role (DOSE) the game colours with the culture Moss carries.
ONE_SIDE = {'jar': 'R'}
# GR-18 wave 2: the arm pose the game holds each item in (GrowthPeopleMotion's, written for the side named; the other side
# mirrored). The board makes an item where the posed hand is and hands it to the wrist; the kit then puts the arm back to
# rest, the item following the wrist, so the game, which hangs items on a wrist at rest, finds it upright in that pose
# (made at rest, a notebook held up or the jar in both arms tipped over with the wrist).
HOLD = {
    'creature_bag': {'shoulder.R': (-50, 10, 0), 'elbow.R': (-96, 0, 30)},  # hugged to the chest, leaving
    'coin': {'shoulder.R': (-68, 6, 0), 'elbow.R': (-24, 0, 0)},  # held out at the counter
    'notebook': {'shoulder.L': (-30, -6, 0), 'elbow.L': (-92, 0, -20)},
    'program': {'shoulder.L': (-28, -6, 0), 'elbow.L': (-80, 0, -10)},
    'jar': {'shoulder.L': (-48, -2, 0), 'shoulder.R': (-48, 2, 0), 'elbow.L': (-56, 0, -14), 'elbow.R': (-56, 0, 14)},  # Nori's
    'pipette': {'shoulder.R': (-36, 8, 0), 'elbow.R': (-86, 0, 18)},
    'clipboard': {'shoulder.L': (-22, -6, 0), 'elbow.L': (-82, 0, -24)},
}


def hold_pose(item, side):
    """The arm pose for an item in a hand: as written, or mirrored onto the other side."""
    p = HOLD.get(item, {})
    if any(k.endswith('.' + side) for k in p) or not p: return dict(p)
    other = 'L' if side == 'R' else 'R'
    return {k.replace('.' + other, '.' + side): (x, -y, -z) for k, (x, y, z) in p.items()}


def build_held():
    spec = body_spec('casual')
    spec['height'], spec['heads'] = 1.6, 3.0
    r = [None]
    phase(lambda: r.__setitem__(0, person(b, spec, 'held')))
    rig = r[0]
    rig.place((0, 0), 0)
    rig.pose({})
    meshes = []
    wrists = {}
    bpy.context.view_layer.update()
    for side in ('L', 'R'): wrists[side] = unity(rig.j['wrist.' + side].matrix_world.translation)
    for side in ('L', 'R'):
        for item in HELD:
            if ONE_SIDE.get(item, side) != side: continue
            bloom = G['BLOOM']
            if item == 'pipette': G['BLOOM'] = 'DOSE'
            rig.pose(hold_pose(item, side))
            made = phase(lambda: held(b, rig, item=item, side=side))
            rig.pose({})
            bpy.context.view_layer.update()
            G['BLOOM'] = bloom
            m = bake([o for o in made if o.type != 'EMPTY'], '%s__%s' % (item, side))
            if m: meshes.append(m)
    keep = set(meshes)
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    rec = export('PK_Held', meshes)
    clear()
    return {'hh': 1.6 / 3.0, 'wrists': wrists}, rec


# ---------------------------------------------------------------------------------------------------- the player's hands
# GR-18 wave 3: the player's arms in first person, the board's (fp_arm, s_hands_carry, s_hands_siphon): the shop's yellow
# gloves and turquoise sleeves rolled up in cream. Each arm is built with its wrist at the origin, its fingers hanging down
# (Blender -Z) and its forearm up (+Z), its palm toward the body as a person's hangs; each hand shape is a part of its own.
# What the hands hold: the shop's water bag (its knot at the origin, its twist tie a role, TIE), the siphon (its bulb at the
# origin, its tube along Blender -Y, Unity +Z), a band of the siphon's fill one metre long (the game scales one per dose,
# in the dose's colour, DOSE), the vacuum's canister (siphon level 4), and a length of pipe across (along X).
FP_SHAPES = ['relaxed', 'grip', 'open', 'point', 'cup']
FP_FOREARM = 0.42
SIPHON_TUBE = (0.04, 0.3)  # where the siphon's tube starts and ends along its axis
SIPHON_BAND = 0.016
PIPE_LENGTH = 0.34


def build_hands():
    C = G['C']
    glove, sleeve, cuff = slot('gloss', C['yellow']), slot('cloth', C['teal']), slot('cloth', C['cream'])
    spec = dict(gloves=C['yellow'], hand=0.62)
    meshes = []

    def add(name, objs):
        m = bake([o for o in objs if o.type != 'EMPTY'], name)
        if m: meshes.append(m)

    def along_y(o):
        o.rotation_euler = (math.radians(90), 0, 0)
        return o

    for side in ('L', 'R'):
        add('arm__' + side, [b.torus('Glove cuff', (0, 0, 0.03), 0.038, 0.016, glove),
                             G['curve'](b, 'Forearm', [Vector((0, 0, 0.02)), Vector((0, 0, FP_FOREARM))], [0.036, 0.048], sleeve, poly=True),
                             b.torus('Rolled sleeve', (0, 0, 0.14), 0.045, 0.02, cuff)])
        for shape in FP_SHAPES:
            add('hand-%s__%s' % (shape, side), hands(b, None, side, shape, spec, at=Vector((0, 0, 0)), m=glove))
    add('bag__fp', G['bag_'](b, Vector((0, 0, 0)), 0.5, color='TIE'))
    add('siphon_bulb__fp', [b.ball('Siphon bulb', (0, 0, 0), (0.042, 0.042, 0.042), slot('gloss', C['coral']), seg=24)])
    t0, t1 = SIPHON_TUBE
    add('siphon_tube__fp', [along_y(b.cyl('Siphon tube', (0, -(t0 + t1) / 2, 0), 0.022, t1 - t0, slot('glass', '#F2FCFF'), r=0)),
                            b.ball('Siphon nozzle', (0, -t1, 0), 0.014, slot('gloss', C['teal_dark']), seg=12)])
    add('siphon_band__fp', [along_y(b.cyl('Siphon fill', (0, -0.5, 0), SIPHON_BAND, 1.0, slot('liquid', 'DOSE'), r=0))])
    add('siphon_canister__fp', [along_y(b.cyl('Vacuum canister', (0, -0.13, -0.05), 0.03, 0.1, slot('gloss', C['teal_dark']), r=0.008)),
                                along_y(b.cyl('Canister band', (0, -0.13, -0.05), 0.032, 0.02, slot('cloth', C['cream']), r=0.004))])
    pipe = [b.cyl('Pipe', (0, 0, 0), 0.045, PIPE_LENGTH, slot('glass', '#EBFAFF'), r=0, rot=(0, 90, 0))]
    for x in (-PIPE_LENGTH / 2, PIPE_LENGTH / 2):
        pipe.append(b.ball('Pipe joint', (x, 0, 0), 0.052, slot('gloss', C['teal']), seg=20))
    add('pipe__fp', pipe)
    keep = set(meshes)
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    rec = export('PK_Hands', meshes)
    clear()
    info = {'forearm': FP_FOREARM, 'bagCreature': unity(Vector((0, 0, -0.1))), 'tube': list(SIPHON_TUBE), 'band': SIPHON_BAND, 'pipe': PIPE_LENGTH}
    return info, rec


# ---------------------------------------------------------------------------------------------------- the comedy (wave 4)
# GR-18 wave 4 (§6.7): the faces and props of the silly moments, apart from the head kit so its parts stay as they were.
# The face's parts sit on a head one unit tall on its joint, as the head kit's do (the board's surface, `surf`): a wobbly
# grimace, eyes squeezed shut (><) and a slap's hand mark on the left cheek. The props are in metres: a ring of dizzy stars
# (centred on its axis), a puff of a cloud, a burp's bubble and a music note (each centred at its origin).
STARS_RING, STAR_SIZE = 0.15, 0.03


def face_surface():
    """The board's head surface (one unit tall, its joint at the origin) to lay the face's new parts on; the head it was
    built on is removed."""
    r = Rig('gagface')
    r.joint('head', None, (0, 0, 0))
    r.d = {'H': 1.016, 'hh': 1.0}
    spec = {'face': {'nose': 'none', 'mouth': 'none', 'brows': 0, 'blush': False, 'iris': 'IRIS', 'brow_color': 'HAIR'},
            'hair': {'style': 'none', 'color': 'HAIR'}, 'hat': None, 'glasses': None, 'skin': 'SKIN'}
    r.spec = spec; r.skin = slot('skin', 'SKIN')
    for o in phase(lambda: head(b, r, spec)): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.objects.remove(r.j['head'], do_unlink=True)
    return r.surf


def star(name, centre, outward, size, m):
    """A five-pointed star standing on a ring, its face outward."""
    out = Vector(outward).normalized(); up = Vector((0, 0, 1)); side = up.cross(out).normalized()
    pts = []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        rad = size if i % 2 == 0 else size * 0.45
        pts.append(Vector(centre) + side * (rad * math.cos(a)) + up * (rad * math.sin(a)))
    return G['tri'](b, name, pts, size * 0.4, m)


def build_gags():
    C = G['C']
    surf = face_surface()
    ink, lip = slot('eye', G['INK']), slot('eye', G['LIP'])
    meshes = []

    def add(name, objs):
        m = bake([o for o in objs if o.type != 'EMPTY'], name)
        if m: meshes.append(m)

    # The wobbly grimace: a zigzag where the mouth is.
    pts = [surf(t * 0.19, -1, -0.44 + 0.026 * (1 if i % 2 else -1), 0.006)[0] for i, t in enumerate((-1, -0.6, -0.2, 0.2, 0.6, 1))]
    add('head__mouth-wobble', [G['curve'](b, 'Grimace', pts, [0.011, 0.015, 0.016, 0.016, 0.015, 0.011], lip, poly=True)])
    # Eyes squeezed shut: > on the right eye and < on the left, their points towards the nose.
    eyes = []
    for s in (-1, 1):
        chevron = [surf(s * (0.36 + 0.08), -1, 0.06 + 0.055, 0.01)[0], surf(s * (0.36 - 0.05), -1, 0.06, 0.012)[0],
                   surf(s * (0.36 + 0.08), -1, 0.06 - 0.055, 0.01)[0]]
        eyes.append(G['curve'](b, 'Squeezed eye', chevron, [0.014, 0.018, 0.014], ink, poly=True))
    add('head__eyes-squeeze', eyes)
    # A slap's hand mark on the left cheek: a palm and four fingers, rosy pink.
    mark = slot('skin', '#FF6F82')
    p, n = surf(0.56, -1, -0.24, 0.004)
    parts = [G['orient'](b.ball('Mark palm', p, (0.085, 0.01, 0.07), mark, seg=20, shadow=False), n)]
    for k, dx in enumerate((-0.085, -0.03, 0.025, 0.075)):
        q, nq = surf(0.56 + dx, -1, -0.24 + 0.13 - 0.02 * abs(k - 1.5), 0.004)
        parts.append(G['orient'](b.ball('Mark finger', q, (0.022, 0.009, 0.06), mark, seg=14, shadow=False), nq, roll=(k - 1.5) * 10))
    add('head__mark-slap', parts)
    # Dizzy stars: five on a ring about its axis.
    yellow = slot('gloss', C['yellow'])
    add('gag__stars', [star('Dizzy star', (STARS_RING * math.cos(a), STARS_RING * math.sin(a), 0), (math.cos(a), math.sin(a), 0), STAR_SIZE, yellow)
                       for a in (2 * math.pi * i / 5 for i in range(5))])
    # A puff of a cloud: soft pale green-grey blobs.
    puff = slot('plastic', '#CFE6B4')
    add('gag__cloud', [b.ball('Puff', c, r, puff, seg=20, shadow=False) for c, r in
                       (((0, 0, 0), 0.07), ((0.07, 0.01, 0.02), 0.055), ((-0.065, -0.01, 0.015), 0.05), ((0.02, 0.02, 0.06), 0.05),
                        ((-0.03, 0.03, -0.045), 0.045), ((0.05, -0.03, -0.04), 0.04))])
    # A burp's bubble: a glossy ball with a glint.
    add('gag__bubble', [b.ball('Bubble', (0, 0, 0), 0.09, slot('glass', '#DFF7FF'), seg=28, shadow=False),
                        b.ball('Bubble glint', (-0.035, -0.06, 0.04), 0.016, slot('gloss', '#FFFFFF'), seg=12, shadow=False)])
    # A music note: a head, a stem and a flag, in the shop's coral.
    coral = slot('gloss', C['coral'])
    head_ball = b.ball('Note head', (0, 0, -0.045), (0.03, 0.02, 0.022), coral, seg=20, shadow=False)
    head_ball.rotation_euler = (0, math.radians(-20), 0)
    add('gag__note', [head_ball, b.cyl('Note stem', (0.026, 0, 0.0), 0.006, 0.1, coral, r=0),
                      G['curve'](b, 'Note flag', [Vector((0.026, 0, 0.05)), Vector((0.05, 0, 0.025)), Vector((0.052, 0, -0.005))], [0.008, 0.007, 0.005], coral)])
    keep = set(meshes)
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    rec = export('PK_Gags', meshes)
    clear()
    return {'starsRing': STARS_RING}, rec


# ---------------------------------------------------------------------------------------------------- the wishes (GR-20)
# GR-20: a visitor's wish bubble and what shows in it, an asset of its own so the earlier parts stay as they were. Built facing
# -Y like the people (Unity +Z, the side the game turns to the camera); the trail runs to Blender -x (Unity +X, the viewer's
# left) and down, towards the head the bubble floats beside.
WISH_FRONT = 0.062           # how far in front of the cloud's centre its icons stand (Unity +Z)
WISH_ICON = 0.075            # the radius an icon fits in
WISH_TRAIL = (-0.17, 0.0, -0.185)   # the trail's last puff (Blender)
WISH_COLOURS = {'plain': ('#22BFB0', '#15867F'), 'sunny': ('#FFC23C', '#F08A24'), 'deep': ('#3D6BD9', '#233E96'),
                'shimmer': ('#C9A8FF', '#9A78F2')}


def outline(name, pts2, depth, m, y=0.0):
    """A flat, puffy shape from an outline in the x-z plane (a heart, a drop, a star), thickened along y."""
    return G['tri'](b, name, [Vector((x, y, z)) for x, z in pts2], depth, m)


def build_wish():
    meshes = []

    def add(name, objs):
        m = bake([o for o in objs if o.type != 'EMPTY'], name)
        if m: meshes.append(m)

    cream, rim = slot('plastic', '#FFFBF2'), slot('plastic', G['INK'])
    # The cloud: a soft middle and eight puffs on an ellipse, flattened front to back; behind it the same, a little larger,
    # in ink, which shows as the bubble's rim; and the trail of two puffs to the head.
    puffs = [((0, 0, 0), (0.13, 0.047, 0.096))]
    for i in range(8):
        a = 2 * math.pi * i / 8 + math.pi / 8
        r = 0.056 if i % 2 else 0.05
        puffs.append(((0.122 * math.cos(a), 0, 0.088 * math.sin(a)), (r, 0.03, r)))
    trail = [((-0.128, 0, -0.132), 0.027), (WISH_TRAIL, 0.017)]
    parts = [b.ball('Wish cloud', c, s, cream, seg=40, shadow=False) for c, s in puffs]
    parts += [b.ball('Wish rim', (c[0], c[1] + 0.012, c[2]), (s[0] + 0.009, s[1], s[2] + 0.009), rim, seg=32, shadow=False) for c, s in puffs]
    for c, r in trail:
        parts.append(b.ball('Wish trail', c, (r, 0.02, r), cream, seg=16, shadow=False))
        parts.append(b.ball('Wish trail rim', (c[0], c[1] + 0.008, c[2]), (r + 0.006, 0.018, r + 0.006), rim, seg=16, shadow=False))
    add('wish__bubble', parts)

    y = -WISH_FRONT
    ink, white = slot('eye', G['INK']), slot('eye', '#FFFFFF')
    # A fish token, side on, facing the viewer's left: a round body, a V tail, a top fin and a big eye; in plain teal and
    # in each colour a wish can ask for.
    for key, (body, fin) in WISH_COLOURS.items():
        mb, mf = slot('gloss', body), slot('gloss', fin)
        token = [b.ball('Token body', (0, y, 0), (0.05, 0.022, 0.036), mb, seg=24, shadow=False),
                 outline('Token tail', [(0.038, 0), (0.075, 0.032), (0.066, 0), (0.075, -0.032)], 0.014, mf, y),
                 outline('Token fin', [(-0.012, 0.03), (0.016, 0.052), (0.026, 0.026)], 0.01, mf, y),
                 b.ball('Token eye', (-0.024, y - 0.018, 0.008), (0.011, 0.006, 0.011), white, seg=14, shadow=False),
                 b.ball('Token pupil', (-0.026, y - 0.023, 0.008), (0.0055, 0.003, 0.0055), ink, seg=10, shadow=False)]
        if key == 'shimmer':
            token += [b.ball('Token glint', (x, y - 0.019, z), (0.0045, 0.003, 0.0045), white, seg=8, shadow=False) for x, z in ((0.004, 0.018), (0.02, -0.01), (-0.01, -0.016))]
        add('wish__fish-' + key, token)
    # The colours' signs: a sun (Sunny), a drop (Deep), a four-pointed sparkle (Shimmer).
    sun = slot('gloss', '#FFC93C')
    add('wish__sun', [b.ball('Sun', (0, y, 0), (0.017, 0.009, 0.017), sun, seg=18, shadow=False)] +
        [b.ball('Sun ray', (0.028 * math.cos(a), y, 0.028 * math.sin(a)), (0.006, 0.006, 0.006), sun, seg=10, shadow=False)
         for a in (2 * math.pi * i / 8 for i in range(8))])
    # The drop: its point up, its round end down (an arc from upper right, round the bottom, to upper left).
    drop = [(0, 0.03)] + [(0.017 * math.cos(math.radians(60 - 300 * i / 15)), 0.017 * math.sin(math.radians(60 - 300 * i / 15)) - 0.006) for i in range(16)]
    add('wish__drop', [outline('Drop', drop, 0.014, slot('gloss', '#3D6BD9'), y)])
    star4 = [((0.03 if i % 2 == 0 else 0.009) * math.cos(math.pi / 2 + i * math.pi / 4), (0.03 if i % 2 == 0 else 0.009) * math.sin(math.pi / 2 + i * math.pi / 4)) for i in range(8)]
    add('wish__sparkle', [outline('Sparkle', star4, 0.01, slot('gloss', '#C9A8FF'), y), b.ball('Sparkle eye', (0, y - 0.007, 0), (0.006, 0.004, 0.006), white, seg=10, shadow=False)])
    # A size pip (the game sets one, two or four under the token, as a creature takes spaces in a tank).
    add('wish__pip', [b.ball('Pip', (0, y, 0), (0.0095, 0.007, 0.0095), slot('gloss', G['C']['coral']), seg=12, shadow=False)])
    # The heart a granted wish pops into.
    heart = [(0.0042 * 16 * math.sin(t) ** 3, 0.0042 * (13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)))
             for t in (2 * math.pi * i / 28 for i in range(28))]
    add('wish__heart', [outline('Heart', heart, 0.026, slot('gloss', '#FF6F91'), 0.0)])
    # The outlines (a heart, a drop, a sparkle) are not convex: split every face into triangles here, by ear clipping, so
    # the game draws the shapes made rather than its own guess at them.
    import bmesh
    for m in meshes:
        bm = bmesh.new(); bm.from_mesh(m.data)
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='EAR_CLIP')
        bm.to_mesh(m.data); bm.free()
    keep = set(meshes)
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    rec = export('PK_Wish', meshes)
    clear()
    return {'front': WISH_FRONT, 'iconRadius': WISH_ICON, 'trail': unity(WISH_TRAIL)}, rec


# ---------------------------------------------------------------------------------------------------- build, export, record
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
old = {}
if (SOURCE / 'manifest.json').is_file():
    old = json.loads((SOURCE / 'manifest.json').read_text(encoding='utf-8'))
kinds = dict(old.get('kinds', {}))
records = {r['id']: r for r in old.get('assets', [])}
head_info, held_info, hands_info, gags_info, wish_info = old.get('head'), old.get('held'), old.get('hands'), old.get('gags'), old.get('wish')
for kind in KINDS:
    if ONLY and 'Body_' + kind not in ONLY: continue
    info, rec = build_body(kind)
    kinds[kind] = info; records[rec['id']] = rec
if not ONLY or 'Head' in ONLY:
    head_info, rec = build_head(); records[rec['id']] = rec
if not ONLY or 'Held' in ONLY:
    held_info, rec = build_held(); records[rec['id']] = rec
if not ONLY or 'Hands' in ONLY:
    hands_info, rec = build_hands(); records[rec['id']] = rec
if not ONLY or 'Gags' in ONLY:
    gags_info, rec = build_gags(); records[rec['id']] = rec
if not ONLY or 'Wish' in ONLY:
    wish_info, rec = build_wish(); records[rec['id']] = rec
slots = dict(old.get('materials', {})); slots.update(SLOTS)
manifest = {
    'schema': 1, 'title': 'Evolution Shop people kit (GR-18): the new game\'s visitors, guests, helpers and the player\'s hands',
    'origin': {'author': 'Original local asset authorship for Evolution Shop (GR-18)', 'external_sources': [],
               'paid_generation': False, 'external_uploads': False},
    'blender_version': bpy.app.version_string, 'source_script': 'Tools/Art/create_people_kit.py',
    'source_script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'builder_script': 'Tools/Art/gr18_characters.py', 'builder_script_sha256': hashlib.sha256(LIB.read_bytes()).hexdigest(),
    'axis_mapping': 'Blender (x, y, z) -> Unity (-x, z, -y); people face Unity +Z',
    'kinds': kinds, 'head': head_info, 'held': held_info, 'hands': hands_info, 'gags': gags_info, 'wish': wish_info,
    'materials': dict(sorted(slots.items())),
    'assets': [records[k] for k in sorted(records)],
}
(SOURCE / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
# The game's copy: what it needs to assemble people (joints and part names), as a TextAsset.
game = {'kinds': [dict(id=k, height=v['height'], hh=v['hh'], build=v['build'], gripDrop=v['gripDrop'], headCentre=v['headCentre'],
                       joints=[dict(name=j, parent=jv['parent'] or '', rest=jv['rest']) for j, jv in v['joints'].items()],
                       parts=sorted(records['PK_Body_' + k]['parts']))
                  for k, v in sorted(kinds.items())],
        'head': dict(jointToCentre=head_info['jointToCentre'], parts=sorted(records['PK_Head']['parts'])),
        'held': dict(hh=held_info['hh'], wristL=held_info['wrists']['L'], wristR=held_info['wrists']['R'], parts=sorted(records['PK_Held']['parts'])),
        'hands': dict(forearm=hands_info['forearm'], bagCreature=hands_info['bagCreature'], tube=hands_info['tube'], band=hands_info['band'],
                      pipe=hands_info['pipe'], parts=sorted(records['PK_Hands']['parts'])),
        'gags': dict(starsRing=gags_info['starsRing'], parts=sorted(records['PK_Gags']['parts'])),
        'wish': dict(front=wish_info['front'], iconRadius=wish_info['iconRadius'], trail=wish_info['trail'], parts=sorted(records['PK_Wish']['parts'])),
        'materials': [dict(name=n, kind=v['kind'], colour=v['colour']) for n, v in sorted(slots.items())]}
(EXPORT / 'people.json').write_text(json.dumps(game, indent=1) + '\n', encoding='utf-8')
print('PEOPLE_KIT_COMPLETE', len(records))
