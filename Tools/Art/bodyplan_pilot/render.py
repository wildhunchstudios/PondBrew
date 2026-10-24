# GR-12B pilot sheets: builds PILOT (or, with OLD, the kit's own builders for the same species) and renders them into OUT_DIR,
# each file name starting with PREFIX.
import os
os.makedirs(OUT_DIR, exist_ok=True)
built = []
for sid, asset, build in PILOT:
    if OLD:
        build = CREATURE_BUILDERS[sid]
    root = build()
    rec = stats(root, asset)
    print('BP_BUILT', sid, rec['triangles'], [round(x, 3) for x in rec['bounds_unity_m']['size']],
          sorted(o.name for o in root.children_recursive))
    for child in root.children_recursive:
        child.name = asset + ' / ' + child.name
    built.append((sid, root))

scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_object_outline = True
scene.display.shading.object_outline_color = (0.02, .06, .06)
scene.display.shading.show_cavity = True
scene.display.shading.show_shadows = True
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new('Review sky') if scene.world is None else scene.world
scene.world.color = (.85, .78, .62)
camdata = bpy.data.cameras.new('Pilot camera'); cam = bpy.data.objects.new('Pilot camera', camdata)
COLLECTION.objects.link(cam); scene.camera = cam; camdata.lens = 50
def shot(fname, pos, target, w, h):
    scene.render.resolution_x, scene.render.resolution_y = w, h
    cam.location = u(pos)
    cam.rotation_euler = (u(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(OUT_DIR, PREFIX + fname)
    bpy.ops.render.render(write_still=True)
    print('BP_RENDER', fname)
def place(fn):
    for j, (sid, root) in enumerate(built):
        root.location = u(fn(j))
camdata.type = 'ORTHO'; camdata.ortho_scale = 7.6
place(lambda j: ((j % 4 - 1.5) * 1.75, (1 - j // 4) * 1.5, 40))
shot("Front.png", (-6.0, 4.0, 40 - 10.0), (0, .55, 40), 1800, 1000)
camdata.type = 'PERSP'
place(lambda j: ((j % 4 - 1.5) * 1.7, (1 - j // 4) * 1.45, 40))
shot('Back.png', (1.9, 1.4, 40 + 9.0), (0, .62, 40), 1800, 1000)
place(lambda j: (0, (1 - j // 4) * 1.45, 40 + (j % 4 - 1.5) * 2.4))
shot('Side.png', (-12.2, 1.0, 40.1), (0, .62, 40.1), 2000, 900)
place(lambda j: ((j % 4 - 1.5) * 1.6, 0, 40 + (j // 4 - .5) * 1.9))
shot('Top.png', (0, 9.5, 40.01), (0, 0, 40), 1600, 1100)
# Close-ups of each face.
for j, (sid, root) in enumerate(built):
    for k, (s2, r2) in enumerate(built):
        r2.location = u((0, 0, 0)) if k == j else u((100 + k * 3, 0, 0))
    shot('Face_' + sid + '.png', (-.75, .25, -1.45), (0, .02, -.1), 700, 600)
print('BP_DONE')
