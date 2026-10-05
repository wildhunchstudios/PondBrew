"""Original Evolution Shop art kit. Blender 5.0, no external assets or add-ons.

Run: blender --background --factory-startup --python create_redesign_kit.py -- --repo C:/dev/DumbVideoGameStudio
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
SOURCE = REPO / 'ArtSource/RedesignKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/RedesignKit'
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
    root['unity_front']='-Z'
    root['unity_pivot']=('existing tool origin; root coordinates retained' if name in {'Siphon','SiphonCrown'} else 'floor centre; GrowerCap remains floor-relative for direct fit')
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

# Cozy front-shop furniture: rounded top, real routed-panel depth and turned feet.
for x in [-.82,.82]:
    for z in [-.44,.44]: foot(x,z)
box('Cabinet carcass',(0,.55,0),(1.94,.65,1.16),'RW_Teal',.075)
box('Oak plinth',(0,.235,0),(2,.11,1.20),'RW_Oak',.035)
box('Honey plinth lip',(0,.295,0),(2.01,.05,1.22),'RW_Honey',.02)
for x in [-.475,.475]:
    box('Routed door frame',(x,.572,-.565),(.895,.48,.065),'RW_Honey',.043)
    box('Teal raised panel',(x,.569,-.603),(.764,.349,.045),'RW_Teal',.033)
    box('Mint panel reveal',(x,.569,-.629),(.674,.266,.012),'RW_Mint',.019)
    box('Inset enamel face',(x,.571,-.640),(.623,.221,.008),'RW_Teal',.016)
    handle(x,.675,-.59,.21)
box('Honey bullnose top',(0,.923,0),(2.10,.114,1.30),'RW_PaleOak',.054)
box('Oak under-top bead',(0,.851,0),(2.055,.038,1.25),'RW_Oak',.016)
scallop('Scalloped display apron',2.0,.799,.881,-.605,.045,'RW_Honey',9)
for x in [-.934,.934]:
    box('Rounded corner stile',(x,.576,-.567),(.082,.53,.092),'RW_PaleOak',.038)
finish('DisplayCabinet','Teal two-door specimen cabinet with turned feet, routed panels, bow handles and scalloped honey-wood rim. No tank/glass.')

for x in [-.97,.97]:
    for z in [-.34,.34]: foot(x,z,.25)
box('Warm counter body',(0,.596,0),(2.22,.80,.93),'RW_Honey',.12)
box('Oak lower moulding',(0,.237,0),(2.28,.12,.99),'RW_Oak',.045)
box('Coral lower highlight',(0,.305,-.01),(2.28,.035,1.00),'RW_Coral',.015)
for i in range(11):
    x=(i-5)*.181
    box('Rounded reeded front',(x,.646,-.472),(.142,.62,.06),'RW_PaleOak',.027)
box('Counter greeting enamel',(0,.68,-.494),(1.00,.41,.055),'RW_Teal',.11)
box('Cream label inset',(0,.695,-.526),(.79,.268,.018),'RW_Cream',.073)
for x in [-.423,.423]: disk('Brass badge pin',(x,.694,-.535),.022,.015,'RW_Brass',16)
scallop('Counter scalloped apron',2.21,.889,.985,-.48,.045,'RW_Oak',10)
box('Deep rounded counter top',(0,1.040,0),(2.40,.120,1.10),'RW_PaleOak',.056)
box('Counter edge warm bead',(0,.966,0),(2.34,.037,1.05),'RW_Brass',.016)
for x in [-1.068,1.068]:
    box('Turned corner post',(x,.621,-.415),(.13,.69,.13),'RW_Honey',.056)
finish('ServiceCounter','Welcoming reeded wood counter with rounded honey slab, blank cream nameplate and scalloped apron.')

# Quirky lab: enamel bodies, generous curves and analog instruments.
for x in [-.54,.54]:
    for z in [-.40,.40]:
        lathe('Rubber machine foot',(x,0,z),[(.105,0),(.115,.025),(.115,.09),(.085,.14)],'RW_Ink',20)
box('Grower lower plinth',(0,.155,0),(1.48,.15,1.28),'RW_Teal',.065)
box('Mint grower shell',(0,.420,0),(1.42,.46,1.22),'RW_Mint',.13)
box('Coral belt',(0,.607,0),(1.455,.065,1.245),'RW_Coral',.031)
lathe('Enamel vessel collar',(0,0,.035),[(.53,.63),(.61,.665),(.63,.706),(.625,.75),(.59,.788)],'RW_Cream',32,ellipse=(1,.88))
lathe('Vessel seat lip',(0,0,.035),[(.535,.779),(.59,.791),(.592,.815),(.538,.830)],'RW_Brass',32,ellipse=(1,.88))
box('Control panel',(0,.462,-.594),(1.17,.27,.055),'RW_Teal',.06)
gauge((-.37,.475,-.623),.112)
for i,mat in enumerate(['RW_Coral','RW_Brass','RW_Mint']):
    disk('Chunky control key',(.065+i*.17,.48,-.640),.048,.044,mat,20)
for x in [-.48,-.30,-.12,.06,.24,.42]:
    box('Lower rounded cooling vent',(x,.267,-.615),(.094,.036,.022),'RW_Ink',.015)
for side in [-1,1]:
    tube('Curved side pickup',[(side*.54,.55,.31),(side*.68,.63,.30),(side*.72,.81,.26),(side*.67,.94,.17)],.042,'RW_Brass',10)
    ellipsoid('Coral pipe valve',(side*.67,.94,.17),(.13,.13,.13),'RW_Coral',16,8)
finish('GrowerBase','Mint/coral rounded grower base with visible brass vessel collar, analog dial, chunky buttons and sweeping side pickups. Central vessel stays open above collar.')

lathe('Cap teal gasket',(0,0,.035),[(.53,2.09),(.585,2.103),(.59,2.125),(.56,2.14)],'RW_Teal',32,ellipse=(1,.88))
lathe('Coral domed vessel cap',(0,0,.035),[(.57,2.126),(.608,2.14),(.60,2.17),(.53,2.199),(.41,2.22),(.24,2.237),(.04,2.245)],'RW_Coral',32,ellipse=(1,.88))
for i in range(8):
    t=i*math.tau/8
    ellipsoid('Cap cream rivet',(.56*math.cos(t),2.174,.035+.492*math.sin(t)),(.045,.032,.045),'RW_Cream',12,6)
finish('GrowerCap','Low domed coral cap with teal gasket and chunky cream rivets; authored floor-relative for unchanged station vessel.')

for x in [-.72,.72]:
    for z in [-.48,.48]:
        lathe('Incubator rubber foot',(x,0,z),[(.10,0),(.112,.03),(.10,.13)],'RW_Ink',20)
box('Incubator teal plinth',(0,.162,0),(1.86,.19,1.34),'RW_Teal',.084)
box('Incubator enamel shell',(0,.493,0),(1.78,.61,1.27),'RW_Mint',.135)
box('Cream top seat',(0,.835,0),(1.90,.13,1.40),'RW_Cream',.060)
box('Coral top reveal',(0,.756,0),(1.85,.036,1.34),'RW_Coral',.016)
box('Dark control recess',(0,.395,-.613),(1.38,.27,.055),'RW_Teal',.07)
gauge((-.48,.40,-.643),.107)
box('Blank status lozenge',(.03,.408,-.648),(.44,.113,.032),'RW_Cream',.048)
for x,mat in [(.40,'RW_Coral'),(.58,'RW_Brass')]:
    disk('Incubator control button',(x,.408,-.665),.052,.046,mat,20)
for x in [-.49,-.30,-.11,.08,.27,.46]:
    box('Incubator vent',(x,.228,-.666),(.111,.034,.017),'RW_Ink',.014)
for side in [-1,1]:
    tube('Cream side grip',[(side*.83,.43,-.32),(side*.90,.45,-.26),(side*.90,.45,.16),(side*.83,.43,.22)],.027,'RW_Cream',8)
finish('IncubatorBase','Wide mint incubator base with soft cream working rim, dark instrument recess, gauge and coral/brass controls; no vessel replacement.')

# Tool fits the established hand root. The transparent runtime reservoir and
# status readout remain visible between the authored open protective rails.
lathe('Siphon shaped grip',(0,0,-.07),[(.027,-.265),(.037,-.251),(.044,-.215),(.046,-.165),(.04,-.120),(.031,-.099)],'RW_Mint',24)
lathe('Siphon lower vessel seat',(0,0,.025),[(.033,-.110),(.059,-.103),(.061,-.09),(.052,-.077)],'RW_Teal',24)
tube('Grip link',[(0,-.11,-.07),(0,-.10,-.025),(0,-.097,.025)],.029,'RW_Teal',10)
for side in [-1,1]:
    tube('Reservoir protective rail',[(side*.05,-.087,.026),(side*.067,-.047,.035),(side*.068,.079,.035),(side*.05,.142,.026)],.009,'RW_Brass',8)
tube('Sweeping siphon spout',[(0,.139,.062),(0,.114,.136),(0,.060,.190),(0,.050,.303),(0,.055,.398),(0,.051,.470)],.018,'RW_Brass',10)
ellipsoid('Spout nozzle',(0,.05,.479),(.047,.046,.032),'RW_Cream',16,8)
arc('Siphon grip guard',(-.035,-.163,-.06),.066,.076,math.pi*.45,math.pi*1.58,'RW_Teal',.010,12)
finish('Siphon','Mint shaped grip, open brass reservoir cage and +Z spout. Existing transparent reservoir remains at (0,.02,.025), Y -.09..+.13; root stays at existing tool origin. Pump crown is a separate SiphonCrown export.')

# The reservoir cap rises with upgrades. Keep the complete pump crown separate
# in TOOL-ROOT coordinates, so the runtime visual child translates by +.1022m
# on Y without scaling or moving the fixed grip, open cage, nozzle, or readout.
lathe('Siphon brass top ferrule',(0,0,.025),[(.052,.124),(.061,.139),(.058,.156),(.031,.168)],'RW_Brass',24)
lathe('Siphon pump stem',(0,0,.025),[(.022,.164),(.024,.207)],'RW_Cream',20)
ellipsoid('Siphon coral pump bulb',(0,.236,.025),(.135,.12,.13),'RW_Coral',24,12)
lathe('Pump collar',(0,0,.025),[(.027,.194),(.035,.200),(.036,.207)],'RW_Teal',20)
finish('SiphonCrown','Separate brass base, cream stem and coral pump bulb in existing TOOL-ROOT coordinates. Attach at zero for base tool; translate Y +0.1022m with upgraded reservoir cap. No independent gameplay or collider.')
RECORDS[-1]['attachment']={'parent':'Existing tool root','base_local_position':[0,0,0],
    'upgraded_local_position':[0,.1022,0],'bulb_center_base':[0,.236,.025],
    'bulb_center_upgraded':[0,.3382,.025],'preserved_reservoir_cap_center_y':[.135,.2372]}

# Two optional accents use shapes, not lettering or licensed graphics.
box('Oak sign backing',(0,.43,0),(1.62,.82,.11),'RW_Oak',.16)
box('Honey sign border',(0,.43,-.056),(1.54,.75,.06),'RW_Honey',.14)
box('Teal sign face',(0,.43,-.091),(1.42,.64,.035),'RW_Teal',.12)
ellipsoid('Fish sign body',(-.035,.46,-.132),(.58,.32,.046),'RW_Cream',24,12)
mesh('Heart-shaped fish tail',[(.19,.46,-.147),(.43,.64,-.147),(.48,.56,-.147),(.47,.47,-.147),(.49,.35,-.147),(.43,.29,-.147)],[(0,1,2,3,4,5)],'RW_Coral')
disk('Fish sign eye',(-.20,.50,-.161),.020,.015,'RW_Ink',16)
for x,y,r in [(-.43,.59,.04),(-.51,.69,.028),(.13,.70,.025)]:
    disk('Sign floating bubble',(x,y,-.137),r,.013,'RW_Brass',16)
arc('Smile of leaves',(0,.38,-.129),.52,.21,math.pi*1.12,math.pi*1.88,'RW_Mint',.017,15)
finish('WallSign','Original fish-and-bubbles emblem on rounded teal/honey sign, without text. Optional decorative accent.')

box('Shelf back',(0,.145,.107),(1.42,.29,.055),'RW_Teal',.044)
box('Shelf bullnose',(0,.111,-.014),(1.48,.082,.35),'RW_PaleOak',.036)
for x in [-.49,.49]:
    tube('Brass shelf bracket',[(x,.075,-.145),(x,0,.102),(x,.085,.105)],.023,'RW_Brass',8)
for px,mat,scale in [(-.48,'RW_Coral',.92),(.03,'RW_Cream',.72)]:
    lathe('Little glazed plant pot',(px,.152,-.02),[(.067*scale,0),(.085*scale,.025*scale),(.115*scale,.18*scale),(.127*scale,.186*scale),(.127*scale,.217*scale),(.112*scale,.222*scale)],mat,24)
    lathe('Pot dark soil',(px,.152,-.02),[(.110*scale,.212*scale),(.110*scale,.219*scale)],'RW_Oak',20)
    for k in range(5):
        ang=k*2.399
        top=(px+math.cos(ang)*.10*scale,.152+(.37+.075*(k%2))*scale,-.02+math.sin(ang)*.085*scale)
        tube('Soft leaf stem',[(px,.152+.19*scale,-.02),top],.010*scale,'RW_Leaf',6)
        leaf=ellipsoid('Cartoon botanical leaf',top,(.10*scale,.19*scale,.042*scale),'RW_Leaf' if k%2 else 'RW_Mint',16,8)
        leaf.rotation_euler[1]=(k-2)*.25
lathe('Coral shelf reagent',(.48,.152,-.02),[(.075,0),(.089,.03),(.085,.17),(.036,.22),(.033,.29)],'RW_Coral',24)
lathe('Reagent stopper',(.48,.152,-.02),[(.042,.275),(.046,.29),(.043,.315)],'RW_Brass',20)
finish('BotanicalShelf','Small honey-wood shelf with two sculpted potted plants and opaque coral reagent bottle. Optional wall dressing.')

# Files are exported one asset at a time from the original floor origin.
for name,(root,obj) in ASSETS.items():
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    dest=EXPORT/(name+'.fbx')
    if args.only_export is None or name in args.only_export:
        bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,
            global_scale=1.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
            axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,
            object_types={'MESH','EMPTY'},use_mesh_modifiers=True,mesh_smooth_type='OFF',
            use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',embed_textures=False)
    else:
        assert dest.is_file(), 'Selective export requires the unchanged prior asset: '+str(dest)
    RECORDS[list(ASSETS).index(name)]['sha256']=hashlib.sha256(dest.read_bytes()).hexdigest()
    RECORDS[list(ASSETS).index(name)]['bytes']=dest.stat().st_size

# Contact sheet is a Blender studio view, explicitly not Unity proof.
positions={'DisplayCabinet':(-2.1,0,-1.45),'ServiceCounter':(1.65,0,-1.45),
           'GrowerBase':(-2.0,0,1.1),'GrowerCap':(-2.0,0,1.1),
           'IncubatorBase':(.3,0,1.1),'Siphon':(2.18,.32,1.05),'SiphonCrown':(2.18,.32,1.05),
           'WallSign':(-.55,.94,2.52),'BotanicalShelf':(1.4,.97,2.48)}
for name,(root,obj) in ASSETS.items(): root.location=u(positions[name])
# Isolate optional cap lower in studio solely for silhouette; floor-relative export is unaffected.
ASSETS['GrowerCap'][0].location += u((0,-.75,0))
# Shared studio floor is not part of an export.
floor=box('Studio floor',(0,-.075,.45),(10,.13,7.5),'RW_Cream',.06)
active_parts=[]
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=40
scene.cycles.use_denoising=True
scene.render.resolution_x=1800
scene.render.resolution_y=1300
scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(SOURCE/'RedesignKit_ContactSheet.png')
def area(name,pos,power,size):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=power
    data.shape='DISK'
    data.size=size
    ob=bpy.data.objects.new(name,data)
    scene.collection.objects.link(ob)
    ob.location=u(pos)
    ob.rotation_euler=(u((0,.7,.3))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Large soft key',(-3,6,-5),850,5)
area('Mint-side fill',(5,4,1),500,4)
area('Warm rear rim',(-2,5,5),650,3)
camdata=bpy.data.cameras.new('Kit review camera')
cam=bpy.data.objects.new('Kit review camera',camdata)
scene.collection.objects.link(cam)
cam.location=u((6,6.4,-9))
cam.rotation_euler=(u((0,.45,.35))-cam.location).to_track_quat('-Z','Y').to_euler()
camdata.type='ORTHO'
camdata.ortho_scale=8.9
scene.camera=cam
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'EvolutionShop_RedesignKit.blend'))
bpy.ops.render.render(write_still=True)
blend=SOURCE/'EvolutionShop_RedesignKit.blend'
manifest={'schema':1,'title':'Evolution Shop original cozy shop / quirky lab kit',
 'origin':{'author':'Original local asset authorship by Codex for this Evolution Shop task',
 'external_sources':[],'license':'Original project work; no third-party source or third-party asset license',
 'paid_generation':False,'external_uploads':False},
 'blender_version':bpy.app.version_string,'source_script':'Tools/Art/create_redesign_kit.py',
 'source_script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
 'blend_source':str(blend.relative_to(REPO)).replace('\\','/'),'blend_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),
 'scale':'1 unit = 1 metre','front':'Unity local -Z','up':'Unity +Y',
 'pivot':'Furniture floor centre at (0,0,0); GrowerCap remains at vessel height in floor-root coordinates. Siphon and SiphonCrown use existing hand origin; crown translates Y +0.1022m with upgraded reservoir. Optional wall accents use a lower-centre attachment; BotanicalShelf bracket extends 0.023m below attachment.',
 'export_settings':{'format':'FBX','forward':'-Z','up':'Y','global_scale':1,'apply_unit_scale':True,'apply_scale_options':'FBX_SCALE_UNITS','bake_space_transform':True,'animations':False},
 'unity_import_expected':{'globalScale':1,'useFileScale':True,'animationType':'None','importAnimation':False,'addCollider':False,'material_mapping':'Root owner remaps RW_* names to shared URP/Lit materials'},
 'materials':{name:{'srgb_hex':'#'+data[0],'roughness':data[1],'metallic':data[2]} for name,data in PALETTE.items()},
 'materials_exception':'Multiple shared opaque flat-color slots preserve clean authored color separation without textures; root maps/reuses shared palette. No glass/emission imported.',
 'assets':RECORDS,'render':'ArtSource/RedesignKit/RedesignKit_ContactSheet.png',
 'roundtrip_validation':'ArtSource/RedesignKit/fbx_roundtrip_validation.json',
 'validation_limits':['Contact sheet is a Blender studio render, not Unity import/runtime evidence.','Unity orientation, scale, shader mapping, footprints, play-distance readability and player captures remain for root integration.','No LOD or performance improvement claim; face counts are measured geometry only.','Editable blend presents separated models; regenerate/export via script to restore original floor-root export coordinates.']}
(SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('REDESIGN_KIT_COMPLETE '+json.dumps([{'id':r['id'],'triangles':r['triangles'],'bounds':r['bounds_unity_m']} for r in RECORDS]))
