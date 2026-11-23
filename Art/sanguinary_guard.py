"""Builds the Sanguinary Guard: burnished gold artificer armour over a black bodyglove, a
gold death-mask helm framed by a sunburst halo, a red right pauldron and a white winged left one, ruby blood drops
hanging from the collar and belt, a white scroll across the chest, great white feathered wings on the jump pack, a
narthecium on the left arm, the Blood Chalice at his hip, and a long chain-toothed blade held upright in the right hand.

Based on a reference image the user supplied (used as a guide only, nothing from it is imported).

    blender -b --python Art/sanguinary_guard.py                    # writes Assets/Models/SanguinaryGuard.fbx
    blender -b --python Art/sanguinary_guard.py -- --render DIR    # also renders preview PNGs into DIR

The blade sits on its own bone ("Chainsword", from the grip up the blade) and is modelled pointing straight up at
rest: BloodBladeEffect reads the blade direction from that.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "SanguinaryGuard.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(0.95)

mat("armor", (0.78, 0.55, 0.22), metallic=0.9, rough=0.28)          # burnished gold
mat("armor_dark", (0.5, 0.34, 0.12), metallic=0.85, rough=0.35)
mat("gold", (0.98, 0.78, 0.35), metallic=0.95, rough=0.2)            # bright trim
mat("red", (0.62, 0.04, 0.05), metallic=0.4, rough=0.35)
mat("red_dark", (0.32, 0.02, 0.03), rough=0.6)
mat("black", (0.04, 0.04, 0.05), metallic=0.3, rough=0.5)
mat("steel", (0.55, 0.62, 0.68), metallic=0.9, rough=0.25)
mat("white", (0.93, 0.93, 0.92), rough=0.6)
mat("feather_tip", (0.72, 0.84, 0.9), rough=0.6)                    # cold blue shading at the feather ends
# The wings get their own copies of those two, so the game can find them by name and set them blazing during the
# ultimate (WingsOfTheAngel swaps "wing" / "wing_tip" for glowing gold).
mat("wing", (0.93, 0.93, 0.92), rough=0.6)
mat("wing_tip", (0.72, 0.84, 0.9), rough=0.6)
mat("parchment", (0.9, 0.86, 0.74), rough=0.7)
mat("ruby", (0.75, 0.02, 0.05), metallic=0.3, rough=0.15, emit=(0.6, 0.0, 0.02), emit_strength=0.6)
mat("eye", (1.0, 0.2, 0.12), emit=(1.0, 0.15, 0.08), emit_strength=3.0)
mat("thruster", (0.35, 0.7, 1.0), emit=(0.3, 0.65, 1.0), emit_strength=2.5)   # blue burner glow

ARMS = {
    # Left: forearm across the body, the narthecium forward. Right: the blade held upright beside him.
    "L": dict(sh=(0.62, 0, 1.65), el=(0.86, -0.1, 1.32), wr=(0.62, -0.42, 1.24)),
    # Right arm hanging at his side, the sword held low and angled forward, as in the reference art.
    "R": dict(sh=(-0.62, 0, 1.65), el=(-0.84, -0.04, 1.3), wr=(-0.86, -0.26, 1.06)),
}

# The blade runs from the grip down and forward, a little out to his right: the point rests near the ground ahead of him.
SWORD_DIR = Vector((-0.3, -0.82, -0.5)).normalized()
SWORD_ROT = SWORD_DIR.to_track_quat("Z", "Y").to_matrix()
BLADE = 1.4   # blade length from the chain-wheel to the start of the point (was 1.04)


def _grip(side):
    wr, el = Vector(ARMS[side]["wr"]), Vector(ARMS[side]["el"])
    return tuple(wr + (wr - el).normalized() * 0.05)


SWORD_GRIP = _grip("R")


def drop(loc, size, bone, material="ruby"):
    """A blood drop: a round bottom with a point rising out of it (hanging gems, inlays)."""
    x, y, z = loc
    prim("sphere", (x, y, z), (size, size * 0.7, size), material, bone, verts=10)
    limb((x, y, z + size * 0.25), (x, y, z + size * 1.1), size * 0.42, 0.004, material, bone, verts=8)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.24 * s
        prim("cube", (x, -0.1, 0.12), (0.38, 0.6, 0.24), "armor", f"Foot_{side}", bevel=0.08)
        prim("cube", (x, -0.1, 0.02), (0.4, 0.62, 0.05), "armor_dark", f"Foot_{side}", bevel=0.01)
        limb((x, 0, 0.2), (x, 0, 0.62), 0.24, 0.21, "armor", f"Shin_{side}")
        prim("cube", (x, -0.22, 0.4), (0.3, 0.06, 0.4), "armor", f"Shin_{side}", bevel=0.03)   # greave
        drop((x, -0.26, 0.36), 0.07, f"Shin_{side}")
        prim("sphere", (x, -0.1, 0.64), (0.38, 0.35, 0.34), "armor", f"Shin_{side}")          # knee
        limb((x, 0, 0.66), (x, 0, 1.02), 0.21, 0.25, "armor", f"Thigh_{side}")
        prim("cube", (x, -0.2, 0.86), (0.28, 0.06, 0.3), "armor", f"Thigh_{side}", bevel=0.04)  # thigh plate
    # A small gold death mask on the left knee, as on the reference.
    prim("sphere", (0.24, -0.3, 0.66), (0.14, 0.06, 0.18), "gold", "Shin_L", verts=14)
    for x in (0.2, 0.28):
        prim("sphere", (x, -0.33, 0.7), (0.04, 0.02, 0.02), "black", "Shin_L", verts=8)
    for a in (-40, 0, 40):  # spikes crowning it
        r = math.radians(a)
        limb((0.24 + math.sin(r) * 0.08, -0.29, 0.74 + math.cos(r) * 0.06),
             (0.24 + math.sin(r) * 0.2, -0.29, 0.74 + math.cos(r) * 0.2), 0.03, 0.004, "gold", "Shin_L", verts=6)


def tabard():
    """A red cloth hanging from under the belt buckle almost to the knees' bottom, a little wider at the hem, with darker
    folds running down it and a white winged blood drop on it, after the reference."""
    y = -0.335
    top, hem = 1.15, 0.3
    flat_poly([(-0.17, top), (0.17, top), (0.21, hem + 0.03), (0.0, hem - 0.02), (-0.21, hem + 0.03)], y, 0.025, "red", "Hips")
    # Folds: darker creases running from the belt and spreading toward the hem.
    for x0, x1 in ((-0.09, -0.12), (0.0, 0.01), (0.1, 0.13)):
        limb((x0, y - 0.016, top - 0.05), (x1, y - 0.016, hem + 0.06), 0.012, 0.02, "red_dark", "Hips", verts=6)
    # Gold hem band.
    flat_poly([(-0.21, hem + 0.03), (0.0, hem - 0.02), (0.21, hem + 0.03), (0.21, hem + 0.07), (0.0, hem + 0.02),
               (-0.21, hem + 0.07)], y - 0.016, 0.01, "gold", "Hips")
    # Emblem: a white blood drop between two small swept wings, two thirds of the way down.
    ez = 0.6
    prim("sphere", (0, y - 0.018, ez), (0.07, 0.01, 0.07), "white", "Hips", verts=12)
    flat_poly([(-0.03, ez + 0.02), (0.03, ez + 0.02), (0, ez + 0.12)], y - 0.018, 0.01, "white", "Hips")
    for s in (1, -1):
        wing = [(0.04, ez + 0.02), (0.1, ez + 0.1), (0.17, ez + 0.13), (0.15, ez + 0.07), (0.17, ez + 0.03),
                (0.12, ez + 0.03), (0.13, ez - 0.01), (0.06, ez - 0.01)]
        pts = [(u * s, z) for u, z in wing]
        flat_poly(pts if s > 0 else pts[::-1], y - 0.018, 0.01, "white", "Hips")


def waist():
    """Black bodyglove at the waist, a gold belt with a skull buckle, and ruby drops hanging from it."""
    prim("cube", (0, 0, 1.06), (0.7, 0.46, 0.3), "black", "Hips", bevel=0.08)
    prim("cyl", (0, 0, 1.2), (0.84, 0.56, 0.08), "armor_dark", "Hips", bevel=0.01, verts=24)
    prim("cube", (0, -0.29, 1.2), (0.3, 0.04, 0.12), "gold", "Hips", bevel=0.01)
    prim("sphere", (0, -0.32, 1.2), (0.1, 0.05, 0.1), "parchment", "Hips", verts=12)          # skull
    for x in (-0.03, 0.03):
        prim("sphere", (x, -0.355, 1.21), (0.03, 0.02, 0.03), "black", "Hips", verts=8)
    for x in (-0.27, 0.27):
        limb((x, -0.29, 1.16), (x, -0.3, 1.04), 0.012, 0.012, "gold", "Hips", verts=6)
        drop((x, -0.3, 0.98), 0.06, "Hips")
    tabard()
    # Side tassets.
    for s in (1, -1):
        prim("cube", (0.37 * s, -0.04, 0.94), (0.16, 0.48, 0.34), "armor", "Hips", rot=(0, -12 * s, 0), bevel=0.04)
        drop((0.45 * s, -0.25, 0.8), 0.05, "Hips")


def torso():
    prim("cube", (0, 0, 1.55), (0.96, 0.6, 0.7), "armor", "Spine", bevel=0.15)
    # Sculpted muscles on the breastplate: pectorals and abdominal plates.
    for s in (1, -1):
        prim("sphere", (0.18 * s, -0.27, 1.64), (0.34, 0.12, 0.24), "armor", "Spine", verts=16)
        for i in range(3):
            prim("cube", (0.08 * s, -0.29, 1.44 - i * 0.09), (0.13, 0.05, 0.075), "armor", "Spine", bevel=0.02)
    # A white scroll banner across the chest, its ends curling.
    prim("cube", (0, -0.34, 1.79), (0.56, 0.03, 0.1), "parchment", "Spine", bevel=0.01)
    for s in (1, -1):
        prim("cyl", (0.3 * s, -0.33, 1.79), (0.06, 0.06, 0.12), "parchment", "Spine", verts=10)
    # Collar with a row of ruby drops hanging from it, and the high gorget the helm sits in.
    prim("cyl", (0, 0, 1.92), (0.64, 0.54, 0.2), "armor", "Spine", bevel=0.02)
    prim("cyl", (0, 0, 2.0), (0.66, 0.56, 0.04), "gold", "Spine", bevel=0.005)
    for x in (-0.24, -0.08, 0.08, 0.24):
        drop((x, -0.3, 1.86), 0.045, "Spine")


def pauldrons():
    # Right: a red pauldron trimmed in gold, a scroll over it. Left: a white pauldron carved as a folded wing.
    x, z = -0.7, 1.74
    c.pauldron_block(x, z, -1, 0.9, 0.76, 0.66, "UpperArm_R", mat="red", trim="gold", tilt=16)
    prim("cube", (x - 0.02, -0.36, z - 0.22), (0.56, 0.03, 0.1), "parchment", "UpperArm_R", rot=(0, -16, 0), bevel=0.01)
    drop((x - 0.3, -0.2, z - 0.3), 0.05, "UpperArm_R")

    x = 0.7
    c.pauldron_block(x, z, 1, 0.9, 0.76, 0.66, "UpperArm_L", mat="white", trim="gold", tilt=16)
    # A gold blood drop on its face.
    drop((x + 0.08, -0.37, z - 0.1), 0.08, "UpperArm_L", material="gold")


def arms():
    for side, a in ARMS.items():
        limb(a["sh"], a["el"], 0.18, 0.16, "black", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.33, 0.33, 0.33), "armor", f"UpperArm_{side}")
        limb(a["el"], a["wr"], 0.18, 0.2, "armor", f"Forearm_{side}")
        mid = (Vector(a["el"]) + Vector(a["wr"])) / 2
        rot = tuple(math.degrees(e) for e in (Vector(a["wr"]) - Vector(a["el"])).to_track_quat("Z", "Y").to_euler())
        prim("cyl", tuple(mid), (0.36, 0.36, 0.08), "gold", f"Forearm_{side}", rot=rot, verts=16)
        prim("sphere", _grip(side), (0.3, 0.3, 0.3), "armor", f"Hand_{side}")
    # Upper-arm plates over the black bodyglove.
    for side in ("L", "R"):
        a = ARMS[side]
        prim("sphere", tuple((Vector(a["sh"]) * 0.4 + Vector(a["el"]) * 0.6)), (0.3, 0.3, 0.3), "armor",
             f"UpperArm_{side}", verts=16)

    # Narthecium on the left forearm: a white housing with a red cross, a blood vial and a reductor spike.
    a = ARMS["L"]
    el, wr = Vector(a["el"]), Vector(a["wr"])
    d = (wr - el).normalized()
    rot = tuple(math.degrees(e) for e in d.to_track_quat("Z", "Y").to_euler())
    box = el + (wr - el) * 0.55 + Vector((0, 0, 0.13))
    prim("cube", tuple(box), (0.24, 0.3, 0.2), "white", "Forearm_L", rot=rot, bevel=0.03)
    prim("cube", tuple(box + Vector((0, 0, 0.11))), (0.12, 0.04, 0.03), "red", "Forearm_L", rot=rot)
    prim("cube", tuple(box + Vector((0, 0, 0.11))), (0.04, 0.12, 0.03), "red", "Forearm_L", rot=rot)
    prim("cyl", tuple(box + Vector((0.1, 0, 0.1))), (0.06, 0.06, 0.16), "ruby", "Forearm_L", rot=rot, verts=10)
    limb(tuple(wr + d * 0.08), tuple(wr + d * 0.3), 0.04, 0.008, "steel", "Hand_L", verts=8)


def helm():
    """Gold death-mask helm: a calm angel's face with red lenses and a ruby on the brow, framed by a sunburst halo
    behind the head, with a tall spike rising from its top."""
    prim("sphere", (0, -0.02, 2.06), (0.44, 0.48, 0.5), "armor", "Head", verts=24)
    prim("sphere", (0, -0.2, 2.02), (0.32, 0.12, 0.38), "gold", "Head", verts=20)              # face plate
    prim("cube", (0, -0.27, 2.03), (0.03, 0.04, 0.12), "armor_dark", "Head", bevel=0.01)       # nose
    for i in range(4):                                                                        # mouth grille
        prim("cube", (-0.045 + i * 0.03, -0.265, 1.92), (0.012, 0.02, 0.07), "black", "Head")
    for x in (-0.09, 0.09):
        prim("sphere", (x, -0.265, 2.09), (0.085, 0.03, 0.045), "eye", "Head", verts=10)
    drop((0, -0.27, 2.16), 0.045, "Head")                                                     # brow ruby
    # Sunburst halo: a gold disc standing behind the head with rays and a spike on top.
    hy, hz = 0.12, 2.18
    prim("cyl", (0, hy, hz), (0.62, 0.62, 0.05), "gold", "Head", rot=(90, 0, 0), verts=32)
    torus((0, hy - 0.03, hz), 0.27, 0.025, "armor_dark", "Head", rot=(90, 0, 0))
    for i in range(16):
        ang = i * math.pi * 2 / 16
        if abs(math.sin(ang)) > 0.97 and math.cos(ang) < 0:  # leave the bottom clear for the neck
            continue
        r0, r1 = 0.18, 0.36 if i % 2 == 0 else 0.3
        limb((math.cos(ang) * r0, hy - 0.035, hz + math.sin(ang) * r0),
             (math.cos(ang) * r1, hy - 0.035, hz + math.sin(ang) * r1), 0.022, 0.006, "armor_dark", "Head", verts=6)
    limb((0, hy - 0.02, hz + 0.3), (0, hy - 0.02, hz + 0.62), 0.06, 0.006, "gold", "Head", verts=8)


def _feather(root, angle, length, width, y, material, bone="Spine"):
    """A flat feather in the XZ plane at depth y, hanging from `root` and pointing `angle` degrees from straight down
    (positive = outward to +X)."""
    a = math.radians(angle)
    dx, dz = math.sin(a), -math.cos(a)           # along the feather
    nx, nz = -dz, dx                             # across it
    rx, rz = root
    w = width / 2
    pts = [(rx - nx * w * 0.5, rz - nz * w * 0.5), (rx + nx * w * 0.5, rz + nz * w * 0.5),
           (rx + dx * length * 0.7 + nx * w, rz + dz * length * 0.7 + nz * w),
           (rx + dx * length, rz + dz * length),
           (rx + dx * length * 0.7 - nx * w, rz + dz * length * 0.7 - nz * w)]
    flat_poly(pts, y, 0.025, material, bone)


# +X is his left (the right arm sits at -X). Each wing is skinned to its own bone, pivoting at the wing mount, so the
# game can spread and beat it (Wings of the Angel).
WING_BONE = {1: "Wing_L", -1: "Wing_R"}
WING_MOUNT = (0.45, 0.72, 1.8)
WING_TIP = (1.06, 0.72, 2.72)


def wings():
    """Jump pack and two great white wings: an arm that sweeps up and out and hooks over at the top, with long primaries
    fanning down and out beneath it and shorter covert rows layered over their roots."""
    jump_pack()

    y0 = 0.72  # the wings hang behind the pack, mounted on its sides below the engine tops
    k = 0.72  # wing size: kept narrow enough that he still reads as one character from the top-down camera
    for s in (1, -1):
        bone = WING_BONE[s]
        # The wing's leading edge: out from the pack's side, up past the engines, curling over at the tip.
        arm = [(0.45 + (x - 0.45) * k, 1.8 + (z - 1.8) * k)
               for x, z in ((0.45, 1.8), (0.65, 2.4), (0.95, 2.9), (1.3, 3.08), (1.55, 2.95))]
        prim("cube", (0.45 * s, 0.62, 1.8), (0.14, 0.2, 0.22), "armor_dark", "Spine", bevel=0.03)  # wing mount
        for (ax, az), (bx, bz) in zip(arm, arm[1:]):
            limb((ax * s, y0, az), (bx * s, y0, bz), 0.07, 0.06, "wing", bone, verts=10)
        # Primaries: long feathers hanging from the edge, longer and more splayed toward the tip.
        for i in range(9):
            t = i / 8.0
            seg = min(3, int(t * 4))
            u = t * 4 - seg
            (ax, az), (bx, bz) = arm[seg], arm[seg + 1]
            rx, rz = ax + (bx - ax) * u, az + (bz - az) * u
            length = (1.0 + 0.9 * t) * k
            angle = 6 + 30 * t
            _feather((rx * s, rz), angle * s, length, 0.24 * k, y0 + 0.05 + 0.01 * i, "wing_tip" if i % 2 else "wing", bone)
            _feather((rx * s, rz), angle * s, length * 0.86, 0.22 * k, y0 + 0.02 + 0.01 * i, "wing", bone)
        # Coverts: two rows of short feathers over the primaries' roots.
        for row, (ln, off) in enumerate(((0.55, 0.0), (0.32, 0.08))):
            for i in range(8):
                t = (i + 0.5) / 8.0
                seg = min(3, int(t * 4))
                u = t * 4 - seg
                (ax, az), (bx, bz) = arm[seg], arm[seg + 1]
                rx, rz = ax + (bx - ax) * u, az + (bz - az) * u - off * k
                _feather((rx * s, rz), (10 + 26 * t) * s, ln * k, 0.22 * k, y0 - 0.02 - 0.02 * row, "wing", bone)


def engine_axis(s):
    """Where one turbine sits: its intake end (top, by the shoulder) and the unit direction it points, down, out to the
    side and a little back. Shared by the mesh and the Jet bones so the flame leaves the nozzle along its axis."""
    top = Vector((0.36 * s, 0.5, 2.38))
    d = Vector((0.55 * s, 0.25, -1.0)).normalized()
    return top, d


ENGINE_LENGTH = 1.05


def jump_pack():
    """The jump pack: a heavy gold pack on his back with two big turbine engines mounted at its upper corners, angled
    down and out to the sides. Each is fattest round the turbine, a rounded intake by the shoulder at the top and a
    tapered tail ending in an exhaust nozzle that points down and outward (where JetPackFx draws the blue flame)."""
    prim("cube", (0, 0.38, 1.62), (0.56, 0.36, 0.84), "armor", "Spine", bevel=0.08)
    prim("cube", (0, 0.57, 1.62), (0.3, 0.04, 0.5), "armor_dark", "Spine", bevel=0.01)       # rear access plate
    drop((0, 0.6, 1.62), 0.07, "Spine")
    for s in (1, -1):
        top, d = engine_axis(s)
        rot = tuple(math.degrees(e) for e in d.to_track_quat("Z", "Y").to_euler())

        def at(t):
            return tuple(top + d * t)

        # Intake: a rounded cowl with a black mouth and a steel grille, facing up and in toward the shoulder.
        prim("sphere", at(0.12), (0.5, 0.5, 0.36), "armor", "Spine", rot=rot, verts=20)
        prim("cyl", at(0.0), (0.34, 0.34, 0.04), "black", "Spine", rot=rot, verts=20)
        for i in range(5):
            prim("cube", at(-0.01), (0.03, 0.3 - abs(i - 2) * 0.06, 0.02), "steel", "Spine", rot=rot)
        # Turbine: the fat middle, banded in bright gold.
        prim("sphere", at(0.38), (0.6, 0.6, 0.62), "armor", "Spine", rot=rot, verts=24)
        for t in (0.24, 0.52):
            prim("cyl", at(t), (0.56, 0.56, 0.05), "gold", "Spine", rot=rot, verts=24)
        drop(at(0.38), 0.06, "Spine")  # ruby on the cowling (sits inside; reads as a glint)
        # Tail cone narrowing to the nozzle, then the flared steel nozzle with the burner glowing in it.
        limb(at(0.6), at(0.86), 0.26, 0.15, "armor", "Spine", verts=20)
        limb(at(0.86), at(ENGINE_LENGTH), 0.13, 0.19, "steel", "Spine", verts=18)
        prim("cyl", at(ENGINE_LENGTH - 0.01), (0.3, 0.3, 0.02), "thruster", "Spine", rot=rot, verts=18)
        # Strut bolting the engine to the pack.
        limb((0.22 * s, 0.48, 1.9), at(0.38), 0.07, 0.07, "armor_dark", "Spine", verts=10)


def chalice():
    """The Blood Chalice hanging at his left hip on a short chain."""
    x, y, z = 0.44, -0.24, 0.98
    prim("cyl", (x, y, z - 0.12), (0.14, 0.14, 0.03), "gold", "Hips", verts=14)
    prim("cyl", (x, y, z - 0.04), (0.04, 0.04, 0.14), "gold", "Hips", verts=10)
    prim("sphere", (x, y, z + 0.03), (0.07, 0.07, 0.05), "gold", "Hips", verts=10)
    prim("cyl", (x, y, z + 0.1), (0.18, 0.18, 0.12), "gold", "Hips", verts=16)
    prim("cyl", (x, y, z + 0.155), (0.15, 0.15, 0.02), "ruby", "Hips", verts=16)
    limb((x, y, z + 0.16), (0.36, -0.2, 1.18), 0.015, 0.015, "gold", "Hips", verts=6)


def chainsword():
    """A long Encarmine-style blade held low at his right side, the point angled forward toward the ground, as
    in the reference art. An ornate hilt: a ruby-capped spiked pommel, a red grip bound in gold, a winged gold
    crossguard sweeping up toward the blade with a blood drop at its heart, and a crown of layered steel fins (like
    stacked feathers) clasping the base of the blade, then a straight steel blade with a fuller.
    Modelled along +Z from the grip, then turned as a whole onto SWORD_DIR (the Chainsword bone runs the same way, and
    Chainsword_Tip marks the point, so BloodBladeEffect follows the blade at any angle)."""
    px, py, pz = SWORD_GRIP
    b = "Chainsword"
    p = Vector(SWORD_GRIP) * c.S
    xf = Matrix.Translation(p) @ SWORD_ROT.to_4x4() @ Matrix.Translation(-p)

    def P(u, z):  # (sideways, up the sword) in the sword's own frame -> flat_poly coordinates
        return (px + u, pz + z)

    # Pommel: a gold knot with a ruby and a short spike.
    prim("sphere", (px, py, pz - 0.25), (0.13, 0.13, 0.11), "gold", b, xf=xf, verts=14)
    prim("sphere", (px, py - 0.06, pz - 0.25), (0.06, 0.03, 0.06), "ruby", b, xf=xf, verts=10)
    limb((px, py, pz - 0.3), (px, py, pz - 0.44), 0.035, 0.004, "gold", b, xf=xf, verts=8)
    # Grip: red wrap bound with gold rings.
    prim("cyl", (px, py, pz - 0.05), (0.08, 0.08, 0.36), "red", b, xf=xf, verts=12)
    for z in (-0.18, -0.05, 0.08):
        prim("cyl", (px, py, pz + z), (0.095, 0.095, 0.025), "gold", b, xf=xf, verts=12)

    # Crossguard: a gold boss with a blood drop, and a swept wing on each side curling up toward the blade.
    gz = 0.16
    prim("sphere", (px, py, pz + gz), (0.2, 0.12, 0.14), "gold", b, xf=xf, verts=16)
    for s in (1, -1):
        wing = [(0.06, gz - 0.05), (0.2, gz - 0.06), (0.34, gz - 0.02), (0.44, gz + 0.08), (0.48, gz + 0.22),
                (0.4, gz + 0.14), (0.36, gz + 0.2), (0.3, gz + 0.1), (0.24, gz + 0.14), (0.18, gz + 0.05), (0.06, gz + 0.05)]
        pts = [P(u * s, z) for u, z in wing]
        flat_poly(pts if s > 0 else pts[::-1], py, 0.06, "gold", b, xf=xf)
        # Feather grooves picked out on its face.
        for u in (0.2, 0.3, 0.4):
            prim("cube", (px + u * s, py - 0.035, pz + gz + 0.03 + (u - 0.2) * 0.4), (0.015, 0.01, 0.1), "armor_dark", b,
                 xf=xf, rot=(0, -35 * s, 0))
    prim("sphere", (px, py - 0.08, pz + gz), (0.07, 0.03, 0.07), "ruby", b, xf=xf, verts=10)
    limb((px, py - 0.08, pz + gz + 0.02), (px, py - 0.08, pz + gz + 0.11), 0.03, 0.003, "ruby", b, xf=xf, verts=8)

    # A crown of steel fins clasping the forte: layered blades fanning up and out, longest in the middle row.
    for row, (z0, spread, length) in enumerate(((0.22, 0.2, 0.26), (0.3, 0.16, 0.3), (0.38, 0.12, 0.22))):
        for s in (1, -1):
            for k in range(2):
                u0 = (0.05 + k * 0.05) * s
                fin = [(u0, z0), (u0 + 0.05 * s, z0 - 0.02), (u0 + (spread + 0.05 + k * 0.04) * s, z0 + length * 0.55),
                       (u0 + (spread * 0.6 + k * 0.03) * s, z0 + length)]
                pts = [P(u, z) for u, z in fin]
                flat_poly(pts if s > 0 else pts[::-1], py + (0.012 if k else -0.012), 0.025,
                          "steel" if (row + k) % 2 == 0 else "armor_dark", b, xf=xf)
    # Gold ricasso collar where the blade leaves the fins.
    prim("cube", (px, py, pz + 0.36), (0.14, 0.08, 0.12), "gold", b, xf=xf, bevel=0.02)
    # The blade: about a third longer than before.
    prim("cube", (px, py, pz + 0.34 + BLADE / 2), (0.17, 0.05, BLADE), "steel", b, xf=xf, bevel=0.012)
    prim("cube", (px, py - 0.027, pz + 0.3 + BLADE * 0.45), (0.05, 0.01, BLADE * 0.8), "armor_dark", b, xf=xf)
    tip0 = pz + 0.34 + BLADE
    flat_poly([(px - 0.085, tip0), (px + 0.085, tip0), (px, tip0 + 0.18)], py, 0.05, "steel", b, xf=xf)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.2)),
    ("Hips", "Root", (0, 0, 1.0), (0, 0, 1.2)),
    ("Spine", "Hips", (0, 0, 1.2), (0, 0, 1.8)),
    ("Head", "Spine", (0, 0, 1.8), (0, 0, 2.2)),
]
for _side, _a in ARMS.items():
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.14)),
    ]
_tipPoint = Vector(SWORD_GRIP) + SWORD_DIR * (0.34 + BLADE + 0.18)
BONES.append(("Chainsword", "Hand_R", SWORD_GRIP, tuple(Vector(SWORD_GRIP) + SWORD_DIR * 0.4)))
BONES.append(("Chainsword_Tip", "Chainsword", tuple(_tipPoint), tuple(_tipPoint + SWORD_DIR * 0.1)))
# Wing bones: each pivots at its wing mount and points out to the wing's crest (Wings of the Angel spreads and beats them).
for _s in (1, -1):
    BONES.append((WING_BONE[_s], "Spine", (WING_MOUNT[0] * _s, WING_MOUNT[1], WING_MOUNT[2]),
                  (WING_TIP[0] * _s, WING_TIP[1], WING_TIP[2])))
# Jet exhaust markers: no mesh of their own. Jet_L / Jet_R sit at each nozzle's mouth and Jet_L_Tip / Jet_R_Tip a little
# further along the engine's axis, so JetPackFx reads the exhaust direction from the pair (whatever the pose).
for _s, _side in ((1, "L"), (-1, "R")):
    _top, _d = engine_axis(_s)
    _mouth = _top + _d * ENGINE_LENGTH
    _tip = _mouth + _d * 0.25
    BONES.append((f"Jet_{_side}", "Spine", tuple(_mouth), tuple(_tip)))
    BONES.append((f"Jet_{_side}_Tip", f"Jet_{_side}", tuple(_tip), tuple(_tip + _d * 0.1)))
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.24 * _s, 0, 1.0), (0.24 * _s, 0, 0.6)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.24 * _s, 0, 0.6), (0.24 * _s, 0, 0.2)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.24 * _s, 0, 0.2), (0.24 * _s, -0.3, 0.05)),
    ]

legs()
waist()
torso()
pauldrons()
arms()
helm()
wings()
chalice()
chainsword()

armature = c.build_armature(BONES)
mesh = c.assemble("SanguinaryGuard", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.6, distance=1.5)
