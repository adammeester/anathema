"""Builds the Grey Knight Interceptor: chunky gunmetal power armour, storm bolter, Nemesis halberd.

Based on the reference images in Art/Reference (used as a guide only, nothing from them is imported).

    blender -b --python Art/grey_knight.py                    # writes Assets/Models/GreyKnight.fbx
    blender -b --python Art/grey_knight.py -- --render DIR    # also renders preview PNGs into DIR
"""
import math
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
mat("black", (0.035, 0.035, 0.045), metallic=0.5, rough=0.35)
mat("steel",(0.8, 0.82, 0.85), metallic=0.65, rough=0.25)
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
        x = 0.25 * s
        prim("cube", (x, -0.1, 0.12), (0.38, 0.6, 0.24), "armor", f"Foot_{side}", bevel=0.08)
        prim("cube", (x, -0.1, 0.02), (0.4, 0.62, 0.05), "armor_dark", f"Foot_{side}", bevel=0.01)
        limb((x, 0, 0.2), (x, 0, 0.62), 0.24, 0.21, "armor", f"Shin_{side}")
        # Greave: gold-trimmed plate, purity seal and wax.
        prim("cube", (x, -0.23, 0.4), (0.36, 0.04, 0.42), "gold", f"Shin_{side}", bevel=0.01)
        prim("cube", (x, -0.245, 0.4), (0.3, 0.05, 0.36), "armor_dark", f"Shin_{side}", bevel=0.02)
        prim("cube", (x, -0.28, 0.34), (0.1, 0.015, 0.24), "parchment", f"Shin_{side}")
        prim("sphere", (x, -0.29, 0.45), (0.06, 0.04, 0.06), "wax", f"Shin_{side}", verts=10)
        prim("sphere", (x, -0.1, 0.62), (0.38, 0.34, 0.34), "armor", f"Shin_{side}")  # knee
        prim("sphere", (x, -0.27, 0.63), (0.15, 0.1, 0.17), "gold", f"Shin_{side}", verts=12)  # knee skull
        limb((x, 0, 0.64), (x, 0, 1.02), 0.21, 0.25, "armor", f"Thigh_{side}")


def torso():
    prim("cube", (0, 0, 1.08), (0.82, 0.52, 0.3), "armor", "Hips", bevel=0.09)
    prim("cyl", (0, 0, 1.22), (0.88, 0.58, 0.08), "armor_dark", "Hips", bevel=0.01, verts=24)
    prim("cube", (0, -0.275, 1.22), (0.14, 0.03, 0.12), "gold", "Hips", bevel=0.01)
    prim("cube", (0, -0.265, 0.93), (0.3, 0.02, 0.5), "parchment", "Hips")  # tabard
    prim("cube", (0, -0.27, 0.69), (0.32, 0.03, 0.04), "gold", "Hips")
    prim("sphere", (0, -0.285, 1.08), (0.07, 0.04, 0.07), "wax", "Hips", verts=10)

    prim("cube", (0, 0, 1.55), (1.0, 0.62, 0.7), "armor", "Spine", bevel=0.15)
    prim("cube", (0, 0, 1.24), (0.97, 0.6, 0.05), "armor_dark", "Spine", bevel=0.01)
    prim("cube", (0, -0.328, 1.6), (0.17, 0.03, 0.17), "gold", "Spine", rot=(0, 45, 0), bevel=0.01)  # chest badge
    prim("sphere", (0, -0.345, 1.6), (0.07, 0.04, 0.07), "red", "Spine", verts=10)
    for x in (-0.26, 0.26):  # purity seals
        prim("cube", (x, -0.322, 1.36), (0.07, 0.015, 0.22), "parchment", "Spine")
        prim("sphere", (x, -0.332, 1.47), (0.05, 0.03, 0.05), "wax", "Spine", verts=10)
    # High gorget collar the head sits down inside, like the reference.
    prim("cyl", (0, 0, 1.92), (0.62, 0.54, 0.2), "armor", "Spine", bevel=0.02)
    prim("cyl", (0, 0, 2.0), (0.64, 0.56, 0.04), "gold", "Spine", bevel=0.005)


def pauldron(s, bone):
    """Big layered shoulder dome: a rounded cap over a banded skirt with a gold ring trim."""
    x, z = 0.72 * s, 1.74
    c.pauldron_block(x, z, s, 0.94, 0.78, 0.68, bone, mat="armor", trim="gold", tilt=16)
    return x, z


