"""Exact local Blender FBX reimport / envelope audit; not Unity player proof."""
import bpy, hashlib, json, math
from pathlib import Path
repo=Path('C:/dev/DumbVideoGameStudio')
folder=repo/'ArtSource/CollectorKit'
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
results=[]
for record in manifest['assets']:
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    fbx=repo/record['export']
    assert hashlib.sha256(fbx.read_bytes()).hexdigest()==record['sha256']
    bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=False)
    meshes=[ob for ob in bpy.context.scene.objects if ob.type=='MESH']
    assert len(meshes)==1
    coords=[]; face_samples=[]
    for ob in meshes:
        assert ob.matrix_world.determinant()>0
        def point(v):
            p=ob.matrix_world@v
            return (p.x,p.z,-p.y)
        coords.extend(point(v.co) for v in ob.data.vertices)
        for p in ob.data.polygons:
            verts=[ob.data.vertices[i].co for i in p.vertices]
            center=sum(verts,verts[0]*0)/len(verts)
            face_samples.append(point(center))
            for i in range(len(verts)): face_samples.append(point((verts[i]+verts[(i+1)%len(verts)])/2))
    actual={key:[fn(v[i] for v in coords) for i in range(3)] for key,fn in [('min',min),('max',max)]}
    errors=[abs(actual[key][i]-record['bounds_unity_m'][key][i]) for key in ['min','max'] for i in range(3)]
    triangles=sum(len(ob.data.polygons) for ob in meshes)
    assert max(errors)<.0001
    assert triangles==record['triangles'] and triangles<15000
    assert all(len(ob.data.uv_layers)==1 for ob in meshes)
    assert all(math.isfinite(c) for p in coords for c in p)
    r={'id':record['id'],'sha256_matches':True,'mesh_count':len(meshes),'triangles':triangles,'triangle_count_matches':True,'max_bounds_error_m':max(errors),'positive_determinants':True,'uv_layers':1,'bounds_roundtrip_unity_m':actual}
    if record['id']=='CollectorBody':
        assert actual['min'][0]>-.20 and actual['max'][0]<.20
        assert actual['min'][1]>-.26 and actual['max'][1]<.17
        assert actual['min'][2]>-.20 and actual['max'][2]<.50
        probes=coords+face_samples
        checks=[]
        for name,cy,radius,hh in [('base',.02,.1,.11),('upgraded',.067,.135,.1628)]:
            inside=[p for p in probes if cy-hh+.0001<p[1]<cy+hh-.0001 and p[0]**2+(p[2]-.025)**2<(radius-.0001)**2]
            assert not inside,(name,inside[:5])
            checks.append({'size':name,'glass_center_tool_local':[0,cy,.025],'glass_radius_m':radius,'glass_half_height_m':hh,'mesh_vertex_edge_midpoint_face_center_probes':len(probes),'probes_inside_open_glass':0})
        r['body_clearance_sampling']=checks
        r['muzzle_z_extents_m']=[.451,.495]
        r['effect_center_m']=[0,.05,.489]
    else:
        assert actual['min'][1]>.116 and actual['max'][1]<.179
        r['base_tool_root_bounds_y_m']=[.02+actual['min'][1],.02+actual['max'][1]]
        r['upgraded_tool_root_bounds_y_m']=[.067+1.48*actual['min'][1],.067+1.48*actual['max'][1]]
        r['intentional_overlap']='Lower lid gasket joins the preserved top reservoir seal; no hidden upright pump or independent offset.'
    results.append(r)
receipt={'blender':bpy.app.version_string,'passed':True,'kind':'Local FBX export/reimport plus bounds and sampled open-reservoir exclusion; not Unity import/runtime evidence','assets':results,'limits':['Vertex/edge-midpoint/face-center exclusion is a sampled geometry audit, not exhaustive triangle-cylinder collision proof.','Source studio render reviewed separately; player visibility/effect alignment and actual Unity import remain root integration.']}
(folder/'fbx_roundtrip_validation.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print('COLLECTOR_FBX_ROUNDTRIP_PASS '+json.dumps(results))
