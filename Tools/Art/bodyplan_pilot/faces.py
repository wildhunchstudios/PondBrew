"""Combines the eight face close-ups of the pilot into (Before_)Faces.png, four by two.
Run: blender --background --factory-startup --python Tools/Art/bodyplan_pilot/faces.py -- <folder> [<prefix>]"""
import sys, bpy, numpy as np
argv = sys.argv[sys.argv.index('--') + 1:]
d, prefix = argv[0], (argv[1] if len(argv) > 1 else '')
names = ['moonmouth', 'gloomfin', 'shadeback', 'hushray', 'duskdrifter', 'murkmaw', 'nightlantern', 'eclipseleviathan']
imgs = [bpy.data.images.load(d + '/' + prefix + 'Face_' + n + '.png') for n in names]
W, H = imgs[0].size
buf = np.zeros((H * 2, W * 4, 4), dtype=np.float32)
for k, im in enumerate(imgs):
    a = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4)
    r, c = divmod(k, 4); y = (1 - r) * H
    buf[y:y + H, c * W:(c + 1) * W] = a
out = bpy.data.images.new('faces', W * 4, H * 2)
out.pixels[:] = buf.ravel(); out.filepath_raw = d + '/' + prefix + 'Faces.png'; out.file_format = 'PNG'; out.save()