def pauldrons():
    # Right (-X): red heraldic plate with the white cross. Left (+X): gold lettering and scrolls.
    x, z = pauldron(-1, "UpperArm_R")
    # Whole dome split down the middle: white front half, red rear half (shells just proud of the base dome).
    w, d, h, tilt = 0.94, 0.78, 0.68, 16
    zc, cut = z - 0.1 * h, -0.4 * h
    for half, m in ((-1, "white"), (1, "red")):
        c.clipped_ellipsoid((x, 0, zc), (w * 1.015, d * 1.015, 1.2 * h * 1.015), cut * 1.015 + 0.01, None, m,
                            "UpperArm_R", rot=(0, -tilt, 0), half=half)
    prim("cube", (x - 0.2, -0.34, z - 0.12), (0.22, 0.02, 0.3), "parchment", "UpperArm_R")  # hanging scroll
    prim("sphere", (x - 0.2, -0.35, z + 0.02), (0.06, 0.03, 0.06), "wax", "UpperArm_R", verts=10)

    x, z = pauldron(1, "UpperArm_L")
    # Book resting on top of the left pauldron: dark cover, page block, gold clasp and corner studs.
    bx, bz, br = x + 0.05, z + 0.33, (0, 16, 0)
    prim("cube", (bx, -0.02, bz), (0.4, 0.3, 0.09), "armor_dark", "UpperArm_L", rot=br, bevel=0.015)  # cover
    prim("cube", (bx, -0.02, bz + 0.005), (0.37, 0.28, 0.1), "parchment", "UpperArm_L", rot=br)  # pages
    prim("cube", (bx, -0.02, bz + 0.05), (0.4, 0.3, 0.02), "armor_dark", "UpperArm_L", rot=br, bevel=0.01)  # top cover
    prim("cube", (bx, -0.02, bz - 0.01), (0.5, 0.4, 0.09), "red", "UpperArm_L", rot=br, bevel=0.015)  # red frame
    # Sword laid across the book (shoulder to shoulder): pommel toward his head (-X), point outward. Offsets follow the tilt.
    ca, sa = math.cos(math.radians(16)), math.sin(math.radians(16))

    def on_book(dx, dz=0.085):
        return (bx + dx * ca + dz * sa, -0.02, bz - dx * sa + dz * ca)

    prim("cube", on_book(0.27), (0.52, 0.04, 0.02), "steel", "UpperArm_L", rot=br)  # blade
    prim("cube", on_book(0.0, 0.09), (0.035, 0.2, 0.03), "gold", "UpperArm_L", rot=br)  # crossguard
    prim("cube", on_book(-0.09), (0.13, 0.04, 0.03), "armor_dark", "UpperArm_L", rot=br)  # grip
    prim("sphere", on_book(-0.17), (0.07, 0.07, 0.06), "gold", "UpperArm_L", rot=br, verts=10)  # pommel
    prim("cube", (x + 0.1, -0.34, z - 0.14), (0.32, 0.02, 0.18), "parchment", "UpperArm_L")  # script banner
    prim("cube", (x + 0.28, 0.18, z + 0.0), (0.24, 0.02, 0.3), "parchment", "UpperArm_L")  # rear scroll


def arms():
    arms_def = {
        "L": dict(sh=(0.62, 0, 1.65), el=(0.86, -0.08, 1.3), wr=(0.68, -0.32, 1.1)),
        "R": dict(sh=(-0.62, 0, 1.65), el=(-0.86, -0.1, 1.32), wr=(-0.7, -0.5, 1.38)),
    }
    from mathutils import Vector
    for side, a in arms_def.items():
        limb(a["sh"], a["el"], 0.19, 0.17, "armor", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.34, 0.34, 0.34), "armor", f"UpperArm_{side}")
        limb(a["el"], a["wr"], 0.18, 0.2, "armor", f"Forearm_{side}")
        prim("sphere", tuple(Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.05),
             (0.32, 0.32, 0.32), "armor_dark", f"Hand_{side}")


