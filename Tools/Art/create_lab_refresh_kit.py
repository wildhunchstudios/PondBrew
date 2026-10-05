"""Original Evolution Shop art kit. Blender 5.0, no external assets or add-ons.

Run: blender --background --factory-startup --python create_lab_refresh_kit.py -- --repo C:/dev/DumbVideoGameStudio
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
SOURCE = REPO / 'ArtSource/LabRefreshKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/LabRefreshKit'
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
        bevel(obj, min(radius, min(size) * .45), 2 if min(size)<.04 else 3)
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
    previous_side=None
    for idx, p in enumerate(points):
        center=Vector(p)
        tangent=Vector(points[min(idx+1,len(points)-1)])-Vector(points[max(idx-1,0)])
        tangent.normalize()
        if previous_side is None:
            reference=min([Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))],key=lambda v: abs(v.dot(tangent)))
            side=tangent.cross(reference)
        else:
            # Parallel transport the frame through bends. Rebuilding a frame
            # from a fixed up vector flips rings when a C-shaped arm turns.
            side=previous_side-tangent*previous_side.dot(tangent)
            if side.length<.001:
                reference=min([Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))],key=lambda v: abs(v.dot(tangent)))
                side=tangent.cross(reference)
        side.normalize()
        previous_side=side.copy()
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
    root['unity_front']='-Z'
    root['unity_pivot']='floor centre at (0,0,0); front is Unity -Z'
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

def oval_ring(name, p, outer, inner, depth, material, segments=48, edge=.012):
    # True open annulus: no opaque polygon crosses its central opening.
    verts=[]
    for z,rx,ry in [(p[2]-depth/2,*outer),(p[2]-depth/2,*inner),
                     (p[2]+depth/2,*outer),(p[2]+depth/2,*inner)]:
        verts.extend((p[0]+rx*math.cos(i*math.tau/segments),p[1]+ry*math.sin(i*math.tau/segments),z)
                     for i in range(segments))
    faces=[]
    for i in range(segments):
        j=(i+1)%segments
        faces.extend([(i,j,segments+j,segments+i),
                      (2*segments+i,3*segments+i,3*segments+j,2*segments+j),
                      (i,2*segments+i,2*segments+j,j),
                      (segments+i,segments+j,3*segments+j,3*segments+i)])
    obj=mesh(name,verts,faces,material,True)
    if edge: bevel(obj,edge,2)
    return obj

def closed_oval_path(cx,cy,z,rx,ry,steps=40):
    return [(cx+rx*math.cos(i*math.tau/steps),cy+ry*math.sin(i*math.tau/steps),z) for i in range(steps+1)]

def lamp_point(name, p, size, material):
    ellipsoid(name,p,size,material,16,8)

# RESEARCH: a compact vintage scientific desk, with a protected central Spark
# chamber and two deliberately different instrument silhouettes either side.
for x in [-.64,.64]:
    for z in [-.40,.40]:
        lathe('Squat scientific desk foot',(x,0,z),[(.065,0),(.085,.025),(.08,.08),(.065,.17),(.074,.23)],'RW_Teal',20)
box('Desk lower honey plinth',(0,.234,0),(1.57,.12,1.01),'RW_Honey',.046)
box('Cream desk cabinet',(0,.610,0),(1.58,.68,1.04),'RW_Cream',.11)
box('Mint front inset',(0,.622,-.514),(1.43,.55,.057),'RW_Mint',.062)
for side in [-1,1]:
    for y in [.423,.658,.865]:
        box('Rounded little drawer',(side*.535,y,-.547),(.332,.172,.042),'RW_Cream',.035)
        handle(side*.535,y+.008,-.544,.13)
box('Teal center cabinet door',(0,.605,-.556),(.64,.532,.046),'RW_Teal',.06)
box('Blank front research placard',(0,.65,-.583),(.56,.198,.018),'RW_Cream',.034)
for x in [-.274,.274]: disk('Placard pin',(x,.65,-.596),.012,.005,'RW_Brass',12)
box('Honey desk bullnose',(0,1.020,0),(1.70,.13,1.20),'RW_Honey',.055)
box('Pale worktop inset',(0,1.086,0),(1.61,.006,1.115),'RW_PaleOak',.002)
box('Teal under-slab reveal',(0,.955,0),(1.645,.039,1.15),'RW_Teal',.016)
desktop_first_part=len(active_parts)

# Original compact CRT. All opaque components stay right of the chamber.
box('CRT pedestal',(.565,1.148,.15),(.34,.09,.27),'RW_Teal',.038)
box('CRT rounded cream body',(.565,1.435,.18),(.48,.49,.43),'RW_Cream',.092)
box('CRT teal screen surround',(.565,1.484,-.037),(.412,.335,.045),'RW_Teal',.066)
box('CRT dark curved screen',(.565,1.49,-.063),(.345,.264,.021),'RW_Ink',.055)
oval_ring('CRT orbital trace',(.565,1.49,-.078),(.121,.061),(.114,.054),.003,'RW_Mint',32,0)
for x,y,size,mat in [(.481,1.535,.038,'RW_Coral'),(.649,1.445,.030,'RW_Brass'),(.565,1.49,.034,'RW_Sky')]:
    disk('Original science point',(x,y,-.083),size/2,.007,mat,16)
tube('CRT original waveform',[(.426,1.435,-.080),(.465,1.435,-.080),(.498,1.47,-.080),(.524,1.428,-.080),(.552,1.448,-.080),(.617,1.448,-.080)],.0045,'RW_Sky',6)
disk('CRT power dial',(.704,1.303,-.060),.030,.026,'RW_Brass',20)
disk('CRT coral power dot',(.408,1.305,-.060),.016,.013,'RW_Coral',16)
for x in [.475,.515,.555,.595]: box('CRT speaker notch',(x,1.304,-.052),(.018,.04,.012),'RW_Teal',.006)
box('Keyboard mint housing',(.565,1.14,-.316),(.452,.07,.267),'RW_Mint',.027)
for row in range(2):
    for col in range(5):
        box('Large retro key',(.395+col*.084,1.181,-.380+row*.092),(.061,.018,.063),'RW_Cream' if col<4 else 'RW_Coral',.007)

# Microscope body is a true C silhouette, well clear of the central vessel.
box('Microscope mint foot',(-.559,1.146,.075),(.39,.088,.35),'RW_Mint',.039)
tube('Microscope arched arm',[(-.568,1.171,.204),(-.568,1.286,.269),(-.568,1.490,.282),(-.568,1.604,.218),(-.568,1.626,.088)],.047,'RW_Cream',12)
box('Microscope dark stage',(-.559,1.341,.019),(.32,.027,.26),'RW_Teal',.010)
box('Microscope specimen slide',(-.555,1.36,-.01),(.19,.012,.093),'RW_Sky',.004)
tube('Microscope barrel',[(-.554,1.475,.079),(-.554,1.697,.226)],.052,'RW_Mint',16)
tube('Microscope objective',[(-.554,1.433,.052),(-.554,1.485,.086)],.035,'RW_Brass',16)
tube('Microscope eyepiece',[(-.554,1.693,.226),(-.554,1.768,.276)],.037,'RW_Ink',16)
tube('Microscope eyepiece cream lip',[(-.554,1.757,.269),(-.554,1.784,.287)],.049,'RW_Cream',16)
disk('Microscope focus dial',(-.711,1.46,.003),.055,.035,'RW_Brass',24)
disk('Microscope focus center',(-.711,1.46,-.020),.027,.018,'RW_Teal',16)
tube('Microscope focus axle',[(-.554,1.46,.079),(-.711,1.46,.007)],.019,'RW_Brass',10)
for yy,mat,w in [(1.110,'RW_Teal',.36),(1.181,'RW_Coral',.33)]:
    box('Scientific book cover',(-.548,yy,-.362),(w,.018,.237),mat,.006)
    box('Scientific book pages',(-.548,yy+.032,-.360),(w-.018,.047,.214),'RW_Cream',.008)
    box('Scientific book cover',(-.548,yy+.063,-.362),(w,.016,.237),mat,.006)
    box('Book spine band',(-.548,yy+.035,-.482),(.045,.056,.009),'RW_Brass',.003)
for desktop_part in active_parts[desktop_first_part:]:
    desktop_part.location+=u((0,-.016,0))

# Thin rear stand and a deliberately blank small label above the Spark chamber.
for side in [-1,1]:
    tube('Cream back-stand post',[(side*.77,1.105,.475),(side*.77,1.98,.475),(side*.68,2.124,.475)],.026,'RW_Cream',10)
tube('Back stand bow',[(-.68,2.124,.475),(-.51,2.171,.475),(.51,2.171,.475),(.68,2.124,.475)],.026,'RW_Cream',10)
box('Teal upper label border',(0,2.141,.449),(.86,.118,.06),'RW_Teal',.037)
box('Blank small back label',(0,2.144,.414),(.76,.087,.018),'RW_Cream',.021)
# A coiled cable stays entirely behind the protected chamber cylinder.
points=[]
for i in range(53):
    t=i/52
    points.append((-.35+.70*t,1.112+.018*math.sin(t*math.tau*5),.457+.038*math.cos(t*math.tau*5)))
tube('Original coiled research cable',points,.011,'RW_Teal',6)
finish('ResearchBench','Original cream/mint scientific desk with honey worktop, little drawers/books, left microscope and right CRT with original geometric science graphic. Central Spark chamber stays open and readable.')
RECORDS[-1]['exclusion_contract']={'kind':'vertical cylinder','center_xz':[0,0],'radius':.29,'y_min':1.09,'y_max':2.0,
    'purpose':'Existing functional Spark glass/cells; unobstructed front view between side instruments'}
RECORDS[-1]['label_spaces']=[{'center':[0,.65,-.600],'size':[.56,.198]}, {'center':[0,2.144,.403],'size':[.76,.087]}]

# INCUBATOR: rounded retro hatchery with an open oval porthole, independent
# front/rear surrounds and side pillars, without a fishbowl or an opaque back.
for x in [-.69,.69]:
    for z in [-.48,.48]:
        lathe('Hatchery squat rubber foot',(x,0,z),[(.112,0),(.135,.028),(.128,.11),(.107,.18)],'RW_Teal',24)
box('Hatchery coral bottom bumper',(0,.211,0),(1.84,.14,1.33),'RW_Coral',.06)
box('Hatchery mint base',(0,.558,0),(1.86,.64,1.34),'RW_Mint',.16)
box('Hatchery cream shoulder',(0,.887,0),(1.90,.10,1.38),'RW_Cream',.045)
box('Hatchery lower cream reveal',(0,.303,-.004),(1.865,.032,1.35),'RW_Cream',.014)

# Front ring sits in front of the required cavity (backmost Z=-.525), while
# rear ring sits behind it (frontmost Z=.525). No face fills either aperture.
oval_ring('Egg-shaped cream front surround',(0,1.5,-.620),(.968,.70),(.735,.55),.19,'RW_Cream',56,.020)
oval_ring('Thin mint porthole reveal',(0,1.5,-.708),(.787,.602),(.754,.569),.005,'RW_Mint',48,.002)
oval_ring('Brass porthole bead',(0,1.5,-.711),(.746,.562),(.736,.552),.004,'RW_Brass',48,.001)
oval_ring('Mint rear hatchery arch',(0,1.5,.595),(.935,.669),(.735,.55),.14,'RW_Mint',48,.017)
for side in [-1,1]:
    box('Hatchery side pillar',(side*.864,1.471,.02),(.18,1.035,1.105),'RW_Cream',.078)
    box('Coral hinge shoe',(side*.892,1.50,-.645),(.129,.29,.115),'RW_Coral',.043)
    box('Brass hinge pin',(side*.894,1.50,-.701),(.063,.184,.019),'RW_Brass',.008)
box('Cream crown bridge',(0,2.088,.035),(.715,.166,1.18),'RW_Cream',.077)
box('Mint crown accent',(0,2.179,.033),(.54,.034,.58),'RW_Mint',.015)
for x in [-.17,0,.17]:
    box('Top crown vent',(x,2.199,.027),(.075,.006,.263),'RW_Teal',.002)

# Console does not enter the specimen's Y>=.98 interior.
box('Hatchery dark console',(0,.61,-.661),(1.48,.361,.038),'RW_Teal',.10)
box('Blank hatchery nameplate',(0,.646,-.684),(.59,.198,.015),'RW_Cream',.037)
gauge((-.532,.632,-.661),.117)
for x,mat in [(.409,'RW_Coral'),(.62,'RW_Brass')]:
    disk('Large hatchery dial',(x,.634,-.689),.064,.030,mat,24)
    box('Dial grip tick',(x,.653,-.710),(.017,.052,.006),'RW_Cream',.002)
for x in [-.30,-.15,0,.15,.30]:
    box('Hatchery lower slot',(x,.384,-.68),(.081,.024,.018),'RW_Teal',.009)
finish('IncubatorFrame','Original retro egg hatchery with cream oval front porthole, mint rear arch, side pillars, coral feet/hinges and lower analog console. Central cavity and back remain open for the existing creature, runtime glass and water.')
RECORDS[-1]['exclusion_contract']={'kind':'box','min':[-.70,.98,-.50],'max':[.70,1.98,.50],
    'purpose':'Unfilled creature/glass/water cavity centered near (0,1.48,0)'}
RECORDS[-1]['porthole']={'center':[0,1.5,-.715],'inner_width':1.47,'inner_height':1.10,
    'front_frame_z':[-.715,-.525],'rear_frame_z':[.525,.665],'shape':'ellipse','glass':'none; provided by runtime'}
RECORDS[-1]['label_spaces']=[{'center':[0,.646,-.697],'size':[.59,.198]}]

# Validate the exact mesh against limits and the protected functional volumes.
LIMITS={'ResearchBench':(1.70,2.20,1.20),'IncubatorFrame':(1.95,2.22,1.43)}
for record in RECORDS:
    name=record['id']
    root,obj=ASSETS[name]
    dims=record['bounds_unity_m']['size']
    limit=LIMITS[name]
    assert all(dims[i]<=limit[i]+.00001 for i in range(3)),(name,dims,limit)
    assert record['bounds_unity_m']['min'][1]>=-.00001
    assert record['bounds_unity_m']['max'][1]<=limit[1]+.00001
    volume=record['exclusion_contract']
    points=[un(v.co) for v in obj.data.vertices]
    # Also sample triangle centers, so a broad polygon spanning a protected
    # volume cannot slip through a vertex-only receipt.
    for poly in obj.data.polygons:
        points.append(un(poly.center))
    if volume['kind']=='box':
        blocked=[p for p in points if all(volume['min'][i]<p[i]<volume['max'][i] for i in range(3))]
    else:
        blocked=[p for p in points if volume['y_min']<p[1]<volume['y_max'] and p[0]**2+p[2]**2<volume['radius']**2]
    assert not blocked,(name,'Functional volume occupied',blocked[:8])
    record['protected_volume_sample_points']=len(points)
    record['protected_volume_intersecting_samples']=len(blocked)
    record['material_slot_count']=len(record['material_slots'])
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True);obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    dest=EXPORT/(name+'.fbx')
    if args.only_export is None or name in args.only_export:
        bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,
            global_scale=1.,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
            axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,
            object_types={'MESH','EMPTY'},use_mesh_modifiers=True,mesh_smooth_type='OFF',
            use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',embed_textures=False)
    else:
        assert dest.is_file(), 'Selective export requires the unchanged prior asset: '+str(dest)
    record['sha256']=hashlib.sha256(dest.read_bytes()).hexdigest()
    record['bytes']=dest.stat().st_size

# Separate objects only for the authoring review; exports above remain at zero.
ASSETS['ResearchBench'][0].location=u((-1.18,0,0))
ASSETS['IncubatorFrame'][0].location=u((1.18,0,0))
floor=box('Studio floor',(0,-.08,.3),(7,.15,4.6),'RW_Cream',.065)
active_parts=[]
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.render.resolution_x=1900
scene.render.resolution_y=1400
scene.render.resolution_percentage=100
scene.world.color=(.32,.32,.32)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(SOURCE/'LabRefreshKit_ContactSheet.png')
def area(name,p,power,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=u(p)
    ob.rotation_euler=(u((0,1,0))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Soft studio key',(-4,5,-5),850,4)
area('Warm studio fill',(4,4,-1),500,4)
area('Clean shape rim',(0,5,4),700,3)
camera_data=bpy.data.cameras.new('Lab refresh review camera')
camera=bpy.data.objects.new('Lab refresh review camera',camera_data);scene.collection.objects.link(camera)
camera.location=u((4.7,3.6,-8.5))
camera.rotation_euler=(u((0,1.06,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=5.35;scene.camera=camera
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'EvolutionShop_LabRefreshKit.blend'))
bpy.ops.render.render(write_still=True)
blend=SOURCE/'EvolutionShop_LabRefreshKit.blend'
manifest={'schema':1,'title':'Evolution Shop research workbench and retro hatchery refresh',
    'origin':{'author':'Original local Blender authoring by Codex for this authorized Evolution Shop task','external_sources':[],
        'license':'Original project work; no third-party source asset or third-party asset license','paid_generation':False,'external_uploads':False},
    'blender_version':bpy.app.version_string,'source_script':'Tools/Art/create_lab_refresh_kit.py',
    'source_script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'blend_source':'ArtSource/LabRefreshKit/EvolutionShop_LabRefreshKit.blend','blend_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),
    'scale':'1 Unity unit = 1 metre','up':'Unity +Y','front':'Unity -Z','pivot':'Floor center, unit scale, positive determinant',
    'export_settings':{'format':'FBX','forward':'-Z','up':'Y','global_scale':1,'apply_unit_scale':True,'apply_scale_options':'FBX_SCALE_UNITS','bake_space_transform':True,'animations':False},
    'materials':{name:{'srgb_hex':'#'+v[0],'roughness':v[1],'metallic':v[2]} for name,v in PALETTE.items()},
    'materials_exception':'Shared original RW_* opaque palette slots; no texture, glass, emission, collider or gameplay component. Root remaps to existing URP palette.',
    'assets':RECORDS,'render':'ArtSource/LabRefreshKit/LabRefreshKit_ContactSheet.png',
    'roundtrip_validation':'ArtSource/LabRefreshKit/fbx_roundtrip_validation.json',
    'validation_limits':['Blender studio render, not Unity/runtime proof.','Protected-volume verification samples vertices and face centers; root validates full imported bounds and actual rendered view.','No performance/LOD or human art acceptance claim.']}
(SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('LAB_REFRESH_COMPLETE '+json.dumps(RECORDS))
