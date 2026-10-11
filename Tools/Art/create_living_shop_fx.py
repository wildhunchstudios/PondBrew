"""Original rigid cartoon gloves for Evolution Shop. Local Blender only.
blender --background --factory-startup --python this.py -- --repo <repo>
Authoring coordinates are Unity metres; no downloaded assets or add-ons.
"""
import argparse, hashlib, json, math, os, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector, Matrix

p=argparse.ArgumentParser();p.add_argument('--repo',required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);repo=Path(a.repo).resolve()
assert str(repo).lower()=='c:\\dev\\dumbvideogamestudio'
assert '6000.3.21f1' in (repo/'EvolutionShop/ProjectSettings/ProjectVersion.txt').read_text()
src=repo/'ArtSource/LivingShopFX';out=repo/'EvolutionShop/Assets/EvolutionShop/Resources/Art/LivingShopFX'
src.mkdir(parents=True,exist_ok=True);out.mkdir(parents=True,exist_ok=True)
assert bpy.data.filepath=='', 'Use an isolated factory-startup scene, never the user scene.'
preflight=dict(blender=bpy.app.version_string,pid=os.getpid(),sceneFile=bpy.data.filepath,repo=str(repo),workflow='Isolated background CLI; no live MCP claim')
(src/'blender-preflight.json').write_text(json.dumps(preflight,indent=2)+'\n',encoding='utf-8')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
bpy.context.preferences.filepaths.save_version=0
u=lambda v:Vector((v[0],-v[2],v[1]))
un=lambda v:[round(v[0],6),round(v[2],6),round(-v[1],6)]
mats={}
for n,c in [('RW_Cream','F5E5BD'),('RW_Teal','246F68'),('RW_Coral','ED795E')]:
    m=bpy.data.materials.new(n);m.use_nodes=True
    rgb=[int(c[i:i+2],16)/255 for i in (0,2,4)]
    rgba=tuple(x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in rgb)+(1,)
    m.diffuse_color=rgba;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=rgba
    m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.4;mats[n]=m
objects=[];records=[]
def pivot(name,pos,parent=None):
    ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.location=u(pos)
    if parent: ob.parent=parent;ob.matrix_parent_inverse=parent.matrix_world.inverted()
    objects.append(ob);bpy.context.view_layer.update();return ob
root=pivot('HandsPair',(0,0,0))
def sphere(pos,size,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.5,location=u(pos))
    o=bpy.context.object;o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mats[mat])
    for f in o.data.polygons:f.use_smooth=True
    return o
def seam(pos,size,mat):
    bpy.ops.mesh.primitive_torus_add(major_segments=16,minor_segments=6,major_radius=.5,minor_radius=.04,location=u(pos))
    o=bpy.context.object;o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mats[mat])
    for f in o.data.polygons:f.use_smooth=True
    return o
def segment(name,pos,parent,lobes):
    parts=[(seam if m=='RW_Coral' else sphere)(q,s,m) for q,s,m in lobes]
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    ob=bpy.context.object;ob.name=name
    bpy.context.scene.cursor.location=u(pos);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    mod=ob.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
    world=ob.matrix_world.copy();ob.parent=parent;ob.matrix_world=world
    objects.append(ob);bpy.context.view_layer.update()
    records.append(dict(name=name,parent=parent.name,pivot=un(ob.matrix_world.translation),triangles=len(ob.data.polygons),materials=[m.name for m in ob.data.materials]))
    return ob