def head_and_pack():
    prim("sphere", (0, -0.02, 2.04), (0.46, 0.48, 0.48), "armor", "Head", verts=24)
    prim("cube", (0, -0.245, 2.05), (0.36, 0.05, 0.12), "armor_dark", "Head", bevel=0.02)  # visor
    for x in (-0.07, 0.07):
        prim("sphere", (x, -0.27, 2.05), (0.08, 0.03, 0.06), "cyan", "Head", verts=10)
    prim("cyl", (0, -0.25, 1.93), (0.14, 0.08, 0.14), "steel", "Head", rot=(90, 0, 0), verts=12)  # vox grille
    prim("cube", (0, 0, 2.3), (0.04, 0.34, 0.07), "gold", "Head", bevel=0.01)  # crest

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
    # Black casing with a gold Inquisitorial "I" and two barrels, like the reference.
    x, z = -0.82, 1.6
    gz = z + 0.02  # barrel axis height
    prim("cube", (x, -0.52, z), (0.2, 0.44, 0.22), "black", "Bolter", bevel=0.03)  # receiver
    prim("cube", (x, -0.4, z + 0.125), (0.1, 0.2, 0.04), "armor_dark", "Bolter", bevel=0.008)  # rear sight
    prim("cube", (x, -0.58, z + 0.125), (0.14, 0.22, 0.025), "steel", "Bolter", bevel=0.005)  # top rail
    prim("cube", (x - 0.13, -0.42, z - 0.06), (0.12, 0.22, 0.2), "armor_dark", "Bolter", bevel=0.02)  # ammo box
    prim("cube", (x - 0.13, -0.42, z + 0.045), (0.125, 0.225, 0.016), "gold", "Bolter")  # ammo box trim
    # Twin barrels with collars, muzzle brakes and dark bores, poking out of the front of the casing.
    for dx in (-0.06, 0.06):
        prim("cyl", (x + dx, -0.81, gz), (0.075, 0.075, 0.14), "armor_dark", "Bolter", rot=(90, 0, 0), verts=14)
        prim("cyl", (x + dx, -0.76, gz), (0.095, 0.095, 0.04), "steel", "Bolter", rot=(90, 0, 0), verts=14)  # collar
        prim("cyl", (x + dx, -0.86, gz), (0.1, 0.1, 0.06), "steel", "Bolter", rot=(90, 0, 0), verts=14)  # brake
        prim("cyl", (x + dx, -0.89, gz), (0.055, 0.055, 0.01), "black", "Bolter", rot=(90, 0, 0), verts=12)  # bore
    # Inquisitorial "I": gold bar with serif caps and a roundel, on the top of the casing and on the outer side.
    ix, iy, iz = x, -0.58, z + 0.14
    prim("cube", (ix, iy, iz), (0.03, 0.22, 0.012), "gold", "Bolter")  # stem
    prim("cube", (ix, iy + 0.1, iz), (0.1, 0.03, 0.012), "gold", "Bolter")  # rear serif
    prim("cube", (ix, iy - 0.1, iz), (0.1, 0.03, 0.012), "gold", "Bolter")  # front serif
    prim("cyl", (ix, iy, iz), (0.07, 0.07, 0.012), "gold", "Bolter", verts=14)  # roundel
    prim("cyl", (ix, iy, iz + 0.004), (0.035, 0.035, 0.012), "black", "Bolter", verts=12)
    sx = x - 0.105  # outer side of the receiver
    prim("cube", (sx, iy, z), (0.012, 0.03, 0.2), "gold", "Bolter")  # stem
    prim("cube", (sx, iy, z + 0.09), (0.012, 0.1, 0.03), "gold", "Bolter")  # top serif
    prim("cube", (sx, iy, z - 0.09), (0.012, 0.1, 0.03), "gold", "Bolter")  # bottom serif
    prim("cyl", (sx, iy, z), (0.07, 0.07, 0.012), "gold", "Bolter", rot=(0, 90, 0), verts=14)  # roundel
    prim("cyl", (sx - 0.004, iy, z), (0.035, 0.035, 0.012), "black", "Bolter", rot=(0, 90, 0), verts=12)
    # Wrist bracer and strap that hold it on.
    prim("cyl", (-0.7, -0.44, 1.37), (0.33, 0.16, 0.33), "gold", "Forearm_R", rot=(90, 0, 0), verts=16)
    prim("cube", (-0.78, -0.4, 1.5), (0.26, 0.1, 0.2), "armor_dark", "Forearm_R", bevel=0.02)


