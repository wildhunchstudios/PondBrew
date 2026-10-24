"""GR-12B body-plan pilot (GR.34): the Gloamer's eight as animals of their own, rendered as review sheets for the user's look.
Nothing is exported to the game: the kit script's helpers run (everything before its build loop, which writes nothing), then
helpers.py (body-plan helpers), gloamer.py (the eight builders) and render.py (the sheets).

Run: blender --background --factory-startup --python Tools/Art/bodyplan_pilot/pilot.py -- --repo C:/dev/DumbVideoGameStudio [--old]
--old renders the kit's current builders for the same species instead, for the before-and-after sheets.
Writes ArtSource/DioramaKit/GR12B_pilot/(Before_)Front.png, Side.png, Top.png, Faces.png and the face close-ups.
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index('--') + 1:]
repo = argv[argv.index('--repo') + 1]
old = '--old' in argv
kit = os.path.join(repo, 'Tools/Art/create_diorama_kit.py')
src = open(kit, encoding='utf-8').read()
cut = src.index('# ================================================================ build, export, review renders, manifest')
sys.argv = [sys.argv[0], '--', '--repo', repo, '--no-render']
g = {'__file__': kit, '__name__': '__kit__'}
exec(compile(src[:cut], kit, 'exec'), g)
for name in ('helpers.py', 'gloamer.py'):
    path = os.path.join(HERE, name)
    exec(compile(open(path, encoding='utf-8').read(), path, 'exec'), g)
g['OUT_DIR'] = os.path.join(repo, 'ArtSource/DioramaKit/GR12B_pilot')
g['OLD'] = old
g['PREFIX'] = 'Before_' if old else ''
path = os.path.join(HERE, 'render.py')
exec(compile(open(path, encoding='utf-8').read(), path, 'exec'), g)
