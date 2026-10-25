# GR-16 look development (not game art): the same corner of the shop and of the back room, built in three directions for
# the user to choose from or mix, each cartoony and in soft colours (the user's word on 2026-10-25: "I didn't really like
# the old design, remake it, but keep the cartoony and soft colors"). Original, local (Blender 5.0, EEVEE):
#   blender --background --factory-startup --python Tools/Art/gr16_look_directions.py -- --repo C:/dev/DumbVideoGameStudio --out <folder>
# It renders <out>/<direction>-shop.png and <direction>-lab.png at 1280x720 from the player's eye height (1.65 m), with the
# game's own creatures (appended from ArtSource/DioramaKit) in the tank.
import bpy, math, random, sys, argparse
from pathlib import Path
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument('--repo', default='C:/dev/DumbVideoGameStudio')
ap.add_argument('--out', required=True)
ap.add_argument('--only', default='')
args = ap.parse_args(argv)
OUT = Path(args.out); OUT.mkdir(parents=True, exist_ok=True)
KIT = Path(args.repo) / 'ArtSource' / 'DioramaKit' / 'EvolutionShop_DioramaKit.blend'
import json
KIT_COLOURS = {k: v['srgb_hex'] for k, v in json.loads((Path(args.repo) / 'ArtSource' / 'DioramaKit' / 'manifest.json').read_text()).get('materials', {}).items()}


def hexc(h, a=1.0):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    # sRGB to linear, as Blender's colour sockets expect.
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (lin[0], lin[1], lin[2], a)


DIRECTIONS = {
    # A: pastel claymation. Puffy shapes with big round edges, butter cream and mint, a peach checker floor, lilac shade.
    'clay': dict(
        name='Soft Clay', bevel=0.075, wobble=0.0, outline=None, ramp='soft', shade='#B8A2DD',
        wall='#F6E7CB', dado='#BEE3CF', trim='#F4B6A6', floor=('#F7D9C6', '#F2C8B3'), ceiling='#FBF2E1',
        counter='#CDBEEB', counter_top='#A9DCCA', stand='#F3BAA9', lamp='#FFE2A0', shelf='#E6CDEE', frame='#A9DCCA',
        sky='#BFE6F2', text='#7C70B2', pedestal='#F3BAA9', cap='#CDBEEB', incubator='#BEE3CF', board='#F7D9C6',
        sun=('#FFF1DC', 2.6, (52, 0, 28)), world='#E9DFF2', fill=('#DCE8FF', 120)),
    # B: a house in a tide pool. Seafoam walls with a scalloped dado, round portholes and arches, a sand floor of
    # round pebbles, coral-pink trim, bathysphere growers and a diving-helmet incubator; blue shade.
    'tide': dict(
        name='Tide Pool', bevel=0.05, wobble=0.0, outline=None, ramp='medium', shade='#8DB2D8',
        wall='#D4EFE8', dado='#9FD3CC', trim='#F4AAA1', floor=('#F2E4C4', '#E7D2A8'), ceiling='#E7F6F2',
        counter='#F6C6B9', counter_top='#F2E4C4', stand='#B8D0E3', lamp='#F9D5E9', shelf='#F4AAA1', frame='#F4AAA1',
        sky='#A8DDEB', text='#3F7E86', pedestal='#9FD3CC', cap='#F4AAA1', incubator='#B8D0E3', board='#F2E4C4',
        sun=('#F2FBFF', 2.3, (55, 0, -20)), world='#CFE9F0', fill=('#BDEBFF', 160)),
    # C: a storybook. Cream walls with a sage dado, painted floor boards, dusty blue and rose, shapes a little hand-cut and
    # wobbly, a soft ink outline on everything, two-tone shading.
    'paper': dict(
        name='Storybook', bevel=0.018, wobble=1.0, outline=('#4E4C5E', 0.014), ramp='hard', shade='#ABA1C7',
        wall='#F4EEDC', dado='#B9CBA8', trim='#8EA7C8', floor=('#E4CAA9', '#D9BD99'), ceiling='#F7F2E6',
        counter='#E3B5A7', counter_top='#B9CBA8', stand='#A9C1D9', lamp='#F6E1B3', shelf='#CBAB8C', frame='#8EA7C8',
        sky='#CFE5F1', text='#5C5B74', pedestal='#CBAB8C', cap='#E3B5A7', incubator='#A9C1D9', board='#E4CAA9',
        sun=('#FFF6E6', 2.4, (50, 0, 35)), world='#E8E2EE', fill=('#E6E2FF', 100)),
}
CULTURES = {'bloom': '#F08C8C', 'flow': '#7CC3EA'}