def halberd():
    px, py, pz = 0.66, -0.36, 1.06  # left hand
    xf = pivot_rotation((px, py, pz), (0, 0, 0))  # kept upright: HalberdSweep re-aims the whole shaft
    prim("cyl", (px, py, pz + 0.45), (0.075, 0.075, 2.3), "armor_dark", "Halberd", xf=xf, verts=12)  # shaft
    prim("cyl", (px, py, pz - 0.72), (0.12, 0.12, 0.12), "gold", "Halberd", xf=xf, verts=12)  # butt
    for z in (0.2, 0.9):
        prim("cyl", (px, py, pz + z), (0.115, 0.115, 0.05), "gold", "Halberd", xf=xf, verts=12)
    prim("cube", (px, py, pz + 1.55), (0.26, 0.12, 0.1), "gold", "Halberd", xf=xf, bevel=0.02)  # guard
    prim("cube", (px + 0.14, py, pz + 1.5), (0.08, 0.06, 0.16), "gold", "Halberd", xf=xf, bevel=0.01)
    prim("cube", (px, py, pz + 1.95), (0.16, 0.04, 0.7), "psy", "Halberd", xf=xf)  # blade
    prim("cube", (px, py, pz + 2.37), (0.09, 0.035, 0.14), "psy", "Halberd", xf=xf, rot=(0, 0, 0))


GRIP_R = (-0.7, -0.6, 1.39)  # the right hand; two-handed weapons ride here until the game places them


def daemonhammer():
    """Nemesis Daemon Hammer: a long haft, a hub carrying two big sigil-faced blocks, a hose and a spiked pommel.
    Modelled upright in the right hand; the game solves both arms onto its two grip markers."""
    px, py, pz = GRIP_R
    b = "Daemonhammer"
    prim("cyl", (px, py, pz + 0.55), (0.085, 0.085, 2.1), "armor_dark", b, verts=12)  # haft
    for z in (0.0, 0.45):
        prim("cyl", (px, py, pz + z), (0.12, 0.12, 0.34), "black", b, verts=12)  # leather grips
    for z in (-0.2, 0.2, 0.72):
        prim("cyl", (px, py, pz + z), (0.14, 0.14, 0.05), "gold", b, verts=12)
    prim("cyl", (px, py, pz - 0.55), (0.1, 0.1, 0.1), "gold", b, verts=12)  # pommel
    prim("cyl", (px, py, pz - 0.66), (0.04, 0.04, 0.14), "steel", b, verts=8)  # pommel spike
    hz = pz + 1.62  # head height
    prim("cyl", (px, py, hz), (0.2, 0.2, 0.52), "steel", b, rot=(0, 90, 0), verts=14)  # hub on the X axis
    prim("cube", (px, py, hz - 0.16), (0.2, 0.2, 0.18), "gold", b, bevel=0.02)  # hub collar
    for sd in (1, -1):
        prim("cube", (px + 0.36 * sd, py, hz), (0.44, 0.34, 0.54), "armor_dark", b, bevel=0.04)  # block
        prim("cube", (px + 0.36 * sd, py, hz), (0.48, 0.2, 0.2), "steel", b, bevel=0.02)  # band
        prim("cube", (px + 0.6 * sd, py, hz), (0.05, 0.3, 0.46), "gold", b, bevel=0.01)  # striking face trim
        for fy in (-1, 1):
            prim("cube", (px + 0.36 * sd, py + 0.176 * fy, hz), (0.36, 0.02, 0.46), "gold", b)  # face plate
            prim("cube", (px + 0.36 * sd, py + 0.186 * fy, hz), (0.24, 0.012, 0.34), "armor_dark", b)
        prim("sphere", (px + 0.36 * sd, py - 0.19, hz + 0.02), (0.15, 0.05, 0.17), "steel", b, verts=12)  # skull
        prim("cube", (px + 0.36 * sd, py + 0.19, hz), (0.05, 0.012, 0.26), "psy", b)  # glowing sigil
        prim("cube", (px + 0.36 * sd, py + 0.19, hz), (0.2, 0.012, 0.05), "psy", b)
    limb((px + 0.1, py, hz - 0.2), (px + 0.2, py, pz + 1.15), 0.025, 0.025, "black", b)  # hose
    limb((px + 0.2, py, pz + 1.15), (px + 0.08, py, pz + 0.85), 0.025, 0.025, "black", b)


