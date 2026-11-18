"""Builds the Thousand Sons Sorcerer: deep blue power armour trimmed in gold, a tall Prosperine crested helm with
blue-and-gold striped crest, curling horn ornaments over the shoulders, a crimson cloak with a cream lining, cream and
red robes hanging between the legs, and a tall staff topped with a sun-disc and a glowing warp gem.

Based on the reference images the user supplied (used as a guide only, nothing from them is imported).

    blender -b --python Art/sorcerer.py                    # writes Assets/Models/Sorcerer.fbx
    blender -b --python Art/sorcerer.py -- --render DIR    # also renders preview PNGs into DIR
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "Sorcerer.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(0.95)

mat("armor", (0.05, 0.25, 0.62), metallic=0.65, rough=0.3)        # Thousand Sons blue
mat("armor_dark", (0.03, 0.12, 0.32), metallic=0.6, rough=0.35)
mat("gold", (0.95, 0.7, 0.22), metallic=0.85, rough=0.25)
mat("brass", (0.6, 0.42, 0.18), metallic=0.8, rough=0.35)
mat("black", (0.03, 0.03, 0.04), metallic=0.4, rough=0.4)
mat("cream", (0.86, 0.8, 0.66), rough=0.75)
mat("crimson", (0.42, 0.05, 0.09), rough=0.7)
mat("crimson_dark", (0.26, 0.03, 0.06), rough=0.75)
mat("ivory", (0.9, 0.86, 0.76), rough=0.5)
mat("stripe_blue", (0.1, 0.45, 0.85), metallic=0.4, rough=0.35)
mat("stripe_gold", (0.98, 0.82, 0.3), metallic=0.6, rough=0.3)
mat("warp", (0.3, 0.8, 1.0), emit=(0.25, 0.75, 1.0), emit_strength=3.0)
mat("eye", (0.2, 0.95, 0.6), emit=(0.2, 1.0, 0.6), emit_strength=3.0)
mat("red_gem", (0.8, 0.08, 0.06), emit=(0.9, 0.1, 0.05), emit_strength=1.2)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.24 * s
        prim("cube", (x, -0.1, 0.12), (0.36, 0.58, 0.24), "armor", f"Foot_{side}", bevel=0.08)
        prim("cube", (x, -0.1, 0.02), (0.38, 0.6, 0.05), "armor_dark", f"Foot_{side}", bevel=0.01)
        prim("cube", (x, -0.37, 0.14), (0.3, 0.04, 0.12), "gold", f"Foot_{side}", bevel=0.01)  # toe cap trim
        limb((x, 0, 0.2), (x, 0, 0.62), 0.23, 0.2, "armor", f"Shin_{side}")
        # Greave: a gold-edged plate with the eye of Tzeentch.
        prim("cube", (x, -0.22, 0.4), (0.34, 0.04, 0.42), "gold", f"Shin_{side}", bevel=0.01)
        prim("cube", (x, -0.235, 0.4), (0.28, 0.05, 0.36), "armor", f"Shin_{side}", bevel=0.02)
        prim("sphere", (x, -0.265, 0.44), (0.1, 0.03, 0.06), "gold", f"Shin_{side}", verts=12)
        prim("sphere", (x, -0.275, 0.44), (0.04, 0.02, 0.04), "eye", f"Shin_{side}", verts=10)
        prim("sphere", (x, -0.1, 0.62), (0.36, 0.33, 0.33), "armor", f"Shin_{side}")  # knee
        prim("sphere", (x, -0.26, 0.63), (0.16, 0.08, 0.16), "gold", f"Shin_{side}", verts=12)
        limb((x, 0, 0.64), (x, 0, 1.02), 0.2, 0.24, "armor", f"Thigh_{side}")


def robes():
    """Cream robe panel down the front with a crimson inner layer and gold hem, between the legs like the reference."""
    flat_poly([(-0.2, 1.08), (0.2, 1.08), (0.24, 0.42), (0, 0.32), (-0.24, 0.42)], -0.3, 0.02, "crimson", "Hips")
    flat_poly([(-0.16, 1.06), (0.16, 1.06), (0.18, 0.5), (0, 0.42), (-0.18, 0.5)], -0.315, 0.02, "cream", "Hips")
    flat_poly([(-0.18, 0.52), (0.0, 0.44), (0.18, 0.52), (0.18, 0.47), (0.0, 0.39), (-0.18, 0.47)], -0.33, 0.015, "gold", "Hips")
    prim("cube", (0, -0.33, 0.85), (0.04, 0.012, 0.36), "gold", "Hips")  # embroidered stripe
    # Side tassets: armoured skirt plates over the thighs.
    for s in (1, -1):
        prim("cube", (0.36 * s, -0.04, 0.95), (0.16, 0.48, 0.32), "armor", "Hips", rot=(0, -12 * s, 0), bevel=0.04)
        prim("cube", (0.43 * s, -0.04, 0.8), (0.05, 0.5, 0.04), "gold", "Hips", rot=(0, -12 * s, 0))


def torso():
    prim("cube", (0, 0, 1.08), (0.78, 0.5, 0.3), "armor", "Hips", bevel=0.09)
    prim("cyl", (0, 0, 1.22), (0.84, 0.56, 0.08), "brass", "Hips", bevel=0.01, verts=24)  # belt
    prim("cube", (0, -0.29, 1.22), (0.2, 0.04, 0.14), "gold", "Hips", bevel=0.01)  # buckle
    prim("sphere", (0, -0.315, 1.22), (0.07, 0.03, 0.07), "red_gem", "Hips", verts=10)

    prim("cube", (0, 0, 1.55), (0.96, 0.6, 0.7), "armor", "Spine", bevel=0.15)
    prim("cube", (0, 0, 1.24), (0.93, 0.58, 0.05), "armor_dark", "Spine", bevel=0.01)
    # Breastplate: a gold-framed plate with the great eye of the Thousand Sons at its heart.
    flat_poly([(-0.36, 1.82), (0.36, 1.82), (0.3, 1.45), (0, 1.3), (-0.3, 1.45)], -0.31, 0.03, "gold", "Spine")
    flat_poly([(-0.32, 1.79), (0.32, 1.79), (0.27, 1.47), (0, 1.34), (-0.27, 1.47)], -0.325, 0.03, "armor", "Spine")
    prim("sphere", (0, -0.35, 1.58), (0.22, 0.05, 0.13), "gold", "Spine", verts=16)
    prim("sphere", (0, -0.37, 1.58), (0.15, 0.04, 0.1), "ivory", "Spine", verts=14)
    prim("sphere", (0, -0.385, 1.58), (0.07, 0.03, 0.07), "eye", "Spine", verts=12)
    for s in (1, -1):  # gold wings flaring out from the eye
        prim("cube", (0.2 * s, -0.345, 1.66), (0.2, 0.02, 0.04), "gold", "Spine", rot=(0, 20 * s, 0))
        prim("cube", (0.18 * s, -0.345, 1.5), (0.16, 0.02, 0.035), "gold", "Spine", rot=(0, -20 * s, 0))
    # High collar the helm sits in.
    prim("cyl", (0, 0, 1.92), (0.6, 0.52, 0.2), "armor", "Spine", bevel=0.02)
    prim("cyl", (0, 0, 2.0), (0.62, 0.54, 0.04), "gold", "Spine", bevel=0.005)


def pauldrons():
    for s, side in ((1, "L"), (-1, "R")):
        x, z = 0.7 * s, 1.74
        bone = f"UpperArm_{side}"
        c.pauldron_block(x, z, s, 0.9, 0.76, 0.66, bone, mat="armor", trim="gold", tilt=16)
        # A blue stripe inside the gold rim, Egyptian style.
        prim("cube", (x + 0.02 * s, -0.385, z - 0.2), (0.62, 0.02, 0.05), "stripe_blue", bone, rot=(0, 16 * s, 0))
        # Eye of Tzeentch on the face of each plate.
        prim("sphere", (x + 0.08 * s, -0.39, z - 0.05), (0.18, 0.04, 0.11), "gold", bone, verts=14)
        prim("sphere", (x + 0.08 * s, -0.41, z - 0.05), (0.07, 0.03, 0.07), "eye", bone, verts=10)
        # Curling horn ornament: ivory tusks sweeping up and out from the back of the shoulder, ending in a hook.
        pts = [(x * 0.9, 0.2, z + 0.22), (x * 1.2, 0.24, z + 0.42), (x * 1.42, 0.22, z + 0.7),
               (x * 1.5, 0.16, z + 0.96), (x * 1.38, 0.08, z + 1.1)]
        r = 0.11
        for a, b in zip(pts, pts[1:]):
            limb(a, b, r, r * 0.78, "ivory", bone)
            prim("sphere", b, (r * 1.6, r * 1.6, r * 1.6), "ivory", bone, verts=10)
            r *= 0.74
        prim("cyl", pts[1], (0.16, 0.16, 0.05), "gold", bone, rot=(0, 30 * s, 0), verts=14)  # gold collar on the horn


def arms():
    # Left: reaching forward and out, palm open, wreathed in warp-fire (casting). Right: gripping the staff.
    arms_def = {
        "L": dict(sh=(0.6, 0, 1.65), el=(0.88, -0.2, 1.38), wr=(0.84, -0.55, 1.36)),
        "R": dict(sh=(-0.6, 0, 1.65), el=(-0.86, -0.08, 1.3), wr=(-0.76, -0.34, 1.12)),
    }
    from mathutils import Vector
    for side, a in arms_def.items():
        limb(a["sh"], a["el"], 0.18, 0.16, "armor", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.32, 0.32, 0.32), "armor", f"UpperArm_{side}")
        limb(a["el"], a["wr"], 0.17, 0.19, "armor", f"Forearm_{side}")
        # Gold vambrace band.
        mid = (Vector(a["el"]) + Vector(a["wr"])) / 2
        prim("cyl", tuple(mid), (0.34, 0.34, 0.08), "gold", f"Forearm_{side}",
             rot=tuple(math.degrees(e) for e in (Vector(a["wr"]) - Vector(a["el"])).to_track_quat("Z", "Y").to_euler()), verts=16)
        prim("sphere", tuple(Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.05),
             (0.3, 0.3, 0.3), "armor_dark", f"Hand_{side}")
    # Warp-fire licking up from the open left hand.
    hx, hy, hz = 0.84, -0.66, 1.42
    prim("sphere", (hx, hy, hz), (0.16, 0.16, 0.2), "warp", "Hand_L", verts=14)
    for i, (dx, dz, h) in enumerate(((0.05, 0.12, 0.22), (-0.05, 0.1, 0.18), (0.0, 0.16, 0.28))):
        limb((hx + dx, hy, hz + 0.05), (hx + dx * 1.6, hy - 0.03, hz + dz + h), 0.045, 0.005, "warp", "Hand_L", verts=8)


def helm():
    """Prosperine helm: a rounded dome with a T-shaped visor, side horns, and the tall striped crest rising from it."""
    prim("sphere", (0, -0.02, 2.06), (0.44, 0.48, 0.5), "armor", "Head", verts=24)
    prim("cube", (0, -0.25, 2.06), (0.34, 0.05, 0.08), "black", "Head", bevel=0.015)   # T-visor bar
    prim("cube", (0, -0.25, 1.98), (0.08, 0.05, 0.16), "black", "Head", bevel=0.015)   # T-visor stem
    for x in (-0.09, 0.09):
        prim("sphere", (x, -0.27, 2.06), (0.07, 0.03, 0.05), "eye", "Head", verts=10)
    prim("cube", (0, -0.24, 2.16), (0.36, 0.04, 0.04), "gold", "Head", bevel=0.01)     # brow band
    prim("cube", (0, -0.245, 1.9), (0.22, 0.04, 0.06), "gold", "Head", bevel=0.01)      # chin

    # The crest: a tall flat plate standing on the crown, widening into a T at the top, banded blue and gold like
    # a pharaoh's headdress, with a gold frame and a red gem.
    y = -0.02
    flat_poly([(-0.24, 2.22), (0.24, 2.22), (0.3, 2.78), (0.36, 2.84), (0.36, 2.98), (-0.36, 2.98), (-0.36, 2.84), (-0.3, 2.78)],
              y, 0.08, "gold", "Head")
    z = 2.28
    i = 0
    while z < 2.76:
        w = 0.19 + (z - 2.28) * 0.12
        flat_poly([(-w, z), (w, z), (w, z + 0.045), (-w, z + 0.045)], y - 0.041, 0.01,
                  "stripe_blue" if i % 2 == 0 else "stripe_gold", "Head")
        z += 0.06
        i += 1
    flat_poly([(-0.32, 2.87), (0.32, 2.87), (0.32, 2.95), (-0.32, 2.95)], y - 0.041, 0.01, "stripe_blue", "Head")
    prim("sphere", (0, y - 0.055, 2.82), (0.09, 0.03, 0.09), "red_gem", "Head", verts=12)
    # A ridge running back over the dome into the crest.
    prim("cube", (0, 0.05, 2.28), (0.06, 0.4, 0.08), "gold", "Head", bevel=0.015)


def cloak():
    """Crimson cloak, cream lining showing at the edges, hanging from the shoulders down to the ankles."""
    y = 0.5
    flat_poly([(-0.55, 1.95), (0.55, 1.95), (0.72, 0.9), (0.66, 0.18), (0.2, 0.12), (-0.2, 0.12), (-0.66, 0.18), (-0.72, 0.9)],
              y, 0.04, "crimson", "Cloak")
    flat_poly([(-0.52, 1.92), (0.52, 1.92), (0.68, 0.92), (0.62, 0.22), (0.2, 0.17), (-0.2, 0.17), (-0.62, 0.22), (-0.68, 0.92)],
              y - 0.03, 0.02, "cream", "Cloak")
    for s in (1, -1):  # folds
        prim("cube", (0.3 * s, y + 0.03, 1.0), (0.05, 0.03, 1.5), "crimson_dark", "Cloak", rot=(0, 4 * s, 0))
    prim("cube", (0, y + 0.03, 1.0), (0.05, 0.03, 1.6), "crimson_dark", "Cloak")
    # Gold clasps at the shoulders.
    for s in (1, -1):
        prim("cyl", (0.42 * s, 0.3, 1.92), (0.12, 0.12, 0.05), "gold", "Spine", rot=(90, 0, 0), verts=14)
        limb((0.42 * s, 0.3, 1.92), (0.5 * s, 0.5, 1.9), 0.06, 0.08, "crimson", "Cloak")  # cloth over the shoulder


def backpack():
    prim("cube", (0, 0.3, 1.52), (0.52, 0.3, 0.5), "armor", "Spine", bevel=0.07)
    for s in (-1, 1):
        prim("cyl", (0.2 * s, 0.34, 1.86), (0.14, 0.14, 0.24), "brass", "Spine", verts=14)  # exhaust stacks
        prim("cyl", (0.2 * s, 0.34, 1.99), (0.1, 0.1, 0.04), "black", "Spine", verts=12)


STAFF_GRIP = (-0.76, -0.42, 1.08)


def staff():
    """Force staff: a dark shaft banded in gold, a sun-disc head with radiating spikes and a glowing warp gem, and a
    small crook curling from the top, after the reference."""
    px, py, pz = STAFF_GRIP
    b = "Staff"
    prim("cyl", (px, py, pz + 0.3), (0.07, 0.07, 2.4), "black", b, verts=12)          # shaft
    for z in (-0.6, -0.1, 0.25, 0.9):
        prim("cyl", (px, py, pz + z), (0.1, 0.1, 0.05), "gold", b, verts=12)
    prim("cyl", (px, py, pz - 0.92), (0.11, 0.11, 0.08), "gold", b, verts=12)         # butt cap
    hz = pz + 1.6
    prim("cyl", (px, py, hz - 0.15), (0.14, 0.14, 0.14), "gold", b, verts=14)         # collar
    torus((px, py, hz + 0.08), 0.2, 0.035, "gold", b, rot=(90, 0, 0))                 # sun-disc ring
    for i in range(10):                                                               # rays
        a = i * math.pi * 2 / 10
        d = (math.cos(a), 0, math.sin(a))
        limb((px + d[0] * 0.22, py, hz + 0.08 + d[2] * 0.22), (px + d[0] * 0.36, py, hz + 0.08 + d[2] * 0.36),
             0.03, 0.005, "gold", b, verts=6)
    prim("sphere", (px, py, hz + 0.08), (0.2, 0.12, 0.2), "warp", b, verts=16)         # gem
    prim("sphere", (px, py - 0.06, hz + 0.08), (0.08, 0.04, 0.08), "eye", b, verts=10)
    # Crook curling over the top.
    pts = [(px, py, hz + 0.3), (px + 0.06, py, hz + 0.48), (px + 0.2, py, hz + 0.56), (px + 0.3, py, hz + 0.48)]
    for a2, b2 in zip(pts, pts[1:]):
        limb(a2, b2, 0.035, 0.03, "gold", b, verts=8)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.0), (0, 0, 1.2)),
    ("Spine", "Hips", (0, 0, 1.2), (0, 0, 1.8)),
    ("Head", "Spine", (0, 0, 1.8), (0, 0, 2.2)),
    ("Cloak", "Spine", (0, 0.5, 1.9), (0, 0.5, 1.0)),
    ("UpperArm_L", "Spine", (0.6, 0, 1.65), (0.88, -0.2, 1.38)),
    ("Forearm_L", "UpperArm_L", (0.88, -0.2, 1.38), (0.84, -0.55, 1.36)),
    ("Hand_L", "Forearm_L", (0.84, -0.55, 1.36), (0.84, -0.68, 1.4)),
    ("UpperArm_R", "Spine", (-0.6, 0, 1.65), (-0.86, -0.08, 1.3)),
    ("Forearm_R", "UpperArm_R", (-0.86, -0.08, 1.3), (-0.76, -0.34, 1.12)),
    ("Hand_R", "Forearm_R", (-0.76, -0.34, 1.12), (-0.76, -0.44, 1.06)),
    ("Staff", "Hand_R", STAFF_GRIP, (STAFF_GRIP[0], STAFF_GRIP[1], STAFF_GRIP[2] + 0.4)),
]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.24 * _s, 0, 1.0), (0.24 * _s, 0, 0.6)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.24 * _s, 0, 0.6), (0.24 * _s, 0, 0.2)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.24 * _s, 0, 0.2), (0.24 * _s, -0.3, 0.05)),
    ]

legs()
robes()
torso()
pauldrons()
arms()
helm()
cloak()
backpack()
staff()

armature = c.build_armature(BONES)
mesh = c.assemble("Sorcerer", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.5, distance=1.25)
