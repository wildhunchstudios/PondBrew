"""Read-only FBX round-trip check for the original Evolution Shop kit."""
import bpy
import hashlib
import json
from pathlib import Path

repo=Path('C:/dev/DumbVideoGameStudio')
folder=repo/'ArtSource/RedesignKit'
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
results=[]
for record in manifest['assets']:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    fbx=repo/record['export']
    assert hashlib.sha256(fbx.read_bytes()).hexdigest()==record['sha256']
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=False)
    meshes=[ob for ob in bpy.context.scene.objects if ob.type=='MESH']
    coords=[]
    for ob in meshes:
        assert ob.matrix_world.determinant()>0, record['id']
        for v in ob.data.vertices:
            p=ob.matrix_world@v.co
            coords.append((p.x,p.z,-p.y))
    actual={'min':[min(p[i] for p in coords) for i in range(3)],'max':[max(p[i] for p in coords) for i in range(3)]}
    expected=record['bounds_unity_m']
    errors=[abs(actual[key][i]-expected[key][i]) for key in ['min','max'] for i in range(3)]
    max_error=max(errors)
    assert max_error<.0001,(record['id'],actual,expected)
    triangles=sum(len(ob.data.polygons) for ob in meshes)
    assert triangles==record['triangles'],record['id']
    assert len(meshes)==1,record['id']
    assert all(len(ob.data.uv_layers)==1 for ob in meshes),record['id']
    results.append({'id':record['id'],'sha256_matches':True,'mesh_count':len(meshes),'triangles':triangles,
                    'triangle_count_matches':True,'max_bounds_error_m':max_error,
                    'positive_determinants':True,'uv_layers':1,'bounds_roundtrip_unity_m':actual})
receipt={'blender':bpy.app.version_string,'kind':'Local Blender FBX export/reimport verification; not Unity proof',
         'passed':True,'assets':results}
(folder/'fbx_roundtrip_validation.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print('FBX_ROUNDTRIP_PASS '+json.dumps(results))