def psycannon():
    """Grey Knight Psycannon, built to be carried in two hands: pistol grip under the receiver (rear hand) and a carry
    handle on top (front hand). Barrels point down -Y (the model's forward). Black casing, after the reference art: a tall
    receiver with a stepped rear block and skull plaque, an ammo box with a brass grille hanging under the front, a
    corrugated feed hose looping from the back, and a barrel with red coils ending in a thick drilled muzzle."""
    gx, gy, gz = GRIP_R
    b = "Psycannon"
    ax = gz + 0.2  # height of the barrel axis

    # Receiver: tall main block, stepped lower rear block, steel end cap and top plates.
    prim("cube", (gx, gy - 0.55, ax), (0.3, 0.8, 0.42), "black", b, bevel=0.04)
    prim("cube", (gx, gy - 0.02, gz + 0.12), (0.28, 0.42, 0.28), "black", b, bevel=0.04)
    prim("cube", (gx, gy + 0.2, gz + 0.12), (0.26, 0.04, 0.3), "steel", b, bevel=0.01)
    prim("cube", (gx, gy - 0.02, gz + 0.27), (0.22, 0.4, 0.03), "steel", b, bevel=0.008)
    prim("cube", (gx, gy - 0.72, ax + 0.22), (0.2, 0.3, 0.03), "armor_dark", b, bevel=0.008)  # top plate
    prim("cube", (gx, gy - 0.82, ax + 0.25), (0.14, 0.06, 0.06), "steel", b, bevel=0.01)  # fore sight
    prim("cube", (gx, gy - 0.35, ax + 0.222), (0.12, 0.05, 0.03), "armor_dark", b)  # handle mount
    for sd in (1, -1):
        sx = gx + 0.155 * sd
        prim("cube", (sx, gy - 0.42, ax), (0.02, 0.5, 0.34), "armor_dark", b, bevel=0.01)  # side panel
        prim("cube", (sx + 0.007 * sd, gy - 0.58, ax + 0.06), (0.012, 0.22, 0.045), "steel", b)  # ejection slot
        prim("cube", (sx + 0.008 * sd, gy - 0.3, ax - 0.02), (0.012, 0.05, 0.28), "gold", b)  # name strip
        prim("cube", (gx + 0.145 * sd, gy - 0.02, gz + 0.12), (0.012, 0.26, 0.2), "armor_dark", b)  # plaque base
        prim("sphere", (gx + 0.152 * sd, gy - 0.02, gz + 0.13), (0.02, 0.1, 0.11), "gold", b, verts=10)  # skull
        prim("cyl", (gx + 0.158 * sd, gy - 0.1, gz + 0.04), (0.04, 0.04, 0.012), "steel", b, rot=(0, 90, 0), verts=8)  # rivet

    # Pistol grip and trigger guard.
    prim("cube", (gx, gy, gz - 0.02), (0.12, 0.15, 0.3), "black", b, rot=(-12, 0, 0), bevel=0.02)
    prim("cube", (gx, gy - 0.12, gz - 0.08), (0.04, 0.12, 0.03), "steel", b)  # guard bottom
    prim("cube", (gx, gy - 0.17, gz - 0.02), (0.04, 0.03, 0.14), "steel", b)  # guard front
    prim("cube", (gx, gy - 0.08, gz - 0.04), (0.03, 0.03, 0.08), "steel", b)  # trigger

    # Ammo box under the front of the receiver, with brass grille ribs on both faces.
    prim("cube", (gx, gy - 0.74, gz - 0.02), (0.3, 0.32, 0.4), "armor_dark", b, bevel=0.03)
    prim("cube", (gx, gy - 0.74, gz - 0.23), (0.32, 0.34, 0.03), "steel", b)  # base plate
    for sd in (1, -1):
        prim("cube", (gx + 0.152 * sd, gy - 0.74, gz - 0.02), (0.016, 0.26, 0.32), "steel", b)  # grille frame
        for dz in (-0.11, -0.04, 0.03, 0.10):
            prim("cube", (gx + 0.162 * sd, gy - 0.74, gz - 0.02 + dz), (0.014, 0.22, 0.04), "brass", b)

    # Barrel: collar, coil housing with red coils, muzzle collar and a thick drilled muzzle.
    prim("cyl", (gx, gy - 1.02, ax), (0.3, 0.3, 0.1), "steel", b, rot=(90, 0, 0), verts=16)
    prim("cyl", (gx, gy - 1.25, ax), (0.24, 0.24, 0.36), "armor_dark", b, rot=(90, 0, 0), verts=16)
    for dy in (-1.13, -1.25, -1.37):
        prim("cyl", (gx, gy + dy, ax), (0.29, 0.29, 0.055), "red", b, rot=(90, 0, 0), verts=16)
    prim("cyl", (gx, gy - 1.5, ax), (0.32, 0.32, 0.07), "steel", b, rot=(90, 0, 0), verts=16)
    prim("cyl", (gx, gy - 1.68, ax), (0.24, 0.24, 0.3), "steel", b, rot=(90, 0, 0), verts=16)
    prim("cyl", (gx, gy - 1.84, ax), (0.17, 0.17, 0.03), "black", b, rot=(90, 0, 0), verts=16)  # bore
    prim("cyl", (gx, gy - 1.845, ax), (0.08, 0.08, 0.03), "psy", b, rot=(90, 0, 0), verts=12)
    prim("cyl", (gx, gy - 1.68, ax), (0.075, 0.075, 0.25), "black", b, rot=(0, 90, 0), verts=10)  # cross hole
    prim("cube", (gx, gy - 1.12, ax + 0.18), (0.08, 0.14, 0.06), "steel", b, bevel=0.01)  # barrel mount block

    # Top carry handle.
    prim("cube", (gx, gy - 0.5, gz + 0.5), (0.09, 0.3, 0.055), "steel", b, bevel=0.01)
    for dy in (-0.36, -0.64):
        prim("cube", (gx, gy + dy, gz + 0.4), (0.07, 0.06, 0.17), "steel", b, bevel=0.01)

    # Corrugated feed hose: from the rear block, looping low on the left side, up into the ammo box.
    path = [(0.16, 0.1, 0.12), (0.26, 0.22, 0.0), (0.27, 0.14, -0.26), (0.22, -0.14, -0.36), (0.18, -0.46, -0.3), (0.1, -0.6, -0.14)]
    n = 0
    for p0, p1 in zip(path, path[1:]):
        steps = max(2, int(((p1[0] - p0[0]) ** 2 + (p1[1] - p0[1]) ** 2 + (p1[2] - p0[2]) ** 2) ** 0.5 / 0.035))
        for i in range(steps):
            t = i / steps
            q = tuple(p0[k] + (p1[k] - p0[k]) * t for k in range(3))
            prim("sphere", (gx + q[0], gy + q[1], gz + q[2]), (0.085, 0.085, 0.085), "black" if n % 2 == 0 else "armor_dark", b, verts=8)
            n += 1


