"""Original Evolution Shop Pocket Aquarium Diorama kit (AD-A to AD-C, GR-12). Blender 5.0, no external assets or add-ons.

Run: blender --background --factory-startup --python create_diorama_kit.py -- --repo C:/dev/DumbVideoGameStudio

Builds the creature species on their family rigs (AD-B's five, and GR-12's waves: wave 1 completes the Brookkin, Sparkline
and Airfin to eight each; GR-12B gives each species an aquatic animal of its own, wave by wave, the Gloamer first) and the seven characters on one humanoid rig (AD-C: the four
customer types and the three crew) as rigid parts on named pivots (so the game's existing pose code drives them) with face
shape keys, plus the six hand-drawn effect shapes as one flipbook sheet and the creatures' colour variant atlas.
Everything is generated from the numbers below; nothing is imported.

Authoring helpers take Unity coordinates (X right, Y up, Z forward, left-handed) and reflect them into
Blender's right-handed space as (-X, -Z, Y); the FBX export bakes the Y-up conversion. Import probes showed
that this is the mapping Unity reads back exactly, and that only parts directly under the asset root keep
their transforms, so every part is a direct child of the root and is its own pivot (a fin mesh is the
"Left fin pivot"). The visitor's face parts are reparented onto its "Head pivot" by the game at build time.
The Pondlet faces local -Z (creature convention); the visitor faces +Z (character convention).
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Vector

parser = argparse.ArgumentParser()
parser.add_argument('--repo', required=True)
parser.add_argument('--no-render', action='store_true', help='Skip the review renders.')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
REPO = Path(args.repo).resolve()
assert (REPO / 'EvolutionShop/ProjectSettings/ProjectVersion.txt').is_file()
SOURCE = REPO / 'ArtSource/DioramaKit'
EXPORT = REPO / 'EvolutionShop/Assets/EvolutionShop/Resources/Art/DioramaKit'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for item in list(block):
        block.remove(item)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
bpy.context.preferences.filepaths.save_version = 0
COLLECTION = scene.collection

# Slot names only. Unity maps each DK_* slot to its own toon material (ShopDioramaPalette); the hex values
# here are the same sRGB colours, for the review renders and the manifest.
PALETTE = {
    'DK_Blush': 'FF9EA0', 'DK_EyeWhite': 'FFFFFF', 'DK_Pupil': '1B413E', 'DK_Glint': 'FFFFFF',
    'DK_CasualKnit': 'F08A3C', 'DK_Collar': 'FFF6DF', 'DK_Trousers': '2B3B4F', 'DK_Shoes': '3B2A22',
    'DK_Skin': 'B9774E', 'DK_Beret': '1B2B33', 'DK_Nub': 'D66145', 'DK_Tote': 'E9D6A8', 'DK_Hair': '3B2A22',
    'DK_Mouth': '5A1F22', 'DK_Button': 'D66145', 'DK_Gold': 'D9B04A', 'DK_Lens': 'CFEFF2',
    # AD-C: the other three customers (never teal) and the crew (always shop teal).
    'DK_BargainKnit': '6E9B4A', 'DK_BargainScarf': 'E8B83A', 'DK_BargainTrousers': '6B4E32', 'DK_BargainShoes': '3A2A1E',
    'DK_BargainSkin': 'E3AE88', 'DK_BargainHair': '8C5A2B', 'DK_BargainCap': 'C98F3A', 'DK_BargainPurse': 'C8553D',
    'DK_CollectorCoat': '7A4A8C', 'DK_CollectorTrousers': '2E2638', 'DK_CollectorShoes': '1E1A22', 'DK_CollectorSkin': '7A4E33',
    'DK_CollectorHair': 'D8D2C8', 'DK_CollectorHat': '2A2230', 'DK_CollectorBow': 'C9473F', 'DK_CollectorHandle': '6B3F2A',
    'DK_PremiumCoat': 'EDE3CC', 'DK_PremiumTrim': '2B2B33', 'DK_PremiumTrousers': '1E1E24', 'DK_PremiumShoes': '141418',
    'DK_PremiumSkin': 'C08A63', 'DK_PremiumHair': '2B2118', 'DK_PremiumHat': '18181C', 'DK_PremiumBag': 'E8C25A',
    'DK_PremiumFlower': 'E8628C',
    'DK_CrewTeal': '1F7A72', 'DK_CrewCream': 'F4E9CF', 'DK_CrewBadge': 'E8664F', 'DK_CrewTrousers': '33404D', 'DK_CrewShoes': '3B2A22',
    'DK_PipSkin': 'D09466', 'DK_PipHair': '3B2A22', 'DK_Pencil': 'F2C14E', 'DK_Stool': 'C98F52',
    'DK_NoriSkin': '8A5A3C', 'DK_NoriHair': '1E1612', 'DK_NoriGlove': 'C9A06A',
    'DK_MossSkin': 'F0C8A0', 'DK_MossHair': '5E8F3A', 'DK_MossGoggle': '2B3B4F',
    'DK_Crate': 'C99A62', 'DK_CrateSlat': 'A87A48',
    # GR-12 wave 2: the Rainbow Regent's middle tail bands (its other bands are its own colours).
    'DK_RainbowYellow': 'FFE27A', 'DK_RainbowGreen': '8FE08A',
}

# AD-B: the creature families and the colour variant atlas. Species keep their order here forever (the atlas rows and the
# game's DioramaSpecies table follow it; GR-12 appends each wave's species, 72 in nine families when all three are in). Each species has five colour slots, named
# DK_<Species><Slot>, and a sheen (Shimmer's iridescent rim; its alpha is the strength). Variants are colour swaps only.
FAMILIES = {'Brookkin': 'river fish: pouty lips, round fins', 'Sparkline': 'Spark: a glowing lantern on a stalk',
            'Airfin': 'Mist: wide gliding sails', 'Cinderling': 'Ember: fiery animals of every kind (GR-12B), each with a row of glowing ember spines down its back',
            'Dewling': 'Mist: round and pearly, shiny dew beads on the head and back',
            'Reefback': 'Brine: big and gentle reef animals of every kind (GR-12B), crusted with corals that bloom and with barnacles', 'Saltscale': 'Brine: sea animals of every kind (GR-12B), each with a glittering salt stripe along its sides',
            'Gloamer': 'Dusk: deep-sea animals of every kind (GR-12B), each with a glowing lure on a stalk and soft glowing lights', 'Cometail': 'Spark and Dusk: sea and star animals of every kind (GR-12B), each with a bright glowing head and a comet tail of glowing wisps'}  # all nine families (GR-12 waves 1 to 4)
SLOTS = ['Body', 'Fin', 'Belly', 'Accent', 'Blush', 'Sheen']
VARIANT_NAMES = ['Classic', 'Sunny', 'Deep', 'Shimmer']
ATLAS_SPECIES_CAPACITY = 72  # GR.7's 72 species
SPECIES = [  # id, kit asset, family, {variant: [Body, Fin, Belly, Accent, Blush, Sheen(rgb+alpha)]}
    ('pondlet', 'DioramaPondlet', 'Brookkin', {
        'Classic': ['FF6F57', 'FF9E6E', 'FFD9A0', 'FF8FA0', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['FFA94D', 'FFD27A', 'FFF0B8', 'FF9F8A', 'FFB0A0', 'FFFFFF00'],
        'Deep':    ['C7364A', 'E2665E', 'F2C49A', 'D9577A', 'E58A96', 'FFFFFF00'],
        'Shimmer': ['FFB3A6', 'FFD8C8', 'FFF4E6', 'FFB6C8', 'FFC2CC', 'B8F0FF59']}),
    ('bubblebelly', 'DioramaBubblebelly', 'Brookkin', {
        'Classic': ['FFB83D', 'FFD98A', 'FFF1D0', 'E8862A', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['FFD34D', 'FFEA9A', 'FFF8DC', 'F0A030', 'FFB0A0', 'FFFFFF00'],
        'Deep':    ['E07A1F', 'F0A35A', 'F5D7A8', 'B85A18', 'E58A80', 'FFFFFF00'],
        'Shimmer': ['FFE3A0', 'FFF2CC', 'FFFBEA', 'F5C070', 'FFC8C0', 'FFD6F559']}),
    ('ribbonfin', 'DioramaRibbonfin', 'Brookkin', {
        'Classic': ['2FC4C8', 'FF8FB1', 'E8FFF7', '1A8A93', 'FF9EB0', 'FFFFFF00'],
        'Sunny':   ['5FD9A8', 'FFB36B', 'F4FFEA', '2E9E78', 'FFB0A0', 'FFFFFF00'],
        'Deep':    ['1E7FA8', 'D8487E', 'CFEFF5', '135A7A', 'E07A9A', 'FFFFFF00'],
        'Shimmer': ['A8F0EC', 'FFD0E4', 'FFFFFF', '6CC9CC', 'FFC8DA', 'FFE6A859']}),
    ('lanternfin', 'DioramaLanternfin', 'Sparkline', {
        'Classic': ['5B5BD6', '9D8CF0', 'F2E9FF', 'FFD24A', 'FF9EC8', 'FFFFFF00'],
        'Sunny':   ['8A6BE8', 'C2A8FF', 'FFF2DC', 'FFE07A', 'FFB0C8', 'FFFFFF00'],
        'Deep':    ['30307E', '5E52B8', 'CFC6F2', 'FFB63A', 'D07AB0', 'FFFFFF00'],
        'Shimmer': ['B9B6FF', 'DCD6FF', 'FFFFFF', 'FFF0A8', 'FFC8E8', 'A8FFE666']}),
    ('driftwing', 'DioramaDriftwing', 'Airfin', {
        'Classic': ['D8A6F2', 'BFF5E6', 'FFF1E8', 'A06FD1', 'FF9EC0', 'FFFFFF00'],
        'Sunny':   ['F5B6D8', 'FFF0B0', 'FFF8EC', 'D97AB0', 'FFB0B0', 'FFFFFF00'],
        'Deep':    ['8F5CC9', '7FD9C4', 'EDE0F5', '5E3A99', 'D088C0', 'FFFFFF00'],
        'Shimmer': ['F0DCFF', 'E6FFF8', 'FFFFFF', 'C9A8F0', 'FFD0E8', 'FFF0A866']}),
    # GR-12 wave 1 (2026-10-24): the Brookkin, Sparkline and Airfin completed to eight each, in GR.7's order. Sunny is warmer
    # and lighter, Deep darker and cooler, Shimmer pale with its sheen, worked from each Classic as AD-B's were picked.
    ('puddlepup', 'DioramaPuddlepup', 'Brookkin', {
        'Classic': ['F2C48D', 'C97B4A', 'FFF3DE', '7A4B33', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['F5D760', 'F5D866', 'FFEDA6', 'F5DB72', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['D08260', '953528', 'BDA699', '702430', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['F7E5D0', 'F5D1BA', 'FFFAF2', 'F5D1BF', 'FFBBBC', 'B8F0FF5E']}),
    ('splashkit', 'DioramaSplashkit', 'Brookkin', {
        'Classic': ['F590B8', 'FFD27A', 'FFF0F6', 'B0457E', 'FF8FA8', 'FFFFFF00'],
        'Sunny':   ['F59B86', 'FFE785', 'FFEDDC', 'F56C75', 'FF989E', 'FFFFFF00'],
        'Deep':    ['B55C97', 'DB814F', 'E5DDEA', '822670', 'E67398', 'FFFFFF00'],
        'Shimmer': ['FAD3E2', 'FFEECC', 'EAF1F3', 'F5BCDA', 'FFB1C2', 'B8FFE65E']}),
    ('reedwhisker', 'DioramaReedwhisker', 'Brookkin', {
        'Classic': ['B9C25E', 'E6D98C', 'FBF7DD', '5E6B26', 'FFA0A0', 'FFFFFF00'],
        'Sunny':   ['F5DE81', 'F5E39D', 'FCF2CB', 'A59140', 'FFB5A6', 'FFFFFF00'],
        'Deep':    ['A7913D', 'C69961', 'E2E2D6', '5C5416', 'E5828C', 'FFFFFF00'],
        'Shimmer': ['F0F5C5', 'F5F0D0', 'E8F5E3', 'F1FFB5', 'FFBDBD', 'FFD6F55E']}),
    ('lilyfin', 'DioramaLilyfin', 'Brookkin', {
        'Classic': ['67C48A', 'FFB0CC', 'EDFFF1', 'FFD84A', 'FF9EB8', 'FFFFFF00'],
        'Sunny':   ['94F58A', 'FFB0A6', 'F2F8D9', 'FFDE58', 'FFA6AF', 'FFFFFF00'],
        'Deep':    ['3F9174', 'BD74A1', 'D7E9E6', 'DB7D25', 'E680A6', 'FFFFFF00'],
        'Shimmer': ['C9F5D9', 'FFE1EC', 'DFFAF0', 'FFF0BA', 'FFBBCD', 'FFE6A85E']}),
    ('brookmonarch', 'DioramaBrookmonarch', 'Brookkin', {
        'Classic': ['D23F5B', 'FF9F43', 'FFEED6', 'FFD84A', 'FF9EB0', 'FFFFFF00'],
        'Sunny':   ['F58357', 'FFDC52', 'FFEDA6', 'FFDE58', 'FFA6A7', 'FFFFFF00'],
        'Deep':    ['9B1E57', 'BD411D', 'BDA092', 'DB7D25', 'E6809D', 'FFFFFF00'],
        'Shimmer': ['F5B4C0', 'FFDBB8', 'FFF9EF', 'FFF0BA', 'FFBBC8', 'FFF0B05E']}),
    ('sparkminnow', 'DioramaSparkminnow', 'Sparkline', {
        'Classic': ['4FC0F2', '9FE4FF', 'EAF9FF', 'FFE34A', 'FF9EC0', 'FFFFFF00'],
        'Sunny':   ['5DF5DD', 'A6FFEE', 'F0F4E3', 'FFDE58', 'FFA6B6', 'FFFFFF00'],
        'Deep':    ['2863B3', '678DBD', 'D5E4F1', 'DB8925', 'E680AE', 'FFFFFF00'],
        'Shimmer': ['B8E4F7', 'DBF5FF', 'DDF7F9', 'FFF4BA', 'FFBBD3', 'FFE6A85E']}),
    ('fizzlet', 'DioramaFizzlet', 'Sparkline', {
        'Classic': ['F2578C', 'FFA6C4', 'FFE6F0', 'B4FF5A', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['F57765', 'FFB1A6', 'FFE6D8', 'FFFD67', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['B32E7F', 'BD6C9D', 'E5D5E5', '57BD2F', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['F7BBD0', 'FFDDE9', 'EAEAEF', 'E3FFC0', 'FFBBBC', 'B8F0FF5E']}),
    ('glowbelly', 'DioramaGlowbelly', 'Sparkline', {
        'Classic': ['3E4FB0', 'A9B6FF', 'DCE2FF', 'FFE28A', 'FF9EC8', 'FFFFFF00'],
        'Sunny':   ['9163F5', 'C2A6FF', 'C0A6FF', 'FFE993', 'FFA6BE', 'FFFFFF00'],
        'Deep':    ['214982', '6F8FBD', '97A8BD', 'DB965C', 'E680B6', 'FFFFFF00'],
        'Shimmer': ['B9C2F5', 'DEE3FF', 'F2F4FF', 'FFF4D3', 'FFBBD9', 'FFE6A85E']}),
    ('flickerfin', 'DioramaFlickerfin', 'Sparkline', {
        'Classic': ['FF7440', 'FFB45A', 'FFF0DC', 'FFF37A', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['FFD14F', 'FFE167', 'FFEDA6', 'FFE785', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['BD1C1B', 'DB6633', 'BDA397', 'DBA44F', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['FFCAB6', 'FFE3C0', 'FFF9F2', 'FFFACC', 'FFBBBC', 'C8E8FF5E']}),
    ('buzzgill', 'DioramaBuzzgill', 'Sparkline', {
        'Classic': ['FFC83A', '4A3426', 'FFF4D2', 'FFF7A0', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['FFCF10', '786D41', 'FFEDA6', 'FFEDA6', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['DB6A18', '441C21', 'BDA58F', 'DBB26F', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['FFEAB4', 'D9BCAA', 'FFFBEE', 'FFFCDB', 'FFBBBC', 'D6C8FF5E']}),
    ('starglimmer', 'DioramaStarglimmer', 'Sparkline', {
        'Classic': ['2A2F72', '6F7CE8', 'B7C0FF', 'FFE36A', 'FF9EC8', 'FFFFFF00'],
        'Sunny':   ['6C46AE', 'AA7FF5', 'C4A6FF', 'FFE476', 'FFA6BE', 'FFFFFF00'],
        'Deep':    ['1E606F', '426AAC', '7A94BD', 'DB9041', 'E680B6', 'FFFFFF00'],
        'Shimmer': ['B7BCFF', 'C4C9F5', 'E4E7FF', 'FFF4C6', 'FFBBD9', 'FFE6F55E']}),
    ('aurorafin', 'DioramaAurorafin', 'Sparkline', {
        'Classic': ['7C5CD8', '5EEFC0', 'C9F7FF', 'FFF2A8', 'FFA6D0', 'FFFFFF00'],
        'Sunny':   ['D174F5', '6CF589', 'A6FFE3', 'FFEDA6', 'FFA6C1', 'FFFFFF00'],
        'Deep':    ['3536A0', '34B1AA', '88A7BD', 'DBAE76', 'E588BD', 'FFFFFF00'],
        'Shimmer': ['CDBFF5', 'BCF5E3', 'EAFCFF', 'FFFADE', 'FFC1DE', 'A8FFE65E']}),
    ('puffglider', 'DioramaPuffglider', 'Airfin', {
        'Classic': ['B5DEFF', 'F6F8FF', 'FFF6FB', 'FF9EC4', 'FF9EB8', 'FFFFFF00'],
        'Sunny':   ['93FFF0', 'FAF0D4', 'FFF2E0', 'FFACA6', 'FFA6AF', 'FFFFFF00'],
        'Deep':    ['788CBD', 'BBC3DC', 'E5E1EE', 'BD669F', 'E680A6', 'FFFFFF00'],
        'Shimmer': ['E3F2FF', 'D6F4F4', 'EAF5F6', 'FFDAE9', 'FFBBCD', 'FFE0F05E']}),
    ('hazewing', 'DioramaHazewing', 'Airfin', {
        'Classic': ['5FB7A6', 'DDFFF6', 'F2FFFB', '2F8C7C', 'FFA0B0', 'FFFFFF00'],
        'Sunny':   ['88F5AD', 'A6FFBE', 'F6F8E0', '5FF595', 'FFA6A6', 'FFFFFF00'],
        'Deep':    ['3A8287', '98BDBD', 'DBE9EE', '186068', 'E5829D', 'FFFFFF00'],
        'Shimmer': ['C8F5EC', 'F2FFFC', 'E2FAF6', 'B7F5EA', 'FFBDC8', 'FFE6C85E']}),
    ('gustgill', 'DioramaGustgill', 'Airfin', {
        'Classic': ['5878BA', 'D2E2FF', 'EEF3FF', 'FFCA4F', 'FFA0B8', 'FFFFFF00'],
        'Sunny':   ['7EDCF5', 'A6EFFF', 'F3EFE3', 'FFDF5D', 'FFA6AD', 'FFFFFF00'],
        'Deep':    ['343A8A', '8F93BD', 'D8DFF1', 'DB702A', 'E582A5', 'FFFFFF00'],
        'Shimmer': ['C4D4F5', 'EEF4FF', 'DFF3F9', 'FFEBBC', 'FFBDCD', 'FFF0B05E']}),
    ('mistrunner', 'DioramaMistrunner', 'Airfin', {
        'Classic': ['E66A8C', 'F3EDFF', 'FFF0F4', 'A93D66', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['F5937B', 'F8EAD4', 'FFEDDB', 'F57165', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['AA3F79', 'B9BEDC', 'E5DDE8', '7D205C', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['F5C3D0', 'D4F0F4', 'EAF1F2', 'F5B9D0', 'FFC1D3', 'C8F0FF5E']}),
    ('cloudcarp', 'DioramaCloudcarp', 'Airfin', {
        'Classic': ['F5F5FA', 'D6EAFF', 'FFFFFF', '3E9BF0', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['FAEFD2', 'A6FBFF', 'FFF8E3', '4EF3F5', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['BAC2D9', '929CBD', 'E5E9F1', '1A41B2', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['CDF3F0', 'EFF7FF', 'EAFAF9', 'B0D4F5', 'FFC1CD', 'FFD6F05E']}),
    ('breezeling', 'DioramaBreezeling', 'Airfin', {
        'Classic': ['A2EEC4', 'FAFFF4', 'FFFFF0', 'FFD84A', 'FF9EB0', 'FFFFFF00'],
        'Sunny':   ['9BF58D', 'FCF4CE', 'FFF8D8', 'FFDE58', 'FFA6A7', 'FFFFFF00'],
        'Deep':    ['6BB09C', 'BDC7D6', 'E5E9E5', 'DB7D25', 'E6809D', 'FFFFFF00'],
        'Shimmer': ['D7F5E4', 'D7F7F0', 'EAFAEF', 'FFF0BA', 'FFBBC8', 'FFE0F55E']}),
    ('zephyray', 'DioramaZephyray', 'Airfin', {
        'Classic': ['34448A', '7AA2F5', 'EEF2FF', 'FFD45A', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['9068F5', '84DDF5', 'F3EFE3', 'FFE167', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['1C3E66', '4A50B5', 'D8DEF1', 'DB7D33', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['BBC6F5', 'CADAFA', 'DFF2F9', 'FFEFC0', 'FFC1D9', 'FFF0C05E']}),
    ('emberpuff', 'DioramaEmberpuff', 'Cinderling', {
        'Classic': ['FF7A4D', 'FFB25E', 'FFE9D6', 'FFD84A', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['FFD15B', 'FFE16B', 'FFEDA6', 'FFDE58', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['BD2527', 'DB6837', 'BD9B92', 'DB7D25', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['FFCCBB', 'FFE2C2', 'FFF7EF', 'FFF0BA', 'FFBBBC', 'B8F0FF5E']}),
    ('cindertail', 'DioramaCindertail', 'Cinderling', {
        'Classic': ['E8442E', 'FF8A3D', 'FFE2CC', 'FFE066', 'FF9EA8', 'FFFFFF00'],
        'Sunny':   ['F5AA40', 'FFDB4D', 'FFEDA6', 'FFE372', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['AC0E26', 'BD2E18', 'BD938B', 'DB8C3D', 'E68095', 'FFFFFF00'],
        'Shimmer': ['F5B3AA', 'FFD3B5', 'FFF4EC', 'FFF3C5', 'FFBBC2', 'C8E8FF5E']}),
    ('coalbelly', 'DioramaCoalbelly', 'Cinderling', {
        'Classic': ['3E3A40', '6B5E5A', 'B8A8A0', 'FF9A2E', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['95886B', 'A59D8C', 'F5E49F', 'FFD93F', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['5F657C', '624950', '88706E', 'DB4B0D', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['95B2B1', 'FFF1ED', 'F5EDE9', 'FFD9B0', 'FFBBBC', 'FFD0A85E']}),
    ('sizzlesnout', 'DioramaSizzlesnout', 'Cinderling', {
        'Classic': ['C8323A', 'F2A43A', 'FFE0D0', 'FFF07A', 'FFA0B0', 'FFFFFF00'],
        'Sunny':   ['F5924C', 'F5D34A', 'FFEDA6', 'FFE785', 'FFA6A6', 'FFFFFF00'],
        'Deep':    ['94153D', 'D05019', 'BD918E', 'DBA14F', 'E5829D', 'FFFFFF00'],
        'Shimmer': ['F5AFB3', 'F7D9B0', 'FFF3ED', 'FFF9CC', 'FFBDC8', 'B8FFE65E']}),
    ('kindlekoi', 'DioramaKindlekoi', 'Cinderling', {
        'Classic': ['FFE2C4', 'FFC9A0', 'FFFFFF', 'FF7A2E', 'FFA6B0', 'FFFFFF00'],
        'Sunny':   ['FFE993', 'FFEDA6', 'FFF8E3', 'FFD93F', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['BD9284', 'BD7567', 'E5E9F1', 'BD1D0C', 'E5889C', 'FFFFFF00'],
        'Shimmer': ['FFF7EF', 'FFEADB', 'EAFAF9', 'FFCCB0', 'FFC1C8', 'FFD6F05E']}),
    ('sootsnap', 'DioramaSootsnap', 'Cinderling', {
        'Classic': ['5A5560', '3A3640', 'A8A0A8', 'FF6A2E', 'FF9EA8', 'FFFFFF00'],
        'Sunny':   ['A4977D', '685B6B', 'C2B5A6', 'FFD03F', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['6D728C', '2C2D3B', 'A09DAB', 'BD100C', 'E68095', 'FFFFFF00'],
        'Shimmer': ['9FBBBC', 'BEB8C6', 'B2BDC1', 'FFC6B0', 'FFBBC2', 'FFE0B05E']}),
    ('ashfin', 'DioramaAshfin', 'Cinderling', {
        'Classic': ['C9C2BC', 'E8E2DC', 'F6F2EE', 'FF8A3A', 'FFA6B0', 'FFFFFF00'],
        'Sunny':   ['EDDBA9', 'F2E4C1', 'F9EFD7', 'FFDB4A', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['A4A9BA', 'B3B9CA', 'DEDEE3', 'BD2D16', 'E5889C', 'FFFFFF00'],
        'Shimmer': ['C6E1DD', 'D1ECE8', 'E5F2EE', 'FFD3B4', 'FFC1C8', 'FFD0C05E']}),
    ('phoenixfry', 'DioramaPhoenixfry', 'Cinderling', {
        'Classic': ['D9283A', 'FFB23A', 'FFE8C8', 'FFF5A0', 'FFA0B0', 'FFFFFF00'],
        'Sunny':   ['F5803D', 'FFDB4A', 'FFEDA6', 'FFEDA6', 'FFA6A6', 'FFFFFF00'],
        'Deep':    ['A10B42', 'DB5418', 'BD9887', 'DBB06F', 'E5829D', 'FFFFFF00'],
        'Shimmer': ['F5A9B1', 'FFE2B4', 'FFF6EA', 'FFFBDB', 'FFBDC8', 'FFF0B05E']}),
    ('dewdrop', 'DioramaDewdrop', 'Dewling', {
        'Classic': ['A8E6F2', 'D6F6FF', 'F2FDFF', 'F4FFFF', 'FFB0C0', 'FFFFFF00'],
        'Sunny':   ['8DF5C9', 'A6FFE9', 'F6F6E3', 'F9F4D4', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['6F96B3', '92A8BD', 'DBE7F1', 'BAC7DC', 'E691AA', 'FFFFFF00'],
        'Shimmer': ['DAF2F7', 'EFFCFF', 'E2F9F9', 'D5F7F4', 'FFC8D3', 'FFE6F55E']}),
    ('fogfin', 'DioramaFogfin', 'Dewling', {
        'Classic': ['B8B0E0', 'E0DCF4', 'F4F2FC', 'FFFFFF', 'FFB0C8', 'FFFFFF00'],
        'Sunny':   ['DB8DF5', 'EEE1CE', 'F7EFE0', 'FFF4D4', 'FFA6B2', 'FFFFFF00'],
        'Deep':    ['777CA6', 'B0B6D6', 'DDDEEE', 'BFC7DC', 'E691B3', 'FFFFFF00'],
        'Shimmer': ['E4E1F5', 'CEEAF0', 'E3F2F7', 'D9F7F4', 'FFC8D9', 'C8FFE85E']}),
    ('mistlet', 'DioramaMistlet', 'Dewling', {
        'Classic': ['F4B8D8', 'FFE0EE', 'FFF6FA', 'FFFFFF', 'FF9EB8', 'FFFFFF00'],
        'Sunny':   ['F59FA5', 'FFA7A6', 'FFF2DF', 'FFF4D4', 'FFA6AF', 'FFFFFF00'],
        'Deep':    ['B57CA9', 'BD9AB3', 'E5E1ED', 'BFC7DC', 'E680A6', 'FFFFFF00'],
        'Shimmer': ['F9E2EE', 'FFF3F9', 'EAF5F6', 'D9F7F4', 'FFBBCD', 'C8F0FF5E']}),
    ('drizzler', 'DioramaDrizzler', 'Dewling', {
        'Classic': ['7084C8', 'A8C4F0', 'DCE8FF', 'E8FBFF', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['92D6F5', '9FE8F5', 'A6EEFF', 'F2F2D4', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['494694', '6F77B2', '979ABD', 'B3C5DC', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['CCD5F5', 'D9E4F5', 'F2F6FF', 'D1F5F4', 'FFC1D3', 'FFE6C85E']}),
    ('pearlpip', 'DioramaPearlpip', 'Dewling', {
        'Classic': ['F4F0F8', 'E0D4F4', 'FFFFFF', 'FFFBF0', 'FFB0C8', 'FFFFFF00'],
        'Sunny':   ['FAEBC7', 'E79FF5', 'FFF8E3', 'FFF2CC', 'FFA6B2', 'FFFFFF00'],
        'Deep':    ['BAC0D8', '9692B5', 'E5E9F1', 'BFC5D4', 'E691B3', 'FFFFFF00'],
        'Shimmer': ['C5F2EE', 'F1EDF9', 'EAFAF9', 'D9F5EF', 'FFC8D9', 'FFD6F05E']}),
    ('haloling', 'DioramaHaloling', 'Dewling', {
        'Classic': ['FAEAB0', 'FFF4D0', 'FFFCEE', 'FFE070', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['FAE17D', 'FFEDA6', 'FFF6D7', 'FFE57B', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['B99875', 'BDA58E', 'E5E6E3', 'DB8E46', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['FFF9E2', 'FFFBED', 'EAF9EE', 'FFF3C9', 'FFC1CD', 'D0E8FF5E']}),
    ('rainskipper', 'DioramaRainskipper', 'Dewling', {
        'Classic': ['5AAAF0', 'B0DCFF', 'E8F6FF', 'F0FFFF', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['68F5F5', 'A6FFFD', 'EFF2E3', 'F7F4D4', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['3153B2', '7489BD', 'D3E1F1', 'B8C7DC', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['BBDAF5', 'E1F2FF', 'DBF5F9', 'D3F7F4', 'FFC1D3', 'FFE6B05E']}),
    ('rainbowregent', 'DioramaRainbowregent', 'Dewling', {
        'Classic': ['C4AEF4', '7FD3F7', 'FFF8EC', 'FFB0D0', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['EA8DF5', '89F7E5', 'FFF3D5', 'FFABA6', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['7774B5', '4E7BB7', 'E5E3E2', 'BD74A5', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['E6DEF9', 'CEEEFC', 'EAF6ED', 'FFE1ED', 'FFC1D9', 'FFF0B05E']}),
    ('shellback', 'DioramaShellback', 'Reefback', {
        'Classic': ['7FB3A0', 'C9A36B', 'F2EBD8', 'FF8A80', 'FF9EA8', 'FFFFFF00'],
        'Sunny':   ['9FF5AE', 'F5E08B', 'F6EAC7', 'FFCA8A', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['548480', 'AD6647', 'DBD9D2', 'BD4E63', 'E68095', 'FFFFFF00'],
        'Shimmer': ['DAF5EB', 'F5E3C9', 'E2EDE0', 'FFD3CF', 'FFBBC2', 'FFE0C85E']}),
    ('coralhorn', 'DioramaCoralhorn', 'Reefback', {
        'Classic': ['E8735A', '8E5A3C', 'FFE6D0', 'FF5A8C', 'FFA6B0', 'FFFFFF00'],
        'Sunny':   ['F5C46B', 'F5DB73', 'FFEDA6', 'FF8067', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['AC323D', '692922', 'BD978E', 'BD2F80', 'E5889C', 'FFFFFF00'],
        'Shimmer': ['F5C6BC', 'F5D3BF', 'FFF6ED', 'FFC0D3', 'FFC1C8', 'FFD0F05E']}),
    ('clamjaw', 'DioramaClamjaw', 'Reefback', {
        'Classic': ['6E7FA8', 'E8D8C0', 'F6EFE6', 'C88AE8', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['9FE0F5', 'F5E49F', 'F9EDD1', 'F59AE9', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['48497C', 'AC9183', 'DEDCDD', '7958AC', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['D5DEF5', 'F5EEE5', 'E5F0E9', 'E8CFF5', 'FFC1CD', 'E0F0FF5E']}),
    ('kelpgrazer', 'DioramaKelpgrazer', 'Reefback', {
        'Classic': ['5E9E5A', '3E6B3A', 'E6F0C8', 'C8E05A', 'FFA6A0', 'FFFFFF00'],
        'Sunny':   ['C7F594', '82A55D', 'F5E49F', 'F5DA6E', 'FFBAA6', 'FFFFFF00'],
        'Deep':    ['397545', '2C6242', '9CB289', 'C1B036', 'E58286', 'FFFFFF00'],
        'Shimmer': ['CFF5CD', 'CFFFCA', 'F1F5E6', 'EBF5BD', 'FFC1BD', 'F0FFC85E']}),
    ('barnaclebrow', 'DioramaBarnaclebrow', 'Reefback', {
        'Classic': ['9A8C7A', '6A5A4A', 'EDE6DA', 'FFB27A', 'FF9EA0', 'FFFFFF00'],
        'Sunny':   ['F5E49F', 'A49A75', 'F2E6C9', 'FFE785', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['725C53', '5B4035', 'D7D5D3', 'BD5B49', 'E6808D', 'FFFFFF00'],
        'Shimmer': ['F5ECE1', 'FFEEDC', 'DFEAE1', 'FFE2CC', 'FFBBBC', 'FFE8D05E']}),
    ('tidetusk', 'DioramaTidetusk', 'Reefback', {
        'Classic': ['4A4A7A', 'E8E0D0', 'F4F0E8', 'FF7A5A', 'FFA0B0', 'FFFFFF00'],
        'Sunny':   ['C69CF5', 'F2E3BA', 'F7EDD2', 'FFCB67', 'FFA6A6', 'FFFFFF00'],
        'Deep':    ['3D7279', 'B3B8C4', 'DDDDDE', 'BD2F3A', 'E5829D', 'FFFFFF00'],
        'Shimmer': ['D0D0F5', 'D1ECE4', 'E3F1EA', 'FFCCC0', 'FFBDC8', 'E8D8FF5E']}),
    ('wavebreaker', 'DioramaWavebreaker', 'Reefback', {
        'Classic': ['E8C84A', '4A6A8A', 'FFF6D8', 'FF6A6A', 'FFA6A8', 'FFFFFF00'],
        'Sunny':   ['F5C712', '8CF1F5', 'FFEDA6', 'FFB676', 'FFB3A6', 'FFFFFF00'],
        'Deep':    ['C87728', '2E3B66', 'BDA994', 'BD3C5E', 'E58893', 'FFFFFF00'],
        'Shimmer': ['F5E8B5', 'CADFF5', 'FFFCF0', 'FFC6C6', 'FFC1C2', 'C8F0FF5E']}),
    ('coralossus', 'DioramaCoralossus', 'Reefback', {
        'Classic': ['F28A6A', 'B5784A', 'FFF0E0', 'C060E0', 'FFA0B0', 'FFFFFF00'],
        'Sunny':   ['F5CF76', 'F5DA70', 'FFEDA6', 'F574D9', 'FFA6A6', 'FFFFFF00'],
        'Deep':    ['B33D41', '86392A', 'BDA39A', '6D37A6', 'E5829D', 'FFFFFF00'],
        'Shimmer': ['F7CFC2', 'F5D5BE', 'FFF9F3', 'E8C0F5', 'FFBDC8', 'FFE0F85E']}),
    ('brinefin', 'DioramaBrinefin', 'Saltscale', {
        'Classic': ['4A7AC8', '2E4E8A', 'E8F0FA', 'FFFFFF', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['67DFF5', '5FD9F5', 'EFEDDF', 'FFF4D4', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['283494', '171E66', 'D3DDED', 'BFC7DC', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['BAD1F5', 'B7CCF5', 'DBF1F6', 'D9F7F4', 'FFC1D3', 'FFF0D05E']}),
    ('pebblejaw', 'DioramaPebblejaw', 'Saltscale', {
        'Classic': ['8A90A8', '5A5A6E', 'E6E6EE', 'FAFAFF', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['9FD8F5', '988CA9', 'EEE6D7', 'FCF1D4', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['5E6C7C', '495765', 'D1D5E3', 'BDC4DC', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['E4E8F5', 'EAEAFF', 'DAEAEE', 'D7F5F4', 'FFC1CD', 'E8FFF05E']}),
    ('seaspark', 'DioramaSeaspark', 'Saltscale', {
        'Classic': ['2EA8B8', '1E6A8A', 'E0F8FA', 'FFFFF0', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['4CF5BA', '45F5D7', 'E9F3DF', 'FFF4CC', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['135B88', '0B3366', 'CDE3ED', 'BFC7D4', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['AFEDF5', 'ACDFF5', 'D6F6F6', 'D9F7EF', 'FFC1D3', 'FFF8C05E']}),
    ('foamtail', 'DioramaFoamtail', 'Saltscale', {
        'Classic': ['3E5AA8', '9AB8F0', 'EEF4FF', 'FFFFFF', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['67CFF5', '9FE5F5', 'F3F0E3', 'FFF4D4', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['22217C', '646AB2', 'D8E0F1', 'BFC7DC', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['BACAF5', 'D4DFF5', 'DFF3F9', 'D9F7F4', 'FFC1D9', 'D8FFF85E']}),
    ('surfskipper', 'DioramaSurfskipper', 'Saltscale', {
        'Classic': ['4AA8E8', '2E6AB8', 'F0FAFF', 'FFFFFF', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['5BF5EB', '4CE4F5', 'F5F4E3', 'FFF4D4', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['2551AC', '132788', 'D9E5F1', 'BFC7DC', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['B5DBF5', 'AFCDF5', 'E1F7F9', 'D9F7F4', 'FFC1D3', 'FFE8F85E']}),
    ('shoalglider', 'DioramaShoalglider', 'Saltscale', {
        'Classic': ['6A6AB8', '3E3E7A', 'ECECFA', 'FFFFFF', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['C295F5', 'BA86F5', 'F2EBDF', 'FFF4D4', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['435588', '2E4F70', 'D6D9ED', 'BFC7DC', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['CDCDF5', 'C7C7F5', 'DEEEF6', 'D9F7F4', 'FFC1D9', 'D0FFE85E']}),
    ('tidepip', 'DioramaTidepip', 'Saltscale', {
        'Classic': ['5AB8A8', '2E7A7A', 'EAFAF6', 'FFFFFF', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['82F5AC', '69F5B3', 'F0F4DC', 'FFF4D4', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['368088', '1F4870', 'D5E5EA', 'BFC7DC', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['C5F5ED', 'BBF5F5', 'DDF7F3', 'D9F7F4', 'FFC1CD', 'FFE8D05E']}),
    ('pearlemperor', 'DioramaPearlemperor', 'Saltscale', {
        'Classic': ['2E3E7A', 'C8D0F0', 'F4F6FF', 'FFF8F0', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['69C8F5', '9FD8F5', 'F7F2E3', 'FFF0CC', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['4F2176', '899CB2', 'DDE1F1', 'BFC3D4', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['BBC7F5', 'E6E9F5', 'E3F5F9', 'D9F4EF', 'FFC1D9', 'FFF0E05E']}),
    ('moonmouth', 'DioramaMoonmouth', 'Gloamer', {
        'Classic': ['2E3A6E', '1E2648', '8C93C8', 'FFF2B0', 'FF9EC0', 'FFFFFF00'],
        'Sunny':   ['624CA9', '443476', 'BD9FF5', 'FFEDA6', 'FFA6B6', 'FFFFFF00'],
        'Deep':    ['22666B', '153442', '5D7294', 'DBB07D', 'E680AE', 'FFFFFF00'],
        'Shimmer': ['BCC9FF', '9DA8D5', 'D9DCF5', 'FFFAE1', 'FFBBD3', 'FFF0C85E']}),
    ('gloomfin', 'DioramaGloomfin', 'Gloamer', {
        'Classic': ['2A4A4E', '16282C', '7FA8A8', '6AF0FF', 'FF9EB0', 'FFFFFF00'],
        'Sunny':   ['477E6C', '2A5046', '9FF5CD', '76FFCD', 'FFA6A7', 'FFFFFF00'],
        'Deep':    ['3D2555', '101828', '55727C', '3C8DBD', 'E6809D', 'FFFFFF00'],
        'Shimmer': ['B2DCE1', '7C9AA0', 'DEF5F5', 'C6F9FF', 'FFBBC8', 'C8FFF85E']}),
    ('shadeback', 'DioramaShadeback', 'Gloamer', {
        'Classic': ['3A2E4A', '241A30', '9A88B0', 'B8FF6A', 'FFA0B8', 'FFFFFF00'],
        'Sunny':   ['724D78', '513055', 'EE9FF5', 'FEFF76', 'FFA6AD', 'FFFFFF00'],
        'Deep':    ['223E68', '14152C', '635C82', '5DBD3C', 'E582A5', 'FFFFFF00'],
        'Shimmer': ['C4B4D9', '9585A8', 'E9E0F5', 'E4FFC6', 'FFBDCD', 'E8FFC85E']}),
    ('hushray', 'DioramaHushray', 'Gloamer', {
        'Classic': ['5E5C66', '3A3844', 'CACAD8', '9AE8FF', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['A69B80', '695D70', 'DAD3C7', 'A2FFE9', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['6F768F', '2D333F', 'BBBED2', '6390BD', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['A0BEBE', 'C0BDCE', 'C8D8E0', 'D9F6FF', 'FFC1D3', 'D8F0FF5E']}),
    ('duskdrifter', 'DioramaDuskdrifter', 'Gloamer', {
        'Classic': ['4E2E5A', '2E1A38', 'B090C0', 'FFB0E8', 'FFA0C0', 'FFFFFF00'],
        'Sunny':   ['8E4C83', '602F5B', 'F59FE9', 'FFA6BB', 'FFA6B5', 'FFFFFF00'],
        'Deep':    ['242557', '181334', '73618E', 'BD74BB', 'E582AE', 'FFFFFF00'],
        'Shimmer': ['E9C1F8', 'A88BB7', 'EDDEF5', 'FFE1F6', 'FFBDD3', 'FFD8F85E']}),
    ('murkmaw', 'DioramaMurkmaw', 'Gloamer', {
        'Classic': ['2E3A2A', '1C2418', '8A9A7A', 'FFD86A', 'FF9A9A', 'FFFFFF00'],
        'Sunny':   ['5B6349', '40452F', 'F2F59F', 'FFE476', 'FFB2A2', 'FFFFFF00'],
        'Deep':    ['265A4D', '132115', '5A7253', 'DB8541', 'E67D87', 'FFFFFF00'],
        'Shimmer': ['A9BBA4', '82917B', 'EBF5E1', 'FFF0C6', 'FFB8B8', 'FFF0C05E']}),
    ('nightlantern', 'DioramaNightlantern', 'Gloamer', {
        'Classic': ['1E2A4E', '121A34', '7A88B8', 'FFF6C8', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['34667E', '22485B', '9FDAF5', 'FFEDA6', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['4A1752', '150C30', '525088', 'B7BD87', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['A2B2E1', '7C88AF', 'D5DDF5', 'FFFCEA', 'FFC1D9', 'FFF8E05E']}),
    ('eclipseleviathan', 'DioramaEclipseleviathan', 'Gloamer', {
        'Classic': ['1A1A2A', '3A2A5A', '6A6A8A', 'FFC860', 'FF9AB0', 'FFFFFF00'],
        'Sunny':   ['3D314D', '7D468E', 'C79FF5', 'FFE26D', 'FFA2A7', 'FFFFFF00'],
        'Deep':    ['163C43', '1E2753', '475066', 'DB7238', 'E67D9E', 'FFFFFF00'],
        'Shimmer': ['82829C', 'D0BCF8', 'DFDFF5', 'FFEAC3', 'FFB8C8', 'FFE0A05E']}),
    ('starmaw', 'DioramaStarmaw', 'Cometail', {
        'Classic': ['2E5AA8', '1E3A78', 'D8E4FF', 'FFF080', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['51D9F5', '4CCFF5', 'A6EBFF', 'FFE88A', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['151E7C', '22116E', '9496BD', 'DBA354', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['B1CAF5', 'AFC5F5', 'F0F5FF', 'FFF9CF', 'FFC1CD', 'FFF8C85E']}),
    ('meteorback', 'DioramaMeteorback', 'Cometail', {
        'Classic': ['6A4A3A', '4A3428', 'C8B8A8', 'FF8A3A', 'FF9A9A', 'FFFFFF00'],
        'Sunny':   ['A4935D', '786D44', 'F5E49F', 'FFDB4A', 'FFB2A2', 'FFFFFF00'],
        'Deep':    ['6F3262', '441E24', '947B73', 'BD2D16', 'E67D87', 'FFFFFF00'],
        'Shimmer': ['FFDCCB', 'D9BCAC', 'F5EDE6', 'FFD3B4', 'FFB8B8', 'FFE0C05E']}),
    ('nebulafin', 'DioramaNebulafin', 'Cometail', {
        'Classic': ['4AA8C8', '8AD8E8', 'E8F8FF', 'FFB0F0', 'FFA6C0', 'FFFFFF00'],
        'Sunny':   ['67F5D7', '9AF5DA', 'EFF3E3', 'FFA6C4', 'FFA6B1', 'FFFFFF00'],
        'Deep':    ['285C94', '5887AC', 'D3E3F1', 'B774BD', 'E588AC', 'FFFFFF00'],
        'Shimmer': ['BAE6F5', 'CFEEF5', 'DBF6F9', 'FFE1F9', 'FFC1D3', 'FFD8F85E']}),
    ('orbitail', 'DioramaOrbitail', 'Cometail', {
        'Classic': ['E8B04A', '8A5A2A', 'FFF0D0', '8AF0FF', 'FF9A9A', 'FFFFFF00'],
        'Sunny':   ['F5D65B', 'F5D558', 'FFEDA6', '93FFDB', 'FFB2A2', 'FFFFFF00'],
        'Deep':    ['C85F28', '773316', 'BDA18E', '5694BD', 'E67D87', 'FFFFFF00'],
        'Shimmer': ['F5DEB5', 'F5D4B4', 'FFF9ED', 'D3F9FF', 'FFB8B8', 'D0F8FF5E']}),
    ('quasarray', 'DioramaQuasarray', 'Cometail', {
        'Classic': ['3A3A8A', '6A6AC8', 'D0D0F8', 'FFFFFF', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['AF72F5', 'BC8BF5', 'CAA1F8', 'FFF4D4', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['213366', '415794', '8E99B8', 'BFC7DC', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['BFBFF5', 'C9C9F5', 'EEEEFD', 'D9F7F4', 'FFC1D9', 'E8E8FF5E']}),
    ('novatusk', 'DioramaNovatusk', 'Cometail', {
        'Classic': ['2E8A7A', '1E5A50', 'D8F4EC', 'C8FF8A', 'FFA6B0', 'FFFFFF00'],
        'Sunny':   ['5FF595', '348E58', 'E4F0D5', 'FFFF93', 'FFABA6', 'FFFFFF00'],
        'Deep':    ['175F66', '133E53', 'C6E0E2', '71BD56', 'E5889C', 'FFFFFF00'],
        'Shimmer': ['B7F5EA', 'ADF8EB', 'D1F3ED', 'EAFFD3', 'FFC1C8', 'F0FFD05E']}),
    ('cosmocarp', 'DioramaCosmocarp', 'Cometail', {
        'Classic': ['F2F2F8', 'E89AC8', 'FFFFFF', '6A5AE8', 'FFA6B8', 'FFFFFF00'],
        'Sunny':   ['F9ECC7', 'F59FAA', 'FFF8E3', 'BB6BF5', 'FFA6A9', 'FFFFFF00'],
        'Deep':    ['B9C1D8', 'AC65A1', 'E5E9F1', '3244AC', 'E588A4', 'FFFFFF00'],
        'Shimmer': ['C5F2EE', 'F5D6E8', 'EAFAF9', 'C2BCF5', 'FFC1CD', 'E0D8FF5E']}),
    ('celestialwyrm', 'DioramaCelestialwyrm', 'Cometail', {
        'Classic': ['1E3A8A', 'E8C8FF', 'F8F0FF', 'FFE8A0', 'FFA6C8', 'FFFFFF00'],
        'Sunny':   ['45C4F5', 'FFA6FB', 'FAEDE3', 'FFEDA6', 'FFA6B9', 'FFFFFF00'],
        'Deep':    ['0B0B66', '9887BD', 'E0DDF1', 'DBA16F', 'E588B5', 'FFFFFF00'],
        'Shimmer': ['ACBFF5', 'F6EAFF', 'E6F1F9', 'FFF6DB', 'FFC1D9', 'FFF0D05E']}),
]
def slot_name(species_id, slot):
    return 'DK_' + species_id[:1].upper() + species_id[1:] + slot
for sid, _, _, variants in SPECIES:
    for k, slot in enumerate(SLOTS[:5]):  # the species' own five material slots, Classic colours in the review renders
        PALETTE[slot_name(sid, slot)] = variants['Classic'][k]
MAT = {}
def srgb_linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4
for name, code in PALETTE.items():
    mat = bpy.data.materials.new(name)
    rgb = [int(code[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    mat.diffuse_color = (*[srgb_linear(v) for v in rgb], 1)
    MAT[name] = mat

def u(p):
    """Unity point -> Blender point. A reflection, since Unity is left-handed and Blender right-handed."""
    return Vector((-p[0], -p[2], p[1]))

def un(p):
    """Blender point -> Unity point (the inverse of u)."""
    return (-p[0], p[2], -p[1])

def rot(p, euler_deg):
    """Rotate a Unity-space offset by Unity Euler degrees (applied Z, then X, then Y, as Unity does)."""
    x, y, z = (math.radians(a) for a in euler_deg)
    v = Vector(p)
    v = Vector((v.x * math.cos(z) - v.y * math.sin(z), v.x * math.sin(z) + v.y * math.cos(z), v.z))
    v = Vector((v.x, v.y * math.cos(x) - v.z * math.sin(x), v.y * math.sin(x) + v.z * math.cos(x)))
    v = Vector((v.x * math.cos(y) + v.z * math.sin(y), v.y, -v.x * math.sin(y) + v.z * math.cos(y)))
    return v

# ---------------------------------------------------------------- mesh building (Unity space, pure python)
class Part:
    """Accumulates one exported mesh: vertex list, faces and per-face slot index, in Unity space relative to a pivot."""
    def __init__(self, name):
        self.name, self.verts, self.faces, self.slots, self.mats = name, [], [], [], []
    def slot(self, material):
        if material not in self.mats:
            self.mats.append(material)
        return self.mats.index(material)
    def add(self, verts, faces, material):
        base, index = len(self.verts), self.slot(material)
        self.verts += [Vector(v) for v in verts]
        self.faces += [tuple(base + i for i in f) for f in faces]
        self.slots += [index] * len(faces)
        return range(base, base + len(verts))

def sphere_grid(seg, rings):
    """Unit sphere (Y poles) as (verts, faces, params) with params (phi, theta) for each vertex."""
    verts, params = [(0, 1, 0)], [(0, 0)]
    for r in range(1, rings):
        phi = math.pi * r / rings
        for s in range(seg):
            theta = math.tau * s / seg
            verts.append((math.sin(phi) * math.cos(theta), math.cos(phi), math.sin(phi) * math.sin(theta)))
            params.append((phi, theta))
    verts.append((0, -1, 0)); params.append((math.pi, 0))
    faces, bottom = [], len(verts) - 1
    for s in range(seg):
        faces.append((0, 1 + (s + 1) % seg, 1 + s))
    for r in range(rings - 2):
        a, b = 1 + r * seg, 1 + (r + 1) * seg
        for s in range(seg):
            t = (s + 1) % seg
            faces.append((a + s, a + t, b + t, b + s))
    last = 1 + (rings - 2) * seg
    for s in range(seg):
        faces.append((last + s, last + (s + 1) % seg, bottom))
    return verts, faces, params

def ellipsoid(part, center, radii, material, euler=(0, 0, 0), seg=28, rings=16, squarish=1.0):
    """Ellipsoid (squarish > 1 makes a rounded box) rotated by Unity Euler degrees and placed at center."""
    unit, faces, _ = sphere_grid(seg, rings)
    out = []
    for v in unit:
        x, y, z = v
        if squarish != 1.0:
            m = (abs(x) ** squarish + abs(y) ** squarish + abs(z) ** squarish) ** (1 / squarish)
            m = max(m, 1e-6)
            x, y, z = x / m, y / m, z / m
        p = rot((x * radii[0], y * radii[1], z * radii[2]), euler)
        out.append((p.x + center[0], p.y + center[1], p.z + center[2]))
    return part.add(out, faces, material)

def loft_z(part, z0, z1, rings, seg, profile, material, belly=None, yoff=lambda t: 0):
    """Closed surface along Z. profile(t) -> (rx, ry_top, ry_bottom); poles at both ends."""
    verts, faces, slot_override = [], [], []
    verts.append((0, yoff(0), z0))
    for r in range(1, rings):
        t = r / rings
        rx, ryt, ryb = profile(t)
        z = z0 + (z1 - z0) * t
        for s in range(seg):
            a = math.tau * s / seg
            c, sn = math.cos(a), math.sin(a)
            verts.append((rx * c, yoff(t) + (ryt if sn > 0 else ryb) * sn, z))
    verts.append((0, yoff(1), z1))
    end = len(verts) - 1
    for s in range(seg):
        faces.append((0, 1 + s, 1 + (s + 1) % seg))
    for r in range(rings - 2):
        a, b = 1 + r * seg, 1 + (r + 1) * seg
        for s in range(seg):
            t = (s + 1) % seg
            faces.append((a + s, b + s, b + t, a + t))
    last = 1 + (rings - 2) * seg
    for s in range(seg):
        faces.append((last + s, end, last + (s + 1) % seg))
    rng = part.add(verts, faces, material)
    if belly:
        # Faces pointing low enough (and away from the very ends) take the belly slot. The test uses the face's angle
        # around the body, which is constant along a segment, so the edge follows the mesh lines instead of a staircase.
        bslot = part.slot(belly[0])
        first_face = len(part.faces) - len(faces)
        for fi, f in enumerate(faces):
            zs = [verts[i][2] for i in f]
            tmid = (sum(zs) / len(zs) - z0) / (z1 - z0)
            mx = sum(verts[i][0] for i in f) / len(f)
            my = sum(verts[i][1] for i in f) / len(f) - yoff(tmid)
            if belly[1](math.sin(math.atan2(my, mx)), tmid):
                part.slots[first_face + fi] = bslot
    return rng

def paint(part, first_face, center, radii, slot, test):
    """Give faces from first_face on the slot when test(direction) holds; direction is the face centre's unit vector in the
    ellipsoid's own frame, so tests on its y (latitude) or its angle about Y (longitude) follow the grid lines."""
    index = part.slot(slot)
    for fi in range(first_face, len(part.faces)):
        vs = [part.verts[i] for i in part.faces[fi]]
        c = Vector((sum(v.x for v in vs), sum(v.y for v in vs), sum(v.z for v in vs))) / len(vs)
        n = Vector(((c.x - center[0]) / radii[0], (c.y - center[1]) / radii[1], (c.z - center[2]) / radii[2]))
        if n.length > 1e-6 and test(n.normalized()):
            part.slots[fi] = index

def shell(part, center, radii, material, euler, phi_max, thickness=.012, seg=24, rows=8):
    """A cap of an ellipsoid from its top pole down to phi_max (radians), with thickness. Returns (indices, builder)."""
    def build(phi_limit):
        verts = []
        for layer, scale in ((0, 1.0), (1, 1.0 - thickness / max(radii))):
            verts.append((0, radii[1] * scale, 0))
            for r in range(1, rows + 1):
                phi = phi_limit * r / rows
                for s in range(seg):
                    th = math.tau * s / seg
                    verts.append((math.sin(phi) * math.cos(th) * radii[0] * scale, math.cos(phi) * radii[1] * scale,
                                  math.sin(phi) * math.sin(th) * radii[2] * scale))
        out = []
        for v in verts:
            p = rot(v, euler)
            out.append((p.x + center[0], p.y + center[1], p.z + center[2]))
        return out
    n = 1 + rows * seg
    faces = []
    for layer in (0, 1):
        o = layer * n
        for s in range(seg):
            f = (o, o + 1 + (s + 1) % seg, o + 1 + s)
            faces.append(f if layer == 0 else f[::-1])
        for r in range(rows - 1):
            a, b = o + 1 + r * seg, o + 1 + (r + 1) * seg
            for s in range(seg):
                t = (s + 1) % seg
                f = (a + s, a + t, b + t, b + s)
                faces.append(f if layer == 0 else f[::-1])
    rim_o, rim_i = 1 + (rows - 1) * seg, n + 1 + (rows - 1) * seg
    for s in range(seg):
        t = (s + 1) % seg
        faces.append((rim_o + s, rim_o + t, rim_i + t, rim_i + s))
    idx = part.add(build(phi_max), faces, material)
    return idx, build

def torus(part, center, major, minor, material, scale=(1, 1, 1), seg=36, sides=12, euler=(0, 0, 0)):
    """Ring around the Z axis (faces -Z/+Z)."""
    verts, faces = [], []
    for s in range(seg):
        a = math.tau * s / seg
        for j in range(sides):
            b = math.tau * j / sides
            r = major + minor * math.cos(b)
            p = rot((r * math.cos(a) * scale[0], r * math.sin(a) * scale[1], minor * math.sin(b) * scale[2]), euler)
            verts.append((p.x + center[0], p.y + center[1], p.z + center[2]))
    for s in range(seg):
        for j in range(sides):
            a, b = s * sides + j, s * sides + (j + 1) % sides
            c, d = ((s + 1) % seg) * sides + (j + 1) % sides, ((s + 1) % seg) * sides + j
            faces.append((a, b, c, d))
    return part.add(verts, faces, material)

def tube(part, points, radius, material, sides=10):
    verts = []
    for i, p in enumerate(points):
        c = Vector(p)
        tan = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
        side = tan.cross(Vector((0, 1, 0)))
        if side.length < .01:
            side = tan.cross(Vector((1, 0, 0)))
        side.normalize(); other = tan.cross(side).normalized()
        for k in range(sides):
            a = math.tau * k / sides
            verts.append(tuple(c + radius * (math.cos(a) * side + math.sin(a) * other)))
    faces = []
    for r in range(len(points) - 1):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append((r * sides + k, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + k))
    faces += [tuple(range(sides - 1, -1, -1)), tuple((len(points) - 1) * sides + k for k in range(sides))]
    return part.add(verts, faces, material)

# ---------------------------------------------------------------- objects, pivots and shape keys
RECORDS = []
LIFT = Vector((0, 0, 0))

def make_object(part, root, pivot=(0, 0, 0), name=None):
    """Mesh object directly under the root whose origin is the pivot (Unity space); vertices are in root space."""
    me = bpy.data.meshes.new(part.name)
    px = Vector(pivot)
    me.from_pydata([u(Vector(v) - px) for v in part.verts], [], part.faces)
    for m in part.mats:
        me.materials.append(MAT[m])
    for poly, s in zip(me.polygons, part.slots):
        poly.material_index = s
    me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    me.shade_smooth()
    obj = bpy.data.objects.new(name or part.name, me)
    COLLECTION.objects.link(obj)
    obj.parent = root
    obj.location = u(px + LIFT)
    return obj

def empty(name, pivot=(0, 0, 0), root=None):
    """An empty (the asset root, or a joint with no mesh of its own directly under the root)."""
    obj = bpy.data.objects.new(name, None)
    COLLECTION.objects.link(obj)
    obj.empty_display_size = .05
    if root is not None:
        obj.parent = root
    obj.location = u(Vector(pivot) + (LIFT if root is not None else Vector((0, 0, 0))))
    return obj

def shape_key(obj, part, name, fn, indices=None, pivot=(0, 0, 0)):
    """fn(index, unity_root_vertex) -> new unity_root_vertex for the given vertex indices."""
    if obj.data.shape_keys is None:
        obj.shape_key_add(name='Basis', from_mix=False)
    key = obj.shape_key_add(name=name, from_mix=False)
    key.value = 0.0  # Blender 5.0 adds new keys at full strength; the rest pose is the basis
    px = Vector(pivot)
    for i in (indices if indices is not None else range(len(part.verts))):
        key.data[i].co = u(Vector(fn(i, part.verts[i])) - px)
    return key

def stats(root, kind):
    tris, verts = 0, 0
    mins, maxs = [1e9] * 3, [-1e9] * 3
    for obj in [root] + list(root.children_recursive):
        if obj.type != 'MESH':
            continue
        tris += sum(len(p.vertices) - 2 for p in obj.data.polygons)
        verts += len(obj.data.vertices)
        for v in obj.data.vertices:
            w = un(obj.matrix_world @ v.co)
            for k in range(3):
                mins[k] = min(mins[k], w[k]); maxs[k] = max(maxs[k], w[k])
    return {'triangles': tris, 'vertices': verts, 'bounds_unity_m': {'min': mins, 'max': maxs, 'size': [maxs[k] - mins[k] for k in range(3)]}}

# ================================================================ creatures (front -Z): the family rigs
# Every creature shares one rig: Body (Puffed), Eyes, Pupils, Glints, Lids (Blink, Happy), Blush and Lips (Happy, Surprised),
# and the four pivots the game's swim code drives (Left fin pivot, Right fin pivot, Tail pivot, Crest pivot). A family adds
# its signature part (Sparkline: Lantern pivot). Species differ by body, fins, colours and that feature.

def creature_root(asset):
    root = empty(asset)
    root['front'] = 'creature local -Z'
    return root

def creature_face(root, sid, eye_c, eye_r, eye_yaw, blush_c, blush_r, blush_yaw, lip_c, lip_major=.046, lip_minor=.025,
                  lip_scale=(1.22, .82, 1.0), pupil_k=.6, smile=.03):
    """Eyes a third of the head's height with pupils, glints and lids, blush, and pouty lips with a dark mouth."""
    S = lambda slot: slot_name(sid, slot)
    eyes, pupils, glints, lids, blush = Part('Eyes'), Part('Pupils'), Part('Glints'), Part('Lids'), Part('Blush')
    lid_builders = []
    for side in (-1, 1):
        c = (side * eye_c[0], eye_c[1], eye_c[2])
        yaw = -side * eye_yaw  # positive Unity yaw turns -Z toward -X, so the right eye (+X) takes a negative yaw
        ellipsoid(eyes, c, eye_r, 'DK_EyeWhite', (0, yaw, 0), 24, 14)
        fwd = rot((0, 0, -1), (0, yaw, 0))
        pr = (eye_r[0] * pupil_k, eye_r[1] * pupil_k * 1.06, eye_r[2] * .5)
        pc = Vector(c) + fwd * (eye_r[2] * .72) + Vector((0, -.004, 0))
        ellipsoid(pupils, pc, pr, 'DK_Pupil', (0, yaw, 0), 20, 12)
        gc = Vector(c) + fwd * (eye_r[2] * 1.15) + Vector((-side * .006 - eye_r[0] * .16, eye_r[1] * .29, 0))
        ellipsoid(glints, gc, (eye_r[0] * .22, eye_r[1] * .2, eye_r[2] * .17), 'DK_Glint', (0, yaw, 0), 12, 8)
        lid_r = (eye_r[0] * 1.07, eye_r[1] * 1.06, eye_r[2] * 1.08)
        idx, build = shell(lids, c, lid_r, S('Body'), (0, yaw, 0), math.radians(24), .010, 24, 8)
        lid_builders.append((idx, build))
        ellipsoid(blush, (side * blush_c[0], blush_c[1], blush_c[2]), blush_r, S('Blush'), (0, -side * blush_yaw, 0), 16, 8)
    for p in (eyes, pupils, glints, blush):
        make_object(p, root)
    lids_obj = make_object(lids, root)
    for name, angle in (('Blink', 104), ('Happy', 58)):
        moved = {}
        for idx, build in lid_builders:
            for i, v in zip(idx, build(math.radians(angle))):
                moved[i] = v
        shape_key(lids_obj, lids, name, lambda i, v, moved=moved: moved[i])
    lips = Part('Lips')
    lc = Vector(lip_c)
    torus(lips, lc, lip_major, lip_minor, S('Accent') if sid == 'pondlet' else S('Blush'), lip_scale, 32, 12)
    ellipsoid(lips, lc + Vector((0, 0, lip_minor * .5)), (lip_major * .83, lip_minor * .96, .012), 'DK_Mouth', (0, 0, 0), 16, 8)
    lips_obj = make_object(lips, root)
    def happy(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * 1.18, d.y * .8 + smile * (d.x / (lip_major * 1.5)) ** 2 - .006, d.z))
    def surprised(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * 1.05, d.y * 1.55, d.z))
    shape_key(lips_obj, lips, 'Happy', happy)
    shape_key(lips_obj, lips, 'Surprised', surprised)

def puff_key(body_obj, body, z0, z1, gain=.16, yoff=lambda t: 0, center_y=None):
    def puffed(i, v):
        t = (v[2] - z0) / (z1 - z0)
        g = 1 + gain * math.sin(math.pi * min(1, max(0, t)) ** .8)
        if center_y is not None:
            return (v[0] * g, center_y + (v[1] - center_y) * g, v[2] * (1 + (g - 1) * .35))
        return (v[0] * g, yoff(t) + (v[1] - yoff(t)) * g, v[2])
    shape_key(body_obj, body, 'Puffed', puffed)

# ---------------------------------------------------------------- Brookkin: Pondlet (the AD-A model, unchanged)
def pondlet():
    sid = 'pondlet'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaPondlet')
    Z0, Z1 = -.37, .38
    def prof(t):
        if t < .42:
            f = math.sqrt(max(0, 1 - (1 - t / .42) ** 2))
        else:
            s = (t - .42) / .58
            f = 1 - .78 * (s * s * (3 - 2 * s))
        return .27 * f, .245 * f, .215 * f
    yoff = lambda t: .015 * t * t
    body = Part('Body')
    loft_z(body, Z0, Z1, 30, 36, prof, S('Body'), belly=(S('Belly'), lambda sn, t: sn < -.5 and .06 < t < .86), yoff=yoff)
    body_obj = make_object(body, root)
    puff_key(body_obj, body, Z0, Z1, .16, yoff)
    creature_face(root, sid, (.132, .072, -.262), (.088, .104, .072), 32, (.208, -.03, -.228), (.046, .028, .016), 52,
                  (0, -.032, -.382))
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .205, -.07, -.04)
        fin = Part(label + ' fin')
        ellipsoid(fin, (pivot[0] + side * .05, pivot[1] - .045, pivot[2] + .09), (.034, .078, .13), S('Fin'), (0, side * 18, side * -38), 20, 12)
        make_object(fin, root, pivot, label + ' fin pivot')
    tail_pivot = (0, .012, .33)
    tail = Part('Tail')
    ellipsoid(tail, (0, tail_pivot[1], tail_pivot[2] + .03), (.05, .07, .08), S('Fin'), (0, 0, 0), 16, 10)
    for side in (-1, 1):
        ellipsoid(tail, (0, tail_pivot[1] + side * .105, tail_pivot[2] + .165), (.042, .155, .19), S('Fin'), (side * 36, 0, 0), 22, 14)
    make_object(tail, root, tail_pivot, 'Tail pivot')
    crest_pivot = (0, .2, -.01)
    crest = Part('Crest')
    ellipsoid(crest, (0, crest_pivot[1] + .045, crest_pivot[2] + .07), (.034, .085, .155), S('Fin'), (24, 0, 0), 20, 12)
    make_object(crest, root, crest_pivot, 'Crest pivot')
    return root

# ---------------------------------------------------------------- Brookkin: Bubblebelly, a round ball with a freckled belly
def bubblebelly():
    sid = 'bubblebelly'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaBubblebelly')
    body = Part('Body')
    R = (.38, .36, .40)
    first = len(body.faces)
    ellipsoid(body, (0, 0, 0), R, S('Body'), (0, 0, 0), 40, 26)
    # The belly: the lower part of the ball, all round, takes the cream slot.
    paint(body, first, (0, 0, 0), R, S('Belly'), lambda n: n.y < -.22)
    # Three freckles on the belly, just proud of the surface.
    for k in (-1, 0, 1):
        a, e = math.radians(k * 22), math.radians(-34)
        n = Vector((math.sin(a) * math.cos(e), math.sin(e), -math.cos(a) * math.cos(e)))
        p = Vector((n.x * R[0], n.y * R[1], n.z * R[2])) * 1.004
        ellipsoid(body, tuple(p), (.034, .036, .012), S('Accent'), (math.degrees(e) * .9, math.degrees(-a), 0), 14, 8)
    body_obj = make_object(body, root)
    def puffed(i, v):
        # Its trick: a balloon belly, rounder and a fifth bigger, swelling most below the middle.
        g = 1.2 + (.08 if v.y < 0 else 0) * min(1, -v.y / .3)
        return (v.x * g, v.y * (g + .02), v.z * g)
    shape_key(body_obj, body, 'Puffed', puffed)
    creature_face(root, sid, (.15, .1, -.31), (.098, .118, .078), 28, (.25, -.035, -.30), (.05, .03, .016), 46,
                  (0, -.045, -.405), .036, .021, (1.15, .95, 1.0), .62, .024)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .35, -.09, -.02)
        fin = Part(label + ' fin')
        ellipsoid(fin, (pivot[0] + side * .035, pivot[1] - .02, pivot[2] + .06), (.03, .07, .1), S('Fin'), (0, side * 20, side * -30), 18, 10)
        make_object(fin, root, pivot, label + ' fin pivot')
    tail_pivot = (0, .0, .37)
    tail = Part('Tail')
    ellipsoid(tail, (0, 0, tail_pivot[2] + .02), (.045, .06, .06), S('Fin'), (0, 0, 0), 14, 8)
    for side in (-1, 1):
        ellipsoid(tail, (0, side * .07, tail_pivot[2] + .1), (.036, .1, .12), S('Fin'), (side * 32, 0, 0), 18, 12)
    make_object(tail, root, tail_pivot, 'Tail pivot')
    crest_pivot = (0, .34, -.02)
    crest = Part('Crest')
    # A little curl of a topknot.
    ellipsoid(crest, (0, crest_pivot[1] + .045, crest_pivot[2] + .02), (.03, .07, .08), S('Fin'), (-18, 0, 0), 16, 10)
    ellipsoid(crest, (0, crest_pivot[1] + .095, crest_pivot[2] + .065), (.026, .045, .05), S('Fin'), (40, 0, 0), 14, 8)
    make_object(crest, root, crest_pivot, 'Crest pivot')
    return root

# ---------------------------------------------------------------- Brookkin: Ribbonfin, slender with long ribbons
def ribbonfin():
    sid = 'ribbonfin'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaRibbonfin')
    Z0, Z1 = -.44, .36
    def prof(t):
        if t < .36:
            f = math.sqrt(max(0, 1 - (1 - t / .36) ** 2))
        else:
            s = (t - .36) / .64
            f = 1 - .72 * (s * s * (3 - 2 * s))
        return .2 * f, .225 * f, .19 * f
    body = Part('Body')
    # Two dark bands across the back are its markings: whole rings of the loft (rings 15-16 and 21-22 of 32), above the belly.
    def mark(sn, t):
        return sn < -.48 and .07 < t < .84
    loft_z(body, Z0, Z1, 32, 32, prof, S('Body'), belly=(S('Belly'), mark))
    band = body.slot(S('Accent'))
    for fi, f in enumerate(body.faces):
        if body.slots[fi] != body.slot(S('Body')):
            continue
        t = (sum(body.verts[i].z for i in f) / len(f) - Z0) / (Z1 - Z0)
        if .455 < t < .52 or .64 < t < .705:
            body.slots[fi] = band
    body_obj = make_object(body, root)
    puff_key(body_obj, body, Z0, Z1, .14)
    creature_face(root, sid, (.112, .075, -.29), (.076, .092, .062), 30, (.16, -.03, -.29), (.04, .024, .014), 50,
                  (0, -.035, -.445), .036, .02, (1.2, .85, 1.0), .6, .026)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .17, -.07, -.06)
        fin = Part(label + ' fin')
        ellipsoid(fin, (pivot[0] + side * .03, pivot[1] - .05, pivot[2] + .14), (.022, .05, .17), S('Fin'), (12, side * 14, side * -32), 18, 12)
        make_object(fin, root, pivot, label + ' fin pivot')
    tail_pivot = (0, .01, .33)
    tail = Part('Tail')
    ellipsoid(tail, (0, .01, tail_pivot[2] + .02), (.035, .05, .06), S('Fin'), (0, 0, 0), 14, 8)
    for side in (-1, 1):
        ellipsoid(tail, (0, .01 + side * .085, tail_pivot[2] + .28), (.026, .07, .32), S('Fin'), (side * 16, 0, 0), 20, 14)
    make_object(tail, root, tail_pivot, 'Tail pivot')
    crest_pivot = (0, .205, -.12)
    crest = Part('Crest')
    ellipsoid(crest, (0, crest_pivot[1] + .06, crest_pivot[2] + .22), (.022, .07, .3), S('Fin'), (14, 0, 0), 20, 14)
    make_object(crest, root, crest_pivot, 'Crest pivot')
    return root

# ---------------------------------------------------------------- Sparkline: Lanternfin, compact with a glowing lantern
def lanternfin():
    sid = 'lanternfin'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaLanternfin')
    body = Part('Body')
    first = len(body.faces)
    ellipsoid(body, (0, 0, 0), (.29, .27, .39), S('Body'), (0, 0, 0), 36, 22, 2.5)
    paint(body, first, (0, 0, 0), (.29, .27, .39), S('Belly'), lambda n: n.y < -.42)
    body_obj = make_object(body, root)
    def puffed(i, v):
        return (v.x * 1.13, v.y * 1.15, v.z * 1.05)
    shape_key(body_obj, body, 'Puffed', puffed)
    creature_face(root, sid, (.185, .075, -.25), (.094, .112, .074), 34, (.24, -.06, -.24), (.046, .027, .015), 54,
                  (0, -.075, -.402), .034, .02, (1.25, .8, 1.0), .6, .03)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .26, -.03, .0)
        fin = Part(label + ' fin')
        # Rounded wing fins that lift outward.
        ellipsoid(fin, (pivot[0] + side * .1, pivot[1] + .03, pivot[2] + .04), (.15, .03, .17), S('Fin'), (0, side * 10, side * 24), 22, 12)
        make_object(fin, root, pivot, label + ' fin pivot')
    tail_pivot = (0, 0, .36)
    tail = Part('Tail')
    ellipsoid(tail, (0, 0, tail_pivot[2] + .1), (.035, .13, .13), S('Fin'), (0, 0, 0), 18, 12)
    make_object(tail, root, tail_pivot, 'Tail pivot')
    crest_pivot = (0, .245, .08)
    crest = Part('Crest')
    ellipsoid(crest, (0, crest_pivot[1] + .04, crest_pivot[2] + .06), (.026, .065, .11), S('Fin'), (20, 0, 0), 16, 10)
    make_object(crest, root, crest_pivot, 'Crest pivot')
    # The family's signature: a stalk curling up and forward to a glowing bulb.
    lantern_pivot = (0, .245, -.17)
    lantern = Part('Lantern')
    pts = [(0, .245 + .25 * math.sin(math.radians(a)) - .02, -.17 - .16 * (1 - math.cos(math.radians(a)))) for a in range(0, 101, 10)]
    tube(lantern, pts, .02, S('Body'), 10)
    bulb = Vector(pts[-1]) + Vector((0, .02, -.065))
    ellipsoid(lantern, tuple(bulb), (.08, .085, .08), S('Accent'), (0, 0, 0), 24, 14)
    ellipsoid(lantern, tuple(bulb + Vector((-.025, .035, -.055))), (.022, .02, .014), 'DK_Glint', (0, 0, 0), 10, 6)
    make_object(lantern, root, lantern_pivot, 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Airfin: Driftwing, a lilac glider with pearl sails
def driftwing():
    sid = 'driftwing'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaDriftwing')
    Z0, Z1 = -.36, .36
    def prof(t):
        if t < .4:
            f = math.sqrt(max(0, 1 - (1 - t / .4) ** 2))
        else:
            s = (t - .4) / .6
            f = 1 - .8 * (s * s * (3 - 2 * s))
        return .24 * f, .23 * f, .19 * f
    body = Part('Body')
    loft_z(body, Z0, Z1, 30, 32, prof, S('Body'), belly=(S('Belly'), lambda sn, t: sn < -.48 and .08 < t < .84))
    body_obj = make_object(body, root)
    puff_key(body_obj, body, Z0, Z1, .15)
    creature_face(root, sid, (.118, .075, -.24), (.082, .098, .066), 32, (.18, -.03, -.235), (.043, .025, .014), 50,
                  (0, -.04, -.37), .034, .019, (1.2, .8, 1.0), .6, .026)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .2, .03, -.03)
        sail = Part(label + ' fin')
        c = Vector((pivot[0] + side * .25, pivot[1] + .03, pivot[2] + .05))
        first = len(sail.faces)
        ellipsoid(sail, tuple(c), (.27, .03, .19), S('Fin'), (0, side * -8, side * 10), 36, 16)
        # The sail's rim, round its edge, in the accent colour (the grid's equator rings, so the band is clean).
        rim = sail.slot(S('Accent'))
        rows, seg = 16, 36
        for fi in range(first, len(sail.faces)):
            ring = 0 if fi - first < seg else 1 + (fi - first - seg) // seg
            if 6 <= ring <= 9:
                sail.slots[fi] = rim
        make_object(sail, root, pivot, label + ' fin pivot')
    tail_pivot = (0, .02, .32)
    tail = Part('Tail')
    for side in (-1, 1):
        ellipsoid(tail, (side * .06, .02, tail_pivot[2] + .2), (.03, .06, .24), S('Fin'), (0, side * 12, 0), 18, 12)
    make_object(tail, root, tail_pivot, 'Tail pivot')
    crest_pivot = (0, .2, -.02)
    crest = Part('Crest')
    ellipsoid(crest, (0, crest_pivot[1] + .06, crest_pivot[2] + .1), (.02, .095, .16), S('Accent'), (26, 0, 0), 20, 12)
    make_object(crest, root, crest_pivot, 'Crest pivot')
    return root

# ================================================================ GR-12 wave 1: the Brookkin, Sparkline and Airfin to eight each
# Every species is built on its family's rig (the same parts, pivots and shape keys as AD-B's) with the helpers below. A
# family keeps its look (Brookkin: round river fish with pouty lips; Sparkline: a glowing lantern on a stalk; Airfin: wide
# gliding sails); its species differ by body, fins, markings and colours. Crowns are grander: bigger, more parts, a crown.

def nose_profile(nose, taper, rx, ryt, ryb):
    """A loft body's profile: a round dome to the nose fraction, then a smooth taper to the tail (AD-B's fish shape)."""
    def prof(t):
        if t < nose:
            f = math.sqrt(max(0, 1 - (1 - t / nose) ** 2))
        else:
            s = (t - nose) / (1 - nose)
            f = 1 - taper * (s * s * (3 - 2 * s))
        return rx * f, ryt * f, ryb * f
    return prof

def on_body(prof, Z0, Z1, t, angle, k=1.0, yoff=lambda t: 0):
    """A point on a loft body at its length fraction t and angle round it (degrees; 0 the right side, 90 the top), pulled in to
    k of the radius."""
    rx, ryt, ryb = prof(t)
    a = math.radians(angle); c, s = math.cos(a), math.sin(a)
    return Vector((rx * c * k, yoff(t) + (ryt if s > 0 else ryb) * s * k, Z0 + (Z1 - Z0) * t))

def on_ellipsoid(R, az, el, k=1.0, center=(0, 0, 0)):
    """A point on an ellipsoid body (front -Z): az degrees from the front toward the right side, el degrees up."""
    a, e = math.radians(az), math.radians(el)
    return Vector((center[0] + R[0] * math.cos(e) * math.sin(a) * k, center[1] + R[1] * math.sin(e) * k,
                   center[2] - R[2] * math.cos(e) * math.cos(a) * k))

def repaint(part, first_face, Z0, Z1, slot, test, yoff=lambda t: 0):
    """Give a loft body's faces from first_face on the slot when test(sin, t, cos) holds: sin and cos of the face's angle round
    the body (sin 1 the top, cos 1 the right side) and t its length fraction, so marks follow the mesh lines."""
    index = part.slot(slot)
    for fi in range(first_face, len(part.faces)):
        f = part.faces[fi]
        t = (sum(part.verts[i].z for i in f) / len(f) - Z0) / (Z1 - Z0)
        mx = sum(part.verts[i].x for i in f) / len(f)
        my = sum(part.verts[i].y for i in f) / len(f) - yoff(t)
        a = math.atan2(my, mx)
        if test(math.sin(a), t, math.cos(a)):
            part.slots[fi] = index

def near(part, first_face, slot, points, radius):
    """Give faces from first_face whose centre is within radius of any of the points the slot (spots and patches)."""
    index = part.slot(slot)
    for fi in range(first_face, len(part.faces)):
        vs = [part.verts[i] for i in part.faces[fi]]
        c = Vector((sum(v.x for v in vs), sum(v.y for v in vs), sum(v.z for v in vs))) / len(vs)
        if any((c - Vector(p)).length < radius for p in points):
            part.slots[fi] = index

def cone(part, base, tip, radius, material, sides=12, rings=5, bulge=.25):
    """A rounded cone from a base circle to a tip (ears, flame tongues, spikes and star rays), closed at the base."""
    base, tip = Vector(base), Vector(tip)
    axis = (tip - base).normalized()
    side = axis.cross(Vector((0, 1, 0)))
    if side.length < .01:
        side = axis.cross(Vector((1, 0, 0)))
    side.normalize(); other = axis.cross(side).normalized()
    verts, faces = [], []
    for r in range(rings):
        s = r / rings
        rad = radius * (1 - s) * (1 + bulge * math.sin(math.pi * s))
        c = base + (tip - base) * s
        for k in range(sides):
            a = math.tau * k / sides
            verts.append(tuple(c + rad * (math.cos(a) * side + math.sin(a) * other)))
    verts.append(tuple(tip)); verts.append(tuple(base))
    top, bottom = len(verts) - 2, len(verts) - 1
    for r in range(rings - 1):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append((r * sides + k, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + k))
    last = (rings - 1) * sides
    for k in range(sides):
        faces.append((last + k, last + (k + 1) % sides, top))
        faces.append(((k + 1) % sides, k, bottom))
    return part.add(verts, faces, material)

def taper_tube(part, points, r0, r1, material, sides=10):
    """A tube along points whose radius goes from r0 to r1 (whiskers, streamers and whip tails), with a round tip."""
    verts = []
    n = len(points)
    for i, p in enumerate(points):
        c = Vector(p)
        tan = (Vector(points[min(i + 1, n - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
        side = tan.cross(Vector((0, 1, 0)))
        if side.length < .01:
            side = tan.cross(Vector((1, 0, 0)))
        side.normalize(); other = tan.cross(side).normalized()
        r = r0 + (r1 - r0) * i / (n - 1)
        for k in range(sides):
            a = math.tau * k / sides
            verts.append(tuple(c + r * (math.cos(a) * side + math.sin(a) * other)))
    faces = []
    for r in range(n - 1):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append((r * sides + k, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + k))
    faces.append(tuple(range(sides - 1, -1, -1)))
    faces.append(tuple((n - 1) * sides + k for k in range(sides)))
    part.add(verts, faces, material)
    ellipsoid(part, points[-1], (r1 * 1.6, r1 * 1.6, r1 * 1.6), material, (0, 0, 0), 10, 6)

def side_fins(root, sid, pivot, pieces):
    """The Left and Right fin pivots, mirrored. pivot and each piece are given for the right fin (+X): a piece is
    (offset from the pivot, radii, Unity Euler degrees, slot[, squarish]); a rim adds the accent band round a sail's edge."""
    S = lambda slot: slot_name(sid, slot)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        p = (side * pivot[0], pivot[1], pivot[2])
        fin = Part(label + ' fin')
        for piece in pieces:
            off, radii, eul, slot = piece[:4]
            squarish = piece[4] if len(piece) > 4 else 1.0
            rim = piece[5] if len(piece) > 5 else None
            first = len(fin.faces)
            seg, rings = (36, 16) if rim else (22, 12)
            ellipsoid(fin, (p[0] + side * off[0], p[1] + off[1], p[2] + off[2]), radii, S(slot),
                      (eul[0], side * eul[1], side * eul[2]), seg, rings, squarish)
            if rim:
                band = fin.slot(S(rim))
                for fi in range(first, len(fin.faces)):
                    ring = 0 if fi - first < seg else 1 + (fi - first - seg) // seg
                    if 6 <= ring <= 9:
                        fin.slots[fi] = band
        make_object(fin, root, p, label + ' fin pivot')

def flame(part, base, tip, radius, slot, glow):
    """A flame tongue: a teardrop from base to tip, its tip in the glowing accent."""
    base, tip = Vector(base), Vector(tip)
    cone(part, tuple(base), tuple(tip), radius, slot, 12, 7, .9)
    cone(part, tuple(base + (tip - base) * .5), tuple(tip + (tip - base) * .05), radius * .74, glow, 12, 6, .9)

def gold_crown(part, center, radius, points, height, slot, tilt=0):
    """A little crown: a band with points, a ball on each (the crowns' sign of rank)."""
    c = Vector(center)
    torus(part, c, radius, radius * .2, slot, (1, 1, 1), 32, 10, (90 + tilt, 0, 0))
    for k in range(points):
        a = math.tau * (k + .5) / points
        base = c + rot((radius * math.cos(a), 0, radius * math.sin(a)), (tilt, 0, 0))
        up = rot((0, height, 0), (tilt, 0, 0))
        cone(part, base, base + up, radius * .3, slot, 10, 4, .1)
        ellipsoid(part, tuple(base + up * 1.08), (radius * .16,) * 3, slot, (0, 0, 0), 10, 6)

def lantern_on_stalk(root, sid, base, rise, reach, bulb, stalk=.02, bend=100, kind='round'):
    """The Sparkline's signature: a stalk curling up and forward from base to a glowing bulb (the species' accent). kind:
    'round', 'flame' (a teardrop), 'star' (a bulb with five rays) or 'fizz' (three little bulbs)."""
    S = lambda slot: slot_name(sid, slot)
    lantern = Part('Lantern')
    pts = [(base[0], base[1] + rise * math.sin(math.radians(a)), base[2] - reach * (1 - math.cos(math.radians(a))))
           for a in range(0, bend + 1, 10)]
    tube(lantern, pts, stalk, S('Body'), 10)
    end = Vector(pts[-1])
    head = end + Vector((0, bulb * .25, -bulb * .8))
    if kind == 'flame':
        ellipsoid(lantern, tuple(head + Vector((0, bulb * .25, 0))), (bulb * .8, bulb * 1.25, bulb * .8), S('Accent'), (0, 0, 0), 22, 14)
    elif kind == 'fizz':
        for off in ((0, 0, 0), (-bulb * .95, -bulb * .35, bulb * .3), (bulb * .9, -bulb * .45, bulb * .35)):
            ellipsoid(lantern, tuple(head + Vector(off)), (bulb * .62,) * 3, S('Accent'), (0, 0, 0), 18, 10)
    else:
        ellipsoid(lantern, tuple(head), (bulb, bulb * 1.05, bulb), S('Accent'), (0, 0, 0), 24, 14)
        if kind == 'star':
            for k in range(5):
                a = math.tau * k / 5
                d = Vector((math.sin(a), math.cos(a), 0))
                cone(lantern, tuple(head + d * bulb * .7), tuple(head + d * bulb * 1.8), bulb * .38, S('Accent'), 10, 4, .1)
    ellipsoid(lantern, tuple(head + Vector((-bulb * .3, bulb * .45, -bulb * .7))), (bulb * .27, bulb * .25, bulb * .17), 'DK_Glint', (0, 0, 0), 10, 6)
    return lantern

def loft_creature(root, sid, Z0, Z1, prof, eye_t, eye_angle, eye_r, eye_yaw=32, eye_k=.76, blush_t=None, blush_angle=-12,
                  blush_r=(.044, .026, .015), lip_y=-.035, lip=(.036, .02, (1.2, .82, 1.0)), pupil_k=.6, smile=.026,
                  belly=lambda sn, t: sn < -.5 and .06 < t < .86, gain=.15, rings=30, seg=36, marks=None, extra=None,
                  yoff=lambda t: 0):
    """A fish on a loft body with the family face: its Body (Puffed), marks painted on it, extra geometry, and the face placed
    on the body's surface."""
    S = lambda slot: slot_name(sid, slot)
    body = Part('Body')
    loft_z(body, Z0, Z1, rings, seg, prof, S('Body'), belly=(S('Belly'), belly), yoff=yoff)
    if marks:
        marks(body)
    if extra:
        extra(body)
    body_obj = make_object(body, root)
    puff_key(body_obj, body, Z0, Z1, gain, yoff)
    eye = on_body(prof, Z0, Z1, eye_t, eye_angle, eye_k, yoff)
    bl = on_body(prof, Z0, Z1, blush_t if blush_t is not None else eye_t + .07, blush_angle, .94, yoff)
    creature_face(root, sid, tuple(eye), eye_r, eye_yaw, tuple(bl), blush_r, 52, (0, yoff(0) + lip_y, Z0 - .012),
                  lip[0], lip[1], lip[2], pupil_k, smile)
    return body

def round_creature(root, sid, R, eye_az, eye_el, eye_r, eye_yaw=30, eye_k=.82, blush=(48, -12), blush_r=(.05, .03, .016),
                   lip_el=-7, lip=(.036, .021, (1.15, .95, 1.0)), pupil_k=.62, smile=.024, belly_y=-.3, squarish=1.0,
                   puff=1.16, marks=None, extra=None):
    """A round fish (an ellipsoid body, as the Bubblebelly and the Lanternfin) with the family face on its surface."""
    S = lambda slot: slot_name(sid, slot)
    body = Part('Body')
    first = len(body.faces)
    ellipsoid(body, (0, 0, 0), R, S('Body'), (0, 0, 0), 40, 26, squarish)
    paint(body, first, (0, 0, 0), R, S('Belly'), lambda n: n.y < belly_y)
    if marks:
        marks(body, first)
    if extra:
        extra(body)
    body_obj = make_object(body, root)
    shape_key(body_obj, body, 'Puffed', lambda i, v: (v.x * puff, v.y * (puff + .02), v.z * (1 + (puff - 1) * .45)))
    eye = on_ellipsoid(R, eye_az, eye_el, eye_k)
    bl = on_ellipsoid(R, blush[0], blush[1], .985)
    lips = on_ellipsoid(R, 0, lip_el, 1.0) + Vector((0, 0, -.006))
    creature_face(root, sid, tuple(eye), eye_r, eye_yaw, tuple(bl), blush_r, 46, tuple(lips), lip[0], lip[1], lip[2],
                  pupil_k, smile)
    return body

def simple_part(root, name, pivot, build):
    """A part directly under the root that is its own pivot, such as the Tail pivot (its mesh named without 'pivot')."""
    part = Part(name.replace(' pivot', ''))
    build(part)
    return make_object(part, root, pivot, name)

# ---------------------------------------------------------------- Brookkin: Puddlepup, a chubby pup with floppy ear fins
def puddlepup():
    sid = 'puddlepup'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaPuddlepup')
    Z0, Z1 = -.36, .34
    prof = nose_profile(.5, .62, .29, .27, .245)
    eye_t, eye_angle = .15, 30
    def marks(body):
        # A brown patch round the right eye and a spot on the back: a pup's markings.
        eye = on_body(prof, Z0, Z1, eye_t, eye_angle)
        near(body, 0, S('Accent'), [eye + Vector((.03, .02, .03))], .13)
        near(body, 0, S('Accent'), [on_body(prof, Z0, Z1, .58, 75) * 1.0], .11)
    loft_creature(root, sid, Z0, Z1, prof, eye_t, eye_angle, (.094, .11, .076), 30, .76, None, -12, (.046, .028, .016),
                  -.04, (.038, .022, (1.2, .85, 1.0)), .62, .03, marks=marks, gain=.17)
    # Floppy ear fins high by the head that flap as it swims.
    side_fins(root, sid, (.255, .1, -.14), [((.04, -.11, .04), (.038, .15, .09), (16, 14, -10), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, 0, .33), (.045, .055, .055), S('Fin'), (0, 0, 0), 14, 8)
        ellipsoid(part, (0, .02, .44), (.04, .13, .12), S('Fin'), (0, 0, 0), 18, 12)
    simple_part(root, 'Tail pivot', (0, 0, .31), tail)
    def crest(part):
        for k, (y, z, r, tilt) in enumerate(((.045, .0, .045, -22), (.075, .045, .036, 18), (.06, -.04, .03, -48))):
            ellipsoid(part, (0, .25 + y, -.06 + z), (r * .7, r * 1.3, r), S('Fin'), (tilt, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', (0, .25, -.06), crest)
    return root

# ---------------------------------------------------------------- Brookkin: Splashkit, a kitten with pointed ears and a curling tail
def splashkit():
    sid = 'splashkit'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaSplashkit')
    Z0, Z1 = -.38, .36
    prof = nose_profile(.42, .72, .24, .235, .205)
    def marks(body):
        # Tabby stripes over the back.
        repaint(body, 0, Z0, Z1, S('Accent'), lambda sn, t, c: sn > -.15 and any(a < t < a + .055 for a in (.36, .5, .64, .77)))
    def whiskers(body):
        for side in (-1, 1):
            for k, droop in enumerate((-.01, .035)):
                start = Vector((side * .12, -.045 + droop * .2, -.33))
                pts = [tuple(start + Vector((side * .11 * s, -droop * s * 1.5 + .01 * s, -.03 * s))) for s in (0, .33, .66, 1)]
                taper_tube(body, pts, .007, .004, S('Accent'), 8)
    loft_creature(root, sid, Z0, Z1, prof, .14, 31, (.086, .102, .07), 32, .76, None, -14, (.042, .025, .015),
                  -.036, (.034, .019, (1.2, .82, 1.0)), .58, .034, marks=marks, extra=whiskers)
    side_fins(root, sid, (.18, -.09, -.04), [((.035, -.04, .08), (.03, .062, .1), (0, 16, -36), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .01, .34), (.036, .05, .055), S('Fin'), (0, 0, 0), 14, 8)
        pts = [(0, .01 + .2 * s ** 1.6 - .07 * max(0, s - .75) ** 2 * 16, .34 + .3 * math.sin(s * 1.9)) for s in
               (0, .1, .2, .3, .4, .5, .6, .7, .8, .9, 1)]
        taper_tube(part, pts, .034, .03, S('Fin'), 12)
        ellipsoid(part, tuple(Vector(pts[-1]) + Vector((0, -.02, .0))), (.04, .05, .045), S('Accent'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, .01, .33), tail)
    def ears(part):
        for side in (-1, 1):
            base = Vector((side * .085, .2, -.17))
            cone(part, tuple(base), tuple(base + Vector((side * .035, .13, .02))), .055, S('Body'), 14, 6, .15)
            cone(part, tuple(base + Vector((side * .006, .02, -.012))), tuple(base + Vector((side * .03, .1, .005))), .03, S('Blush'), 12, 5, .1)
    simple_part(root, 'Crest pivot', (0, .2, -.17), ears)
    return root

# ---------------------------------------------------------------- Brookkin: Reedwhisker, long and slender with drooping barbels
def reedwhisker():
    sid = 'reedwhisker'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaReedwhisker')
    Z0, Z1 = -.44, .38
    prof = nose_profile(.34, .7, .195, .205, .18)
    def marks(body):
        # Reed stripes down its sides.
        repaint(body, 0, Z0, Z1, S('Accent'), lambda sn, t, c: sn > -.42 and any(a < t < a + .035 for a in (.34, .45, .56, .67, .78)))
    def barbels(body):
        for side in (-1, 1):
            start = Vector((side * .055, -.065, -.425))
            pts = [tuple(start + Vector((side * (.05 * s + .02 * s * s), -.075 * s, .24 * s))) for s in (0, .25, .5, .75, 1)]
            taper_tube(body, pts, .011, .006, S('Accent'), 8)
    loft_creature(root, sid, Z0, Z1, prof, .12, 30, (.074, .09, .062), 30, .76, None, -12, (.04, .024, .014),
                  -.034, (.034, .019, (1.2, .82, 1.0)), .6, .022, marks=marks, extra=barbels, gain=.13, rings=32, seg=32)
    side_fins(root, sid, (.16, -.08, -.08), [((.025, -.04, .09), (.024, .05, .11), (8, 14, -30), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .01, .35), (.032, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        for s in (-1, 1):
            ellipsoid(part, (0, .01 + s * .07, .47), (.026, .1, .13), S('Fin'), (s * 24, 0, 0), 18, 12)
    simple_part(root, 'Tail pivot', (0, .01, .33), tail)
    def crest(part):
        ellipsoid(part, (0, .215, .06), (.017, .05, .2), S('Fin'), (10, 0, 0), 20, 12)
    simple_part(root, 'Crest pivot', (0, .19, -.08), crest)
    return root

# ---------------------------------------------------------------- Brookkin: Lilyfin, petal fins and a lily on its head
def lilyfin():
    sid = 'lilyfin'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaLilyfin')
    Z0, Z1 = -.38, .34
    prof = nose_profile(.44, .68, .25, .24, .21)
    def dots(body):
        # Yellow beads along each side, like stamens.
        for t, a, r in ((.46, 18, .02), (.58, 24, .018), (.7, 14, .016)):
            p = on_body(prof, Z0, Z1, t, a, .99)
            for side in (-1, 1):
                ellipsoid(body, (side * p.x, p.y, p.z), (r, r, r), S('Accent'), (0, 0, 0), 10, 6)
    loft_creature(root, sid, Z0, Z1, prof, .15, 31, (.086, .102, .07), 32, .76, None, -12, (.044, .026, .015),
                  -.035, (.034, .019, (1.2, .82, 1.0)), .6, .03, extra=dots)
    # Two petals each side, fanned.
    side_fins(root, sid, (.2, -.07, -.03), [((.035, -.05, .1), (.03, .055, .13), (10, 22, -40), 'Fin'),
                                            ((.03, -.02, .13), (.026, .045, .11), (-14, 36, -24), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, 0, .33), (.04, .05, .055), S('Fin'), (0, 0, 0), 14, 8)
        for a in (-56, -28, 0, 28, 56):
            d = rot((0, 0, .13), (a, 0, 0))
            ellipsoid(part, (0, d.y, .34 + d.z), (.024, .05, .14), S('Fin'), (a, 0, 0), 18, 12)
    simple_part(root, 'Tail pivot', (0, 0, .31), tail)
    def lily(part):
        c = Vector((0, .27, -.07))
        for k in range(5):
            a = math.tau * k / 5
            d = Vector((math.cos(a), 0, math.sin(a)))
            ellipsoid(part, tuple(c + d * .05 + Vector((0, .025, 0))), (.025, .055, .04), S('Fin'),
                      (0, -math.degrees(a), -34), 14, 8)
        ellipsoid(part, tuple(c + Vector((0, .04, 0))), (.026, .026, .026), S('Accent'), (0, 0, 0), 12, 8)
    simple_part(root, 'Crest pivot', (0, .24, -.07), lily)
    return root

# ---------------------------------------------------------------- Brookkin crown: Brook Monarch, crimson and gold with a crown
def brookmonarch():
    sid = 'brookmonarch'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaBrookmonarch')
    Z0, Z1 = -.42, .42
    prof = nose_profile(.45, .66, .32, .31, .27)
    def marks(body):
        # A gold sash round its middle.
        repaint(body, 0, Z0, Z1, S('Accent'), lambda sn, t, c: sn > -.55 and .4 < t < .46)
    loft_creature(root, sid, Z0, Z1, prof, .14, 30, (.1, .118, .08), 30, .77, None, -12, (.05, .03, .017),
                  -.04, (.042, .024, (1.2, .85, 1.0)), .62, .03, marks=marks, gain=.15)
    # A royal cape: two long sweeping fins a side.
    side_fins(root, sid, (.24, -.06, -.02), [((.05, -.07, .12), (.03, .085, .21), (12, 22, -40), 'Fin'),
                                             ((.04, -.11, .2), (.024, .06, .17), (24, 30, -58), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .01, .41), (.05, .07, .07), S('Fin'), (0, 0, 0), 16, 10)
        for s in (-1, 1):
            ellipsoid(part, (0, .01 + s * .13, .58), (.042, .17, .21), S('Fin'), (s * 38, 0, 0), 22, 14)
            ellipsoid(part, (0, .01 + s * .2, .7), (.03, .05, .05), S('Accent'), (0, 0, 0), 12, 8)
        ellipsoid(part, (0, .01, .6), (.034, .08, .18), S('Accent'), (0, 0, 0), 18, 12)
    simple_part(root, 'Tail pivot', (0, .01, .39), tail)
    def crown(part):
        gold_crown(part, (0, .32, -.12), .085, 5, .085, S('Accent'), -12)
        ellipsoid(part, (0, .31, -.12), (.07, .02, .07), S('Fin'), (-12, 0, 0), 16, 8)
    simple_part(root, 'Crest pivot', (0, .3, -.12), crown)
    return root

# ---------------------------------------------------------------- Sparkline: Sparkminnow, a slim minnow with a glowing zigzag
def sparkminnow():
    sid = 'sparkminnow'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaSparkminnow')
    Z0, Z1 = -.42, .36
    prof = nose_profile(.36, .72, .19, .2, .175)
    def marks(body):
        # A glowing stripe along each side, its lateral line (the accent glows).
        repaint(body, 0, Z0, Z1, S('Accent'), lambda sn, t, c: .2 < t < .84 and abs(sn) < .09)
    loft_creature(root, sid, Z0, Z1, prof, .12, 30, (.074, .09, .062), 30, .76, None, -12, (.04, .024, .014),
                  -.032, (.032, .018, (1.2, .82, 1.0)), .6, .026, marks=marks, rings=32, seg=40, gain=.13)
    side_fins(root, sid, (.15, -.08, -.08), [((.02, -.04, .1), (.02, .045, .12), (14, 14, -34), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .01, .34), (.03, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        for s in (-1, 1):
            ellipsoid(part, (0, .01 + s * .09, .48), (.022, .05, .18), S('Fin'), (s * 32, 0, 0), 18, 12)
    simple_part(root, 'Tail pivot', (0, .01, .32), tail)
    def crest(part):
        ellipsoid(part, (0, .18 + .04, .02 + .06), (.018, .055, .12), S('Fin'), (32, 0, 0), 16, 10)
    simple_part(root, 'Crest pivot', (0, .18, .02), crest)
    make_object(lantern_on_stalk(root, sid, (0, .19, -.2), .17, .09, .06, .016, 80), root, (0, .19, -.2), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Sparkline: Fizzlet, a round fizz of bubbles
def fizzlet():
    sid = 'fizzlet'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaFizzlet')
    R = (.31, .3, .35)
    def bubbles(body):
        for az, el, r in ((-30, 52, .045), (20, 58, .038), (55, 40, .032), (-62, 34, .03), (5, 30, .026), (88, 22, .028),
                          (-95, 20, .026), (35, 72, .03), (130, 38, .034), (-135, 44, .03), (160, 60, .026)):
            ellipsoid(body, tuple(on_ellipsoid(R, az, el, 1.0)), (r, r, r), S('Belly'), (0, 0, 0), 12, 8)
    round_creature(root, sid, R, 30, 10, (.09, .108, .074), 30, .84, (50, -14), (.05, .03, .016), -8,
                   (.034, .02, (1.15, .95, 1.0)), .62, .03, -.35, 1.0, 1.18, extra=bubbles)
    side_fins(root, sid, (.28, -.08, .0), [((.03, -.02, .05), (.026, .055, .075), (0, 20, -30), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, 0, .36), (.035, .05, .05), S('Fin'), (0, 0, 0), 14, 8)
        for s in (-1, 1):
            ellipsoid(part, (0, s * .06, .44), (.03, .085, .09), S('Fin'), (s * 34, 0, 0), 16, 10)
    simple_part(root, 'Tail pivot', (0, 0, .34), tail)
    def crest(part):
        ellipsoid(part, (0, .32, .06), (.03, .045, .04), S('Fin'), (0, 0, 0), 12, 8)
        ellipsoid(part, (0, .36, .1), (.022, .03, .03), S('Fin'), (0, 0, 0), 12, 8)
    simple_part(root, 'Crest pivot', (0, .29, .06), crest)
    make_object(lantern_on_stalk(root, sid, (0, .27, -.14), .2, .14, .07, .018, 100, 'fizz'), root, (0, .27, -.14), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Sparkline: Glowbelly, a ribbon fish with a glowing belly
def glowbelly():
    sid = 'glowbelly'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaGlowbelly')
    Z0, Z1 = -.42, .36
    prof = nose_profile(.38, .72, .215, .225, .2)
    def marks(body):
        # The glowing belly (the accent), with a pale rim between it and the body.
        repaint(body, 0, Z0, Z1, S('Accent'), lambda sn, t, c: sn < -.62 and .14 < t < .8)
    loft_creature(root, sid, Z0, Z1, prof, .13, 31, (.08, .096, .066), 31, .76, None, -10, (.042, .025, .015),
                  -.034, (.034, .019, (1.2, .82, 1.0)), .6, .026, belly=lambda sn, t: sn < -.42 and .08 < t < .86,
                  marks=marks, rings=32, seg=36)
    side_fins(root, sid, (.17, -.07, -.06), [((.03, -.05, .14), (.02, .045, .17), (14, 14, -30), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .01, .35), (.032, .045, .055), S('Fin'), (0, 0, 0), 14, 8)
        for s in (-1, 1):
            ellipsoid(part, (0, .01 + s * .08, .6), (.024, .065, .3), S('Fin'), (s * 15, 0, 0), 20, 14)
    simple_part(root, 'Tail pivot', (0, .01, .33), tail)
    def crest(part):
        ellipsoid(part, (0, .235, .06), (.02, .06, .2), S('Fin'), (14, 0, 0), 20, 12)
    simple_part(root, 'Crest pivot', (0, .2, -.08), crest)
    make_object(lantern_on_stalk(root, sid, (0, .21, -.22), .16, .1, .055, .016, 90), root, (0, .21, -.22), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Sparkline: Flickerfin, flame fins with glowing tips
def flickerfin():
    sid = 'flickerfin'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaFlickerfin')
    Z0, Z1 = -.38, .34
    prof = nose_profile(.42, .7, .24, .24, .21)
    loft_creature(root, sid, Z0, Z1, prof, .14, 31, (.086, .102, .07), 32, .76, None, -12, (.044, .026, .015),
                  -.035, (.034, .019, (1.2, .82, 1.0)), .6, .03)
    S_ = S
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .2, -.06, -.02)
        fin = Part(label + ' fin')
        # Three flame tongues, each tipped with the glowing accent.
        for k, (ang, length) in enumerate(((-24, .2), (6, .16))):
            d = rot((side * .5, -.3, .8), (0, 0, side * ang))
            base = Vector(pivot) + Vector((side * .02, -.01, .02 + .04 * k))
            tip = base + d.normalized() * length
            flame(fin, base, tip, .05, S_('Fin'), S_('Accent'))
        make_object(fin, root, pivot, label + ' fin pivot')
    def tail(part):
        ellipsoid(part, (0, 0, .33), (.036, .05, .05), S('Fin'), (0, 0, 0), 14, 8)
        for a, length in ((-32, .22), (0, .27), (32, .22)):
            base = Vector((0, 0, .34)); tip = base + rot((0, 0, length), (a, 0, 0))
            flame(part, base, tip, .06, S('Fin'), S('Accent'))
    simple_part(root, 'Tail pivot', (0, 0, .31), tail)
    def crest(part):
        for k, (z, h) in enumerate(((-.06, .09), (.03, .13), (.12, .1))):
            base = Vector((0, .2, z)); tip = base + Vector((0, h, .05))
            flame(part, base, tip, .034, S('Fin'), S('Accent'))
    simple_part(root, 'Crest pivot', (0, .21, .03), crest)
    make_object(lantern_on_stalk(root, sid, (0, .22, -.18), .2, .13, .065, .018, 100, 'flame'), root, (0, .22, -.18), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Sparkline: Buzzgill, a striped bee with little wings
def buzzgill():
    sid = 'buzzgill'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaBuzzgill')
    Z0, Z1 = -.37, .37
    prof = nose_profile(.5, .42, .3, .28, .27)
    def bands(body):
        # Dark bands round its back (the fin colour), bee fashion.
        repaint(body, 0, Z0, Z1, S('Fin'), lambda sn, t, c: sn > -.6 and (.52 < t < .6 or .7 < t < .78))
    loft_creature(root, sid, Z0, Z1, prof, .16, 30, (.088, .106, .072), 32, .77, None, -12, (.048, .028, .016),
                  -.04, (.034, .02, (1.15, .92, 1.0)), .62, .03, marks=bands, gain=.14)
    side_fins(root, sid, (.27, -.07, .02), [((.025, -.03, .05), (.022, .045, .07), (0, 20, -34), 'Fin')])
    def tail(part):
        cone(part, (0, 0, .35), (0, -.01, .5), .05, S('Fin'), 12, 5, .15)
        ellipsoid(part, (0, 0, .36), (.035, .04, .04), S('Fin'), (0, 0, 0), 12, 8)
    simple_part(root, 'Tail pivot', (0, 0, .34), tail)
    def wings(part):
        for s in (-1, 1):
            ellipsoid(part, (s * .1, .33, .08), (.11, .018, .07), S('Belly'), (0, s * 28, s * 30), 22, 10)
            ellipsoid(part, (s * .08, .31, .17), (.08, .016, .05), S('Belly'), (0, s * 52, s * 24), 20, 10)
    simple_part(root, 'Crest pivot', (0, .27, .08), wings)
    make_object(lantern_on_stalk(root, sid, (0, .25, -.18), .13, .1, .06, .017, 90), root, (0, .25, -.18), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Sparkline: Starglimmer, a night sky with glowing stars
def starglimmer():
    sid = 'starglimmer'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaStarglimmer')
    Z0, Z1 = -.38, .36
    prof = nose_profile(.44, .7, .25, .25, .22)
    def stars(body):
        for t, a, r in ((.34, 62, .02), (.42, 112, .016), (.5, 40, .018), (.58, 88, .022), (.66, 130, .016), (.7, 30, .016),
                        (.46, 150, .02), (.26, 140, .014), (.77, 70, .014), (.38, 20, .014)):
            p = on_body(prof, Z0, Z1, t, a, 1.0)
            ellipsoid(body, tuple(p), (r, r, r), S('Accent'), (0, 0, 0), 10, 6)
    loft_creature(root, sid, Z0, Z1, prof, .15, 31, (.088, .104, .072), 32, .76, None, -12, (.044, .026, .015),
                  -.035, (.034, .019, (1.2, .82, 1.0)), .6, .03, extra=stars)
    side_fins(root, sid, (.2, -.07, -.03), [((.03, -.05, .1), (.022, .05, .13), (10, 18, -36), 'Fin', .85)])
    def tail(part):
        ellipsoid(part, (0, 0, .33), (.036, .05, .05), S('Fin'), (0, 0, 0), 14, 8)
        for a in (-40, 0, 40):
            base = Vector((0, 0, .34)); tip = base + rot((0, 0, .22), (a, 0, 0))
            cone(part, tuple(base), tuple(tip), .05, S('Fin'), 12, 6, .2)
    simple_part(root, 'Tail pivot', (0, 0, .31), tail)
    def star(part):
        c = Vector((0, .34, .02))
        ellipsoid(part, tuple(c), (.03, .04, .04), S('Accent'), (0, 0, 0), 12, 8)
        for k in range(5):
            a = math.tau * k / 5
            d = Vector((0, math.cos(a), math.sin(a)))
            cone(part, tuple(c + d * .02), tuple(c + d * .1), .028, S('Accent'), 10, 4, .1)
    simple_part(root, 'Crest pivot', (0, .24, .02), star)
    make_object(lantern_on_stalk(root, sid, (0, .22, -.2), .18, .12, .055, .017, 100, 'star'), root, (0, .22, -.2), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Sparkline crown: Aurorafin, aurora sails and three lanterns
def aurorafin():
    sid = 'aurorafin'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaAurorafin')
    Z0, Z1 = -.42, .42
    prof = nose_profile(.44, .68, .3, .3, .26)
    loft_creature(root, sid, Z0, Z1, prof, .14, 30, (.098, .116, .08), 30, .77, None, -12, (.05, .03, .017),
                  -.04, (.04, .023, (1.2, .85, 1.0)), .62, .03)
    # Aurora sails with glowing rims, two a side.
    side_fins(root, sid, (.24, .0, -.02), [((.2, .02, .08), (.22, .025, .16), (0, -14, 12), 'Fin', 1.0, 'Accent'),
                                          ((.14, -.07, .2), (.15, .02, .11), (0, -30, -8), 'Belly', 1.0, 'Accent')])
    def tail(part):
        ellipsoid(part, (0, 0, .41), (.045, .06, .06), S('Fin'), (0, 0, 0), 14, 8)
        for a in (-26, 0, 26):
            d = rot((0, 0, .3), (a, 0, 0))
            ellipsoid(part, (0, d.y, .42 + d.z), (.024, .06, .32), S('Fin'), (a, 0, 0), 20, 14)
            ellipsoid(part, (0, d.y * 1.9, .42 + d.z * 1.9), (.03, .03, .04), S('Accent'), (0, 0, 0), 10, 6)
    simple_part(root, 'Tail pivot', (0, 0, .39), tail)
    def sail(part):
        ellipsoid(part, (0, .29 + .1, .04 + .14), (.022, .12, .26), S('Fin'), (22, 0, 0), 22, 14)
    simple_part(root, 'Crest pivot', (0, .29, .04), sail)
    # A crown of three lanterns.
    lantern = Part('Lantern')
    base = Vector((0, .29, -.14))
    for side, rise, lean in ((0, .32, 0), (-1, .24, .09), (1, .24, .09)):
        pts = [tuple(base + Vector((side * lean * math.sin(math.radians(a)) * 1.2, rise * math.sin(math.radians(a)),
                                    -.12 * (1 - math.cos(math.radians(a)))))) for a in range(0, 91, 10)]
        tube(lantern, pts, .017, S('Body'), 10)
        head = Vector(pts[-1]) + Vector((0, .03, -.03))
        ellipsoid(lantern, tuple(head), (.058, .062, .058), S('Accent'), (0, 0, 0), 22, 12)
        ellipsoid(lantern, tuple(head + Vector((-.018, .028, -.04))), (.016, .015, .01), 'DK_Glint', (0, 0, 0), 10, 6)
    make_object(lantern, root, tuple(base), 'Lantern pivot')
    return root

# ---------------------------------------------------------------- Airfin: Puffglider, a round puff on little sails
def puffglider():
    sid = 'puffglider'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaPuffglider')
    R = (.33, .31, .35)
    round_creature(root, sid, R, 30, 10, (.09, .108, .074), 30, .83, (50, -14), (.05, .03, .016), -8,
                   (.034, .02, (1.15, .95, 1.0)), .62, .03, -.36, 1.0, 1.2)
    side_fins(root, sid, (.3, .02, .0), [((.16, .02, .04), (.18, .025, .14), (0, -6, 12), 'Fin', 1.0, 'Accent')])
    def tail(part):
        for s in (-1, 1):
            ellipsoid(part, (s * .05, .02, .43), (.05, .055, .07), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, .02, .34), tail)
    def puffs(part):
        for k, (z, r) in enumerate(((-.06, .05), (.02, .065), (.1, .045))):
            ellipsoid(part, (0, .32 + r * .6, z), (r * .8, r, r), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', (0, .3, .02), puffs)
    return root

# ---------------------------------------------------------------- Airfin: Hazewing, a slender glider with dragonfly wings
def hazewing():
    sid = 'hazewing'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaHazewing')
    Z0, Z1 = -.4, .38
    prof = nose_profile(.36, .74, .195, .195, .17)
    loft_creature(root, sid, Z0, Z1, prof, .12, 30, (.076, .092, .064), 30, .76, None, -12, (.04, .024, .014),
                  -.033, (.032, .018, (1.2, .82, 1.0)), .6, .026, rings=32, seg=32)
    side_fins(root, sid, (.16, .04, -.06), [((.26, .02, -.02), (.28, .016, .068), (0, 14, 6), 'Fin', 1.0, 'Accent'),
                                           ((.22, .0, .1), (.24, .016, .06), (0, -16, 2), 'Fin', 1.0, 'Accent')])
    def tail(part):
        pts = [(0, .01 + .03 * math.sin(s * math.pi), .34 + .42 * s) for s in (0, .2, .4, .6, .8, 1)]
        taper_tube(part, pts, .03, .018, S('Body'), 12)
        for s in (-1, 1):
            ellipsoid(part, (0, .03 + s * .045, .78), (.018, .05, .07), S('Fin'), (s * 30, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, .01, .33), tail)
    def crest(part):
        ellipsoid(part, (0, .18 + .04, -.02 + .06), (.016, .05, .1), S('Accent'), (26, 0, 0), 16, 10)
    simple_part(root, 'Crest pivot', (0, .18, -.02), crest)
    return root

# ---------------------------------------------------------------- Airfin: Gustgill, swept wings, a forked tail and gill frills
def gustgill():
    sid = 'gustgill'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaGustgill')
    Z0, Z1 = -.38, .36
    prof = nose_profile(.38, .74, .22, .215, .19)
    def frills(body):
        # Gill frills behind each eye, in the accent.
        for side in (-1, 1):
            for k, (t, a) in enumerate(((.3, 14), (.33, -6), (.31, 34))):
                p = on_body(prof, Z0, Z1, t, a if side > 0 else 180 - a, 1.0)
                ellipsoid(body, tuple(p), (.016, .045, .03), S('Accent'), (0, side * 24, 0), 12, 8)
    loft_creature(root, sid, Z0, Z1, prof, .13, 31, (.08, .096, .066), 31, .76, None, -12, (.042, .025, .015),
                  -.034, (.034, .019, (1.2, .82, 1.0)), .6, .026, extra=frills)
    side_fins(root, sid, (.18, .03, -.04), [((.24, .02, .1), (.32, .022, .09), (0, -32, 10), 'Fin', .9, 'Accent')])
    def tail(part):
        ellipsoid(part, (0, .01, .34), (.034, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        for s in (-1, 1):
            ellipsoid(part, (0, .01 + s * .1, .5), (.022, .05, .23), S('Fin'), (s * 30, 0, 0), 18, 12)
    simple_part(root, 'Tail pivot', (0, .01, .32), tail)
    def crest(part):
        ellipsoid(part, (0, .19 + .05, .0 + .08), (.016, .065, .15), S('Fin'), (40, 0, 0), 16, 10)
    simple_part(root, 'Crest pivot', (0, .19, .0), crest)
    return root

# ---------------------------------------------------------------- Airfin: Mistrunner, long trailing sails and streamers
def mistrunner():
    sid = 'mistrunner'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaMistrunner')
    Z0, Z1 = -.44, .4
    prof = nose_profile(.32, .78, .2, .2, .17)
    loft_creature(root, sid, Z0, Z1, prof, .12, 30, (.076, .092, .064), 30, .76, None, -12, (.04, .024, .014),
                  -.033, (.032, .018, (1.2, .82, 1.0)), .6, .026, rings=32, seg=32, gain=.13)
    side_fins(root, sid, (.17, .02, -.06), [((.2, .02, .16), (.2, .02, .27), (0, -14, 8), 'Fin', 1.0, 'Accent')])
    def tail(part):
        ellipsoid(part, (0, .01, .38), (.032, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        for s in (-1, 1):
            pts = [(s * (.03 + .04 * u), .01 + .035 * math.sin(u * math.pi * 1.5), .38 + .5 * u) for u in (0, .2, .4, .6, .8, 1)]
            taper_tube(part, pts, .022, .012, S('Fin'), 10)
    simple_part(root, 'Tail pivot', (0, .01, .37), tail)
    def crest(part):
        ellipsoid(part, (0, .18 + .04, -.06 + .2), (.016, .055, .27), S('Accent'), (10, 0, 0), 18, 12)
    simple_part(root, 'Crest pivot', (0, .18, -.06), crest)
    return root

# ---------------------------------------------------------------- Airfin: Cloudcarp, a koi-patched carp with cloud fins
def cloudcarp():
    sid = 'cloudcarp'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaCloudcarp')
    Z0, Z1 = -.4, .38
    prof = nose_profile(.42, .66, .25, .3, .26)
    def marks(body):
        # Koi patches in the accent.
        near(body, 0, S('Accent'), [on_body(prof, Z0, Z1, .38, 70), on_body(prof, Z0, Z1, .62, 120),
                                     on_body(prof, Z0, Z1, .7, 40), on_body(prof, Z0, Z1, .2, 100)], .1)
    def barbels(body):
        for side in (-1, 1):
            start = Vector((side * .06, -.06, -.39))
            pts = [tuple(start + Vector((side * .07 * s, -.08 * s * s, .05 * s))) for s in (0, .33, .66, 1)]
            taper_tube(body, pts, .013, .009, S('Fin'), 8)
    loft_creature(root, sid, Z0, Z1, prof, .14, 30, (.084, .1, .07), 30, .76, None, -12, (.044, .026, .015),
                  -.04, (.036, .021, (1.2, .85, 1.0)), .6, .028, marks=marks, extra=barbels, rings=44, seg=52)
    # Cloud fins: three puffs a side.
    side_fins(root, sid, (.22, -.02, -.02), [((.1, .0, .03), (.1, .035, .09), (0, 0, 8), 'Fin'),
                                            ((.17, .02, .1), (.08, .03, .075), (0, 0, 14), 'Fin'),
                                            ((.08, -.02, .13), (.08, .03, .07), (0, 0, 2), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .0, .37), (.035, .05, .05), S('Fin'), (0, 0, 0), 14, 8)
        for y, z, r in ((.08, .46, .07), (-.06, .46, .065), (.01, .52, .075)):
            ellipsoid(part, (0, y, z), (.035, r, r), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, 0, .35), tail)
    def crest(part):
        for z, r in ((-.04, .05), (.04, .06), (.12, .045)):
            ellipsoid(part, (0, .28 + r * .5, z), (r * .7, r * .8, r), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', (0, .27, .04), crest)
    return root

# ---------------------------------------------------------------- Airfin: Breezeling, a little round chick of the air
def breezeling():
    sid = 'breezeling'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaBreezeling')
    R = (.27, .26, .3)
    round_creature(root, sid, R, 29, 10, (.084, .1, .07), 30, .83, (50, -14), (.046, .028, .015), -8,
                   (.032, .019, (1.15, .95, 1.0)), .62, .03, -.38, 1.0, 1.18)
    side_fins(root, sid, (.25, .02, .02), [((.11, .02, .04), (.14, .024, .1), (0, -10, 18), 'Fin', 1.0, 'Accent')])
    def tail(part):
        for s in (-1, 1):
            ellipsoid(part, (0, s * .045, .36), (.025, .07, .075), S('Fin'), (s * 34, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, 0, .28), tail)
    def tuft(part):
        # A soft tuft of three feathers, the middle one lemon.
        for k, (tilt, length) in enumerate(((-26, .06), (6, .075), (38, .055))):
            c = Vector((0, .24, -.01 + .025 * k)) + rot((0, length, 0), (tilt, 0, 0))
            ellipsoid(part, tuple(c), (.022, length, .034), S('Fin' if k != 1 else 'Accent'), (tilt, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', (0, .24, .0), tuft)
    return root

# ---------------------------------------------------------------- Airfin crown: Zephyray, a ray with great wings and a crown
def zephyray():
    sid = 'zephyray'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaZephyray')
    R = (.34, .2, .44)
    def lobes(body):
        # Two curled lobes beside the mouth, as a ray's.
        for side in (-1, 1):
            ellipsoid(body, (side * .17, -.02, -.38), (.04, .06, .07), S('Fin'), (0, 0, side * -20), 14, 8)
    round_creature(root, sid, R, 33, 22, (.096, .112, .076), 36, .82, (52, -2), (.05, .03, .016), -6,
                   (.04, .023, (1.2, .85, 1.0)), .62, .03, -.2, 1.0, 1.12, extra=lobes)
    # The great wings, pointed at the tips, edged in gold.
    side_fins(root, sid, (.3, .0, .0), [((.3, .0, .08), (.42, .03, .25), (0, -18, 6), 'Fin', .82, 'Accent')])
    def tail(part):
        pts = [(0, .02 * math.sin(s * math.pi), .38 + .62 * s) for s in (0, .15, .3, .45, .6, .75, .9, 1)]
        taper_tube(part, pts, .03, .012, S('Body'), 12)
        ellipsoid(part, (0, 0, .42), (.06, .05, .07), S('Fin'), (0, 0, 0), 14, 8)
        cone(part, (0, .0, .98), (0, .0, 1.08), .04, S('Accent'), 10, 4, .2)
    simple_part(root, 'Tail pivot', (0, 0, .36), tail)
    def crown(part):
        gold_crown(part, (0, .21, -.1), .08, 5, .08, S('Accent'), -18)
    simple_part(root, 'Crest pivot', (0, .19, -.1), crown)
    return root

WAVE1_BUILDERS = {'puddlepup': puddlepup, 'splashkit': splashkit, 'reedwhisker': reedwhisker, 'lilyfin': lilyfin,
                  'brookmonarch': brookmonarch, 'sparkminnow': sparkminnow, 'fizzlet': fizzlet, 'glowbelly': glowbelly,
                  'flickerfin': flickerfin, 'buzzgill': buzzgill, 'starglimmer': starglimmer, 'aurorafin': aurorafin,
                  'puffglider': puffglider, 'hazewing': hazewing, 'gustgill': gustgill, 'mistrunner': mistrunner,
                  'cloudcarp': cloudcarp, 'breezeling': breezeling, 'zephyray': zephyray}

# ================================================================ GR-12 wave 2: the Cinderling and the Dewling
# Two new families on the same rig (AD-B's parts, pivots and face shape keys). The user chose their looks (2026-10-24): the
# Cinderling's ember spines (a row of glowing ember spines down the back, its Crest pivot, which the game flares) and the
# Dewling's dew beads (round, pearly little fish with shiny dew beads on the head and back, the beads on its Crest pivot).

def ember_spines(part, sid, spines, slot='Fin'):
    """The Cinderling's crest: a row of ember spines, each a sharp spine whose upper part is a glowing ember (the species'
    accent glows). spines: [(base point, height, lean back in degrees, radius)]."""
    S = lambda s: slot_name(sid, s)
    for base, height, lean, radius in spines:
        base = Vector(base)
        tip = base + rot((0, height, 0), (lean, 0, 0))
        cone(part, tuple(base), tuple(tip), radius, S(slot), 12, 6, .15)
        ember = base + (tip - base) * .38
        cone(part, tuple(ember), tuple(tip + (tip - base) * .03), radius * .68, S('Accent'), 12, 5, .15)

def fan_tail(part, base, lobes, thick=.02):
    """A tail of flat lobes fanned from its base: lobes [(angle down from straight back in degrees, length, width, slot)]."""
    base = Vector(base)
    for a, length, width, slot in lobes:
        c = base + rot((0, 0, length * .85), (a, 0, 0))
        ellipsoid(part, tuple(c), (thick, width, length), slot, (a, 0, 0), 20, 12)

def back_spines(prof, Z0, Z1, ts, heights, lean=22, radius=.034, sink=.012, yoff=lambda t: 0):
    """Spine bases along a loft body's back at its length fractions, just inside the surface."""
    out = []
    for t, h in zip(ts, heights):
        p = on_body(prof, Z0, Z1, t, 90, 1.0, yoff)
        out.append(((0, p.y - sink, p.z), h, lean, radius))
    return out

def rimmed_lobes(part, base, lobes, rim, thick=.02, edge=.74):
    """fan_tail's lobes with a band of the rim slot round each lobe's edge (where its flat shape is past edge of its size)."""
    base = Vector(base)
    band = part.slot(rim)
    for a, length, width, slot in lobes:
        c = base + rot((0, 0, length * .85), (a, 0, 0))
        first = len(part.faces)
        ellipsoid(part, tuple(c), (thick, width, length), slot, (a, 0, 0), 32, 16)
        for fi in range(first, len(part.faces)):
            vs = [part.verts[i] for i in part.faces[fi]]
            d = Vector((sum(v.x for v in vs), sum(v.y for v in vs), sum(v.z for v in vs))) / len(vs) - c
            d = rot(d, (-a, 0, 0))
            if math.hypot(d.y / width, d.z / length) > edge:
                part.slots[fi] = band

def dew_beads(part, sid, beads, slot='Accent'):
    """The Dewling's dew beads: shiny pearls, each with a glint. beads: [(centre, radius)]."""
    S = lambda s: slot_name(sid, s)
    for c, r in beads:
        c = Vector(c); r *= 1.15
        ellipsoid(part, tuple(c), (r, r * 1.02, r), S(slot), (0, 0, 0), 18, 12)
        ellipsoid(part, tuple(c + Vector((-r * .35, r * .45, -r * .55))), (r * .28, r * .24, r * .18), 'DK_Glint', (0, 0, 0), 10, 6)

def bead_row(prof, Z0, Z1, ts, radii, angle=90, yoff=lambda t: 0, k=1.0):
    """Bead centres sitting on a loft body's surface at its length fractions and an angle round it."""
    out = []
    for t, r in zip(ts, radii):
        p = on_body(prof, Z0, Z1, t, angle, k, yoff)
        n = Vector((math.cos(math.radians(angle)), math.sin(math.radians(angle)), 0))
        out.append((tuple(p + n * r * .55), r))
    return out

def snout_profile(snout, nose, taper, rx, ryt, ryb, tip=.3, neck=.42):
    """A body with a long snout: a narrow snout (tip to neck, shares of the full radius, rounded at its end) over the snout
    fraction, then the head swelling to the nose fraction and AD-B's taper."""
    def prof(t):
        if t < snout:
            f = tip + (neck - tip) * t / snout
            f *= math.sqrt(max(0, 1 - (1 - t / .03) ** 2)) if t < .03 else 1
        elif t < nose:
            s = (t - snout) / (nose - snout)
            f = neck + (1 - neck) * math.sin(s * math.pi / 2)
        else:
            s = (t - nose) / (1 - nose)
            f = 1 - taper * (s * s * (3 - 2 * s))
        return rx * f, ryt * f, ryb * f
    return prof

# GR-12B wave 3: the Cinderling's eight as animals of their own (their GR-12 builders replaced; the helpers are in the GR-12B section).
# ---------------------------------------------------------------- Cinderling: Emberpuff, a pufferfish
def emberpuff():
    sid = 'emberpuff'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaEmberpuff')
    spine = BPSpine([(0, 0, -.34), (0, .01, 0), (0, .02, .3)])
    body = BPBody(spine, bp_profile([(0, .14, .13, .13), (.25, .28, .28, .27), (.6, .25, .25, .24), (.86, .1, .1, .1), (1, .055, .055, .055)],
                                    nose=.24, tail=.06))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.45), ring_paint=True)
    for k, t in enumerate((.22, .32, .42, .52, .62, .72)):  # little prickles all over it
        for j in range(12):
            a = j * 30 + (15 if k % 2 else 0)
            if 60 < a < 120:
                continue
            p, n = body.at(t, a, .97), body.normal(t, a)
            cone(B, tuple(p), tuple(p + n * .04 + Vector((0, 0, .012))), .011, S('Fin'), 6, 3, .1)
    bp_spots(B, body, sid, [(.4, 30, .022), (.55, 40, .018), (.48, 10, .016)], 'Fin')
    for y, r in ((-.014, (.05, .019, .026)), (-.048, (.046, .017, .024))):  # its beak, two white plates
        ellipsoid(B, (0, y, -.348), r, 'DK_EyeWhite', (0, 0, 0), 16, 8)
    bp_puffed(make_object(B, root), B, body, .24)
    bp_face(root, sid, tuple(body.at(.18, 30, .82)), (.078, .088, .068), 46,
            mouth_arc((0, -.031, -.368), .05, .004, .007, .01))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.24, -.02, -.06), (.3, .0, -.04), (.33, -.04, .02), (.29, -.07, .02), (.24, -.05, -.02)], 16)],
                 .014, S('Fin'), rim=S('Accent'))
    bp_fins(root, (.24, -.03, -.04), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .03, .3), (0, .09, .37), (0, .07, .43), (0, .0, .41), (0, -.07, .43), (0, -.09, .37), (0, -.03, .3)], 22),
                 .016, S('Fin'), rim=S('Accent'))
    simple_part(root, 'Tail pivot', (0, .02, .3), tail)
    def spines(part):
        bp_ember_row(part, sid, body, (.3, .42, .54, .66), (.15, .19, .17, .12), 14, .045, k=.95)
    simple_part(root, 'Crest pivot', tuple(body.at(.48, 90, 1.0)), spines)
    return root

# ---------------------------------------------------------------- Cinderling: Cindertail, a fire salamander with a flaming tail tip
def cindertail():
    sid = 'cindertail'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaCindertail')
    spine = BPSpine([(0, 0, -.46), (0, .01, -.3), (0, .01, -.05), (.02, .01, .2), (-.02, .02, .42), (0, .03, .6)], 121)
    body = BPBody(spine, bp_profile([(0, .05, .03, .03), (.06, .09, .05, .05), (.12, .1, .055, .055), (.17, .075, .05, .05), (.3, .09, .07, .065),
                                     (.45, .085, .07, .065), (.55, .06, .06, .05), (.85, .03, .045, .035), (1, .012, .02, .015)], nose=.05, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), 0, 1, 100, 32, belly=bp_belly_to_nose(S, -.5), ring_paint=True)
    bp_spots(B, body, sid, [(.07, 50, .022), (.2, 60, .024), (.3, 30, .028), (.38, 70, .022), (.47, 40, .024), (.58, 60, .018),
                            (.68, 45, .015), (.77, 65, .012), (.25, 10, .016)], 'Accent')
    for side in (1, -1):  # its hind legs
        hip = body.at(.48, -10 if side > 0 else -170, .85)
        bp_leg(B, S, hip, hip + Vector((side * .07, -.02, .02)), hip + Vector((side * .09, -.07, -.03)), .026, .018, 'Body', 4, .028)
    body_obj = make_object(B, root)
    bp_puffed(body_obj, B, body, .08)
    bp_wave_keys(body_obj, B, body, .06, 1.0, .45)
    bp_face(root, sid, tuple(body.at(.08, 42, .84)), (.034, .04, .032), 52, mouth_grin(body, .01, .12, -12, .007), eye_pitch=14)
    def arm(part, m):
        hip = (.075, -.02, -.28)
        bp_leg(part, S, m(hip), m((.14, -.04, -.27)), m((.16, -.09, -.31)), .024, .017, 'Body', 4, .026)
    bp_fins(root, (.075, -.02, -.28), arm)
    end, tan = spine.frame(1.0)[0], spine.tangent(.98)
    def tail(part):
        flame(part, end - tan * .03, end + tan * .14 + Vector((0, .02, 0)), .03, S('Fin'), S('Accent'))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.97)[0]), tail)
    def spines(part):
        bp_ember_row(part, sid, body, (.2, .27, .34, .41), (.07, .09, .09, .07), 34, .02, k=.92)
    simple_part(root, 'Crest pivot', tuple(body.at(.3, 90, 1.0)), spines)
    return root

# ---------------------------------------------------------------- Cinderling: Coalbelly, a frogfish with a glowing belly
def coalbelly():
    sid = 'coalbelly'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaCoalbelly')
    spine = BPSpine([(0, 0, -.32), (0, .02, -.05), (0, .04, .2), (0, .05, .32)])
    body = BPBody(spine, bp_profile([(0, .13, .16, .12), (.2, .22, .25, .22), (.5, .22, .24, .21), (.8, .12, .14, .11), (1, .055, .065, .05)],
                                    nose=.2, tail=.05))
    B = Part('Body')
    def glow(t, sn, cs):
        if sn < -.62 and .14 < t < .85:
            return S('Accent')  # its belly glows like a coal
        if sn < -.35 and t < .95:
            return S('Belly')
        return None
    bp_loft(B, body, S('Body'), belly=glow, ring_paint=True)
    for t, a, r in ((.3, 20, .024), (.4, 50, .02), (.5, 25, .026), (.6, 60, .02), (.7, 30, .018), (.35, 120, .016), (.55, 140, .02),
                    (.45, 160, .018), (.25, 40, .016), (.65, 5, .016)):  # warty lumps
        ellipsoid(B, tuple(body.at(t, a, .97)), (r, r * .8, r), S('Fin'), (0, 0, 0), 10, 6)
    base = body.at(.05, 90, .95)  # its lure, a little fishing rod with a glowing bait
    pts = [tuple(base + Vector((0, .1 * math.sin(math.radians(a)), -.08 * (1 - math.cos(math.radians(a)))))) for a in range(0, 101, 20)]
    tube(B, pts, .008, S('Fin'), 8)
    ellipsoid(B, tuple(Vector(pts[-1]) + Vector((0, -.01, -.02))), (.022, .022, .022), S('Accent'), (0, 0, 0), 12, 8)
    for side in (1, -1):  # little hand-like fins under its throat
        bp_plate(B, bp_outline([(side * .05, -.18, -.12), (side * .09, -.24, -.14), (side * .11, -.25, -.08), (side * .06, -.2, -.06)], 14), .016, S('Fin'), normal=(0, 1, 0))
    bp_puffed(make_object(B, root), B, body, .14)
    bp_face(root, sid, tuple(body.at(.16, 58, .84)), (.04, .046, .036), 40,
            mouth_gape((0, .005, -.33), .1, .045, S('Fin'), sid, upper=[(-.55, .02, .008), (-.2, .02, .008), (.2, .02, .008), (.55, .02, .008)],
                       tilt=-38, rim_r=.02, depth=.03), eye_pitch=12)
    def arm(part, m):
        # Arm-like fins with an elbow, a hand of fin rays at the end to walk on.
        taper_tube(part, [m((.2, -.08, -.02)), m((.28, -.14, -.0)), m((.3, -.22, -.06))], .03, .022, S('Body'), 10)
        bp_plate(part, [m(p) for p in bp_outline([(.29, -.22, -.06), (.36, -.25, -.1), (.34, -.25, -.0), (.27, -.24, -.02)], 16)], .016, S('Fin'), rim=S('Accent'),
                 normal=(0, 1, 0))
    bp_fins(root, (.2, -.08, -.02), arm)
    def tail(part):
        bp_plate(part, bp_outline([(0, .04, .3), (0, .12, .38), (0, .1, .46), (0, .0, .48), (0, -.09, .45), (0, -.09, .37), (0, -.03, .3)], 22), .018, S('Fin'),
                 rim=S('Accent'))
    simple_part(root, 'Tail pivot', (0, .05, .3), tail)
    def spines(part):
        bp_ember_row(part, sid, body, (.3, .44, .58), (.12, .15, .12), 18, .04, k=.95)
    simple_part(root, 'Crest pivot', tuple(body.at(.44, 90, 1.0)), spines)
    return root

# ---------------------------------------------------------------- Cinderling: Sizzlesnout, a gharial whose snout tip sizzles
def sizzlesnout():
    sid = 'sizzlesnout'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaSizzlesnout')
    spine = BPSpine([(0, 0, -.6), (0, 0, -.42), (0, .02, -.24), (0, .02, 0), (.02, .02, .22), (-.02, .03, .45), (0, .04, .62)], 121)
    body = BPBody(spine, bp_profile([(0, .042, .042, .03), (.03, .034, .03, .025), (.2, .036, .03, .028), (.26, .07, .065, .05), (.31, .085, .075, .06),
                                     (.37, .07, .06, .055), (.5, .11, .09, .08), (.62, .09, .08, .07), (.85, .035, .06, .04), (1, .015, .03, .02)],
                                    nose=.02, tail=.03))
    B = Part('Body')
    def paint(t, sn, cs):
        if t < .035:
            return S('Accent')  # the bulb on its snout's tip, sizzling
        if sn < -.5 and t < .97:
            return S('Belly')
        return None
    bp_loft(B, body, S('Body'), 0, 1, 110, 32, belly=paint, ring_paint=True)
    for t in (.66, .71, .76, .81, .86, .91):  # a double row of scutes down its tail
        for a in (70, 110):
            p = body.at(t, a, .92)
            cone(B, tuple(p), tuple(p + Vector((0, .03, .012))), .012, S('Fin'), 6, 3, .1)
    for side in (1, -1):  # its hind legs
        hip = body.at(.58, -10 if side > 0 else -170, .85)
        bp_leg(B, S, hip, hip + Vector((side * .08, -.02, .02)), hip + Vector((side * .1, -.08, -.02)), .028, .02, 'Body', 4, .03)
    body_obj = make_object(B, root)
    bp_puffed(body_obj, B, body, .08)
    bp_wave_keys(body_obj, B, body, .06, 1.0, .55)
    teeth = [(w, .016, .005, (0, -1 if k % 2 else 1, 0)) for k, w in enumerate((.06, .12, .18, .24, .3, .36, .64, .7, .76, .82, .88, .94))]
    bp_face(root, sid, tuple(body.at(.29, 58, .84)), (.034, .04, .032), 34, mouth_grin(body, .02, .27, -4, .006, teeth=teeth), eye_pitch=22)
    def arm(part, m):
        bp_leg(part, S, m((.08, -.03, -.08)), m((.15, -.05, -.07)), m((.17, -.1, -.11)), .026, .019, 'Body', 4, .028)
    bp_fins(root, (.08, -.03, -.08), arm)
    end, tan = spine.frame(1.0)[0], spine.tangent(.98)
    def tail(part):
        bp_plate(part, bp_outline([tuple(end - tan * .06 + Vector((0, .02, 0))), tuple(end + Vector((0, .05, 0))), tuple(end + tan * .06),
                                   tuple(end - Vector((0, .02, 0)))], 16), .016, S('Fin'), rim=S('Accent'))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.97)[0]), tail)
    def spines(part):
        for a in (75, 105):
            bp_ember_row(part, sid, body, (.38, .44, .5, .55), (.06, .08, .08, .06), 30, .02, angle=a, k=.92)
    simple_part(root, 'Crest pivot', tuple(body.at(.46, 90, 1.0)), spines)
    return root

# ---------------------------------------------------------------- Cinderling: Kindlekoi, a betta with flowing flame fins
def kindlekoi():
    sid = 'kindlekoi'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaKindlekoi')
    spine = BPSpine([(0, .01, -.3), (0, .01, -.1), (0, .01, .12), (0, .02, .24)])
    body = BPBody(spine, bp_profile([(0, .055, .06, .055), (.15, .1, .12, .11), (.45, .11, .13, .12), (.8, .07, .08, .075), (1, .04, .05, .045)],
                                    nose=.14, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.5), ring_paint=True)
    bp_spots(B, body, sid, [(.35, 50, .024), (.55, 30, .02), (.62, 70, .018), (.45, 15, .016)], 'Accent')
    bp_plate(B, bp_long_fin(body, .4, .9, .17, -90, .1, 7, .09), .014, S('Fin'), rim=S('Accent'), rim_from=.7, normal=(1, 0, 0), curl=.02)  # its long anal fin
    for side in (1, -1):  # long pelvic streamers
        b0 = body.at(.3, -70 if side > 0 else -110, .9)
        taper_tube(B, [tuple(b0), tuple(b0 + Vector((side * .02, -.08, .05))), tuple(b0 + Vector((side * .03, -.16, .12)))], .01, .004, S('Accent'), 6)
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.15, 24, .85)), (.044, .05, .04), 58, mouth_arc((0, .012, -.3), .022, -.004, .006, .008), eye_pitch=-2)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.1, -.02, -.12), (.15, -.0, -.1), (.17, -.04, -.05), (.12, -.05, -.08)], 14)], .01, S('Fin'))
    bp_fins(root, (.1, -.02, -.1), pec)
    def tail(part):
        # A great veil of a tail, its edge flickering like flame.
        bp_plate(part, bp_frill((0, .02, .23), -95, 62, lambda s: .36 * (.7 + .3 * math.sin(math.pi * s)), 9, .1), .016, S('Fin'), rim=S('Accent'),
                 rim_from=.66, normal=(1, 0, 0), curl=.03)
    simple_part(root, 'Tail pivot', (0, .02, .22), tail)
    def crest(part):
        # Its flowing dorsal fin, its rays ember spines.
        bp_plate(part, bp_long_fin(body, .4, .9, .16, 90, .12, 6, .09), .014, S('Fin'), rim=S('Accent'), rim_from=.7, normal=(1, 0, 0), curl=.02)
        bp_ember_row(part, sid, body, (.48, .58, .68, .78), (.1, .12, .14, .14), 34, .014, k=.92)
    simple_part(root, 'Crest pivot', tuple(body.at(.6, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cinderling: Sootsnap, a snapping turtle, its shell's keels in embers
def sootsnap():
    sid = 'sootsnap'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaSootsnap')
    spine = BPSpine([(0, 0, -.42), (0, .02, -.3), (0, .04, -.16), (0, .05, .08), (0, .03, .3), (0, 0, .52)])
    body = BPBody(spine, bp_profile([(0, .055, .05, .05), (.06, .1, .085, .075), (.14, .12, .1, .09), (.22, .075, .07, .07), (.4, .15, .1, .1),
                                     (.62, .12, .08, .08), (.7, .05, .05, .05), (1, .012, .015, .012)], nose=.05, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.5), ring_paint=True)
    c, R = Vector((0, .07, .06)), (.25, .12, .3)
    first = len(B.faces)
    ellipsoid(B, tuple(c), R, S('Fin'), (0, 0, 0), 48, 24)  # its rough shell
    paint(B, first, tuple(c), R, S('Belly'), lambda n: n.y < .1)
    ellipsoid(B, (0, .0, .06), (.22, .045, .27), S('Belly'), (0, 0, 0), 32, 12)  # its plastron
    for el in (40, 70, 110, 140):  # the shell's plates
        tube(B, [tuple(on_dome(c, R, az, el, 1.004)) for az in range(-180, 181, 12)], .006, S('Body'), 6)
    tip = body.at(.01, 90, .7)  # its hooked beak
    cone(B, tuple(tip + Vector((0, .02, .03))), tuple(tip + Vector((0, -.035, -.04))), .032, S('Fin'), 10, 4, .3)
    for side in (1, -1):  # its hind legs, clawed
        hip = Vector((side * .17, -.02, .22))
        bp_leg(B, S, hip, hip + Vector((side * .07, -.04, .03)), hip + Vector((side * .09, -.09, .0)), .034, .026, 'Body', 4, .03)
    for t in (.74, .8, .86, .92):  # the saw of its tail
        p = body.at(t, 90, .9)
        cone(B, tuple(p), tuple(p + Vector((0, .028, .01))), .012, S('Fin'), 6, 3, .1)
    bp_puffed(make_object(B, root), B, body, .08)
    bp_face(root, sid, tuple(body.at(.09, 38, .84)), (.04, .046, .036), 50, mouth_grin(body, .025, .13, -18, .008, lip=S('Fin')), eye_pitch=6)
    def arm(part, m):
        bp_leg(part, S, m((.15, -.02, -.16)), m((.24, -.05, -.18)), m((.27, -.11, -.24)), .034, .026, 'Body', 4, .032)
    bp_fins(root, (.15, -.02, -.16), arm)
    def tail(part):
        cone(part, (0, .0, .5), (0, -.01, .6), .016, S('Body'), 8, 4, .1)
    simple_part(root, 'Tail pivot', (0, .0, .5), tail)
    def keels(part):
        rows = [[on_dome(c, R, 0 if el < 90 else 180, el if el <= 90 else 180 - el, .97) for el in (40, 70, 110, 140)]]
        rows += [[on_dome(c, R, s * az, 52, .97) for az in (45, 80, 115, 145)] for s in (1, -1)]
        spines = []
        for row in rows:
            for p in row:
                n = dome_normal(c, R, p)
                spines.append((tuple(p), .07, 0, .026))
        ember_spines(part, sid, spines)
    simple_part(root, 'Crest pivot', tuple(c + Vector((0, R[1], 0))), keels)
    return root

# ---------------------------------------------------------------- Cinderling: Ashfin, a lionfish with long ember spines
def ashfin():
    sid = 'ashfin'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaAshfin')
    spine = BPSpine([(0, 0, -.36), (0, .01, -.1), (0, .01, .16), (0, .02, .3)])
    body = BPBody(spine, bp_profile([(0, .06, .07, .065), (.15, .1, .13, .11), (.45, .11, .14, .12), (.8, .065, .08, .065), (1, .035, .045, .035)],
                                    nose=.14, tail=.04))
    B = Part('Body')
    def bands(t, sn, cs):
        if sn < -.55 and t < .95:
            return S('Belly')
        if .08 < t < .92 and math.sin(t * math.pi * 11 + sn * .8) > .55:
            return S('Accent')  # its bands glow like embers
        return None
    bp_loft(B, body, S('Body'), rings=60, seg=40, belly=bands, ring_paint=True)
    for side in (1, -1):  # fleshy tentacles over its eyes
        b0 = body.at(.15, 62 if side > 0 else 118, .95)
        taper_tube(B, [tuple(b0), tuple(b0 + Vector((side * .02, .05, .01))), tuple(b0 + Vector((side * .03, .08, .04)))], .008, .003, S('Fin'), 6)
    bp_back_fin(B, body, .62, .82, .07, S('Fin'), rim=S('Accent'), sweep=.3)  # its soft second dorsal and anal fins
    bp_back_fin(B, body, .62, .82, .06, S('Fin'), rim=S('Accent'), sweep=.3, angle=-90)
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.15, 28, .84)), (.044, .05, .04), 54, mouth_grin(body, .02, .08, -8, .007))
    def fan(part, m):
        # Great feathery fans: long rays with a web between them, their tips glowing.
        rays = []
        for k in range(7):
            a = math.radians(-5 + 16 * k)
            tip = Vector((.12, -.04, -.04)) + (Vector((.75, -.66, 0)) * math.cos(a) + Vector((0, 0, 1)) * math.sin(a)).normalized() * (.2 + .05 * math.sin(math.pi * k / 6))
            rays.append(tip)
            taper_tube(part, [m((.12, -.04, -.04)), m(tuple(tip))], .008, .004, S('Fin'), 6)
            ellipsoid(part, m(tuple(tip)), (.012, .012, .012), S('Accent'), (0, 0, 0), 8, 5)
        bp_plate(part, [m(tuple(p)) for p in bp_outline([(.12, -.04, -.04)] + [tuple(Vector((.12, -.04, -.04)) + (r - Vector((.12, -.04, -.04))) * .82) for r in rays], 30)],
                 .008, S('Fin'), rim=S('Accent'), rim_from=.85)
    bp_fins(root, (.12, -.04, -.04), fan)
    def tail(part):
        bp_plate(part, bp_outline([(0, .03, .28), (0, .09, .35), (0, .08, .42), (0, .0, .44), (0, -.08, .42), (0, -.09, .35), (0, -.03, .28)], 24), .016, S('Fin'),
                 rim=S('Accent'))
    simple_part(root, 'Tail pivot', (0, .02, .28), tail)
    def spines(part):
        bp_ember_row(part, sid, body, (.22, .29, .36, .43, .5, .57), (.2, .26, .27, .25, .2, .14), 24, .02, k=.92)
    simple_part(root, 'Crest pivot', tuple(body.at(.4, 90, 1.0)), spines)
    return root

# ---------------------------------------------------------------- Cinderling crown: Phoenix Fry, a baby Spinosaurus with a flame sail
def phoenixfry():
    sid = 'phoenixfry'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaPhoenixfry')
    spine = BPSpine([(0, 0, -.56), (0, .01, -.4), (0, .03, -.22), (0, .04, 0), (.02, .03, .22), (-.02, .03, .44), (0, .04, .64)], 121)
    body = BPBody(spine, bp_profile([(0, .03, .03, .028), (.1, .045, .045, .04), (.17, .07, .07, .06), (.22, .085, .085, .07), (.28, .07, .07, .065),
                                     (.42, .13, .12, .12), (.55, .11, .1, .1), (.7, .06, .07, .055), (.92, .025, .05, .035), (1, .012, .03, .02)],
                                    nose=.03, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), 0, 1, 110, 32, belly=bp_belly_to_nose(S, -.5), ring_paint=True)
    for k in range(2):  # its nostrils, far up its snout
        ellipsoid(B, tuple(body.at(.1, 70 if k == 0 else 110, .98)), (.008, .006, .014), 'DK_Mouth', (0, 0, 0), 8, 4)
    for side in (1, -1):  # its strong hind legs
        hip = body.at(.52, -10 if side > 0 else -170, .85)
        bp_leg(B, S, hip, hip + Vector((side * .08, -.03, .04)), hip + Vector((side * .1, -.1, -.0)), .036, .024, 'Body', 3, .035)
    lo, hi, sd = [], [], []  # a finned tail for paddling
    for k in range(24):
        t = .66 + .32 * k / 23
        c, s, up = spine.frame(t)
        base = body.at(t, 90, .9)
        lo.append(tuple(base)); hi.append(tuple(base + up * (.05 * math.sin(math.pi * (k + 1) / 25) + .006))); sd.append(tuple(s))
    bp_strip(B, lo, hi, .014, S('Fin'), rim=S('Accent'), side_dirs=sd)
    body_obj = make_object(B, root)
    bp_puffed(body_obj, B, body, .1)
    bp_wave_keys(body_obj, B, body, .06, 1.0, .6)
    teeth = [(w, .02, .006, (0, -1 if k % 2 else 1, 0)) for k, w in enumerate((.08, .16, .24, .32, .68, .76, .84, .92))]
    bp_face(root, sid, tuple(body.at(.22, 40, .82)), (.046, .054, .042), 46, mouth_grin(body, .015, .22, -6, .007, teeth=teeth), eye_pitch=10)
    def arm(part, m):
        bp_leg(part, S, m((.08, -.04, -.12)), m((.14, -.08, -.14)), m((.15, -.13, -.19)), .022, .016, 'Body', 3, .03)
    bp_fins(root, (.08, -.04, -.12), arm)
    end, tan = spine.frame(1.0)[0], spine.tangent(.98)
    def tail(part):
        flame(part, end - tan * .02, end + tan * .16 + Vector((0, .03, 0)), .035, S('Fin'), S('Accent'))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.97)[0]), tail)
    def sail(part):
        # Its sail: tall ember spines down its back with a web of flame between them, and its gold crown.
        ts = [.34 + .03 * k for k in range(9)]
        hs = [.1 + .14 * math.sin(math.pi * (k + 1) / 10) for k in range(9)]
        bp_ember_row(part, sid, body, ts, hs, 10, .016, k=.9)
        lo = [tuple(body.at(t, 90, .9)) for t in ts]
        hi = [tuple(Vector(lo[k]) + rot((0, hs[k] * .86, 0), (10, 0, 0))) for k in range(9)]
        bp_strip(part, lo, hi, .012, S('Fin'), rim=S('Accent'), side_dirs=[(1, 0, 0)] * 9)
        top = body.at(.24, 90, 1.0)
        gold_crown(part, (0, top.y + .004, top.z), .055, 5, .055, 'DK_Gold', -8)
    simple_part(root, 'Crest pivot', tuple(body.at(.42, 90, 1.0)), sail)
    return root

# ---------------------------------------------------------------- Dewling: Dewdrop, a pale drop with beads and a dripping tail
def dewdrop():
    sid = 'dewdrop'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaDewdrop')
    R = (.3, .31, .32)
    def side_beads(body):
        dew_beads(body, sid, [(tuple(on_ellipsoid(R, az, el, 1.0)), r) for az, el, r in ((120, 30, .03), (-120, 30, .03), (150, 12, .024), (-150, 12, .024))])
    round_creature(root, sid, R, 30, 10, (.09, .108, .074), 30, .83, (50, -14), (.05, .03, .016), -8,
                   (.034, .02, (1.15, .95, 1.0)), .62, .03, -.38, 1.0, 1.16, extra=side_beads)
    side_fins(root, sid, (.27, -.06, .0), [((.03, -.02, .05), (.026, .055, .075), (0, 20, -30), 'Fin')])
    def tail(part):
        # A drip curling up behind it.
        ellipsoid(part, (0, 0, .33), (.04, .045, .045), S('Fin'), (0, 0, 0), 14, 8)
        ellipsoid(part, (0, .07, .47), (.055, .06, .06), S('Fin'), (0, 0, 0), 18, 10)
        cone(part, (0, .07, .47), (0, .0, .33), .055, S('Fin'), 14, 7, .5)
    simple_part(root, 'Tail pivot', (0, 0, .31), tail)
    def beads(part):
        dew_beads(part, sid, [(tuple(on_ellipsoid(R, 0, el, 1.0) + Vector((0, r * .6, 0))), r)
                              for el, r in ((60, .05), (76, .042), (90, .036))])
    simple_part(root, 'Crest pivot', (0, .3, -.05), beads)
    return root

# ---------------------------------------------------------------- Dewling: Fogfin, wispy fins trailing like fog
def fogfin():
    sid = 'fogfin'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaFogfin')
    Z0, Z1 = -.4, .34
    prof = nose_profile(.42, .72, .22, .22, .2)
    loft_creature(root, sid, Z0, Z1, prof, .14, 31, (.086, .102, .07), 32, .76, None, -12, (.044, .026, .015),
                  -.035, (.034, .019, (1.2, .82, 1.0)), .6, .03)
    # Two long wisps a side.
    side_fins(root, sid, (.18, -.05, -.02), [((.03, -.05, .14), (.018, .04, .19), (14, 20, -36), 'Fin'),
                                            ((.025, -.09, .12), (.016, .035, .16), (30, 28, -54), 'Belly')])
    def tail(part):
        ellipsoid(part, (0, .01, .33), (.03, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        fan_tail(part, (0, .01, .33), ((-24, .22, .04, S('Fin')), (0, .26, .034, S('Belly')), (24, .22, .04, S('Fin'))), .012)
    simple_part(root, 'Tail pivot', (0, .01, .31), tail)
    def beads(part):
        dew_beads(part, sid, bead_row(prof, Z0, Z1, (.32, .46), (.036, .03)))
        ellipsoid(part, tuple(on_body(prof, Z0, Z1, .6, 90, 1.0) + Vector((0, .02, .04))), (.012, .05, .11), S('Belly'), (24, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', (0, .22, .0), beads)
    return root

# ---------------------------------------------------------------- Dewling: Mistlet, a tiny pink puff with a cloud tail
def mistlet():
    sid = 'mistlet'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaMistlet')
    R = (.27, .26, .29)
    round_creature(root, sid, R, 30, 10, (.088, .104, .072), 30, .83, (50, -14), (.048, .028, .015), -8,
                   (.032, .019, (1.15, .95, 1.0)), .62, .03, -.38, 1.0, 1.18)
    side_fins(root, sid, (.24, -.05, .02), [((.025, -.02, .04), (.022, .045, .06), (0, 20, -30), 'Fin')])
    def tail(part):
        for y, z, r in ((.06, .33, .055), (-.05, .33, .05), (.0, .38, .06)):
            ellipsoid(part, (0, y, z), (.04, r, r), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, 0, .28), tail)
    def beads(part):
        top = on_ellipsoid(R, 0, 80, 1.0)
        dew_beads(part, sid, [(tuple(top + Vector((0, .03, -.02))), .04), (tuple(top + Vector((-.045, .015, .03))), .03),
                              (tuple(top + Vector((.045, .015, .03))), .03)])
    simple_part(root, 'Crest pivot', (0, .26, .0), beads)
    return root

# ---------------------------------------------------------------- Dewling: Drizzler, a raincloud with drops beneath its fins
def drizzler():
    sid = 'drizzler'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaDrizzler')
    Z0, Z1 = -.36, .32
    prof = nose_profile(.48, .62, .27, .26, .25)
    loft_creature(root, sid, Z0, Z1, prof, .16, 30, (.09, .106, .072), 30, .77, None, -12, (.046, .028, .016),
                  -.038, (.034, .02, (1.15, .92, 1.0)), .62, .03, belly=lambda sn, t: sn < -.3 and .06 < t < .88)
    # Cloud-puff fins with drizzle drops hanging below them.
    for side, label in ((-1, 'Left'), (1, 'Right')):
        pivot = (side * .24, -.03, -.02)
        fin = Part(label + ' fin')
        for off, r in (((.07, 0, .02), .055), ((.12, .01, .07), .045), ((.06, -.01, .09), .045)):
            ellipsoid(fin, (pivot[0] + side * off[0], pivot[1] + off[1], pivot[2] + off[2]), (r, r * .6, r), S('Fin'), (0, 0, 0), 14, 8)
        for k, (x, z) in enumerate(((.07, .03), (.12, .08))):
            c = Vector((pivot[0] + side * x, pivot[1] - .07 - .02 * k, pivot[2] + z))
            cone(fin, tuple(c + Vector((0, .035, 0))), tuple(c + Vector((0, .06, 0))), .012, S('Accent'), 8, 3, .1)
            dew_beads(fin, sid, [(tuple(c), .02)])
        make_object(fin, root, pivot, label + ' fin pivot')
    def tail(part):
        ellipsoid(part, (0, 0, .31), (.035, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        fan_tail(part, (0, 0, .31), ((-30, .11, .055, S('Fin')), (30, .11, .055, S('Fin'))), .022)
    simple_part(root, 'Tail pivot', (0, 0, .29), tail)
    def beads(part):
        dew_beads(part, sid, bead_row(prof, Z0, Z1, (.3, .44, .58, .7), (.034, .038, .032, .026)))
    simple_part(root, 'Crest pivot', (0, .26, .0), beads)
    return root

# ---------------------------------------------------------------- Dewling: Pearlpip, pearl-white with a big pearl and shell fins
def pearlpip():
    sid = 'pearlpip'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaPearlpip')
    R = (.29, .28, .31)
    round_creature(root, sid, R, 30, 10, (.09, .106, .074), 30, .83, (50, -14), (.048, .028, .016), -8,
                   (.034, .02, (1.15, .95, 1.0)), .62, .03, -.4, 1.0, 1.16)
    # Scalloped shell fins: three overlapping lobes.
    side_fins(root, sid, (.26, -.05, .02), [((.04, -.02, .03), (.02, .05, .05), (0, 20, -40), 'Fin'),
                                           ((.05, -.04, .07), (.02, .05, .05), (0, 30, -24), 'Fin'),
                                           ((.03, -.06, .1), (.02, .045, .045), (0, 40, -10), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, 0, .32), (.035, .045, .045), S('Fin'), (0, 0, 0), 14, 8)
        for a in (-40, 0, 40):
            d = rot((0, 0, .08), (a, 0, 0))
            ellipsoid(part, (0, d.y, .33 + d.z), (.022, .05, .06), S('Fin'), (a, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, 0, .3), tail)
    def pearl(part):
        top = on_ellipsoid(R, 0, 70, 1.0)
        dew_beads(part, sid, [(tuple(top + Vector((0, .07, 0))), .07), (tuple(on_ellipsoid(R, 0, 88, 1.0) + Vector((0, .02, .06))), .03)])
        torus(part, top + Vector((0, .015, 0)), .05, .014, S('Fin'), (1, 1, 1), 24, 8, (90, 0, 0))
    simple_part(root, 'Crest pivot', (0, .27, -.06), pearl)
    return root

# ---------------------------------------------------------------- Dewling: Haloling, a halo of dew beads above its head
def haloling():
    sid = 'haloling'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaHaloling')
    Z0, Z1 = -.34, .32
    prof = nose_profile(.5, .62, .25, .25, .23)
    loft_creature(root, sid, Z0, Z1, prof, .17, 30, (.088, .104, .072), 30, .77, None, -12, (.046, .028, .016),
                  -.036, (.032, .019, (1.15, .92, 1.0)), .62, .03)
    side_fins(root, sid, (.22, -.04, -.02), [((.06, -.01, .04), (.07, .02, .06), (0, -10, -14), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, 0, .3), (.032, .045, .045), S('Fin'), (0, 0, 0), 14, 8)
        fan_tail(part, (0, 0, .3), ((-28, .1, .05, S('Fin')), (28, .1, .05, S('Fin'))), .02)
    simple_part(root, 'Tail pivot', (0, 0, .28), tail)
    def halo(part):
        # A ring of light floating over its head, set with six beads.
        top = on_body(prof, Z0, Z1, .3, 90, 1.0)
        c = Vector((0, top.y + .11, top.z))
        torus(part, c, .12, .017, S('Accent'), (1, 1, 1), 40, 10, (90, 0, 0))
        dew_beads(part, sid, [(tuple(c + Vector((.12 * math.cos(a), .0, .12 * math.sin(a)))), .024) for a in
                              [math.tau * k / 6 for k in range(6)]], 'Belly')
    simple_part(root, 'Crest pivot', (0, .24, -.06), halo)
    return root

# ---------------------------------------------------------------- Dewling: Rainskipper, sleek with long skipping fins
def rainskipper():
    sid = 'rainskipper'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaRainskipper')
    Z0, Z1 = -.4, .36
    prof = nose_profile(.34, .74, .19, .19, .17)
    def lateral(body):
        beads = []
        for side in (-1, 1):
            for t in (.3, .42, .54, .66):
                p = on_body(prof, Z0, Z1, t, 0 if side > 0 else 180, 1.0)
                beads.append((tuple(p + Vector((side * .006, 0, 0))), .016))
        dew_beads(body, sid, beads)
    loft_creature(root, sid, Z0, Z1, prof, .12, 30, (.076, .092, .064), 30, .76, None, -12, (.04, .024, .014),
                  -.033, (.032, .018, (1.2, .82, 1.0)), .6, .026, extra=lateral, rings=32, seg=32)
    side_fins(root, sid, (.15, -.04, -.06), [((.16, -.02, .06), (.2, .018, .055), (0, -14, -6), 'Fin')])
    def tail(part):
        ellipsoid(part, (0, .01, .35), (.03, .045, .05), S('Fin'), (0, 0, 0), 14, 8)
        fan_tail(part, (0, .01, .35), ((-24, .15, .045, S('Fin')), (24, .15, .045, S('Fin'))), .02)
    simple_part(root, 'Tail pivot', (0, .01, .33), tail)
    def beads(part):
        dew_beads(part, sid, bead_row(prof, Z0, Z1, (.3, .42), (.03, .026)))
        ellipsoid(part, tuple(on_body(prof, Z0, Z1, .55, 90, 1.0) + Vector((0, .04, .03))), (.014, .045, .09), S('Fin'), (26, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', (0, .19, .0), beads)
    return root

# ---------------------------------------------------------------- Dewling crown: Rainbow Regent, a rainbow tail and a pearl crown
def rainbowregent():
    sid = 'rainbowregent'; S = lambda slot: slot_name(sid, slot)
    root = creature_root('DioramaRainbowregent')
    Z0, Z1 = -.42, .42
    prof = nose_profile(.46, .64, .3, .3, .27)
    loft_creature(root, sid, Z0, Z1, prof, .15, 30, (.098, .116, .08), 30, .77, None, -12, (.05, .03, .017),
                  -.04, (.04, .023, (1.2, .85, 1.0)), .62, .03)
    side_fins(root, sid, (.25, -.05, -.02), [((.05, -.06, .12), (.026, .075, .18), (14, 22, -40), 'Fin'),
                                            ((.04, -.1, .18), (.022, .06, .14), (26, 30, -58), 'Accent')])
    def tail(part):
        # A rainbow fan: five bands.
        ellipsoid(part, (0, 0, .41), (.045, .06, .06), S('Fin'), (0, 0, 0), 14, 8)
        for a, slot in ((-52, S('Accent')), (-26, 'DK_RainbowYellow'), (0, 'DK_RainbowGreen'), (26, S('Fin')), (52, S('Body'))):
            d = rot((0, 0, .18), (a, 0, 0))
            ellipsoid(part, (0, d.y, .42 + d.z), (.024, .06, .2), slot, (a, 0, 0), 20, 12)
    simple_part(root, 'Tail pivot', (0, 0, .39), tail)
    def crown(part):
        top = on_body(prof, Z0, Z1, .26, 90, 1.0)
        gold_crown(part, (0, top.y + .005, top.z), .09, 5, .08, 'DK_Gold', -10)
        # A pearl on every point.
        for k in range(5):
            a = math.tau * (k + .5) / 5
            base = Vector((0, top.y + .005, top.z)) + rot((.09 * math.cos(a), 0, .09 * math.sin(a)), (-10, 0, 0))
            dew_beads(part, sid, [(tuple(base + rot((0, .1, 0), (-10, 0, 0))), .022)])
    simple_part(root, 'Crest pivot', (0, .3, -.12), crown)
    return root

WAVE2_BUILDERS = {'emberpuff': emberpuff, 'cindertail': cindertail, 'coalbelly': coalbelly, 'sizzlesnout': sizzlesnout,
                  'kindlekoi': kindlekoi, 'sootsnap': sootsnap, 'ashfin': ashfin, 'phoenixfry': phoenixfry,
                  'dewdrop': dewdrop, 'fogfin': fogfin, 'mistlet': mistlet, 'drizzler': drizzler, 'pearlpip': pearlpip,
                  'haloling': haloling, 'rainskipper': rainskipper, 'rainbowregent': rainbowregent}

# ================================================================ GR-12 wave 3: the Reefback and the Saltscale
# Two new families on the same rig. The user chose their looks (2026-10-24): the Reefback's coral shell (big, gentle fish with
# a domed shell on the back crusted with little coral branches and barnacles; the corals ride its Crest pivot, which the game
# opens in its bloom) and the Saltscale's salt stripe (sleek, shiny fish with a glittering white salt stripe along each side,
# painted along the body's mesh lines in the accent, which glows; its dorsal sail rides the Crest pivot).

def coral_shell(part, sid, prof, Z0, Z1, t=.56, length=.32, width=.93, height=.62, rim=.1):
    """The Reefback's domed shell on its back, like a turtle's carapace: an ellipsoid centred halfway up the body, so it
    covers the back down to the upper flanks, in its fin colour, with a pale rim on the lip that overhangs the body,
    below its widest. Centred at the length fraction t; returns its centre and radii for the corals and barnacles on it."""
    S = lambda s: slot_name(sid, s)
    rx, ryt, _ = prof(t)
    top = on_body(prof, Z0, Z1, t, 90, 1.0)
    c = Vector((0, top.y * .5, top.z))
    R = (rx * width, top.y * height, (Z1 - Z0) * length)
    first = len(part.faces)
    ellipsoid(part, tuple(c), R, S('Fin'), (0, 0, 0), 48, 24)
    paint(part, first, tuple(c), R, S('Belly'), lambda n: n.y < rim)
    return c, R

def on_dome(c, R, az, el, k=1.0):
    """A point on a shell dome (front -Z): az degrees from the front toward the right side, el degrees up."""
    return on_ellipsoid(R, az, el, k, tuple(c))

def coral(part, sid, base, height, lean=0, turn=0, arms=3, r=.028, slot='Accent'):
    """A little coral: a stubby trunk that forks into chunky arms, each tipped with a round polyp."""
    S = lambda s: slot_name(sid, s)
    base = Vector(base)
    up = rot((0, height * .4, 0), (lean, turn, 0))
    mid = base + up
    taper_tube(part, [tuple(base), tuple(base + up * .5), tuple(mid)], r, r * .9, S(slot), 10)
    for k in range(arms):
        a = (k - (arms - 1) / 2) * (90 / max(1, arms - 1)) if arms > 1 else 0
        d = rot((0, height * .6, 0), (lean, turn, a))
        tip = mid + d
        taper_tube(part, [tuple(mid), tuple(mid + d * .5 + Vector((0, height * .08, 0))), tuple(tip)], r * .85, r * .7, S(slot), 10)
        ellipsoid(part, tuple(tip), (r * 1.25, r * 1.25, r * 1.25), S(slot), (0, 0, 0), 12, 8)

def brain_coral(part, sid, at, r=.04, slot='Accent'):
    """A round brain coral knob sitting on the shell."""
    S = lambda s: slot_name(sid, s)
    ellipsoid(part, tuple(at), (r, r * .75, r), S(slot), (0, 0, 0), 16, 10)

def barnacle(part, sid, at, normal, r=.024):
    """A barnacle: a pale stump with a dark opening on top."""
    S = lambda s: slot_name(sid, s)
    n = Vector(normal).normalized()
    at = Vector(at)
    cone(part, tuple(at - n * r * .4), tuple(at + n * r * 1.0), r, S('Belly'), 10, 4, .35)
    ellipsoid(part, tuple(at + n * r * .9), (r * .42, r * .42, r * .42), S('Fin'), (0, 0, 0), 10, 6)

def dome_normal(c, R, p):
    """The outward normal of a dome at a point on it."""
    d = Vector(p) - Vector(c)
    return Vector((d.x / R[0] ** 2, d.y / R[1] ** 2, d.z / R[2] ** 2)).normalized()

def flippers(root, sid, pivot, size=(.15, .026, .075), sweep=-18, droop=-14):
    """A Reefback's broad paddle flippers."""
    side_fins(root, sid, pivot, [((size[0] * .85, -.01, .02), size, (0, sweep, droop), 'Fin')])

def salt_stripe(sid, Z0, Z1, t0=.22, t1=.9, width=.16, band=0.0, prof=None):
    """The Saltscale's salt stripe along each side: a thin closed ribbon lying on the body, widest in its middle and
    tapering smoothly to a point at both ends (painted faces would step at a taper). width is its half-width in degrees
    round the body / 60 (so .16 is about 10 degrees); band moves it up or down the same way."""
    S = lambda s: slot_name(sid, s)
    half, lift = width * 60, band * 60
    def ribbon(body):
        n = 36
        for side in (1, -1):
            outer, inner = [], []
            for i in range(n + 1):
                u = i / n
                tt = t0 + (t1 - t0) * u
                a = max(.6, half * math.sin(math.pi * u) ** .5)
                centre = (0 if side > 0 else 180) + (lift if side > 0 else -lift)
                for k, out in ((1.012, outer), (.985, inner)):
                    out.append((tuple(on_body(prof, Z0, Z1, tt, centre + a, k)), tuple(on_body(prof, Z0, Z1, tt, centre - a, k))))
            verts, faces = [], []
            for i in range(n + 1):
                verts += [outer[i][0], outer[i][1], inner[i][0], inner[i][1]]
            for i in range(n):
                a0, a1 = 4 * i, 4 * (i + 1)
                faces.append((a0, a1, a1 + 1, a0 + 1))          # outer
                faces.append((a0 + 2, a0 + 3, a1 + 3, a1 + 2))  # inner
                faces.append((a0, a0 + 2, a1 + 2, a1))          # one edge
                faces.append((a0 + 1, a1 + 1, a1 + 3, a0 + 3))  # the other edge
            last = 4 * n
            faces.append((0, 1, 3, 2)); faces.append((last, last + 2, last + 3, last + 1))
            body.add(verts, faces, S('Accent'))
    return ribbon

def dorsal_sail(part, sid, prof, Z0, Z1, t, height, length, lean=28, slot='Fin'):
    """A Saltscale's dorsal sail: a blade from a root along the back, its tip swept back (a cone flattened side to side)."""
    S = lambda s: slot_name(sid, s)
    top = on_body(prof, Z0, Z1, t, 90, .9)
    tip = top + rot((0, height * 1.6, 0), (lean + 22, 0, 0)) + Vector((0, 0, length * .3))
    idx = cone(part, tuple(top), tuple(tip), length * .5, S(slot), 16, 7, .15)
    for i in idx:
        v = part.verts[i]
        part.verts[i] = Vector((v.x * .18, v.y, v.z))

# GR-12B wave 2: the Reefback's eight as animals of their own (their GR-12 builders replaced; the helpers are in the GR-12B section).
# ---------------------------------------------------------------- Reefback: Shellback, a sea turtle
def shellback():
    sid = 'shellback'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaShellback')
    spine = BPSpine([(0, 0, -.44), (0, .02, -.32), (0, .03, -.2), (0, .04, .05), (0, .05, .3)])
    body = BPBody(spine, bp_profile([(0, .07, .065, .06), (.06, .085, .08, .075), (.14, .07, .065, .06), (.22, .075, .07, .07), (.4, .17, .1, .1),
                                     (.8, .12, .08, .08), (1, .03, .03, .03)], nose=.06, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.55, .2), ring_paint=True)
    c, R = Vector((0, .07, .04)), (.27, .13, .31)
    first = len(B.faces)
    ellipsoid(B, tuple(c), R, S('Fin'), (0, 0, 0), 48, 24)  # its carapace
    paint(B, first, tuple(c), R, S('Belly'), lambda n: n.y < .08)
    ellipsoid(B, (0, .01, .04), (.24, .05, .28), S('Belly'), (0, 0, 0), 32, 12)  # its plastron
    # The plates of its shell, outlined.
    tube(B, [tuple(on_dome(c, R, 0 if el < 90 else 180, el if el <= 90 else 180 - el, 1.004)) for el in range(28, 153, 8)], .008, S('Body'), 6)
    for az in (55, -55, 125, -125):
        tube(B, [tuple(on_dome(c, R, az, el, 1.004)) for el in range(24, 70, 6)], .007, S('Body'), 6)
    for el in (52,):
        tube(B, [tuple(on_dome(c, R, az, el, 1.004)) for az in range(-180, 181, 10)], .007, S('Body'), 6)
    for az, el in ((60, 30), (-60, 30), (150, 35), (-150, 35)):
        p = on_dome(c, R, az, el, .98)
        barnacle(B, sid, p, dome_normal(c, R, p), .018)
    # Its beak's hook, and little rear flippers.
    tip = body.at(.01, 90, .7)
    cone(B, tuple(tip + Vector((0, .01, .02))), tuple(tip + Vector((0, -.025, -.03))), .03, S('Fin'), 10, 4, .3)
    for side in (1, -1):
        bp_plate(B, bp_outline([(side * .12, -.0, .24), (side * .22, -.03, .3), (side * .2, -.04, .35), (side * .12, -.02, .3)], 14), .022, S('Body'))
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.07, 32, .84)), (.045, .052, .04), 55, mouth_grin(body, .025, .08, -14, .008))
    def flipper(part, m):
        out = bp_outline([(.15, 0, -.2), (.3, -.03, -.19), (.46, -.07, -.1), (.48, -.08, -.04), (.35, -.05, -.06), (.16, -.02, -.12)], 28)
        bp_plate(part, [m(p) for p in out], .03, S('Body'))
    bp_fins(root, (.16, -.01, -.16), flipper)
    def tail(part):
        cone(part, (0, .04, .3), (0, .03, .38), .03, S('Body'), 10, 4, .3)
    simple_part(root, 'Tail pivot', (0, .04, .32), tail)
    top = Vector((c.x, c.y + R[1], c.z))
    def corals(part):
        reef(part, sid, None, corals=[(on_dome(c, R, 0, 72, .97), .13, -8, 0, 3), (on_dome(c, R, 120, 58, .97), .1, 14, 40, 3), (on_dome(c, R, -120, 58, .97), .1, 10, -40, 3)],
             brains=[(on_dome(c, R, 60, 66, .98), .032), (on_dome(c, R, -60, 66, .98), .032), (on_dome(c, R, 180, 64, .98), .03)])
    simple_part(root, 'Crest pivot', tuple(top), corals)
    return root

# ---------------------------------------------------------------- Reefback: Coralhorn, a cowfish with coral horns
def coralhorn():
    sid = 'coralhorn'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaCoralhorn')
    spine = BPSpine([(0, 0, -.32), (0, .01, 0), (0, .01, .26)])
    body = BPBody(spine, bp_profile([(0, .12, .1, .12), (.2, .17, .15, .16), (.6, .16, .14, .15), (.9, .08, .07, .07), (1, .05, .05, .05)],
                                    nose=.16, tail=.1))
    B = Part('Body')
    box = lambda t: 1.0 + 1.5 * max(0, min(1, (t - .05) / .1)) * max(0, min(1, (.92 - t) / .1))
    bp_loft(B, body, S('Body'), squarish=box, belly=bp_belly_to_nose(S, -.6), ring_paint=True)
    bp_spots(B, body, sid, [(t, a, .012) for t in (.24, .34, .44, .54, .64, .74) for a in (-20, 10, 40, 70, 90)], 'Belly')
    for side in (1, -1):  # horns at its back corners
        b0 = body.at(.82, -40 if side > 0 else -140, .95)
        cone(B, tuple(b0), tuple(b0 + Vector((side * .03, -.04, .12))), .022, S('Fin'), 10, 4, .1)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.16, 55, .82)), (.064, .074, .056), 38, mouth_o((0, -.035, -.322), .024, S('Fin'), (-5, 0, 0)))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.15, -.04, -.08), (.21, -.06, -.05), (.23, -.08, .0), (.18, -.08, .0), (.15, -.06, -.04)], 16)],
                 .016, S('Fin'))
    bp_fins(root, (.15, -.05, -.05), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .05, .26), (0, .1, .34), (0, .06, .4), (0, -.03, .41), (0, -.09, .36), (0, -.05, .27)], 22), .02, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .01, .27), tail)
    def horns(part):
        for side in (1, -1):  # its coral horns, branching over its eyes
            h0 = body.at(.1, 72 if side > 0 else 108, .95)
            tipp = h0 + Vector((side * .03, .06, -.12))
            cone(part, tuple(h0), tuple(tipp), .026, S('Accent'), 12, 5, .15)
            for f, d in ((.55, (side * .04, .05, .0)), (.75, (-side * .01, .05, -.02))):
                q = h0 + (tipp - h0) * f
                taper_tube(part, [tuple(q), tuple(q + Vector(d))], .012, .009, S('Accent'), 8)
        reef(part, sid, None, corals=[(body.at(.5, 90, .97), .08, -6, 0, 3), (body.at(.62, 70, .97), .06, 10, 30, 2)],
             brains=[(body.at(.4, 110, .99), .022)])
    simple_part(root, 'Crest pivot', tuple(body.at(.4, 90, 1.0)), horns)
    return root

# ---------------------------------------------------------------- Reefback: Clamjaw, a grouper with thick lips and a clam on its back
def clamjaw():
    sid = 'clamjaw'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaClamjaw')
    spine = BPSpine([(0, 0, -.4), (0, .01, -.1), (0, .01, .2), (0, .02, .36)])
    body = BPBody(spine, bp_profile([(0, .15, .14, .17), (.22, .22, .22, .23), (.5, .2, .21, .19), (.8, .1, .11, .09), (1, .05, .06, .05)],
                                    nose=.14, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.5), ring_paint=True)
    bp_spots(B, body, sid, [(t, a, .011) for t in (.18, .26, .34, .42, .5, .58, .66, .74) for a in (-10, 20, 50, 80)], 'Belly')
    bp_puffed(make_object(B, root), B, body, .14)
    bp_face(root, sid, tuple(body.at(.15, 34, .85)), (.07, .08, .06), 40,
            mouth_gape((0, -.06, -.405), .13, .045, S('Accent'), sid, tilt=14, rim_r=.032, depth=.012), eye_pitch=2)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.2, -.06, -.12), (.3, -.1, -.08), (.34, -.14, .0), (.27, -.13, .02), (.2, -.09, -.05)], 20)],
                 .022, S('Fin'))
    bp_fins(root, (.2, -.07, -.1), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .04, .35), (0, .12, .42), (0, .1, .5), (0, -.0, .52), (0, -.1, .49), (0, -.1, .41), (0, -.03, .35)], 24), .022, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .02, .35), tail)
    def crest(part):
        # Its spiny dorsal, a giant clam on its back with a pearl, and corals round it.
        lo, hi, sd = [], [], []
        for k in range(14):
            t = .28 + .36 * k / 13
            c, s, up = spine.frame(t)
            base = body.at(t, 90, .92)
            h = .07 + .025 * math.sin(k * 1.9)
            lo.append(tuple(base)); hi.append(tuple(base + up * h)); sd.append(tuple(s))
        bp_strip(part, lo, hi, .016, S('Fin'), side_dirs=sd)
        at = body.at(.48, 70, .96)
        for k, tilt in enumerate((18, -26)):
            first = len(part.faces)
            ellipsoid(part, tuple(at + Vector((.0, .02 + .02 * k, 0))), (.06, .02, .05), S('Fin') if k == 0 else S('Belly'), (tilt, 0, 30), 22, 10)
        ellipsoid(part, tuple(at + Vector((0, .04, -.01))), (.016, .016, .016), 'DK_Glint', (0, 0, 0), 10, 6)
        reef(part, sid, None, corals=[(body.at(.62, 100, .97), .07, 8, 20, 2), (body.at(.36, 110, .97), .06, -8, -20, 2)],
             brains=[(body.at(.56, 60, .99), .022)])
    simple_part(root, 'Crest pivot', tuple(body.at(.46, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Reefback: Kelpgrazer, a manatee with kelp growing on its back
def kelpgrazer():
    sid = 'kelpgrazer'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaKelpgrazer')
    spine = BPSpine([(0, -.03, -.40), (0, -.01, -.22), (0, .01, .05), (0, .02, .30)])
    body = BPBody(spine, bp_profile([(0, .1, .09, .1), (.1, .14, .13, .14), (.2, .15, .14, .15), (.45, .22, .21, .21), (.8, .15, .13, .13),
                                     (1, .06, .04, .04)], nose=.12, tail=.06))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.55), ring_paint=True)
    for side in (1, -1):  # its round whiskery muzzle and nostrils
        ellipsoid(B, (side * .032, -.045, -.385), (.055, .045, .045), S('Body'), (0, 0, 0), 18, 10)
        for k in range(6):
            a = math.radians(-60 + 24 * k)
            ellipsoid(B, (side * (.032 + .04 * math.cos(a)), -.045 + .03 * math.sin(a), -.42), (.005, .005, .005), S('Fin'), (0, 0, 0), 6, 4)
        ellipsoid(B, (side * .025, -.005, -.39), (.01, .005, .008), 'DK_Mouth', (0, 0, 0), 8, 4)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.12, 28, .9)), (.046, .052, .04), 55, mouth_arc((0, -.088, -.375), .04, .008, .008, .012))
    def flipper(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.17, -.1, -.22), (.26, -.15, -.2), (.31, -.2, -.13), (.26, -.19, -.1), (.17, -.13, -.15)], 18)],
                 .028, S('Body'))
        for k in range(3):
            ellipsoid(part, m((.27 + .012 * k, -.19 + .015 * k, -.14 - .015 * k)), (.008, .006, .008), S('Belly'), (0, 0, 0), 6, 4)
    bp_fins(root, (.17, -.1, -.2), flipper)
    def tail(part):
        circ = [(0.15 * math.sin(math.tau * k / 10), .02, .42 + .14 * math.cos(math.tau * k / 10) - .02) for k in range(10)]
        bp_plate(part, bp_outline(circ, 30), .03, S('Body'), normal=(0, 1, 0))
    simple_part(root, 'Tail pivot', (0, .02, .29), tail)
    def kelp(part):
        for k, (t, a, h) in enumerate(((.38, 80, .16), (.48, 105, .2), (.58, 70, .14))):
            base = body.at(t, a, .96)
            pts = [base + Vector((.012 * math.sin(s * 5 + k), h * s, .04 * s * s)) for s in (0, .25, .5, .75, 1)]
            lo = [tuple(p) for p in pts]; hi = [tuple(p + Vector((0, 0, .03 * (1 - i / 4) + .012))) for i, p in enumerate(pts)]
            bp_strip(part, lo, hi, .01, S('Accent'), side_dirs=[(1, 0, 0)] * 5)
        reef(part, sid, None, corals=[(body.at(.52, 60, .97), .06, 10, 30, 2)], brains=[(body.at(.44, 120, .99), .024), (body.at(.62, 100, .99), .02)])
    simple_part(root, 'Crest pivot', tuple(body.at(.48, 90, 1.0)), kelp)
    return root

# ---------------------------------------------------------------- Reefback: Barnaclebrow, a grey whale calf, barnacles on its brow
def barnaclebrow():
    sid = 'barnaclebrow'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaBarnaclebrow')
    spine = BPSpine([(0, 0, -.42), (0, .02, -.12), (0, .03, .18), (0, .05, .36)])
    body = BPBody(spine, bp_profile([(0, .09, .1, .11), (.15, .16, .18, .19), (.45, .21, .21, .21), (.8, .11, .11, .1), (1, .05, .05, .05)],
                                    nose=.12, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.55), ring_paint=True)
    bp_spots(B, body, sid, [(t, a, r) for t, a, r in ((.3, 40, .03), (.38, 10, .022), (.47, 60, .028), (.55, 25, .02), (.63, 50, .024), (.72, 15, .018),
                                                         (.42, 130, .02), (.58, 150, .022))], 'Belly')
    for t, a, r in ((.05, 90, .016), (.08, 70, .018), (.08, 110, .018), (.12, 90, .02), (.13, 60, .015), (.13, 120, .015), (.17, 80, .016),
                    (.17, 100, .016), (.1, 45, .013), (.1, 135, .013)):  # barnacles crusting its brow
        barnacle(B, sid, body.at(t, a, .99), body.normal(t, a), r)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.21, -2, .9)), (.042, .048, .038), 62, mouth_grin(body, .02, .19, 2, .009))
    def flipper(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.18, -.1, -.14), (.27, -.16, -.11), (.33, -.22, -.03), (.27, -.2, -.01), (.18, -.12, -.08)], 18)],
                 .026, S('Body'))
    bp_fins(root, (.18, -.1, -.12), flipper)
    def tail(part):
        fluke_lobes(part, S, (0, .05, .35), .2, .12, 'Body')
    simple_part(root, 'Tail pivot', (0, .05, .34), tail)
    def crest(part):
        for t in (.55, .62, .69, .76):
            p = body.at(t, 90, .95)
            ellipsoid(part, tuple(p + Vector((0, .012, 0))), (.022, .016, .026), S('Body'), (0, 0, 0), 10, 6)
        reef(part, sid, None, corals=[(body.at(.46, 90, .97), .08, -6, 0, 3)], brains=[(body.at(.4, 70, .99), .02), (body.at(.5, 115, .99), .02)])
    simple_part(root, 'Crest pivot', tuple(body.at(.55, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Reefback: Tidetusk, a walrus
def tidetusk():
    sid = 'tidetusk'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaTidetusk')
    spine = BPSpine([(0, -.02, -.36), (0, 0, -.2), (0, .01, .05), (0, .02, .3)])
    body = BPBody(spine, bp_profile([(0, .12, .12, .13), (.15, .17, .17, .17), (.25, .18, .18, .19), (.5, .24, .24, .25), (.85, .14, .12, .12),
                                     (1, .06, .05, .05)], nose=.12, tail=.06))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.6), ring_paint=True)
    for side in (1, -1):  # its whisker pads, whiskers and long tusks
        ellipsoid(B, (side * .045, -.05, -.352), (.056, .046, .045), S('Belly'), (0, 0, 0), 18, 10)
        for k in range(7):
            a = math.radians(-50 + 20 * k)
            p = Vector((side * (.045 + .042 * math.cos(a)), -.05 + .03 * math.sin(a), -.392))
            cone(B, tuple(p), tuple(p + Vector((side * .02, 0, -.012))), .003, S('Fin'), 6, 3, .1)
        cone(B, (side * .035, -.08, -.35), (side * .045, -.31, -.33), .019, S('Fin'), 12, 6, .05)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.13, 45, .87)), (.042, .048, .038), 42, mouth_arc((0, -.092, -.345), .024, .006, .007, .01))
    def flipper(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.2, -.12, -.14), (.29, -.19, -.12), (.33, -.25, -.05), (.27, -.24, -.03), (.2, -.15, -.08)], 18)],
                 .028, S('Body'))
    bp_fins(root, (.2, -.12, -.12), flipper)
    def tail(part):
        for side in (1, -1):  # its hind flippers, spread behind
            bp_plate(part, bp_outline([(side * .03, .0, .3), (side * .1, -.01, .36), (side * .15, -.02, .46), (side * .1, -.02, .47), (side * .04, -.01, .4)], 18),
                     .026, S('Body'), normal=(0, 1, 0))
    simple_part(root, 'Tail pivot', (0, .01, .3), tail)
    def crest(part):
        reef(part, sid, None, corals=[(body.at(.5, 90, .97), .09, -6, 0, 3), (body.at(.6, 120, .97), .06, 10, 40, 2)],
             brains=[(body.at(.42, 70, .99), .024)],
             barnacles=[(body.at(t, a, .99), body.normal(t, a), .016) for t, a in ((.4, 100, ), (.56, 60), (.66, 95), (.48, 130))])
    simple_part(root, 'Crest pivot', tuple(body.at(.5, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Reefback: Wavebreaker, a hammerhead, corals at its fin
def wavebreaker():
    sid = 'wavebreaker'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaWavebreaker')
    spine = BPSpine([(0, 0, -.32), (0, .01, 0), (0, .02, .25), (0, .04, .40)])
    body = BPBody(spine, bp_profile([(0, .07, .06, .06), (.12, .12, .11, .1), (.4, .15, .15, .13), (.75, .09, .09, .08), (.93, .04, .05, .04),
                                     (1, .025, .03, .025)], nose=.12, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.45), ring_paint=True)
    first = len(B.faces)
    ellipsoid(B, (0, .02, -.31), (.27, .034, .075), S('Body'), (0, 0, 0), 36, 12)  # its hammer
    paint(B, first, (0, .02, -.31), (.27, .034, .075), S('Belly'), lambda n: n.y < -.3)
    for side in (1, -1):  # pelvic fins, the second dorsal
        bp_plate(B, bp_outline([(side * .07, -.06, .2), (side * .14, -.09, .25), (side * .12, -.09, .28), (side * .07, -.06, .26)], 14), .016, S('Fin'))
    a2, b2 = body.at(.72, 90, .9), body.at(.8, 90, .9)
    bp_plate(B, bp_outline([tuple(a2), tuple(a2 + Vector((0, .05, .03))), tuple(b2 + Vector((0, .03, .02))), tuple(b2)], 12), .016, S('Fin'))
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, (.255, .035, -.31), (.042, .048, .038), 82, mouth_arc((0, -.032, -.27), .05, .012, .008, .02), eye_pitch=4)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.12, -.07, -.14), (.24, -.13, -.08), (.3, -.17, .02), (.23, -.15, .0), (.13, -.09, -.08)], 20)],
                 .022, S('Fin'))
    bp_fins(root, (.12, -.07, -.12), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .04, .38), (0, .13, .45), (0, .22, .55), (0, .12, .52), (0, .05, .45)], 20), .02, S('Fin'))
        bp_plate(part, bp_outline([(0, .02, .38), (0, .0, .45), (0, -.07, .49), (0, -.06, .43)], 14), .018, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .04, .38), tail)
    def crest(part):
        a, b = body.at(.36, 90, .9), body.at(.5, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .1, .04))), tuple(a + Vector((0, .17, .1))), tuple(b + Vector((0, .07, .04))), tuple(b)], 20), .024, S('Fin'))
        reef(part, sid, None, corals=[(body.at(.34, 70, .97), .06, -10, -30, 2), (body.at(.52, 110, .97), .055, 10, 30, 2)],
             brains=[(body.at(.44, 60, .99), .018), (body.at(.44, 120, .99), .018)])
    simple_part(root, 'Crest pivot', tuple(body.at(.42, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Reefback crown: Coralossus, a giant decorator crab wearing a reef
def coralossus():
    sid = 'coralossus'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaCoralossus')
    spine = BPSpine([(0, 0, -.3), (0, .02, 0), (0, 0, .28)])
    body = BPBody(spine, bp_profile([(0, .2, .08, .06), (.3, .34, .16, .1), (.7, .32, .15, .1), (1, .16, .06, .05)], nose=.22, tail=.22))
    B = Part('Body')
    bp_loft(B, body, S('Body'), squarish=lambda t: 1.3, belly=bp_belly_to_nose(S, -.3), ring_paint=True)
    for side in (1, -1):
        for k, t in enumerate((.36, .52, .68)):  # three walking legs a side, bent at the knee
            base = body.at(t, 0 if side > 0 else 180, .92)
            knee = base + Vector((side * .17, .07, .02 * (k - 1)))
            foot = knee + Vector((side * .12, -.2, .05 * (k - 1)))
            taper_tube(B, [tuple(base), tuple(knee)], .03, .024, S('Fin'), 10)
            ellipsoid(B, tuple(knee), (.03, .03, .03), S('Fin'), (0, 0, 0), 10, 6)
            taper_tube(B, [tuple(knee), tuple(foot)], .024, .012, S('Fin'), 10)
        stalk0 = Vector((side * .07, .1, -.24))
        taper_tube(B, [tuple(stalk0), tuple(stalk0 + Vector((side * .01, .1, -.04)))], .02, .016, S('Body'), 8)  # eye stalks
    bp_puffed(make_object(B, root), B, body, .08)
    bp_face(root, sid, (.08, .23, -.3), (.05, .058, .045), 20, mouth_arc((0, .02, -.31), .04, .01, .008, .015), eye_pitch=6)
    def claw(part, m):
        taper_tube(part, [m((.18, -.0, -.2)), m((.3, .02, -.32)), m((.27, .04, -.44))], .036, .03, S('Fin'), 10)
        ellipsoid(part, m((.26, .05, -.5)), (.06, .05, .08), S('Fin'), (0, 0, 0), 18, 10)
        cone(part, m((.24, .07, -.55)), m((.22, .08, -.68)), .03, S('Fin'), 10, 4, .2)
        cone(part, m((.27, .03, -.55)), m((.26, -.0, -.66)), .026, S('Belly'), 10, 4, .2)
    bp_fins(root, (.18, 0, -.2), claw)
    def tail(part):
        bp_plate(part, bp_outline([(0, -.04, .24), (.08, -.05, .28), (.06, -.06, .34), (-.06, -.06, .34), (-.08, -.05, .28)], 16), .02, S('Belly'), normal=(0, 1, 0))
    simple_part(root, 'Tail pivot', (0, -.04, .27), tail)
    def reefcrown(part):
        top = body.at(.5, 90, 1.0)
        gold_crown(part, (0, top.y + .02, top.z), .07, 5, .08, 'DK_Gold', 0)
        corals = [(body.at(t, a, .97), h, lean, turn, arms) for t, a, h, lean, turn, arms in
                  ((.3, 60, .12, -12, -30, 3), (.32, 120, .11, -10, 30, 3), (.62, 50, .1, 12, -40, 3), (.64, 130, .12, 14, 40, 3),
                   (.46, 30, .08, 0, -70, 2), (.48, 150, .08, 0, 70, 2), (.78, 90, .09, 18, 0, 3))]
        brains = [(body.at(t, a, .99), r) for t, a, r in ((.4, 80, .032), (.56, 100, .03), (.7, 70, .026), (.22, 90, .026), (.5, 20, .022), (.5, 160, .022))]
        reef(part, sid, None, corals=corals, brains=brains)
        for t, a in ((.38, 40), (.6, 140)):  # sponges
            p = body.at(t, a, .97)
            taper_tube(part, [tuple(p), tuple(p + Vector((0, .07, 0)))], .022, .026, S('Belly'), 10)
    simple_part(root, 'Crest pivot', tuple(body.at(.5, 90, 1.0)), reefcrown)
    return root

# GR-12B wave 3: the Saltscale's eight as animals of their own (their GR-12 builders replaced).
# ---------------------------------------------------------------- Saltscale: Brinefin, a mackerel
def brinefin():
    sid = 'brinefin'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaBrinefin')
    spine = BPSpine([(0, -.01, -.42), (0, .01, -.15), (0, .01, .12), (0, .02, .34)])
    body = BPBody(spine, bp_profile([(0, .05, .05, .05), (.12, .1, .115, .1), (.38, .125, .145, .125), (.66, .085, .095, .085),
                                     (.86, .032, .042, .032), (1, .022, .03, .022)], nose=.1, tail=.03))
    B = Part('Body')
    def paint(t, sn, cs):
        if sn < -.3 and t < .97:
            return S('Belly')
        if sn > .35 and .14 < t < .9 and math.sin(t * 62 + abs(cs) * 9) > .3:
            return S('Fin')  # the wavy bars of its back
        return None
    bp_loft(B, body, S('Body'), rings=56, seg=44, belly=paint, ring_paint=True)
    bp_salt_stripe(B, body, sid, .16, .86, .1, .05)
    for k in range(5):  # finlets above and below its tail stalk
        t = .64 + .05 * k
        for a, d in ((90, 1), (-90, -1)):
            p = body.at(t, a, .85)
            up = spine.frame(t)[2]
            cone(B, tuple(p), tuple(p + up * (d * .04) + Vector((0, 0, .03))), .011, S('Fin'), 6, 3, .1)
    bp_back_fin(B, body, .5, .6, .05, S('Fin'))  # the second dorsal and the anal fin
    bp_back_fin(B, body, .5, .6, .045, S('Fin'), angle=-90)
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.1, 22, .84)), (.044, .05, .04), 58, mouth_grin(body, .018, .075, -10, .007))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.1, -.02, -.24), (.17, -.05, -.18), (.19, -.07, -.1), (.15, -.05, -.13), (.1, -.04, -.2)], 16)],
                 .014, S('Fin'))
    bp_fins(root, (.1, -.03, -.22), pec)
    def tail(part):
        for d in (1, -1):  # a deep fork, two stiff curved lobes
            bp_plate(part, bp_outline([(0, .0, .32), (0, d * .07, .37), (0, d * .16, .47), (0, d * .13, .48), (0, d * .03, .4)], 18), .016, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .02, .32), tail)
    def crest(part):
        bp_back_fin(part, body, .26, .44, .085, S('Fin'), sweep=.2, peak=.15)
    simple_part(root, 'Crest pivot', tuple(body.at(.34, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Saltscale: Pebblejaw, a parrotfish
def pebblejaw():
    sid = 'pebblejaw'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaPebblejaw')
    spine = BPSpine([(0, -.02, -.38), (0, .02, -.12), (0, .02, .16), (0, .02, .32)])
    body = BPBody(spine, bp_profile([(0, .085, .09, .09), (.14, .15, .19, .16), (.42, .17, .22, .18), (.72, .11, .13, .11), (.88, .05, .06, .05),
                                     (1, .035, .045, .035)], nose=.16, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly_to_nose(S, -.55), ring_paint=True)
    for i, t in enumerate([.2 + .065 * k for k in range(10)]):  # its big scales, in rows like pebbles
        for a in ([-45, -15, 15, 45, 70] if i % 2 == 0 else [-30, 0, 30, 58]):
            for aa in (a, 180 - a):
                ellipsoid(B, tuple(body.at(t, aa, .975)), (.019, .019, .019), S('Fin'), (0, 0, 0), 8, 5)
    bp_salt_stripe(B, body, sid, .24, .82, .09)
    for y, r in ((-.008, (.046, .02, .026)), (-.046, (.042, .018, .024))):  # its beak, two plates of fused teeth
        ellipsoid(B, (0, y, -.382), r, S('Accent'), (0, 0, 0), 20, 10)
        tube(B, [(0, y + r[1] * .9, -.405), (0, y - r[1] * .9, -.405)], .003, 'DK_Mouth', 6)
    bp_back_fin(B, body, .62, .84, .04, S('Fin'), angle=-90, sweep=.3)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.16, 36, .84)), (.05, .058, .045), 46, mouth_arc((0, -.027, -.405), .038, .003, .006, .012), eye_pitch=4)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.15, -.04, -.12), (.22, -.06, -.1), (.27, -.1, -.04), (.22, -.11, -.02), (.16, -.08, -.07)], 18)],
                 .016, S('Fin'), rim=S('Accent'))
    bp_fins(root, (.15, -.05, -.1), pec)
    def tail(part):
        # A lyre: a square tail with long tips above and below.
        bp_plate(part, bp_outline([(0, .03, .3), (0, .1, .38), (0, .16, .5), (0, .06, .44), (0, .0, .43), (0, -.06, .44), (0, -.16, .5), (0, -.1, .38),
                                   (0, -.03, .3)], 30), .018, S('Fin'), rim=S('Accent'))
    simple_part(root, 'Tail pivot', (0, .02, .31), tail)
    def crest(part):
        lo, hi, sd = [], [], []
        for k in range(16):
            t = .2 + .62 * k / 15
            c, s, up = spine.frame(t)
            base = body.at(t, 90, .9)
            lo.append(tuple(base)); hi.append(tuple(base + up * (.055 - .01 * math.cos(k * 1.3)))); sd.append(tuple(s))
        bp_strip(part, lo, hi, .016, S('Fin'), rim=S('Accent'), side_dirs=sd)
    simple_part(root, 'Crest pivot', tuple(body.at(.5, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Saltscale: Seaspark, a banded sea snake
def seaspark():
    sid = 'seaspark'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaSeaspark')
    spine = BPSpine([(0, .02, -.52), (.015, .015, -.34), (-.03, .01, -.1), (.03, .0, .16), (-.03, -.005, .42), (.015, -.01, .62)], 141)
    body = BPBody(spine, bp_profile([(0, .035, .03, .03), (.04, .05, .042, .04), (.08, .042, .036, .035), (.14, .038, .036, .034),
                                     (.6, .046, .052, .046), (.88, .03, .055, .048), (1, .012, .03, .025)], nose=.04, tail=.03))
    B = Part('Body')
    def bands(t, sn, cs):
        if sn < -.62 and t < .97:
            return S('Belly')
        if t < .07 or (.11 < t < .97 and math.sin(t * math.pi * 17) > .25):
            return S('Fin')
        return None
    bp_loft(B, body, S('Body'), 0, 1, 130, 28, belly=bands, ring_paint=True)
    bp_salt_stripe(B, body, sid, .12, .92, .07)
    body_obj = make_object(B, root)
    bp_puffed(body_obj, B, body, .08)
    bp_wave_keys(body_obj, B, body, .055, 1.25, .15)
    bp_face(root, sid, tuple(body.at(.04, 40, .78)), (.03, .034, .026), 52, mouth_grin(body, .008, .04, -16, .005), eye_pitch=12)
    nose = spine.frame(0)[0]
    def tongue(part, m):
        # A forked tongue, a prong on each side.
        base = nose + Vector((0, -.012, .01))
        taper_tube(part, [m(tuple(base)), m(tuple(base + Vector((.004, -.003, -.035)))), m(tuple(base + Vector((.016, -.006, -.06))))], .006, .003, 'DK_Mouth', 6)
    bp_fins(root, (.006, nose.y - .012, nose.z + .01), tongue)
    end, tan = spine.frame(1.0)[0], spine.tangent(.98)
    def tail(part):
        # Its paddle tail, flat from side to side.
        bp_plate(part, bp_outline([tuple(end - tan * .06 + Vector((0, .045, 0))), tuple(end + tan * .04 + Vector((0, .07, 0))), tuple(end + tan * .08),
                                   tuple(end + tan * .04 - Vector((0, .06, 0))), tuple(end - tan * .06 - Vector((0, .04, 0)))], 22), .02, S('Fin'), rim=S('Accent'))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.97)[0]), tail)
    def crest(part):
        # Salt crystals crusting its crown.
        for t, a, h in ((.035, 90, .03), (.055, 70, .024), (.055, 110, .024), (.075, 90, .022)):
            p = body.at(t, a, .92)
            cone(part, tuple(p), tuple(p + Vector((0, h, .006))), .01, S('Accent'), 5, 2, .0)
    simple_part(root, 'Crest pivot', tuple(body.at(.055, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Saltscale: Foamtail, a dolphin with foam on its fluke
def foamtail():
    sid = 'foamtail'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaFoamtail')
    spine = BPSpine([(0, -.01, -.46), (0, .02, -.34), (0, .04, -.1), (0, .04, .18), (0, .05, .36)])
    body = BPBody(spine, bp_profile([(0, .03, .026, .03), (.07, .04, .036, .042), (.11, .085, .1, .09), (.18, .13, .15, .13), (.42, .155, .165, .155),
                                     (.75, .085, .09, .08), (.93, .035, .04, .032), (1, .025, .03, .022)], nose=.05, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), rings=56, seg=40, belly=bp_belly_to_nose(S, -.42), ring_paint=True)
    bp_salt_stripe(B, body, sid, .14, .7, .08, -.18)
    ellipsoid(B, tuple(body.at(.2, 90, .99)), (.016, .008, .01), 'DK_Mouth', (0, 0, 0), 10, 6)  # its blowhole
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.16, -4, .86)), (.04, .046, .036), 62, mouth_grin(body, .02, .12, -20, .008))
    def flipper(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.12, -.08, -.2), (.2, -.12, -.16), (.27, -.18, -.08), (.23, -.16, -.07), (.13, -.1, -.13)], 18)],
                 .022, S('Fin'))
    bp_fins(root, (.12, -.08, -.18), flipper)
    def tail(part):
        fluke_lobes(part, S, (0, .05, .35), .19, .12, 'Fin')
        for side in (1, -1):  # foam at the tips of its fluke
            tip = Vector((side * .19, .05, .46))
            for off, r in (((0, 0, 0), .016), ((side * -.022, .012, -.012), .014), ((side * .012, -.012, -.01), .013), ((side * -.035, .004, .012), .012),
                           ((side * -.05, -.006, -.004), .011), ((side * .0, .016, .014), .01), ((side * -.012, -.014, .018), .01), ((side * .02, .004, .01), .009)):
                ellipsoid(part, tuple(tip + Vector(off)), (r, r, r), S('Accent'), (0, 0, 0), 12, 8)
    simple_part(root, 'Tail pivot', (0, .05, .34), tail)
    def crest(part):
        bp_back_fin(part, body, .38, .56, .11, S('Body'), sweep=.45, peak=.2, thick=.022)
    simple_part(root, 'Crest pivot', tuple(body.at(.46, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Saltscale: Surfskipper, a penguin
def surfskipper():
    sid = 'surfskipper'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaSurfskipper')
    spine = BPSpine([(0, .03, -.46), (0, .05, -.36), (0, .05, -.24), (0, .03, .02), (0, .03, .26), (0, .04, .36)])
    body = BPBody(spine, bp_profile([(0, .012, .012, .01), (.08, .026, .026, .022), (.15, .095, .1, .09), (.23, .1, .11, .11), (.5, .15, .16, .17),
                                     (.85, .08, .08, .08), (1, .03, .03, .03)], nose=.03, tail=.05))
    B = Part('Body')
    def coat(t, sn, cs):
        if t < .085:
            return S('Body')  # its beak
        if sn < -.05 and .14 < t < .97:
            return S('Belly')
        return None
    bp_loft(B, body, S('Fin'), rings=56, seg=40, belly=coat, ring_paint=True)
    bp_salt_stripe(B, body, sid, .22, .86, .06, -.04)
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.14, 30, .86)), (.034, .04, .032), 58, mouth_grin(body, .03, .085, -2, .005), eye_pitch=4)
    def wing(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.1, .02, -.17), (.2, -.03, -.13), (.32, -.1, -.01), (.34, -.13, .06), (.28, -.11, .04), (.12, -.03, -.07)], 22)],
                 .024, S('Fin'), rim=S('Belly'), rim_from=.84)
    bp_fins(root, (.1, .0, -.14), wing)
    def tail(part):
        cone(part, (0, .04, .33), (0, .05, .43), .035, S('Fin'), 10, 4, .1)  # its stiff little tail
        for side in (1, -1):  # webbed feet trailing behind
            bp_plate(part, bp_outline([(side * .04, -.06, .3), (side * .09, -.07, .4), (side * .07, -.07, .43), (side * .03, -.065, .4)], 16), .02, S('Body'), normal=(0, 1, 0))
    simple_part(root, 'Tail pivot', (0, .03, .33), tail)
    def crest(part):
        # A crest of salt-white plumes over its eyes.
        for side in (1, -1):
            for k, (dz, dy) in enumerate(((.04, .05), (.07, .07), (.1, .06))):
                b0 = body.at(.15, 62 if side > 0 else 118, .95)
                taper_tube(part, [tuple(b0), tuple(b0 + Vector((side * .02, dy * .6, dz * .5))), tuple(b0 + Vector((side * .045, dy, dz)))], .009, .003, S('Accent'), 6)
    simple_part(root, 'Crest pivot', tuple(body.at(.16, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Saltscale: Shoalglider, a spotted eagle ray
def shoalglider():
    sid = 'shoalglider'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaShoalglider')
    spine = BPSpine([(0, -.01, -.4), (0, .0, -.26), (0, .01, 0), (0, .02, .2)])
    body = BPBody(spine, bp_profile([(0, .04, .028, .022), (.08, .065, .045, .035), (.18, .085, .06, .045), (.3, .16, .085, .05), (.6, .17, .08, .045),
                                     (1, .06, .04, .03)], nose=.08, tail=.1))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=lambda t, sn, cs: S('Belly') if sn < -.25 else None, ring_paint=True)
    bp_salt_stripe(B, body, sid, .3, .9, .06, .45)
    bp_spots(B, body, sid, [(.45, 70, .014), (.6, 75, .013), (.72, 80, .012)], 'Belly')
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.17, 12, .82)), (.038, .044, .034), 72, mouth_arc((0, -.026, -.37), .035, .008, .006, .014), eye_pitch=8)
    def wing(part, m):
        out = bp_outline([(.1, .01, -.2), (.26, .005, -.15), (.46, 0, -.04), (.62, -.005, .07), (.46, 0, .07), (.26, .01, .12), (.1, .02, .1)], 40)
        bp_plate(part, [m(p) for p in out], .05, S('Body'), rim=S('Accent'), rim_from=.86, normal=(0, 1, 0))
        for p in ((.2, .03, -.08), (.28, .025, -.03), (.36, .02, .02), (.26, .03, .05), (.44, .015, .04), (.18, .03, .03), (.33, .025, -.08)):
            ellipsoid(part, m(p), (.016, .008, .016), S('Belly'), (0, 0, 0), 10, 6)
    bp_fins(root, (.14, .0, -.05), wing)
    def tail(part):
        taper_tube(part, [(0, .015, .2), (0, .02, .36), (.02, .03, .52), (-.015, .03, .68), (.01, .025, .84)], .026, .004, S('Fin'), 10)
        cone(part, (0, .035, .26), (0, .05, .33), .009, S('Accent'), 8, 3, .1)  # its barb
    simple_part(root, 'Tail pivot', (0, .01, .2), tail)
    def crest(part):
        bp_plate(part, bp_outline([(0, .04, .1), (0, .085, .14), (0, .08, .18), (0, .04, .17)], 16), .016, S('Fin'))
    simple_part(root, 'Crest pivot', (0, .05, .14), crest)
    return root

# ---------------------------------------------------------------- Saltscale: Tidepip, a seahorse, upright
def tidepip():
    sid = 'tidepip'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaTidepip')
    # Its trunk stands upright, its back (+Z) the up of its frames, so 90 degrees round it is its back and -90 its belly.
    trunk = BPBody(BPSpine([(0, .27, .04), (0, .17, .0), (0, .05, -.02), (0, -.07, .01), (0, -.16, .07)], 81, up=(0, 0, 1)),
                   bp_profile([(0, .06, .06, .06), (.25, .075, .07, .1), (.55, .08, .07, .115), (.85, .058, .06, .07), (1, .042, .045, .045)], nose=.1, tail=.05))
    head = BPBody(BPSpine([(0, .3, -.31), (0, .31, -.2), (0, .32, -.1), (0, .31, .0), (0, .29, .06)]),
                  bp_profile([(0, .016, .018, .016), (.32, .018, .02, .018), (.48, .045, .05, .045), (.7, .072, .082, .068), (.9, .058, .06, .05),
                              (1, .03, .03, .03)], nose=.03, tail=.1))
    B = Part('Body')
    bp_loft(B, trunk, S('Body'), belly=lambda t, sn, cs: S('Belly') if sn < -.5 and .15 < t < .9 else None, ring_paint=True)
    bp_loft(B, head, S('Body'), rings=36, seg=28)
    bp_salt_stripe(B, trunk, sid, .1, .9, .1)
    for k in range(9):  # the bony rings of its trunk, a knob at each edge
        t = .08 + .1 * k
        for a in (55, 125, -40, -140):
            ellipsoid(B, tuple(trunk.at(t, a, .99)), (.011, .011, .011), S('Fin'), (0, 0, 0), 8, 5)
    for k, (dx, h) in enumerate(((0, .05), (-.02, .035), (.02, .035), (0, .03))):  # its coronet, crusted with salt
        p = head.at(.86, 90, .9) + Vector((dx, 0, -.012 * k))
        cone(B, tuple(p), tuple(p + Vector((0, h, .01))), .012, S('Accent'), 6, 3, .1)
    bp_puffed(make_object(B, root), B, trunk, .1)
    bp_face(root, sid, tuple(head.at(.74, 14, .84)), (.036, .042, .033), 70, mouth_o((0, .3, -.315), .014, S('Fin')))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.055, .32, .02), (.09, .345, .045), (.1, .3, .06), (.065, .285, .04)], 16)], .012, S('Fin'), normal=(1, 0, 0))
    bp_fins(root, (.055, .31, .03), pec)
    def tail(part):
        # Its tail curls down and forward under it, a tightening spiral.
        cy, cz = -.33, 0.0
        pts, radii = [], []
        for k in range(40):
            s = k / 39
            th = math.radians(67.6 - 390 * s)
            r = .184 * (1 - .82 * s)
            pts.append((0, cy + r * math.sin(th), cz + r * math.cos(th)))
            radii.append(.044 * (1 - s) + .008)
        swell_tube(part, pts, radii, S('Body'), 12)
    simple_part(root, 'Tail pivot', (0, -.16, .07), tail)
    a, b = trunk.at(.38, 90, .9), trunk.at(.66, 90, .9)
    def crest(part):
        # Its little back fin, which flutters to push it along.
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .01, .06))), tuple((a + b) / 2 + Vector((0, 0, .075))), tuple(b + Vector((0, -.01, .055))),
                                   tuple(b)], 20), .014, S('Fin'), rim=S('Accent'), normal=(1, 0, 0))
    simple_part(root, 'Crest pivot', tuple((a + b) / 2 + Vector((0, 0, .03))), crest)
    return root

# ---------------------------------------------------------------- Saltscale crown: Pearl Emperor, an ichthyosaur
def pearlemperor():
    sid = 'pearlemperor'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaPearlemperor')
    spine = BPSpine([(0, .01, -.54), (0, .02, -.36), (0, .03, -.2), (0, .03, .05), (0, .03, .3), (0, .04, .44)], 101)
    body = BPBody(spine, bp_profile([(0, .022, .018, .02), (.12, .032, .028, .028), (.2, .075, .08, .068), (.27, .11, .12, .1), (.46, .16, .17, .16),
                                     (.75, .1, .1, .09), (.93, .04, .05, .04), (1, .03, .035, .03)], nose=.03, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), rings=64, belly=bp_belly_to_nose(S, -.45), ring_paint=True)
    bp_salt_stripe(B, body, sid, .24, .88, .1)
    beads = [(tuple(body.at(t, a, 1.0)), .018) for t in (.32, .42, .52, .62, .72) for a in (0, 180)]
    dew_beads(B, sid, beads, 'Accent')  # pearls along its stripe
    eye = body.at(.215, 26, .8)
    for side in (1, -1):  # the ring of bony plates round each great eye
        torus(B, Vector((side * eye.x, eye.y, eye.z)) + rot((0, 0, -.01), (6, -side * 52, 0)), .074, .012, S('Fin'), (1, 1, 1), 28, 8, (6, -side * 52, 0))
    def rear(m):
        return [m(p) for p in bp_outline([(.12, -.12, .16), (.19, -.16, .2), (.24, -.2, .27), (.2, -.19, .29), (.13, -.13, .25)], 20)]
    for side in (1, -1):
        bp_plate(B, rear(lambda p, side=side: (side * p[0], p[1], p[2])), .026, S('Fin'))
    bp_puffed(make_object(B, root), B, body, .12)
    down, up_ = (0, -1, -.1), (0, 1, -.1)
    teeth = [(w, .018, .006, down) for w in (.08, .15, .22, .29, .36, .64, .71, .78, .85, .92)] + [(w, .016, .006, up_) for w in (.12, .19, .26, .33, .67, .74, .81, .88)]
    bp_face(root, sid, tuple(eye), (.064, .072, .056), 52, mouth_grin(body, .01, .2, -10, .007, teeth=teeth), eye_pitch=6)
    def flipper(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.12, -.08, -.2), (.22, -.13, -.17), (.33, -.2, -.08), (.3, -.19, -.05), (.13, -.1, -.12)], 22)],
                 .026, S('Fin'), rim=S('Accent'), rim_from=.84)
    bp_fins(root, (.12, -.08, -.17), flipper)
    def tail(part):
        # A crescent tail standing upright, its lower lobe the larger (its spine bends into it).
        bp_plate(part, bp_outline([(0, .02, .42), (0, -.06, .5), (0, -.17, .58), (0, -.18, .63), (0, -.08, .58), (0, .01, .5)], 22), .026, S('Fin'), rim=S('Accent'), rim_from=.82)
        bp_plate(part, bp_outline([(0, .04, .42), (0, .1, .5), (0, .16, .57), (0, .14, .6), (0, .07, .55), (0, .03, .48)], 22), .024, S('Fin'), rim=S('Accent'), rim_from=.82)
    simple_part(root, 'Tail pivot', (0, .04, .42), tail)
    def crown(part):
        top = body.at(.25, 90, 1.0)
        gold_crown(part, (0, top.y + .004, top.z), .075, 5, .07, 'DK_Gold', -6)
        for k in range(5):
            a = math.tau * (k + .5) / 5
            base = Vector((0, top.y + .004, top.z)) + rot((.075 * math.cos(a), 0, .075 * math.sin(a)), (-6, 0, 0))
            dew_beads(part, sid, [(tuple(base + rot((0, .085, 0), (-6, 0, 0))), .016)], 'Accent')
        bp_back_fin(part, body, .44, .6, .12, S('Fin'), rim=S('Accent'), sweep=.35, peak=.2, thick=.024)
    simple_part(root, 'Crest pivot', tuple(body.at(.4, 90, 1.0)), crown)
    return root

WAVE3_BUILDERS = {'shellback': shellback, 'coralhorn': coralhorn, 'clamjaw': clamjaw, 'kelpgrazer': kelpgrazer,
                  'barnaclebrow': barnaclebrow, 'tidetusk': tidetusk, 'wavebreaker': wavebreaker, 'coralossus': coralossus,
                  'brinefin': brinefin, 'pebblejaw': pebblejaw, 'seaspark': seaspark, 'foamtail': foamtail,
                  'surfskipper': surfskipper, 'shoalglider': shoalglider, 'tidepip': tidepip, 'pearlemperor': pearlemperor}

# ================================================================ GR-12 wave 4: the Gloamer and the Cometail
# The last two families on the same rig. The user chose their looks (2026-10-24): the Gloamer's glowing lure (big, dark, gentle
# deep-sea fish with a glowing lure dangling from the brow on a curved stalk, its Lantern pivot, which the game sways and lets
# its motes go from, and soft glowing spots along the sides) and the Cometail's comet tail (sleek fish with a bright glowing head
# and a long tail streaming behind like a comet's, in glowing wisps, on its Tail pivot).

def glow_spots(body, sid, prof, Z0, Z1, ts, angle=-28, r=.022, yoff=lambda t: 0):
    """The Gloamer's lights: little glowing spots along each lower flank (the accent, which glows)."""
    S = lambda s: slot_name(sid, s)
    for side in (1, -1):
        for t in ts:
            p = on_body(prof, Z0, Z1, t, angle if side > 0 else 180 - angle, 1.0, yoff)
            ellipsoid(body, tuple(p), (r, r, r), S('Accent'), (0, 0, 0), 12, 8)

def lure(root, sid, base, rise, reach, bulb, stalk=.02, bend=110, kind='round'):
    """The Gloamer's lure: a stalk from the brow curving up and over to a glowing bulb that dangles ahead of the mouth (the
    Sparkline's lantern helper, bent further), on the Lantern pivot."""
    make_object(lantern_on_stalk(root, sid, base, rise, reach, bulb, stalk, bend, kind), root, tuple(base), 'Lantern pivot')

def swell_tube(part, points, radii, material, sides=14):
    """A tube along points with a radius at each (closed at both ends): a comet's wisp."""
    verts, n = [], len(points)
    for i, p in enumerate(points):
        c = Vector(p)
        tan = (Vector(points[min(i + 1, n - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
        side = tan.cross(Vector((0, 1, 0)))
        if side.length < .01:
            side = tan.cross(Vector((1, 0, 0)))
        side.normalize(); other = tan.cross(side).normalized()
        for k in range(sides):
            a = math.tau * k / sides
            verts.append(tuple(c + radii[i] * (math.cos(a) * side + math.sin(a) * other)))
    faces = []
    for r in range(n - 1):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append((r * sides + k, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + k))
    faces.append(tuple(range(sides - 1, -1, -1)))
    faces.append(tuple((n - 1) * sides + k for k in range(sides)))
    return part.add(verts, faces, material)

def comet_tail(part, base, wisps):
    """The Cometail's signature: a comet's tail of wisps streaming back from the tail root, each swelling from a narrow root to
    its widest a third of the way back and thinning to a point, spreading apart as they go, as a comet's tail does.
    wisps: [(spread in degrees to the side, rise in degrees up, length, widest radius, slot)]."""
    base = Vector(base)
    for spread, rise, length, width, slot in wisps:
        pts, radii = [], []
        for k in range(13):
            s = k / 12
            d = rot((0, 0, length * s), (-rise * (.35 + .65 * s), spread * (.35 + .65 * s), 0))
            pts.append(tuple(base + d))
            swell = math.sin(math.pi * .5 * s / .35) if s < .35 else (1 - (s - .35) / .65) ** .85
            radii.append(width * swell + .006)
        swell_tube(part, pts, radii, slot, 14)

def comet_head(sid, prof, Z0, Z1, t=.2, radius=None):
    """The Cometail's bright head: its head, nose to the back of its eyes, in the accent, which glows (its belly kept), the
    comet's bright head before its tail."""
    S = lambda s: slot_name(sid, s)
    def marks(body):
        repaint(body, 0, Z0, Z1, S('Accent'), lambda sn, tt, cs: tt < t and sn > -.5)
    return marks

# ================================================================ GR-12B: body plans (GR.34)
# The user found (2026-10-24) that most creatures shared one face and body; GR-12B gives every species an aquatic animal of its
# own. These helpers build any body: a loft along a curved spine with frames that turn as little as they can (BPSpine,
# bp_profile, BPBody.at for points on its surface, bp_loft, bp_puffed), soft fin membranes and strips (bp_plate, bp_strip),
# mirrored fins (bp_fins), the house eyes with a mouth of any shape (bp_face; mouth_gape, mouth_grin, mouth_arc, mouth_o),
# and an undulating body's Wave keys (bp_wave_keys). Every model keeps the rig's names, so the game drives it as before.

def bp_catmull(points, n):
    """n points along a Catmull-Rom curve through points, evenly spaced by arc length."""
    P = [Vector(p) for p in points]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    segs = len(points) - 1
    fine = []
    m = max(400, n * 10)
    for i in range(m + 1):
        s = i / m * segs
        k = min(int(s), segs - 1); f = s - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        fine.append(.5 * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f * f * f))
    L = [0.0]
    for i in range(1, len(fine)):
        L.append(L[-1] + (fine[i] - fine[i - 1]).length)
    out, j = [], 0
    for i in range(n):
        target = L[-1] * i / (n - 1)
        while j < len(L) - 2 and L[j + 1] < target:
            j += 1
        span = L[j + 1] - L[j]
        f = 0 if span < 1e-9 else (target - L[j]) / span
        out.append(fine[j].lerp(fine[j + 1], min(1, max(0, f))))
    return out

class BPSpine:
    """A smooth spine through control points (head first), with frames that turn as little as they can (parallel transport).
    side is the creature's right (+X when the spine runs toward +Z), up its top."""
    def __init__(self, points, n=81, up=(0, 1, 0)):
        self.n = n
        self.pts = bp_catmull(points, n)
        self.sides, self.ups = [], []
        side = None
        for j in range(n):
            tan = (self.pts[min(j + 1, n - 1)] - self.pts[max(j - 1, 0)]).normalized()
            if side is None:
                side = Vector(up).cross(tan).normalized()
            side = (side - tan * side.dot(tan)).normalized()
            self.sides.append(side.copy()); self.ups.append(tan.cross(side).normalized())
    def frame(self, t):
        x = min(max(t, 0), 1) * (self.n - 1)
        k = min(int(x), self.n - 2); f = x - k
        c = self.pts[k].lerp(self.pts[k + 1], f)
        s = self.sides[k].lerp(self.sides[k + 1], f).normalized()
        up = self.ups[k].lerp(self.ups[k + 1], f).normalized()
        return c, s, up
    def tangent(self, t):
        a, b = self.frame(max(0, t - .01))[0], self.frame(min(1, t + .01))[0]
        return (b - a).normalized()

def bp_profile(keys, nose=.12, tail=.04):
    """A body profile from keys [(t, rx, ry_top, ry_bottom)], eased between keys, rounded off to a dome at the nose and a cap
    at the tail end. Returns profile(t) -> (rx, ryt, ryb)."""
    def lerp_keys(t):
        if t <= keys[0][0]:
            return keys[0][1:]
        for a, b in zip(keys, keys[1:]):
            if t <= b[0]:
                f = (t - a[0]) / (b[0] - a[0]); f = f * f * (3 - 2 * f)
                return tuple(a[i] + (b[i] - a[i]) * f for i in range(1, 4))
        return keys[-1][1:]
    def prof(t):
        rx, ryt, ryb = lerp_keys(t)
        d = 1.0
        if t < nose:
            d = math.sqrt(max(0, 1 - (1 - t / nose) ** 2))
        elif t > 1 - tail:
            d = math.sqrt(max(0, 1 - ((t - (1 - tail)) / tail) ** 2))
        return rx * d, ryt * d, ryb * d
    return prof

class BPBody:
    """What a spine loft made: the spine and profile (to place things on its surface) and each vertex's spine fraction, so
    the Puffed key can swell it from its spine."""
    def __init__(self, spine, prof):
        self.spine, self.prof = spine, prof
    def at(self, t, angle, k=1.0):
        """A point on the surface at spine fraction t and angle round it (0 the right side, 90 the top), k of the radius."""
        c, s, up = self.spine.frame(t)
        rx, ryt, ryb = self.prof(t)[:3]
        a = math.radians(angle); cs, sn = math.cos(a), math.sin(a)
        return c + s * (rx * cs * k) + up * ((ryt if sn > 0 else ryb) * sn * k)
    def normal(self, t, angle):
        p0 = self.at(t, angle, 1.0); c = self.spine.frame(t)[0]
        return (p0 - c).normalized()

def bp_loft(part, body, material, t0=0.0, t1=1.0, rings=40, seg=36, belly=None, squarish=None, caps=(True, True), ring_paint=False):
    """A closed loft along body's spine from t0 to t1 (a pole at each end where caps say so; an open end is closed flat).
    belly(t, sin, cos) -> slot or None paints faces. squarish(t) > 1 squares the section. ring_paint (GR-12B wave 2) paints
    the faces round a pole by their ring alone, so a paint edge stays straight to the nose rather than fanning into teeth."""
    spine, prof = body.spine, body.prof
    verts, params = [], []
    ts = [t0 + (t1 - t0) * r / rings for r in range(rings + 1)]
    inner = ts[1:-1] if caps[0] and caps[1] else ts[1:] if not caps[0] and caps[1] else ts[:-1] if caps[0] else ts
    if caps[0]:
        verts.append(tuple(spine.frame(t0)[0])); params.append((t0, 0, 0))
    for t in inner:
        c, s, up = spine.frame(t)
        rx, ryt, ryb = prof(t)[:3]
        rx, ryt, ryb = max(rx, .002), max(ryt, .002), max(ryb, .002)
        sq = squarish(t) if squarish else 1.0
        for k in range(seg):
            a = math.tau * k / seg
            x, y = math.cos(a), math.sin(a)
            if sq != 1.0:
                m = max((abs(x) ** sq + abs(y) ** sq) ** (1 / sq), 1e-6); x, y = x / m, y / m
            verts.append(tuple(c + s * (rx * x) + up * ((ryt if y > 0 else ryb) * y)))
            params.append((t, math.sin(a), math.cos(a)))
    if caps[1]:
        verts.append(tuple(spine.frame(t1)[0])); params.append((t1, 0, 0))
    faces = []
    o = 1 if caps[0] else 0
    nr = len(inner)
    if caps[0]:
        for k in range(seg):
            faces.append((0, o + k, o + (k + 1) % seg))
    else:
        faces.append(tuple(o + k for k in range(seg - 1, -1, -1)))
    for r in range(nr - 1):
        a, b = o + r * seg, o + (r + 1) * seg
        for k in range(seg):
            j = (k + 1) % seg
            faces.append((a + k, b + k, b + j, a + j))
    last = o + (nr - 1) * seg
    if caps[1]:
        end = len(verts) - 1
        for k in range(seg):
            faces.append((last + k, end, last + (k + 1) % seg))
    else:
        faces.append(tuple(last + k for k in range(seg)))
    first_face = len(part.faces)
    rng = part.add(verts, faces, material)  # faces and params both index this loft's own vertices here
    if belly:
        for fi, f in enumerate(faces):
            ring = [i for i in f if params[i][1] or params[i][2]] if ring_paint else f
            ring = ring or f
            t = sum(params[i][0] for i in ring) / len(ring)
            sn = sum(params[i][1] for i in ring) / len(ring); cs = sum(params[i][2] for i in ring) / len(ring)
            slot = belly(t, sn, cs)
            if slot:
                part.slots[first_face + fi] = part.slot(slot)
    return rng

def bp_puffed(obj, part, body, gain=.15):
    """The Puffed key for a spine body: every vertex of the part swells away from the nearest point of the spine, most at
    the middle of its length."""
    pts = body.spine.pts; n = len(pts)
    def puffed(i, v):
        v = Vector(v)
        best, bj = 1e9, 0
        for j in range(0, n, 2):
            d = (pts[j] - v).length_squared
            if d < best:
                best, bj = d, j
        t = bj / (n - 1)
        g = 1 + gain * math.sin(math.pi * min(1, max(0, t))) ** .8
        return pts[bj] + (v - pts[bj]) * g
    shape_key(obj, part, 'Puffed', puffed)

def bp_plate(part, outline, thick, material, rim=None, rim_from=.72, normal=None, curl=0.0):
    """A soft fin membrane: a pillowy slab over a closed outline (roughly planar, star-shaped about its centre), thickest
    in the middle and thin at the edge, with an optional band of the rim slot round its edge."""
    P = [Vector(p) for p in outline]
    c = sum(P, Vector()) / len(P)
    if normal is None:
        nrm = Vector((0, 0, 0))
        for i in range(len(P)):
            a, b = P[i] - c, P[(i + 1) % len(P)] - c
            nrm += a.cross(b)
        nrm = nrm.normalized()
    else:
        nrm = Vector(normal).normalized()
    n = len(P)
    rings = (0.0, .45, rim_from, .9, 1.0)
    heights = (1.0, .92, .62, .42, .2)
    verts = []
    for side in (1, -1):
        verts.append(tuple(c + nrm * (thick * .5 * side)))
        for f, h in zip(rings[1:], heights[1:]):
            for p in P:
                q = c + (p - c) * f
                bend = nrm * (curl * f * f)
                verts.append(tuple(q + bend + nrm * (thick * .5 * h * side)))
    per = 1 + n * (len(rings) - 1)
    faces, slots = [], []
    for side in (0, 1):
        o = side * per
        for i in range(n):
            j = (i + 1) % n
            f = (o, o + 1 + i, o + 1 + j)
            faces.append(f if side == 0 else f[::-1]); slots.append(0)
        for r in range(len(rings) - 2):
            for i in range(n):
                j = (i + 1) % n
                a, b = o + 1 + r * n, o + 1 + (r + 1) * n
                f = (a + i, b + i, b + j, a + j)
                faces.append(f if side == 0 else f[::-1]); slots.append(1 if r >= 1 else 0)
    lo, hi = 1 + (len(rings) - 2) * n, per + 1 + (len(rings) - 2) * n
    for i in range(n):
        j = (i + 1) % n
        faces.append((lo + i, lo + j, hi + j, hi + i)); slots.append(1)
    first = len(part.faces)
    rng = part.add(verts, faces, material)
    if rim:
        for fi, s in enumerate(slots):
            if s:
                part.slots[first + fi] = part.slot(rim)
    return rng

def bp_outline(points, n=40, closed=True):
    """A smooth closed outline through points (Catmull-Rom, evenly spaced)."""
    pts = list(points) + [points[0], points[1]] if closed else list(points)
    curve = bp_catmull(pts[:len(points) + 1], n + 1)
    return [tuple(p) for p in curve[:-1]]

def bp_spots(part, body, sid, items, slot='Accent'):
    """Little round lights on the body's surface: items [(t, angle, radius)], both sides mirrored when angle isn't 90/-90."""
    for t, angle, r in items:
        for a in ((angle, 180 - angle) if abs(abs(angle) - 90) > 1 else (angle,)):
            p = body.at(t, a, .985)
            ellipsoid(part, tuple(p), (r, r, r), slot_name(sid, slot), (0, 0, 0), 12, 8)

def bp_face(root, sid, eye_c, eye_r, eye_yaw, mouth, eye_pitch=0, pupil_k=.6, blush=None, lid_slot='Body', smile=.025):
    """The house eyes (glossy whites, pupils, glints and lids with Blink and Happy) placed where the animal has them, a mouth
    of its own built by mouth(part) -> (centre, half width) into Lips (with Happy and Surprised), and blush only if given
    (blush=(centre, radii, yaw))."""
    S = lambda slot: slot_name(sid, slot)
    eyes, pupils, glints, lids, bl = Part('Eyes'), Part('Pupils'), Part('Glints'), Part('Lids'), Part('Blush')
    lid_builders = []
    for side in (-1, 1):
        c = (side * eye_c[0], eye_c[1], eye_c[2])
        eul = (eye_pitch, -side * eye_yaw, 0)
        ellipsoid(eyes, c, eye_r, 'DK_EyeWhite', eul, 24, 14)
        fwd = rot((0, 0, -1), eul)
        pr = (eye_r[0] * pupil_k, eye_r[1] * pupil_k * 1.06, eye_r[2] * .5)
        pc = Vector(c) + fwd * (eye_r[2] * .72) + Vector((0, -.004, 0))
        ellipsoid(pupils, pc, pr, 'DK_Pupil', eul, 20, 12)
        upv = rot((0, 1, 0), eul); sidev = rot((1, 0, 0), eul)
        gc = Vector(c) + fwd * (eye_r[2] * 1.15) + upv * (eye_r[1] * .29) - sidev * (side * (.006 + eye_r[0] * .16))
        ellipsoid(glints, gc, (eye_r[0] * .22, eye_r[1] * .2, eye_r[2] * .17), 'DK_Glint', eul, 12, 8)
        lid_r = (eye_r[0] * 1.07, eye_r[1] * 1.06, eye_r[2] * 1.08)
        idx, build = shell(lids, c, lid_r, S(lid_slot), eul, math.radians(24), .010, 24, 8)
        lid_builders.append((idx, build))
        if blush:
            bc, br, by = blush
            ellipsoid(bl, (side * bc[0], bc[1], bc[2]), br, S('Blush'), (0, -side * by, 0), 16, 8)
    if not blush:
        # The rig keeps a Blush part: a speck hidden inside the head, for animals that don't blush.
        ellipsoid(bl, (0, eye_c[1], eye_c[2] + .05), (.004, .004, .004), S('Blush'), (0, 0, 0), 6, 4)
    for p in (eyes, pupils, glints, bl):
        make_object(p, root)
    lids_obj = make_object(lids, root)
    for name, angle in (('Blink', 104), ('Happy', 58)):
        moved = {}
        for idx, build in lid_builders:
            for i, v in zip(idx, build(math.radians(angle))):
                moved[i] = v
        shape_key(lids_obj, lids, name, lambda i, v, moved=moved: moved[i])
    lips = Part('Lips')
    lc, hw = mouth(lips)
    lc = Vector(lc)
    lips_obj = make_object(lips, root)
    def happy(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * 1.1, d.y * .85 + smile * min(1.5, (d.x / hw) ** 2) - .004, d.z))
    def surprised(i, v):
        d = Vector(v) - lc
        return lc + Vector((d.x * .95, d.y * 1.5, d.z * 1.05))
    shape_key(lips_obj, lips, 'Happy', happy)
    shape_key(lips_obj, lips, 'Surprised', surprised)

# ---------------------------------------------------------------- mouths (each builds into the Lips part)
def mouth_grin(body, t_mid, t_corner, a_corner, r=.012, sid=None, teeth=None, lip=None, k=1.0, depth=.0):
    """A smile drawn along the snout: a dark line from one corner (at spine fraction t_corner, angle a_corner on the right)
    under the snout (t_mid at the bottom) to the other. teeth: [(where along the line 0..1, length, radius, up?)]."""
    def build(part):
        pts = []
        for k_ in range(15):
            s = k_ / 14
            ang = (-180 - a_corner) + (a_corner - (-180 - a_corner)) * s
            w = abs(s - .5) * 2
            t = t_mid + (t_corner - t_mid) * w ** 1.6
            pts.append(body.at(t, ang, k + depth))
        tube(part, [tuple(p) for p in pts], r, 'DK_Mouth', 10)
        if lip:
            tube(part, [tuple(p + Vector((0, r * 1.2, 0))) for p in pts[2:-2]], r * .8, lip, 8)
        for where, length, rad, up in (teeth or ()):
            i = min(14, max(0, int(round(where * 14))))
            base = pts[i]
            d = Vector((0, 1 if up else -1, 0)) if isinstance(up, bool) else Vector(up).normalized()
            tip = base + d * length
            cone(part, tuple(base - d * rad * .5), tuple(tip), rad, 'DK_EyeWhite', 10, 4, .15)
        mid = pts[7]; width = (pts[0] - pts[14]).length / 2
        return tuple(mid), max(width, .02)
    return build

def mouth_gape(center, rx, ry, rim, sid, upper=(), lower=(), tilt=0, rim_r=.03, depth=.06):
    """A wide open mouth on the front of the face: a dark hollow, a rim of lips round it, and rounded teeth along the
    upper and lower edges [(across -1..1, length, radius)], pointing in."""
    def build(part):
        c = Vector(center)
        ellipsoid(part, tuple(c + rot((0, 0, depth * .4), (tilt, 0, 0))), (rx * .92, ry * .9, depth), 'DK_Mouth', (tilt, 0, 0), 28, 14)
        torus(part, c, 1.0, rim_r, rim, (rx, ry, 1), 40, 12, (tilt, 0, 0))
        for teeth, sign in ((upper, 1), (lower, -1)):
            for x, length, rad in teeth:
                a = math.acos(max(-1, min(1, x)))
                p = c + rot((rx * math.cos(a), sign * ry * math.sin(a) * .97, 0), (tilt, 0, 0))
                tip = p + rot((0, -sign * length, -.006), (tilt, 0, 0))
                cone(part, tuple(p), tuple(tip), rad, 'DK_EyeWhite', 10, 4, .2)
        return tuple(c), rx
    return build

def mouth_o(center, r, rim, euler=(0, 0, 0)):
    """A small round pursed mouth (a sunfish's), a ring of lips with a dark middle."""
    def build(part):
        c = Vector(center)
        torus(part, c, r, r * .55, rim, (1, 1, 1), 28, 10, euler)
        ellipsoid(part, tuple(c + rot((0, 0, .004), euler)), (r * .8, r * .8, .01), 'DK_Mouth', euler, 14, 8)
        return tuple(c), r * 1.4
    return build

def bp_lure(root, sid, base, rise, reach, bulb, stalk=.02, bend=110, kind='round'):
    lure(root, sid, tuple(base), rise, reach, bulb, stalk, bend, kind)

def bp_fins(root, pivot, build):
    """The Left and Right fin pivots, mirrored: build(part, m) draws the right fin (+X) through m(point), which mirrors x for
    the left one, and returns nothing."""
    for side, label in ((-1, 'Left'), (1, 'Right')):
        part = Part(label + ' fin')
        build(part, lambda p, side=side: (side * p[0], p[1], p[2]))
        make_object(part, root, (side * pivot[0], pivot[1], pivot[2]), label + ' fin pivot')

def bp_strip(part, lower, upper, thick, material, rim=None, seg=12, side_dirs=None):
    """A fin strip between two edges (lists of points, front to back): a flat tube whose section is a thin lens from the
    lower edge to the upper, rounded at both ends; rim paints its upper edge."""
    n = len(lower)
    verts, faces, slots = [], [], []
    for i in range(n):
        lo, hi = Vector(lower[i]), Vector(upper[i])
        c = (lo + hi) / 2; up = hi - lo
        h = max(up.length / 2, 1e-4); up = up.normalized()
        tan = (Vector(lower[min(i + 1, n - 1)]) - Vector(lower[max(i - 1, 0)])).normalized()
        sd = Vector(side_dirs[i]) if side_dirs else tan.cross(up).normalized()
        end = 1.0 if 0 < i < n - 1 else .35
        for k in range(seg):
            a = math.tau * k / seg
            verts.append(tuple(c + up * (h * math.sin(a)) + sd * (thick * .5 * math.cos(a) * end)))
    for r in range(n - 1):
        for k in range(seg):
            j = (k + 1) % seg
            faces.append((r * seg + k, r * seg + j, (r + 1) * seg + j, (r + 1) * seg + k))
            slots.append(math.sin(math.tau * (k + .5) / seg) > .55)
    faces.append(tuple(range(seg - 1, -1, -1))); slots.append(False)
    faces.append(tuple((n - 1) * seg + k for k in range(seg))); slots.append(False)
    first = len(part.faces)
    rng = part.add(verts, faces, material)
    if rim:
        for fi, s in enumerate(slots):
            if s:
                part.slots[first + fi] = part.slot(rim)
    return rng

def mouth_arc(center, width, droop, r=.01, back=.02, right=(1, 0, 0), up=(0, 1, 0), teeth=(), lip=None):
    """A smile on a face that looks forward: a dark line curving down from its corners (droop) to its middle, its corners
    set back by back to follow the face. teeth: [(across -1..1, length, radius, (dx, dy, dz))]."""
    def build(part):
        c, rt, upv = Vector(center), Vector(right), Vector(up)
        fwd = rt.cross(upv).normalized()  # toward the back of the head (+Z for a creature facing -Z)
        pts = []
        for k in range(13):
            x = -1 + 2 * k / 12
            pts.append(c + rt * (width * x) + upv * (droop * (x * x - 1)) + fwd * (back * x * x))
        tube(part, [tuple(p) for p in pts], r, 'DK_Mouth', 10)
        if lip:
            tube(part, [tuple(p - upv * r * 1.3) for p in pts[2:-2]], r * .8, lip, 8)
        for x, length, rad, d in teeth:
            base = c + rt * (width * x) + upv * (droop * (x * x - 1)) + fwd * (back * x * x)
            d = Vector(d).normalized()
            cone(part, tuple(base - d * rad * .4), tuple(base + d * length), rad, 'DK_EyeWhite', 10, 4, .15)
        return tuple(c + upv * (-droop * .5)), width
    return build

def bp_wave_keys(obj, part, body, amp=.055, waves=1.2, start=.2):
    """An undulating body's four Wave keys (WaveSin and WaveCos, each with its negative, so the game's weights stay positive):
    each vertex moves sideways by amp, growing from nothing at start of the body's length to full at its tail, times the sine
    or the cosine of its place along the body (waves of them from head to tail). Weighted by the cosine and the sine of the
    swim's phase, they make a wave that runs from head to tail."""
    pts, sides = body.spine.pts, body.spine.sides
    n = len(pts)
    near = []
    for v in part.verts:
        best, bj = 1e9, 0
        for j in range(n):
            d = (pts[j] - v).length_squared
            if d < best:
                best, bj = d, j
        near.append(bj)
    def key(name, fn, sign):
        def moved(i, v):
            s = near[i] / (n - 1)
            e = 0.0 if s <= start else (s - start) / (1 - start)
            e = e * e * (3 - 2 * e)
            return Vector(v) + sides[near[i]] * (sign * amp * e * fn(math.tau * waves * s))
        shape_key(obj, part, name, moved)
    key('WaveSin', math.sin, 1); key('WaveSinNeg', math.sin, -1); key('WaveCos', math.cos, 1); key('WaveCosNeg', math.cos, -1)

# GR-12B wave 2's helpers: a Cometail's glowing head and comet wisps, an ellipse-sectioned tube (an ammonite's shell), a
# Reefback's reef patches and a whale's fluke.
def bp_head_belly(S, head=.17, below=-.5, t_from=.06, t_to=.97):
    """A Cometail's paint: its head, nose to the back of its eyes, in the accent (which glows), its belly kept."""
    def paint(t, sn, cs):
        if t < head and sn > -.55:
            return S('Accent')
        return S('Belly') if sn < below and (t < head or t_from < t) and t < t_to else None
    return paint

def bp_tube(part, centers, ups, rx, ry, material, seg=18, caps=True):
    """A tube along centers whose section is an ellipse: ry along ups (one per point) and rx across them (both lists)."""
    n = len(centers)
    verts, faces = [], []
    for i in range(n):
        c = Vector(centers[i])
        tan = (Vector(centers[min(i + 1, n - 1)]) - Vector(centers[max(i - 1, 0)])).normalized()
        up = Vector(ups[i]); up = (up - tan * up.dot(tan)).normalized()
        side = tan.cross(up).normalized()
        for k in range(seg):
            a = math.tau * k / seg
            verts.append(tuple(c + side * (rx[i] * math.cos(a)) + up * (ry[i] * math.sin(a))))
    for r in range(n - 1):
        for k in range(seg):
            j = (k + 1) % seg
            faces.append((r * seg + k, r * seg + j, (r + 1) * seg + j, (r + 1) * seg + k))
    if caps:
        faces.append(tuple(range(seg - 1, -1, -1)))
        faces.append(tuple((n - 1) * seg + k for k in range(seg)))
    return part.add(verts, faces, material)

def comet_wisps(S, width=1.0, length=1.0):
    a, f = S('Accent'), S('Fin')
    return ((0, 4, .5 * length, .06 * width, a), (-17, 8, .42 * length, .046 * width, a), (17, 8, .42 * length, .046 * width, a),
            (-9, -12, .4 * length, .04 * width, f), (9, -12, .4 * length, .04 * width, f))

def bp_belly_to_nose(S, below=-.5, t_to=.97):
    return lambda t, sn, cs: S('Belly') if sn < below and t < t_to else None

def reef(part, sid, spots, corals=(), brains=(), barnacles=()):
    """A patch of reef: corals [(point, height, lean, turn, arms)], brain corals [(point, r)], barnacles [(point, normal, r)]."""
    for p, h, lean, turn, arms in corals:
        coral(part, sid, tuple(p), h, lean, turn, arms)
    for p, r in brains:
        brain_coral(part, sid, tuple(p), r)
    for p, n, r in barnacles:
        barnacle(part, sid, tuple(p), tuple(n), r)

def fluke_lobes(part, S, at, span=.2, depth=.12, slot='Fin'):
    """A whale's fluke lying flat across the water: two lobes swept back from its tail stock."""
    at = Vector(at)
    for side in (1, -1):
        out = bp_outline([tuple(at), tuple(at + Vector((side * span * .5, 0, depth * .3))), tuple(at + Vector((side * span, 0, depth * .85))),
                          tuple(at + Vector((side * span * .8, 0, depth))), tuple(at + Vector((side * span * .2, 0, depth * .75)))], 20)
        bp_plate(part, out, .024, S(slot), normal=(0, 1, 0))

# GR-12B wave 3's helpers: a Saltscale's salt stripe on a body-plan body, fins standing on it (a dorsal, a long flowing
# fin, a frilled fan), a Cinderling's row of ember spines, and short legs with feet and claws.
def bp_salt_stripe(part, body, sid, t0=.22, t1=.88, width=.16, lift=0.0):
    """The Saltscale's salt stripe on a body-plan body: a thin closed ribbon lying on each side, widest in its middle and
    tapering to a point at both ends; width is its half-width in degrees round the body / 60, lift moves it up the side."""
    S = lambda s: slot_name(sid, s)
    half, up = width * 60, lift * 60
    n = 36
    for side in (1, -1):
        outer, inner = [], []
        for i in range(n + 1):
            uu = i / n
            tt = t0 + (t1 - t0) * uu
            a = max(.6, half * math.sin(math.pi * uu) ** .5)
            centre = up if side > 0 else 180 - up
            for k, out in ((1.012, outer), (.985, inner)):
                out.append((tuple(body.at(tt, centre + a, k)), tuple(body.at(tt, centre - a, k))))
        verts, faces = [], []
        for i in range(n + 1):
            verts += [outer[i][0], outer[i][1], inner[i][0], inner[i][1]]
        for i in range(n):
            a0, a1 = 4 * i, 4 * (i + 1)
            faces.append((a0, a1, a1 + 1, a0 + 1)); faces.append((a0 + 2, a0 + 3, a1 + 3, a1 + 2))
            faces.append((a0, a0 + 2, a1 + 2, a1)); faces.append((a0 + 1, a1 + 1, a1 + 3, a0 + 3))
        last = 4 * n
        faces.append((0, 1, 3, 2)); faces.append((last, last + 2, last + 3, last + 1))
        part.add(verts, faces, S('Accent'))

def bp_back_fin(part, body, t0, t1, height, material, rim=None, sweep=.4, angle=90, k=.9, thick=.018, peak=.35):
    """A fin standing on the body at angle (90 the back, -90 the belly) from t0 to t1: its leading edge rising to height at
    peak of its length and sweeping back by sweep of its root's length."""
    a, b = body.at(t0, angle, k), body.at(t1, angle, k)
    c, s, up = body.spine.frame((t0 + t1) / 2)
    if angle < 0:
        up = -up
    L = (b - a).length
    tip = a + (b - a) * (peak + sweep) + up * height
    pts = [a, a + (b - a) * peak * .5 + up * height * .55, tip, b + up * height * .25, b]
    return bp_plate(part, bp_outline([tuple(p) for p in pts], 24), thick, material, rim=rim, rim_from=.8)

def bp_ember_row(part, sid, body, ts, heights, lean=22, radius=.03, angle=90, k=.9):
    """A row of the Cinderling's ember spines on a body-plan body: at its length fractions, leaning back."""
    ember_spines(part, sid, [(tuple(body.at(t, angle, k)), h, lean, radius) for t, h in zip(ts, heights)])

def bp_leg(part, S, hip, knee, foot, r0, r1, slot='Body', toes=4, toe=.03):
    """A short leg bent at the knee, a flat foot and little clawed toes splayed forward."""
    hip, knee, foot = Vector(hip), Vector(knee), Vector(foot)
    side = 1 if hip.x >= 0 else -1
    taper_tube(part, [tuple(hip), tuple((hip + knee) / 2), tuple(knee), tuple((knee + foot) / 2), tuple(foot)], r0, r1 * .85, S(slot), 12)
    pad = foot + Vector((0, -r1 * .35, -r1 * .6))
    ellipsoid(part, tuple(pad), (r1 * 1.25, r1 * .5, r1 * 1.45), S(slot), (0, 0, 0), 14, 8)
    for k in range(toes):
        a = math.radians(-45 + 90 * k / max(1, toes - 1))
        d = Vector((math.sin(a) * side, -.1, -math.cos(a))).normalized()
        base = pad + d * r1 * .9
        taper_tube(part, [tuple(base), tuple(base + d * toe)], r1 * .32, r1 * .24, S(slot), 8)
        cone(part, tuple(base + d * toe), tuple(base + d * (toe + r1 * .7) + Vector((0, -r1 * .2, 0))), r1 * .2, S('Belly'), 6, 3, .1)

def bp_long_fin(body, t0, t1, height, angle=90, sweep=.14, lobes=6, amp=.08, n=40, k=.88):
    """A long flowing fin along the back (angle 90) or the belly (-90) from t0 to t1, its free edge rising toward its back
    end, swept back past it and gently scalloped."""
    a, b = body.at(t0, angle, k), body.at(t1, angle, k)
    pts = [tuple(a)]
    for i in range(n + 1):
        s = i / n
        c, side, up = body.spine.frame(t0 + (t1 - t0) * s)
        if angle < 0:
            up = -up
        h = height * (.45 + .55 * s) * (1 + amp * math.sin(math.pi * lobes * s))
        pts.append(tuple(a.lerp(b, s) + up * h + Vector((0, 0, sweep * s * s))))
    pts.append(tuple(b))
    return pts

def bp_frill(base, phi0, phi1, radius, lobes=6, amp=.15, n=60):
    """A frilled fin fanning from base in its upright plane (phi 0 straight back, 90 straight up): its edge scalloped into
    lobes, radius a length or a function of the share of the way round."""
    base = Vector(base)
    pts = [tuple(base)]
    for i in range(n + 1):
        s = i / n
        phi = math.radians(phi0 + (phi1 - phi0) * s)
        r = radius(s) if callable(radius) else radius
        r *= 1 - amp * (1 - abs(math.sin(math.pi * lobes * s)))
        pts.append(tuple(base + Vector((0, math.sin(phi) * r, math.cos(phi) * r))))
    return pts

# ---------------------------------------------------------------- GR-12B wave 1: the Gloamer's eight as animals of their own
def bp_belly(S, below=-.5, t_from=.06, t_to=.97):
    return lambda t, sn, cs: S('Belly') if sn < below and t_from < t < t_to else None

# ---------------------------------------------------------------- Moonmouth: an anglerfish
def moonmouth():
    sid = 'moonmouth'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaMoonmouth')
    spine = BPSpine([(0, 0, -.40), (0, 0, -.12), (0, .03, .18), (0, .06, .40)])
    body = BPBody(spine, bp_profile([(0, .30, .26, .30), (.25, .37, .35, .35), (.5, .25, .24, .23), (.74, .085, .1, .085),
                                     (1, .05, .06, .05)], nose=.14, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.45, .1))
    # The jutting lower jaw: a heavy chin under the gape.
    ellipsoid(B, (0, -.17, -.345), (.2, .075, .075), S('Belly'), (-10, 0, 0), 28, 14)
    bp_spots(B, body, sid, [(.32, -12, .024), (.42, -14, .022), (.52, -16, .02), (.62, -18, .017), (.38, -40, .018), (.5, -42, .016)])
    for side in (1, -1):
        tube(B, [tuple(body.at(t, (-28 if side > 0 else -152) + 6 * (t - .05) / .2, 1.0)) for t in (.03, .08, .13, .18, .23)], .012, 'DK_Mouth', 10)
    bp_puffed(make_object(B, root), B, body, .14)
    eye = body.at(.13, 58, .84)
    bp_face(root, sid, tuple(eye), (.085, .1, .075), 30,
            mouth_gape((0, -.09, -.405), .2, .115, S('Belly'), sid,
                       upper=[(-.7, .04, .014), (-.35, .05, .016), (0, .05, .016), (.35, .05, .016), (.7, .04, .014)],
                       lower=[(-.8, .045, .015), (-.5, .06, .018), (-.15, .065, .019), (.15, .065, .019), (.5, .06, .018), (.8, .045, .015)],
                       tilt=16, rim_r=.032, depth=.016), eye_pitch=14)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.27, -.04, .0), (.36, -.08, .05), (.42, -.12, .13), (.36, -.17, .15), (.29, -.13, .07)], 24)],
                 .03, S('Fin'))
    bp_fins(root, (.27, -.08, .03), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .07, .37), (0, .12, .44), (0, .1, .5), (0, .03, .52), (0, -.04, .5), (0, -.07, .44), (0, -.03, .37)], 30),
                 .03, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .05, .37), tail)
    def crest(part):
        a, b = body.at(.56, 90, .9), body.at(.72, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .09, .03))), tuple(a + Vector((0, .11, .09))), tuple(b + Vector((0, .07, .02))), tuple(b)], 24),
                 .025, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.6, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.09, 90, .96), .3, .12, .085, .022, 150)
    return root

# ---------------------------------------------------------------- Gloomfin: an ocean sunfish
def gloomfin():
    sid = 'gloomfin'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaGloomfin')
    spine = BPSpine([(0, 0, -.30), (0, 0, -.05), (0, 0, .24)])
    body = BPBody(spine, bp_profile([(0, .09, .2, .2), (.3, .14, .31, .31), (.65, .13, .3, .3), (1, .1, .25, .25)], nose=.18, tail=.1))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.55, .08))
    bp_spots(B, body, sid, [(.35, 10, .02), (.47, 25, .018), (.58, 5, .02), (.68, 30, .016), (.5, -20, .018), (.75, -5, .016)])
    bp_puffed(make_object(B, root), B, body, .12)
    eye = body.at(.13, 26, .86)
    bp_face(root, sid, tuple(eye), (.075, .088, .068), 52, mouth_o((0, -.045, -.302), .026, S('Belly')))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.11, .02, -.17), (.15, .04, -.14), (.18, .03, -.09), (.15, -.0, -.09), (.11, -.0, -.13)], 20)],
                 .022, S('Body'), rim=S('Fin'))
    bp_fins(root, (.11, .01, -.15), pec)
    def clavus(part):
        # The sunfish's tail: a frilly scalloped rim round its back.
        c = Vector((0, 0, .06))
        for k in range(9):
            th = math.radians(-64 + 128 * k / 8)
            p = c + Vector((0, .24 * math.sin(th), .2 * math.cos(th) + .02))
            ellipsoid(part, tuple(p), (.022, .062, .055), S('Fin'), (-math.degrees(th), 0, 0), 18, 10)
    simple_part(root, 'Tail pivot', (0, 0, .26), clavus)
    fin_outline = [(0, .26, -.0), (0, .42, .06), (0, .6, .16), (0, .68, .25), (0, .62, .3), (0, .44, .25), (0, .26, .22)]
    def crest(part):
        bp_plate(part, bp_outline(fin_outline, 32), .035, S('Fin'), rim=S('Accent'), rim_from=.8)
    simple_part(root, 'Crest pivot', (0, .3, .1), crest)
    def keel(part):
        bp_plate(part, bp_outline([(0, -y, z) for _, y, z in fin_outline][::-1], 32), .035, S('Fin'), rim=S('Accent'), rim_from=.8)
    simple_part(root, 'Keel pivot', (0, -.3, .1), keel)
    bp_lure(root, sid, body.at(.07, 90, .96), .16, .1, .05, .016, 140)
    return root

# ---------------------------------------------------------------- Shadeback: a coelacanth
def shadeback():
    sid = 'shadeback'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaShadeback')
    spine = BPSpine([(0, 0, -.40), (0, .01, -.1), (0, 0, .2), (0, 0, .36)])
    body = BPBody(spine, bp_profile([(0, .15, .15, .16), (.25, .21, .23, .22), (.55, .19, .21, .18), (.82, .085, .1, .08),
                                     (1, .05, .06, .05)], nose=.2, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.55, .12, .9))
    bp_spots(B, body, sid, [(.3, 20, .03), (.38, 46, .022), (.45, 4, .028), (.52, 34, .025), (.6, -12, .024), (.66, 22, .02),
                            (.72, 48, .018), (.4, -28, .02), (.78, 6, .016)])
    # Fleshy lobed fins: the pelvic pair, the second dorsal and the anal, each a lobe and a little fan.
    def lobed(part, base, out, length, rad, fan):
        base, out = Vector(base), Vector(out).normalized()
        tip = base + out * length
        cone(part, tuple(base - out * .015), tuple(tip), rad, S('Body'), 14, 6, .35)
        back = Vector((0, 0, 1))
        a = (back - out * back.dot(out)).normalized()
        pts = [tip - out * .01 + a * -.012, tip + out * fan * .55 + a * -.02, tip + out * fan + a * .02, tip + out * fan * .8 + a * fan * .55,
               tip + out * fan * .25 + a * fan * .55, tip + a * .03]
        bp_plate(part, bp_outline([tuple(p) for p in pts], 26), .016, S('Fin'))
    lobed(B, body.at(.74, 90, .92), (0, 1, .5), .05, .022, .08)
    lobed(B, body.at(.74, -90, .92), (0, -1, .5), .05, .022, .08)
    for side in (1, -1):
        lobed(B, body.at(.56, -62 if side > 0 else -118, .92), (side * .6, -.8, .55), .045, .02, .075)
    bp_puffed(make_object(B, root), B, body, .14)
    eye = body.at(.16, 22, .84)
    bp_face(root, sid, tuple(eye), (.08, .09, .07), 44, mouth_grin(body, .035, .14, -22, .011), eye_pitch=4)
    def pec(part, m):
        cone(part, m((.18, -.08, -.1)), m((.26, -.13, -.03)), .03, S('Body'), 14, 6, .35)
        bp_plate(part, [m(p) for p in bp_outline([(.25, -.12, -.04), (.31, -.15, .0), (.35, -.2, .07), (.3, -.2, .1), (.25, -.15, .02)], 24)],
                 .018, S('Fin'))
    bp_fins(root, (.2, -.09, -.08), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .04, .34), (0, .15, .4), (0, .18, .5), (0, .07, .5), (0, .02, .43)], 24), .022, S('Fin'))
        bp_plate(part, bp_outline([(0, -.04, .34), (0, -.02, .43), (0, -.07, .5), (0, -.18, .5), (0, -.15, .4)], 24), .022, S('Fin'))
        ellipsoid(part, (0, 0, .46), (.024, .032, .085), S('Body'), (0, 0, 0), 14, 8)
        ellipsoid(part, (0, 0, .555), (.012, .05, .04), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, 0, .33), tail)
    def crest(part):
        a, b = body.at(.34, 90, .9), body.at(.46, 90, .9)
        top = [tuple(a), tuple(a + Vector((0, .1, .0))), tuple(a + Vector((0, .15, .05))), tuple(b + Vector((0, .13, .03))),
               tuple(b + Vector((0, .05, .03))), tuple(b)]
        bp_plate(part, bp_outline(top, 28), .022, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.4, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.14, 90, .96), .2, .15, .055, .018, 120)
    return root

# ---------------------------------------------------------------- Hushray: a stingray
def hushray():
    sid = 'hushray'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaHushray')
    spine = BPSpine([(0, 0, -.30), (0, .01, 0), (0, .02, .22)])
    body = BPBody(spine, bp_profile([(0, .14, .06, .035), (.3, .22, .095, .045), (.7, .17, .08, .04), (1, .07, .045, .03)], nose=.25, tail=.12))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=lambda t, sn, cs: S('Belly') if sn < -.2 else None)
    for side in (1, -1):
        ellipsoid(B, (side * .075, .07, -.14), (.06, .045, .06), S('Body'), (0, 0, 0), 18, 10)  # eye mounds
        ellipsoid(B, (side * .085, .085, -.06), (.022, .008, .016), 'DK_Mouth', (0, 0, 0), 12, 6)  # spiracles
    bp_spots(B, body, sid, [(.5, 90, .016), (.62, 90, .014), (.74, 90, .012)])
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, (.075, .1, -.15), (.06, .07, .055), 18,
            mouth_arc((0, -.022, -.285), .1, .02, .009, .05), eye_pitch=42, pupil_k=.62)
    def wing(part, m):
        out = bp_outline([(.12, .01, -.25), (.28, .005, -.2), (.42, 0, -.08), (.46, 0, .02), (.4, 0, .12), (.26, .01, .17),
                          (.12, .02, .14), (.1, .03, -.06)], 40)
        bp_plate(part, [m(p) for p in out], .075, S('Fin'), rim=S('Accent'), rim_from=.84, normal=(0, 1, 0))
        for p in ((.24, .04, -.12), (.31, .035, -.05), (.36, .03, .03), (.27, .04, .03), (.2, .045, .07)):
            ellipsoid(part, m(p), (.015, .01, .015), S('Accent'), (0, 0, 0), 10, 6)
    bp_fins(root, (.16, .0, -.04), wing)
    def tail(part):
        taper_tube(part, [(0, .015, .2), (0, .025, .34), (.02, .035, .48), (-.01, .035, .62), (0, .03, .74)], .032, .007, S('Fin'), 12)
        cone(part, (0, .04, .33), (0, .07, .42), .012, S('Accent'), 10, 4, .1)
    simple_part(root, 'Tail pivot', (0, .01, .2), tail)
    def crest(part):
        bp_plate(part, bp_outline([(0, .05, .12), (0, .1, .17), (0, .09, .21), (0, .05, .2)], 16), .018, S('Fin'))
    simple_part(root, 'Crest pivot', (0, .06, .16), crest)
    bp_lure(root, sid, (0, .095, -.2), .15, .12, .045, .014, 130)
    return root

# ---------------------------------------------------------------- Duskdrifter: an oarfish
def duskdrifter():
    sid = 'duskdrifter'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaDuskdrifter')
    spine = BPSpine([(0, 0, -.44), (.012, .005, -.22), (-.02, .01, .02), (.025, 0, .26), (-.02, -.01, .47), (.015, 0, .62)], 121)
    body = BPBody(spine, bp_profile([(0, .058, .11, .1), (.12, .066, .13, .115), (.5, .056, .115, .095), (.85, .036, .075, .058),
                                     (1, .016, .04, .03)], nose=.08, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), 0, 1.0, 80, 28, belly=bp_belly(S, -.55, .05))
    lo, hi, sd = [], [], []
    for k in range(48):
        t = .07 + .9 * k / 47
        c, s, up = spine.frame(t)
        base = body.at(t, 90, .9)
        h = .075 * (1 - .4 * t) * math.sin(math.pi / 2 * min(1, (t - .07) / .08)) ** .5
        lo.append(tuple(base)); hi.append(tuple(base + up * max(h, .006))); sd.append(tuple(s))
    bp_strip(B, lo, hi, .02, S('Fin'), rim=S('Accent'), side_dirs=sd)
    bp_spots(B, body, sid, [(t, -30, .013) for t in (.16, .24, .32, .4, .48, .56, .64, .72, .8)])
    body_obj = make_object(B, root)
    bp_puffed(body_obj, B, body, .1)
    bp_wave_keys(body_obj, B, body)
    def tail(part):
        # The oarfish has hardly a tail fin: a little tuft at the end of its ribbon.
        end, tan = spine.frame(1.0)[0], spine.tangent(.98)
        bp_plate(part, bp_outline([tuple(end - tan * .03 + Vector((0, .03, 0))), tuple(end + tan * .05 + Vector((0, .045, 0))),
                                   tuple(end + tan * .07), tuple(end + tan * .05 - Vector((0, .04, 0))), tuple(end - tan * .03 - Vector((0, .025, 0)))], 20),
                 .016, S('Fin'), rim=S('Accent'))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.97)[0]), tail)
    eye = body.at(.065, 18, .82)
    bp_face(root, sid, tuple(eye), (.05, .06, .046), 56, mouth_arc((0, -.03, -.437), .03, .01, .007, .015), eye_pitch=4)
    def oars(part, m):
        pts = [(.03, -.11, -.32), (.06, -.17, -.22), (.08, -.2, -.06), (.09, -.21, .1)]
        taper_tube(part, [m(p) for p in pts], .009, .006, S('Fin'), 10)
        ellipsoid(part, m((.09, -.212, .14)), (.014, .03, .05), S('Accent'), (0, 0, 0), 14, 8)
    bp_fins(root, (.03, -.1, -.33), oars)
    def crest(part):
        for k, (t, lean, length) in enumerate(((.05, 22, .2), (.065, 34, .26), (.08, 46, .3), (.095, 56, .27), (.11, 66, .22), (.125, 74, .17))):
            base = body.at(t, 90, .9)
            pts = [tuple(base + rot((0, length * s, 0), (lean * s, 0, 0)) + Vector((0, 0, .02 * s * s))) for s in (0, .25, .5, .75, 1)]
            taper_tube(part, pts, .012, .007, S('Fin'), 10)
            ellipsoid(part, pts[-1], (.014, .014, .014), S('Accent'), (0, 0, 0), 10, 6)
    simple_part(root, 'Crest pivot', tuple(body.at(.08, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.04, 90, .96), .14, .1, .045, .014, 130, 'flame')
    return root

# ---------------------------------------------------------------- Murkmaw: a viperfish
def murkmaw():
    sid = 'murkmaw'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaMurkmaw')
    spine = BPSpine([(0, 0, -.40), (0, .02, -.15), (0, 0, .15), (0, .02, .42)])
    body = BPBody(spine, bp_profile([(0, .1, .1, .15), (.18, .17, .18, .23), (.4, .15, .16, .15), (.75, .08, .09, .07),
                                     (1, .035, .045, .035)], nose=.14, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_belly(S, -.55, .14))
    bp_spots(B, body, sid, [(t, -58, .016) for t in (.24, .32, .4, .48, .56, .64, .72)] + [(t, -78, .013) for t in (.28, .36, .44, .52, .6)])
    bp_puffed(make_object(B, root), B, body, .13)
    eye = body.at(.12, 38, .84)
    fang = (0, 1, -.5)
    bp_face(root, sid, tuple(eye), (.075, .086, .066), 38,
            mouth_grin(body, .03, .17, -14, .013, teeth=[(.4, .17, .02, fang), (.6, .17, .02, fang), (.27, .05, .011, True),
                                                         (.73, .05, .011, True), (.33, .045, .01, False), (.67, .045, .01, False)]))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.15, -.08, -.18), (.22, -.1, -.13), (.25, -.14, -.05), (.2, -.15, -.06), (.15, -.12, -.12)], 20)],
                 .02, S('Fin'))
    bp_fins(root, (.15, -.1, -.15), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .02, .39), (0, .1, .45), (0, .16, .56), (0, .1, .55), (0, .02, .47)], 20), .02, S('Fin'))
        bp_plate(part, bp_outline([(0, -.02, .39), (0, -.02, .47), (0, -.1, .55), (0, -.16, .56), (0, -.1, .45)], 20), .02, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .02, .39), tail)
    def crest(part):
        a, b = body.at(.62, 90, .9), body.at(.72, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .07, .02))), tuple(b + Vector((0, .06, .03))), tuple(b)], 16), .02, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.66, 90, 1.0)), crest)
    bp_lure(root, sid, body.at(.3, 90, .96), .26, .24, .05, .012, 162)
    return root

# ---------------------------------------------------------------- Nightlantern: a vampire squid
def nightlantern():
    sid = 'nightlantern'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaNightlantern')
    spine = BPSpine([(0, -.08, -.1), (0, .1, -.02), (0, .3, .12)], 61, up=(0, 0, -1))
    body = BPBody(spine, bp_profile([(0, .18, .18, .18), (.35, .23, .23, .23), (.75, .19, .19, .19), (1, .1, .1, .1)], nose=.16, tail=.22))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=lambda t, sn, cs: S('Belly') if sn < -.6 and t < .5 else None)
    for side in (1, -1):
        ellipsoid(B, (side * .2, .2, .08), (.028, .028, .028), S('Accent'), (0, 0, 0), 14, 8)  # lights at the fin bases
    bp_puffed(make_object(B, root), B, body, .12)
    eye = body.at(.32, 58, .82)
    bp_face(root, sid, tuple(eye), (.098, .11, .082), 26, mouth_arc(tuple(body.at(.1, 90, 1.0)), .045, .015, .009, .02), eye_pitch=-4)
    def ears(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.17, .24, .02), (.26, .3, .06), (.32, .3, .12), (.27, .24, .14), (.18, .2, .1)], 24)],
                 .03, S('Fin'))
    bp_fins(root, (.18, .23, .07), ears)
    def cloak(part):
        c = Vector((0, -.12, -.1))
        tips = []
        for k in range(8):
            phi = math.tau * (k + .5) / 8
            rad = Vector((math.sin(phi), 0, -math.cos(phi)))
            pts = [c + rad * .05, c + rad * .15 + Vector((0, -.08, 0)), c + rad * .24 + Vector((0, -.13, 0)),
                   c + rad * .3 + Vector((0, -.12, 0)), c + rad * .33 + Vector((0, -.08, 0))]
            taper_tube(part, [tuple(p) for p in pts], .028, .011, S('Body'), 10)
            ellipsoid(part, tuple(pts[-1] + Vector((0, .006, 0))), (.022, .022, .022), S('Accent'), (0, 0, 0), 12, 8)
            for s in (.35, .6):  # soft spines (cirri) along each arm
                q = c + rad * (.05 + .25 * s) + Vector((0, -.1 * s - .02, 0))
                cone(part, tuple(q), tuple(q + Vector((0, -.04, 0)) + rad * .01), .008, S('Fin'), 8, 3, .1)
            tips.append([c + rad * .07, c + rad * .17 + Vector((0, -.09, 0)), c + rad * .25 + Vector((0, -.125, 0))])
        for k in range(8):
            a, b = tips[k], tips[(k + 1) % 8]
            web = [tuple(c + Vector((0, -.02, 0))), tuple(a[1]), tuple(a[2]), tuple((a[2] + b[2]) / 2 + Vector((0, .03, 0))), tuple(b[2]), tuple(b[1])]
            bp_plate(part, web, .014, S('Fin'))
    simple_part(root, 'Tail pivot', (0, -.12, -.1), cloak)
    def crest(part):
        top = body.at(.97, 90, .5)
        cone(part, tuple(top), tuple(top + Vector((0, .06, .05))), .05, S('Body'), 14, 5, .4)
    simple_part(root, 'Crest pivot', tuple(body.at(1.0, 90, 0)), crest)
    bp_lure(root, sid, body.at(.72, 90, .96), .12, .14, .05, .013, 125, 'fizz')
    return root

# ---------------------------------------------------------------- Eclipse Leviathan: a mosasaur, its lure an eclipse
def eclipseleviathan():
    sid = 'eclipseleviathan'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaEclipseleviathan')
    spine = BPSpine([(0, .04, -.56), (0, .03, -.3), (0, 0, -.05), (0, 0, .2), (0, .03, .46)], 101)
    body = BPBody(spine, bp_profile([(0, .04, .03, .035), (.08, .075, .062, .062), (.19, .12, .115, .1), (.26, .11, .11, .105),
                                     (.42, .18, .18, .19), (.7, .125, .135, .12), (.9, .055, .07, .05), (1, .03, .04, .03)],
                                    nose=.05, tail=.03))
    B = Part('Body')
    bp_loft(B, body, S('Body'), rings=60, belly=bp_belly(S, -.5, .03))
    for side in (1, -1):  # brow ridges over the eyes
        p = body.at(.17, 52 if side > 0 else 128, 1.0)
        ellipsoid(B, tuple(p), (.05, .022, .065), S('Body'), (0, 0, 0), 16, 8)
    def rear(m):
        return [m(p) for p in bp_outline([(.12, -.12, .16), (.2, -.17, .2), (.26, -.21, .26), (.22, -.2, .29), (.13, -.13, .25)], 22)]
    for side in (1, -1):
        bp_plate(B, rear(lambda p, side=side: (side * p[0], p[1], p[2])), .03, S('Fin'), rim=S('Accent'), rim_from=.82)
    bp_spots(B, body, sid, [(t, -22, .022) for t in (.36, .45, .54, .63, .72)] + [(t, -46, .017) for t in (.4, .5, .6, .68)])
    bp_puffed(make_object(B, root), B, body, .12)
    eye = body.at(.165, 40, .84)
    down, up_ = (0, -1, -.15), (0, 1, -.15)
    teeth = [(w, .026, .009, down) for w in (.12, .2, .28, .36, .64, .72, .8, .88)] + [(w, .022, .008, up_) for w in (.16, .24, .32, .68, .76, .84)]
    bp_face(root, sid, tuple(eye), (.068, .078, .06), 36, mouth_grin(body, .012, .2, -6, .009, teeth=teeth), eye_pitch=8)
    def flipper(part, m):
        out = bp_outline([(.13, -.08, -.18), (.24, -.14, -.15), (.34, -.21, -.06), (.37, -.23, .03), (.3, -.19, .05), (.14, -.1, -.06)], 28)
        bp_plate(part, [m(p) for p in out], .035, S('Fin'), rim=S('Accent'), rim_from=.82)
    bp_fins(root, (.14, -.09, -.13), flipper)
    def tail(part):
        bp_plate(part, bp_outline([(0, .02, .4), (0, -.06, .47), (0, -.18, .56), (0, -.2, .62), (0, -.09, .58), (0, .02, .5)], 26),
                 .03, S('Fin'), rim=S('Accent'), rim_from=.82)
        bp_plate(part, bp_outline([(0, .05, .43), (0, .1, .5), (0, .12, .56), (0, .06, .53), (0, .03, .48)], 18), .025, S('Fin'))
    simple_part(root, 'Tail pivot', (0, .03, .42), tail)
    def crown(part):
        top = body.at(.21, 90, 1.0)
        gold_crown(part, (0, top.y + .004, top.z), .075, 5, .07, 'DK_Gold', -6)
        for t in (.34, .42, .5, .58, .66):  # a low ridge of scutes down the back
            p = body.at(t, 90, .96)
            cone(part, tuple(p), tuple(p + Vector((0, .045, .02))), .022, S('Fin'), 10, 4, .2)
    simple_part(root, 'Crest pivot', tuple(body.at(.3, 90, 1.0)), crown)
    base = body.at(.1, 90, .96)
    lantern = Part('Lantern')
    pts = [tuple(base + Vector((0, .16 * math.sin(math.radians(a)), -.16 * (1 - math.cos(math.radians(a)))))) for a in range(0, 121, 10)]
    tube(lantern, pts, .018, S('Body'), 10)
    head = Vector(pts[-1]) + Vector((0, .0, -.07))
    ellipsoid(lantern, tuple(head), (.064, .066, .064), S('Fin'), (0, 0, 0), 24, 14)
    torus(lantern, head + Vector((0, 0, .01)), .084, .016, S('Accent'), (1, 1, 1), 36, 10)
    for k in range(8):
        a = math.tau * k / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        cone(lantern, tuple(head + d * .095), tuple(head + d * .12), .013, S('Accent'), 8, 3, .1)
    make_object(lantern, root, tuple(base), 'Lantern pivot')
    return root

# GR-12B wave 2: the Cometail's eight as animals of their own (their GR-12 builders replaced).
# ---------------------------------------------------------------- Cometail: Starmaw, a whale shark
def starmaw():
    sid = 'starmaw'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaStarmaw')
    spine = BPSpine([(0, 0, -.42), (0, .01, -.1), (0, .02, .2), (0, .04, .38)])
    body = BPBody(spine, bp_profile([(0, .22, .11, .12), (.12, .25, .15, .15), (.35, .24, .22, .21), (.7, .15, .15, .13),
                                     (.92, .07, .08, .06), (1, .05, .06, .05)], nose=.07, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_head_belly(S, .15, -.45), ring_paint=True)
    for ang in (38, 64):  # the ridges along its back
        for side in (1, -1):
            a = ang if side > 0 else 180 - ang
            tube(B, [tuple(body.at(t, a, 1.0)) for t in (.2, .32, .44, .56, .68, .8)], .009, S('Fin'), 8)
    spots = [(t, a, .013) for t in (.24, .31, .38, .45, .52, .59, .66, .73) for a in (12, 30, 50, 75)]
    bp_spots(B, body, sid, spots, 'Belly')
    # The star on its brow, glowing.
    c = body.at(.1, 90, .99) + Vector((0, .012, 0))
    ellipsoid(B, tuple(c), (.022, .022, .016), S('Accent'), (0, 0, 0), 12, 8)
    for k in range(5):
        a = math.tau * k / 5
        d = Vector((math.sin(a), 0, -math.cos(a)))
        cone(B, tuple(c + d * .015), tuple(c + d * .06), .016, S('Accent'), 8, 3, .1)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.11, 14, .9)), (.05, .058, .045), 62, mouth_arc((0, -.06, -.412), .16, .012, .011, .028))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.2, -.08, -.17), (.34, -.16, -.1), (.42, -.22, .02), (.36, -.2, .04), (.21, -.11, -.04)], 26)],
                 .03, S('Fin'))
    bp_fins(root, (.2, -.08, -.12), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .06, .36), (0, .17, .44), (0, .25, .53), (0, .15, .5), (0, .07, .44)], 22), .026, S('Fin'))
        bp_plate(part, bp_outline([(0, .02, .36), (0, .02, .44), (0, -.05, .49), (0, -.1, .48), (0, -.05, .41)], 18), .022, S('Fin'))
        comet_tail(part, (0, .04, .4), comet_wisps(S))
    simple_part(root, 'Tail pivot', (0, .04, .37), tail)
    def crest(part):
        a, b = body.at(.42, 90, .9), body.at(.56, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .1, .04))), tuple(a + Vector((0, .15, .1))), tuple(b + Vector((0, .07, .04))), tuple(b)], 20), .03, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.48, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cometail: Meteorback, a Dunkleosteus: an armoured head, molten seams
def meteorback():
    sid = 'meteorback'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaMeteorback')
    spine = BPSpine([(0, 0, -.42), (0, .01, -.15), (0, 0, .15), (0, .02, .38)])
    body = BPBody(spine, bp_profile([(0, .16, .15, .18), (.15, .22, .22, .24), (.32, .21, .21, .2), (.6, .14, .15, .12),
                                     (.88, .06, .07, .05), (1, .04, .05, .04)], nose=.12, tail=.04))
    B = Part('Body')
    head = lambda t: 1.0 + 1.4 * max(0, min(1, (.4 - t) / .15))  # a boxy armoured head
    def armour(tt, sn, cs):
        if tt < .34 and sn > -.6:
            return S('Fin')  # its armour plates, dark
        return S('Belly') if sn < -.5 and tt < .97 else None
    bp_loft(B, body, S('Body'), squarish=head, belly=armour, ring_paint=True)
    # Molten seams glowing between its plates (the Cometail's bright head): two rings round its head, one down its brow.
    for ts in (.13, .3):
        tube(B, [tuple(body.at(ts, a, 1.006)) for a in range(-30, 211, 10)], .009, S('Accent'), 8)
    tube(B, [tuple(body.at(tt, 90, 1.006)) for tt in (.02, .06, .1, .14, .18, .22, .26, .3)], .009, S('Accent'), 8)
    for side in (1, -1):
        tube(B, [tuple(body.at(tt, 35 if side > 0 else 145, 1.006)) for tt in (.13, .17, .21, .25, .3)], .008, S('Accent'), 8)
    for t2, a2, r in ((.5, 60, .02), (.58, 120, .018), (.66, 70, .016), (.55, 20, .015), (.62, 160, .015)):  # craggy lumps
        ellipsoid(B, tuple(body.at(t2, a2, .98)), (r * 1.4, r, r * 1.3), S('Fin'), (0, 0, 0), 10, 6, 1.6)
    for side in (1, -1):  # its bony blades: a pair at the front of each jaw
        for up in (1, -1):
            p = body.at(.03, -60 if side > 0 else -120, .99) + Vector((side * -.02, .018 * up, -.01))
            q = p + Vector((side * -.005, -.05 * up, -.03))
            cone(B, tuple(p), tuple(q), .02, S('Belly'), 10, 4, .1)
    bp_puffed(make_object(B, root), B, body, .12)
    blade = (0, 1, -.35)
    bp_face(root, sid, tuple(body.at(.13, 36, .86)), (.064, .074, .057), 40,
            mouth_grin(body, .03, .2, -18, .013, teeth=None),
            eye_pitch=4)
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.17, -.12, -.06), (.27, -.18, -.01), (.3, -.22, .07), (.24, -.2, .07), (.17, -.14, .0)], 20)],
                 .022, S('Fin'))
    bp_fins(root, (.17, -.12, -.04), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .04, .36), (0, .14, .43), (0, .2, .54), (0, .1, .5), (0, .04, .44)], 20), .022, S('Fin'))
        bp_plate(part, bp_outline([(0, 0, .36), (0, -.02, .43), (0, -.08, .46), (0, -.07, .4)], 14), .02, S('Fin'))
        comet_tail(part, (0, .02, .4), comet_wisps(S, 1.0, 1.0))
    simple_part(root, 'Tail pivot', (0, .02, .37), tail)
    def crest(part):
        for t, h, r in ((.44, .07, .03), (.52, .09, .034), (.6, .06, .028)):
            p = body.at(t, 90, .92)
            ellipsoid(part, tuple(p + Vector((0, h * .5, 0))), (r, h * .7, r * 1.2), S('Fin'), (-12, 0, 0), 12, 8, 1.6)
            ellipsoid(part, tuple(p + Vector((0, h * .85, -.004))), (r * .45, h * .2, r * .5), S('Accent'), (0, 0, 0), 10, 6)
    simple_part(root, 'Crest pivot', tuple(body.at(.52, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cometail: Nebulafin, a cuttlefish with a cloudy, starry skirt
def nebulafin():
    sid = 'nebulafin'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaNebulafin')
    spine = BPSpine([(0, 0, -.32), (0, .01, 0), (0, .02, .32)])
    body = BPBody(spine, bp_profile([(0, .11, .1, .1), (.12, .14, .13, .12), (.2, .12, .11, .1), (.35, .2, .13, .11), (.75, .18, .12, .1),
                                     (1, .08, .06, .05)], nose=.1, tail=.15))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_head_belly(S, .19, -.5), ring_paint=True)
    clouds = [(.38, 70, .03), (.46, 110, .034), (.55, 60, .028), (.62, 120, .03), (.7, 85, .026), (.5, 30, .022), (.58, 150, .022), (.8, 95, .02)]
    for t, a, r in clouds:
        p = body.at(t, a, .99)
        ellipsoid(B, tuple(p), (r * 1.4, r * .45, r * 1.2), S('Accent'), (0, 0, 0), 12, 6)
    bp_spots(B, body, sid, [(t, a, .007) for t, a in ((.42, 95), (.52, 80), (.6, 100), (.67, 70), (.74, 110), (.45, 50), (.56, 130))], 'Belly')
    # Eight short arms reaching forward from its head, and two longer tentacles tucked among them.
    for k in range(8):
        a = math.tau * (k + .5) / 8
        rad = Vector((math.cos(a), math.sin(a) * .8, 0))
        base = Vector((0, -.005, -.3)) + rad * .055
        pts = [base, base + rad * .02 + Vector((0, 0, -.06)), base + rad * .045 + Vector((0, -.012, -.12)), base + rad * .05 + Vector((0, -.03, -.16))]
        taper_tube(B, [tuple(p) for p in pts], .022, .009, S('Body'), 10)
    for side in (1, -1):
        pts = [(side * .03, -.04, -.3), (side * .05, -.06, -.4), (side * .06, -.07, -.48), (side * .05, -.06, -.52)]
        taper_tube(B, pts, .012, .01, S('Fin'), 8)
        ellipsoid(B, (side * .05, -.06, -.535), (.016, .012, .028), S('Fin'), (0, 0, 0), 10, 6)
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.13, 32, .86)), (.07, .078, .06), 55, mouth_arc((0, -.055, -.312), .028, .008, .007, .01))
    def skirt(part, m):
        # A rippling fin skirt along its side, scalloped, its edge in the accent.
        lo, hi, sd = [], [], []
        for k in range(26):
            t = .26 + .7 * k / 25
            base = body.at(t, 0, .9)
            w = .07 * math.sin(math.pi * (k / 25)) ** .6 * (1 + .14 * math.sin(k * 1.7)) + .005
            lo.append(m(tuple(base))); hi.append(m(tuple(base + Vector((w + .005, -.004, 0))))); sd.append((0, 1, 0))
        bp_strip(part, lo, hi, .018, S('Fin'), rim=S('Accent'), side_dirs=sd)
    bp_fins(root, (.17, .01, .05), skirt)
    def tail(part):
        comet_tail(part, (0, .02, .33), comet_wisps(S, .85, .9))
    simple_part(root, 'Tail pivot', (0, .02, .31), tail)
    def crest(part):
        for dz, dy, r in ((0, 0, .04), (-.04, -.006, .03), (.04, -.008, .032)):
            c = body.at(.5, 90, .98) + Vector((0, .022 + dy, dz))
            ellipsoid(part, tuple(c), (r, r * .8, r), S('Fin'), (0, 0, 0), 14, 8)
    simple_part(root, 'Crest pivot', tuple(body.at(.5, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cometail: Orbitail, an ammonite, a ring orbiting its shell
def orbitail():
    sid = 'orbitail'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaOrbitail')
    # Its soft head pokes forward out of its shell's opening.
    spine = BPSpine([(0, -.06, -.38), (0, -.05, -.24), (0, -.04, -.06)])
    body = BPBody(spine, bp_profile([(0, .1, .1, .1), (.4, .13, .13, .12), (1, .13, .13, .12)], nose=.3, tail=.01))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_head_belly(S, .55, -.5), ring_paint=True)
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, tuple(body.at(.4, 38, .86)), (.06, .068, .052), 48, mouth_arc((0, -.08, -.385), .026, .008, .007, .01))
    def arms(part, m):
        for k in range(5):  # five short tentacles a side curling forward and down, swaying as it swims
            a = math.radians(-80 + 40 * k)
            rad = Vector((abs(math.cos(a)) + .05, math.sin(a) * .85, 0))
            base = Vector((0, -.07, -.36)) + rad * .055
            pts = [base, base + rad * .03 + Vector((0, -.02, -.06)), base + rad * .05 + Vector((0, -.06, -.1)), base + rad * .05 + Vector((0, -.1, -.11))]
            taper_tube(part, [m(tuple(p)) for p in pts], .016, .008, S('Body'), 8)
        pts = [(.06, -.09, -.36), (.1, -.13, -.44), (.12, -.17, -.5), (.11, -.2, -.54)]
        taper_tube(part, [m(p) for p in pts], .014, .01, S('Fin'), 8)
        ellipsoid(part, m((.11, -.205, -.555)), (.016, .013, .02), S('Accent'), (0, 0, 0), 10, 6)
    bp_fins(root, (.06, -.09, -.36), arms)
    C = Vector((0, .1, .12))
    def shell(part):
        # A coiled shell: a tube along a spiral that winds in to its middle, its ribs raised, a glowing ring orbiting it.
        phi0, turns, R1 = -.6, 2.3, .26
        b = math.log(1 / .14) / (turns * math.tau)
        cs, ups, rx, ry = [], [], [], []
        n = 90
        for i in range(n + 1):
            phi = phi0 + turns * math.tau * i / n
            R = R1 * math.exp(-b * (phi - phi0))
            d = Vector((0, math.sin(phi), -math.cos(phi)))
            cs.append(tuple(C + d * R)); ups.append(tuple(d)); rx.append(R * .3); ry.append(R * .38)
        bp_tube(part, cs, ups, rx, ry, S('Body'), 20)
        for i in range(0, n - 6, 4):
            c, d = Vector(cs[i]), Vector(ups[i])
            nxt = Vector(cs[i + 1]) - c
            tan = nxt.normalized()
            alpha = math.degrees(math.atan2(-tan.y, tan.z))
            torus(part, c, 1.0, .01, S('Fin'), (rx[i] * 1.03, ry[i] * 1.03, 1), 24, 6, (alpha, 0, 0))
    simple_part(root, 'Crest pivot', tuple(C + Vector((0, .2, 0))), shell)
    def tail(part):
        comet_tail(part, (0, .14, .36), comet_wisps(S, .9, .9))
        ring = Vector((0, .15, .5))
        torus(part, ring, .12, .009, S('Accent'), (1, 1, 1), 40, 8, (-20, 0, 0))
        ellipsoid(part, tuple(ring + rot((.12, 0, 0), (-20, 0, 0))), (.028, .028, .028), S('Belly'), (0, 0, 0), 14, 8)
    simple_part(root, 'Tail pivot', (0, .14, .35), tail)
    return root

# ---------------------------------------------------------------- Cometail: Quasarray, a sawfish with a blazing core
def quasarray():
    sid = 'quasarray'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaQuasarray')
    spine = BPSpine([(0, 0, -.30), (0, .01, 0), (0, .02, .28), (0, .03, .42)])
    body = BPBody(spine, bp_profile([(0, .1, .05, .05), (.15, .18, .09, .07), (.35, .17, .13, .11), (.7, .1, .1, .08), (.92, .05, .06, .05),
                                     (1, .03, .04, .03)], nose=.1, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_head_belly(S, .2, -.4), ring_paint=True)
    # Its saw: a long flat blade from the snout, white teeth along both edges.
    ellipsoid(B, (0, .005, -.47), (.046, .013, .19), S('Body'), (0, 0, 0), 24, 10)
    for k in range(11):
        z = -.33 - .27 * k / 10
        w = .046 * math.sqrt(max(0, 1 - ((z + .47) / .19) ** 2)) + .002
        for side in (1, -1):
            cone(B, (side * (w - .004), .005, z), (side * (w + .022), .005, z + .004), .007, 'DK_EyeWhite', 8, 3, .1)
    for side in (1, -1):  # pelvic fins and the second dorsal
        bp_plate(B, bp_outline([(side * .07, -.05, .14), (side * .16, -.07, .2), (side * .14, -.07, .25), (side * .07, -.05, .22)], 14), .018, S('Fin'))
    a2, b2 = body.at(.68, 90, .9), body.at(.78, 90, .9)
    bp_plate(B, bp_outline([tuple(a2), tuple(a2 + Vector((0, .07, .03))), tuple(b2 + Vector((0, .05, .03))), tuple(b2)], 14), .02, S('Fin'))
    bp_puffed(make_object(B, root), B, body, .1)
    bp_face(root, sid, (.06, .1, -.16), (.05, .056, .045), 22, mouth_grin(body, .09, .15, -48, .009), eye_pitch=32)
    def wing(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.12, 0, -.2), (.3, -.02, -.1), (.4, -.03, .02), (.33, -.02, .05), (.13, 0, .02)], 26)],
                 .03, S('Fin'), rim=S('Accent'), rim_from=.84)
    bp_fins(root, (.13, 0, -.08), wing)
    def tail(part):
        bp_plate(part, bp_outline([(0, .03, .4), (0, .12, .47), (0, .17, .56), (0, .09, .52), (0, .03, .46)], 18), .02, S('Fin'))
        comet_tail(part, (0, .03, .43), comet_wisps(S, .9, 1.0))
    simple_part(root, 'Tail pivot', (0, .03, .4), tail)
    def crest(part):
        top = body.at(.4, 90, .95)
        ellipsoid(part, tuple(top + Vector((0, .03, 0))), (.035, .035, .035), S('Accent'), (0, 0, 0), 16, 10)
        for side in (1, -1):
            cone(part, tuple(top + Vector((side * .02, .04, .01))), tuple(top + Vector((side * .06, .13, .09))), .02, S('Accent'), 10, 4, .3)
        a, b = body.at(.44, 90, .9), body.at(.56, 90, .9)
        bp_plate(part, bp_outline([tuple(a), tuple(a + Vector((0, .09, .04))), tuple(b + Vector((0, .06, .04))), tuple(b)], 16), .022, S('Fin'))
    simple_part(root, 'Crest pivot', tuple(body.at(.45, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cometail: Novatusk, a narwhal with a glowing spiral tusk
def novatusk():
    sid = 'novatusk'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaNovatusk')
    spine = BPSpine([(0, 0, -.40), (0, .02, -.1), (0, .03, .2), (0, .05, .36)])
    body = BPBody(spine, bp_profile([(0, .14, .15, .14), (.2, .21, .22, .21), (.5, .21, .21, .2), (.8, .11, .11, .1), (1, .05, .05, .05)],
                                    nose=.2, tail=.05))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_head_belly(S, .17, -.45), ring_paint=True)
    bp_spots(B, body, sid, [(t, a, r) for t, a, r in ((.3, 70, .022), (.38, 110, .018), (.45, 80, .02), (.52, 120, .016), (.6, 60, .018),
                                                         (.66, 100, .015), (.42, 40, .014), (.56, 140, .014), (.72, 85, .013))], 'Fin')
    # Its tusk: a long glowing spiral from the left of its lip.
    base = body.at(.04, 115, .92)
    tip = base + Vector((-.03, .07, -.56))
    cone(B, tuple(base), tuple(tip), .026, S('Accent'), 14, 10, .05)
    axis = (tip - base)
    pts = []
    for k in range(40):
        s = k / 39
        a = s * math.tau * 6
        q = axis.normalized().orthogonal().normalized()
        r2 = axis.normalized().cross(q)
        rad = .026 * (1 - s) * 1.05 + .002
        pts.append(tuple(base + axis * s + (q * math.cos(a) + r2 * math.sin(a)) * rad))
    tube(B, pts, .005, S('Fin'), 6)
    bp_puffed(make_object(B, root), B, body, .12)
    bp_face(root, sid, tuple(body.at(.14, 6, .88)), (.05, .058, .046), 56, mouth_grin(body, .03, .1, -24, .008))
    def flipper(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.18, -.1, -.18), (.26, -.15, -.15), (.32, -.2, -.08), (.27, -.18, -.06), (.18, -.12, -.11)], 18)],
                 .026, S('Fin'))
    bp_fins(root, (.18, -.1, -.15), flipper)
    def tail(part):
        for side in (1, -1):  # a fluke, flat and wide, lying across the water
            bp_plate(part, bp_outline([(0, .05, .34), (side * .1, .05, .38), (side * .2, .05, .45), (side * .16, .05, .48), (side * .04, .05, .44)], 18),
                     .024, S('Fin'), normal=(0, 1, 0))
        comet_tail(part, (0, .05, .42), comet_wisps(S, .85, .9))
    simple_part(root, 'Tail pivot', (0, .05, .33), tail)
    def crest(part):
        for t in (.5, .56, .62, .68):  # the low bumps along its back
            p = body.at(t, 90, .96)
            ellipsoid(part, tuple(p + Vector((0, .012, 0))), (.018, .014, .024), S('Body'), (0, 0, 0), 10, 6)
    simple_part(root, 'Crest pivot', tuple(body.at(.58, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cometail: Cosmocarp, a koi with galaxy spots
def cosmocarp():
    sid = 'cosmocarp'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaCosmocarp')
    spine = BPSpine([(0, 0, -.38), (0, .01, -.1), (0, .01, .2), (0, .02, .34)])
    body = BPBody(spine, bp_profile([(0, .12, .12, .12), (.22, .17, .22, .2), (.5, .16, .21, .19), (.8, .08, .1, .08), (1, .04, .05, .04)],
                                    nose=.2, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), belly=bp_head_belly(S, .18, -.5), ring_paint=True)
    galaxy = [(.3, 60, .04), (.42, 20, .03), (.48, 100, .045), (.58, 40, .032), (.66, 120, .028), (.72, 70, .025), (.36, 150, .03), (.55, -10, .022)]
    for t, a, r in galaxy:
        p = body.at(t, a, .99)
        ellipsoid(B, tuple(p), (r * 1.2, r * .4, r * 1.3), S('Accent'), (0, 0, 0), 12, 6)
        for k in range(3):
            q = body.at(t + .015 * (k - 1), a + 9 * (k - 1), 1.01)
            ellipsoid(B, tuple(q), (.004, .004, .004), S('Belly'), (0, 0, 0), 6, 4)
    for side in (1, -1):  # barbels at the corners of its mouth
        b0 = body.at(.03, -40 if side > 0 else -140, .97)
        taper_tube(B, [tuple(b0), tuple(b0 + Vector((side * .04, -.03, .02))), tuple(b0 + Vector((side * .07, -.07, .06)))], .006, .004, S('Fin'), 6)
    bp_puffed(make_object(B, root), B, body, .14)
    bp_face(root, sid, tuple(body.at(.13, 24, .86)), (.062, .072, .056), 46, mouth_o((0, -.03, -.39), .028, S('Fin'), (-10, 0, 0)))
    def pec(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.12, -.08, -.14), (.22, -.14, -.08), (.28, -.2, .02), (.2, -.18, .02), (.13, -.11, -.06)], 20)],
                 .02, S('Fin'))
    bp_fins(root, (.12, -.09, -.12), pec)
    def tail(part):
        bp_plate(part, bp_outline([(0, .02, .32), (0, .12, .4), (0, .16, .48), (0, .06, .46), (0, .02, .42)], 18), .02, S('Fin'))
        bp_plate(part, bp_outline([(0, .0, .32), (0, -.0, .42), (0, -.1, .46), (0, -.14, .44), (0, -.08, .38)], 18), .02, S('Fin'))
        comet_tail(part, (0, .01, .38), comet_wisps(S, .85, .95))
    simple_part(root, 'Tail pivot', (0, .01, .33), tail)
    def crest(part):
        lo, hi, sd = [], [], []
        for k in range(16):
            t = .3 + .38 * k / 15
            c, s, up = spine.frame(t)
            base = body.at(t, 90, .92)
            h = .09 * math.sin(math.pi * k / 15) ** .5 + .006
            lo.append(tuple(base)); hi.append(tuple(base + up * h + Vector((0, 0, .02)))); sd.append(tuple(s))
        bp_strip(part, lo, hi, .016, S('Fin'), side_dirs=sd)
    simple_part(root, 'Crest pivot', tuple(body.at(.48, 90, 1.0)), crest)
    return root

# ---------------------------------------------------------------- Cometail crown: Celestial Wyrm, a true sea serpent
def celestialwyrm():
    sid = 'celestialwyrm'; S = lambda s: slot_name(sid, s)
    root = creature_root('DioramaCelestialwyrm')
    spine = BPSpine([(0, .04, -.52), (.015, .035, -.32), (-.025, .02, -.06), (.025, .01, .2), (-.025, .01, .45), (.015, 0, .66)], 141)
    body = BPBody(spine, bp_profile([(0, .06, .05, .05), (.05, .095, .085, .08), (.1, .1, .095, .085), (.16, .075, .075, .07),
                                     (.32, .095, .095, .09), (.7, .08, .08, .075), (.94, .04, .04, .035), (1, .02, .02, .02)], nose=.05, tail=.04))
    B = Part('Body')
    bp_loft(B, body, S('Body'), 0, 1, 120, 30, belly=bp_head_belly(S, .12, -.5), ring_paint=True)
    lo, hi, sd = [], [], []  # its frill along its whole back
    for k in range(60):
        t = .14 + .82 * k / 59
        c, s, up = spine.frame(t)
        base = body.at(t, 90, .9)
        h = .05 * (1 - .5 * t) * math.sin(math.pi / 2 * min(1, (t - .14) / .06)) ** .5 + .004
        lo.append(tuple(base)); hi.append(tuple(base + up * h)); sd.append(tuple(s))
    bp_strip(B, lo, hi, .016, S('Fin'), rim=S('Accent'), side_dirs=sd)
    for side in (1, -1):  # whiskers from its snout
        b0 = body.at(.03, 20 if side > 0 else 160, .97)
        taper_tube(B, [tuple(b0), tuple(b0 + Vector((side * .06, -.01, .02))), tuple(b0 + Vector((side * .13, -.04, .08))), tuple(b0 + Vector((side * .17, -.09, .14)))],
                   .007, .004, S('Accent'), 6)
    bp_spots(B, body, sid, [(t, -20, .008) for t in (.2, .3, .4, .5, .6, .7, .8)], 'Belly')
    body_obj = make_object(B, root)
    bp_puffed(body_obj, B, body, .08)
    bp_wave_keys(body_obj, B, body, .05, 1.1, .2)
    fang = (0, -1, -.2)
    bp_face(root, sid, tuple(body.at(.075, 36, .84)), (.05, .058, .045), 40,
            mouth_grin(body, .015, .08, -8, .008, teeth=[(.3, .022, .007, fang), (.7, .022, .007, fang)]), eye_pitch=6)
    def wingfin(part, m):
        bp_plate(part, [m(p) for p in bp_outline([(.07, -.0, -.3), (.18, .03, -.26), (.26, .06, -.16), (.2, .02, -.14), (.14, -.0, -.2), (.08, -.02, -.24)], 22)],
                 .02, S('Fin'), rim=S('Accent'), rim_from=.8)
    bp_fins(root, (.07, .0, -.27), wingfin)
    end = spine.frame(1.0)[0]
    def tail(part):
        comet_tail(part, tuple(end + Vector((0, 0, .01))), comet_wisps(S, 1.0, 1.2))
    simple_part(root, 'Tail pivot', tuple(spine.frame(.98)[0]), tail)
    def crown(part):
        top = body.at(.1, 90, 1.0)
        gold_crown(part, (0, top.y + .004, top.z + .01), .06, 5, .06, 'DK_Gold', -10)
        for side in (1, -1):  # horns sweeping back, and its mane
            h0 = body.at(.12, 60 if side > 0 else 120, .95)
            cone(part, tuple(h0), tuple(h0 + Vector((side * .04, .1, .1))), .016, S('Accent'), 10, 5, .2)
        for k in range(7):
            t = .13 + .025 * k
            p = body.at(t, 90, .95)
            cone(part, tuple(p), tuple(p + Vector((0, .07 - .006 * k, .05))), .02, S('Fin'), 8, 4, .3)
    simple_part(root, 'Crest pivot', tuple(body.at(.12, 90, 1.0)), crown)
    return root

WAVE4_BUILDERS = {'moonmouth': moonmouth, 'gloomfin': gloomfin, 'shadeback': shadeback, 'hushray': hushray,
                  'duskdrifter': duskdrifter, 'murkmaw': murkmaw, 'nightlantern': nightlantern, 'eclipseleviathan': eclipseleviathan,
                  'starmaw': starmaw, 'meteorback': meteorback, 'nebulafin': nebulafin, 'orbitail': orbitail,
                  'quasarray': quasarray, 'novatusk': novatusk, 'cosmocarp': cosmocarp, 'celestialwyrm': celestialwyrm}

CREATURE_BUILDERS = {'pondlet': pondlet, 'bubblebelly': bubblebelly, 'ribbonfin': ribbonfin, 'lanternfin': lanternfin,
                     'driftwing': driftwing, **WAVE1_BUILDERS, **WAVE2_BUILDERS, **WAVE3_BUILDERS, **WAVE4_BUILDERS}
assert [sid for sid, _, _, _ in SPECIES] == list(CREATURE_BUILDERS), 'every species has its builder, in the atlas order'

# ================================================================ AD-C: the humanoid rig (front +Z)
# One rig for every character, customers and crew alike: Left/Right hip (a leg and its shoe), Body (the torso and its outfit),
# Left/Right shoulder (a sleeve, a mitten hand with a thumb, and anything held), Head pivot, and the head's parts: Head (with its
# hair, hat and face props), Eyes, Pupils, Glints, Lids (Blink), Blush, Mouth (Happy, Haggle, Shocked, Ouch) and the Left/Right
# brow pivots. The game reparents the head's parts onto Head pivot (ShopCharacterVisual.DioramaHeadParts) and drives every role
# through the same joints, face and poses. Roles differ by proportions, outfit, props and colours (AD.4: Casual round, Bargain
# tall and thin, Collector pear, Big spender a wide coat); the Casual is AD-A's visitor, built from the same numbers. Crew always
# wear shop teal and carry a Carry crate on the parcel socket; customers never wear teal.
def catmull(points, t):
    """Piecewise Catmull-Rom through (y, r) points, sampled at normalized t."""
    n = len(points) - 1
    f = min(n - 1e-6, max(0, t * n)); i = int(f); s = f - i
    p0 = points[max(i - 1, 0)][1]; p1 = points[i][1]; p2 = points[i + 1][1]; p3 = points[min(i + 2, n)][1]
    return .5 * ((2 * p1) + (-p0 + p2) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s + (-p0 + 3 * p1 - 3 * p2 + p3) * s ** 3)

def char_legs(root, s, slots):
    """Short trousered legs; each leg mesh is the hip joint the walk code swings."""
    for side, label in ((-1, 'Left'), (1, 'Right')):
        hip = (side * s['hip_x'], s['hip_y'], 0)
        leg = Part(label + ' leg')
        dy, r = s['thigh']
        ellipsoid(leg, (hip[0], hip[1] + dy, 0), r, slots['trousers'], (0, 0, 0), 20, 12)
        dy, r, dz = s['shoe']
        ellipsoid(leg, (hip[0], hip[1] + dy, dz), r, slots['shoes'], (0, 0, 0), 20, 10, 1.6)
        make_object(leg, root, hip, label + ' hip')

def char_body(root, s, slots, outfit):
    """The torso, lathed from a (y, radius) profile and squashed front to back by depth; outfit(body, first_face, front)
    adds collars, buttons and the rest, front(y) being the surface's distance in front of the axis at height y."""
    body = Part('Body')
    radius, (y0, y1), depth = s['profile'], s['body_y'], s.get('depth', .8)
    seg, rings = 36, 28
    verts = [(0, y0, 0)]
    for r in range(1, rings):
        t = r / rings
        y = y0 + (y1 - y0) * t
        rr = catmull(radius, (y - radius[0][0]) / (radius[-1][0] - radius[0][0]))
        for k in range(seg):
            a = math.tau * k / seg
            verts.append((rr * math.cos(a), y, rr * depth * math.sin(a)))
    verts.append((0, y1, 0))
    faces = []
    for k in range(seg):
        faces.append((0, 1 + (k + 1) % seg, 1 + k))
    for r in range(rings - 2):
        a, b = 1 + r * seg, 1 + (r + 1) * seg
        for k in range(seg):
            t = (k + 1) % seg
            faces.append((a + k, a + t, b + t, b + k))
    last, end = 1 + (rings - 2) * seg, len(verts) - 1
    for k in range(seg):
        faces.append((last + k, last + (k + 1) % seg, end))
    body.add(verts, faces, slots['body'])
    def front(y):
        return catmull(radius, (y - radius[0][0]) / (radius[-1][0] - radius[0][0])) * depth
    outfit(body, 0, front)
    make_object(body, root)

def paint_where(part, first_face, last_face, slot, test):
    """Give faces in [first_face, last_face) the slot where test(face centre) holds (Unity space)."""
    index = part.slot(slot)
    for fi in range(first_face, last_face):
        vs = [part.verts[i] for i in part.faces[fi]]
        c = Vector((sum(v.x for v in vs), sum(v.y for v in vs), sum(v.z for v in vs))) / len(vs)
        if test(c):
            part.slots[fi] = index

def char_arms(root, s, slots, held=None):
    """Arms with mitten hands (thumb forward); held(arm, side, hand) adds what that hand carries."""
    k = s.get('arm', 1.0)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        sh = (side * s['shoulder'][0], s['shoulder'][1], 0)
        arm = Part(label + ' arm')
        ellipsoid(arm, (sh[0] + side * .025, sh[1] - .14 * k, 0), (s.get('sleeve_x', .082), .175 * k, s.get('sleeve_z', .086)),
                  slots['sleeve'], (0, 0, side * 9), 20, 12)
        torus(arm, (sh[0] + side * .046, sh[1] - .285 * k, 0), .06, .022, slots['cuff'], (1, 1, 1), 24, 8, (90, 0, 0))
        hand = (sh[0] + side * .05, sh[1] - .36 * k, .012)
        ellipsoid(arm, hand, (.074, .084, .062), slots['hand'], (0, 0, 0), 18, 10)
        ellipsoid(arm, (hand[0] - side * .045, hand[1] + .012, hand[2] + .042), (.03, .046, .03), slots['hand'], (-30, 0, side * 20), 12, 8)
        if held:
            held(arm, side, hand, sh)
        make_object(arm, root, sh, label + ' shoulder')

def char_head(root, s, slots, headwear):
    """Head on its pivot: big face, eyes with lids, brows, button nose, open mouth, blush, and the role's hair, hat and face
    props (headwear). Every face part has its origin at the head pivot (the brows at their own pivots)."""
    hp = (0, s['head_y'], 0)
    empty('Head pivot', hp, root)
    hc = Vector((0, hp[1] + .33, .01))
    head = Part('Head')
    ellipsoid(head, hc, (.34, .32, .31), slots['skin'], (0, 0, 0), 36, 22)
    for side in (-1, 1):
        ellipsoid(head, hc + Vector((side * .335, -.02, -.01)), (.05, .075, .06), slots['skin'], (0, 0, 0), 14, 8)
    ellipsoid(head, hc + Vector((0, -.06, .305)), (.046, .04, .04), slots['skin'], (0, 0, 0), 14, 8)
    headwear(head, hc)
    make_object(head, root, hp)
    eyes, pupils, glints, lids, blush = Part('Eyes'), Part('Pupils'), Part('Glints'), Part('Lids'), Part('Blush')
    lid_builders = []
    for side in (-1, 1):
        c = hc + Vector((side * .118, .035, .262))
        ellipsoid(eyes, c, (.082, .098, .05), 'DK_EyeWhite', (0, 0, 0), 22, 12)
        ellipsoid(pupils, c + Vector((side * .004, -.008, .034)), (.044, .058, .026), 'DK_Pupil', (0, 0, 0), 18, 10)
        ellipsoid(glints, c + Vector((side * .004 - .016, .016, .056)), (.014, .016, .008), 'DK_Glint', (0, 0, 0), 10, 6)
        idx, build = shell(lids, c, (.088, .104, .056), slots['skin'], (0, 0, 0), math.radians(18), .008, 22, 8)
        lid_builders.append((idx, build))
        ellipsoid(blush, hc + Vector((side * .2, -.08, .225)), (.05, .028, .016), 'DK_Blush', (0, side * 34, 0), 14, 8)
    for p in (eyes, pupils, glints, blush):
        make_object(p, root, hp)
    lids_obj = make_object(lids, root, hp)
    closed = {}
    for idx, build in lid_builders:
        for i, v in zip(idx, build(math.radians(104))):
            closed[i] = v
    shape_key(lids_obj, lids, 'Blink', lambda i, v: closed[i], pivot=hp)
    for side, label in ((-1, 'Left'), (1, 'Right')):
        brow = Part(label + ' brow')
        bc = hc + Vector((side * .118, .16, .262))
        ellipsoid(brow, bc, (.06, .017, .02), slots['brow'], (0, 0, -side * 8), 14, 8)
        make_object(brow, root, tuple(bc), label + ' brow pivot')
    mouth = Part('Mouth')
    mc = hc + Vector((0, -.16, .272))
    ellipsoid(mouth, mc, (.072, .034, .02), 'DK_Mouth', (-12, 0, 0), 22, 12)
    mouth_obj = make_object(mouth, root, hp)
    def m(fn):
        return lambda i, v: tuple(mc + fn(Vector(v) - mc))
    shape_key(mouth_obj, mouth, 'Happy', m(lambda d: Vector((d.x * 1.25, (d.y * 1.35 - .012 if d.y < 0 else d.y * .6) + .016 * (d.x / .072) ** 2, d.z))), pivot=hp)
    shape_key(mouth_obj, mouth, 'Haggle', m(lambda d: Vector((d.x * .9 + .018, d.y * .28 + .012 * d.x / .072, d.z))), pivot=hp)
    shape_key(mouth_obj, mouth, 'Shocked', m(lambda d: Vector((d.x * .62, d.y * 1.75 - .01, d.z))), pivot=hp)
    shape_key(mouth_obj, mouth, 'Ouch', m(lambda d: Vector((d.x * 1.1, d.y * .42 + .009 * math.sin(d.x * 70), d.z))), pivot=hp)

def carry_crate(root, slots, centre):
    """Crew: the crate the game hangs on its parcel socket while carrying (the classic rig's preview parcel)."""
    crate = Part('Carry crate')
    c = Vector(centre)
    ellipsoid(crate, c, (.17, .11, .125), slots['crate'], (0, 0, 0), 20, 12, 4)
    for dy in (-.05, .05):
        ellipsoid(crate, c + Vector((0, dy, .126)), (.165, .018, .008), slots['slat'], (0, 0, 0), 12, 6, 4)
        ellipsoid(crate, c + Vector((0, dy, -.126)), (.165, .018, .008), slots['slat'], (0, 0, 0), 12, 6, 4)
    make_object(crate, root, centre, 'Carry crate')

def humanoid(asset, s, slots, outfit, headwear, held=None, extra=None):
    root = empty(asset)
    root['front'] = 'character local +Z'
    char_legs(root, s, slots)
    char_body(root, s, slots, outfit)
    char_arms(root, s, slots, held)
    char_head(root, s, slots, headwear)
    if extra:
        extra(root)
    return root

# ---------------------------------------------------------------- the four customers
CASUAL = {'hip_x': .11, 'hip_y': .40, 'thigh': (-.15, (.095, .175, .10)), 'shoe': (-.33, (.105, .072, .155), .045),
          'profile': [(.30, .02), (.33, .2), (.40, .285), (.50, .32), (.62, .325), (.74, .30), (.85, .25), (.93, .17), (.97, .07)],
          'body_y': (.30, .98), 'depth': .8, 'shoulder': (.27, .86), 'head_y': .95}
CASUAL_SLOTS = {'trousers': 'DK_Trousers', 'shoes': 'DK_Shoes', 'body': 'DK_CasualKnit', 'sleeve': 'DK_CasualKnit',
                'cuff': 'DK_Collar', 'hand': 'DK_Skin', 'skin': 'DK_Skin', 'brow': 'DK_Hair'}

def casual():
    """Round knit body: an egg with a cream collar, coral buttons and a patch pocket; a tote in the right hand; a beret."""
    def outfit(body, first, front):
        ellipsoid(body, (0, .925, 0), (.205, .07, .175), 'DK_Collar', (0, 0, 0), 28, 10)
        for i in range(3):
            by = .80 - i * .12
            ellipsoid(body, (0, by, front(by) + .004), (.026, .026, .014), 'DK_Button', (0, 0, 0), 12, 8)
        ellipsoid(body, (-.165, .52, .205), (.06, .05, .012), 'DK_Collar', (0, -34, 0), 16, 8, 2.4)
    def held(arm, side, hand, sh):
        if side > 0:
            bag = (hand[0] + .012, hand[1] - .23, hand[2] + .01)
            ellipsoid(arm, bag, (.065, .15, .15), 'DK_Tote', (0, 0, 0), 20, 12, 3.2)
            for dz in (-.05, .05):
                tube(arm, [(bag[0], bag[1] + .12, bag[2] + dz), (hand[0] + .01, hand[1] - .03, hand[2] + dz * .6),
                           (hand[0] + .004, hand[1] + .02, hand[2] + dz * .3)], .011, 'DK_Tote', 8)
    def headwear(head, hc):
        ellipsoid(head, hc + Vector((0, .14, -.05)), (.345, .25, .32), 'DK_Hair', (0, 0, 0), 32, 16)
        beret = hc + Vector((-.03, .29, -.02))
        ellipsoid(head, beret, (.335, .078, .305), 'DK_Beret', (0, 0, 8), 36, 14)
        ellipsoid(head, beret + Vector((-.012, .085, 0)), (.034, .034, .034), 'DK_Nub', (0, 0, 0), 12, 8)
    return humanoid('DioramaCasualVisitor', CASUAL, CASUAL_SLOTS, outfit, headwear, held)

def bargain():
    """Tall and thin: a green sweater with a mustard scarf and elbow patches, a flat cap, a coin purse in the right hand."""
    s = {'hip_x': .095, 'hip_y': .52, 'thigh': (-.20, (.078, .235, .085)), 'shoe': (-.455, (.095, .065, .16), .05),
         'profile': [(.42, .02), (.45, .16), (.55, .205), (.68, .215), (.82, .21), (.95, .195), (1.05, .165), (1.12, .11), (1.16, .045)],
         'body_y': (.42, 1.16), 'depth': .82, 'shoulder': (.215, 1.04), 'arm': 1.15, 'sleeve_x': .07, 'sleeve_z': .074, 'head_y': 1.13}
    slots = {'trousers': 'DK_BargainTrousers', 'shoes': 'DK_BargainShoes', 'body': 'DK_BargainKnit', 'sleeve': 'DK_BargainKnit',
             'cuff': 'DK_BargainScarf', 'hand': 'DK_BargainSkin', 'skin': 'DK_BargainSkin', 'brow': 'DK_BargainHair'}
    def outfit(body, first, front):
        torus(body, (0, .47, 0), .2, .028, 'DK_BargainKnit', (1, .82, 1), 36, 8, (90, 0, 0))
        torus(body, (0, 1.115, 0), .125, .045, 'DK_BargainScarf', (1, .9, 1), 32, 10, (90, 0, 0))
        ellipsoid(body, (.055, .98, front(.98) + .025), (.045, .12, .02), 'DK_BargainScarf', (0, 0, -8), 14, 8, 1.6)
        for i in range(2):
            by = .86 - i * .14
            ellipsoid(body, (0, by, front(by) + .004), (.022, .022, .012), 'DK_BargainTrousers', (0, 0, 0), 12, 8)
    def held(arm, side, hand, sh):
        # Elbow patches, and a coin purse with a gold clasp in the right hand.
        ellipsoid(arm, (sh[0] + side * .03, sh[1] - .2, -.06), (.045, .055, .02), 'DK_BargainTrousers', (0, 0, 0), 14, 8)
        if side > 0:
            purse = (hand[0] + .01, hand[1] - .1, hand[2] + .03)
            ellipsoid(arm, purse, (.055, .045, .03), 'DK_BargainPurse', (0, 0, 0), 16, 10)
            torus(arm, (purse[0], purse[1] + .045, purse[2]), .03, .008, 'DK_Gold', (1, 1, 1), 16, 6, (90, 0, 0))
    def headwear(head, hc):
        ellipsoid(head, hc + Vector((0, .12, -.07)), (.345, .24, .31), 'DK_BargainHair', (0, 0, 0), 32, 16)
        ellipsoid(head, hc + Vector((0, .265, -.02)), (.335, .095, .315), 'DK_BargainCap', (6, 0, 0), 36, 14)
        ellipsoid(head, hc + Vector((0, .23, .26)), (.24, .024, .13), 'DK_BargainCap', (-8, 0, 0), 24, 8, 2)
        ellipsoid(head, hc + Vector((0, .355, -.02)), (.03, .03, .03), 'DK_BargainPurse', (0, 0, 0), 12, 8)
    return humanoid('DioramaBargainVisitor', s, slots, outfit, headwear, held)

def collector():
    """Pear-shaped: a plum coat with a bow tie, grey hair and moustache, a bowler, round gold spectacles and a magnifier."""
    s = {'hip_x': .12, 'hip_y': .38, 'thigh': (-.14, (.10, .165, .105)), 'shoe': (-.31, (.105, .07, .15), .045),
         'profile': [(.28, .02), (.31, .26), (.38, .35), (.47, .37), (.58, .345), (.70, .29), (.80, .23), (.89, .16), (.95, .06)],
         'body_y': (.28, .95), 'depth': .82, 'shoulder': (.21, .83), 'arm': .95, 'head_y': .92}
    slots = {'trousers': 'DK_CollectorTrousers', 'shoes': 'DK_CollectorShoes', 'body': 'DK_CollectorCoat', 'sleeve': 'DK_CollectorCoat',
             'cuff': 'DK_Collar', 'hand': 'DK_CollectorSkin', 'skin': 'DK_CollectorSkin', 'brow': 'DK_CollectorHair'}
    def outfit(body, first, front):
        ellipsoid(body, (0, .895, 0), (.17, .065, .145), 'DK_Collar', (0, 0, 0), 28, 10)
        bow = (0, .86, front(.86) + .02)
        for side in (-1, 1):
            ellipsoid(body, (bow[0] + side * .045, bow[1], bow[2]), (.045, .03, .018), 'DK_CollectorBow', (0, 0, side * 12), 14, 8)
        ellipsoid(body, bow, (.018, .018, .02), 'DK_CollectorBow', (0, 0, 0), 10, 6)
        for i in range(3):
            by = .72 - i * .13
            ellipsoid(body, (0, by, front(by) + .004), (.024, .024, .013), 'DK_Gold', (0, 0, 0), 12, 8)
        ellipsoid(body, (-.15, .70, front(.70) - .03), (.05, .035, .012), 'DK_CollectorBow', (0, -30, 0), 14, 8, 2.4)
    def held(arm, side, hand, sh):
        if side > 0:
            handle = (hand[0] + .005, hand[1] - .02, hand[2] + .06)
            tube(arm, [handle, (handle[0], handle[1] + .05, handle[2] + .12)], .018, 'DK_CollectorHandle', 10)
            lens = (handle[0], handle[1] + .12, handle[2] + .2)
            torus(arm, lens, .08, .014, 'DK_Gold', (1, 1, 1), 28, 8, (-40, 0, 0))
            ellipsoid(arm, lens, (.074, .074, .007), 'DK_Lens', (-40, 0, 0), 20, 6)
    def headwear(head, hc):
        ellipsoid(head, hc + Vector((0, .04, -.10)), (.35, .22, .27), 'DK_CollectorHair', (0, 0, 0), 32, 16)
        ellipsoid(head, hc + Vector((0, .30, -.01)), (.30, .17, .29), 'DK_CollectorHat', (0, 0, 0), 32, 14)
        ellipsoid(head, hc + Vector((0, .20, -.01)), (.41, .024, .39), 'DK_CollectorHat', (0, 0, 0), 36, 8)
        ellipsoid(head, hc + Vector((0, .235, -.01)), (.305, .035, .295), 'DK_Gold', (0, 0, 0), 32, 6)
        for side in (-1, 1):
            torus(head, hc + Vector((side * .118, .035, .325)), .1, .01, 'DK_Gold', (1, 1.05, 1), 28, 8)
            ellipsoid(head, hc + Vector((side * .05, -.105, .292)), (.06, .024, .02), 'DK_CollectorHair', (0, 0, side * -15), 14, 8)
        tube(head, [hc + Vector((-.02, .05, .33)), hc + Vector((0, .06, .335)), hc + Vector((.02, .05, .33))], .008, 'DK_Gold', 8)
    return humanoid('DioramaCollectorVisitor', s, slots, outfit, headwear, held)

def premium():
    """The big spender: a wide cream coat with dark lapels, gold buttons and a pink carnation, a top hat, a gold gift bag."""
    s = {'hip_x': .13, 'hip_y': .42, 'thigh': (-.16, (.10, .18, .11)), 'shoe': (-.35, (.11, .07, .16), .05),
         'profile': [(.20, .03), (.24, .33), (.34, .39), (.48, .41), (.62, .40), (.76, .37), (.87, .30), (.95, .20), (1.0, .08)],
         'body_y': (.20, 1.0), 'depth': .78, 'shoulder': (.36, .88), 'sleeve_x': .095, 'sleeve_z': .098, 'head_y': .97}
    slots = {'trousers': 'DK_PremiumTrousers', 'shoes': 'DK_PremiumShoes', 'body': 'DK_PremiumCoat', 'sleeve': 'DK_PremiumCoat',
             'cuff': 'DK_PremiumTrim', 'hand': 'DK_PremiumSkin', 'skin': 'DK_PremiumSkin', 'brow': 'DK_PremiumHair'}
    def outfit(body, first, front):
        ellipsoid(body, (0, .955, 0), (.2, .06, .17), 'DK_PremiumTrim', (0, 0, 0), 28, 10)
        for side in (-1, 1):
            ellipsoid(body, (side * .085, .83, front(.83) + .006), (.05, .13, .014), 'DK_PremiumTrim', (0, 0, side * 22), 16, 8, 1.8)
        for by in (.66, .50):
            ellipsoid(body, (0, by, front(by) + .006), (.032, .032, .016), 'DK_Gold', (0, 0, 0), 14, 8)
        flower = (-.17, .80, front(.80) - .02)
        for k in range(5):
            a = math.tau * k / 5
            ellipsoid(body, (flower[0] + .022 * math.cos(a), flower[1] + .022 * math.sin(a), flower[2] + .01), (.022, .022, .012), 'DK_PremiumFlower', (0, 0, 0), 10, 6)
        ellipsoid(body, (flower[0], flower[1], flower[2] + .016), (.012, .012, .01), 'DK_Gold', (0, 0, 0), 8, 6)
        torus(body, (0, .28, 0), .33, .024, 'DK_PremiumTrim', (1, .78, 1), 40, 8, (90, 0, 0))
    def held(arm, side, hand, sh):
        if side > 0:
            bag = (hand[0] + .02, hand[1] - .2, hand[2] + .02)
            ellipsoid(arm, bag, (.075, .12, .13), 'DK_PremiumBag', (0, 0, 0), 20, 12, 4)
            for dz in (-.05, .05):
                tube(arm, [(bag[0], bag[1] + .11, bag[2] + dz), (hand[0] + .012, hand[1] - .02, hand[2] + dz * .5),
                           (hand[0] + .006, hand[1] + .02, hand[2] + dz * .3)], .01, 'DK_PremiumTrim', 8)
            ellipsoid(arm, (bag[0] - .076, bag[1] + .02, bag[2]), (.006, .04, .04), 'DK_PremiumTrim', (0, 0, 0), 10, 6)
    def headwear(head, hc):
        ellipsoid(head, hc + Vector((0, .12, -.06)), (.345, .25, .32), 'DK_PremiumHair', (0, 0, 0), 32, 16)
        ellipsoid(head, hc + Vector((0, .43, -.01)), (.205, .2, .205), 'DK_PremiumHat', (0, 0, 0), 32, 14, 4)
        ellipsoid(head, hc + Vector((0, .245, -.01)), (.335, .022, .325), 'DK_PremiumHat', (0, 0, 0), 36, 8)
        ellipsoid(head, hc + Vector((0, .29, -.01)), (.21, .036, .21), 'DK_Gold', (0, 0, 0), 32, 6, 4)
    return humanoid('DioramaPremiumVisitor', s, slots, outfit, headwear, held)

# ---------------------------------------------------------------- the three crew (always shop teal)
STOOL_LIFT = .3

def cashier():
    """Pip: a teal shirt with a cream apron and a coral name badge, a teal visor, a pencil behind the ear; stands on a step
    stool behind the counter, so its face and hands clear the countertop."""
    global LIFT
    s = {'hip_x': .11, 'hip_y': .40, 'thigh': (-.15, (.095, .175, .10)), 'shoe': (-.33, (.105, .072, .155), .045),
         'profile': [(.30, .02), (.33, .19), (.40, .27), (.50, .30), (.62, .305), (.74, .285), (.85, .24), (.93, .165), (.97, .07)],
         'body_y': (.30, .98), 'depth': .8, 'shoulder': (.265, .86), 'head_y': .95}
    slots = {'trousers': 'DK_CrewTrousers', 'shoes': 'DK_CrewShoes', 'body': 'DK_CrewTeal', 'sleeve': 'DK_CrewTeal',
             'cuff': 'DK_CrewCream', 'hand': 'DK_PipSkin', 'skin': 'DK_PipSkin', 'brow': 'DK_PipHair',
             'crate': 'DK_Crate', 'slat': 'DK_CrateSlat'}
    def outfit(body, first, front):
        last = len(body.faces)
        paint_where(body, first, last, 'DK_CrewCream', lambda c: c.z > .12 and c.y < .74)
        ellipsoid(body, (0, .925, 0), (.2, .065, .17), 'DK_CrewCream', (0, 0, 0), 28, 10)
        ellipsoid(body, (0, .56, front(.56) + .01), (.085, .055, .014), 'DK_CrewTeal', (0, 0, 0), 16, 8, 2.4)
        ellipsoid(body, (-.13, .80, front(.80) + .006), (.04, .04, .012), 'DK_CrewBadge', (0, -20, 0), 14, 8)
    def headwear(head, hc):
        ellipsoid(head, hc + Vector((0, .13, -.06)), (.35, .25, .325), 'DK_PipHair', (0, 0, 0), 32, 16)
        ellipsoid(head, hc + Vector((0, -.02, -.24)), (.13, .13, .11), 'DK_PipHair', (0, 0, 0), 20, 12)
        torus(head, hc + Vector((0, .2, 0)), .315, .035, 'DK_CrewTeal', (1, .95, 1), 40, 8, (90, 0, 0))
        ellipsoid(head, hc + Vector((0, .21, .29)), (.24, .022, .14), 'DK_CrewCream', (-10, 0, 0), 24, 8, 2)
        tube(head, [hc + Vector((.31, .1, -.06)), hc + Vector((.355, .02, .1))], .012, 'DK_Pencil', 8)
        ellipsoid(head, hc + Vector((.355, .02, .11)), (.013, .013, .016), 'DK_CrewBadge', (0, 0, 0), 8, 6)
    def stool(root):
        global LIFT
        saved, LIFT = LIFT, Vector((0, 0, 0))
        st = Part('Step stool')
        ellipsoid(st, (0, STOOL_LIFT - .035, 0), (.3, .035, .24), 'DK_Stool', (0, 0, 0), 24, 8, 4)
        for x in (-.22, .22):
            for z in (-.16, .16):
                ellipsoid(st, (x, (STOOL_LIFT - .07) / 2, z), (.032, (STOOL_LIFT - .07) / 2, .032), 'DK_Stool', (0, 0, 0), 12, 8, 3)
        make_object(st, root, (0, 0, 0), 'Step stool')
        LIFT = saved
        carry_crate(root, slots, (0, .62, .32))
    LIFT = Vector((0, STOOL_LIFT, 0))
    try:
        return humanoid('DioramaCrewCashier', s, slots, outfit, headwear, None, stool)
    finally:
        LIFT = Vector((0, 0, 0))

def stocker():
    """Nori: sturdy, in teal overalls over a cream shirt, work gloves and a teal beanie with a cream pompom."""
    s = {'hip_x': .12, 'hip_y': .40, 'thigh': (-.15, (.105, .175, .11)), 'shoe': (-.33, (.115, .072, .16), .045),
         'profile': [(.30, .02), (.33, .23), (.40, .32), (.50, .35), (.62, .35), (.74, .33), (.85, .28), (.93, .19), (.97, .07)],
         'body_y': (.30, .98), 'depth': .8, 'shoulder': (.30, .86), 'sleeve_x': .09, 'sleeve_z': .092, 'head_y': .95}
    slots = {'trousers': 'DK_CrewTeal', 'shoes': 'DK_CrewShoes', 'body': 'DK_CrewCream', 'sleeve': 'DK_CrewCream',
             'cuff': 'DK_CrewTeal', 'hand': 'DK_NoriGlove', 'skin': 'DK_NoriSkin', 'brow': 'DK_NoriHair',
             'crate': 'DK_Crate', 'slat': 'DK_CrateSlat'}
    def outfit(body, first, front):
        last = len(body.faces)
        paint_where(body, first, last, 'DK_CrewTeal', lambda c: c.y < .55 or (abs(c.z) > .16 and c.y < .84 and abs(c.x) < .2))
        for side in (-1, 1):
            tube(body, [(side * .13, .82, front(.82) + .006), (side * .17, .95, .1), (side * .17, .97, -.05), (side * .14, .78, -.27)], .022, 'DK_CrewTeal', 8)
            ellipsoid(body, (side * .12, .80, front(.80) + .012), (.022, .022, .012), 'DK_CrewCream', (0, 0, 0), 10, 6)
        ellipsoid(body, (0, .66, front(.66) + .01), (.08, .06, .014), 'DK_CrewCream', (0, 0, 0), 16, 8, 2.4)
        ellipsoid(body, (.11, .70, front(.70) + .02), (.03, .03, .012), 'DK_CrewBadge', (0, 0, 0), 12, 8)
    def headwear(head, hc):
        for side in (-1, 1):
            ellipsoid(head, hc + Vector((side * .3, -.05, -.02)), (.06, .12, .09), 'DK_NoriHair', (0, 0, 0), 14, 8)
        ellipsoid(head, hc + Vector((0, .17, -.03)), (.355, .23, .33), 'DK_CrewTeal', (0, 0, 0), 36, 16)
        torus(head, hc + Vector((0, .1, -.03)), .335, .04, 'DK_CrewTeal', (1, .95, 1), 40, 8, (90, 0, 0))
        ellipsoid(head, hc + Vector((0, .41, -.04)), (.07, .065, .07), 'DK_CrewCream', (0, 0, 0), 16, 10)
    def extra(root):
        carry_crate(root, slots, (0, .62, .32))
    return humanoid('DioramaCrewStocker', s, slots, outfit, headwear, None, extra)

def technician():
    """Moss: a long teal lab coat over a cream collar, mossy green hair, goggles up on the forehead, a vial in the pocket."""
    s = {'hip_x': .10, 'hip_y': .46, 'thigh': (-.175, (.085, .205, .09)), 'shoe': (-.39, (.10, .07, .155), .045),
         'profile': [(.24, .03), (.28, .24), (.38, .27), (.52, .265), (.68, .25), (.82, .235), (.93, .20), (1.01, .13), (1.06, .05)],
         'body_y': (.24, 1.06), 'depth': .8, 'shoulder': (.25, .95), 'arm': 1.08, 'head_y': 1.04}
    slots = {'trousers': 'DK_CrewTrousers', 'shoes': 'DK_CrewShoes', 'body': 'DK_CrewTeal', 'sleeve': 'DK_CrewTeal',
             'cuff': 'DK_CrewCream', 'hand': 'DK_MossSkin', 'skin': 'DK_MossSkin', 'brow': 'DK_MossHair',
             'crate': 'DK_Crate', 'slat': 'DK_CrateSlat'}
    def outfit(body, first, front):
        ellipsoid(body, (0, 1.015, 0), (.16, .06, .135), 'DK_CrewCream', (0, 0, 0), 28, 10)
        for side in (-1, 1):
            ellipsoid(body, (side * .07, .93, front(.93) + .006), (.04, .1, .012), 'DK_CrewCream', (0, 0, side * 24), 14, 8, 1.8)
        for by in (.82, .66, .50):
            ellipsoid(body, (0, by, front(by) + .004), (.022, .022, .012), 'DK_CrewCream', (0, 0, 0), 12, 8)
        pocket = (.13, .78, front(.78) + .008)
        ellipsoid(body, pocket, (.055, .045, .012), 'DK_CrewCream', (0, 18, 0), 14, 8, 2.4)
        tube(body, [(pocket[0] + .01, pocket[1] - .01, pocket[2] + .01), (pocket[0] + .01, pocket[1] + .1, pocket[2] + .012)], .016, 'DK_Lens', 10)
        ellipsoid(body, (pocket[0] + .01, pocket[1] + .105, pocket[2] + .012), (.019, .016, .019), 'DK_CrewBadge', (0, 0, 0), 10, 6)
        ellipsoid(body, (-.13, .86, front(.86) + .006), (.035, .035, .012), 'DK_CrewBadge', (0, 20, 0), 12, 8)
    def headwear(head, hc):
        ellipsoid(head, hc + Vector((0, .15, -.05)), (.35, .24, .325), 'DK_MossHair', (0, 0, 0), 32, 16)
        for k, (dx, dy) in enumerate(((-.08, .33), (.02, .36), (.11, .32))):
            ellipsoid(head, hc + Vector((dx, dy, .02)), (.07, .09, .07), 'DK_MossHair', (0, 0, (k - 1) * 25), 14, 8)
        torus(head, hc + Vector((0, .2, 0)), .33, .022, 'DK_MossGoggle', (1, .95, 1), 40, 8, (90, 0, 0))
        for side in (-1, 1):
            g = hc + Vector((side * .1, .23, .27))
            torus(head, g, .065, .016, 'DK_MossGoggle', (1, 1, 1), 24, 8, (-25, 0, 0))
            ellipsoid(head, g, (.06, .06, .012), 'DK_Lens', (-25, 0, 0), 18, 6)
    def extra(root):
        carry_crate(root, slots, (0, .70, .32))
    return humanoid('DioramaCrewTechnician', s, slots, outfit, headwear, None, extra)

CHARACTERS = [  # role id, kit asset, builder, crew
    ('casual', 'DioramaCasualVisitor', casual, False),
    ('bargain', 'DioramaBargainVisitor', bargain, False),
    ('collector', 'DioramaCollectorVisitor', collector, False),
    ('premium', 'DioramaPremiumVisitor', premium, False),
    ('cashier', 'DioramaCrewCashier', cashier, True),
    ('stocker', 'DioramaCrewStocker', stocker, True),
    ('technician', 'DioramaCrewTechnician', technician, True),
]

# ================================================================ hand-drawn effect sheet
CELL, COLS = 128, 4
SHEET_ROWS = ['bubble', 'swirl', 'sparkle', 'puff', 'ripple', 'coin']
INK = np.array([0x1B, 0x41, 0x3E]) / 255

def flipbook():
    ss = 3
    n = CELL * ss
    g = (np.arange(n) + .5) / n - .5
    X, Y = np.meshgrid(g, -g)
    R, T = np.hypot(X, Y), np.arctan2(Y, X)
    def wob(seed, amp=.008):
        return amp * (np.sin(3 * T + seed) + .6 * np.sin(5 * T + 2.1 * seed) + .4 * np.sin(7 * T - seed))
    def ellipse(cx, cy, rx, ry):
        return (np.hypot((X - cx) / rx, (Y - cy) / ry) - 1) * min(rx, ry)
    def star(r_in, r_out, spin, cx=0.0, cy=0.0):
        # Four pointed tips with concave sides (an astroid), plus a small round core so the centre reads.
        c, s = math.cos(spin), math.sin(spin)
        x, y = (X - cx) * c - (Y - cy) * s, (X - cx) * s + (Y - cy) * c
        p = .55
        m = (np.abs(x) ** p + np.abs(y) ** p) ** (1 / p)
        tips = (m - r_out) * .45
        core = np.hypot(x, y) - r_in
        return np.minimum(tips, core)
    def cell(frame):
        rgb = np.ones((n, n, 3)); alpha = np.zeros((n, n))
        return rgb, alpha
    def paint(rgb, alpha, d, fill=(1, 1, 1), fill_alpha=1.0, line=.022):
        inside = np.clip(.5 - d / (1.2 / n), 0, 1)
        edge = np.clip(.5 - (np.abs(d) - line * .5) / (1.2 / n), 0, 1)
        f = np.array(fill)
        rgb[:] = rgb * (1 - inside[..., None] * fill_alpha) + f * inside[..., None] * fill_alpha
        alpha[:] = np.maximum(alpha, inside * fill_alpha)
        rgb[:] = rgb * (1 - edge[..., None]) + INK * edge[..., None]
        alpha[:] = np.maximum(alpha, edge)
    def highlight(rgb, alpha, d, a=.95):
        inside = np.clip(.5 - d / (1.2 / n), 0, 1) * a
        rgb[:] = rgb * (1 - inside[..., None]) + inside[..., None]
        alpha[:] = np.maximum(alpha, inside)
    frames = []
    for row, kind in enumerate(SHEET_ROWS):
        for f in range(COLS):
            rgb, alpha = cell(f)
            s = row * 11 + f * 3
            if kind == 'bubble':
                if f < 3:
                    r = (.2, .36, .37)[f]; sq = (1, 1, .9)[f]
                    d = ellipse(0, 0, r / sq ** .5, r * sq ** .5) + wob(s)
                    paint(rgb, alpha, d, (1, 1, 1), .28)
                    highlight(rgb, alpha, ellipse(-r * .38, r * .42, r * .22, r * .12) + wob(s + 1, .003))
                else:
                    ring = np.abs(R - .34) - .015 + wob(s, .01)
                    gaps = np.cos(5 * T + 1.3) > .35
                    ring = np.where(gaps, .5, ring)
                    paint(rgb, alpha, ring, (1, 1, 1), 1.0, .02)
                    for k in range(4):
                        a = k * math.tau / 4 + .6
                        paint(rgb, alpha, ellipse(.43 * math.cos(a), .43 * math.sin(a), .045, .045), (1, 1, 1), .9)
            elif kind == 'swirl':
                # A fat curled ribbon drawn as a band around an Archimedean path.
                turn = f * .7
                rr = .08 + (T + math.pi + turn) % math.tau / math.tau * .3
                band = np.abs(R - rr) - (.035 + .02 * (rr - .08) / .3)
                band = np.where(R > .44, .5, band)
                paint(rgb, alpha, band + wob(s, .004), (1, 1, 1), 1.0, .02)
            elif kind == 'sparkle':
                size = (.22, .42, .38, .2)[f]; spin = (0, 0, .25, .5)[f]
                paint(rgb, alpha, star(size * .2, size, spin) + wob(s, .003), (1, 1, 1), 1.0)
                if f in (1, 2):
                    paint(rgb, alpha, star(.025, .11, spin + .4, .3, .3), (1, 1, 1), 1.0, .018)
            elif kind == 'puff':
                grow = (.7, 1.0, 1.12, 1.2)[f]
                blobs = [(0, -.02, .2), (-.17, -.06, .14), (.17, -.07, .15), (-.08, .12, .15), (.1, .11, .13)]
                if f == 3:
                    blobs = [(-.24, -.04, .12), (.2, .02, .11), (-.02, .22, .1)]
                d = np.full_like(X, 9.0)
                for bx, by, br in blobs:
                    d = np.minimum(d, ellipse(bx * grow, by * grow, br * grow, br * grow))
                d += wob(s, .006)
                paint(rgb, alpha, d, (1, 1, 1), 1.0 if f < 3 else .85)
                # A soft shaded crescent along the lower edge only.
                inner = np.clip(.5 - d / (1.2 / n), 0, 1)
                crescent = inner * np.clip((d + .055) / .03, 0, 1) * np.clip(-Y / .08, 0, 1)
                rgb[:] = rgb * (1 - .2 * crescent[..., None])
            elif kind == 'ripple':
                r = (.14, .24, .34, .43)[f]
                a = (1, .95, .8, .55)[f]
                for k, rr in enumerate((r, r * .58)):
                    if f == 0 and k == 1:
                        continue
                    ring = np.abs(np.hypot(X / 1.0, Y / .45) - rr) - .012
                    paint(rgb, alpha, ring + wob(s + k, .003), (1, 1, 1), a, .016)
                alpha *= a
            elif kind == 'coin':
                w = (1, .62, .14, .62)[f]
                d = ellipse(0, 0, .3 * w + .02, .3)
                paint(rgb, alpha, d + wob(s, .003), (1, 1, 1), 1.0, .026)
                if f != 2:
                    paint(rgb, alpha, np.abs(ellipse(0, 0, .2 * w, .2)) - .006, (1, 1, 1), 0, .018)
                    highlight(rgb, alpha, ellipse(-.1 * w, .14, .05 * w + .01, .03), .8)
            # Downsample the supersampled cell (box filter).
            rgb_d = rgb.reshape(CELL, ss, CELL, ss, 3).mean(axis=(1, 3))
            a_d = alpha.reshape(CELL, ss, CELL, ss).mean(axis=(1, 3))
            frames.append((row, f, rgb_d, a_d))
    W, H = CELL * COLS, CELL * len(SHEET_ROWS)
    sheet = np.zeros((H, W, 4))
    for row, f, rgb_d, a_d in frames:
        sheet[row * CELL:(row + 1) * CELL, f * CELL:(f + 1) * CELL, :3] = rgb_d
        sheet[row * CELL:(row + 1) * CELL, f * CELL:(f + 1) * CELL, 3] = a_d
    img = bpy.data.images.new('DioramaFlipbook', W, H, alpha=True, float_buffer=False)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(np.flipud(sheet).astype(np.float32).ravel())
    path = EXPORT / 'DioramaFlipbook.png'
    img.filepath_raw = str(path); img.file_format = 'PNG'
    img.save()
    return path, W, H

# ================================================================ the colour variant atlas
def variant_atlas():
    """One texel per (species, variant) row and slot column, sRGB; rows species_index * 4 + variant, top row first.
    The game reads it to colour every variant; rows past the five species are empty until AD-F fills them."""
    W, H = len(SLOTS), ATLAS_SPECIES_CAPACITY * len(VARIANT_NAMES)
    px = np.zeros((H, W, 4))
    for s_index, (sid, _, _, variants) in enumerate(SPECIES):
        for v_index, vname in enumerate(VARIANT_NAMES):
            row = s_index * len(VARIANT_NAMES) + v_index
            for k, code in enumerate(variants[vname]):
                rgb = [int(code[i:i + 2], 16) / 255 for i in (0, 2, 4)]
                alpha = int(code[6:8], 16) / 255 if len(code) == 8 else 1.0
                px[row, k] = (*rgb, alpha)
    img = bpy.data.images.new('DioramaVariantAtlas', W, H, alpha=True, float_buffer=False)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(np.flipud(px).astype(np.float32).ravel())
    path = EXPORT / 'DioramaVariantAtlas.png'
    img.filepath_raw = str(path); img.file_format = 'PNG'
    img.save()
    # A big swatch sheet of the same colours for people to read (not used by the game).
    cell = 36
    sheet = np.ones((len(SPECIES) * len(VARIANT_NAMES) * cell, W * cell, 4))
    for row in range(len(SPECIES) * len(VARIANT_NAMES)):
        for k in range(W):
            c = px[row, k].copy()
            if k == W - 1:  # the sheen: shown over white by its strength
                c[:3] = 1 - (1 - c[:3]) * max(c[3], .1)
            c[3] = 1
            sheet[row * cell:(row + 1) * cell, k * cell:(k + 1) * cell] = c
            sheet[row * cell:(row + 1) * cell, k * cell] = (.1, .2, .2, 1)
        sheet[row * cell] = (.1, .2, .2, 1)
    simg = bpy.data.images.new('DioramaVariantSwatches', sheet.shape[1], sheet.shape[0], alpha=True, float_buffer=False)
    simg.colorspace_settings.name = 'sRGB'
    simg.pixels.foreach_set(np.flipud(sheet).astype(np.float32).ravel())
    spath = SOURCE / 'DioramaKit_VariantSwatches.png'
    simg.filepath_raw = str(spath); simg.file_format = 'PNG'
    simg.save()
    return path, W, H, spath

# ================================================================ build, export, review renders, manifest
assets = {}
BUILDS = [(asset, CREATURE_BUILDERS[sid]) for sid, asset, _, _ in SPECIES] + [(asset, build) for _, asset, build, _ in CHARACTERS]
for name, build in BUILDS:
    # Blender object names are global, so each asset is built and exported before the next one is made; its parts
    # are then renamed with its id, freeing the shared names (Eyes, Lids, Body...) for the next asset.
    root = assets[name] = build()
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    dest = EXPORT / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(dest), use_selection=True, global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
                             bake_space_transform=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=False,
                             mesh_smooth_type='OFF', use_tspace=False, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False)
    rec = stats(root, name)
    rec.update({'id': name, 'export': str(dest.relative_to(REPO)).replace('\\', '/'),
                'sha256': hashlib.sha256(dest.read_bytes()).hexdigest(), 'bytes': dest.stat().st_size,
                'objects': sorted(o.name for o in root.children_recursive),
                'shape_keys': {o.name: [k.name for k in o.data.shape_keys.key_blocks[1:]] for o in root.children_recursive
                               if o.type == 'MESH' and o.data.shape_keys},
                'material_slots': sorted({m.name for o in root.children_recursive if o.type == 'MESH' for m in o.data.materials})})
    assert rec['triangles'] < 40000, rec
    assert not any('.' in o for o in rec['objects']), rec['objects']
    RECORDS.append(rec)
    for child in root.children_recursive:
        child.name = name + ' / ' + child.name

sheet_path, sheet_w, sheet_h = flipbook()
atlas_path, atlas_w, atlas_h, swatch_path = variant_atlas()

renders = []
if not args.no_render:
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_object_outline = True
    scene.display.shading.object_outline_color = (0.02, .06, .06)
    scene.display.shading.show_cavity = True
    scene.display.shading.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new('Review sky') if scene.world is None else scene.world
    scene.world.color = (.85, .78, .62)
    lineup = [asset for _, asset, _, _ in SPECIES[:5]]  # AD-B's five, as AD-B rendered them
    for k, asset in enumerate(lineup):
        assets[asset].location = u(((k - 2) * 1.25, 0, 0))
    wave = [asset for _, asset, _, _ in SPECIES[5:]]
    for k, asset in enumerate(wave):
        assets[asset].location = u((100 + k * 3, 0, 0))
        for o in [assets[asset]] + list(assets[asset].children_recursive):
            o.hide_render = True
    cast = [asset for _, asset, _, _ in CHARACTERS]
    for k, asset in enumerate(cast):
        assets[asset].location = u(((k - 3) * 1.0, 0, 3.5))
    camdata = bpy.data.cameras.new('Diorama review camera'); cam = bpy.data.objects.new('Diorama review camera', camdata)
    COLLECTION.objects.link(cam); scene.camera = cam; camdata.lens = 50
    def shot(fname, pos, target):
        cam.location = u(pos)
        cam.rotation_euler = (u(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(SOURCE / fname)
        bpy.ops.render.render(write_still=True)
        renders.append(str((SOURCE / fname).relative_to(REPO)).replace('\\', '/'))
    scene.render.resolution_x, scene.render.resolution_y = 1800, 700
    shot('DioramaKit_Creatures_Front.png', (-1.2, 1.6, -8.5), (0, .1, 0))
    shot('DioramaKit_Creatures_Back.png', (1.6, 1.6, 8.5), (0, .1, 0))
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    for k, asset in enumerate(lineup):
        x = (k - 2) * 1.25
        for other in assets.values():
            for o in [other] + list(other.children_recursive):
                o.hide_render = other is not assets[asset]
        shot(asset.replace('Diorama', 'DioramaKit_') + '_Front.png', (x - 1.25, .35, -1.55), (x, .02, 0))
        shot(asset.replace('Diorama', 'DioramaKit_') + '_Side.png', (x - 2.0, .1, .02), (x, .02, .06))
    for other in assets.values():
        for o in [other] + list(other.children_recursive):
            o.hide_render = other not in [assets[a] for a in cast] or o.name.endswith('Carry crate')
    scene.render.resolution_x, scene.render.resolution_y = 2000, 800
    shot('DioramaKit_Characters_Front.png', (0, 1.45, 3.5 + 10.5), (0, .95, 3.5))
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    for k, (role, asset, _, crew) in enumerate(CHARACTERS):
        x = (k - 3) * 1.0
        for other in assets.values():
            for o in [other] + list(other.children_recursive):
                o.hide_render = other is not assets[asset] or o.name.endswith('Carry crate')
        name = 'Casual' if role == 'casual' else asset.replace('Diorama', '').replace('Visitor', '').replace('Crew', 'Crew')
        top = assets[asset].children_recursive
        head = next(o for o in top if o.name.endswith('Head pivot'))
        hy = un(head.matrix_world.translation)[1] + .33
        shot('DioramaKit_' + name + '_Front.png', (x + .9, 1.25 + hy - 1.28, 3.5 + 3.4), (x, .86 + hy - 1.28, 3.5))
        shot('DioramaKit_' + name + '_Face.png', (x + .25, hy + .07, 3.5 + 1.3), (x, hy, 3.5))
    # GR-12: each family's eight on one sheet, four by two, from the front three-quarter and from the side.
    for family in FAMILIES:
        members = [asset for _, asset, fam, _ in SPECIES if fam == family]
        for other in assets.values():
            for o in [other] + list(other.children_recursive):
                o.hide_render = other.name not in members
        for j, asset in enumerate(members):
            row, col = divmod(j, 4)
            assets[asset].location = u(((col - 1.5) * 1.55, (1 - row) * 1.3, 40))
        scene.render.resolution_x, scene.render.resolution_y = 1800, 1000
        shot('DioramaKit_' + family + '_Front.png', (-1.7, 1.3, 40 - 8.2), (0, .62, 40))
        for j, asset in enumerate(members):
            row, col = divmod(j, 4)
            assets[asset].location = u((0, (1 - row) * 1.3, 40 + (col - 1.5) * 2.2))
        scene.render.resolution_x, scene.render.resolution_y = 2000, 900
        shot('DioramaKit_' + family + '_Side.png', (-11.2, 1.0, 40.1), (0, .62, 40.1))
    for asset in assets.values():
        asset.location = u((0, 0, 0))
        for o in [asset] + list(asset.children_recursive):
            o.hide_render = False

blend = SOURCE / 'EvolutionShop_DioramaKit.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
manifest = {
    'schema': 1, 'title': 'Evolution Shop Pocket Aquarium Diorama kit (AD-A slice, AD-B creature families, AD-C characters, GR-12 waves 1 to 4, GR-12B body plans)',
    'origin': {'author': 'Original local Blender authorship by Claude for Evolution Shop (generated from Tools/Art/create_diorama_kit.py)',
               'external_sources': [], 'license': 'Original project work; no third-party source or asset license',
               'paid_generation': False, 'external_uploads': False},
    'blender_version': bpy.app.version_string, 'source_script': 'Tools/Art/create_diorama_kit.py',
    'source_script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'blend_source': str(blend.relative_to(REPO)).replace('\\', '/'),
    'scale': '1 unit = 1 metre', 'up': 'Unity +Y',
    'front': {**{asset: 'local -Z (creature)' for _, asset, _, _ in SPECIES}, **{asset: 'local +Z (character)' for _, asset, _, _ in CHARACTERS}},
    'characters': [{'role': role, 'asset': asset, 'crew': crew} for role, asset, _, crew in CHARACTERS],
    'humanoid_rig': {'joints': ['Left hip', 'Right hip', 'Left shoulder', 'Right shoulder', 'Head pivot'],
                     'head_parts': ['Head', 'Eyes', 'Pupils', 'Glints', 'Lids', 'Blush', 'Mouth', 'Left brow pivot', 'Right brow pivot'],
                     'face': {'Lids': ['Blink'], 'Mouth': ['Happy', 'Haggle', 'Shocked', 'Ouch']},
                     'parts': ['Body'], 'crew_parts': ['Carry crate'], 'cashier_parts': ['Step stool'],
                     'step_stool_m': STOOL_LIFT,
                     'notes': 'One rig and one builder for every character; roles differ by proportions, outfit, props and colours. Crew wear shop teal; customers never do.'},
    'families': FAMILIES,
    'species': [{'id': sid, 'asset': asset, 'family': family, 'atlas_rows': [k * len(VARIANT_NAMES), k * len(VARIANT_NAMES) + len(VARIANT_NAMES) - 1],
                 'variants': {v: dict(zip(SLOTS, codes)) for v, codes in variants.items()}}
                for k, (sid, asset, family, variants) in enumerate(SPECIES)],
    'rig': {'pivots': ['Left fin pivot', 'Right fin pivot', 'Tail pivot', 'Crest pivot'], 'family_pivots': {'Sparkline': ['Lantern pivot'], 'Gloamer': ['Lantern pivot']},
            'body_plans': {'Keel pivot': 'a fin under the body that sways with the crest (the sunfish), GR-12B',
                           'Wave keys': 'WaveSin, WaveSinNeg, WaveCos, WaveCosNeg on an undulating Body (the oarfish): weighted by the cosine and sine of the swim, a wave from head to tail'},
            'face': {'Body': ['Puffed'], 'Lids': ['Blink', 'Happy'], 'Lips': ['Happy', 'Surprised']},
            'parts': ['Body', 'Eyes', 'Pupils', 'Glints', 'Lids', 'Blush', 'Lips']},
    'export_settings': {'format': 'FBX', 'forward': '-Z', 'up': 'Y', 'global_scale': 1, 'bake_space_transform': True,
                        'animations': False, 'shape_keys': True, 'modifiers': 'none (shape keys require unmodified meshes)',
                        'axis_mapping': 'Unity (x, y, z) -> Blender (-x, -z, y), a reflection; probed to import exactly',
                        'hierarchy': 'every part directly under the asset root and its own pivot; the characters\' head parts are reparented onto Head pivot, and the crew\'s Carry crate onto the parcel socket, in game'},
    'unity_import_expected': {'globalScale': 1, 'useFileScale': True, 'importAnimation': False, 'addCollider': False,
                              'importBlendShapes': True, 'material_mapping': 'DK_* slot names map to runtime toon materials'},
    'materials': {name: {'srgb_hex': '#' + code} for name, code in PALETTE.items()},
    'assets': RECORDS,
    'flipbook': {'export': str(sheet_path.relative_to(REPO)).replace('\\', '/'), 'size': [sheet_w, sheet_h],
                 'cell_px': CELL, 'columns': COLS, 'rows_top_to_bottom': SHEET_ROWS,
                 'sha256': hashlib.sha256(sheet_path.read_bytes()).hexdigest(),
                 'drawing': 'White fills and dark ink outline drawn from wobbled signed-distance shapes, 3x supersampled; particles tint the fill.'},
    'variant_atlas': {'export': str(atlas_path.relative_to(REPO)).replace('\\', '/'), 'size': [atlas_w, atlas_h],
                      'columns': SLOTS, 'variants': VARIANT_NAMES, 'species_capacity': ATLAS_SPECIES_CAPACITY,
                      'row': 'species index * 4 + variant index, counted from the top', 'colour_space': 'sRGB',
                      'sheen': "the Sheen column is Shimmer's iridescent rim; its alpha is the strength (0 for the other variants)",
                      'sha256': hashlib.sha256(atlas_path.read_bytes()).hexdigest(),
                      'review_swatches': str(swatch_path.relative_to(REPO)).replace('\\', '/')},
    'renders': renders,
    'validation_limits': ['Workbench review renders show shape and colour only, not the in-game toon shading, outline or bloom.',
                          'In-game scale, fit inside containers and posing are verified by the AD-A, AD-B, AD-C and GR-12 routes, not here.'],
}
(SOURCE / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print('DIORAMA_KIT_COMPLETE ' + json.dumps([{'id': r['id'], 'triangles': r['triangles'], 'size': r['bounds_unity_m']['size']} for r in RECORDS]))
