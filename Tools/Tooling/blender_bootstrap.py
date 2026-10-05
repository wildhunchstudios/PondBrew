"""Project-only Blender addon session; no saved preferences or external services."""
import bpy
import importlib.util
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SESSION = ROOT / '.tooling' / 'blender-session'
SESSION.mkdir(parents=True, exist_ok=True)
ADDON = ROOT / '.tooling' / 'blender-venv' / 'Lib' / 'site-packages' / 'blender_mcp' / 'bundled' / 'addon.py'
if bpy.app.background:
    raise RuntimeError('Upstream addon requires the Blender GUI event loop. Launch with Start-Blender.ps1.')
os.environ['DISABLE_TELEMETRY'] = 'true'
os.environ['BLENDER_MCP_SAFE_MODE'] = '1'
spec = importlib.util.spec_from_file_location('evolution_blender_mcp', ADDON)
addon = importlib.util.module_from_spec(spec)
spec.loader.exec_module(addon)
addon.register()
# The reviewed addon schedules an update check and auto-start. This isolated
# session explicitly owns its local server and does not run either callback.
addon._blendermcp_unregister_auto_start()
if bpy.app.timers.is_registered(addon._addon_update_check_on_startup):
    bpy.app.timers.unregister(addon._addon_update_check_on_startup)
scene = bpy.context.scene
scene.name = 'EvolutionShop_Tooling_Scratch'
scene.blendermcp_auto_start_server = False
for provider in ('polyhaven', 'hyper3d', 'hunyuan3d', 'sketchfab', 'tripo', 'polypizza'):
    setattr(scene, 'blendermcp_use_' + provider, False)
server = addon.BlenderMCPServer(host='127.0.0.1', port=19876)
bpy.types.blendermcp_server = server
server.start()
if not server.running:
    raise RuntimeError('Blender addon server did not start')
receipt = {'pid': os.getpid(), 'blenderVersion': bpy.app.version_string,
    'projectRoot': str(ROOT), 'scene': scene.name, 'file': bpy.data.filepath,
    'host': server.host, 'port': server.port, 'telemetry': False,
    'providersEnabled': [], 'preferencesSaved': False}
(SESSION / 'ready.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')

def check_stop():
    if (SESSION / 'stop.request').exists():
        server.stop()
        (SESSION / 'stopped.json').write_text(json.dumps({'pid': os.getpid(), 'cleanStopRequested': True}), encoding='utf-8')
        bpy.ops.wm.quit_blender()
        return None
    return 0.5

bpy.app.timers.register(check_stop)
print('EVOLUTION_BLENDER_READY ' + json.dumps(receipt), flush=True)
