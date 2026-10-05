"""Original Evolution Shop art kit. Blender 5.0, no external assets or add-ons.

Run: blender --background --factory-startup --python create_collector_kit.py -- --repo C:/dev/DumbVideoGameStudio
Authoring helpers take Unity X/right, Y/up, Z/back coordinates and convert them
to Blender X/right, -Y/back, Z/up. FBX exports bake the Y-up conversion.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--repo', required=True)
parser.add_argument('--only-export', nargs='*', help='Rebuild only these FBX files; an empty list refreshes source/render/manifest without rewriting admitted FBX bytes. All Unity metadata is preserved.')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
REPO = Path(args.repo).resolve()
assert (REPO / 'EvolutionShop/ProjectSettings/ProjectVersion.txt').is_file()
SOURCE = REPO / 'ArtSource/CollectorKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/CollectorKit'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0
# Keep reruns deterministic without accumulating save-version copies; this
# runtime preference is never written to the user's Blender preferences.
bpy.context.preferences.filepaths.save_version = 0

PALETTE = {
    'RW_Oak': ('754426', .55, 0), 'RW_Honey': ('C98B46', .42, 0),
    'RW_PaleOak': ('E7B777', .44, 0), 'RW_Teal': ('246F68', .3, 0),
    'RW_Mint': ('8CCCB0', .3, 0), 'RW_Cream': ('F5E5BD', .4, 0),
    'RW_Coral': ('ED795E', .3, 0), 'RW_Brass': ('D5AA4D', .3, .5),
    'RW_Ink': ('253C3E', .5, 0), 'RW_Leaf': ('538A4C', .5, 0),
    'RW_Sky': ('8CCCD4', .28, 0),
}
MATERIALS = {}
def srgb_linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4
for name, (code, roughness, metallic) in PALETTE.items():
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    rgb = [int(code[i:i+2], 16) / 255 for i in (0, 2, 4)]
    color = (*[srgb_linear(v) for v in rgb], 1)
    mat.diffuse_color = color
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = color
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Metallic'].default_value = metallic
    MATERIALS[name] = mat

def u(p):
    return Vector((p[0], -p[2], p[1]))

def un(p):
    return (p[0], p[2], -p[1])

active_parts = []
ASSETS = {}
RECORDS = []

def register(obj, name, material, smooth=False):
    obj.name = name
    obj.data.materials.append(MATERIALS[material])
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    active_parts.append(obj)
    return obj

def bevel(obj, width=.025, segments=3):
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new('Soft authored edge', 'BEVEL')
    mod.width, mod.segments = width, segments
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    norm = obj.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL')
    norm.keep_sharp = True
    norm.weight = 50
    bpy.ops.object.modifier_apply(modifier=norm.name)

def box(name, p, size, material, radius=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=u(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    register(obj, name, material)
    if radius:
        bevel(obj, min(radius, min(size) * .45))
    return obj

def ellipsoid(name, p, size, material, segments=20, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=.5, location=u(p))
    obj = bpy.context.object
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return register(obj, name, material, True)

def mesh(name, verts, faces, material, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata([u(p) for p in verts], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return register(obj, name, material, smooth)

def lathe(name, p, profile, material, segments=24, ellipse=(1,1)):
    # Profiles proceed bottom to top, as (radius, height).
    verts = [(p[0] + r * math.cos(t*math.tau/segments)*ellipse[0], p[1] + h,
              p[2] + r * math.sin(t*math.tau/segments)*ellipse[1])
             for r,h in profile for t in range(segments)]
    faces = []
    for row in range(len(profile)-1):
        for i in range(segments):
            j = (i+1)%segments
            faces.append((row*segments+i, (row+1)*segments+i, (row+1)*segments+j, row*segments+j))
    faces.append(tuple(range(segments-1,-1,-1)))
    faces.append(tuple((len(profile)-1)*segments+i for i in range(segments)))
    obj = mesh(name, verts, faces, material, True)
    # Normals are set explicitly by consistent winding in a later mesh pass.
    return obj

def tube(name, points, radius, material, sides=8):
    verts=[]
    for idx, p in enumerate(points):
        center=Vector(p)
        tangent=Vector(points[min(idx+1,len(points)-1)])-Vector(points[max(idx-1,0)])
        tangent.normalize()
        side=tangent.cross(Vector((0,1,0)))
        if side.length < .01: side=tangent.cross(Vector((1,0,0)))
        side.normalize()
        other=tangent.cross(side).normalized()
        for i in range(sides):
            v=center+radius*(math.cos(i*math.tau/sides)*side+math.sin(i*math.tau/sides)*other)
            verts.append(tuple(v))
    faces=[]
    for row in range(len(points)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    faces += [tuple(range(sides-1,-1,-1)), tuple((len(points)-1)*sides+i for i in range(sides))]
    return mesh(name, verts, faces, material, True)

def arc(name, center, rx, ry, start, end, material, radius=.014, steps=15):
    pts=[(center[0]+rx*math.cos(start+(end-start)*i/steps),
          center[1]+ry*math.sin(start+(end-start)*i/steps),center[2]) for i in range(steps+1)]
    return tube(name, pts, radius, material)

def disk(name, p, radius, thickness, material, segments=24):
    # Front-facing round dial on the X/Y plane, outward toward local -Z.
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius, depth=thickness, location=u(p), rotation=(math.pi/2,0,0))
    obj=register(bpy.context.object,name,material,True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel(obj,min(.007,thickness*.22),2)
    return obj

def gauge(p, r=.13):
    disk('Brass dial rim', p, r, .035, 'RW_Brass')
    disk('Cream dial face', (p[0],p[1],p[2]-.022),r*.79,.014,'RW_Cream')
    arc('Friendly dial sweep',(p[0],p[1],p[2]-.033),r*.56,r*.56,.3,math.pi-.3,'RW_Teal',r*.048,10)
    tube('Coral gauge needle',[(p[0],p[1],p[2]-.038),(p[0]+r*.28,p[1]+r*.42,p[2]-.038)],r*.07,'RW_Coral')
    disk('Needle hub',(p[0],p[1],p[2]-.045),r*.12,.012,'RW_Ink',16)

def scallop(name, width, bottom, top, z, thickness, material, lobes=7):
    pts=[(-width/2,top),(width/2,top)]
    for i in range(lobes*10,-1,-1):
        x=-width/2+width*i/(lobes*10)
        y=bottom+.037*math.cos(i/(lobes*10)*lobes*math.tau)
        pts.append((x,y))
    count=len(pts)
    verts=[(x,y,z+d) for d in [-thickness/2,thickness/2] for x,y in pts]
    faces=[tuple(range(count-1,-1,-1)),tuple(range(count,count*2))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    obj=mesh(name,verts,faces,material)
    bevel(obj,.012,2)

def foot(x,z,height=.23):
    lathe('Turned oak foot',(x,0,z),[(.08,0),(.095,.025),(.08,.055),(.067,.075),(.088,.10),(.095,.14),(.065,.18),(.075,height)],'RW_Oak',20)
    lathe('Honey foot collar',(x,0,z),[(.08,height-.06),(.087,height-.04),(.08,height-.012)],'RW_Honey',20)

def handle(x,y,z,width=.20):
    for dx in [-width/2,width/2]: disk('Handle rosette',(x+dx,y,z+.008),.026,.022,'RW_Brass',16)
    tube('Bow handle',[(x-width/2,y,z),(x-width*.45,y+.025,z-.023),(x,y+.043,z-.038),(x+width*.45,y+.025,z-.023),(x+width/2,y,z)],.016,'RW_Brass')

def finish(name, description):
    global active_parts
    bpy.ops.object.select_all(action='DESELECT')
    for obj in active_parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=active_parts[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name+'_Visual'
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Recalculate outward normals on every disconnected solid, then provide one
    # usable UV set even though this palette kit requires no texture files.
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    tri=obj.modifiers.new('Export triangulation','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=tri.name)
    root=bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(root)
    obj.parent=root
    root['origin']='Original authored Evolution Shop kit; no external source assets'
    root['unity_front']='+Z suction muzzle; rear readout faces -Z'
    root['unity_pivot']='ToolBody identity for CollectorBody; ToolChamber identity for CollectorCap'
    coords=[un(obj.matrix_world@v.co) for v in obj.data.vertices]
    mins=[min(v[i] for v in coords) for i in range(3)]
    maxs=[max(v[i] for v in coords) for i in range(3)]
    record={'id':name,'description':description,'triangles':len(obj.data.polygons),
            'vertices':len(obj.data.vertices),'bounds_unity_m':{'min':mins,'max':maxs,'size':[maxs[i]-mins[i] for i in range(3)]},
            'material_slots':[m.name for m in obj.data.materials], 'uv_layers':[l.name for l in obj.data.uv_layers],
            'mirrored':False,'negative_scale':False,'root_scale':[1,1,1],'colliders':0,
            'export':str((EXPORT/(name+'.fbx')).relative_to(REPO)).replace('\\','/')}
    assert record['triangles']<=15000, record
    assert all(math.isfinite(c) for v in coords for c in v)
    RECORDS.append(record)
    ASSETS[name]=(root,obj)
    active_parts=[]
    return root

# Original playful horizontal culture vacuum. All helpers are copied from this
# project's first original authored kit, with no external geometry or textures.
# The glass, culture cells, capacity band, and collection light remain runtime-owned.
def axial_shell(name, profile, material, segments=32, ex=1.0, ey=1.0, center=(0,.05)):
    # Closed profiles describe a hollow Z-facing shell (radius,z). The open
    # mouth is actual geometry, not a dark disk over an opaque barrel.
    vs=[(center[0]+r*math.cos(i*math.tau/segments)*ex,
         center[1]+r*math.sin(i*math.tau/segments)*ey,z)
        for r,z in profile for i in range(segments)]
    fs=[]
    for row in range(len(profile)):
        nextrow=(row+1)%len(profile)
        for i in range(segments):
            j=(i+1)%segments
            fs.append((row*segments+i,row*segments+j,nextrow*segments+j,nextrow*segments+i))
    return mesh(name,vs,fs,material,True)

# Rounded motor body has a broad sideways silhouette in front of the canister.
# Its nearest point is Z .181: 21mm beyond the upgraded glass front at Z .160.
box('Cream forward motor hood',(0,.044,.261),(.214,.177,.160),'RW_Cream',.060)
box('Teal motor lower shell',(0,-.013,.270),(.218,.074,.181),'RW_Teal',.035)
axial_shell('Mint motor belt',[(.080,.287),(.090,.294),(.090,.312),(.080,.319),(.070,.313),(.070,.294)],'RW_Mint',32,1.15,.94)
axial_shell('Cream barrel throat',[(.073,.305),(.077,.318),(.074,.367),(.061,.376),(.046,.370),(.046,.309)],'RW_Cream',32,1.12,.96)
axial_shell('Teal suction bell',[(.059,.345),(.070,.371),(.091,.406),(.111,.450),(.113,.469),(.093,.468),(.074,.414),(.046,.367)],'RW_Teal',32,1.12,.82)
axial_shell('Broad coral muzzle lip',[(.108,.451),(.123,.462),(.127,.476),(.124,.489),(.117,.495),(.095,.493),(.091,.482),(.093,.466)],'RW_Coral',40,1.12,.82)
axial_shell('Cream inner mouth highlight',[(.097,.469),(.100,.476),(.100,.484),(.094,.486),(.090,.480)],'RW_Cream',32,1.12,.82)
# Three broad fin shapes frame the barrel without creating a realistic weapon.
for side in [-1,1]:
    fin=mesh('Coral curved side fin',[(side*.072,.081,.251),(side*.119,.114,.259),
        (side*.170,.107,.320),(side*.173,.067,.363),(side*.123,.061,.356),
        (side*.080,.057,.321),(side*.072,.065,.251),(side*.119,.098,.259),
        (side*.170,.091,.320),(side*.173,.051,.363),(side*.123,.045,.356),(side*.080,.041,.321)],
        [(0,1,2,3,4,5),(11,10,9,8,7,6)]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)],'RW_Coral')
    bevel(fin,.007,3)
box('Mint dorsal motor crest',(0,.132,.258),(.038,.065,.096),'RW_Mint',.018)
# Rear-side cooling slots are substantial enough to read at hand scale.
for side in [-1,1]:
    for z in [.219,.246,.273]:
        box('Teal side motor vent',(side*.105,.047,z),(.012,.045,.012),'RW_Teal',.005)
    ellipsoid('Brass side fastener',(side*.112,-.006,.315),(.017,.020,.020),'RW_Brass',12,6)
# The low fixed cradle intersects only the bottom solid cap, not live glass.
box('Low canister cradle',(0,-.130,.035),(.076,.052,.215),'RW_Teal',.019)
tube('Swept front cradle',[(0,-.131,.102),(0,-.125,.178),(0,-.068,.221),(0,-.021,.229)],.023,'RW_Teal',12)
# Stout angled comfort grip, not the original syringe shaft.
grip=box('Angled mint comfort grip',(0,-.192,-.080),(.092,.135,.087),'RW_Mint',.039)
grip.rotation_euler[0]=math.radians(-22)
box('Coral grip heel',(0,-.244,-.101),(.101,.029,.094),'RW_Coral',.013)
for y,z in [(-.182,-.118),(-.205,-.127),(-.229,-.136)]:
    box('Grip rear rubber rib',(0,y,z),(.077,.012,.020),'RW_Teal',.005)
# A softly curved thumb/trigger lever completes the toy-vacuum shape; cosmetic.
tube('Coral squeeze lever',[(.048,-.133,-.009),(.061,-.146,.001),(.067,-.181,-.010),(.061,-.207,-.031)],.014,'RW_Coral',10)
# Readout deliberately below the vessel, leaving its rear face unobstructed.
box('Cream rear readout bezel',(0,-.130,-.152),(.186,.072,.033),'RW_Cream',.022)
box('Teal rear readout inset',(0,-.129,-.172),(.153,.044,.012),'RW_Teal',.016)
for x,mat in [(-.079,'RW_Coral'),(.079,'RW_Brass')]:
    disk('Readout bezel screw',(x,-.131,-.175),.008,.008,mat,12)
finish('CollectorBody','Original compact retro horizontal culture vacuum: rounded cream/teal motor, open coral suction bell, wide coral fins, angled mint comfort grip and low rear readout frame. Reservoir space remains open.')
RECORDS[-1]['attachment']={'parent':'Existing ToolBody','local_position':[0,0,0],'local_rotation_euler':[0,0,0],'local_scale':[1,1,1]}
RECORDS[-1]['muzzle_effect_center_tool_local']=[0,.05,.489]
RECORDS[-1]['readout_text_center_tool_local']=[0,-.129,-.182]
RECORDS[-1]['readout_text_max_width_m']=.135
RECORDS[-1]['readout_text_height_m']=.030
RECORDS[-1]['forward_motor_min_z_m']=.181
RECORDS[-1]['upgraded_glass_max_z_m']=.160

# Low saucer lid follows the existing chamber scale and eliminates the tall
# plunger/pump silhouette. Kept directly chamber-local, no manual upgrade offset.
lathe('Teal canister lid gasket',(0,0,0),[(.107,.117),(.117,.123),(.119,.132),(.111,.139)],'RW_Teal',32)
lathe('Cream saucer cap',(0,0,0),[(.114,.130),(.121,.137),(.117,.146),(.099,.155),(.054,.159),(.012,.160)],'RW_Cream',32)
box('Mint low twist handle',(0,.165,0),(.102,.026,.043),'RW_Mint',.013)
for x in [-.074,.074]:
    box('Coral lid key',(x,.147,0),(.024,.018,.060),'RW_Coral',.008)
finish('CollectorCap','Low saucer lid and wide mint twist handle in ToolChamber-local coordinates. Follows existing chamber position/scale; no upright stem or pump bulb.')
RECORDS[-1]['attachment']={'parent':'Existing ToolChamber','local_position':[0,0,0],'local_rotation_euler':[0,0,0],'local_scale':[1,1,1],'upgrade_rule':'Inherit established ToolChamber position and positive scale; no independent animation or translation.'}

for name,(root,obj) in ASSETS.items():
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True); obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    dest=EXPORT/(name+'.fbx')
    if args.only_export is None or name in args.only_export:
        bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,global_scale=1.0,
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
            use_space_transform=True,bake_space_transform=True,object_types={'MESH','EMPTY'},
            use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False)
    record=next(r for r in RECORDS if r['id']==name)
    record['sha256']=hashlib.sha256(dest.read_bytes()).hexdigest(); record['bytes']=dest.stat().st_size

# Ghost canister proxies below are REVIEW-ONLY, named distinctly and never
# exported. They show both preserved sizes against the new source geometry.
glassmat=bpy.data.materials.new('Review_only_reservoir_glass'); glassmat.use_nodes=True
g=glassmat.node_tree.nodes.get('Principled BSDF')
g.inputs['Base Color'].default_value=(.55,.95,.89,1)
g.inputs['Transmission Weight'].default_value=.88
g.inputs['Roughness'].default_value=.12
g.inputs['IOR'].default_value=1.38
def preview_canister(offset, upgraded=False):
    scale=(1.35,1.48,1.35) if upgraded else (1,1,1)
    center=(offset[0],offset[1]+(.067 if upgraded else .02),offset[2]+.025)
    mat=MATERIALS.get('REVIEW_GLASS'); MATERIALS['REVIEW_GLASS']=glassmat
    can=lathe('REVIEW_ONLY glass proxy',center,[(.1*scale[0],-.11*scale[1]),(.1*scale[0],.11*scale[1])],'REVIEW_GLASS',48)
    # Sidewall shell transmits the review cells; flat end caps are unnecessary.
    solid=can.modifiers.new('Review vessel thin glass','SOLIDIFY'); solid.thickness=.002
    for y,radius,height,color in [(-.115,.115,.019,'RW_Teal'),(.115,.115,.025,'RW_Cream')]:
        lathe('REVIEW_ONLY preserved reservoir rim',center,[(radius*scale[0],(y-height)*scale[1]),(radius*scale[0],(y+height)*scale[1])],color,32)
    for i in range(4 if not upgraded else 8):
        angle=i*2.399
        ellipsoid('REVIEW_ONLY culture cell',(center[0]+math.cos(angle)*.054*scale[0],center[1]+((i%4)-1.5)*.037*scale[1],center[2]+math.sin(angle)*.050*scale[0]),(.028,.030,.024),'RW_Coral' if i%2 else 'RW_Mint',16,8)
    return center,scale

for idx,(upgraded,offset) in enumerate([(False,(-.40,0,0)),(True,(.40,0,0))]):
    for name,(root,obj) in list(ASSETS.items()):
        if idx:
            displayroot=root.copy(); bpy.context.collection.objects.link(displayroot)
            displayobj=obj.copy(); displayobj.data=obj.data.copy(); bpy.context.collection.objects.link(displayobj); displayobj.parent=displayroot
            displayroot.name=name+'_UpgradedReview'; displayobj.name=name+'_UpgradedReviewMesh'
        else: displayroot=root
        if name=='CollectorBody': displayroot.location=u(offset)
        else:
            displayroot.location=u((offset[0],offset[1]+(.067 if upgraded else .02),offset[2]+.025))
            # Blender axes: X/right, Y/depth, Z/up.
            displayroot.scale=(1.35,1.35,1.48) if upgraded else (1,1,1)
    preview_canister(offset,upgraded)
active_parts=[]
box('Review only studio floor',(0,-.276,.10),(3,.018,2),'RW_Cream',.008)
active_parts=[]
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=48; scene.cycles.use_denoising=True
scene.render.resolution_x=1800; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3); scene.view_settings.view_transform='AgX'; scene.render.image_settings.file_format='PNG'
def area(name,pos,power,size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    ob=bpy.data.objects.new(name,data); scene.collection.objects.link(ob); ob.location=u(pos)
    ob.rotation_euler=(u((0,0,.08))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Large soft key',(-1.4,2,-2),190,2)
area('Mint-side fill',(2,1.1,.7),130,1.5)
area('Warm rear rim',(-1,1.4,1.5),180,1.2)
camdata=bpy.data.cameras.new('Collector review camera'); cam=bpy.data.objects.new('Collector review camera',camdata)
scene.collection.objects.link(cam); camdata.type='ORTHO'; camdata.ortho_scale=1.66; scene.camera=cam
cam.location=u((1.8,1.15,-2.1)); cam.rotation_euler=(u((0,-.02,.12))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SOURCE/'CollectorKit_ContactSheet.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'EvolutionShop_CollectorKit.blend'))
bpy.ops.render.render(write_still=True)
cam.location=u((1.7,.9,2.2)); cam.rotation_euler=(u((0,-.02,.12))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SOURCE/'CollectorKit_MuzzleReview.png'); bpy.ops.render.render(write_still=True)
blend=SOURCE/'EvolutionShop_CollectorKit.blend'
manifest={'schema':1,'title':'Evolution Shop original retro culture vacuum',
    'origin':{'author':'Original local Blender authorship by Codex for Evolution Shop','external_sources':[],
              'license':'Original project work; no third-party source or asset license','paid_generation':False,'external_uploads':False},
    'blender_version':bpy.app.version_string,'source_script':'Tools/Art/create_collector_kit.py',
    'source_script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'blend_source':str(blend.relative_to(REPO)).replace('\\','/'),'blend_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),
    'scale':'1 unit = 1 metre','up':'Unity +Y','front':'Suction muzzle +Z; rear readout -Z',
    'export_settings':{'format':'FBX','forward':'-Z','up':'Y','global_scale':1,'apply_unit_scale':True,'apply_scale_options':'FBX_SCALE_UNITS','bake_space_transform':True,'animations':False},
    'unity_import_expected':{'globalScale':1,'useFileScale':True,'importAnimation':False,'addCollider':False,'material_mapping':'Root remaps existing RW_* names to shared owned URP/Lit palette'},
    'materials':{name:{'srgb_hex':'#'+d[0],'roughness':d[1],'metallic':d[2]} for name,d in PALETTE.items()},
    'materials_exception':'Shared opaque palette slots preserve original flat-color separation without textures. No glass imported. Runtime owns transparent vessel.',
    'assets':RECORDS,'renders':['ArtSource/CollectorKit/CollectorKit_ContactSheet.png','ArtSource/CollectorKit/CollectorKit_MuzzleReview.png'],
    'integration':{'remove_old':'Siphon, SiphonCage, SiphonCrown authored visuals and old grip/base/barrel/nozzle collar/readout panel; keep runtime glass/rims/cells/band/effect light.',
       'readout':'Move existing live text to (0,-.129,-.182), max width .135, full height .030. Model has an inset, no baked text.',
       'base_chamber':{'position':[0,.02,.025],'scale':[1,1,1]},'upgraded_chamber':{'position':[0,.067,.025],'scale':[1.35,1.48,1.35]},
       'cap':'Parent CollectorCap directly to ToolChamber at identity; no manual translation.',
       'muzzle_effect':'Keep existing nozzle effect center (0,.05,.489). The mouth is hollow; existing light remains visible.'},
    'validation_limits':['Blender contact views are source reviews, not in-game captures or Unity import evidence.',
        'Both review assemblies include non-exported glass/rim/culture proxies; these remain runtime-owned.',
        'Actual first-person camera, cap fit, effect alignment, upgrade motion and readout legibility must pass root integration.',
        'No LOD, collision or performance claim. Editable blend displays source meshes at review offsets; generator exports at the documented origins.']}
(SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('COLLECTOR_KIT_COMPLETE '+json.dumps([{'id':r['id'],'triangles':r['triangles'],'bounds':r['bounds_unity_m']} for r in RECORDS]))
