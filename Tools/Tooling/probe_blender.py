"""Read-only MCP end-to-end probe. Run with the pinned project venv Python."""
import asyncio
import json
import os
from pathlib import Path
import sys
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = Path(__file__).resolve().parents[2]
async def main():
    env = dict(os.environ, BLENDER_HOST='127.0.0.1', BLENDER_PORT='19876', BLENDER_MCP_SAFE_MODE='1', DISABLE_TELEMETRY='true')
    server = StdioServerParameters(command=str(ROOT/'.tooling/blender-venv/Scripts/mcp-for-blender.exe'), args=[], env=env)
    evidence = {}
    async with stdio_client(server) as (read, write):
        async with ClientSession(read, write) as session:
            evidence['initialize'] = (await session.initialize()).model_dump(mode='json')
            listing = await session.list_tools()
            evidence['tools'] = [{'name': t.name, 'inputSchema': t.inputSchema} for t in listing.tools]
            result = await session.call_tool('get_scene_info', {'limit': 10})
            evidence['scene'] = result.model_dump(mode='json')
            # Prove that the actual safe-mode MCP path rejects a forbidden import
            # without executing it in Blender. This has no command side effect.
            blocked = await session.call_tool('execute_blender_code', {'code': 'import os\nprint(os.getcwd())', 'user_prompt': ''})
            evidence['safeModeRejection'] = blocked.model_dump(mode='json')
    target = Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'.tooling/blender-session/mcp-probe.json'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(evidence,indent=2),encoding='utf-8')
    scene_text = '\n'.join(c.get('text','') for c in evidence['scene'].get('content',[]))
    reject_text = '\n'.join(c.get('text','') for c in evidence['safeModeRejection'].get('content',[]))
    ok = 'EvolutionShop_Tooling_Scratch' in scene_text and ('not allowed' in reject_text or 'blocked' in reject_text.lower() or 'SAFE_MODE' in reject_text)
    print(json.dumps({'passed':ok,'scene':scene_text,'safeModeRejection':reject_text,'evidence':str(target)},indent=2))
    if not ok: raise SystemExit(1)

asyncio.run(main())
