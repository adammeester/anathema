"""Builds the Drukhari Mandrake: a gaunt, shadow-skinned assassin. Blue-black skin carved with glowing baleflame runes,
a long mane of white hair bound in a topknot with a red tie, pointed ears and burning green eyes, a bare runed torso,
a long ragged tan skirt tied with a red sash and cords, bare clawed feet. The right hand holds a long curved glimmersteel
blade; the left is wreathed in green baleflame.

Based on the reference artwork and Kill Team miniatures (used as a guide only, nothing from them is imported).

    blender -b --python Art/mandrake.py                    # writes Assets/Models/Mandrake.fbx
    blender -b --python Art/mandrake.py -- --render DIR    # also renders preview PNGs into DIR

About 1.9 m tall and slender, same skeleton naming as the other characters (Hips, Spine, Head, UpperArm_L, Thigh_L, ...)
so ProceduralWalk and the arm poses (Glimmersteel swings UpperArm_R) work unchanged.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "Mandrake.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(1.0)

mat("skin", (0.05, 0.065, 0.09), rough=0.55)
mat("skin_hi", (0.09, 0.11, 0.15), rough=0.5)
mat("rune", (0.4, 1.0, 0.3), emit=(0.35, 1.0, 0.25), emit_strength=5.0)
mat("bale", (0.55, 1.0, 0.35), emit=(0.45, 1.0, 0.3), emit_strength=8.0)
mat("bale_teal", (0.25, 0.95, 0.75), emit=(0.2, 0.9, 0.7), emit_strength=6.0)
mat("eye", (0.6, 1.0, 0.5), emit=(0.5, 1.0, 0.4), emit_strength=10.0)
mat("hair", (0.88, 0.88, 0.86), rough=0.8)
mat("hair_shade", (0.68, 0.68, 0.68), rough=0.85)
mat("cloth", (0.55, 0.4, 0.28), rough=0.9)
mat("cloth_dark", (0.36, 0.24, 0.16), rough=0.95)
mat("red", (0.5, 0.06, 0.05), rough=0.8)
mat("blade", (0.72, 0.74, 0.78), metallic=0.95, rough=0.2)
mat("blade_dark", (0.3, 0.3, 0.34), metallic=0.9, rough=0.35)
mat("grip", (0.15, 0.08, 0.06), rough=0.7)
mat("bone", (0.82, 0.75, 0.55), rough=0.6)
mat("claw", (0.02, 0.02, 0.03), rough=0.4)


def legs():
    """Long, thin legs (mostly hidden by the skirt) and bare clawed feet."""
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.11 * s
        prim("cube", (x, -0.08, 0.04), (0.1, 0.24, 0.07), "skin", f"Foot_{side}", bevel=0.025)
        for k in (-1, 0, 1):  # three long toes ending in claws
            tx = x + 0.03 * k
            limb((tx, -0.18, 0.04), (tx + 0.01 * k, -0.27, 0.02), 0.018, 0.012, "skin", f"Foot_{side}", verts=8)
            limb((tx + 0.01 * k, -0.27, 0.02), (tx + 0.015 * k, -0.32, 0.0), 0.011, 0.0, "claw", f"Foot_{side}", verts=6)
        limb((x, 0, 0.07), (x, 0, 0.5), 0.045, 0.06, "skin", f"Shin_{side}", verts=12)
        prim("sphere", (x, -0.01, 0.5), (0.09, 0.09, 0.09), "skin", f"Shin_{side}", verts=12)
        limb((x, 0, 0.5), (x, 0, 0.94), 0.06, 0.08, "skin", f"Thigh_{side}", verts=12)


def torso():
    """Lean, wiry male torso: broad shoulders and a flat chest tapering to a narrow waist, carved with jagged runes.
    Also the red sash and the ragged skirt."""
    prim("cube", (0, 0, 1.0), (0.26, 0.19, 0.16), "skin", "Hips", bevel=0.05)
    # Narrow waist widening into a broad ribcage (a V-taper), leaning forward a touch.
    limb((0, 0, 1.06), (0, -0.01, 1.22), 0.11, 0.15, "skin", "Spine", verts=16)
    prim("cube", (0, -0.01, 1.33), (0.44, 0.24, 0.24), "skin", "Spine", bevel=0.07)
    # Flat, angular chest plates (wider than tall) with a shallow groove down the sternum.
    for x in (-0.09, 0.09):
        prim("cube", (x, -0.115, 1.35), (0.17, 0.04, 0.1), "skin_hi", "Spine", rot=(0, 0, 0), bevel=0.02)
    prim("cube", (0, -0.135, 1.33), (0.012, 0.01, 0.14), "skin", "Spine")
    # Lats flaring out under the arms, and the trapezius sloping from neck to shoulders.
    for sd in (-1, 1):
        prim("cube", (0.17 * sd, 0.0, 1.24), (0.1, 0.2, 0.2), "skin", "Spine", rot=(0, -18 * sd, 0), bevel=0.04)
        limb((0.04 * sd, -0.01, 1.5), (0.24 * sd, -0.01, 1.43), 0.05, 0.06, "skin", "Spine", verts=10)
    for k in range(3):  # abdomen
        for x in (-0.04, 0.04):
            prim("cube", (x, -0.11, 1.21 - k * 0.05), (0.06, 0.03, 0.04), "skin_hi", "Spine", bevel=0.012)
    limb((0, -0.02, 1.42), (0, -0.04, 1.54), 0.055, 0.05, "skin", "Spine", verts=10)  # neck

    # Glowing runes: jagged, angular lines (like the miniatures' baleflame markings) rather than rounded swirls.
    def zigzag(points, bone, r1=0.007, r2=0.006):
        for a, b in zip(points, points[1:]):
            limb(a, b, r1, r2, "rune", bone, verts=6)

    y = -0.142
    for sd in (-1, 1):
        # From the collarbone down and in toward the sternum, then a spur across the ribs.
        zigzag([(0.2 * sd, y + 0.02, 1.43), (0.13 * sd, y, 1.4), (0.15 * sd, y, 1.33), (0.05 * sd, y, 1.29)], "Spine")
        zigzag([(0.15 * sd, y, 1.33), (0.2 * sd, y + 0.02, 1.27)], "Spine", 0.005, 0.003)
        # Down the obliques in a lightning stroke.
        zigzag([(0.15 * sd, y + 0.03, 1.22), (0.11 * sd, y + 0.02, 1.16), (0.13 * sd, y + 0.02, 1.1), (0.08 * sd, y + 0.04, 1.05)],
               "Spine", 0.006, 0.004)
        # Back: a long jagged line down each side of the spine.
        zigzag([(0.05 * sd, 0.125, 1.46), (0.1 * sd, 0.125, 1.36), (0.06 * sd, 0.12, 1.26), (0.1 * sd, 0.1, 1.14)], "Spine")
    zigzag([(0, y, 1.26), (0.02, y + 0.005, 1.2), (-0.02, y + 0.005, 1.14), (0, y + 0.01, 1.08)], "Spine", 0.006, 0.004)

    # Red sash knotted at the front, with two hanging cords.
    torus((0, 0, 1.03), 0.16, 0.03, "red", "Hips", squash_y=0.72)
    prim("sphere", (0.04, -0.13, 1.02), (0.07, 0.04, 0.06), "red", "Hips", verts=10)
    for x, l in ((0.03, 0.38), (0.07, 0.3)):
        limb((x, -0.14, 1.0), (x + 0.02, -0.16, 1.0 - l), 0.012, 0.009, "red", "Hips", verts=6)
    # A small bone trophy hanging from the sash.
    prim("sphere", (-0.1, -0.13, 0.92), (0.05, 0.04, 0.055), "bone", "Hips", verts=10)

    # Long ragged skirt: front, back and side panels with torn hems, darker inner layer.
    def ragged(x0, x1, top, bottom, teeth):
        pts = [(x0, top), (x1, top)]
        n = teeth * 2
        for i in range(n + 1):
            t = i / n
            x = x1 + (x0 - x1) * t
            z = bottom + (0.08 if i % 2 else 0.0) + 0.03 * math.sin(i * 1.7)
            pts.append((x * 1.15, z))
        return pts

    flat_poly(ragged(-0.15, 0.15, 1.02, 0.06, 4), -0.12, 0.02, "cloth", "Hips")
    flat_poly(ragged(-0.17, 0.17, 1.02, 0.03, 5), 0.11, 0.02, "cloth", "Hips")
    flat_poly(ragged(-0.14, 0.14, 0.98, 0.1, 3), -0.1, 0.012, "cloth_dark", "Hips")
    for s in (-1, 1):  # side panels, angled out like the flared skirts of the miniatures
        side = [(0.0, 1.02), (0.12, 1.0), (0.18, 0.4), (0.22, 0.05), (0.14, 0.12), (0.08, 0.03), (0.02, 0.1), (-0.02, 0.4)]
        xf = c.pivot_rotation((0, 0, 0), (0, 0, 90 * s))
        flat_poly([(px, pz) for px, pz in side], 0.16 * s, 0.02, "cloth", "Hips", xf=xf)
    for x in (-0.09, 0.0, 0.09):  # folds and patch seams
        prim("cube", (x, -0.135, 0.55), (0.012, 0.008, 0.75), "cloth_dark", "Hips")
    prim("cube", (0.06, -0.133, 0.38), (0.09, 0.006, 0.1), "cloth_dark", "Hips")


def head():
    """Gaunt head with a strong jaw, pointed ears and burning eyes, mostly hidden behind long white hair that hangs
    forward over the face; a red-tied topknot on top."""
    prim("sphere", (0, -0.05, 1.64), (0.15, 0.17, 0.21), "skin", "Head", verts=16)
    prim("cube", (0, -0.12, 1.56), (0.1, 0.06, 0.06), "skin", "Head", bevel=0.015)  # jaw
    for sd in (-1, 1):
        prim("sphere", (0.038 * sd, -0.13, 1.655), (0.045, 0.02, 0.02), "eye", "Head", verts=8)
        # Long pointed ears swept back through the hair.
        limb((0.07 * sd, -0.04, 1.66), (0.19 * sd, 0.05, 1.74), 0.032, 0.0, "skin", "Head", verts=8)
    prim("cube", (0, -0.155, 1.56), (0.05, 0.01, 0.025), "bale", "Head")  # glowing maw

    # Hair: a cap over the skull, a heavy mane down the back and over the shoulders.
    prim("sphere", (0, -0.02, 1.7), (0.19, 0.21, 0.18), "hair", "Head", verts=16)
    limb((0, 0.05, 1.68), (0, 0.15, 1.08), 0.12, 0.08, "hair", "Head", verts=14)
    for sd in (-1, 1):
        limb((0.09 * sd, 0.0, 1.68), (0.17 * sd, 0.02, 1.3), 0.05, 0.022, "hair", "Head", verts=10)
        limb((0.07 * sd, 0.07, 1.68), (0.14 * sd, 0.13, 1.12), 0.05, 0.025, "hair_shade", "Head", verts=10)
    # Strands falling forward over the face from the brow down past the chin. A narrow gap off-centre lets the left
    # eye glare through; the right eye glows between thinner strands.
    for x, w, shade in ((-0.075, 0.03, "hair"), (-0.042, 0.016, "hair_shade"), (-0.01, 0.022, "hair"),
                        (0.1, 0.03, "hair"), (0.125, 0.022, "hair_shade")):
        limb((x * 0.8, -0.13, 1.76), (x * 1.15, -0.175, 1.46), w, w * 0.4, shade, "Head", verts=8)
    # Topknot bound with a red tie, a short plume above.
    prim("sphere", (0, 0.01, 1.82), (0.08, 0.08, 0.08), "hair", "Head", verts=12)
    torus((0, 0.01, 1.86), 0.035, 0.014, "red", "Head")
    limb((0, 0.01, 1.87), (0, 0.06, 1.97), 0.03, 0.01, "hair", "Head", verts=8)
    for sd in (-1, 1):
        limb((0.03 * sd, 0.01, 1.86), (0.09 * sd, 0.04, 1.89), 0.008, 0.004, "red", "Head", verts=6)


# Right arm hangs forward holding the blade low; left arm reaches out to the side, wreathed in baleflame.
ARMS = {
    "L": dict(sh=(0.27, -0.01, 1.42), el=(0.41, -0.06, 1.17), wr=(0.53, -0.2, 1.03)),
    "R": dict(sh=(-0.27, -0.01, 1.42), el=(-0.35, -0.04, 1.15), wr=(-0.34, -0.22, 0.99)),
}


def arms():
    for side, a in ARMS.items():
        bone_ua, bone_fa, bone_h = f"UpperArm_{side}", f"Forearm_{side}", f"Hand_{side}"
        prim("sphere", a["sh"], (0.15, 0.14, 0.14), "skin", bone_ua, verts=12)  # deltoid
        limb(a["sh"], a["el"], 0.065, 0.048, "skin", bone_ua, verts=12)
        prim("sphere", a["el"], (0.085, 0.085, 0.085), "skin", bone_ua, verts=10)
        limb(a["el"], a["wr"], 0.055, 0.036, "skin", bone_fa, verts=12)
        # Jagged runes running down the outside of the arm.
        sh, el, wr = Vector(a["sh"]), Vector(a["el"]), Vector(a["wr"])
        out = Vector((1 if side == "L" else -1, -0.4, 0)).normalized()
        for (a0, a1, bone, rad) in ((sh, el, bone_ua, 0.066), (el, wr, bone_fa, 0.056)):
            pts = []
            for k in range(5):
                p = a0.lerp(a1, 0.1 + k * 0.2) + out * rad + Vector((0, 0, 0.012 if k % 2 else -0.012))
                p += Vector((0, -0.015 if k % 2 else 0.015, 0))
                pts.append(tuple(p))
            for q0, q1 in zip(pts, pts[1:]):
                limb(q0, q1, 0.006, 0.005, "rune", bone, verts=6)
        # Long clawed hand.
        d = (wr - el).normalized()
        palm = wr + d * 0.05
        prim("sphere", tuple(palm), (0.07, 0.06, 0.08), "skin", bone_h, verts=10)
        for k in (-1, 0, 1, 2):
            base = palm + d * 0.03 + Vector((0.018 * k, -0.01 * k, 0))
            tip = base + d * 0.09 + Vector((0, -0.02, -0.02))
            limb(tuple(base), tuple(tip), 0.011, 0.0, "claw", bone_h, verts=6)


def baleflame():
    """Licks of green and teal fire curling up off the left hand."""
    bone = "Hand_L"
    wr = Vector(ARMS["L"]["wr"])
    for k in range(7):
        a = math.radians(k * 51)
        base = wr + Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.0))
        tip = base + Vector((math.cos(a) * 0.05 + 0.02, math.sin(a) * 0.04 - 0.02, 0.14 + 0.05 * (k % 3)))
        limb(tuple(base), tuple(tip), 0.03, 0.0, "bale" if k % 2 == 0 else "bale_teal", bone, verts=8)
    prim("sphere", tuple(wr + Vector((0.02, -0.02, 0.0))), (0.11, 0.11, 0.1), "bale_teal", bone, verts=12)


def glimmersteel():
    """Long curved blade in the right hand: grip with a bone guard, a sweeping single-edged blade with a hooked tip."""
    bone = "Hand_R"
    wr = Vector(ARMS["R"]["wr"])
    hx, hy, hz = wr.x - 0.01, wr.y - 0.04, wr.z
    limb((hx, hy + 0.08, hz + 0.02), (hx, hy - 0.08, hz - 0.02), 0.022, 0.02, "grip", bone, verts=10)
    prim("sphere", (hx, hy + 0.1, hz + 0.025), (0.04, 0.04, 0.04), "bone", bone, verts=8)  # pommel
    prim("cube", (hx, hy - 0.09, hz - 0.02), (0.1, 0.03, 0.03), "bone", bone, bevel=0.008)  # guard
    # The blade sweeps forward and up in a long curve, drawn as a strip in the YZ plane.
    pts_edge, pts_back = [], []
    for i in range(12):
        t = i / 11
        y = hy - 0.11 - t * 0.72
        z = hz - 0.03 + 0.32 * t * t
        w = 0.06 * (1 - 0.6 * t) + 0.015
        pts_edge.append((y, z - w * 0.4))
        pts_back.append((y + 0.01, z + w))
    outline = pts_edge + [(pts_back[-1][0] - 0.06, pts_back[-1][1] + 0.08)] + list(reversed(pts_back))  # hooked tip
    # flat_poly draws in XZ, so build in XZ and turn it 90 degrees about Z to lie in YZ around the hand.
    xf = c.pivot_rotation((hx, hy, 0), (0, 0, 90))
    flat_poly([(hx + (py - hy), pz) for py, pz in outline], hy, 0.012, "blade", bone, xf=xf)
    flat_poly([(hx + (py - hy) + 0.005, pz + 0.01) for py, pz in pts_back], hy, 0.016, "blade_dark", bone, xf=xf)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.1)),
    ("Hips", "Root", (0, 0, 0.96), (0, 0, 1.06)),
    ("Spine", "Hips", (0, 0, 1.06), (0, -0.02, 1.44)),
    ("Head", "Spine", (0, -0.03, 1.5), (0, -0.03, 1.9)),
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
        (f"Thigh_{_side}", "Hips", (0.11 * _s, 0, 0.96), (0.11 * _s, 0, 0.5)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.11 * _s, 0, 0.5), (0.11 * _s, 0, 0.07)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.11 * _s, 0, 0.07), (0.11 * _s, -0.2, 0.02)),
    ]

legs()
torso()
head()
arms()
baleflame()
glimmersteel()

armature = c.build_armature(BONES)
mesh = c.assemble("Mandrake", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.0, distance=0.7)
