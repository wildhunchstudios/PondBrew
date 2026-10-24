"""Stacks each Before_ sheet above its pilot sheet into Compare_<sheet>.png (the current eight on top, the pilot below).
Run: blender --background --factory-startup --python Tools/Art/bodyplan_pilot/compare.py -- <folder>"""
import sys, bpy, numpy as np
d = sys.argv[sys.argv.index('--') + 1]
for sheet in ('Front', 'Faces', 'Side'):
    before, after = (bpy.data.images.load(d + '/' + p + sheet + '.png') for p in ('Before_', ''))
    W, H = after.size
    a = np.array(after.pixels[:], dtype=np.float32).reshape(H, W, 4)
    b = np.array(before.pixels[:], dtype=np.float32).reshape(H, W, 4)
    gap = np.ones((12, W, 4), dtype=np.float32); gap[..., :3] = .25
    buf = np.concatenate([a, gap, b], axis=0)  # rows run bottom to top: the pilot below, the current eight on top
    out = bpy.data.images.new('compare', W, buf.shape[0])
    out.pixels[:] = buf.ravel(); out.filepath_raw = d + '/Compare_' + sheet + '.png'; out.file_format = 'PNG'; out.save()
