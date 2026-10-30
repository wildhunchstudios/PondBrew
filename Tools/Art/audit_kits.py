"""GR-24's audit of every model in the game's three kits (the growth kit, the creatures' diorama kit and the people kit):
each FBX imported as the game reads it, its geometry checked and drawn from two sides on labelled review sheets.

Run: blender --background --factory-startup --python Tools/Art/audit_kits.py -- --repo C:/dev/DumbVideoGameStudio --out <folder>
     [--kits GrowthKit,DioramaKit,PeopleKit] [--only Name,Name] [--no-render]

Writes <out>/audit.json (one record per model) and <out>/sheet-<kit>-<n>.png. The checks, per model:
- parts: the connected pieces of its mesh; closed pieces whose volume comes out negative are inside out (their faces point in);
- floating: pieces that touch no other piece and stand clear of the model's floor (a part left hanging in the air);
- sunken: how far below its origin's floor the model reaches (a station's origin is on the floor, so it should be about 0);
- overlap: pairs of faces on the same plane from different pieces, overlapping (they flicker in the game: z-fighting);
- degenerate faces, edges shared by more than two faces, loose vertices and faces with no material.
None of these is always a fault (a rug lies on the floor, a lamp hangs from a cord); the sheets are looked at with them.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument('--repo', required=True)
parser.add_argument('--out', required=True)
parser.add_argument('--kits', default='GrowthKit,DioramaKit,PeopleKit')
parser.add_argument('--only', default='')
parser.add_argument('--no-render', action='store_true')
parser.add_argument('--per-sheet', type=int, default=8)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
REPO = Path(args.repo).resolve()
OUT = Path(args.out).resolve()
OUT.mkdir(parents=True, exist_ok=True)
ONLY = set(a for a in args.only.split(',') if a)
VIEW_W, VIEW_H = 420, 340


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objs = [o for o in bpy.data.objects if o not in before]
    for m in bpy.data.materials:
        c = None
        if m.use_nodes and m.node_tree:
            bsdf = m.node_tree.nodes.get('Principled BSDF')
            if bsdf:
                c = tuple(bsdf.inputs['Base Color'].default_value[:3])
        # The people kit names its colours (PK_cloth_15867F); the creatures' are the game's: a colour of their own per name.
        hexpart = m.name.split('_')[-1].split('.')[0]
        if len(hexpart) == 6 and all(ch in '0123456789ABCDEFabcdef' for ch in hexpart):
            c = tuple(((int(hexpart[i:i + 2], 16) / 255) ** 2.2) for i in (0, 2, 4))
        elif c is None or (abs(c[0] - c[1]) < 0.02 and abs(c[1] - c[2]) < 0.02 and m.name.startswith(('DK_', 'PK_'))):
            h = sum(ord(ch) * (i + 1) for i, ch in enumerate(m.name)) % 360 / 360.0
            import colorsys
            c = colorsys.hsv_to_rgb(h, 0.45, 0.85)
        m.diffuse_color = (c[0], c[1], c[2], 1.0)
    return objs


def world_mesh(objs):
    """Every mesh of the import in world space, as one bmesh with each face's material name."""
    bm = bmesh.new()
    names = []
    for o in objs:
        if o.type != 'MESH':
            continue
        tmp = bmesh.new()
        tmp.from_mesh(o.data)
        tmp.transform(o.matrix_world)
        offset = len(names)
        for slot in o.material_slots:
            names.append(slot.material.name if slot.material else '')
        if not o.material_slots:
            names.append('')
        for f in tmp.faces:
            f.material_index += offset
        m = bpy.data.meshes.new('tmp')
        tmp.to_mesh(m)
        tmp.free()
        bm.from_mesh(m)
        bpy.data.meshes.remove(m)
    bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    return bm, names


def islands(bm):
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]; part = []
        seen.add(f.index)
        while stack:
            g = stack.pop(); part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); stack.append(h)
        out.append(part)
    return out


def signed_volume(faces):
    v = 0.0
    for f in faces:
        vs = [x.co for x in f.verts]
        for i in range(1, len(vs) - 1):
            v += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
    return v


def closed(faces):
    for f in faces:
        for e in f.edges:
            if len(e.link_faces) < 2:
                return False
    return True


