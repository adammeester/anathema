"""Rigs the generated Silver Knight mesh (Art/Concepts/silver_knight_v1.glb) onto the same bone names the other
characters use, so ProceduralWalk and the ability scripts can drive it. It is an optional asset: nothing references it.

Unlike the other scripts the mesh is not built here; it is one fused, textured mesh, so the weights are computed from
the distance to each bone (sharp falloff, so plates stay near-rigid and only bend close to a joint).

    blender -b --python Art/silver_knight.py                    # writes Assets/Models/SilverKnight.fbx
    blender -b --python Art/silver_knight.py -- --render DIR    # also renders a walking-pose preview into DIR
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import common as c  # noqa: E402

SRC_GLB = os.path.join(HERE, "Concepts", "silver_knight_v1.glb")
OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "SilverKnight.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

HEIGHT = 2.3    # metres to the tip of the plume, to match the Grey Knight's footprint
DEPTH = 1.2     # the generated mesh is a little flat front-to-back
FALLOFF = 8     # higher = more rigid plates, narrower blend at the joints

c.configure(1.0)

# (name, parent, head, tail) in metres. Character faces -Y, "L" is +X.
BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.05), (0, 0, 1.25)),
    ("Spine", "Hips", (0, 0, 1.25), (0, 0, 1.88)),
    ("Head", "Spine", (0, 0, 1.9), (0, 0, 2.2)),
    ("CoatTail", "Spine", (0.25, 0.24, 1.25), (0.3, 0.3, 0.3)),
]
for s, side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"UpperArm_{side}", "Spine", (0.42 * s, 0, 1.72), (0.63 * s, 0, 1.42)),
        (f"Forearm_{side}", f"UpperArm_{side}", (0.63 * s, 0, 1.42), (0.82 * s, 0, 1.14)),
        (f"Hand_{side}", f"Forearm_{side}", (0.82 * s, 0, 1.14), (0.93 * s, 0, 0.93)),
        (f"Thigh_{side}", "Hips", (0.22 * s, 0, 1.1), (0.25 * s, 0, 0.66)),
        (f"Shin_{side}", f"Thigh_{side}", (0.25 * s, 0, 0.66), (0.27 * s, 0, 0.2)),
        (f"Foot_{side}", f"Shin_{side}", (0.27 * s, 0, 0.2), (0.3 * s, -0.25, 0.03)),
    ]
# Unweighted grip markers in the palms, for attaching weapons later.
BONES += [
    ("Grip_L", "Hand_L", (0.88, -0.04, 1.02), (0.88, -0.04, 1.08)),
    ("Grip_R", "Hand_R", (-0.88, -0.04, 1.02), (-0.88, -0.04, 1.08)),
]
MARKERS = {"Root", "Grip_L", "Grip_R"}
TORSO = {"Hips", "Spine"}  # wide bones: distance is measured with X squashed, so the chest does not go to the arms


def load_mesh():
    bpy.ops.import_scene.gltf(filepath=SRC_GLB)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    body = bpy.context.active_object
    if body.parent:
        world = body.matrix_world.copy()
        body.parent = None
        body.matrix_world = world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o is not body:
            bpy.data.objects.remove(o)

    vs = body.data.vertices
    z0, z1 = min(v.co.z for v in vs), max(v.co.z for v in vs)
    cx = (min(v.co.x for v in vs) + max(v.co.x for v in vs)) / 2
    k = HEIGHT / (z1 - z0)
    for v in vs:
        v.co = Vector(((v.co.x - cx) * k, v.co.y * k * DEPTH, (v.co.z - z0) * k))
    body.name = body.data.name = "SilverKnight"
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
    return body


def smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def seg_dist(p, a, b):
    ab = b - a
    t = min(1.0, max(0.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def skin(body):
    segs = [(n, Vector(h), Vector(t)) for n, _, h, t in BONES if n not in MARKERS and n != "CoatTail"]
    groups = {n: body.vertex_groups.new(name=n) for n, _, _, _ in BONES}
    for v in body.data.vertices:
        p = v.co
        w = {}
        for name, a, b in segs:
            if name in TORSO:
                q = Vector((p.x / 2.2, p.y / 1.4, p.z))
                d = seg_dist(q, a, b)
            elif name == "Head":
                d = seg_dist(p, a, b) + 2.0 * max(0.0, abs(p.x) - 0.24)  # the collar and pauldron tops are not head
            else:
                d = seg_dist(p, a, b)
            f = 1.0 / (d + 0.02) ** FALLOFF
            side = 1 if name.endswith("_L") else -1 if name.endswith("_R") else 0
            if side:
                if "Arm" in name or "Hand" in name:
                    f *= smooth(0.3, 0.42, p.x * side)
                else:
                    f *= smooth(-0.03, 0.03, p.x * side)
            w[name] = f

        top = sorted(w.items(), key=lambda kv: -kv[1])[:2]
        total = sum(f for _, f in top) or 1.0
        w = {n: f / total for n, f in top}

        # Cloth. The cloak hangs behind the left side: its lower part gets its own bone (ProceduralWalk sways
        # "CoatTail"), the part over the back rides the spine. The front tabard hangs from the belt.
        cloak = smooth(0.17, 0.22, p.y) * smooth(-0.1, -0.04, p.x)
        # Its outer edge sticks out past the leg, where nothing else is below the hand.
        cloak = max(cloak, smooth(0.46, 0.5, p.x) * (1 - smooth(0.7, 0.74, p.x)) * (1 - smooth(1.0, 1.15, p.z)))
        tail = cloak * (1 - smooth(1.15, 1.35, p.z))
        back = cloak * smooth(1.15, 1.35, p.z) * (1 - smooth(1.5, 1.65, p.z))
        tabard = (1 - smooth(0.11, 0.15, abs(p.x))) * (1 - smooth(-0.14, -0.09, p.y)) * (1 - smooth(1.1, 1.2, p.z))
        for bone, amount in (("CoatTail", tail), ("Spine", back), ("Hips", tabard)):
            if amount > 0:
                w = {n: f * (1 - amount) for n, f in w.items()}
                w[bone] = w.get(bone, 0) + amount

        for n, f in w.items():
            if f > 0.001:
                groups[n].add([v.index], f, "REPLACE")


def walking_pose(arm):
    """Mid-stride pose with an arm raised, to check how the fused mesh bends at the joints."""
    turns = {"Thigh_L": 34, "Shin_L": -40, "Foot_L": 5, "Thigh_R": -34, "Shin_R": -8, "Foot_R": 30,
             "UpperArm_R": -55, "Forearm_R": -40, "CoatTail": -14, "Spine": 5}
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for name, _, _, _ in BONES:  # parent-first
        if name not in turns:
            continue
        pb = arm.pose.bones[name]
        bpy.context.view_layer.update()
        head = pb.matrix.to_translation()
        swing = Matrix.Translation(head) @ Matrix.Rotation(math.radians(turns[name]), 4, "X") @ Matrix.Translation(-head)
        pb.matrix = swing @ pb.matrix
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")


body = load_mesh()
armature = c.build_armature(BONES)
skin(body)
body.parent = armature
body.modifiers.new("Armature", "ARMATURE").object = armature

os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
body.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    apply_scale_options="FBX_SCALE_ALL",
    mesh_smooth_type="OFF",
    path_mode="COPY",
    embed_textures=True,  # the baked colour texture travels inside the FBX
)
print("Exported", OUT_FBX)

if RENDER_DIR:
    walking_pose(armature)
    c.render_previews(RENDER_DIR, focus_z=1.2, distance=1.15)