class Builder:
    def __init__(self, d, key):
        self.d, self.key = d, key
        self.mats = {}
        self.rng = random.Random(7)

    # ---------------------------------------------------------------- materials: a soft toon from the scene's light
    def mat(self, name, color, rough=0.6, glass=False, alpha=1.0, emit=0.0, rim=True):
        k = (name, color, glass, alpha, emit, rim)
        if k in self.mats: return self.mats[k]
        m = bpy.data.materials.new(self.key + ' ' + name)
        m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        base = hexc(color)
        if glass:
            p = nt.nodes.new('ShaderNodeBsdfPrincipled')
            p.inputs['Base Color'].default_value = base
            p.inputs['Roughness'].default_value = 0.08
            p.inputs['Alpha'].default_value = alpha
            nt.links.new(p.outputs[0], out.inputs[0])
            m.surface_render_method = 'BLENDED'
            m.use_backface_culling = False
        elif emit > 0:
            e = nt.nodes.new('ShaderNodeEmission'); e.inputs[0].default_value = base; e.inputs[1].default_value = emit
            nt.links.new(e.outputs[0], out.inputs[0])
        else:
            diff = nt.nodes.new('ShaderNodeBsdfDiffuse'); diff.inputs[0].default_value = (1, 1, 1, 1)
            s2 = nt.nodes.new('ShaderNodeShaderToRGB'); nt.links.new(diff.outputs[0], s2.inputs[0])
            ramp = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(s2.outputs[0], ramp.inputs[0])
            cr = ramp.color_ramp
            shade = hexc(self.d['shade'])
            cr.elements[0].color = (shade[0], shade[1], shade[2], 1)
            cr.elements[1].color = (1, 1, 1, 1)
            if self.d['ramp'] == 'hard':
                cr.interpolation = 'CONSTANT'; cr.elements[0].position = 0.0; cr.elements[1].position = 0.5
            elif self.d['ramp'] == 'medium':
                cr.interpolation = 'EASE'; cr.elements[0].position = 0.36; cr.elements[1].position = 0.56
            else:
                cr.interpolation = 'EASE'; cr.elements[0].position = 0.28; cr.elements[1].position = 0.72
            # Room surfaces (walls, floors, ceilings: what gets no outline) take only a little of the shading, as the
            # classic room style did; props and creatures take all of it.
            flat = nt.nodes.new('ShaderNodeMix'); flat.data_type = 'RGBA'; flat.blend_type = 'MIX'
            flat.inputs['Factor'].default_value = 1.0 if rim else (0.1 if self.d['ramp'] == 'hard' else 0.28)
            flat.inputs[6].default_value = (1, 1, 1, 1); nt.links.new(ramp.outputs[0], flat.inputs[7])
            mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
            mul.inputs['Factor'].default_value = 1.0
            nt.links.new(flat.outputs[2], mul.inputs[6]); mul.inputs[7].default_value = base
            # A soft rim of light, so rounded shapes read as soft (none on the hard storybook shading).
            lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs[0].default_value = 0.25
            rimw = nt.nodes.new('ShaderNodeMath'); rimw.operation = 'MULTIPLY'
            rimw.inputs[1].default_value = 0.0 if self.d['ramp'] == 'hard' or not rim else 0.12
            nt.links.new(lw.outputs['Facing'], rimw.inputs[0])
            add = nt.nodes.new('ShaderNodeMix'); add.data_type = 'RGBA'; add.blend_type = 'ADD'
            nt.links.new(rimw.outputs[0], add.inputs['Factor'])
            nt.links.new(mul.outputs[2], add.inputs[6]); add.inputs[7].default_value = (1, 1, 1, 1)
            em = nt.nodes.new('ShaderNodeEmission'); nt.links.new(add.outputs[2], em.inputs[0])
            nt.links.new(em.outputs[0], out.inputs[0])
        self.mats[k] = m
        return m

    def outline_mat(self):
        if 'outline' in self.mats: return self.mats['outline']
        m = bpy.data.materials.new(self.key + ' outline'); m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        e = nt.nodes.new('ShaderNodeEmission'); e.inputs[0].default_value = hexc(self.d['outline'][0])
        o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], o.inputs[0])
        m.use_backface_culling = True
        self.mats['outline'] = m
        return m

    # ---------------------------------------------------------------- shapes
    def finish(self, obj, mat, bevel=None, outline=True):
        obj.data.materials.append(mat)
        b = self.d['bevel'] if bevel is None else bevel
        if b > 0:
            mod = obj.modifiers.new('Bevel', 'BEVEL'); mod.width = b; mod.segments = 5 if b > 0.03 else 2
            mod.limit_method = 'ANGLE'; mod.harden_normals = False
        if self.d['wobble'] > 0 and outline and obj.dimensions.length < 4:
            obj.rotation_euler.z += math.radians(self.rng.uniform(-2.2, 2.2) * self.d['wobble'])
            obj.rotation_euler.x += math.radians(self.rng.uniform(-1.0, 1.0) * self.d['wobble'])
        if outline and self.d['outline']:
            obj.data.materials.append(self.outline_mat())
            s = obj.modifiers.new('Outline', 'SOLIDIFY'); s.thickness = self.d['outline'][1]
            s.use_flip_normals = True; s.material_offset = len(obj.data.materials) - 1; s.offset = 1
        for p in obj.data.polygons: p.use_smooth = True
        return obj

    def box(self, name, center, size, color, bevel=None, outline=True, rough=0.6, rot=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=center)
        o = bpy.context.object; o.name = name; o.scale = size; o.rotation_euler.z = math.radians(rot)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        o = self.finish(o, self.mat(name, color, rough, rim=outline), bevel, outline)
        if not outline and max(size) > 3: o.visible_shadow = False  # walls and ceilings let the sun in
        return o

    def cyl(self, name, center, radius, depth, color, bevel=None, outline=True, verts=40, rot=(0, 0, 0), glass=False, alpha=1.0):
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=center, rotation=[math.radians(a) for a in rot])
        o = bpy.context.object; o.name = name
        m = self.mat(name, color, glass=glass, alpha=alpha, rim=outline)
        return self.finish(o, m, (min(radius, depth) * 0.3 if bevel is None else bevel) if not glass else 0, outline and not glass)

    def ball(self, name, center, scale, color, glass=False, alpha=1.0, outline=True, emit=0.0):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=20, radius=1, location=center)
        o = bpy.context.object; o.name = name; o.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        return self.finish(o, self.mat(name, color, glass=glass, alpha=alpha, emit=emit, rim=outline), 0, outline and not glass)

    def torus(self, name, center, major, minor, color, rot=(90, 0, 0)):
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=center, rotation=[math.radians(a) for a in rot],
                                         major_segments=48, minor_segments=14)
        o = bpy.context.object; o.name = name
        return self.finish(o, self.mat(name, color), 0)

    def text(self, body, center, size, color, rot=(90, 0, 0)):
        bpy.ops.object.text_add(location=center, rotation=[math.radians(a) for a in rot])
        o = bpy.context.object; o.data.body = body; o.data.size = size; o.data.extrude = 0.012
        o.data.align_x = 'CENTER'; o.data.align_y = 'CENTER'; o.data.bevel_depth = 0.004
        o.data.materials.append(self.mat('Text', color))
        return o

    # ---------------------------------------------------------------- the creatures, from the game's own kit
    def creature(self, species, at, length, yaw):
        names = []
        with bpy.data.libraries.load(str(KIT)) as (src, dst):
            names = [n for n in src.objects if n == 'Diorama' + species or n.startswith('Diorama' + species + ' / ')]
            dst.objects = names
        objs = [o for o in dst.objects if o]
        root = next(o for o in objs if o.name.startswith('Diorama' + species) and ' / ' not in o.name)
        for o in objs: bpy.context.scene.collection.objects.link(o)
        bpy.context.view_layer.update()
        size = max((max(c.dimensions) for c in objs if c.type == 'MESH'), default=1)
        root.scale = (length / size,) * 3
        root.location = at; root.rotation_euler = (0, 0, math.radians(yaw))
        for o in objs:
            if o.type != 'MESH': continue
            for i, slot in enumerate(o.material_slots):
                if slot.material is None: continue
                base = slot.material.name.split('.')[0]
                code = KIT_COLOURS.get(base)
                if code: o.material_slots[i].material = self.mat('Creature ' + base, '#' + code.lstrip('#'))
        return root


