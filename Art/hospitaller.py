"""Builds the Hospitaller: a battle-sister healer of the Orders Hospitaller. Black power armour edged in steel, a white
veil under a red hood, a white-and-red pleated robe over the armour with a long red cloak, a gold sunburst icon raised in
her right hand and an open book of litanies held in front of her in the left. A chirurgeon servo-arm with a syringe
curls up over her left shoulder from the power pack. The doves that circle her are built at runtime (HospitallerDoves).

Based on the reference miniature (Sister Hospitaller with book, icon, servo-arm and doves; used as a guide only, nothing
from it is imported).

    blender -b --python Art/hospitaller.py                    # writes Assets/Models/Hospitaller.fbx
    blender -b --python Art/hospitaller.py -- --render DIR    # also renders preview PNGs into DIR

About 1.85 m tall (a little taller than the Psyker thanks to the armour), same skeleton naming as the other characters
(Hips, Spine, Head, UpperArm_L, Thigh_L, ...) so ProceduralWalk and the arm poses work unchanged.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "Hospitaller.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(1.0)

mat("armor", (0.05, 0.05, 0.06), metallic=0.6, rough=0.35)
mat("armor_edge", (0.55, 0.57, 0.62), metallic=0.85, rough=0.3)
mat("robe", (0.86, 0.85, 0.82), rough=0.85)
mat("robe_shade", (0.62, 0.62, 0.62), rough=0.9)
mat("red", (0.55, 0.04, 0.04), rough=0.75)
mat("red_dark", (0.3, 0.02, 0.02), rough=0.85)
mat("gold", (0.85, 0.6, 0.18), metallic=0.95, rough=0.3)
mat("gold_dark", (0.5, 0.33, 0.08), metallic=0.9, rough=0.4)
mat("steel", (0.45, 0.46, 0.5), metallic=0.9, rough=0.35)
mat("skin", (0.78, 0.6, 0.5), rough=0.7)
mat("lips", (0.55, 0.25, 0.22), rough=0.6)
mat("eye", (0.08, 0.06, 0.05), rough=0.4)
mat("page", (0.92, 0.86, 0.7), rough=0.9)
mat("ink", (0.12, 0.08, 0.06), rough=0.9)
mat("leather", (0.32, 0.06, 0.05), rough=0.7)
mat("lens", (0.4, 0.75, 1.0), emit=(0.3, 0.7, 1.0), emit_strength=3.0)
mat("holy", (1.0, 0.85, 0.45), emit=(1.0, 0.8, 0.35), emit_strength=3.5)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.1 * s
        prim("cube", (x, -0.07, 0.07), (0.13, 0.3, 0.14), "armor", f"Foot_{side}", bevel=0.035)  # sabaton
        prim("cube", (x, -0.07, 0.008), (0.14, 0.31, 0.02), "armor_edge", f"Foot_{side}", bevel=0.006)
        limb((x, 0, 0.1), (x, 0, 0.47), 0.07, 0.08, "armor", f"Shin_{side}")  # greave
        prim("cube", (x, -0.07, 0.3), (0.1, 0.03, 0.3), "armor", f"Shin_{side}", bevel=0.012)  # shin plate
        prim("cube", (x, -0.088, 0.3), (0.02, 0.01, 0.26), "armor_edge", f"Shin_{side}")
        prim("sphere", (x, -0.05, 0.5), (0.13, 0.12, 0.12), "armor", f"Shin_{side}", verts=12)  # knee
        torus((x, -0.05, 0.5), 0.06, 0.01, "armor_edge", f"Shin_{side}", rot=(90, 0, 0))
        limb((x, 0, 0.5), (x, 0, 0.92), 0.08, 0.1, "armor", f"Thigh_{side}")


def torso():
    # Armoured waist and a fitted breastplate.
    prim("cube", (0, 0, 1.0), (0.36, 0.24, 0.18), "armor", "Hips", bevel=0.06)
    prim("cube", (0, 0, 1.2), (0.38, 0.25, 0.3), "armor", "Spine", bevel=0.08)
    for x in (-0.075, 0.075):
        prim("sphere", (x, -0.1, 1.27), (0.15, 0.1, 0.14), "armor", "Spine", verts=16)
    prim("cube", (0, 0, 1.36), (0.42, 0.26, 0.14), "armor", "Spine", bevel=0.05)  # collar block
    torus((0, 0, 1.43), 0.11, 0.022, "armor_edge", "Spine", squash_y=0.9)  # gorget ring
    # Gold fleur-de-lis on the sternum and a steel cord with a small medallion.
    fleur((0, -0.16, 1.24), 0.09, "gold", "Spine", -0.17)
    # Belt with a gold buckle; a fleur on the buckle.
    prim("cyl", (0, 0, 1.06), (0.38, 0.27, 0.06), "leather", "Hips", verts=20)
    prim("cube", (0, -0.14, 1.06), (0.1, 0.02, 0.07), "gold", "Hips", bevel=0.008)

    # Robe: a white front tabard with red pleats on both sides, gold fleur low down, falling between the legs.
    tabard = [(-0.13, 1.04), (0.13, 1.04), (0.15, 0.6), (0.13, 0.2), (0.0, 0.14), (-0.13, 0.2), (-0.15, 0.6)]
    flat_poly(tabard, -0.15, 0.02, "robe", "Hips")
    for x in (-0.1, -0.04, 0.04, 0.1):  # pleat shading
        prim("cube", (x, -0.162, 0.6), (0.012, 0.006, 0.7), "robe_shade", "Hips")
    fleur((0, -0.168, 0.36), 0.07, "gold", "Hips", -0.17)
    for s in (-1, 1):
        pleat = [(0.13 * s, 1.04), (0.24 * s, 1.0), (0.27 * s, 0.6), (0.24 * s, 0.32), (0.18 * s, 0.24), (0.14 * s, 0.4)]
        flat_poly(pleat, -0.13, 0.02, "red", "Hips")
        flat_poly([(px * 1.03, pz - 0.05) for px, pz in pleat[2:]] + [(0.15 * s, 0.6)], -0.12, 0.015, "robe", "Hips")
    # Short hip plates (faulds) over the robe at the sides.
    for s in (-1, 1):
        prim("cube", (0.2 * s, -0.02, 0.95), (0.12, 0.24, 0.16), "armor", "Hips", rot=(0, 12 * s, 0), bevel=0.03)
        prim("cube", (0.2 * s, -0.02, 0.87), (0.125, 0.245, 0.015), "armor_edge", "Hips", rot=(0, 12 * s, 0))

    # Long cloak at the back: red outside, white lining showing at the sides; ragged-free, straight hem.
    back = [(-0.25, 1.38), (0.25, 1.38), (0.33, 0.9), (0.36, 0.4), (0.3, 0.12), (0.0, 0.08), (-0.3, 0.12), (-0.36, 0.4),
            (-0.33, 0.9)]
    flat_poly(back, 0.16, 0.025, "red", "Spine")
    flat_poly([(x * 0.96, z + 0.015) for x, z in back], 0.14, 0.012, "robe", "Spine")
    for x in (-0.18, -0.06, 0.06, 0.18):  # folds
        prim("cube", (x, 0.18, 0.6), (0.03, 0.02, 0.8), "red_dark", "Spine")
    # Cloak edges sweeping round to the front at the shoulders.
    for s in (-1, 1):
        limb((0.24 * s, 0.05, 1.4), (0.3 * s, 0.12, 0.5), 0.05, 0.08, "red", "Spine", verts=10)

    # Power pack.
    prim("cube", (0, 0.2, 1.28), (0.3, 0.16, 0.26), "armor", "Spine", bevel=0.04)
    for s in (-1, 1):
        prim("cyl", (0.1 * s, 0.24, 1.44), (0.06, 0.06, 0.08), "armor_edge", "Spine", verts=10)
    fleur((0, 0.285, 1.28), 0.07, "gold", "Spine", 0.29)


def fleur(center, size, material, bone, y):
    """A small fleur-de-lis drawn in the XZ plane at depth y."""
    cx, _, cz = center
    k = size
    petal = [(0.0, 0.5), (0.12, 0.25), (0.08, 0.0), (-0.08, 0.0), (-0.12, 0.25)]
    flat_poly([(cx + px * k * 2, cz + pz * k * 2) for px, pz in petal], y, 0.012, material, bone)
    for s in (-1, 1):
        side = [(0.06 * s, 0.05), (0.3 * s, 0.35), (0.38 * s, 0.15), (0.2 * s, -0.08)]
        flat_poly([(cx + px * k * 2, cz + pz * k * 2) for px, pz in side], y, 0.012, material, bone)
    prim("cube", (cx, y, cz - 0.02 * k * 2), (0.42 * k * 2, 0.014, 0.08 * k * 2), material, bone)
    flat_poly([(cx + px * k * 2, cz + pz * k * 2) for px, pz in [(-0.06, -0.06), (0.06, -0.06), (0.0, -0.3)]], y, 0.012,
              material, bone)


def head_and_veil():
    # Face.
    prim("sphere", (0, -0.03, 1.62), (0.15, 0.17, 0.2), "skin", "Head", verts=16)
    prim("cube", (0, -0.115, 1.6), (0.025, 0.03, 0.05), "skin", "Head", bevel=0.008)  # nose
    for x in (-0.035, 0.035):
        prim("sphere", (x, -0.105, 1.645), (0.03, 0.015, 0.016), "eye", "Head", verts=8)
    prim("cube", (0, -0.112, 1.55), (0.045, 0.012, 0.014), "lips", "Head", bevel=0.004)
    # White wimple framing the face and falling onto the shoulders.
    torus((0, -0.07, 1.62), 0.1, 0.03, "robe", "Head", rot=(90, 0, 0))
    prim("sphere", (0, 0.04, 1.66), (0.26, 0.26, 0.3), "robe", "Head", verts=20)
    limb((0, 0.06, 1.56), (0, 0.1, 1.4), 0.12, 0.17, "robe", "Head", verts=16)
    # Red hood over the veil, with a white turned-back edge and a gold fleur on the brow.
    prim("sphere", (0, 0.07, 1.7), (0.3, 0.3, 0.32), "red", "Head", verts=20)
    torus((0, -0.06, 1.68), 0.13, 0.024, "robe", "Head", rot=(80, 0, 0))
    limb((0, 0.14, 1.72), (0, 0.2, 1.42), 0.13, 0.07, "red", "Head", verts=14)
    fleur((0, -0.14, 1.77), 0.045, "gold", "Head", -0.16)
    for s in (-1, 1):  # hood sides draping onto the shoulders
        limb((0.13 * s, 0.0, 1.62), (0.2 * s, 0.02, 1.42), 0.08, 0.06, "red", "Head", verts=12)


def pauldrons():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.29 * s
        c.clipped_ellipsoid((x, 0, 1.43), (0.22, 0.24, 0.22), -0.07, None, "armor", f"UpperArm_{side}", rot=(0, 14 * s, 0))
        c.clipped_ellipsoid((x, 0, 1.43), (0.23, 0.25, 0.23), -0.08, -0.05, "armor_edge", f"UpperArm_{side}",
                            rot=(0, 14 * s, 0))
        fleur((x + 0.02 * s, -0.11, 1.44), 0.035, "gold", f"UpperArm_{side}", -0.115)


# Left arm holds the open book in front of the chest; right arm raises the icon.
ARMS = {
    "L": dict(sh=(0.25, 0, 1.38), el=(0.33, -0.08, 1.14), wr=(0.18, -0.34, 1.16)),
    "R": dict(sh=(-0.25, 0, 1.38), el=(-0.36, -0.06, 1.13), wr=(-0.38, -0.3, 1.2)),
}


def arms():
    for side, a in ARMS.items():
        limb(a["sh"], a["el"], 0.065, 0.055, "armor", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.11, 0.11, 0.11), "armor", f"UpperArm_{side}", verts=12)
        torus(a["el"], 0.05, 0.01, "armor_edge", f"UpperArm_{side}")
        limb(a["el"], a["wr"], 0.06, 0.052, "armor", f"Forearm_{side}")
        # Flared gauntlet cuff, edged in steel, with a hanging red sleeve.
        cuff = tuple(Vector(a["el"]) * 0.25 + Vector(a["wr"]) * 0.75)
        limb(cuff, a["wr"], 0.075, 0.06, "armor", f"Forearm_{side}", verts=14)
        torus(cuff, 0.075, 0.01, "armor_edge", f"Forearm_{side}")
        prim("sphere", tuple(Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.05),
             (0.09, 0.09, 0.09), "armor", f"Hand_{side}", verts=12)


def book():
    """Open book in the left hand, tilted up toward her face: red covers, two page blocks, a gold ribbon."""
    bone = "Hand_L"
    wr = Vector(ARMS["L"]["wr"])
    cx, cy, cz = wr.x - 0.04, wr.y - 0.06, wr.z + 0.04
    tilt = 35  # the far edge raised so the pages face her
    for s in (-1, 1):
        rot = (tilt, -10 * s, 0)
        prim("cube", (cx + 0.11 * s, cy, cz), (0.22, 0.26, 0.016), "leather", bone, rot=rot, bevel=0.004)
        prim("cube", (cx + 0.105 * s, cy, cz + 0.02), (0.2, 0.24, 0.03), "page", bone, rot=rot, bevel=0.006)
        for i in range(5):  # lines of text
            prim("cube", (cx + 0.105 * s, cy - 0.08 + 0.035 * i, cz + 0.039 + 0.02 * i), (0.15, 0.008, 0.002), "ink",
                 bone, rot=rot)
    prim("cyl", (cx, cy, cz + 0.01), (0.03, 0.03, 0.27), "leather", bone, rot=(90 - tilt, 0, 0), verts=10)  # spine
    prim("cube", (cx + 0.02, cy - 0.16, cz - 0.1), (0.015, 0.004, 0.2), "gold", bone, rot=(-10, 0, 0))  # ribbon
    prim("cube", (cx - 0.15, cy - 0.12, cz - 0.07), (0.03, 0.006, 0.05), "gold", bone)  # clasp


def icon():
    """Gold sunburst monstrance raised in the right hand: a short haft, a spiked ring and a glowing core."""
    bone = "Hand_R"
    wr = Vector(ARMS["R"]["wr"])
    x, y = wr.x - 0.01, wr.y - 0.03
    limb((x, y, wr.z - 0.1), (x, y, wr.z + 0.3), 0.022, 0.022, "gold_dark", bone, verts=10)
    prim("sphere", (x, y, wr.z - 0.11), (0.05, 0.05, 0.05), "gold", bone, verts=10)
    prim("cube", (x, y, wr.z + 0.32), (0.07, 0.04, 0.05), "gold", bone, bevel=0.01)
    zc = wr.z + 0.47
    torus((x, y, zc), 0.1, 0.018, "gold", bone, rot=(90, 0, 0))
    prim("cyl", (x, y, zc), (0.13, 0.13, 0.02), "holy", bone, rot=(90, 0, 0), verts=16)
    prim("sphere", (x, y - 0.02, zc), (0.06, 0.04, 0.08), "gold_dark", bone, verts=10)  # little saint figure
    for k in range(8):
        a = math.radians(k * 45)
        tip = (x + math.cos(a) * 0.2, y, zc + math.sin(a) * 0.2)
        limb((x + math.cos(a) * 0.1, y, zc + math.sin(a) * 0.1), tip, 0.022, 0.0, "gold", bone, verts=6)


def servo_arm():
    """Chirurgeon arm rising from the left of the power pack, arching over the shoulder with a syringe and a lens."""
    bone = "Spine"
    pts = [(0.12, 0.26, 1.38), (0.22, 0.3, 1.66), (0.3, 0.2, 1.86), (0.4, 0.04, 1.84), (0.46, -0.06, 1.66)]
    for a, b in zip(pts, pts[1:]):
        limb(a, b, 0.028, 0.024, "steel", bone, verts=10)
        prim("sphere", b, (0.06, 0.06, 0.06), "armor", bone, verts=10)
    tip = Vector(pts[-1])
    limb(tuple(tip), tuple(tip + Vector((0.02, -0.04, -0.16))), 0.022, 0.022, "robe", bone, verts=10)  # syringe barrel
    limb(tuple(tip + Vector((0.02, -0.04, -0.16))), tuple(tip + Vector((0.03, -0.06, -0.26))), 0.006, 0.002, "steel",
         bone, verts=6)
    prim("sphere", (0.31, 0.16, 1.92), (0.08, 0.08, 0.08), "robe", bone, verts=12)  # sensor head
    prim("sphere", (0.31, 0.12, 1.92), (0.04, 0.02, 0.04), "lens", bone, verts=10)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.1)),
    ("Hips", "Root", (0, 0, 0.95), (0, 0, 1.05)),
    ("Spine", "Hips", (0, 0, 1.05), (0, 0, 1.42)),
    ("Head", "Spine", (0, 0, 1.46), (0, 0, 1.82)),
]
for _side, _a in ARMS.items():
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.1)),
    ]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.1 * _s, 0, 0.95), (0.1 * _s, 0, 0.5)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.1 * _s, 0, 0.5), (0.1 * _s, 0, 0.1)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.1 * _s, 0, 0.1), (0.1 * _s, -0.2, 0.03)),
    ]

legs()
torso()
head_and_veil()
pauldrons()
arms()
book()
icon()
servo_arm()

armature = c.build_armature(BONES)
mesh = c.assemble("Hospitaller", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.0, distance=0.7)