def check(bm, mats):
    parts = islands(bm)
    report = {'tris': sum(len(f.verts) - 2 for f in bm.faces), 'parts': len(parts)}
    inside_out = []
    boxes = []
    for i, p in enumerate(parts):
        pts = [v.co for f in p for v in f.verts]
        lo = Vector((min(c.x for c in pts), min(c.y for c in pts), min(c.z for c in pts)))
        hi = Vector((max(c.x for c in pts), max(c.y for c in pts), max(c.z for c in pts)))
        boxes.append((lo, hi))
        if closed(p) and signed_volume(p) < -1e-9:
            inside_out.append({'part': i, 'material': mats[p[0].material_index] if p[0].material_index < len(mats) else '', 'size': [round(x, 3) for x in (hi - lo)], 'at': [round(x, 3) for x in (lo + hi) / 2]})
    report['insideOut'] = inside_out
    all_lo = Vector((min(b[0].x for b in boxes), min(b[0].y for b in boxes), min(b[0].z for b in boxes))) if boxes else Vector()
    all_hi = Vector((max(b[1].x for b in boxes), max(b[1].y for b in boxes), max(b[1].z for b in boxes))) if boxes else Vector()
    report['bounds'] = {'min': [round(x, 3) for x in all_lo], 'max': [round(x, 3) for x in all_hi]}
    report['sunken'] = round(-all_lo.z, 3) if all_lo.z < -0.005 else 0

    # Floating: a piece whose every vertex is more than 2 mm from every other piece, clear of the model's floor.
    floating = []
    if 1 < len(parts) <= 900:
        trees = []
        for p in parts:
            vs = []; fs = []; idx = {}
            for f in p:
                face = []
                for v in f.verts:
                    if v.index not in idx:
                        idx[v.index] = len(vs); vs.append(v.co.copy())
                    face.append(idx[v.index])
                fs.append(face)
            trees.append(BVHTree.FromPolygons(vs, fs))
        for i, p in enumerate(parts):
            lo, hi = boxes[i]
            if lo.z <= all_lo.z + 0.01:
                continue
            touching = False
            verts = list({v.index: v.co for f in p for v in f.verts}.values())
            step = max(1, len(verts) // 60)
            for j, t in enumerate(trees):
                if j == i:
                    continue
                olo, ohi = boxes[j]
                if any(lo[k] > ohi[k] + 0.003 or hi[k] < olo[k] - 0.003 for k in range(3)):
                    continue
                for co in verts[::step]:
                    hit = t.find_nearest(co, 0.003)
                    if hit[0] is not None:
                        touching = True; break
                if touching:
                    break
                # A piece inside another (a knob sunk in a body) touches it.
                if all(olo[k] <= lo[k] and hi[k] <= ohi[k] for k in range(3)):
                    touching = True; break
            if not touching:
                floating.append({'part': i, 'material': mats[p[0].material_index] if p[0].material_index < len(mats) else '', 'size': [round(x, 3) for x in (hi - lo)], 'at': [round(x, 3) for x in (lo + hi) / 2], 'gap': round(lo.z - all_lo.z, 3)})
    report['floating'] = floating

    # Overlapping coplanar faces of different pieces (z-fighting).
    part_of = {}
    for i, p in enumerate(parts):
        for f in p:
            part_of[f.index] = i
    groups = {}
    for f in bm.faces:
        if f.calc_area() < 1e-6:
            continue
        n = f.normal
        key = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(n.dot(f.verts[0].co) / 0.0015))
        groups.setdefault(key, []).append(f)
    overlaps = []
    for fs in groups.values():
        if len(fs) < 2 or len(fs) > 400:
            continue
        for a in range(len(fs)):
            fa = fs[a]
            alo = [min(v.co[k] for v in fa.verts) for k in range(3)]; ahi = [max(v.co[k] for v in fa.verts) for k in range(3)]
            for b in range(a + 1, len(fs)):
                fb = fs[b]
                if part_of[fa.index] == part_of[fb.index]:
                    continue
                blo = [min(v.co[k] for v in fb.verts) for k in range(3)]; bhi = [max(v.co[k] for v in fb.verts) for k in range(3)]
                ov = [min(ahi[k], bhi[k]) - max(alo[k], blo[k]) for k in range(3)]
                flat = sorted(ov)
                if flat[1] > 0.004 and flat[2] > 0.004 and ov[0] > -0.002 and ov[1] > -0.002 and ov[2] > -0.002:
                    if not fa.normal.dot(fb.normal) > 0.999:
                        continue
                    overlaps.append({'parts': [part_of[fa.index], part_of[fb.index]], 'materials': [mats[fa.material_index] if fa.material_index < len(mats) else '', mats[fb.material_index] if fb.material_index < len(mats) else ''],
                                     'at': [round((alo[k] + ahi[k]) / 2, 3) for k in range(3)], 'area': round(min(fa.calc_area(), fb.calc_area()), 5)})
                    if len(overlaps) > 60:
                        break
            if len(overlaps) > 60:
                break
    report['overlaps'] = overlaps
    report['degenerate'] = sum(1 for f in bm.faces if f.calc_area() < 1e-9)
    report['nonManifoldEdges'] = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    report['looseVerts'] = sum(1 for v in bm.verts if not v.link_faces)
    report['noMaterial'] = sum(1 for f in bm.faces if f.material_index >= len(mats) or not mats[f.material_index])
    report['materials'] = sorted(set(m for m in mats if m))
    return report