# ---------------------------------------------------------------- the two rooms
def shop(b):
    d = b.d
    ox = 0
    # Floor tiles, walls with their dado and trim, the ceiling.
    tile = 0.6
    if b.key == 'paper':
        for i in range(-11, 12):
            b.box('Board', (ox + i * 0.3, 2.5, -0.05), (0.29, 6, 0.1), d['floor'][i % 2], bevel=0.006, outline=False)
    else:
        for i in range(-5, 6):
            for j in range(0, 9):
                c = d['floor'][(i + j) % 2]
                if b.key == 'tide' and (i + j) % 3: c = d['floor'][0]
                b.box('Tile', (ox + i * tile, j * tile + 0.3, -0.05), (tile, tile, 0.1), c, bevel=0.01 if b.key != 'clay' else 0.02, outline=False)
    if b.key == 'tide':
        for i in range(12):
            b.cyl('Pebble', (ox - 2.6 + (i * 0.47) % 5.2, 0.6 + (i * 0.83) % 4.6, 0.005), 0.1 + 0.03 * (i % 3), 0.02, d['floor'][1], bevel=0.008, outline=False)
    b.box('Back wall', (ox, 5.1, 1.6), (7, 0.2, 3.2), d['wall'], bevel=0, outline=False)
    b.box('Left wall', (ox - 3.1, 2.5, 1.6), (0.2, 6, 3.2), d['wall'], bevel=0, outline=False)
    b.box('Ceiling', (ox, 2.5, 3.25), (7, 6, 0.1), d['ceiling'], bevel=0, outline=False)
    b.box('Right wall', (ox + 3.1, 2.5, 1.6), (0.2, 6, 3.2), d['wall'], bevel=0, outline=False)
    b.box('Dado right', (ox + 2.98, 2.5, 0.5), (0.06, 6, 1.0), d['dado'], bevel=0.01, outline=False)
    # The dado: clay a soft band with a puffy rail, tide a band topped by scallops, paper a painted band with a thin rail.
    for wall, pos, size in (('back', (ox, 4.98, 0.5), (7, 0.06, 1.0)), ('left', (ox - 2.98, 2.5, 0.5), (0.06, 6, 1.0))):
        b.box('Dado ' + wall, pos, size, d['dado'], bevel=0.01, outline=False)
    if b.key == 'tide':
        for k in range(14):
            b.cyl('Scallop', (ox - 3.2 + k * 0.5, 4.96, 1.0), 0.25, 0.06, d['dado'], rot=(90, 0, 0), bevel=0.01, outline=False)
        for k in range(12):
            b.cyl('Scallop', (ox - 2.96, -0.3 + k * 0.5, 1.0), 0.25, 0.06, d['dado'], rot=(0, 90, 0), bevel=0.01, outline=False)
    rail = 0.07 if b.key == 'clay' else 0.04
    b.box('Rail back', (ox, 4.95, 1.02), (7, rail, rail), d['trim'], bevel=rail * 0.45, outline=b.key == 'paper')
    b.box('Rail left', (ox - 2.95, 2.5, 1.02), (rail, 6, rail), d['trim'], bevel=rail * 0.45, outline=b.key == 'paper')
    # The window in the left wall: clay a rounded frame with a flower box, tide a round porthole, paper a cross-paned window with curtains.
    if b.key == 'tide':
        b.torus('Porthole', (ox - 2.98, 1.8, 1.75), 0.55, 0.09, d['frame'], rot=(0, 90, 0))
        b.cyl('Porthole sea', (ox - 2.985, 1.8, 1.75), 0.55, 0.01, d['sky'], rot=(0, 90, 0), bevel=0, outline=False)
        for k, (cy, cz) in enumerate(((1.6, 1.55), (1.95, 1.9), (2.05, 1.5))):
            b.ball('Bubble', (ox - 2.975, cy, cz), (0.01, 0.05 + 0.02 * k, 0.05 + 0.02 * k), '#EAFBFF', outline=False)
        for k in range(8):
            a = k * math.pi / 4
            b.ball('Rivet', (ox - 2.92, 1.8 + 0.66 * math.cos(a), 1.75 + 0.66 * math.sin(a)), (0.03, 0.03, 0.03), d['frame'])
    else:
        b.box('Window sky', (ox - 2.985, 1.8, 1.75), (0.01, 1.6, 1.2), d['sky'], bevel=0, outline=False)
        for k, (cy, cz, s) in enumerate(((1.4, 2.05, 0.16), (1.55, 2.08, 0.12), (2.2, 1.95, 0.14))):
            b.ball('Cloud', (ox - 2.975, cy, cz), (0.01, s * 1.6, s * 0.7), '#FFFFFF', outline=False)
        b.box('Hill', (ox - 2.978, 1.8, 1.3), (0.01, 1.6, 0.3), '#BFE3B4' if b.key != 'paper' else '#B9CBA8', bevel=0, outline=False)
        fb = 0.06 if b.key == 'clay' else 0.02
        b.box('Window top', (ox - 2.96, 1.8, 2.4), (0.14, 1.8, 0.14), d['frame'], bevel=fb)
        b.box('Window bottom', (ox - 2.92, 1.8, 1.1), (0.22, 1.9, 0.12), d['frame'], bevel=fb)
        b.box('Window side', (ox - 2.96, 0.95, 1.75), (0.14, 0.12, 1.3), d['frame'], bevel=fb)
        b.box('Window side', (ox - 2.96, 2.65, 1.75), (0.14, 0.12, 1.3), d['frame'], bevel=fb)
        if b.key == 'paper':
            b.box('Mullion', (ox - 2.97, 1.8, 1.75), (0.08, 0.05, 1.2), d['frame'], bevel=0.01)
            b.box('Transom', (ox - 2.97, 1.8, 1.75), (0.08, 1.6, 0.05), d['frame'], bevel=0.01)
            for s in (-1, 1):
                b.box('Curtain', (ox - 2.88, 1.8 + s * 0.78, 1.75), (0.05, 0.32, 1.4), d['counter'], bevel=0.02)
        else:
            b.box('Flower box', (ox - 2.84, 1.8, 0.98), (0.26, 1.4, 0.2), d['trim'], bevel=0.07)
            for k in range(6):
                b.ball('Flower', (ox - 2.84, 1.25 + k * 0.22, 1.12), (0.07, 0.07, 0.06), ['#F7A8C0', '#FFE08A', '#BFE3CF'][k % 3])
    # The back room doorway: clay a rounded opening, tide an arch, paper a square door with a round sign.
    door_x = ox + 1.85
    gap = {'clay': '#A9CDBB', 'tide': '#93C3D2', 'paper': '#BCC9B4'}[b.key]
    b.box('Door gap', (door_x, 4.995, 1.1), (1.1, 0.01, 2.2), gap, bevel=0, outline=False)
    b.box('Back room glimpse', (door_x, 4.99, 0.25), (1.1, 0.01, 0.5), {'clay': '#B7DCC8', 'tide': '#A9CFDA', 'paper': '#AFC3A3'}[b.key], bevel=0, outline=False)
    if b.key == 'tide':
        b.cyl('Arch top', (door_x, 4.995, 2.2), 0.55, 0.01, gap, rot=(90, 0, 0), bevel=0, outline=False)
        b.torus('Arch frame', (door_x, 4.98, 2.2), 0.6, 0.06, d['trim'], rot=(90, 0, 0))
        for s in (-1, 1): b.box('Arch side', (door_x + s * 0.6, 4.98, 1.1), (0.12, 0.12, 2.2), d['trim'], bevel=0.04)
    else:
        for s in (-1, 1): b.box('Door side', (door_x + s * 0.6, 4.97, 1.15), (0.12, 0.12, 2.3), d['trim'])
        b.box('Door head', (door_x, 4.97, 2.32), (1.32, 0.14, 0.14), d['trim'])
        if b.key == 'paper':
            b.cyl('Door sign', (door_x, 4.92, 2.6), 0.18, 0.03, d['board'], rot=(90, 0, 0))
    # The shelf on the back wall with jars, and the shop's sign above it.
    b.box('Shelf', (ox - 1.0, 4.82, 1.7), (2.2, 0.36, 0.08), d['shelf'])
    for k in range(5):
        col = ['#F7A8C0', '#BFE3CF', '#FFE08A', '#CDBEEB', '#A9D8EF'][k]
        b.cyl('Jar', (ox - 1.8 + k * 0.4, 4.82, 1.86), 0.1, 0.24, col)
        b.cyl('Jar lid', (ox - 1.8 + k * 0.4, 4.82, 2.0), 0.105, 0.05, d['trim'])
    b.text('Evolution Shop', (ox - 1.0, 4.96, 2.55), 0.36, d['text'])
    # The Starter tank against the back wall: its stand, glass and water, with the game's own creatures.
    tx, ty = ox - 1.25, 3.1
    if b.key == 'tide':
        b.ball('Rock stand', (tx, ty, 0.42), (0.85, 0.4, 0.45), '#F2C9B8')
    else:
        b.box('Tank stand', (tx, ty, 0.42), (1.6, 0.7, 0.84), d['stand'], bevel=d['bevel'] * (1.2 if b.key == 'clay' else 1))
    gb = 0.07 if b.key == 'clay' else 0.03 if b.key == 'tide' else 0.0
    bpy.ops.mesh.primitive_cube_add(size=1, location=(tx, ty, 1.24)); g = bpy.context.object; g.name = 'Tank glass'; g.scale = (1.5, 0.6, 0.78)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    b.finish(g, b.mat('Tank glass', '#E9FBFF', glass=True, alpha=0.18), gb, outline=False)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(tx, ty, 1.18)); w = bpy.context.object; w.name = 'Water'; w.scale = (1.44, 0.54, 0.62)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    b.finish(w, b.mat('Water', '#9ED8E8', glass=True, alpha=0.32), gb * 0.8, outline=False)
    b.box('Sand', (tx, ty, 0.9), (1.42, 0.52, 0.05), '#F3E3C0', bevel=0.02, outline=False)
    b.box('Tank rim', (tx, ty, 1.64), (1.56, 0.66, 0.05), d['trim'], bevel=0.02)
    if b.key == 'clay':
        b.cyl('Plant', (tx + 0.55, ty + 0.12, 1.05), 0.05, 0.3, '#9ED6A8')
        b.ball('Plant top', (tx + 0.55, ty + 0.12, 1.22), (0.09, 0.09, 0.07), '#9ED6A8')
    else:
        b.cyl('Plant', (tx + 0.55, ty + 0.12, 1.05), 0.04, 0.32, '#8FCB9C' if b.key == 'tide' else '#A6BE8D')
    b.creature('Pondlet', (tx - 0.38, ty - 0.05, 1.2), 0.24, 200)
    b.creature('Pondlet', (tx - 0.05, ty - 0.1, 1.34), 0.24, 215)
    b.creature('Bubblebelly', (tx + 0.3, ty - 0.02, 1.12), 0.26, 160)
    # A potted plant by the window and a rug at the counter.
    pot = {'clay': '#F3BAA9', 'tide': '#F4AAA1', 'paper': '#CBAB8C'}[b.key]
    b.cyl('Pot', (ox - 2.55, 0.6, 0.25), 0.22, 0.5, pot)
    b.ball('Leaves', (ox - 2.55, 0.6, 0.78), (0.34, 0.34, 0.4), '#9ED6A8' if b.key != 'paper' else '#A6BE8D')
    b.cyl('Rug', (ox + 1.1, 2.0, 0.006), 0.9, 0.012, {'clay': '#E6CDEE', 'tide': '#F6C6B9', 'paper': '#A9C1D9'}[b.key], bevel=0.004, outline=False)
    # The counter on the right, its long side to the room.
    cx, cy = ox + 1.9, 2.0
    if b.key == 'tide':
        for k in range(5):
            b.cyl('Counter rib', (cx, cy - 0.8 + k * 0.4, 0.5), 0.2, 1.0, d['counter'], verts=32)
        b.box('Counter top', (cx, cy, 1.03), (0.75, 2.0, 0.08), d['counter_top'], bevel=0.035)
    else:
        b.box('Counter', (cx, cy, 0.5), (0.7, 2.0, 1.0), d['counter'], bevel=d['bevel'] * (1.4 if b.key == 'clay' else 1))
        b.box('Counter top', (cx, cy, 1.03), (0.82, 2.12, 0.08), d['counter_top'], bevel=0.035 if b.key == 'clay' else 0.012)
        if b.key == 'paper':
            for k in range(3): b.box('Panel', (cx - 0.36, cy - 0.62 + k * 0.62, 0.5), (0.02, 0.48, 0.66), d['trim'], bevel=0.01)
    b.cyl('Bell', (cx, cy - 0.6, 1.1), 0.08, 0.06, '#FFE08A')
    b.ball('Bell top', (cx, cy - 0.6, 1.15), (0.06, 0.06, 0.045), '#FFE08A')
    # The pendant lamp: clay a dome, tide a jellyfish, paper a paper lantern.
    if b.key == 'tide':
        b.ball('Jelly lamp', (ox, 2.4, 2.75), (0.4, 0.4, 0.24), d['lamp'], emit=0.0)
        for k in range(6):
            a = k * math.pi / 3
            b.cyl('Tendril', (ox + 0.22 * math.cos(a), 2.4 + 0.22 * math.sin(a), 2.45), 0.02, 0.4, d['lamp'], verts=12)
    elif b.key == 'paper':
        b.cyl('Cord', (ox, 2.4, 3.05), 0.01, 0.4, '#5C5B74', verts=8, outline=False)
        b.ball('Lantern', (ox, 2.4, 2.65), (0.3, 0.3, 0.36), d['lamp'])
        for k in (-1, 1): b.cyl('Lantern band', (ox, 2.4, 2.65 + k * 0.3), 0.18, 0.05, d['trim'])
    else:
        b.cyl('Cord', (ox, 2.4, 3.0), 0.015, 0.5, '#7C70B2', verts=8, outline=False)
        b.ball('Lamp dome', (ox, 2.4, 2.7), (0.42, 0.42, 0.28), d['lamp'])
    lamp = bpy.data.lights.new('Lamp', 'POINT'); lamp.energy = 90; lamp.color = hexc('#FFE9C4')[:3]; lamp.shadow_soft_size = 0.6
    lo = bpy.data.objects.new('Lamp light', lamp); lo.location = (ox, 2.4, 2.4); bpy.context.scene.collection.objects.link(lo)


