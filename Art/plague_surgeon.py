"""Builds the Plague Surgeon: bloated olive-green power armour with copper trim, a maroon hood and cloak, a tattered
surgeon's apron, a bandolier of syringes and vials, a surgical drill arm, a serrated cleaver, and a loudhailer-and-skull
backpack.

Based on the reference images (Death Guard plague surgeon miniature, front / side / back; used as a guide only, nothing
from them is imported).

    blender -b --python Art/plague_surgeon.py                    # writes Assets/Models/PlagueSurgeon.fbx
    blender -b --python Art/plague_surgeon.py -- --render DIR    # also renders preview PNGs into DIR

Same skeleton naming as the Grey Knight (Hips, Spine, Head, UpperArm_L, Thigh_L, ...) so ProceduralWalk and LimbPose
work unchanged, plus one tool bone per hand: Cleaver_L and Drill_R.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "PlagueSurgeon.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(0.95)

mat("armor", (0.27, 0.31, 0.09), metallic=0.35, rough=0.55)
mat("armor_dark", (0.14, 0.16, 0.06), metallic=0.4, rough=0.6)
mat("copper", (0.72, 0.4, 0.17), metallic=0.85, rough=0.35)
mat("copper_dark", (0.38, 0.2, 0.09), metallic=0.8, rough=0.45)
mat("iron", (0.12, 0.12, 0.12), metallic=0.85, rough=0.45)
mat("steel", (0.62, 0.6, 0.57), metallic=0.9, rough=0.3)
mat("rust", (0.45, 0.28, 0.2), metallic=0.7, rough=0.6)
mat("cloak", (0.36, 0.04, 0.09), rough=0.85)
mat("cloak_dark", (0.14, 0.015, 0.04), rough=0.9)
mat("apron", (0.74, 0.77, 0.67), rough=0.9)
mat("rope", (0.5, 0.4, 0.22), rough=0.9)
mat("bone", (0.8, 0.76, 0.62), rough=0.7)
mat("flesh", (0.6, 0.55, 0.42), rough=0.75)
mat("pus", (0.7, 0.72, 0.18), rough=0.4)
mat("glass", (0.2, 0.85, 0.35), rough=0.1, emit=(0.15, 0.8, 0.25), emit_strength=1.2)
mat("glass_amber", (0.9, 0.5, 0.1), rough=0.1, emit=(0.9, 0.45, 0.08), emit_strength=0.8)
mat("eye", (0.8, 1.0, 0.2), emit=(0.7, 1.0, 0.15), emit_strength=5.0)
mat("blood", (0.35, 0.02, 0.02), rough=0.35)


def skull(loc, size, bone, rot=(0, 0, 0)):
    """Small trophy skull facing -Y: cranium, eye sockets, jaw."""
    x, y, z = loc
    prim("sphere", (x, y, z), (size, size * 1.05, size), "bone", bone, rot=rot, verts=14)
    prim("cube", (x, y - size * 0.3, z - size * 0.45), (size * 0.55, size * 0.4, size * 0.3), "bone", bone, bevel=0.01)
    for dx in (-0.22, 0.22):
        prim("sphere", (x + dx * size, y - size * 0.42, z + size * 0.05), (size * 0.26, size * 0.2, size * 0.28),
             "iron", bone, verts=8)


def spike(a, b, r, bone, material="bone", xf=None):
    limb(a, b, r, 0.0, material, bone, xf=xf, verts=8)


def rivets(points, size, material, bone):
    for p in points:
        prim("sphere", p, (size, size, size), material, bone, verts=8)


def vial(x, y, z, bone, material="glass", h=0.14, r=0.035, tilt=(0, 0, 0)):
    prim("cyl", (x, y, z), (r * 2, r * 2, h), material, bone, rot=tilt, verts=10)
    prim("cyl", (x, y, z + h * 0.55), (r * 2.3, r * 2.3, 0.03), "copper", bone, rot=tilt, verts=10)
    prim("cyl", (x, y, z - h * 0.55), (r * 2.3, r * 2.3, 0.03), "copper", bone, rot=tilt, verts=10)


def syringe(a, b, bone, material="glass"):
    """Barrel (a->b) with a plunger knob at a and a needle past b."""
    limb(a, b, 0.03, 0.03, material, bone, verts=8)
    d = (Vector(b) - Vector(a)).normalized()
    tip = tuple(Vector(b) + d * 0.1)
    limb(b, tip, 0.012, 0.0, "steel", bone, verts=6)
    back = tuple(Vector(a) - d * 0.03)
    limb(a, back, 0.04, 0.04, "copper", bone, verts=8)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.26 * s
        prim("cube", (x, -0.1, 0.11), (0.38, 0.6, 0.22), "armor", f"Foot_{side}", bevel=0.07)
        prim("cube", (x, -0.1, 0.02), (0.4, 0.62, 0.05), "iron", f"Foot_{side}", bevel=0.01)
        prim("cube", (x, -0.38, 0.13), (0.3, 0.08, 0.14), "copper_dark", f"Foot_{side}", bevel=0.02)  # toe cap
        limb((x, 0, 0.2), (x, 0, 0.62), 0.24, 0.21, "armor", f"Shin_{side}")
        prim("cube", (x, -0.2, 0.36), (0.3, 0.06, 0.36), "copper", f"Shin_{side}", bevel=0.015)  # greave plate
        prim("cube", (x, -0.225, 0.36), (0.22, 0.04, 0.28), "armor_dark", f"Shin_{side}", bevel=0.01)
        torus((x, 0, 0.26), 0.23, 0.02, "copper", f"Shin_{side}", squash_y=0.9)
        # Bulbous knee guard with a rivet.
        prim("sphere", (x, -0.14, 0.65), (0.4, 0.36, 0.36), "armor", f"Shin_{side}")
        torus((x, -0.2, 0.65), 0.14, 0.025, "copper", f"Shin_{side}", rot=(90, 0, 0))
        limb((x, 0, 0.64), (x, 0, 1.02), 0.21, 0.25, "armor", f"Thigh_{side}")
        prim("cube", (x + 0.2 * s, 0, 0.85), (0.06, 0.34, 0.3), "armor_dark", f"Thigh_{side}", bevel=0.01)
    # Hip kit: a surgical satchel and a hanging vial.
    prim("cube", (0.47, 0.0, 0.98), (0.14, 0.26, 0.24), "rust", "Hips", bevel=0.03)
    prim("cube", (0.47, -0.1, 1.04), (0.16, 0.08, 0.1), "copper_dark", "Hips", bevel=0.015)
    skull((-0.46, -0.1, 1.0), 0.14, "Hips")
    limb((-0.46, -0.1, 0.9), (-0.46, -0.1, 0.78), 0.01, 0.01, "iron", "Hips", verts=6)
    vial(-0.46, -0.1, 0.71, "Hips", "glass_amber")


def torso():
    prim("cube", (0, 0, 1.08), (0.84, 0.54, 0.3), "armor", "Hips", bevel=0.09)
    prim("cyl", (0, 0, 1.24), (0.9, 0.6, 0.1), "iron", "Hips", bevel=0.01, verts=24)  # belt
    prim("cube", (0, -0.305, 1.24), (0.2, 0.04, 0.14), "copper", "Hips", bevel=0.015)  # buckle
    # Chest: broad and slightly paunchy, a bit narrower than the Berserker.
    prim("cube", (0, 0, 1.55), (1.0, 0.66, 0.7), "armor", "Spine", bevel=0.16)
    prim("sphere", (0, -0.06, 1.38), (0.86, 0.66, 0.5), "armor", "Spine", verts=20)  # paunch
    prim("cube", (0, 0, 1.24), (0.95, 0.62, 0.06), "iron", "Spine", bevel=0.01)
    # Riveted chest seams and exposed pipework.
    for x in (-0.3, 0.3):
        prim("cyl", (x, -0.33, 1.48), (0.06, 0.06, 0.42), "iron", "Spine", rot=(6, 0, 0), verts=8)
    rivets([(x, -0.34, 1.7) for x in (-0.38, -0.26, 0.26, 0.38)], 0.04, "copper", "Spine")
    prim("cyl", (0, 0, 1.92), (0.56, 0.48, 0.16), "iron", "Spine", verts=20)  # gorget
    torus((0, 0, 1.99), 0.26, 0.03, "copper", "Spine", squash_y=0.9)
    # Breather tubes dangling from the neck over the chest.
    for s in (-1, 1):
        limb((0.1 * s, -0.22, 1.96), (0.2 * s, -0.36, 1.72), 0.03, 0.03, "iron", "Spine", verts=8)
        limb((0.2 * s, -0.36, 1.72), (0.14 * s, -0.4, 1.58), 0.03, 0.03, "copper_dark", "Spine", verts=8)

    # Tattered surgeon's apron hanging from the chest down past the hips, with rips and a ragged hem.
    apron = [(-0.3, 1.78), (0.3, 1.78), (0.36, 1.2), (0.38, 0.95), (0.3, 0.76), (0.22, 0.86), (0.14, 0.66),
             (0.04, 0.84), (-0.06, 0.64), (-0.16, 0.86), (-0.27, 0.7), (-0.38, 0.92), (-0.36, 1.2)]
    flat_poly(apron, -0.34, 0.025, "apron", "Hips")
    prim("cube", (0.12, -0.36, 1.12), (0.1, 0.012, 0.16), "cloak_dark", "Hips")  # stains and rips
    prim("cube", (-0.14, -0.36, 0.95), (0.12, 0.012, 0.1), "blood", "Hips")
    prim("sphere", (0.2, -0.36, 0.78), (0.1, 0.012, 0.08), "pus", "Hips", verts=8)
    prim("cube", (0, -0.36, 1.55), (0.08, 0.012, 0.3), "rope", "Hips")  # neck rope

    # Bandolier of syringes and vials, shoulder to opposite hip.
    prim("cube", (0, -0.35, 1.5), (0.14, 0.04, 1.1), "rust", "Spine", rot=(0, 38, 0), bevel=0.01)
    for i in range(5):
        t = (i + 0.5) / 5.0
        bx, bz = -0.4 + 0.8 * t, 1.92 - 0.8 * t
        if i % 2 == 0:
            vial(bx, -0.4, bz, "Spine", "glass" if i != 2 else "glass_amber", tilt=(0, 38, 0))
        else:
            syringe((bx + 0.02, -0.4, bz + 0.08), (bx - 0.02, -0.4, bz - 0.08), "Spine")


def cloak_and_hood():
    # Cloak: one maroon sheet across the whole back, from the shoulders to the ground. The backpack is bolted on
    # over it (see PY), so the cloak shows around and below the pack. Ragged hem and holes on the lower panel.
    upper = [(-0.6, 1.98), (0.6, 1.98), (0.66, 1.5), (0.66, 1.0), (-0.66, 1.0), (-0.66, 1.5)]
    flat_poly(upper, 0.36, 0.05, "cloak", "Spine")
    lower = [(-0.66, 1.0), (0.66, 1.0), (0.7, 0.7), (0.66, 0.42), (0.58, 0.2), (0.48, 0.38), (0.38, 0.1),
             (0.26, 0.32), (0.14, 0.08), (0.02, 0.34), (-0.1, 0.12), (-0.22, 0.38), (-0.34, 0.14), (-0.46, 0.4),
             (-0.58, 0.18), (-0.66, 0.42), (-0.7, 0.7)]
    flat_poly(lower, 0.44, 0.05, "cloak", "Hips")
    flat_poly([(x * 0.94, z) for x, z in lower], 0.48, 0.02, "cloak_dark", "Hips")  # darker lining
    # Folds running down the cloak so it reads as cloth, not a board.
    for x in (-0.44, -0.2, 0.2, 0.44):
        prim("cube", (x, 0.4, 0.7), (0.05, 0.03, 0.6), "cloak_dark", "Hips")
    for x, z, r in ((0.3, 0.7, 0.07), (-0.2, 0.5, 0.06), (0.45, 0.45, 0.05)):  # moth-eaten holes
        prim("cyl", (x, 0.47, z), (r * 2, r * 2, 0.04), "cloak_dark", "Hips", rot=(90, 0, 0), verts=10)
    # Cowl: the hood's collar, draped over the shoulders and upper chest.
    prim("sphere", (0, 0.04, 1.8), (1.2, 0.8, 0.3), "cloak", "Spine", verts=24)
    prim("sphere", (0, -0.16, 1.66), (0.64, 0.3, 0.2), "cloak", "Spine", verts=20)

    # Hood: a deep cloth cowl with a pointed, drooping back and a thick rolled front edge around a shadowed face.
    prim("sphere", (0, 0.1, 1.94), (0.6, 0.62, 0.68), "cloak", "Head", verts=24)  # crown
    limb((0, 0.14, 2.04), (0, 0.34, 1.66), 0.28, 0.06, "cloak", "Head", verts=16)  # cloth tail falling down the back
    limb((0, -0.02, 2.22), (0, -0.32, 2.12), 0.2, 0.03, "cloak", "Head", verts=14)  # peak flopping forward
    for s in (-1, 1):  # side drapes hanging past the cheeks
        limb((0.27 * s, -0.04, 2.04), (0.3 * s, -0.12, 1.72), 0.14, 0.07, "cloak", "Head", verts=12)
    torus((0, -0.2, 1.94), 0.2, 0.07, "cloak", "Head", rot=(90, 0, 0))  # rolled front edge
    prim("cyl", (0, -0.17, 1.94), (0.4, 0.4, 0.04), "cloak_dark", "Head", rot=(90, 0, 0), verts=20)  # shadow
    prim("sphere", (0, -0.12, 1.91), (0.24, 0.22, 0.30), "flesh", "Head", verts=16)  # gaunt face
    for x in (-0.055, 0.055):
        prim("sphere", (x, -0.225, 1.96), (0.055, 0.03, 0.04), "eye", "Head", verts=10)
    prim("cube", (0, -0.235, 1.85), (0.1, 0.05, 0.07), "iron", "Head", bevel=0.01)  # small respirator
    for x in (-0.05, 0.05):
        limb((x, -0.25, 1.84), (x * 2.2, -0.26, 1.64), 0.018, 0.018, "copper_dark", "Head", verts=8)


def pauldrons():
    # Huge, tall pauldrons: their tops rise level with the hood, so the head sits sunk between them.
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.8 * s
        bone = f"UpperArm_{side}"
        prim("sphere", (x, 0, 2.0), (0.82, 0.76, 0.7), "armor", bone, verts=24)
        torus((x, 0, 1.72), 0.37, 0.03, "copper", bone, squash_y=0.9)
        torus((x, 0, 2.28), 0.24, 0.025, "copper", bone, squash_y=0.9)
        prim("sphere", (x + 0.12 * s, -0.04, 2.2), (0.4, 0.4, 0.3), "armor_dark", bone, verts=14)
        rivets([(x + 0.1 * s, -0.36, 2.0), (x + 0.3 * s, -0.26, 1.95), (x + 0.32 * s, 0.24, 1.95), (x + 0.1 * s, 0.36, 2.0)],
               0.055, "copper", bone)
        prim("cube", (x + 0.3 * s, 0, 1.9), (0.08, 0.6, 0.34), "copper_dark", bone, bevel=0.02)  # outer rim plate
    # Left pauldron: pale skull plate and the big curved tusk sweeping up and back.
    skull((1.0, -0.2, 2.0), 0.15, "UpperArm_L")
    limb((0.6, 0.2, 2.22), (0.6, 0.3, 2.6), 0.1, 0.07, "bone", "UpperArm_L", verts=10)
    limb((0.6, 0.3, 2.6), (0.52, 0.18, 2.9), 0.07, 0.03, "bone", "UpperArm_L", verts=10)
    spike((0.52, 0.18, 2.9), (0.46, -0.02, 3.04), 0.03, "UpperArm_L")
    # Right pauldron: a chained vial hanging from the front.
    for i in range(3):
        prim("cube", (-0.6 - 0.06 * i, -0.3, 2.3 - 0.06 * i), (0.05, 0.03, 0.06), "steel", "UpperArm_R", bevel=0.006)
    vial(-0.92, -0.34, 1.8, "UpperArm_R", "glass")


ARMS = {
    "L": dict(sh=(0.74, 0, 1.85), el=(0.8, -0.06, 1.45), wr=(0.78, -0.2, 1.2)),
    "R": dict(sh=(-0.74, 0, 1.85), el=(-0.8, -0.06, 1.45), wr=(-0.78, -0.2, 1.2)),
}


def arms():
    for side, a in ARMS.items():
        s = 1 if side == "L" else -1
        limb(a["sh"], a["el"], 0.2, 0.18, "armor", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.34, 0.34, 0.34), "armor_dark", f"UpperArm_{side}")
        torus(a["el"], 0.15, 0.02, "copper", f"UpperArm_{side}", rot=(0, 90, 0))
        limb(a["el"], a["wr"], 0.19, 0.22, "armor", f"Forearm_{side}")
        mid = tuple((Vector(a["el"]) + Vector(a["wr"])) / 2)
        prim("cyl", mid, (0.3, 0.3, 0.06), "copper", f"Forearm_{side}", rot=(-70, 0, 0), bevel=0.01, verts=16)
        d = (Vector(a["wr"]) - Vector(a["el"])).normalized()
        prim("sphere", tuple(Vector(a["wr"]) + d * 0.05), (0.3, 0.3, 0.3), "iron", f"Hand_{side}")
    # Left forearm: a bound surgical tool-case with vials.
    prim("cube", (0.92, -0.04, 1.3), (0.12, 0.2, 0.24), "rust", "Forearm_L", bevel=0.02)
    vial(0.92, -0.04, 1.5, "Forearm_L", "glass", h=0.14)
    # Right forearm: drill motor housing and hoses.
    prim("cyl", (-0.8, -0.12, 1.26), (0.22, 0.22, 0.32), "copper_dark", "Forearm_R", rot=(-20, 0, 0), verts=14)
    limb((-0.6, 0.12, 1.6), (-0.8, -0.04, 1.45), 0.03, 0.03, "iron", "Forearm_R", verts=8)


def drill():
    """Surgical drill on the right wrist: a collar, then a stepped spiral cone pointing down and forward."""
    wr = Vector(ARMS["R"]["wr"])
    el = Vector(ARMS["R"]["el"])
    base = wr + (wr - el).normalized() * 0.1
    d = Vector((0.0, -0.1, -1.0)).normalized()
    bone = "Drill_R"
    bx, by, bz = base
    prim("cyl", (bx, by - 0.02, bz - 0.08), (0.2, 0.2, 0.14), "copper", bone, rot=(18, 0, 0), verts=14)
    limb(tuple(base), tuple(base + d * 0.22), 0.1, 0.1, "iron", bone, verts=12)  # chuck
    start = base + d * 0.2
    end = start + d * 0.72
    limb(tuple(start), tuple(end), 0.11, 0.0, "steel", bone, verts=12)  # bit
    for i in range(1, 7):  # spiral ridges
        t = i / 7.0
        p = start + (end - start) * t
        r = 0.115 * (1.0 - t) + 0.02
        prim("cyl", tuple(p), (r * 2.4, r * 2.4, 0.035), "copper_dark" if i % 2 else "steel", bone,
             rot=(18, 0, 0), verts=12)
    # Second, thin probe riding alongside.
    limb((bx - 0.14, by - 0.04, bz - 0.04), (bx - 0.2, by - 0.2, bz - 0.5), 0.025, 0.0, "steel", bone, verts=8)


def cleaver():
    """Serrated bloodied cleaver in the left hand, blade hanging down along the leg like the miniature."""
    wr = Vector(ARMS["L"]["wr"])
    el = Vector(ARMS["L"]["el"])
    px, py, pz = tuple(wr + (wr - el).normalized() * 0.05)
    bone = "Cleaver_L"
    prim("cyl", (px, py - 0.02, pz - 0.04), (0.09, 0.09, 0.34), "iron", bone, rot=(0, 0, 0), verts=10)  # grip
    prim("cube", (px, py, pz - 0.24), (0.34, 0.14, 0.08), "copper_dark", bone, bevel=0.02)  # guard
    # Blade: broad, slightly tapering slab with a saw-toothed outer edge, tip at the bottom.
    outer = []
    z = pz - 0.3
    x_out = px + 0.2
    n = 9
    for i in range(n):
        zz = z - 0.78 * i / (n - 1)
        outer.append((x_out + (0.07 if i % 2 else 0.0) - 0.05 * (i / n), zz))
    pts = [(px - 0.2, z)] + outer + [(px + 0.02, z - 0.92)]
    flat_poly(pts, py, 0.05, "steel", bone)
    inner = [(px - 0.14, z - 0.06), (px + 0.1, z - 0.06), (px + 0.1, z - 0.6), (px - 0.02, z - 0.78),
             (px - 0.14, z - 0.5)]
    flat_poly(inner, py - 0.03, 0.02, "rust", bone)
    for dz in (0.2, 0.44, 0.62):  # old blood
        prim("sphere", (px + 0.02, py - 0.045, z - dz), (0.12, 0.012, 0.1), "blood", bone, verts=8)
    spike((px - 0.2, py, z), (px - 0.28, py, z + 0.12), 0.03, bone, "steel")


PY = 0.24  # the backpack sits this far back so the cloak passes between it and the armour


def backpack():
    prim("cube", (0, 0.36 + PY, 1.5), (0.74, 0.44, 0.72), "armor", "Backpack", bevel=0.09)
    prim("cube", (0, 0.585 + PY, 1.5), (0.4, 0.03, 0.44), "iron", "Backpack", bevel=0.01)
    prim("cyl", (0, 0.6 + PY, 1.5), (0.2, 0.05, 0.2), "glass", "Backpack", rot=(90, 0, 0), verts=16)
    for x in (-0.28, 0.28):  # exhaust stacks
        prim("cyl", (x, 0.38 + PY, 1.94), (0.16, 0.16, 0.3), "iron", "Backpack", verts=12)
        prim("cyl", (x, 0.38 + PY, 2.1), (0.2, 0.2, 0.05), "copper", "Backpack", verts=12)
    rivets([(x, 0.34 + PY, 1.86) for x in (-0.3, -0.1, 0.1, 0.3)], 0.045, "copper", "Backpack")

    # Loudhailer ring and horn, mounted on a stalk above the left shoulder.
    limb((0.1, 0.4 + PY, 1.86), (0.12, 0.4 + PY, 2.4), 0.06, 0.05, "iron", "Backpack", verts=10)
    torus((0.12, 0.38 + PY, 2.5), 0.2, 0.035, "copper", "Backpack", rot=(90, 0, 0))
    for i in range(14):  # ring teeth
        a = math.radians(i * 360 / 14)
        prim("cube", (0.12 + math.cos(a) * 0.23, 0.38 + PY, 2.5 + math.sin(a) * 0.23), (0.045, 0.045, 0.045),
             "copper_dark", "Backpack", rot=(0, -math.degrees(a), 0))
    limb((0.12, 0.44 + PY, 2.5), (0.12, 0.22 + PY, 2.5), 0.07, 0.15, "steel", "Backpack", verts=16)  # horn
    prim("cyl", (0.12, 0.2 + PY, 2.5), (0.14, 0.04, 0.14), "iron", "Backpack", rot=(90, 0, 0), verts=14)

    # Skull-box vox unit on the right shoulder, with a dribble of pus.
    prim("cube", (-0.4, 0.3 + PY, 2.5), (0.34, 0.3, 0.3), "iron", "Backpack", bevel=0.03)
    prim("cube", (-0.4, 0.15 + PY, 2.5), (0.28, 0.02, 0.24), "armor_dark", "Backpack")
    skull((-0.4, 0.11 + PY, 2.52), 0.16, "Backpack")
    limb((-0.36, 0.1 + PY, 2.36), (-0.34, 0.1 + PY, 2.14), 0.02, 0.03, "pus", "Backpack", verts=8)
    spike((-0.34, 0.1 + PY, 2.16), (-0.34, 0.1 + PY, 2.06), 0.03, "Backpack", "pus")
    # Hoses from the pack to the hands.
    limb((-0.3, 0.3 + PY, 1.74), (-0.64, 0.12, 1.5), 0.035, 0.035, "iron", "Backpack", verts=8)
    limb((0.3, 0.3 + PY, 1.74), (0.62, 0.1, 1.55), 0.035, 0.035, "copper_dark", "Backpack", verts=8)
    # Skull trophy on a hook at the pack's lower edge.
    skull((0.0, 0.42 + PY, 1.12), 0.15, "Backpack")
    limb((0.0, 0.5 + PY, 1.3), (0.0, 0.46 + PY, 1.22), 0.012, 0.012, "iron", "Backpack", verts=6)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.0), (0, 0, 1.2)),
    ("Spine", "Hips", (0, 0, 1.2), (0, 0, 1.8)),
    ("Head", "Spine", (0, 0, 1.7), (0, 0, 2.1)),
    ("Backpack", "Spine", (0, 0.4, 1.7), (0, 0.6, 1.3)),
]
for _side, _a in ARMS.items():
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    _grip = tuple(_wr + _dir * 0.05)
    _tool = "Cleaver_L" if _side == "L" else "Drill_R"
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.14)),
        (_tool, f"Hand_{_side}", _grip, (_grip[0], _grip[1], _grip[2] - 0.4)),
    ]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.26 * _s, 0, 1.0), (0.26 * _s, 0, 0.62)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.26 * _s, 0, 0.62), (0.26 * _s, 0, 0.2)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.26 * _s, 0, 0.2), (0.26 * _s, -0.3, 0.05)),
    ]

legs()
torso()
cloak_and_hood()
pauldrons()
arms()
drill()
cleaver()
backpack()

armature = c.build_armature(BONES)
mesh = c.assemble("PlagueSurgeon", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.4, distance=1.25)