def order_helm(b, m, crest):
    """Crown, brow and jaw plates over the head, shown by the game only under an Order (white Purifier, gold Paladin)."""
    c.clipped_ellipsoid((0, -0.02, 2.04), (0.5, 0.52, 0.52), 2.12, None, m, b, bevel=0.01)  # crown
    c.clipped_ellipsoid((0, -0.02, 2.04), (0.5, 0.52, 0.52), None, 1.97, m, b)  # jaw and neck
    prim("cube", (0, -0.235, 2.135), (0.4, 0.07, 0.06), m, b, bevel=0.01)  # brow
    for s in (-1, 1):
        prim("cube", (0.235 * s, -0.04, 2.05), (0.04, 0.34, 0.16), m, b, bevel=0.01)  # side plates
    prim("cube", (0, 0, 2.34), (0.05, 0.36, 0.08), crest, b, bevel=0.01)  # crest


def order_shoulders(m, suffix):
    """Shells over the main surface of both pauldrons, shown by the game only under an Order."""
    w, d, h, tilt = 0.94, 0.78, 0.68, 16
    z = 1.74
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.72 * s
        zc = z - 0.1 * h
        cut = -0.4 * h
        c.clipped_ellipsoid((x, 0, zc), (w * 1.025, d * 1.025, 1.2 * h * 1.025), cut * 1.025 + 0.02, None, m,
                            f"Pauldron_{suffix}_{side}", rot=(0, tilt * s, 0))


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.0), (0, 0, 1.2)),
    ("Spine", "Hips", (0, 0, 1.2), (0, 0, 1.8)),
    ("Head", "Spine", (0, 0, 1.8), (0, 0, 2.2)),
    ("Backpack", "Spine", (0, 0.2, 1.7), (0, 0.4, 1.3)),
    ("UpperArm_L", "Spine", (0.62, 0, 1.65), (0.86, -0.08, 1.3)),
    ("Forearm_L", "UpperArm_L", (0.86, -0.08, 1.3), (0.68, -0.32, 1.1)),
    ("Hand_L", "Forearm_L", (0.68, -0.32, 1.1), (0.65, -0.44, 1.0)),
    ("Halberd", "Hand_L", (0.66, -0.36, 1.06), (0.66, -0.36, 1.4)),
    ("UpperArm_R", "Spine", (-0.62, 0, 1.65), (-0.86, -0.1, 1.32)),
    ("Forearm_R", "UpperArm_R", (-0.86, -0.1, 1.32), (-0.7, -0.5, 1.38)),
    ("Hand_R", "Forearm_R", (-0.7, -0.5, 1.38), (-0.7, -0.7, 1.4)),
    ("Muzzle_R", "Forearm_R", (-0.82, -0.9, 1.62), (-0.82, -0.96, 1.62)),
    ("Bolter", "Forearm_R", (-0.82, -0.52, 1.6), (-0.82, -0.6, 1.6)),
    ("Helm_White", "Head", (0, -0.02, 2.04), (0, -0.02, 2.1)),
    ("Pauldron_White_L", "UpperArm_L", (0.72, 0, 1.74), (0.72, 0, 1.8)),
    ("Pauldron_White_R", "UpperArm_R", (-0.72, 0, 1.74), (-0.72, 0, 1.8)),
    ("Helm_Gold", "Head", (0, -0.02, 2.04), (0, -0.02, 2.1)),
    ("Pauldron_Gold_L", "UpperArm_L", (0.72, 0, 1.74), (0.72, 0, 1.8)),
    ("Pauldron_Gold_R", "UpperArm_R", (-0.72, 0, 1.74), (-0.72, 0, 1.8)),
]

