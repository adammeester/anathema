"""Builds the Grey Knight Interceptor: chunky gunmetal power armour, storm bolter, Nemesis halberd.

Based on the reference images in Art/Reference (used as a guide only, nothing from them is imported).

    blender -b --python Art/grey_knight.py                    # writes Assets/Models/GreyKnight.fbx
    blender -b --python Art/grey_knight.py -- --render DIR    # also renders preview PNGs into DIR
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import limb, mat, pivot_rotation, prim, torus  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "GreyKnight.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(0.95)

mat("armor", (0.68, 0.72, 0.78), metallic=0.6, rough=0.3)
mat("armor_dark", (0.38, 0.41, 0.47), metallic=0.55, rough=0.4)
mat("steel", (0.8, 0.82, 0.85), metallic=0.65, rough=0.25)
mat("gold", (0.95, 0.68, 0.16), metallic=0.8, rough=0.3)
mat("brass", (0.55, 0.38, 0.16), metallic=0.75, rough=0.4)
mat("parchment", (0.86, 0.78, 0.6))
mat("wax", (0.65, 0.05, 0.05))
mat("white", (0.88, 0.88, 0.85))
mat("red", (0.55, 0.05, 0.06))
mat("cyan", (0.3, 0.85, 1.0), emit=(0.2, 0.8, 1.0))
mat("psy", (0.15, 0.4, 1.0), emit=(0.2, 0.5, 1.0), emit_strength=1.5)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.2 * s
        prim("cube", (x, -0.08, 0.11), (0.32, 0.52, 0.22), "armor", f"Foot_{side}", bevel=0.07)
        prim("cube", (x, -0.08, 0.02), (0.34, 0.54, 0.05), "armor_dark", f"Foot_{side}", bevel=0.01)
        limb((x, 0, 0.2), (x, 0, 0.6), 0.2, 0.17, "armor", f"Shin_{side}")
        # Greave: gold-trimmed plate, purity seal and wax.
        prim("cube", (x, -0.2, 0.4), (0.3, 0.04, 0.38), "gold", f"Shin_{side}", bevel=0.01)
        prim("cube", (x, -0.215, 0.4), (0.25, 0.05, 0.32), "armor_dark", f"Shin_{side}", bevel=0.02)
        prim("cube", (x, -0.25, 0.34), (0.1, 0.015, 0.24), "parchment", f"Shin_{side}")
        prim("sphere", (x, -0.26, 0.45), (0.06, 0.04, 0.06), "wax", f"Shin_{side}", verts=10)
        prim("sphere", (x, -0.1, 0.62), (0.32, 0.28, 0.28), "armor", f"Shin_{side}")  # knee
        limb((x, 0, 0.62), (x, 0, 1.02), 0.17, 0.19, "armor", f"Thigh_{side}")


def torso():
    prim("cube", (0, 0, 1.08), (0.68, 0.42, 0.3), "armor", "Hips", bevel=0.09)
    prim("cyl", (0, 0, 1.22), (0.72, 0.48, 0.08), "armor_dark", "Hips", bevel=0.01, verts=24)
    prim("cube", (0, -0.245, 1.22), (0.14, 0.03, 0.12), "gold", "Hips", bevel=0.01)
    prim("cube", (0, -0.235, 0.93), (0.26, 0.02, 0.5), "parchment", "Hips")  # tabard
    prim("cube", (0, -0.24, 0.69), (0.28, 0.03, 0.04), "gold", "Hips")
    prim("sphere", (0, -0.255, 1.08), (0.07, 0.04, 0.07), "wax", "Hips", verts=10)

    prim("cube", (0, 0, 1.52), (0.8, 0.5, 0.62), "armor", "Spine", bevel=0.13)
    prim("cube", (0, 0, 1.24), (0.82, 0.52, 0.05), "armor_dark", "Spine", bevel=0.01)
    prim("cube", (0, -0.268, 1.56), (0.17, 0.03, 0.17), "gold", "Spine", rot=(0, 45, 0), bevel=0.01)  # chest badge
    prim("sphere", (0, -0.285, 1.56), (0.07, 0.04, 0.07), "red", "Spine", verts=10)
    for x in (-0.22, 0.22):  # purity seals
        prim("cube", (x, -0.272, 1.36), (0.07, 0.015, 0.22), "parchment", "Spine")
        prim("sphere", (x, -0.282, 1.47), (0.05, 0.03, 0.05), "wax", "Spine", verts=10)
    prim("cyl", (0, 0, 1.86), (0.46, 0.4, 0.14), "armor", "Spine", bevel=0.01)  # gorget


def pauldrons():
    # Right: heraldry plate. Left: gold lettering.
    prim("sphere", (-0.62, 0, 1.74), (0.58, 0.52, 0.44), "armor", "UpperArm_R", verts=24)
    torus((-0.62, 0, 1.6), 0.27, 0.02, "gold", "UpperArm_R", squash_y=0.92)
    prim("sphere", (-0.86, 0, 1.74), (0.1, 0.38, 0.28), "white", "UpperArm_R", verts=14)
    prim("sphere", (-0.885, 0, 1.8), (0.06, 0.38, 0.12), "red", "UpperArm_R", verts=14)

    prim("sphere", (0.62, 0, 1.74), (0.58, 0.52, 0.44), "armor", "UpperArm_L", verts=24)
    torus((0.62, 0, 1.6), 0.27, 0.02, "gold", "UpperArm_L", squash_y=0.92)
    for z in (1.66, 1.74, 1.82):
        prim("cube", (0.86, 0, z), (0.03, 0.3, 0.04), "gold", "UpperArm_L")


def arms():
    arms_def = {
        "L": dict(sh=(0.55, 0, 1.65), el=(0.7, -0.05, 1.3), wr=(0.58, -0.3, 1.1)),
        "R": dict(sh=(-0.55, 0, 1.65), el=(-0.68, -0.1, 1.32), wr=(-0.55, -0.5, 1.38)),
    }
    from mathutils import Vector
    for side, a in arms_def.items():
        limb(a["sh"], a["el"], 0.15, 0.13, "armor", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.26, 0.26, 0.26), "armor", f"UpperArm_{side}")
        limb(a["el"], a["wr"], 0.14, 0.15, "armor", f"Forearm_{side}")
        prim("sphere", tuple(Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.05),
             (0.27, 0.27, 0.27), "armor_dark", f"Hand_{side}")


def head_and_pack():
    prim("sphere", (0, -0.02, 2.02), (0.42, 0.44, 0.44), "armor", "Head", verts=24)
    prim("cube", (0, -0.225, 2.03), (0.36, 0.05, 0.12), "armor_dark", "Head", bevel=0.02)  # visor
    for x in (-0.07, 0.07):
        prim("sphere", (x, -0.25, 2.03), (0.075, 0.03, 0.05), "cyan", "Head", verts=10)
    prim("cyl", (0, -0.2, 1.9), (0.12, 0.08, 0.12), "steel", "Head", rot=(90, 0, 0), verts=12)  # vox grille
    prim("cube", (0, 0, 2.25), (0.04, 0.36, 0.08), "gold", "Head", bevel=0.01)  # crest

    # Backpack with side vents, glowing core and the two cyan-tipped rods.
    prim("cube", (0, 0.3, 1.5), (0.56, 0.34, 0.56), "armor", "Backpack", bevel=0.07)
    for s in (-1, 1):
        prim("cyl", (0.33 * s, 0.3, 1.36), (0.24, 0.16, 0.24), "steel", "Backpack", rot=(0, 90, 0), verts=16)
        prim("cyl", (0.4 * s, 0.3, 1.36), (0.17, 0.05, 0.17), "armor_dark", "Backpack", rot=(0, 90, 0), verts=16)
        limb((0.17 * s, 0.3, 1.78), (0.36 * s, 0.18, 2.4), 0.05, 0.05, "steel", "Backpack")
        limb((0.33 * s, 0.2, 2.25), (0.36 * s, 0.18, 2.42), 0.062, 0.062, "cyan", "Backpack")
        for t in (0.25, 0.45, 0.65):
            px, py, pz = 0.17 * s + 0.19 * s * t, 0.3 - 0.12 * t, 1.78 + 0.62 * t
            prim("cyl", (px, py, pz), (0.1, 0.1, 0.03), "armor_dark", "Backpack", rot=(11, 0, 17 * s), verts=12)
    prim("cyl", (0, 0.485, 1.52), (0.18, 0.04, 0.18), "cyan", "Backpack", rot=(90, 0, 0), verts=20)


def storm_bolter():
    # Strapped to the outside of the right forearm (skinned to Forearm_R, not the hand), so the hand stays free to
    # grip the halberd during the sweep. Compact: it only pokes a little way past the wrist.
    x, z = -0.74, 1.6
    prim("cube", (x, -0.6, z), (0.2, 0.62, 0.22), "brass", "Forearm_R", bevel=0.03)
    prim("cube", (x, -0.58, z + 0.13), (0.06, 0.42, 0.04), "gold", "Forearm_R", bevel=0.005)
    prim("cube", (x - 0.13, -0.5, z - 0.06), (0.12, 0.3, 0.22), "armor_dark", "Forearm_R", bevel=0.02)  # ammo box
    for dx in (-0.055, 0.055):
        prim("cyl", (x + dx, -1.0, z + 0.02), (0.08, 0.34, 0.08), "armor_dark", "Forearm_R", rot=(90, 0, 0), verts=12)
    prim("cube", (x, -1.19, z + 0.02), (0.2, 0.06, 0.13), "steel", "Forearm_R", bevel=0.01)  # muzzle brake
    # Wrist bracer and strap that hold it on.
    prim("cyl", (-0.57, -0.44, 1.37), (0.33, 0.16, 0.33), "gold", "Forearm_R", rot=(90, 0, 0), verts=16)
    prim("cube", (-0.66, -0.4, 1.5), (0.26, 0.1, 0.2), "armor_dark", "Forearm_R", bevel=0.02)


def halberd():
    px, py, pz = 0.56, -0.34, 1.06  # left hand
    xf = pivot_rotation((px, py, pz), (0, 0, 0))  # kept upright: HalberdSweep re-aims the whole shaft
    prim("cyl", (px, py, pz + 0.45), (0.075, 0.075, 2.3), "armor_dark", "Halberd", xf=xf, verts=12)  # shaft
    prim("cyl", (px, py, pz - 0.72), (0.12, 0.12, 0.12), "gold", "Halberd", xf=xf, verts=12)  # butt
    for z in (0.2, 0.9):
        prim("cyl", (px, py, pz + z), (0.115, 0.115, 0.05), "gold", "Halberd", xf=xf, verts=12)
    prim("cube", (px, py, pz + 1.55), (0.26, 0.12, 0.1), "gold", "Halberd", xf=xf, bevel=0.02)  # guard
    prim("cube", (px + 0.14, py, pz + 1.5), (0.08, 0.06, 0.16), "gold", "Halberd", xf=xf, bevel=0.01)
    prim("cube", (px, py, pz + 1.95), (0.16, 0.04, 0.7), "psy", "Halberd", xf=xf)  # blade
    prim("cube", (px, py, pz + 2.37), (0.09, 0.035, 0.14), "psy", "Halberd", xf=xf, rot=(0, 0, 0))


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.0), (0, 0, 1.2)),
    ("Spine", "Hips", (0, 0, 1.2), (0, 0, 1.8)),
    ("Head", "Spine", (0, 0, 1.8), (0, 0, 2.2)),
    ("Backpack", "Spine", (0, 0.2, 1.7), (0, 0.4, 1.3)),
    ("UpperArm_L", "Spine", (0.55, 0, 1.65), (0.7, -0.05, 1.3)),
    ("Forearm_L", "UpperArm_L", (0.7, -0.05, 1.3), (0.58, -0.3, 1.1)),
    ("Hand_L", "Forearm_L", (0.58, -0.3, 1.1), (0.55, -0.42, 1.0)),
    ("Halberd", "Hand_L", (0.56, -0.34, 1.06), (0.56, -0.34, 1.4)),
    ("UpperArm_R", "Spine", (-0.55, 0, 1.65), (-0.68, -0.1, 1.32)),
    ("Forearm_R", "UpperArm_R", (-0.68, -0.1, 1.32), (-0.55, -0.5, 1.38)),
    ("Hand_R", "Forearm_R", (-0.55, -0.5, 1.38), (-0.55, -0.7, 1.4)),
    ("Muzzle_R", "Forearm_R", (-0.74, -1.22, 1.62), (-0.74, -1.28, 1.62)),
]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.2 * _s, 0, 1.0), (0.2 * _s, 0, 0.6)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.2 * _s, 0, 0.6), (0.2 * _s, 0, 0.2)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.2 * _s, 0, 0.2), (0.2 * _s, -0.3, 0.05)),
    ]

legs()
torso()
pauldrons()
arms()
head_and_pack()
storm_bolter()
halberd()

armature = c.build_armature(BONES)
mesh = c.assemble("GreyKnight", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.3, distance=1.15)