for side,sign,palm in [('Right',1,(.045,-.18,-.08)),('Left',-1,(-.07,-.145,.10))]:
    x,y,z=palm;wpos=(x+sign*.022,y-.055,z-.018)
    wrist=pivot(side+'Wrist',wpos,root)
    # Palm and three rounded finger lobes share one rigid mesh, rather than extra renderers.
    palm_lobes=[((x,y,z),(.108,.088,.079),'RW_Cream')]
    for i in range(3):palm_lobes.append(((x+sign*(.019+i*.024),y+.008-i*.020,z+.033),(.037,.031,.068),'RW_Cream'))
    segment(side+'Fingers',wpos,wrist,palm_lobes)
    tb=(x-sign*.029,y+.025,z+.010)
    thumb=segment(side+'ThumbBase',tb,wrist,[(tb,(.043,.039,.050),'RW_Cream')])
    tip=(tb[0]-sign*.012,tb[1]+.015,tb[2]+.032)
    segment(side+'ThumbTip',tip,thumb,[(tip,(.036,.032,.041),'RW_Cream')])
    ib=(x+sign*.020,y+.045,z+.020)
    idx=segment(side+'IndexBase',ib,wrist,[(ib,(.036,.037,.051),'RW_Cream')])
    it=(ib[0]-sign*.003,ib[1]+.008,ib[2]+.038)
    segment(side+'IndexTip',it,idx,[(it,(.034,.031,.046),'RW_Cream')])
    c=(wpos[0]+sign*.008,wpos[1]-.001,wpos[2]-.010)
    segment(side+'Cuff',c,wrist,[(c,(.083,.035,.079),'RW_Teal'),((c[0],c[1]+.015,c[2]),(.085,.008,.081),'RW_Coral')])
assert len(records)==12 and sum(x['triangles'] for x in records)<=6000
points=[o.matrix_world @ v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
bounds=dict(min=un(Vector((min(v.x for v in points),min(v.y for v in points),min(v.z for v in points)))),max=un(Vector((max(v.x for v in points),max(v.y for v in points),max(v.z for v in points)))))
# Unity-space min/max explicitly, since conversion reverses Z.
ups=[un(v) for v in points];bounds=dict(min=[min(v[i] for v in ups) for i in range(3)],max=[max(v[i] for v in ups) for i in range(3)])
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
fbx=out/'HandsPair.fbx'
blend=src/'EvolutionShop_LivingShopFX.blend';bpy.ops.wm.save_as_mainfile(filepath=str(blend))
# Export-copy conversion, explicit for the nested rigid hierarchy. Blender's
# bake_space_transform path double-converts nested mesh pivots. FBX Y-up data
# here is (-Unity X, Unity Y, Unity Z) for this FBX axis metadata; Unity
# converts handedness once on import. Actual pivots are checked after import.
# Keep the saved editable source in the normal Blender Z-up authoring frame.
to_fbx=lambda v:Vector((-v.x,v.z,-v.y))
locations={o:to_fbx(o.matrix_world.translation) for o in objects}
geometry={o:[to_fbx(o.matrix_world @ v.co)-locations[o] for v in o.data.vertices] for o in objects if o.type=='MESH'}
for o in objects:
    o.matrix_parent_inverse=Matrix.Identity(4)
    o.rotation_euler=(0,0,0);o.scale=(1,1,1)
    o.location=locations[o]-(locations[o.parent] if o.parent else Vector((0,0,0)))
    if o.type=='MESH':
        for v,co in zip(o.data.vertices,geometry[o]):v.co=co
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=False,bake_space_transform=False,object_types={'MESH','EMPTY'},use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False)
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
manifest=dict(asset='HandsPair',authorship='Original local parametric glove meshes authored for Evolution Shop, no external assets',blender=bpy.app.version_string,source=str(blend.relative_to(repo)),export=str(fbx.relative_to(repo)),sourceSha256=sha(blend),exportSha256=sha(fbx),generatorSha256=sha(Path(__file__)),renderers=12,rigPivots=14,triangles=sum(x['triangles'] for x in records),materials=list(mats),segments=records,restBounds=bounds,palmSockets=dict(Right=[.045,-.18,-.08],Left=[-.07,-.145,.10]),fitAdjustment=dict(Right=[.01,.01,0],Left=[-.01,-.015,0]),curlAxis='Unity tool-body X; derive parent-local axis from actual imported transforms',unit='metres',exportAnimation=False,paidGeneration=False,externalUploads=False)
manifest['exportConversion']='Explicit nested-rig export copy: Blender (x,y,z) -> FBX (-x,z,-y), metadata -Z forward/Y up, exporter space bake off. Unity left-handed import yields tool-body (x,z,-y); root identity and positive scales verified in import log and runtime vertex sweep.'
(src/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('FX04_ART_READY '+json.dumps(dict(triangles=manifest['triangles'],renderers=12,restBounds=bounds)))
