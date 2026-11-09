"""Builds the Psyker: an unaugmented human sanctioned psyker, much smaller than the power-armoured marines. Deep grey
hood over a braided, cable-wired scalp, a breather collar with hoses, a heavy steel pauldron on the staff shoulder, a long
maroon coat open at the sides, a green sash and a winged-skull belt, pouches, and a tall aquila-and-eye staff.
One hand always crackles with cyan warp light.

Based on the reference artwork (hooded psyker with a staff, one hand outstretched; used as a guide only, nothing from
it is imported).

    blender -b --python Art/psyker.py                    # writes Assets/Models/Psyker.fbx
    blender -b --python Art/psyker.py -- --render DIR    # also renders preview PNGs into DIR

About 1.75 m tall (the marines stand ~2.4-3 m), same skeleton naming as the other characters (Hips, Spine, Head,
UpperArm_L, Thigh_L, ...) so ProceduralWalk and the arm poses work unchanged, plus a Staff_R bone on the right hand.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "Psyker.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(1.0)

mat("coat", (0.36, 0.05, 0.07), rough=0.85)
mat("coat_dark", (0.15, 0.02, 0.04), rough=0.9)
mat("hood", (0.13, 0.15, 0.21), rough=0.9)
mat("hood_dark", (0.05, 0.06, 0.09), rough=0.95)
mat("armor", (0.24, 0.3, 0.36), metallic=0.7, rough=0.4)
mat("armor_dark", (0.12, 0.15, 0.19), metallic=0.7, rough=0.5)
mat("brass", (0.72, 0.5, 0.2), metallic=0.9, rough=0.35)
mat("brass_dark", (0.4, 0.27, 0.1), metallic=0.85, rough=0.45)
mat("iron", (0.1, 0.1, 0.11), metallic=0.85, rough=0.45)
mat("skin", (0.42, 0.3, 0.25), rough=0.75)
mat("sash", (0.08, 0.3, 0.12), rough=0.9)
mat("leather", (0.17, 0.1, 0.07), rough=0.8)
mat("cloth", (0.6, 0.55, 0.42), rough=0.9)
mat("bone", (0.82, 0.78, 0.64), rough=0.7)
mat("psy", (0.35, 0.95, 1.0), emit=(0.25, 0.85, 1.0), emit_strength=6.0)
mat("psy_soft", (0.2, 0.7, 0.9), emit=(0.15, 0.6, 0.85), emit_strength=2.0)
mat("eye", (0.6, 1.0, 1.0), emit=(0.4, 0.95, 1.0), emit_strength=8.0)


def rivets(points, size, material, bone):
    for p in points:
        prim("sphere", p, (size, size, size), material, bone, verts=8)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.1 * s
        prim("cube", (x, -0.06, 0.06), (0.12, 0.3, 0.12), "leather", f"Foot_{side}", bevel=0.03)  # boot
        prim("cube", (x, -0.06, 0.005), (0.13, 0.31, 0.02), "iron", f"Foot_{side}", bevel=0.005)
        limb((x, 0, 0.1), (x, 0, 0.46), 0.075, 0.065, "leather", f"Shin_{side}")
        torus((x, 0, 0.42), 0.075, 0.012, "brass_dark", f"Shin_{side}")
        prim("sphere", (x, -0.04, 0.48), (0.12, 0.12, 0.1), "armor_dark", f"Shin_{side}", verts=10)  # knee
        limb((x, 0, 0.47), (x, 0, 0.92), 0.075, 0.095, "coat_dark", f"Thigh_{side}")
    # Satchel pouches on the belt.
    for s in (-1, 1):
        prim("cube", (0.21 * s, 0.0, 0.92), (0.07, 0.14, 0.12), "leather", "Hips", bevel=0.015)
        prim("cube", (0.21 * s, -0.045, 0.96), (0.075, 0.05, 0.05), "brass_dark", "Hips", bevel=0.008)


def torso():
    prim("cube", (0, 0, 1.0), (0.38, 0.24, 0.18), "coat", "Hips", bevel=0.05)
    prim("cube", (0, 0, 1.2), (0.44, 0.27, 0.36), "coat", "Spine", bevel=0.07)
    prim("cube", (0, -0.14, 1.22), (0.4, 0.03, 0.4), "coat_dark", "Spine", bevel=0.01)  # coat front placket
    # Chest harness with a brass aquila medallion and buttons.
    prim("cube", (0, -0.145, 1.3), (0.16, 0.03, 0.1), "armor_dark", "Spine", bevel=0.01)
    prim("sphere", (0, -0.165, 1.3), (0.07, 0.03, 0.07), "brass", "Spine", verts=10)
    rivets([(0, -0.16, 1.15 - 0.07 * i) for i in range(4)], 0.022, "brass", "Spine")
    # Diagonal leather strap and a small control box on the chest.
    prim("cube", (0.02, -0.16, 1.2), (0.045, 0.02, 0.5), "leather", "Spine", rot=(0, 28, 0), bevel=0.005)
    prim("cube", (-0.1, -0.16, 1.22), (0.1, 0.04, 0.12), "armor_dark", "Spine", bevel=0.01)
    rivets([(-0.12, -0.185, 1.25), (-0.09, -0.185, 1.25), (-0.12, -0.185, 1.2), (-0.09, -0.185, 1.2)], 0.014, "psy_soft", "Spine")
    # Belt, winged skull buckle and a green sash with long hanging tails.
    prim("cyl", (0, 0, 0.99), (0.4, 0.27, 0.07), "leather", "Hips", verts=20)
    prim("cube", (0, -0.14, 0.99), (0.16, 0.025, 0.07), "brass", "Hips", bevel=0.008)
    prim("sphere", (0, -0.16, 0.99), (0.06, 0.03, 0.06), "bone", "Hips", verts=10)
    prim("cyl", (0, 0, 1.06), (0.42, 0.29, 0.07), "sash", "Hips", verts=20)
    flat_poly([(0.14, 1.06), (0.26, 1.04), (0.28, 0.7), (0.24, 0.6), (0.2, 0.68), (0.15, 0.58), (0.12, 0.74)],
              -0.15, 0.02, "sash", "Hips")
    # Breather collar and gorget: a ribbed mask piece under the chin with a grille.
    prim("cyl", (0, 0, 1.44), (0.3, 0.25, 0.1), "armor_dark", "Spine", verts=20)
    prim("cube", (0, -0.12, 1.43), (0.18, 0.06, 0.1), "armor", "Spine", bevel=0.015)
    for i in range(4):
        prim("cube", (-0.045 + 0.03 * i, -0.155, 1.43), (0.01, 0.015, 0.08), "iron", "Spine")
    torus((0, 0, 1.49), 0.14, 0.014, "brass", "Spine", squash_y=0.9)
    for s in (-1, 1):  # hoses from the collar down to the chest and back
        limb((0.11 * s, -0.08, 1.44), (0.2 * s, -0.12, 1.22), 0.016, 0.016, "iron", "Spine", verts=8)
        limb((0.2 * s, -0.12, 1.22), (0.22 * s, -0.05, 1.0), 0.016, 0.016, "brass_dark", "Spine", verts=8)
    # Skull trophy on the left hip.
    prim("sphere", (0.19, -0.06, 0.82), (0.07, 0.075, 0.07), "bone", "Hips", verts=10)
    prim("sphere", (0.19, -0.1, 0.83), (0.05, 0.03, 0.04), "iron", "Hips", verts=8)

    # Long coat: front and back panels with ragged hems, open at the sides so the legs can swing.
    front = [(-0.2, 1.0), (0.2, 1.0), (0.22, 0.7), (0.2, 0.46), (0.14, 0.36), (0.08, 0.46), (0.0, 0.34),
             (-0.08, 0.46), (-0.14, 0.36), (-0.2, 0.46), (-0.22, 0.7)]
    flat_poly(front, -0.14, 0.02, "coat", "Hips")
    back = [(-0.22, 1.0), (0.22, 1.0), (0.25, 0.68), (0.22, 0.38), (0.15, 0.2), (0.08, 0.34), (0.0, 0.16),
            (-0.08, 0.34), (-0.15, 0.2), (-0.22, 0.38), (-0.25, 0.68)]
    flat_poly(back, 0.14, 0.02, "coat", "Hips")
    flat_poly([(x * 0.9, z + 0.02) for x, z in back], 0.155, 0.01, "coat_dark", "Hips")
    for x in (-0.12, 0.0, 0.12):  # folds
        prim("cube", (x, 0.16, 0.62), (0.025, 0.02, 0.5), "coat_dark", "Hips")
    # Paper scroll tags hanging from the belt (purity seals).
    for x, z in ((-0.14, 0.64), (0.0, 0.58), (0.1, 0.66)):
        prim("cube", (x, -0.155, z), (0.05, 0.008, 0.16), "cloth", "Hips")
    prim("sphere", (-0.14, -0.16, 0.74), (0.025, 0.012, 0.025), "psy_soft", "Hips", verts=8)


def head_and_hood():
    # Face: gaunt and lined, with glowing eyes.
    prim("sphere", (0, -0.05, 1.66), (0.17, 0.2, 0.22), "skin", "Head", verts=16)
    prim("cube", (0, -0.15, 1.63), (0.03, 0.04, 0.06), "skin", "Head", bevel=0.008)  # nose
    for x in (-0.04, 0.04):
        prim("sphere", (x, -0.14, 1.69), (0.04, 0.02, 0.025), "eye", "Head", verts=10)
    prim("cube", (0, -0.15, 1.57), (0.08, 0.02, 0.025), "hood_dark", "Head", bevel=0.005)  # grim mouth
    # Hood: deep cowl with a loose tail down the back and a rolled front edge around the face.
    prim("sphere", (0, 0.08, 1.72), (0.3, 0.3, 0.34), "hood", "Head", verts=24)
    limb((0, 0.16, 1.78), (0, 0.24, 1.46), 0.13, 0.04, "hood", "Head", verts=14)
    torus((0, -0.12, 1.7), 0.115, 0.028, "hood", "Head", rot=(90, 0, 0))
    prim("cyl", (0, 0.0, 1.7), (0.22, 0.22, 0.03), "hood_dark", "Head", rot=(90, 0, 0), verts=18)
    for s in (-1, 1):  # side drapes onto the shoulders
        limb((0.14 * s, 0, 1.64), (0.2 * s, -0.04, 1.46), 0.09, 0.05, "hood", "Head", verts=12)
    # Cowl over the shoulders.
    prim("sphere", (0, 0.02, 1.47), (0.6, 0.38, 0.14), "hood", "Spine", verts=20)
    # Braided cables trailing from the scalp out of the hood, with a couple of brass ferrules.
    for s in (-1, 1):
        a, b, d = (0.12 * s, -0.08, 1.6), (0.19 * s, -0.12, 1.42), (0.2 * s, -0.1, 1.28)
        limb(a, b, 0.012, 0.012, "iron", "Head", verts=6)
        limb(b, d, 0.012, 0.012, "iron", "Head", verts=6)
        prim("cyl", d, (0.03, 0.03, 0.03), "brass", "Head", verts=8)
    prim("cyl", (0.1, 0.12, 1.77), (0.025, 0.025, 0.2), "iron", "Head", rot=(30, 0, 0), verts=8)  # rear socket cable


def pauldrons():
    # Right shoulder (staff arm): a big steel pauldron with brass trim, as in the artwork. Left: a small plate.
    x = -0.34
    prim("sphere", (x, 0, 1.5), (0.3, 0.28, 0.2), "armor", "UpperArm_R", verts=20)
    prim("sphere", (x - 0.04, -0.02, 1.57), (0.2, 0.18, 0.1), "armor_dark", "UpperArm_R", verts=12)
    torus((x, 0, 1.4), 0.14, 0.012, "brass", "UpperArm_R", squash_y=0.9)
    rivets([(x - 0.12, -0.1, 1.52), (x + 0.04, -0.12, 1.54), (x - 0.12, 0.1, 1.52)], 0.02, "brass", "UpperArm_R")
    # Chain dangling from the right shoulder, and a hanging seal.
    for i in range(5):
        prim("cube", (x - 0.06, 0.1, 1.38 - 0.04 * i), (0.016, 0.03, 0.03), "iron", "UpperArm_R", rot=(0, 0, 20 * (i % 2)))
    prim("cube", (x - 0.06, 0.1, 1.14), (0.04, 0.012, 0.09), "cloth", "UpperArm_R")
    x = 0.33
    prim("sphere", (x, 0, 1.5), (0.22, 0.22, 0.14), "armor_dark", "UpperArm_L", verts=16)
    torus((x, 0, 1.44), 0.1, 0.01, "brass_dark", "UpperArm_L", squash_y=0.9)


ARMS = {
    "L": dict(sh=(0.27, 0, 1.4), el=(0.46, -0.12, 1.2), wr=(0.52, -0.42, 1.18)),
    "R": dict(sh=(-0.27, 0, 1.4), el=(-0.36, -0.08, 1.12), wr=(-0.4, -0.22, 0.98)),
}


def arms():
    for side, a in ARMS.items():
        s = 1 if side == "L" else -1
        limb(a["sh"], a["el"], 0.06, 0.05, "coat", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.1, 0.1, 0.1), "coat_dark", f"UpperArm_{side}", verts=10)
        limb(a["el"], a["wr"], 0.05, 0.045, "coat", f"Forearm_{side}")
        # Armoured vambrace on the forearm.
        mid = tuple((Vector(a["el"]) * 0.5 + Vector(a["wr"]) * 0.5))
        limb(tuple(Vector(a["el"]) * 0.4 + Vector(a["wr"]) * 0.6), tuple(Vector(a["el"]) * 0.05 + Vector(a["wr"]) * 0.95),
             0.058, 0.058, "armor", f"Forearm_{side}", verts=12)
        torus(tuple(Vector(a["el"]) * 0.3 + Vector(a["wr"]) * 0.7), 0.06, 0.008, "brass", f"Forearm_{side}")
    wr = Vector(ARMS["L"]["wr"])
    d = (wr - Vector(ARMS["L"]["el"])).normalized()
    # Left hand: open, palm out, wreathed in warp light.
    palm = wr + d * 0.05
    prim("sphere", tuple(palm), (0.09, 0.09, 0.09), "skin", "Hand_L", verts=12)
    for i, ang in enumerate((-24, -8, 8, 24)):
        f = wr + d * 0.1 + Vector((math.sin(math.radians(ang)) * 0.05, 0, 0.0))
        tip = f + d * 0.08 + Vector((math.sin(math.radians(ang)) * 0.03, 0, 0.0))
        limb(tuple(f), tuple(tip), 0.012, 0.008, "skin", "Hand_L", verts=6)
    prim("sphere", tuple(palm + d * 0.1), (0.14, 0.14, 0.14), "psy_soft", "Hand_L", verts=14)
    prim("sphere", tuple(palm + d * 0.1), (0.07, 0.07, 0.07), "psy", "Hand_L", verts=12)
    for k in range(5):  # jagged little arcs of lightning
        a = k * 72
        tip = palm + d * 0.12 + Vector((math.cos(math.radians(a)) * 0.11, 0.02, math.sin(math.radians(a)) * 0.11))
        limb(tuple(palm + d * 0.1), tuple(tip), 0.012, 0.0, "psy", "Hand_L", verts=5)
    # Right hand: gloved fist on the staff, faintly glowing.
    wr = Vector(ARMS["R"]["wr"])
    d = (wr - Vector(ARMS["R"]["el"])).normalized()
    prim("sphere", tuple(wr + d * 0.05), (0.1, 0.1, 0.1), "psy_soft", "Hand_R", verts=12)


STAFF_X, STAFF_Y = -0.4, -0.26


def staff():
    """Tall staff held upright in the right hand: rings, a caged power coil with a hose, an eye, and a brass aquila."""
    bone = "Staff_R"
    x, y = STAFF_X, STAFF_Y
    limb((x, y, -0.06), (x, y, 1.96), 0.017, 0.017, "armor_dark", bone, verts=10)  # shaft
    prim("cyl", (x, y, -0.03), (0.04, 0.04, 0.08), "brass_dark", bone, verts=10)  # butt
    for z in (0.6, 0.98, 1.4):
        torus((x, y, z), 0.026, 0.008, "brass", bone)
    # Caged power coil and hose near the top.
    prim("cyl", (x, y, 1.62), (0.06, 0.06, 0.2), "iron", bone, verts=10)
    for a in range(4):
        px = x + math.cos(math.radians(a * 90 + 45)) * 0.034
        py = y + math.sin(math.radians(a * 90 + 45)) * 0.034
        prim("cyl", (px, py, 1.62), (0.008, 0.008, 0.24), "brass", bone, verts=6)
    prim("sphere", (x, y, 1.62), (0.05, 0.05, 0.1), "psy_soft", bone, verts=10)
    limb((x, y, 1.72), (x + 0.1, y + 0.02, 1.68), 0.01, 0.01, "iron", bone, verts=6)
    limb((x + 0.1, y + 0.02, 1.68), (x + 0.11, y + 0.02, 1.5), 0.01, 0.01, "iron", bone, verts=6)
    # Eye housing and the aquila above it.
    prim("cyl", (x, y, 1.78), (0.08, 0.04, 0.08), "brass", bone, rot=(90, 0, 0), verts=14)
    prim("sphere", (x, y - 0.025, 1.78), (0.05, 0.03, 0.05), "eye", bone, verts=10)
    prim("cube", (x, y, 1.86), (0.06, 0.03, 0.07), "brass_dark", bone, bevel=0.008)
    for s in (-1, 1):
        wing = [(0.0, 0.0), (0.09 * s, 0.03), (0.2 * s, 0.1), (0.24 * s, 0.07), (0.19 * s, 0.025), (0.22 * s, 0.0),
                (0.15 * s, -0.025), (0.08 * s, -0.03)]
        flat_poly([(x + px, 1.94 + pz) for px, pz in wing], y, 0.014, "brass", bone)
    prim("cube", (x, y, 1.98), (0.045, 0.02, 0.09), "brass", bone, bevel=0.006)
    prim("sphere", (x, y, 2.05), (0.045, 0.04, 0.05), "brass", bone, verts=8)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.1)),
    ("Hips", "Root", (0, 0, 0.95), (0, 0, 1.05)),
    ("Spine", "Hips", (0, 0, 1.05), (0, 0, 1.45)),
    ("Head", "Spine", (0, 0, 1.5), (0, 0, 1.85)),
]
for _side, _a in ARMS.items():
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.1)),
    ]
BONES.append(("Staff_R", "Hand_R", (STAFF_X, STAFF_Y, 0.9), (STAFF_X, STAFF_Y, 1.2)))
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.1 * _s, 0, 0.95), (0.1 * _s, 0, 0.47)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.1 * _s, 0, 0.47), (0.1 * _s, 0, 0.1)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.1 * _s, 0, 0.1), (0.1 * _s, -0.2, 0.03)),
    ]

legs()
torso()
head_and_hood()
pauldrons()
arms()
staff()

armature = c.build_armature(BONES)
mesh = c.assemble("Psyker", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.0, distance=0.7)
