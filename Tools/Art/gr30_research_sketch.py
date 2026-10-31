# GR-30 look development (not game art): a sketch of the research room for the user's preview (tester round 1, F1, GR.70). It
# builds on the approved sketches' parts (gr16_look_ranch.py): three small research tanks along the back wall, a wall of
# specimen jars for the creatures put in the room, and the research desk with its grid of pairs. Original and local.
#   blender --background --factory-startup --python Tools/Art/gr30_research_sketch.py -- --repo C:/dev/DumbVideoGameStudio --out <folder> [--quick]
import math
from pathlib import Path

_here = Path(__file__).resolve().parent if '__file__' in globals() else Path('Tools/Art')
_source = (_here / 'gr16_look_ranch.py').read_text(encoding='utf-8')
exec(compile(_source[:_source.rindex('main()')], 'gr16_look_ranch.py', 'exec'), globals())


def research_tank(b, at, parents, timer):
    """A small tank for trying a pair: as the breeding tank, without its nursery, and with a round timer on its cabinet."""
    x, y = at
    w, d = 1.06, 0.44
    plastic, teal = b.mat('plastic', C['cream']), b.mat('plastic', C['teal'])
    b.box('Research cabinet', (x, y, 0.39), (w + 0.04, d + 0.06, 0.78), plastic, r=0.07)
    b.box('Research kick', (x, y, 0.04), (w, d, 0.08), b.mat('plastic', C['teal_dark']), r=0.03)
    b.box('Research frame', (x, y, 0.81), (w + 0.08, d + 0.08, 0.06), teal, r=0.03)
    b.box('Research rim', (x, y, 1.3), (w + 0.08, d + 0.08, 0.06), teal, r=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.cyl('Research post', (x + sx * w / 2, y + sy * d / 2, 1.05), 0.03, 0.46, teal, r=0.01)
    b.box('Research glass', (x, y, 1.05), (w, d, 0.46), b.mat('glass', '#EAFBFF', alpha=0.1), r=0, shadow=False)
    b.box('Research water', (x, y, 1.02), (w - 0.04, d - 0.04, 0.38), b.mat('water', '#7FD8F0', alpha=0.25), r=0.01, shadow=False)
    b.box('Research sand', (x, y, 0.86), (w - 0.06, d - 0.06, 0.04), b.mat('paint', C['sand']), r=0.012)
    b.ball('Water plant', (x + 0.42, y + 0.1, 1.0), (0.04, 0.03, 0.14), b.mat('foliage', C['green']), seg=16)
    # The timer: a dark round screen on the cabinet with a bright arc of how far the pair has got.
    b.cyl('Timer face', (x, y - d / 2 - 0.045, 0.45), 0.15, 0.03, b.mat('plastic', C['screen']), r=0.01, rot=(90, 0, 0))
    for k in range(16):
        a = math.radians(90 - k * 22.5)
        on = k / 16 < timer
        b.box('Timer tick', (x + math.cos(a) * 0.11, y - d / 2 - 0.066, 0.45 + math.sin(a) * 0.11), (0.03, 0.006, 0.03),
              b.mat('emit', '#7FE3D6', glow=4) if on else b.mat('plastic', '#2B5C66'), r=0, shadow=False)
    if parents:
        b.creature(parents[0], (x - 0.24, y - 0.02, 1.04), 0.24, 300)
        b.creature(parents[1], (x + 0.16, y - 0.04, 1.06), 0.24, 240)
    # Its cable up the wall: each one draws a unit of the generator's power.
    b.cyl('Power cable', (x + 0.4, y + d / 2 + 0.1, 2.0), 0.012, 1.5, b.mat('plastic', C['ink']), r=0)
    b.box('Power box', (x + 0.4, y + d / 2 + 0.06, 1.42), (0.1, 0.05, 0.14), b.mat('plastic', C['yellow']), r=0.015)


def specimen_wall(b, ox, y0, species):
    """The creatures put in the room: a wall of little lit jars, each showing one, none of them for sale."""
    x = ox + 3.02
    b.box('Specimen back', (x + 0.03, y0 + 1.25, 1.42), (0.06, 2.7, 1.5), b.mat('plastic', C['teal_dark']), r=0.03)
    b.box('Specimen top', (x - 0.1, y0 + 1.25, 2.2), (0.32, 2.8, 0.06), b.mat('plastic', C['teal']), r=0.025)
    n = 0
    for row in range(3):
        z = 0.92 + row * 0.46
        b.box('Specimen shelf', (x - 0.1, y0 + 1.25, z - 0.14), (0.32, 2.7, 0.04), b.mat('plastic', C['cream']), r=0.015)
        for col in range(5):
            yy = y0 + 0.2 + col * 0.525
            b.cyl('Jar', (x - 0.12, yy, z + 0.04), 0.14, 0.3, b.mat('glass', '#EAFBFF', alpha=0.14), r=0)
            b.cyl('Jar lid', (x - 0.12, yy, z + 0.205), 0.145, 0.03, b.mat('plastic', C['yellow']), r=0.01)
            b.cyl('Jar water', (x - 0.12, yy, z + 0.01), 0.125, 0.22, b.mat('water', '#7FD8F0', alpha=0.25), r=0)
            if n < len(species):
                b.creature(species[n], (x - 0.12, yy, z + 0.04), 0.17, 90)
            n += 1
    b.light('AREA', 'Specimen glow', (x - 0.55, y0 + 1.25, 2.1), 22, '#E8FBFF', size=(0.3, 2.4), rot=(0, 30, 0))


def research_desk(b, ox, y):
    """The desk at the front on the left: a big screen with the grid of pairs: rows and columns of the creatures put in, each cell
    what that pair made, a question mark's dot for one not tried, and the ones waiting in the queue."""
    x = ox - 2.35
    b.box('Desk top', (x, y, 0.85), (1.3, 0.9, 0.07), b.mat('plastic', C['cream']), r=0.03)
    b.box('Desk body', (x, y, 0.42), (1.2, 0.8, 0.78), b.mat('wood', C['wood'], grain='z'), r=0.04)
    for k in range(2):
        b.box('Desk drawer', (x + 0.3, y - 0.41, 0.3 + k * 0.32), (0.5, 0.03, 0.26), b.mat('plastic', C['teal']), r=0.02)
        b.ball('Drawer knob', (x + 0.3, y - 0.44, 0.3 + k * 0.32), 0.03, b.mat('plastic', C['yellow']), seg=16)
    b.box('Screen frame', (x, y + 0.2, 1.42), (1.16, 0.06, 0.86), b.mat('plastic', C['teal_dark']), r=0.04)
    b.box('Screen', (x, y + 0.165, 1.42), (1.04, 0.01, 0.74), b.mat('plastic', C['screen']), r=0)
    b.cyl('Screen foot', (x, y + 0.2, 0.95), 0.05, 0.14, b.mat('plastic', C['teal_dark']), r=0.01)
    tried = {(0, 1): '#FF7A7A', (0, 2): '#FFC93C', (1, 2): '#9BDB4D', (1, 3): '#7FE3D6', (2, 3): '#A98BFF', (3, 4): '#FF9EC4'}
    queued = {(0, 3), (0, 4), (2, 4)}
    for r in range(6):
        for c in range(6):
            cx, cz = x - 0.42 + c * 0.17, 1.72 - r * 0.115
            key = (min(r, c), max(r, c))
            if r == c: col, glow = '#1B4F5C', 0
            elif key in tried: col, glow = tried[key], 2.6
            elif key in queued: col, glow = '#FFE08A', 1.2
            else: col, glow = '#3B7382', 0
            b.box('Cell', (x + (cx - x), y + 0.155, cz), (0.14, 0.006, 0.095), b.mat('emit', col, glow=glow) if glow else b.mat('plastic', col), r=0, shadow=False)
    b.box('Keyboard', (x, y - 0.2, 0.9), (0.5, 0.2, 0.025), b.mat('plastic', C['teal']), r=0.008)


def room(b):
    ox = 400
    H = 3.3
    for i in range(-6, 7):
        for j in range(0, 11):
            col = '#7D6BD0' if (i + j) % 4 == 0 else ('#F2EDFF' if (i + j) % 2 else '#E6DEFF')
            b.box('Tile', (ox + i * 0.5, j * 0.5 - 0.25, -0.03), (0.49, 0.49, 0.06), b.mat('plastic', col, rough=0.25), r=0.02, seg=2)
    y0, y1 = shell(b, ox, 'Research', (1.2, 2.5), (1.15, 2.3), b.mat('paint', '#EFE9FF', noise=0.03))
    panel = b.mat('plastic', '#C9BDF2', rough=0.35)
    for k in range(7):
        b.box('Wall panel', (ox - 2.6 + k * 0.87, y1 - 0.12, 0.55), (0.82, 0.05, 0.9), panel, r=0.05)
    for k in range(6):
        yy = y0 + 0.45 + k * 0.9
        if not (1.0 < yy < 2.7): b.box('Wall panel', (ox - 3.08, yy, 0.55), (0.05, 0.84, 0.9), panel, r=0.05)
    b.box('Wall panel', (ox - 3.08, 1.85, 0.57), (0.05, 1.4, 0.98), panel, r=0.05)
    rail = b.mat('plastic', C['teal'])
    b.box('Rail', (ox, y1 - 0.15, 1.05), (6.2, 0.07, 0.07), rail, r=0.03)
    window(b, ox - 3.1, 1.85, 1.15, 2.3, 1.3)
    b.box('Light housing', (ox, 2.4, H - 0.04), (0.5, 4.2, 0.08), b.mat('plastic', C['cream']), r=0.03)
    b.box('Light strip', (ox, 2.4, H - 0.085), (0.36, 4.0, 0.01), b.mat('emit', '#F5FCFF', glow=3.5), r=0, shadow=False)
    research_tank(b, (ox - 1.75, 4.55), ('Pondlet', 'Bubblebelly'), 0.65)
    research_tank(b, (ox - 0.35, 4.55), ('Bubblebelly', 'Pondlet'), 0.3)
    research_tank(b, (ox + 1.05, 4.55), None, 0)
    b.box('Sign plate', (ox - 0.35, y1 - 0.13, 2.45), (2.0, 0.04, 0.42), b.mat('plastic', C['teal_dark']), r=0.05)
    b.box('Sign strip', (ox - 0.35, y1 - 0.155, 2.45), (1.76, 0.01, 0.1), b.mat('emit', '#FFE08A', glow=2), r=0.004, shadow=False)
    specimen_wall(b, ox, 1.45, ['Pondlet', 'Bubblebelly', 'Pondlet', 'Bubblebelly', 'Pondlet', 'Bubblebelly', 'Pondlet', 'Bubblebelly'])
    research_desk(b, ox, 1.05)
    plant(b, (ox + 2.6, 0.75), 0.8)
    b.light('AREA', 'Research strip light', (ox, 2.4, H - 0.12), 110, '#F7F4FF', size=(0.5, 4.0))
    b.light('AREA', 'Research fill', (ox, -0.3, 2.4), 90, '#FFF6EA', size=(5.0, 2.0), rot=(90, 0, 0))


def sketch():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    setup(scene)
    b = B()
    outdoors(b)
    room(b)
    cam = bpy.data.objects.new('Eye', bpy.data.cameras.new('Eye')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.clip_start = 0.1
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(62)
    ox = 400
    shots = {
        'room': ((ox + 0.3, -0.45, 1.65), (ox - 0.1, 4.6, 1.0)),
        'desk': ((ox - 1.3, -0.1, 1.55), (ox - 2.35, 1.3, 1.3)),
        'tanks': ((ox - 0.7, 2.4, 1.45), (ox - 0.9, 4.6, 0.95)),
        'jars': ((ox + 1.5, 1.2, 1.55), (ox + 3.0, 2.7, 1.35)),
    }
    for name, (at, target) in shots.items():
        cam.location = at
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.view_settings.exposure = -0.25
        scene.render.filepath = str(OUT / ('research-' + name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('RENDERED', scene.render.filepath)


sketch()
