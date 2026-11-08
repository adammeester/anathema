"""Builds the stylized Inquisitor character: chunky Battlerite-ish proportions, 40k flair.

Run (from the repo root):
    blender -b --python Art/inquisitor.py                    # writes Assets/Models/Inquisitor.fbx
    blender -b --python Art/inquisitor.py -- --render DIR    # also renders preview PNGs into DIR

Rigid-skinned to a small armature (every part is 100% weighted to one bone). Character faces -Y in Blender,
which imports into Unity facing +Z. Design units are metres before the global scale S, which brings the
character to ~2 m (the player CharacterController height).
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

S = 0.9
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "Inquisitor.fbx"))

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

# ---------------------------------------------------------------- scene setup
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- materials
MATS = {}


def mat(name, color, metallic=0.0, rough=0.55, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    m.diffuse_color = (*color, 1.0)
    MATS[name] = m


mat("coat", (0.42, 0.04, 0.07))
mat("coat_dark", (0.24, 0.02, 0.05))
mat("leather", (0.05, 0.045, 0.05), rough=0.5)
mat("trousers", (0.11, 0.11, 0.13))
mat("gold", (0.95, 0.68, 0.16), metallic=0.8, rough=0.3)
mat("brass", (0.62, 0.45, 0.2), metallic=0.7, rough=0.4)
mat("steel", (0.2, 0.22, 0.25), metallic=0.8, rough=0.35)
mat("skin", (0.86, 0.6, 0.48))
mat("hair", (0.12, 0.08, 0.06))
mat("bone", (0.92, 0.88, 0.76))
mat("hat", (0.06, 0.055, 0.07), rough=0.8)
mat("parchment", (0.86, 0.78, 0.6))
mat("wax", (0.65, 0.05, 0.05))
mat("scar", (0.72, 0.36, 0.36))
mat("eye", (0.03, 0.03, 0.04))
mat("blade", (0.3, 0.9, 1.0), emit=(0.2, 0.85, 1.0))

# ---------------------------------------------------------------- helpers
parts = []


def v(x, y, z):
    return Vector((x, y, z)) * S


def finish(obj, material, bone, bevel=0.0, xf=None):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if xf is not None:  # scale is already baked, so the pivot rotation can not shear the mesh
        obj.matrix_world = xf @ obj.matrix_world
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        m = obj.modifiers.new("bevel", "BEVEL")
        m.width = bevel * S
        m.segments = 3
        m.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
    obj.data.materials.append(MATS[material])
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    parts.append(obj)
    return obj


def prim(kind, loc, size, material, bone, rot=(0, 0, 0), bevel=0.0, xf=None, verts=20):
    """kind: cube | sphere | cyl. `size` is the full extent on each axis."""
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=verts, ring_count=max(8, verts // 2))
    else:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.5, depth=1, vertices=verts)
    o = bpy.context.active_object
    o.location = v(*loc)
    o.scale = Vector(size) * S
    o.rotation_euler = Euler([math.radians(a) for a in rot])
    return finish(o, material, bone, bevel, xf)


def limb(a, b, r1, r2, material, bone, xf=None):
    """Tapered cylinder from point a (radius r1) to point b (radius r2)."""
    a, b = v(*a), v(*b)
    d = b - a
    bpy.ops.mesh.primitive_cone_add(radius1=r1 * S, radius2=r2 * S, depth=d.length, vertices=16)
    o = bpy.context.active_object
    o.location = (a + b) / 2
    up = "Y" if abs(d.y) < 0.9 * d.length else "X"
    o.rotation_euler = d.to_track_quat("Z", up).to_euler()
    return finish(o, material, bone, 0.0, xf)


def torus(loc, major, minor, material, bone, squash_y=1.0):
    bpy.ops.mesh.primitive_torus_add(major_radius=major * S, minor_radius=minor * S, major_segments=24, minor_segments=8)
    o = bpy.context.active_object
    o.location = v(*loc)
    o.scale = (1.0, squash_y, 1.0)
    return finish(o, material, bone)


def pivot_rotation(pivot, rot_deg):
    p = v(*pivot)
    r = Euler([math.radians(a) for a in rot_deg]).to_matrix().to_4x4()
    return Matrix.Translation(p) @ r @ Matrix.Translation(-p)


# ---------------------------------------------------------------- model
HEAD_C = (0, 0, 1.82)
HEAD_R = 0.24


def face_y(x, z):
    """Y of the head surface at (x, z) so face features can sit on it."""
    dz = z - HEAD_C[2]
    return -math.sqrt(max(HEAD_R ** 2 - x * x - dz * dz, 0.0))


def build_body():
    for s, side in ((1, "L"), (-1, "R")):  # L is +X, R is -X (character faces -Y)
        # Legs: tall boots, charcoal trousers.
        prim("cube", (0.17 * s, -0.06, 0.15), (0.27, 0.44, 0.28), "leather", f"Foot_{side}", bevel=0.06)
        prim("cube", (0.17 * s, -0.06, 0.03), (0.29, 0.46, 0.06), "steel", f"Foot_{side}", bevel=0.02)
        limb((0.17 * s, 0, 0.14), (0.17 * s, 0, 0.52), 0.125, 0.145, "leather", f"Shin_{side}")
        prim("sphere", (0.17 * s, -0.12, 0.5), (0.1, 0.06, 0.1), "gold", f"Shin_{side}")  # knee plate
        limb((0.17 * s, 0, 0.52), (0.17 * s, 0, 0.98), 0.15, 0.14, "trousers", f"Thigh_{side}")

    # Greatcoat: flared skirt, gold hem, rear flap, torso, belt, collar.
    bpy.ops.mesh.primitive_cone_add(radius1=0.47 * S, radius2=0.33 * S, depth=0.42 * S, vertices=20)
    skirt = bpy.context.active_object
    skirt.location = v(0, 0, 0.8)
    skirt.scale = (1.0, 0.82, 1.0)
    finish(skirt, "coat", "Hips")
    torus((0, 0, 0.59), 0.47, 0.025, "gold", "Hips", squash_y=0.82)
    prim("cube", (0, 0.2, 0.74), (0.46, 0.07, 0.36), "coat_dark", "CoatTail", rot=(-10, 0, 0), bevel=0.02)
    prim("cube", (0, 0, 1.3), (0.64, 0.4, 0.52), "coat", "Spine", bevel=0.1)
    prim("cube", (0, 0, 1.06), (0.52, 0.34, 0.2), "coat", "Hips", bevel=0.05)
    prim("cyl", (0, 0, 1.0), (0.54, 0.4, 0.09), "leather", "Hips", bevel=0.01, verts=24)
    prim("cube", (0, -0.215, 1.0), (0.12, 0.03, 0.1), "gold", "Hips", bevel=0.01)  # buckle
    prim("cyl", (0, 0, 1.63), (0.4, 0.34, 0.16), "coat", "Spine", bevel=0.01)  # high collar
    prim("cube", (0, -0.21, 1.5), (0.13, 0.05, 0.2), "bone", "Spine", bevel=0.02)  # cravat
    for x in (-0.1, 0.1):  # double-breasted buttons
        for z in (1.14, 1.27, 1.4):
            prim("sphere", (x, -0.205, z), (0.055, 0.04, 0.055), "gold", "Spine", verts=10)
    # Bandolier strap across the chest, with pouches.
    prim("cube", (-0.02, -0.215, 1.3), (0.09, 0.03, 0.62), "leather", "Spine", rot=(0, 130, 0))
    prim("cube", (0.1, -0.235, 1.15), (0.1, 0.07, 0.1), "brass", "Spine", rot=(0, 130, 0), bevel=0.015)
    prim("cube", (-0.12, -0.235, 1.43), (0.1, 0.07, 0.1), "brass", "Spine", rot=(0, 130, 0), bevel=0.015)
    # Inquisitorial rosette over the heart.
    prim("cyl", (0.2, -0.22, 1.44), (0.15, 0.03, 0.15), "gold", "Spine", rot=(90, 0, 0), verts=24)
    prim("cyl", (0.2, -0.237, 1.44), (0.1, 0.02, 0.1), "wax", "Spine", rot=(90, 0, 0), verts=24)
    prim("sphere", (0.2, -0.25, 1.445), (0.055, 0.04, 0.055), "bone", "Spine", verts=12)
    # Cape.
    prim("cube", (0, 0.23, 1.28), (0.58, 0.05, 0.62), "coat_dark", "Spine", rot=(-6, 0, 0), bevel=0.02)
    # Belt trinkets: skull, parchment seal.
    prim("sphere", (-0.21, -0.2, 0.93), (0.13, 0.12, 0.13), "bone", "Hips", verts=14)
    prim("cube", (-0.21, -0.23, 0.87), (0.08, 0.05, 0.05), "bone", "Hips", bevel=0.01)
    prim("cube", (0.21, -0.21, 0.88), (0.07, 0.02, 0.2), "parchment", "Hips")
    prim("sphere", (0.21, -0.225, 0.97), (0.06, 0.04, 0.06), "wax", "Hips", verts=10)

    # Arms. Right (-X) is raised forward holding the pistol; left (+X) hangs holding the sword.
    arms = {
        "L": dict(sh=(0.38, 0, 1.45), el=(0.5, 0, 1.2), wr=(0.5, -0.05, 0.95)),
        "R": dict(sh=(-0.38, 0, 1.45), el=(-0.48, -0.08, 1.2), wr=(-0.4, -0.42, 1.22)),
    }
    for side, a in arms.items():
        limb(a["sh"], a["el"], 0.125, 0.105, "coat", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.22, 0.22, 0.22), "coat", f"UpperArm_{side}")
        limb(a["el"], a["wr"], 0.105, 0.09, "coat", f"Forearm_{side}")
        cuff_start = Vector(a["wr"]) + (Vector(a["el"]) - Vector(a["wr"])).normalized() * 0.08
        limb(tuple(cuff_start), a["wr"], 0.108, 0.108, "gold", f"Forearm_{side}")
        hand = Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.06
        prim("sphere", tuple(hand), (0.2, 0.2, 0.2), "leather", f"Hand_{side}")

    # Pauldrons: big gilded one on the left, small black one on the right.
    prim("sphere", (0.4, 0, 1.56), (0.42, 0.4, 0.3), "gold", "UpperArm_L")
    torus((0.4, 0, 1.56), 0.2, 0.02, "brass", "UpperArm_L")
    prim("sphere", (-0.4, 0, 1.55), (0.3, 0.3, 0.22), "leather", "UpperArm_R")

    # Head.
    prim("sphere", HEAD_C, (HEAD_R * 2,) * 3, "skin", "Head", verts=28)
    prim("sphere", (0, 0.045, HEAD_C[2] + 0.02), (0.5, 0.5, 0.5), "hair", "Head", verts=24)
    for x in (-0.245, 0.245):
        prim("sphere", (x, 0.0, 1.82), (0.06, 0.05, 0.09), "skin", "Head", verts=10)
    for x in (-0.09, 0.09):
        prim("sphere", (x, face_y(x, 1.84) - 0.002, 1.84), (0.06, 0.05, 0.08), "eye", "Head", verts=10)
        slant = 14 if x > 0 else -14
        prim("cube", (x, face_y(x, 1.9) - 0.005, 1.9), (0.1, 0.03, 0.03), "hair", "Head", rot=(0, slant, 0), bevel=0.008)
    prim("sphere", (0, face_y(0, 1.79) - 0.012, 1.79), (0.06, 0.06, 0.07), "skin", "Head", verts=10)  # nose
    prim("sphere", (0, face_y(0, 1.68) - 0.005, 1.68), (0.08, 0.06, 0.1), "hair", "Head", verts=10)  # goatee
    prim("cube", (0.1, face_y(0.1, 1.84) - 0.004, 1.84), (0.012, 0.012, 0.17), "scar", "Head", rot=(0, 25, 0))

    # Wide-brimmed inquisitor hat with a gold band and rosette.
    prim("cyl", (0, -0.01, 1.99), (0.92, 0.84, 0.04), "hat", "Head", rot=(5, 0, 0), bevel=0.012, verts=32)
    bpy.ops.mesh.primitive_cone_add(radius1=0.265 * S, radius2=0.22 * S, depth=0.2 * S, vertices=24)
    crown = bpy.context.active_object
    crown.location = v(0, 0, 2.09)
    finish(crown, "hat", "Head")
    prim("cyl", (0, 0, 2.02), (0.54, 0.54, 0.05), "gold", "Head", verts=24)
    prim("cyl", (0, -0.268, 2.03), (0.13, 0.03, 0.13), "gold", "Head", rot=(90, 0, 0), verts=20)
    prim("sphere", (0, -0.285, 2.03), (0.07, 0.04, 0.07), "bone", "Head", verts=10)

    build_pistol()
    build_sword()


def build_pistol():
    hand_r = "Hand_R"
    x, z = -0.4, 1.22
    prim("cube", (x, -0.45, z - 0.1), (0.075, 0.1, 0.2), "leather", hand_r, rot=(15, 0, 0), bevel=0.015)  # grip
    prim("cube", (x, -0.64, z + 0.06), (0.12, 0.44, 0.13), "steel", hand_r, bevel=0.02)  # body
    prim("cube", (x, -0.63, z + 0.14), (0.045, 0.3, 0.025), "gold", hand_r, bevel=0.005)  # filigree strip
    prim("cyl", (x, -0.93, z + 0.07), (0.075, 0.3, 0.075), "brass", hand_r, rot=(90, 0, 0), verts=14)  # barrel
    prim("cyl", (x, -1.06, z + 0.07), (0.1, 0.07, 0.1), "steel", hand_r, rot=(90, 0, 0), verts=14)  # muzzle brake
    prim("cube", (x, -0.62, z - 0.07), (0.065, 0.12, 0.22), "brass", hand_r, bevel=0.015)  # magazine


def build_sword():
    px, py, pz = 0.5, -0.09, 0.9
    xf = pivot_rotation((px, py, pz), (-40, 0, 0))  # tilt forward so the blade clears the floor
    prim("cyl", (px, py, pz - 0.02), (0.07, 0.07, 0.2), "leather", "Hand_L", xf=xf, verts=12)  # grip
    prim("sphere", (px, py, pz + 0.1), (0.1, 0.1, 0.1), "gold", "Hand_L", xf=xf, verts=12)  # pommel
    prim("cube", (px, py, pz - 0.14), (0.3, 0.06, 0.05), "gold", "Hand_L", xf=xf, bevel=0.012)  # guard
    prim("cube", (px, py, pz - 0.58), (0.11, 0.035, 0.8), "blade", "Hand_L", xf=xf)  # power blade
    bpy.ops.mesh.primitive_cone_add(radius1=0.0425 * S, radius2=0.0, depth=0.14 * S, vertices=4)
    tip = bpy.context.active_object
    tip.location = v(px, py, pz - 1.05)
    tip.rotation_euler = Euler((math.pi, 0, math.radians(45)))
    tip.scale = (1.0, 0.7, 1.0)
    finish(tip, "blade", "Hand_L", xf=xf)


# ---------------------------------------------------------------- rig
BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 0.95), (0, 0, 1.1)),
    ("Spine", "Hips", (0, 0, 1.1), (0, 0, 1.55)),
    ("Head", "Spine", (0, 0, 1.55), (0, 0, 2.05)),
    ("CoatTail", "Hips", (0, 0.2, 0.95), (0, 0.28, 0.45)),
    ("UpperArm_L", "Spine", (0.38, 0, 1.45), (0.5, 0, 1.2)),
    ("Forearm_L", "UpperArm_L", (0.5, 0, 1.2), (0.5, -0.05, 0.95)),
    ("Hand_L", "Forearm_L", (0.5, -0.05, 0.95), (0.5, -0.1, 0.82)),
    ("UpperArm_R", "Spine", (-0.38, 0, 1.45), (-0.48, -0.08, 1.2)),
    ("Forearm_R", "UpperArm_R", (-0.48, -0.08, 1.2), (-0.4, -0.42, 1.22)),
    ("Hand_R", "Forearm_R", (-0.4, -0.42, 1.22), (-0.4, -0.6, 1.22)),
    ("Muzzle_R", "Hand_R", (-0.4, -1.08, 1.29), (-0.4, -1.14, 1.29)),
]
for s, side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{side}", "Hips", (0.17 * s, 0, 0.98), (0.17 * s, 0, 0.52)),
        (f"Shin_{side}", f"Thigh_{side}", (0.17 * s, 0, 0.52), (0.17 * s, 0, 0.14)),
        (f"Foot_{side}", f"Shin_{side}", (0.17 * s, 0, 0.14), (0.17 * s, -0.22, 0.05)),
    ]


def build_armature():
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = bpy.context.active_object
    arm.name = "Armature"
    arm.data.name = "Armature"
    eb = arm.data.edit_bones
    for b in list(eb):
        eb.remove(b)
    for name, parent, head, tail in BONES:
        b = eb.new(name)
        b.head, b.tail = v(*head), v(*tail)
        if parent:
            b.parent = eb[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def assemble():
    build_body()
    arm = build_armature()

    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.active_object
    body.name = body.data.name = "Inquisitor"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")

    body.parent = arm
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return arm, body


def export(arm, body):
    os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="OFF",
        path_mode="AUTO",
    )
    print("Exported", OUT_FBX)


# ---------------------------------------------------------------- preview renders
def render_previews(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene

    bpy.ops.mesh.primitive_circle_add(radius=4, fill_type="NGON", location=(0, 0, -0.001))
    floor = bpy.context.active_object
    floor_mat = bpy.data.materials.new("floor")
    floor_mat.diffuse_color = (0.25, 0.26, 0.3, 1)
    floor_mat.use_nodes = True
    floor_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.26, 0.3, 1)
    floor.data.materials.append(floor_mat)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.52, 0.58, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world

    for name, energy, rot in (("key", 4.0, (50, 0, 35)), ("fill", 1.5, (60, 0, -120))):
        light = bpy.data.lights.new(name, "SUN")
        light.energy = energy
        lo = bpy.data.objects.new(name, light)
        lo.rotation_euler = Euler([math.radians(a) for a in rot])
        scene.collection.objects.link(lo)

    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.film_transparent = False

    def shot(name, pos, target, lens, res):
        cam.location = Vector(pos)
        cam_data.lens = lens
        cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.filepath = os.path.join(out_dir, name + ".png")
        bpy.ops.render.render(write_still=True)

    shot("front", (2.6, -4.2, 1.7), (0, 0, 1.0), 50, (640, 800))
    shot("back", (-2.4, 4.0, 1.7), (0, 0, 1.0), 50, (640, 800))
    d = 14.0  # game camera: pitch 55 deg, looking along the character's facing direction
    shot("game", (0, d * math.cos(math.radians(55)), 1 + d * math.sin(math.radians(55))), (0, 0, 1.0), 60, (800, 600))


arm, body = assemble()
export(arm, body)
if RENDER_DIR:
    render_previews(RENDER_DIR)