# ---------------------------------------------------------------- the review sheets

def render_views(objs, name, folder):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'
    sh.show_object_outline = True; sh.show_specular_highlight = True; sh.background_type = 'VIEWPORT'
    sh.background_color = (0.86, 0.88, 0.9)
    scene.render.resolution_x = VIEW_W; scene.render.resolution_y = VIEW_H; scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.use_stamp = True; scene.render.use_stamp_note = True; scene.render.stamp_font_size = 22
    scene.render.stamp_foreground = (1, 1, 1, 1); scene.render.stamp_background = (0.1, 0.12, 0.2, 0.85)
    for flag in ('use_stamp_date', 'use_stamp_time', 'use_stamp_render_time', 'use_stamp_frame', 'use_stamp_frame_range', 'use_stamp_memory',
                 'use_stamp_hostname', 'use_stamp_camera', 'use_stamp_lens', 'use_stamp_scene', 'use_stamp_marker', 'use_stamp_filename', 'use_stamp_sequencer_strip'):
        if hasattr(scene.render, flag):
            setattr(scene.render, flag, False)
    meshes = [o for o in objs if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    if not pts:
        return []
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2; radius = max((hi - lo).length / 2, 0.05)
    cam_data = bpy.data.cameras.new('cam'); cam_data.lens = 50; cam_data.clip_start = radius * 0.01; cam_data.clip_end = radius * 50
    cam = bpy.data.objects.new('cam', cam_data); scene.collection.objects.link(cam); scene.camera = cam
    files = []
    # Unity's forward (+Z) is Blender's -Y: the front view looks from -Y toward +Y... from the side the game shows (Unity -Z is Blender +Y).
    for i, (az, el) in enumerate([(145, 22), (-35, 22)]):
        d = Vector((math.sin(math.radians(az)) * math.cos(math.radians(el)), math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
        dist = radius / math.tan(math.radians(18)) * 1.05
        cam.location = center + d * dist
        cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.stamp_note_text = name + ('' if i == 0 else ' (back)')
        f = folder / (name + '-' + str(i) + '.png')
        scene.render.filepath = str(f)
        bpy.ops.render.render(write_still=True)
        files.append(f)
    bpy.data.objects.remove(cam); bpy.data.cameras.remove(cam_data)
    return files


def tile(files, out, cols):
    rows = (len(files) + cols - 1) // cols
    sheet = np.ones((rows * VIEW_H, cols * VIEW_W, 4), dtype=np.float32)
    for i, f in enumerate(files):
        img = bpy.data.images.load(str(f))
        px = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
        r, c = divmod(i, cols)
        y0 = (rows - 1 - r) * VIEW_H
        sheet[y0:y0 + img.size[1], c * VIEW_W:c * VIEW_W + img.size[0]] = px
        bpy.data.images.remove(img)
    im = bpy.data.images.new('sheet', cols * VIEW_W, rows * VIEW_H, alpha=True)
    im.pixels = sheet.ravel()
    im.filepath_raw = str(out); im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im)


results = {}
for kit in args.kits.split(','):
    folder = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art' / kit
    files = sorted(folder.glob('*.fbx'))
    views = OUT / ('views-' + kit); views.mkdir(exist_ok=True)
    rendered = []
    for path in files:
        name = path.stem
        if ONLY and name not in ONLY:
            continue
        reset()
        objs = import_fbx(path)
        bm, mats = world_mesh(objs)
        rep = check(bm, mats)
        bm.free()
        rep['kit'] = kit
        results[name] = rep
        if not args.no_render:
            rendered += render_views(objs, name, views)
        print('AUDIT', name, 'tris', rep['tris'], 'parts', rep['parts'], 'insideOut', len(rep['insideOut']), 'floating', len(rep['floating']), 'overlaps', len(rep['overlaps']), 'sunken', rep['sunken'], flush=True)
    if rendered:
        per = args.per_sheet * 2
        for n in range(0, len(rendered), per):
            reset()
            tile(rendered[n:n + per], OUT / ('sheet-' + kit + '-' + str(n // per + 1) + '.png'), 4)

(OUT / 'audit.json').write_text(json.dumps(results, indent=1))
print('AUDIT_COMPLETE', len(results))
