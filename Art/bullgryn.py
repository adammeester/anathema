"""Builds the Bullgryn: a hunched Ogryn in slab-sided blue-grey carapace with teal edging, a bald head in a gas mask,
a hanging apron of tread-plate armour, khaki fatigues, a stacked brute shield on the left arm and two swappable
right-hand weapons: a slab power maul and a grenadier gauntlet.

Based on the reference images (Bullgryn miniatures; used as a guide only, nothing from them is imported).

    blender -b --python Art/bullgryn.py                                 # writes Assets/Models/Bullgryn.fbx (both weapons)
                                                                        # and BullgrynShield.fbx (the shield alone, planted
                                                                        # by Living Wall)
    blender -b --python Art/bullgryn.py -- --render DIR                 # also renders preview PNGs into DIR
    blender -b --python Art/bullgryn.py -- --only maul|gauntlet ...     # previews with one weapon

Same skeleton naming as the other characters (Hips, Spine, Head, UpperArm_L, Thigh_L, ...) so ProceduralWalk and the
pose scripts work unchanged, plus prop bones: Shield (on Hand_L), Maul (on Hand_R) and Gauntlet (on Forearm_R).
Both weapons are in the FBX; the game scales away the one not wielded (like the Berserker's weapons).
Roughly 2.9 m tall: the biggest silhouette on the roster.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import common as c  # noqa: E402
from common import flat_poly, limb, mat, pivot_rotation, prim  # noqa: E402
from mathutils import Matrix  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "Bullgryn.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None
ONLY = argv[argv.index("--only") + 1] if "--only" in argv else None  # maul | gauntlet (previews)
OUT_FBX = argv[argv.index("--out") + 1] if "--out" in argv else OUT_FBX
# The slabshield on its own, planted (Living Wall). Front faces -Y like the character, so it faces +Z in Unity.
OUT_SHIELD = os.path.join(os.path.dirname(OUT_FBX), "BullgrynShield.fbx")

c.configure(1.0)

def make_materials():
    mat("armor", (0.16, 0.27, 0.33), metallic=0.3, rough=0.5)          # blue-grey carapace
    mat("armor_dark", (0.08, 0.13, 0.16), metallic=0.4, rough=0.55)
    mat("trim", (0.12, 0.5, 0.48), metallic=0.3, rough=0.45)           # teal edge highlight
    mat("fatigues", (0.56, 0.46, 0.32), rough=0.85)                     # khaki trousers
    mat("boot", (0.07, 0.07, 0.08), rough=0.6)
    mat("iron", (0.1, 0.1, 0.11), metallic=0.85, rough=0.45)
    mat("steel", (0.5, 0.51, 0.53), metallic=0.9, rough=0.32)
    mat("bronze", (0.62, 0.36, 0.18), metallic=0.85, rough=0.35)
    mat("flesh", (0.78, 0.53, 0.43), rough=0.65)
    mat("hazard_y", (0.95, 0.6, 0.08), rough=0.5)
    mat("hazard_k", (0.03, 0.03, 0.035), rough=0.6)
    mat("roundel", (0.62, 0.72, 0.78), rough=0.5)
    mat("red", (0.6, 0.05, 0.07), rough=0.5)
    mat("eye", (0.05, 0.03, 0.03), rough=0.3)
    mat("power", (0.25, 1.0, 0.35), emit=(0.25, 1.0, 0.35), emit_strength=4.0)


make_materials()


def box_along(a, b, w, h, material, bone, bevel=0.0):
    """Box from point a to point b (its length), w x h across. Like `limb`, but square-sectioned."""
    a, b = c.v(*a), c.v(*b)
    d = b - a
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.active_object
    o.location = (a + b) / 2
    o.scale = (w * c.S, h * c.S, d.length)
    up = "Y" if abs(d.y) < 0.9 * d.length else "X"
    o.rotation_euler = d.to_track_quat("Z", up).to_euler()
    return c.finish(o, material, bone, bevel)


def aquila(cx, y, cz, k, bone, xf=None, material="bronze"):
    """Flat double-headed eagle facing -Y: a body and two feathered wings, `k` scales it (1 = ~0.6 wide)."""
    wing = [(0.03, 0.04), (0.14, 0.1), (0.3, 0.1), (0.27, 0.05), (0.31, 0.02), (0.24, 0.0), (0.27, -0.04),
            (0.18, -0.04), (0.19, -0.08), (0.1, -0.07), (0.03, -0.04)]
    for s in (1, -1):
        pts = [(cx + u * k * s, cz + z * k) for u, z in wing]
        flat_poly(pts if s > 0 else pts[::-1], y, 0.03 * k, material, bone, xf=xf)
        prim("sphere", (cx + 0.035 * k * s, y - 0.005, cz + 0.08 * k), (0.05 * k, 0.04 * k, 0.05 * k), material,
             bone, xf=xf, verts=8)  # heads
    prim("cube", (cx, y - 0.01, cz), (0.06 * k, 0.04 * k, 0.16 * k), material, bone, xf=xf, bevel=0.01 * k)


# --------------------------------------------------------------------------------------------------- body

def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.36 * s
        prim("cube", (x, -0.12, 0.12), (0.44, 0.66, 0.24), "boot", f"Foot_{side}", bevel=0.08)
        prim("cube", (x, -0.12, 0.025), (0.46, 0.68, 0.05), "iron", f"Foot_{side}", bevel=0.01)
        # Khaki fatigues, baggy over the boots.
        limb((x, 0, 0.2), (x, 0, 0.64), 0.27, 0.29, "fatigues", f"Shin_{side}")
        # Boxy carapace greave with a teal top edge and a few battle-damage gouges.
        prim("cube", (x, -0.2, 0.38), (0.42, 0.2, 0.42), "armor", f"Shin_{side}", bevel=0.03)
        prim("cube", (x, -0.205, 0.585), (0.43, 0.21, 0.03), "trim", f"Shin_{side}")
        for dz in (0.3, 0.44):
            prim("cube", (x + 0.08 * s, -0.305, dz), (0.06, 0.02, 0.03), "armor_dark", f"Shin_{side}")
        prim("cube", (x, -0.2, 0.7), (0.36, 0.2, 0.2), "armor", f"Shin_{side}", bevel=0.05)  # knee plate
        limb((x, 0, 0.66), (x * 0.95, 0, 1.06), 0.3, 0.34, "fatigues", f"Thigh_{side}")


def torso():
    # Hips, khaki seat and a belt.
    prim("cube", (0, 0.02, 1.1), (1.0, 0.68, 0.36), "fatigues", "Hips", bevel=0.12)
    prim("cube", (0, 0.0, 1.27), (1.04, 0.72, 0.1), "boot", "Hips", bevel=0.03)

    # Dark undersuit belly behind the tread apron.
    prim("sphere", (0, -0.1, 1.42), (1.04, 0.8, 0.56), "armor_dark", "Spine", verts=24)

    # Slab carapace chest: a big flat-faced block, angled forward, with teal edging.
    prim("cube", (0, -0.02, 1.86), (1.3, 0.86, 0.66), "armor", "Spine", rot=(-10, 0, 0), bevel=0.07)
    prim("cube", (0, -0.47, 1.86), (1.06, 0.06, 0.54), "armor", "Spine", rot=(-10, 0, 0), bevel=0.03)  # breast slab
    prim("cube", (0, -0.47, 1.6), (1.08, 0.07, 0.035), "trim", "Spine", rot=(-10, 0, 0))   # lower edge highlight
    for s in (-1, 1):
        prim("cube", (0.53 * s, -0.47, 1.86), (0.035, 0.07, 0.54), "trim", "Spine", rot=(-10, 0, 0))
    aquila(0, -0.535, 1.97, 0.95, "Spine")
    # Back armour and the tall collar that rises behind the head.
    prim("cube", (0, 0.38, 1.92), (1.1, 0.36, 0.72), "armor", "Spine", bevel=0.06)
    prim("cube", (0, 0.12, 2.28), (0.84, 0.36, 0.3), "armor", "Spine", rot=(12, 0, 0), bevel=0.05)
    prim("cube", (0, 0.12, 2.43), (0.86, 0.37, 0.03), "trim", "Spine", rot=(12, 0, 0))

    # Tread-plate apron hanging from the chest to the groin: heavy steel links on a dark strap.
    prim("cube", (0, -0.42, 1.18), (0.12, 0.04, 0.78), "boot", "Spine")
    for i, z in enumerate((1.5, 1.33, 1.16, 0.99, 0.82)):
        w = 0.5 if i % 2 == 0 else 0.44
        prim("cube", (0, -0.47, z), (w, 0.07, 0.14), "steel", "Spine" if i < 2 else "Hips", bevel=0.015)
        prim("cube", (0, -0.51, z), (w * 0.55, 0.03, 0.07), "iron", "Spine" if i < 2 else "Hips", bevel=0.01)
        for s in (-1, 1):  # link lugs between plates
            prim("cube", (0.17 * s, -0.46, z - 0.085), (0.07, 0.05, 0.05), "iron", "Spine" if i < 2 else "Hips")


def pauldrons():
    # Flat slab shoulder plates sitting high, with a teal edge, over bare upper arms.
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.8 * s
        prim("cube", (x, 0.0, 2.17), (0.56, 0.8, 0.2), "armor", f"UpperArm_{side}", rot=(0, 18 * s, 0), bevel=0.04)
        prim("cube", (x, 0.0, 2.09), (0.565, 0.805, 0.03), "trim", f"UpperArm_{side}", rot=(0, 18 * s, 0))
        prim("cube", (x + 0.2 * s, 0.0, 1.98), (0.12, 0.74, 0.26), "armor", f"UpperArm_{side}", rot=(0, 18 * s, 0),
             bevel=0.03)  # outer drop plate
        for dy in (-0.25, 0.25):
            prim("sphere", (x, dy, 2.28), (0.05, 0.05, 0.04), "steel", f"UpperArm_{side}", verts=8)  # rivets


ARMS = {
    # Long, ape-like arms: the hands hang low in front of the body.
    "L": dict(sh=(0.8, 0, 1.98), el=(1.04, -0.14, 1.48), wr=(0.84, -0.58, 1.3)),
    "R": dict(sh=(-0.8, 0, 1.98), el=(-1.08, -0.06, 1.46), wr=(-0.98, -0.46, 1.2)),
}


def arms():
    for side, a in ARMS.items():
        sh, el, wr = Vector(a["sh"]), Vector(a["el"]), Vector(a["wr"])
        # Bare, heavily muscled arms.
        limb(a["sh"], a["el"], 0.27, 0.22, "flesh", f"UpperArm_{side}")
        prim("sphere", tuple(sh.lerp(el, 0.45) + Vector((0, -0.1, 0))), (0.4, 0.34, 0.46), "flesh",
             f"UpperArm_{side}", verts=16)  # bicep
        prim("sphere", a["el"], (0.36, 0.36, 0.36), "flesh", f"UpperArm_{side}", verts=16)
        limb(a["el"], a["wr"], 0.24, 0.2, "flesh", f"Forearm_{side}")
        d = (wr - el).normalized()
        box_along(tuple(el.lerp(wr, 0.45)), tuple(wr + d * 0.02), 0.44, 0.44, "armor", f"Forearm_{side}", bevel=0.03)
        box_along(tuple(el.lerp(wr, 0.43)), tuple(el.lerp(wr, 0.5)), 0.46, 0.46, "trim", f"Forearm_{side}")
        prim("sphere", tuple(wr + d * 0.12), (0.36, 0.36, 0.34), "flesh", f"Hand_{side}", verts=16)  # fist
        prim("cube", tuple(wr + d * 0.16 + Vector((0, 0, 0.1))), (0.3, 0.22, 0.1), "boot", f"Hand_{side}",
             bevel=0.02)  # glove back


def head():
    # Bald head pushed forward and low, the lower face hidden by a blue gas mask, a steel cranial strip on top.
    hz, hy = 2.36, -0.5
    prim("sphere", (0, hy + 0.04, hz + 0.04), (0.44, 0.48, 0.48), "flesh", "Head", verts=24)  # skull
    prim("cube", (0, hy - 0.17, hz + 0.08), (0.36, 0.1, 0.07), "flesh", "Head", bevel=0.03)   # heavy brow
    for s in (-1, 1):
        prim("sphere", (0.09 * s, hy - 0.18, hz + 0.03), (0.06, 0.03, 0.035), "eye", "Head", verts=8)
        prim("sphere", (0.23 * s, hy + 0.02, hz - 0.0), (0.08, 0.15, 0.15), "flesh", "Head", verts=10)  # ears
    # Cranial strip: forehead to crown.
    for i in range(5):
        t = i / 4
        ang = math.radians(-60 + 110 * t)
        prim("cube", (0, hy + 0.04 + 0.25 * math.sin(ang), hz + 0.04 + 0.245 * math.cos(ang)), (0.07, 0.1, 0.03),
             "steel", "Head", rot=(math.degrees(ang), 0, 0), bevel=0.008)
    # Gas mask.
    prim("cube", (0, hy - 0.13, hz - 0.13), (0.4, 0.26, 0.22), "armor", "Head", bevel=0.06)
    prim("cube", (0, hy - 0.27, hz - 0.12), (0.18, 0.12, 0.16), "armor", "Head", bevel=0.04)  # snout
    for dz in (-0.07, -0.12, -0.17):
        prim("cube", (0, hy - 0.335, hz + dz), (0.12, 0.02, 0.02), "steel", "Head")  # grille slats
    for s in (-1, 1):
        prim("cyl", (0.17 * s, hy - 0.2, hz - 0.12), (0.12, 0.12, 0.06), "armor_dark", "Head", rot=(90, 0, 25 * s),
             verts=14)  # filter cans
        prim("cyl", (0.18 * s, hy - 0.235, hz - 0.12), (0.08, 0.08, 0.02), "steel", "Head", rot=(90, 0, 25 * s),
             verts=14)
    limb((0, hy - 0.3, hz - 0.22), (0, hy - 0.3, hz - 0.33), 0.04, 0.04, "iron", "Head", verts=8)  # hose
    prim("cube", (0, hy - 0.13, hz - 0.01), (0.42, 0.24, 0.03), "trim", "Head")  # mask top edge


# --------------------------------------------------------------------------------------------------- shield

def slabshield(prop=False):
    """A stacked brute shield: two armoured boxes (the upper with a vision port), a recessed band with the eagle,
    hazard-striped panels on the inner side, a skull roundel, projecting bars, and three spikes at the foot."""
    a = ARMS["L"]
    g = Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.1
    bone = "Shield"
    xf = pivot_rotation(tuple(g), (0, 0, -18))
    yf = g.y - 0.24          # front face of the core slab
    x0 = g.x + 0.2           # shield centre; u < 0 is the inner side (toward the body)
    if prop:
        # Standalone and planted: centred on the origin, front face on y = 0, sunk so the spikes are in the ground.
        xf = Matrix.Translation(Vector((-x0, -yf, -0.3)))

    def sb(u0, u1, z0, z1, front, back, material, bevel=0.0):
        """Box spanning u0..u1 across, z0..z1 up, from `front` ahead of the face plane to `back` behind it."""
        prim("cube", (x0 + (u0 + u1) / 2, yf + (back - front) / 2, (z0 + z1) / 2),
             (u1 - u0, front + back, z1 - z0), material, bone, xf=xf, bevel=bevel)

    def stripes(u0, u1, z0, z1, front):
        h = z1 - z0
        for za in (z0 + 0.03 * h, z0 + 0.48 * h):
            zb = za + 0.36 * h
            pts = [(x0 + u0, za), (x0 + u1, zb), (x0 + u1, zb + 0.16 * h), (x0 + u0, za + 0.16 * h)]
            flat_poly(pts, yf - front - 0.005, 0.02, "hazard_k", bone, xf=xf)

    sb(-0.42, 0.42, 0.34, 2.0, 0.0, 0.16, "armor_dark")                   # core slab
    # Upper box with the vision port on the outer side.
    sb(-0.46, 0.46, 1.5, 2.06, 0.12, 0.22, "armor", bevel=0.015)
    sb(-0.47, 0.47, 2.03, 2.065, 0.125, 0.225, "trim")
    sb(-0.47, 0.47, 1.49, 1.52, 0.125, 0.225, "trim")
    sb(0.06, 0.4, 1.74, 1.98, 0.15, 0.0, "steel", bevel=0.01)              # port frame
    sb(0.1, 0.36, 1.78, 1.94, 0.16, 0.0, "hazard_k")                       # the dark port
    sb(-0.41, -0.03, 1.56, 1.98, 0.135, 0.0, "hazard_y")
    stripes(-0.41, -0.03, 1.56, 1.98, 0.135)
    sb(0.0, 0.58, 2.0, 2.04, 0.17, 0.0, "steel")                           # bar projecting past the edge
    # Recessed middle band: vertical rails and the eagle.
    for u in (-0.3, 0.3):
        sb(u - 0.03, u + 0.03, 1.18, 1.5, 0.06, 0.0, "steel")
    aquila(x0, yf - 0.08, 1.36, 1.0, bone, xf=xf)
    # Lower box: hazard panel inner, skull roundel outer, two projecting bars.
    sb(-0.46, 0.46, 0.8, 1.2, 0.12, 0.22, "armor", bevel=0.015)
    sb(-0.47, 0.47, 1.17, 1.2, 0.125, 0.225, "trim")
    sb(-0.41, -0.05, 0.86, 1.14, 0.135, 0.0, "hazard_y")
    stripes(-0.41, -0.05, 0.86, 1.14, 0.135)
    prim("cyl", (x0 + 0.22, yf - 0.13, 1.0), (0.3, 0.3, 0.02), "roundel", bone, xf=xf, rot=(90, 0, 0), verts=20)
    prim("sphere", (x0 + 0.22, yf - 0.145, 1.02), (0.12, 0.03, 0.11), "red", bone, xf=xf, verts=12)  # skull
    prim("cube", (x0 + 0.22, yf - 0.145, 0.95), (0.07, 0.03, 0.05), "red", bone, xf=xf)
    for z in (0.9, 1.1):
        sb(-0.12, 0.56, z - 0.018, z + 0.018, 0.16, 0.0, "steel")
    # Foot: narrower plates on rails and three spikes.
    sb(-0.4, 0.4, 0.42, 0.8, 0.06, 0.18, "armor", bevel=0.01)
    for u in (-0.3, -0.1, 0.1, 0.3):
        sb(u - 0.025, u + 0.025, 0.36, 0.8, 0.09, 0.0, "steel")
    for u in (-0.05, 0.15, 0.32):
        limb((x0 + u, yf - 0.03, 0.42), (x0 + u, yf - 0.03, 0.1), 0.05, 0.0, "steel", bone, xf=xf, verts=8)
    # Rivets and the arm strap behind.
    for u in (-0.4, 0.4):
        for z in (1.56, 2.0, 0.86, 1.14):
            prim("sphere", (x0 + u, yf - 0.13, z), (0.04, 0.03, 0.04), "steel", bone, xf=xf, verts=8)
    prim("cube", (x0 - 0.1, yf + 0.3, g.z), (0.44, 0.14, 0.12), "boot", bone, xf=xf, bevel=0.02)
    return tuple(g)


# --------------------------------------------------------------------------------------------------- weapons

def power_maul():
    """Slab power maul: a long, flat-sided rectangular bludgeon with a crossguard, panelled faces and a green
    power-cell light near the tip."""
    a = ARMS["R"]
    g = Vector(a["wr"]) + (Vector(a["wr"]) - Vector(a["el"])).normalized() * 0.12
    px, py, pz = g
    bone = "Maul"
    xf = pivot_rotation((px, py, pz), (28, -8, 0))  # head leans forward and slightly out

    prim("cyl", (px, py, pz), (0.12, 0.12, 0.42), "boot", bone, xf=xf, verts=12)               # wrapped grip
    prim("cube", (px, py, pz - 0.25), (0.15, 0.13, 0.1), "steel", bone, xf=xf, bevel=0.02)     # pommel
    prim("cube", (px, py, pz + 0.25), (0.3, 0.16, 0.08), "steel", bone, xf=xf, bevel=0.015)    # crossguard
    # The slab: steel body, iron recessed panels on both faces, ribs across.
    L0, L1 = 0.29, 1.32
    prim("cube", (px, py, pz + (L0 + L1) / 2), (0.19, 0.11, L1 - L0), "steel", bone, xf=xf, bevel=0.015)
    for fy in (-0.056, 0.056):
        prim("cube", (px, py + fy, pz + (L0 + L1) / 2 - 0.05), (0.12, 0.01, L1 - L0 - 0.25), "iron", bone, xf=xf)
    for z in (0.5, 0.75, 1.0):
        prim("cube", (px, py, pz + z), (0.21, 0.13, 0.04), "steel", bone, xf=xf, bevel=0.008)
    prim("cube", (px, py, pz + L1 - 0.04), (0.21, 0.13, 0.08), "iron", bone, xf=xf, bevel=0.01)  # tip cap
    prim("sphere", (px, py - 0.065, pz + L1 - 0.15), (0.06, 0.03, 0.06), "power", bone, xf=xf, verts=10)
    prim("cube", (px, py - 0.058, pz + 0.9), (0.025, 0.01, 0.5), "power", bone, xf=xf)  # field strip


def grenadier_gauntlet():
    """Grenadier gauntlet: a boxy launcher encasing the right forearm, its fat barrel jutting past the fist."""
    a = ARMS["R"]
    el, wr = Vector(a["el"]), Vector(a["wr"])
    d = (wr - el).normalized()
    side = Vector((-1, 0, 0))  # outward for the right arm
    up = d.cross(side).normalized()
    if up.z < 0:
        up = -up
    bone = "Gauntlet"

    def P(t, o_up=0.0, o_side=0.0):
        return tuple(el + d * t + up * o_up + side * o_side)

    n = (wr - el).length
    box_along(P(0.02), P(n + 0.2), 0.5, 0.5, "armor", bone, bevel=0.04)                 # housing over the forearm
    box_along(P(0.02), P(0.06), 0.53, 0.53, "trim", bone)
    box_along(P(n - 0.02), P(n + 0.02), 0.53, 0.53, "trim", bone)
    box_along(P(0.12, 0.27), P(n - 0.05, 0.27), 0.26, 0.1, "armor_dark", bone, bevel=0.02)  # top rail
    box_along(P(0.18, 0.0, 0.27), P(n - 0.12, 0.0, 0.27), 0.08, 0.3, "trim", bone)      # side panel edge
    box_along(P(n + 0.12), P(n + 0.42), 0.48, 0.44, "steel", bone, bevel=0.03)          # breech block
    for t in (n + 0.2, n + 0.34):
        box_along(P(t, 0.0, 0.25), P(t + 0.05, 0.0, 0.25), 0.04, 0.2, "iron", bone)    # breech ribs
    limb(P(n + 0.38), P(n + 0.82), 0.17, 0.17, "iron", bone, verts=20)                  # barrel
    limb(P(n + 0.7), P(n + 0.86), 0.21, 0.21, "armor", bone, verts=20)                  # muzzle collar
    limb(P(n + 0.85), P(n + 0.87), 0.215, 0.215, "trim", bone, verts=20)
    limb(P(n + 0.82), P(n + 0.875), 0.12, 0.12, "hazard_k", bone, verts=16)             # bore
    # Drum magazine slung underneath.
    limb(P(n * 0.4, -0.3), P(n * 0.4 + 0.22, -0.3), 0.15, 0.15, "armor_dark", bone, verts=16)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.04), (0, 0, 1.26)),
    ("Spine", "Hips", (0, 0, 1.26), (0, -0.1, 2.06)),
    ("Head", "Spine", (0, -0.34, 2.14), (0, -0.4, 2.5)),
    ("Backpack", "Spine", (0, 0.4, 1.95), (0, 0.55, 1.5)),
]
for _side, _a in ARMS.items():
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.18)),
    ]
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.36 * _s, 0, 1.04), (0.36 * _s, 0, 0.64)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.36 * _s, 0, 0.64), (0.36 * _s, 0, 0.22)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.36 * _s, 0, 0.22), (0.36 * _s, -0.34, 0.06)),
    ]

legs()
torso()
pauldrons()
arms()
head()
_gs = slabshield()
if ONLY in (None, "maul"):
    power_maul()
if ONLY in (None, "gauntlet"):
    grenadier_gauntlet()

_wrR = Vector(ARMS["R"]["wr"])
_elR = Vector(ARMS["R"]["el"])
_gm = tuple(_wrR + (_wrR - _elR).normalized() * 0.12)
BONES += [
    ("Shield", "Hand_L", _gs, (_gs[0], _gs[1], _gs[2] + 0.4)),
    ("Maul", "Hand_R", _gm, (_gm[0], _gm[1], _gm[2] + 0.4)),
    ("Gauntlet", "Forearm_R", tuple(_elR), tuple(_wrR)),
]

armature = c.build_armature(BONES)
mesh = c.assemble("Bullgryn", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.45, distance=1.35)


def export_shield_prop():
    """The slabshield alone, for Living Wall (unrigged, origin on the ground at the front face's centre)."""
    c.configure(1.0)
    make_materials()
    slabshield(prop=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in c.parts:
        o.vertex_groups.clear()
        o.select_set(True)
    bpy.context.view_layer.objects.active = c.parts[0]
    bpy.ops.object.join()
    shield = bpy.context.active_object
    shield.name = shield.data.name = "BullgrynShield"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.ops.export_scene.fbx(filepath=OUT_SHIELD, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="OFF", path_mode="AUTO")
    print("Exported", OUT_SHIELD)
    if RENDER_DIR:
        c.render_previews(os.path.join(RENDER_DIR, "shield"), focus_z=0.9, distance=0.8)


export_shield_prop()
