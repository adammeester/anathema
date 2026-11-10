"""Builds the Khorne Berserker: hulking blood-red power armour, horned helm, bone trophies, dual chain axes.

Based on the reference images (Chaos Space Marine Khorne Berzerker art and miniatures; used as a guide only,
nothing from them is imported).

    blender -b --python Art/khorne_berserker.py                    # writes Assets/Models/KhorneBerserker.fbx
    blender -b --python Art/khorne_berserker.py -- --render DIR    # also renders preview PNGs into DIR

Same skeleton naming as the Grey Knight (Hips, Spine, Head, UpperArm_L, Thigh_L, ...) so ProceduralWalk and LimbPose
work unchanged, plus one weapon bone per hand: Axe_L and Axe_R.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, pivot_rotation, prim, torus  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "KhorneBerserker.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None
ONLY = argv[argv.index("--only") + 1] if "--only" in argv else None  # dual | heavy | evisc (previews)
OUT_FBX = argv[argv.index("--out") + 1] if "--out" in argv else OUT_FBX

c.configure(0.95)

mat("armor", (0.5, 0.035, 0.03), metallic=0.55, rough=0.42)
mat("armor_dark", (0.22, 0.02, 0.02), metallic=0.6, rough=0.5)
mat("iron", (0.1, 0.1, 0.11), metallic=0.85, rough=0.45)
mat("steel", (0.6, 0.6, 0.62), metallic=0.9, rough=0.28)
mat("brass", (0.78, 0.55, 0.2), metallic=0.85, rough=0.32)
mat("bone", (0.82, 0.76, 0.62), rough=0.7)
mat("leather", (0.28, 0.16, 0.1), rough=0.8)
mat("flesh", (0.62, 0.4, 0.34), rough=0.7)
mat("eye", (1.0, 0.25, 0.05), emit=(1.0, 0.25, 0.05), emit_strength=5.0)
mat("rune", (1.0, 0.1, 0.05), emit=(1.0, 0.1, 0.05), emit_strength=2.0)


def skull(loc, size, bone, rot=(0, 0, 0), xf=None):
    """Small trophy skull facing -Y: cranium, eye sockets, jaw."""
    x, y, z = loc
    prim("sphere", (x, y, z), (size, size * 1.05, size), "bone", bone, rot=rot, verts=14, xf=xf)
    prim("cube", (x, y - size * 0.3, z - size * 0.45), (size * 0.55, size * 0.4, size * 0.3), "bone", bone, bevel=0.01, xf=xf)
    for dx in (-0.22, 0.22):
        prim("sphere", (x + dx * size, y - size * 0.42, z + size * 0.05), (size * 0.26, size * 0.2, size * 0.28),
             "iron", bone, verts=8, xf=xf)


def spike(a, b, r, bone, material="bone", xf=None):
    limb(a, b, r, 0.0, material, bone, xf=xf, verts=8)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.25 * s
        prim("cube", (x, -0.1, 0.11), (0.36, 0.58, 0.22), "armor", f"Foot_{side}", bevel=0.07)
        prim("cube", (x, -0.1, 0.02), (0.38, 0.6, 0.05), "iron", f"Foot_{side}", bevel=0.01)
        for dx in (-0.1, 0, 0.1):  # toe spikes
            spike((x + dx, -0.36, 0.1), (x + dx * 1.3, -0.52, 0.1), 0.035, f"Foot_{side}", "brass")
        limb((x, 0, 0.2), (x, 0, 0.62), 0.23, 0.2, "armor", f"Shin_{side}")
        prim("cube", (x, -0.22, 0.4), (0.34, 0.05, 0.4), "brass", f"Shin_{side}", bevel=0.01)  # greave trim
        prim("cube", (x, -0.235, 0.4), (0.28, 0.05, 0.34), "armor", f"Shin_{side}", bevel=0.02)
        prim("sphere", (x, -0.13, 0.64), (0.38, 0.34, 0.34), "armor", f"Shin_{side}")  # knee
        spike((x, -0.3, 0.66), (x, -0.5, 0.7), 0.06, f"Shin_{side}")
        limb((x, 0, 0.64), (x, 0, 1.02), 0.2, 0.23, "armor", f"Thigh_{side}")
    skull((0.25, -0.33, 0.9), 0.17, "Thigh_L")  # trophy on the left thigh
    # Hanging bone totem on the right hip.
    skull((-0.46, -0.1, 1.0), 0.15, "Hips")


def torso():
    prim("cube", (0, 0, 1.08), (0.8, 0.5, 0.3), "armor", "Hips", bevel=0.09)
    prim("cyl", (0, 0, 1.23), (0.86, 0.56, 0.1), "iron", "Hips", bevel=0.01, verts=24)  # belt
    prim("cube", (0, -0.285, 1.23), (0.2, 0.04, 0.16), "brass", "Hips", bevel=0.015)  # buckle
    skull((0, -0.33, 1.23), 0.16, "Hips")
    # Chain loincloth with a brass plate.
    prim("cube", (0, -0.26, 0.9), (0.32, 0.03, 0.44), "iron", "Hips", bevel=0.01)
    for i, z in enumerate((1.04, 0.94, 0.84, 0.74)):
        prim("cube", (0.0, -0.285, z), (0.3, 0.025, 0.05), "steel", "Hips", bevel=0.008)
        for dx in (-0.1, 0.0, 0.1):
            prim("sphere", (dx + 0.05 * (i % 2), -0.3, z - 0.04), (0.06, 0.04, 0.06), "steel", "Hips", verts=8)
    prim("cube", (0, -0.27, 0.66), (0.34, 0.04, 0.05), "brass", "Hips")

    # Massive chest.
    prim("cube", (0, 0, 1.55), (1.0, 0.62, 0.7), "armor", "Spine", bevel=0.15)
    prim("cube", (0, 0, 1.24), (0.95, 0.6, 0.06), "iron", "Spine", bevel=0.01)
    prim("cube", (0, -0.31, 1.62), (0.4, 0.04, 0.22), "brass", "Spine", rot=(0, 0, 0), bevel=0.02)  # chest collar bar
    skull((0, -0.36, 1.58), 0.26, "Spine")
    for x in (-0.33, 0.33):  # cable ribs and a glowing rune each side
        prim("cyl", (x, -0.3, 1.38), (0.06, 0.06, 0.3), "iron", "Spine", rot=(8, 0, 0), verts=8)
        prim("cube", (x, -0.318, 1.64), (0.07, 0.015, 0.2), "rune", "Spine")
    prim("cyl", (0, 0, 1.92), (0.54, 0.46, 0.16), "iron", "Spine", verts=20)  # gorget

    # Tall U-shaped Khorne collar plates behind the head.
    for s in (-1, 1):
        prim("cube", (0.22 * s, 0.06, 2.12), (0.1, 0.12, 0.42), "brass", "Spine", rot=(0, 0, -12 * s), bevel=0.02)
        prim("cube", (0.22 * s, 0.04, 2.12), (0.06, 0.12, 0.34), "armor_dark", "Spine", rot=(0, 0, -12 * s))
    prim("cube", (0, 0.1, 1.97), (0.5, 0.1, 0.1), "brass", "Spine", bevel=0.02)


def pauldrons():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.72 * s
        c.pauldron_block(x, 1.76, s, 0.92, 0.7, 0.62, f"UpperArm_{side}", mat="armor", trim="brass", tilt=16)
        # Bone horns crowning each shoulder.
        spike((x - 0.1 * s, 0.0, 1.92), (x - 0.04 * s, 0.0, 2.2), 0.07, f"UpperArm_{side}")
        spike((x + 0.18 * s, -0.12, 1.9), (x + 0.4 * s, -0.2, 2.12), 0.07, f"UpperArm_{side}")
        spike((x + 0.18 * s, 0.14, 1.9), (x + 0.42 * s, 0.24, 2.1), 0.07, f"UpperArm_{side}")
        prim("sphere", (x + 0.37 * s, 0, 1.7), (0.1, 0.34, 0.26), "brass", f"UpperArm_{side}", verts=12)
        prim("sphere", (x + 0.4 * s, 0, 1.7), (0.06, 0.2, 0.14), "rune", f"UpperArm_{side}", verts=12)


ARMS = {
    "L": dict(sh=(0.62, 0, 1.65), el=(0.86, -0.1, 1.32), wr=(0.7, -0.42, 1.36)),
    "R": dict(sh=(-0.62, 0, 1.65), el=(-0.86, -0.1, 1.32), wr=(-0.7, -0.42, 1.36)),
}


def arms():
    for side, a in ARMS.items():
        s = 1 if side == "L" else -1
        limb(a["sh"], a["el"], 0.19, 0.16, "flesh", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.32, 0.32, 0.32), "armor", f"UpperArm_{side}")
        spike((a["el"][0] + 0.12 * s, a["el"][1] + 0.08, a["el"][2] + 0.04),
              (a["el"][0] + 0.3 * s, a["el"][1] + 0.16, a["el"][2] + 0.12), 0.06, f"UpperArm_{side}", "brass")
        limb(a["el"], a["wr"], 0.17, 0.2, "armor", f"Forearm_{side}")
        mid = tuple((Vector(a["el"]) + Vector(a["wr"])) / 2)
        prim("cyl", mid, (0.28, 0.28, 0.06), "brass", f"Forearm_{side}",
             rot=(-65, 0, 0), bevel=0.01, verts=16)
        d = (Vector(a["wr"]) - Vector(a["el"])).normalized()
        prim("sphere", tuple(Vector(a["wr"]) + d * 0.05), (0.3, 0.3, 0.3), "iron", f"Hand_{side}")


def head_and_pack():
    prim("sphere", (0, -0.02, 2.03), (0.46, 0.48, 0.48), "armor", "Head", verts=24)
    prim("cube", (0, -0.2, 1.93), (0.34, 0.1, 0.26), "iron", "Head", bevel=0.03)  # face plate
    prim("cube", (0, -0.255, 1.9), (0.26, 0.04, 0.18), "brass", "Head", bevel=0.01)  # grille
    for x in (-0.09, -0.03, 0.03, 0.09):  # snarling teeth
        prim("cube", (x, -0.28, 1.87), (0.025, 0.02, 0.1), "iron", "Head")
    prim("cube", (0, -0.225, 2.07), (0.38, 0.07, 0.09), "iron", "Head", bevel=0.02)  # brow
    for x in (-0.09, 0.09):
        prim("sphere", (x, -0.255, 2.06), (0.085, 0.03, 0.05), "eye", "Head", verts=10)
    for x in (-0.24, 0.24):  # tubes
        limb((x * 0.75, -0.16, 1.88), (x * 1.1, -0.04, 1.78), 0.03, 0.03, "iron", "Head", verts=8)
    # Swept-back horns.
    for s in (-1, 1):
        limb((0.2 * s, -0.04, 2.14), (0.42 * s, -0.04, 2.34), 0.08, 0.05, "bone", "Head", verts=10)
        limb((0.42 * s, -0.04, 2.34), (0.52 * s, -0.12, 2.62), 0.05, 0.0, "bone", "Head", verts=10)

    # Backpack: exhaust stacks plus three trophy-skull prongs, like the Berzerker kit.
    prim("cube", (0, 0.34, 1.5), (0.7, 0.4, 0.64), "armor", "Backpack", bevel=0.08)
    prim("cube", (0, 0.545, 1.5), (0.36, 0.03, 0.4), "iron", "Backpack", bevel=0.01)
    prim("cyl", (0, 0.56, 1.5), (0.16, 0.05, 0.16), "rune", "Backpack", rot=(90, 0, 0), verts=16)
    for s in (-1, 1):
        prim("cyl", (0.26 * s, 0.34, 1.9), (0.16, 0.16, 0.34), "iron", "Backpack", verts=12)
        prim("cyl", (0.26 * s, 0.34, 2.08), (0.2, 0.2, 0.05), "brass", "Backpack", verts=12)
    for s, (px, pz) in ((-1, (0.5, 2.35)), (1, (-0.5, 2.35)), (0, (0.0, 2.55))):
        top = (px, 0.32, pz)
        limb((0.0 if s == 0 else px * 0.4, 0.34, 1.85), top, 0.07, 0.05, "armor_dark", "Backpack", verts=10)
        skull((px, 0.3, pz + 0.12), 0.2, "Backpack", rot=(0, 0, 0))
        spike((px - 0.04, 0.3, pz + 0.22), (px - 0.04, 0.3, pz + 0.42), 0.04, "Backpack")


def chain_axe(side):
    """A chain axe held in the given hand: grip, brass collar, wide crimson blade rimmed with chain teeth."""
    s = 1 if side == "L" else -1
    wr = Vector(ARMS[side]["wr"])
    el = Vector(ARMS[side]["el"])
    px, py, pz = tuple(wr + (wr - el).normalized() * 0.05)
    bone = f"Axe_{side}"
    xf = pivot_rotation((px, py, pz), (28, 14 * s, 0))  # head leans forward and out

    prim("cyl", (px, py, pz + 0.5), (0.07, 0.07, 2.0), "iron", bone, xf=xf, verts=10)  # haft
    prim("cyl", (px, py, pz - 0.05), (0.1, 0.1, 0.42), "leather", bone, xf=xf, verts=10)  # grip
    prim("sphere", (px, py, pz - 0.52), (0.16, 0.16, 0.16), "brass", bone, xf=xf, verts=10)  # pommel
    for i, z in enumerate((-0.66, -0.78, -0.9)):  # dangling chain
        prim("cube", (px, py, pz + z), (0.07 if i % 2 else 0.03, 0.03 if i % 2 else 0.07, 0.1), "steel", bone, xf=xf,
             bevel=0.008)
    prim("cyl", (px, py, pz + 0.5), (0.15, 0.15, 0.1), "brass", bone, xf=xf, verts=12)  # lower collar
    prim("cyl", (px, py, pz + 1.36), (0.15, 0.15, 0.1), "brass", bone, xf=xf, verts=12)  # upper collar
    spike((px, py, pz + 1.38), (px, py, pz + 1.78), 0.06, bone, "brass", xf=xf)  # top spike

    # Axe head: a real bearded-axe profile, drawn as (outward, up) points relative to the grip and extruded.
    # Horns sweep up and down from the haft, with a deep curved cutting edge between them.
    def T(u, z):  # shrink the head toward the haft
        return (u * 0.62, 1.0 + (z - 1.05) * 0.62)

    def P(u, z):
        u, z = T(u, z)
        return (px + u * s, pz + z)

    outline = [(0.06, 0.56), (0.06, 1.32), (0.3, 1.58), (0.62, 1.68), (0.78, 1.42), (0.86, 1.08),
               (0.78, 0.74), (0.62, 0.48), (0.3, 0.42)]
    edge = [T(*o) for o in outline[3:8]]  # the cutting edge, top horn tip to bottom horn tip
    pts = [P(*o) for o in outline]
    if s < 0:
        pts.reverse()
    flat_poly(pts, py, 0.1, "iron", bone, xf=xf)  # chain bar / rim, slightly thicker than the face

    inner = [(0.14, 0.64), (0.14, 1.26), (0.32, 1.46), (0.58, 1.54), (0.68, 1.4), (0.74, 1.08), (0.68, 0.8),
             (0.58, 0.58), (0.32, 0.52)]
    pts = [P(*o) for o in inner]
    if s < 0:
        pts.reverse()
    flat_poly(pts, py - 0.01, 0.13, "armor", bone, xf=xf)  # crimson blade face, rim of iron left showing
    flat_poly([P(0.2, 0.9), P(0.5, 1.3), P(0.56, 1.22), P(0.28, 0.84)][::s], py - 0.07, 0.03, "brass", bone, xf=xf)
    prim("sphere", (px + 0.2 * s, py - 0.075, pz + 1.1), (0.09, 0.03, 0.09), "rune", bone, xf=xf, verts=10)

    # Chain teeth riding the cutting edge, each angled out along the edge normal.
    samples = []
    for (u0, z0), (u1, z1) in zip(edge, edge[1:]):
        for k in range(3):
            t = k / 3.0
            samples.append((u0 + (u1 - u0) * t, z0 + (z1 - z0) * t, u1 - u0, z1 - z0))
    samples.append((*edge[-1], 0.0, -0.3))
    for u, z, du, dz in samples:
        ln = math.hypot(du, dz) or 1.0
        nu, nz = -dz / ln, du / ln  # outward normal (edge runs top to bottom)
        ang = math.degrees(math.atan2(-nz, s * nu))
        prim("cube", (px + (u + nu * 0.04) * s, py, pz + z + nz * 0.04), (0.08, 0.09, 0.04), "steel", bone, xf=xf,
             rot=(0, ang, 0), bevel=0.006)

    # Back of the head: a heavy poll block and a rear spike to balance the blade.
    prim("cube", (px - 0.08 * s, py, pz + 0.95), (0.16, 0.12, 0.3), "brass", bone, xf=xf, bevel=0.025)
    spike((px - 0.14 * s, py, pz + 0.95), (px - 0.4 * s, py, pz + 1.0), 0.06, bone, "brass", xf=xf)


def _right_grip():
    wr = Vector(ARMS["R"]["wr"])
    el = Vector(ARMS["R"]["el"])
    return tuple(wr + (wr - el).normalized() * 0.05)


def _teeth(px, py, pz, xs, z0, z1, count, bone, xf):
    """Rows of chain teeth running along the haft (local Z) at each outward offset in xs, alternately angled."""
    for x in xs:
        for i in range(count):
            z = z0 + (z1 - z0) * i / (count - 1)
            prim("cube", (px + x, py, pz + z), (0.07, 0.11, 0.06), "steel", bone, xf=xf,
                 rot=(0, 35 if i % 2 else -35, 0), bevel=0.006)


def _spiked_ball(center, r, bone, xf, n=7):
    cx, cy, cz = center
    prim("sphere", center, (r * 2, r * 2, r * 2), "iron", bone, xf=xf, verts=14)
    dirs = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1), (0.7, -0.7, 0.2)]
    for d in dirs[:n]:
        dv = Vector(d).normalized()
        spike((cx + dv.x * r * 0.8, cy + dv.y * r * 0.8, cz + dv.z * r * 0.8),
              (cx + dv.x * r * 1.8, cy + dv.y * r * 1.8, cz + dv.z * r * 1.8), 0.045, bone, "brass", xf=xf)


def eviscerator():
    """The Eviscerator: a very long frame, a chainsaw bar on top of a crimson motor housing, a spiked ball with a skull
    behind the housing, and a spiked pommel. Held two-handed; here it rides in the right hand."""
    px, py, pz = _right_grip()
    bone = "Eviscerator"
    xf = pivot_rotation((px, py, pz), (36, 6, 0))  # leans forward, blade up

    D = 0.65  # how far the housing and bar sit below the original, long design: a shorter handle
    pz2 = pz - D
    prim("cyl", (px, py, pz + 0.25), (0.08, 0.08, 1.3), "iron", bone, xf=xf, verts=10)  # shaft
    prim("cyl", (px, py, pz), (0.12, 0.12, 0.5), "leather", bone, xf=xf, verts=10)  # rear grip
    prim("cyl", (px, py, pz + 0.5), (0.12, 0.12, 0.35), "leather", bone, xf=xf, verts=10)  # front grip
    for z in (-0.28, 0.27, 0.45, 0.68):
        prim("cyl", (px, py, pz + z), (0.15, 0.15, 0.06), "brass", bone, xf=xf, verts=12)
    prim("sphere", (px, py, pz - 0.4), (0.22, 0.22, 0.22), "brass", bone, xf=xf, verts=10)
    spike((px, py, pz - 0.45), (px, py, pz - 0.75), 0.06, bone, "brass", xf=xf)  # pommel spike

    # Motor housing (compact, so the blade carries the length).
    H = 1.4  # housing centre height
    prim("cube", (px, py, pz2 + H), (0.26, 0.24, 0.2), "armor", bone, xf=xf, bevel=0.03)
    prim("cube", (px, py, pz2 + H), (0.3, 0.18, 0.08), "brass", bone, xf=xf, bevel=0.015)
    prim("cyl", (px + 0.1, py + 0.13, pz2 + H + 0.03), (0.06, 0.06, 0.18), "iron", bone, xf=xf, verts=8)  # exhaust
    prim("sphere", (px, py - 0.125, pz2 + H), (0.07, 0.03, 0.07), "rune", bone, xf=xf, verts=10)

    # Chainsaw bar and teeth: base sits lower and the tip reaches well past the old length.
    B0, B1 = 1.5, 4.3  # bar base and tip
    bar = [(-0.14, B0), (0.14, B0), (0.16, B1 - 0.23), (0.09, B1), (-0.09, B1), (-0.16, B1 - 0.23)]
    flat_poly([(px + u, pz2 + z) for u, z in bar], py, 0.1, "iron", bone, xf=xf)
    inner = [(-0.1, B0 + 0.1), (0.1, B0 + 0.1), (0.11, B1 - 0.28), (0.05, B1 - 0.11), (-0.05, B1 - 0.11),
             (-0.11, B1 - 0.28)]
    flat_poly([(px + u, pz2 + z) for u, z in inner], py - 0.01, 0.13, "steel", bone, xf=xf)
    _teeth(px, py, pz2, (-0.185, 0.185), B0 + 0.1, B1 - 0.13, 20, bone, xf)
    prim("sphere", (px, py, pz2 + B1 - 0.03), (0.17, 0.12, 0.17), "steel", bone, xf=xf, verts=10)  # nose roller
    spike((px, py, pz2 + B1 + 0.05), (px, py, pz2 + B1 + 0.29), 0.05, bone, "brass", xf=xf)

    # Spiked ball and skull trailing from the housing.
    ball = (px - 0.34, py + 0.05, pz2 + H + 0.05)
    limb((px - 0.13, py, pz2 + H), (px - 0.26, py + 0.04, pz2 + H + 0.05), 0.04, 0.04, "iron", bone, xf=xf, verts=8)
    _spiked_ball(ball, 0.15, bone, xf)
    skull((ball[0], ball[1] - 0.02, ball[2] + 0.27), 0.17, bone, xf=xf)
    return xf


def heavy_chainaxe():
    """The Heavy Chainaxe: a stout haft with a huge double-bitted chain axe head and a spiked ball counterweight."""
    px, py, pz = _right_grip()
    bone = "Heavy_Chainaxe"
    xf = pivot_rotation((px, py, pz), (34, 6, 0))

    prim("cyl", (px, py, pz + 0.55), (0.09, 0.09, 2.2), "iron", bone, xf=xf, verts=10)
    prim("cyl", (px, py, pz), (0.13, 0.13, 0.5), "leather", bone, xf=xf, verts=10)
    prim("cyl", (px, py, pz + 0.45), (0.13, 0.13, 0.35), "leather", bone, xf=xf, verts=10)
    for z in (-0.28, 0.28, 0.55, 0.97):
        prim("cyl", (px, py, pz + z), (0.16, 0.16, 0.06), "brass", bone, xf=xf, verts=12)
    _spiked_ball((px, py, pz - 0.62), 0.13, bone, xf, n=6)  # counterweight

    # Head collar and a big double-bitted blade: horns sweep up and down, cutting edges outward on both sides.
    prim("cube", (px, py, pz + 1.55), (0.3, 0.2, 0.5), "brass", bone, xf=xf, bevel=0.04)
    K = 0.42
    def _k(pts):
        return [(u * K, 1.55 + (z - 1.55) * K) for u, z in pts]

    outline = [(0.1, 1.15), (0.1, 1.95), (0.5, 2.3), (0.95, 2.4), (1.15, 2.0), (1.22, 1.55), (1.15, 1.1), (0.95, 0.7),
               (0.5, 0.8)]
    inner = [(0.2, 1.3), (0.2, 1.85), (0.55, 2.1), (0.88, 2.2), (1.0, 1.95), (1.06, 1.55), (1.0, 1.2), (0.88, 0.92),
             (0.55, 1.0)]
    outline, inner = _k(outline), _k(inner)
    for sd in (1, -1):
        def P(pt, sd=sd):
            return (px + pt[0] * sd, pz + pt[1])
        o = [P(p) for p in outline]
        i = [P(p) for p in inner]
        if sd < 0:
            o.reverse()
            i.reverse()
        flat_poly(o, py, 0.12, "iron", bone, xf=xf)
        flat_poly(i, py - 0.01, 0.15, "armor", bone, xf=xf)
        flat_poly([P(_k([(0.3, 1.45)])[0]), P(_k([(0.75, 1.95)])[0]), P(_k([(0.85, 1.85)])[0]), P(_k([(0.4, 1.35)])[0])][::sd], py - 0.08, 0.03, "brass",
                  bone, xf=xf)
        prim("sphere", (px + 0.33 * sd, py - 0.085, pz + 1.56), (0.09, 0.03, 0.09), "rune", bone, xf=xf, verts=10)
        edge = outline[3:8]
        for (u0, z0), (u1, z1) in zip(edge, edge[1:]):
            for k in range(4):
                t = k / 4.0
                u, z = u0 + (u1 - u0) * t, z0 + (z1 - z0) * t
                du, dz = u1 - u0, z1 - z0
                ln = math.hypot(du, dz)
                nu, nz = -dz / ln, du / ln
                ang = math.degrees(math.atan2(-nz, sd * nu))
                prim("cube", (px + (u + nu * 0.05) * sd, py, pz + z + nz * 0.05), (0.07, 0.08, 0.04), "steel", bone,
                     xf=xf, rot=(0, ang, 0), bevel=0.006)
    spike((px, py, pz + 1.85), (px, py, pz + 2.3), 0.07, bone, "brass", xf=xf)  # top spike
    return xf


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.0), (0, 0, 1.2)),
    ("Spine", "Hips", (0, 0, 1.2), (0, 0, 1.8)),
    ("Head", "Spine", (0, 0, 1.8), (0, 0, 2.2)),
    ("Backpack", "Spine", (0, 0.2, 1.7), (0, 0.4, 1.3)),
]
for _side, _a in ARMS.items():
    _s = 1 if _side == "L" else -1
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    _grip = tuple(_wr + _dir * 0.05)
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.14)),
        (f"Axe_{_side}", f"Hand_{_side}", _grip, (_grip[0], _grip[1], _grip[2] + 0.4)),
    ]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.25 * _s, 0, 1.0), (0.25 * _s, 0, 0.62)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.25 * _s, 0, 0.62), (0.25 * _s, 0, 0.2)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.25 * _s, 0, 0.2), (0.25 * _s, -0.3, 0.05)),
    ]

legs()
torso()
pauldrons()
arms()
head_and_pack()
if ONLY in (None, "dual"):
    chain_axe("L")
    chain_axe("R")
if ONLY in (None, "heavy"):
    XF_HEAVY = heavy_chainaxe()
if ONLY in (None, "evisc"):
    XF_EVISC = eviscerator()

# Two-handed weapons ride in the right hand; the game scales away whichever weapons are not wielded.
_g = _right_grip()
BONES += [
    ("Heavy_Chainaxe", "Hand_R", _g, (_g[0], _g[1], _g[2] + 0.4)),
    ("Eviscerator", "Hand_R", _g, (_g[0], _g[1], _g[2] + 0.4)),
]

# Grip markers: where the two hands hold each two-handed weapon (rear hand A, front hand B). The game solves both arms
# onto these, so they must sit on the shaft. They are unweighted helper bones that ride on the weapon bone.
for _prefix, _bone, _xf in (("Heavy", "Heavy_Chainaxe", globals().get("XF_HEAVY")),
                            ("Evisc", "Eviscerator", globals().get("XF_EVISC"))):
    if _xf is None:
        continue
    for _name, _dz in (("GripA", 0.0), ("GripB", 0.42)):
        _p = _xf @ c.v(_g[0], _g[1], _g[2] + _dz)
        _p = Vector((_p.x, _p.y, _p.z)) / c.S
        BONES.append((f"{_prefix}_{_name}", _bone, tuple(_p), tuple(_p + Vector((0, 0, 0.05)))))

armature = c.build_armature(BONES)
mesh = c.assemble("KhorneBerserker", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.4, distance=1.25)
