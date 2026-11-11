"""Sizes the generated Silver Knight weapon meshes (Art/Concepts/*_v1.glb) and exports each as its own FBX, with the
origin at the main grip so it can be parented straight to a hand / grip bone. Optional assets: nothing references them.

    blender -b --python Art/silver_knight_weapons.py    # writes Assets/Models/SilverKnight_<Weapon>.fbx

Polearms stand along +Z (head up); the cannon points down -Y (the characters' forward).
"""
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models"))

# name, source, long axis (0=x, 1=y, 2=z), length in metres, grip as a fraction of the bounding box on each axis
WEAPONS = [
    ("Glaive", "glaive_v1.glb", 2, 2.6, (0.5, 0.5, 0.35)),
    ("Warhammer", "warhammer_v1.glb", 2, 2.1, (0.5, 0.5, 0.25)),
    ("Cannon", "cannon_v1.glb", 1, 1.9, (0.5, 0.66, 0.2)),
]

for name, src, axis, length, grip in WEAPONS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "Concepts", src))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    if obj.parent:
        world = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    vs = obj.data.vertices
    lo = Vector([min(v.co[i] for v in vs) for i in range(3)])
    hi = Vector([max(v.co[i] for v in vs) for i in range(3)])
    k = length / (hi[axis] - lo[axis])
    pivot = Vector([lo[i] + (hi[i] - lo[i]) * grip[i] for i in range(3)])
    for v in vs:
        v.co = (v.co - pivot) * k
    obj.name = obj.data.name = f"SilverKnight_{name}"

    path = os.path.join(OUT_DIR, f"SilverKnight_{name}.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, bake_anim=False,
                             apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="OFF",
                             path_mode="COPY", embed_textures=True)
    print("Exported", path)