def lab(b):
    d = b.d
    ox = 40
    lab_wall = {'clay': '#D9EEDF', 'tide': '#C9E6F0', 'paper': '#E3ECDF'}[b.key]
    lab_floor = {'clay': ('#E4E1F4', '#D7D3EE'), 'tide': ('#E2EEF2', '#D2E4EA'), 'paper': ('#DCE3D2', '#CED8C2')}[b.key]
    tile = 0.5
    for i in range(-6, 7):
        for j in range(0, 10):
            b.box('Lab tile', (ox + i * tile, j * tile + 0.25, -0.05), (tile, tile, 0.1), lab_floor[(i + j) % 2], bevel=0.01, outline=False)
    b.box('Lab back wall', (ox, 5.1, 1.8), (7, 0.2, 3.6), lab_wall, bevel=0, outline=False)
    b.box('Lab left wall', (ox - 3.1, 2.5, 1.8), (0.2, 6, 3.6), lab_wall, bevel=0, outline=False)
    b.box('Lab ceiling', (ox, 2.5, 3.65), (7, 6, 0.1), d['ceiling'], bevel=0, outline=False)
    b.box('Lab right wall', (ox + 3.1, 2.5, 1.8), (0.2, 6, 3.6), lab_wall, bevel=0, outline=False)
    b.box('Lab dado', (ox, 4.98, 0.5), (7, 0.06, 1.0), d['dado'], bevel=0.01, outline=False)
    b.box('Lab rail', (ox, 4.95, 1.02), (7, 0.05, 0.05), d['trim'], bevel=0.02, outline=b.key == 'paper')
    b.box('Light bar', (ox, 2.6, 3.55), (2.6, 0.18, 0.08), '#FFF6DE', bevel=0.03, outline=False)
    # Two growers against the back wall, Bloom and Flow.
    for k, (cname, col) in enumerate(CULTURES.items()):
        gx, gy = ox - 1.9 + k * 1.3, 4.3
        if b.key == 'tide':
            b.cyl('Pedestal', (gx, gy, 0.35), 0.38, 0.7, d['pedestal'], verts=32)
            b.ball('Globe', (gx, gy, 1.15), (0.42, 0.42, 0.42), '#EFFBFF', glass=True, alpha=0.2)
            b.ball('Culture', (gx, gy, 1.05), (0.33, 0.33, 0.28), col)
            b.torus('Band', (gx, gy, 1.15), 0.42, 0.04, d['cap'], rot=(0, 0, 0))
            b.cyl('Top valve', (gx, gy, 1.62), 0.09, 0.12, d['cap'])
            for r in range(6):
                a = r * math.pi / 3
                b.ball('Rivet', (gx + 0.42 * math.cos(a), gy + 0.42 * math.sin(a), 1.15), (0.025, 0.025, 0.025), d['trim'])
        elif b.key == 'paper':
            b.box('Stool', (gx, gy, 0.35), (0.6, 0.6, 0.7), d['pedestal'], bevel=0.02)
            b.cyl('Jar', (gx, gy, 1.05), 0.27, 0.7, '#EFFBFF', glass=True, alpha=0.22)
            b.cyl('Culture', (gx, gy, 0.98), 0.24, 0.5, col)
            b.cyl('Lid band', (gx, gy, 1.42), 0.29, 0.06, d['trim'])
            b.ball('Cloth', (gx, gy, 1.48), (0.31, 0.31, 0.1), d['cap'])
        else:
            b.box('Pedestal', (gx, gy, 0.36), (0.7, 0.7, 0.72), d['pedestal'], bevel=0.12)
            b.cyl('Jar', (gx, gy, 1.08), 0.28, 0.72, '#EFFBFF', glass=True, alpha=0.2)
            b.cyl('Culture', (gx, gy, 1.0), 0.25, 0.52, col, bevel=0.1)
            b.cyl('Cap', (gx, gy, 1.48), 0.3, 0.12, d['cap'], bevel=0.05)
            b.ball('Knob', (gx, gy, 1.58), (0.09, 0.09, 0.07), d['trim'])
    # The incubator: clay an egg dome on a puffy base, tide a diving helmet, paper a bell jar on a painted box.
    ix, iy = ox + 1.2, 3.1
    if b.key == 'tide':
        b.cyl('Incubator base', (ix, iy, 0.42), 0.5, 0.84, d['incubator'], verts=32)
        b.ball('Helmet', (ix, iy, 1.18), (0.46, 0.46, 0.44), '#EFFBFF', glass=True, alpha=0.18)
        b.torus('Collar', (ix, iy, 0.86), 0.48, 0.07, d['trim'], rot=(0, 0, 0))
        b.torus('Porthole', (ix, iy - 0.45, 1.2), 0.17, 0.04, d['trim'], rot=(90, 0, 0))
    elif b.key == 'paper':
        b.box('Incubator box', (ix, iy, 0.42), (0.9, 0.7, 0.84), d['incubator'], bevel=0.02)
        b.cyl('Bell jar', (ix, iy, 1.1), 0.3, 0.5, '#EFFBFF', glass=True, alpha=0.2)
        b.ball('Bell top', (ix, iy, 1.35), (0.3, 0.3, 0.2), '#EFFBFF', glass=True, alpha=0.2)
        b.ball('Bell knob', (ix, iy, 1.57), (0.05, 0.05, 0.05), d['trim'])
    else:
        b.box('Incubator base', (ix, iy, 0.42), (0.95, 0.75, 0.84), d['incubator'], bevel=0.14)
        b.ball('Egg dome', (ix, iy, 1.2), (0.38, 0.33, 0.42), '#EFFBFF', glass=True, alpha=0.2)
        b.torus('Dome ring', (ix, iy, 0.88), 0.38, 0.05, d['trim'], rot=(0, 0, 0))
    b.ball('Dose', (ix - 0.08, iy, 1.0), (0.06, 0.06, 0.06), CULTURES['bloom'])
    b.ball('Dose', (ix + 0.08, iy, 1.0), (0.06, 0.06, 0.06), CULTURES['flow'])
    # The pinboard and the vial shelf.
    b.box('Pinboard', (ox + 1.0, 4.98, 1.8), (1.3, 0.06, 0.85), d['board'], bevel=0.03)
    for k in range(3):
        b.box('Note', (ox + 0.6 + k * 0.4, 4.94, 1.82 + (k % 2) * 0.06), (0.3, 0.02, 0.36), '#FFFBEF', bevel=0.005, outline=False)
        b.ball('Pin', (ox + 0.6 + k * 0.4, 4.92, 1.98 + (k % 2) * 0.06), (0.025, 0.025, 0.025), '#F08C8C', outline=False)
    b.box('Vial shelf', (ox - 2.85, 2.2, 1.3), (0.3, 1.4, 0.06), d['shelf'])
    for k in range(4):
        b.cyl('Vial', (ox - 2.85, 1.7 + k * 0.32, 1.45), 0.06, 0.22, ['#F08C8C', '#7CC3EA', '#FFD27A', '#C6A8F0'][k])
    lamp = bpy.data.lights.new('Lab lamp', 'AREA'); lamp.energy = 160; lamp.size = 2.6; lamp.color = hexc('#F2FBFF')[:3]
    lo = bpy.data.objects.new('Lab lamp', lamp); lo.location = (ox, 2.6, 3.45); bpy.context.scene.collection.objects.link(lo)


def render(key):
    d = DIRECTIONS[key]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.eevee.taa_render_samples = 64
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes['Background']; bg.inputs[0].default_value = hexc(d['world']); bg.inputs[1].default_value = 0.22
    b = Builder(d, key)
    shop(b); lab(b)
    sun_col, sun_e, sun_rot = d['sun']
    sun = bpy.data.lights.new('Sun', 'SUN'); sun.energy = sun_e * 1.1; sun.color = hexc(sun_col)[:3]; sun.angle = math.radians(12)
    so = bpy.data.objects.new('Sun', sun); so.rotation_euler = [math.radians(a) for a in sun_rot]; scene.collection.objects.link(so)
    cam = bpy.data.objects.new('Eye', bpy.data.cameras.new('Eye')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(62)
    for shot, ox, target in (('shop', 0, (-0.4, 4.6, 1.15)), ('lab', 40, (40.2, 4.6, 1.0))):
        cam.location = (ox + 1.0, -0.6, 1.65)
        direction = Vector(target) - cam.location
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(OUT / (key + '-' + shot + '.png'))
        bpy.ops.render.render(write_still=True)
        print('RENDERED', scene.render.filepath)


for key in DIRECTIONS:
    if args.only and key not in args.only.split(','): continue
    render(key)