# Two-handed weapons ride in the right hand; the game scales away whichever are not wielded (and the white helm and the
# bolter when they are not wanted). Grip markers are unweighted helper bones on the weapon: rear hand A, front hand B.
_g = GRIP_R
_hammer_head = (_g[0], _g[1], _g[2] + 1.62)
_cannon_front = (_g[0], _g[1] - 0.4, _g[2] + 0.5)
_cannon_muzzle = (_g[0], _g[1] - 1.86, _g[2] + 0.2)

def _up(p, dz=0.05):
    return (p[0], p[1], p[2] + dz)

BONES += [
    ("Daemonhammer", "Hand_R", _g, _up(_g)),
    ("Hammer_GripA", "Daemonhammer", _g, _up(_g)),
    ("Hammer_GripB", "Daemonhammer", _up(_g, 0.45), _up(_g, 0.5)),
    ("Hammer_Head", "Daemonhammer", _hammer_head, _up(_hammer_head)),
    ("Psycannon", "Hand_R", _g, _up(_g)),
    ("Cannon_GripA", "Psycannon", _g, _up(_g)),
    ("Cannon_GripB", "Psycannon", _cannon_front, _up(_cannon_front)),
    ("Cannon_Muzzle", "Psycannon", _cannon_muzzle, _up(_cannon_muzzle)),
]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.25 * _s, 0, 1.0), (0.25 * _s, 0, 0.6)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.25 * _s, 0, 0.6), (0.25 * _s, 0, 0.2)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.25 * _s, 0, 0.2), (0.25 * _s, -0.3, 0.05)),
    ]

legs()
torso()
pauldrons()
arms()
head_and_pack()
storm_bolter()
halberd()
daemonhammer()
psycannon()
order_helm("Helm_White", "white", "red")
order_shoulders("white", "White")
order_helm("Helm_Gold", "gold", "red")
order_shoulders("gold", "Gold")

armature = c.build_armature(BONES)
mesh = c.assemble("GreyKnight", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.3, distance=1.15)
