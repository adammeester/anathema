"""Builds the Skitarii Ranger: a cybernetic Adeptus Mechanicus marksman. Deep red hooded robe over riveted steel plate,
a skull-like breather mask with glowing cyan lenses, a backpack with a cyan camera-eye and a tall tuning-fork antenna,
and a very long bronze transuranic arquebus held two-handed at the shoulder.

Based on the reference artwork (red-robed Skitarii rangers with galvanic rifles; used as a guide only, nothing from it
is imported).

    blender -b --python Art/skitarii_ranger.py                    # writes Assets/Models/SkitariiRanger.fbx
    blender -b --python Art/skitarii_ranger.py -- --render DIR    # also renders preview PNGs into DIR

About 1.9 m tall (a baseline human augmented with plate; shorter than the marines), same skeleton naming as the other
characters (Hips, Spine, Head, UpperArm_L, Thigh_L, ...) so ProceduralWalk works unchanged, plus a Rifle_R bone (pivots at the right hand) on the
right hand.
"""
import math
import bpy
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import common as c  # noqa: E402
from common import flat_poly, limb, mat, prim, torus  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

OUT_FBX = os.path.normpath(os.path.join(HERE, "..", "Assets", "Models", "SkitariiRanger.fbx"))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER_DIR = argv[argv.index("--render") + 1] if "--render" in argv else None

c.configure(1.0)

mat("robe", (0.55, 0.05, 0.04), rough=0.85)
mat("robe_dark", (0.25, 0.02, 0.025), rough=0.9)
mat("trim", (0.9, 0.88, 0.82), rough=0.8)
mat("steel", (0.28, 0.29, 0.3), metallic=0.8, rough=0.4)
mat("steel_dark", (0.12, 0.12, 0.13), metallic=0.8, rough=0.5)
mat("bronze", (0.5, 0.31, 0.14), metallic=0.9, rough=0.4)
mat("bronze_dark", (0.27, 0.17, 0.08), metallic=0.85, rough=0.5)
mat("leather", (0.14, 0.09, 0.06), rough=0.8)
mat("glove", (0.07, 0.07, 0.08), rough=0.7)
mat("bone", (0.8, 0.76, 0.62), rough=0.7)
mat("lens", (0.4, 0.95, 1.0), emit=(0.25, 0.85, 1.0), emit_strength=8.0)
mat("lens_soft", (0.2, 0.6, 0.8), emit=(0.15, 0.5, 0.8), emit_strength=2.5)


def rivets(points, size, material, bone):
    for p in points:
        prim("sphere", p, (size, size, size), material, bone, verts=8)


def legs():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.11 * s
        prim("cube", (x, -0.07, 0.07), (0.14, 0.34, 0.14), "steel_dark", f"Foot_{side}", bevel=0.03)  # armoured boot
        prim("cube", (x, -0.07, 0.01), (0.15, 0.35, 0.025), "leather", f"Foot_{side}", bevel=0.005)
        limb((x, 0, 0.12), (x, 0, 0.5), 0.085, 0.07, "steel_dark", f"Shin_{side}")
        prim("cube", (x, -0.07, 0.3), (0.11, 0.05, 0.28), "steel", f"Shin_{side}", bevel=0.02)  # greave
        torus((x, 0, 0.46), 0.08, 0.012, "bronze_dark", f"Shin_{side}")
        prim("sphere", (x, -0.06, 0.52), (0.15, 0.13, 0.13), "steel", f"Shin_{side}", verts=12)  # knee plate
        limb((x, 0, 0.52), (x, 0, 0.98), 0.08, 0.1, "robe_dark", f"Thigh_{side}")
        prim("cube", (x, -0.07, 0.78), (0.11, 0.04, 0.2), "steel_dark", f"Thigh_{side}", bevel=0.015)
    for s in (-1, 1):  # ammunition pouches on the belt
        prim("cube", (0.23 * s, 0.0, 0.96), (0.08, 0.15, 0.13), "leather", "Hips", bevel=0.015)
        prim("cube", (0.23 * s, -0.05, 1.0), (0.085, 0.05, 0.05), "bronze_dark", "Hips", bevel=0.008)


def torso():
    prim("cube", (0, 0, 1.03), (0.4, 0.26, 0.2), "robe_dark", "Hips", bevel=0.05)
    prim("cube", (0, 0, 1.26), (0.46, 0.3, 0.4), "steel_dark", "Spine", bevel=0.07)  # breastplate core
    prim("cube", (0, -0.15, 1.3), (0.38, 0.05, 0.3), "steel", "Spine", bevel=0.03)
    # Skull-and-cog emblem and a ribbed bronze vent.
    prim("cyl", (0, -0.185, 1.34), (0.13, 0.13, 0.03), "bronze", "Spine", rot=(90, 0, 0), verts=16)
    prim("sphere", (0, -0.2, 1.34), (0.07, 0.03, 0.08), "bone", "Spine", verts=10)
    for i in range(4):
        prim("cube", (0, -0.18, 1.14 - 0.035 * i), (0.2, 0.02, 0.015), "bronze_dark", "Spine")
    rivets([(-0.17, -0.18, 1.44), (0.17, -0.18, 1.44), (-0.17, -0.18, 1.18), (0.17, -0.18, 1.18)], 0.02, "bronze", "Spine")
    # Belt with a ringed buckle.
    prim("cyl", (0, 0, 1.0), (0.42, 0.29, 0.07), "leather", "Hips", verts=20)
    prim("cyl", (0, -0.14, 1.0), (0.1, 0.1, 0.03), "bronze", "Hips", rot=(90, 0, 0), verts=14)
    # Gorget / mask collar with feed hoses to the backpack.
    prim("cyl", (0, 0, 1.5), (0.3, 0.26, 0.1), "steel_dark", "Spine", verts=20)
    torus((0, 0, 1.54), 0.14, 0.014, "bronze", "Spine", squash_y=0.9)
    for s in (-1, 1):
        limb((0.1 * s, -0.04, 1.5), (0.22 * s, 0.1, 1.36), 0.018, 0.018, "steel_dark", "Spine", verts=8)
        limb((0.22 * s, 0.1, 1.36), (0.2 * s, 0.2, 1.2), 0.018, 0.018, "bronze_dark", "Spine", verts=8)

    # Long robe: front and back panels hanging to the shins, with a pale checkered trim down the edge.
    front = [(-0.2, 1.02), (0.2, 1.02), (0.24, 0.7), (0.22, 0.4), (0.14, 0.3), (0.07, 0.4), (0.0, 0.28),
             (-0.07, 0.4), (-0.14, 0.3), (-0.22, 0.4), (-0.24, 0.7)]
    flat_poly(front, -0.15, 0.02, "robe", "Hips")
    flat_poly([(0.0, 1.0), (0.05, 1.0), (0.07, 0.4), (0.0, 0.28), (-0.01, 0.4)], -0.162, 0.01, "trim", "Hips")
    back = [(-0.23, 1.02), (0.23, 1.02), (0.27, 0.68), (0.25, 0.34), (0.16, 0.16), (0.08, 0.3), (0.0, 0.12),
            (-0.08, 0.3), (-0.16, 0.16), (-0.25, 0.34), (-0.27, 0.68)]
    flat_poly(back, 0.15, 0.02, "robe", "Hips")
    for x in (-0.13, 0.0, 0.13):  # folds
        prim("cube", (x, 0.165, 0.62), (0.025, 0.02, 0.5), "robe_dark", "Hips")
    # Numbered ID plate on the hem (the artwork's "42").
    prim("cube", (-0.12, -0.17, 0.62), (0.08, 0.01, 0.1), "trim", "Hips")
    prim("sphere", (-0.12, -0.18, 0.66), (0.025, 0.01, 0.025), "robe", "Hips", verts=8)


def backpack():
    """Power backpack with a camera-eye box on top and a tall tuning-fork antenna."""
    prim("cube", (0, 0.2, 1.3), (0.4, 0.2, 0.46), "steel_dark", "Spine", bevel=0.04)
    prim("cube", (0, 0.31, 1.3), (0.32, 0.04, 0.36), "bronze_dark", "Spine", bevel=0.015)
    for i in range(3):
        prim("cube", (-0.08 + 0.08 * i, 0.335, 1.3), (0.035, 0.02, 0.26), "steel", "Spine")
    # Cyan camera-eye on the right shoulder of the pack.
    prim("cube", (-0.13, 0.22, 1.6), (0.14, 0.14, 0.12), "steel", "Spine", bevel=0.02)
    prim("sphere", (-0.13, 0.145, 1.6), (0.07, 0.04, 0.07), "lens", "Spine", verts=12)
    # Tuning-fork antenna.
    limb((0.12, 0.22, 1.5), (0.12, 0.22, 2.12), 0.014, 0.012, "steel", "Spine", verts=8)
    limb((0.12, 0.22, 2.1), (0.09, 0.22, 2.28), 0.011, 0.011, "bronze", "Spine", verts=6)
    limb((0.12, 0.22, 2.1), (0.15, 0.22, 2.28), 0.011, 0.011, "bronze", "Spine", verts=6)
    prim("sphere", (0.12, 0.22, 1.82), (0.04, 0.04, 0.04), "lens_soft", "Spine", verts=8)
    # Exhaust pipes.
    for s in (-1, 1):
        prim("cyl", (0.17 * s, 0.24, 1.1), (0.05, 0.05, 0.2), "steel", "Spine", verts=10)
        prim("cyl", (0.17 * s, 0.24, 1.0), (0.06, 0.06, 0.03), "bronze_dark", "Spine", verts=10)


def head_and_hood():
    # Mask: pale-steel skull face with a breather grille and two glowing lenses.
    prim("sphere", (0, -0.04, 1.7), (0.19, 0.22, 0.24), "steel", "Head", verts=16)
    prim("cube", (0, -0.15, 1.64), (0.1, 0.09, 0.12), "steel_dark", "Head", bevel=0.02)  # snout filter
    for i in range(3):
        prim("cube", (-0.025 + 0.025 * i, -0.2, 1.64), (0.008, 0.012, 0.09), "bronze_dark", "Head")
    prim("cyl", (0, -0.17, 1.58), (0.06, 0.06, 0.06), "bronze", "Head", rot=(90, 0, 0), verts=10)
    for x in (-0.05, 0.05):
        prim("sphere", (x, -0.15, 1.72), (0.06, 0.03, 0.055), "lens", "Head", verts=12)
        prim("sphere", (x, -0.14, 1.72), (0.08, 0.03, 0.075), "steel_dark", "Head", verts=10)
    # Deep hood with a sharp peak and a trailing cowl.
    prim("sphere", (0, 0.07, 1.76), (0.34, 0.34, 0.38), "robe", "Head", verts=24)
    limb((0, 0.05, 1.98), (0, -0.04, 1.86), 0.05, 0.12, "robe", "Head", verts=12)  # peak
    limb((0, 0.18, 1.82), (0, 0.27, 1.5), 0.14, 0.04, "robe", "Head", verts=14)
    torus((0, -0.13, 1.74), 0.125, 0.03, "robe", "Head", rot=(90, 0, 0))
    prim("cyl", (0, 0.0, 1.74), (0.24, 0.24, 0.03), "robe_dark", "Head", rot=(90, 0, 0), verts=18)
    for s in (-1, 1):  # side drapes + trim
        limb((0.15 * s, 0, 1.68), (0.22 * s, -0.03, 1.5), 0.1, 0.05, "robe", "Head", verts=12)
        limb((0.15 * s, -0.08, 1.74), (0.17 * s, -0.1, 1.6), 0.014, 0.014, "bronze_dark", "Head", verts=6)
    prim("sphere", (0, 0.02, 1.52), (0.62, 0.4, 0.15), "robe", "Spine", verts=20)  # cowl over the shoulders


def pauldrons():
    for s, side in ((1, "L"), (-1, "R")):
        x = 0.34 * s
        prim("sphere", (x, 0, 1.55), (0.3, 0.28, 0.2), "steel", f"UpperArm_{side}", verts=20)
        prim("sphere", (x + 0.03 * s, -0.02, 1.62), (0.2, 0.18, 0.1), "steel_dark", f"UpperArm_{side}", verts=12)
        torus((x, 0, 1.45), 0.14, 0.012, "bronze", f"UpperArm_{side}", squash_y=0.9)
        rivets([(x + 0.1 * s, -0.1, 1.58), (x - 0.04 * s, -0.12, 1.6), (x + 0.1 * s, 0.1, 1.58)], 0.02, "bronze", f"UpperArm_{side}")
    # Robe sleeves hang under the plates; a pale trim stripe down the left shoulder.
    prim("cube", (0.36, -0.03, 1.5), (0.04, 0.2, 0.05), "trim", "UpperArm_L")


# The rifle is modelled straight (barrel along -Y) and then tilted into a relaxed two-handed carry: right hand on the
# grip at the hip, left hand on the fore-end, muzzle angled down and slightly across the body. In game the pose script
# (RangerPose) lifts it into the aim pose on top of this rest pose.
RIFLE_GRIP = Vector((0, -0.3, 1.2))   # right hand, in the straight rifle frame
RIFLE_FORE = Vector((0, -0.6, 1.2))   # left hand, 0.3 ahead on the fore-end
CARRY_GRIP = Vector((-0.2, -0.22, 1.12))
CARRY = (Matrix.Translation(CARRY_GRIP) @ Matrix.Rotation(math.radians(34), 4, "Z") @ Matrix.Rotation(math.radians(24), 4, "X")
         @ Matrix.Translation(-RIFLE_GRIP))


def carry(p):
    return CARRY @ Vector(p)


def elbow(sh, wr, pole, l1=0.4, l2=0.4):
    """Elbow position for a two-bone arm reaching wr from sh, bending toward `pole`."""
    sh, wr, pole = Vector(sh), Vector(wr), Vector(pole)
    d = wr - sh
    dist = min(d.length, (l1 + l2) * 0.98)
    axis = d.normalized()
    along = (dist * dist + l1 * l1 - l2 * l2) / (2 * dist)
    off = math.sqrt(max(0.0, l1 * l1 - along * along))
    bend = pole - pole.dot(axis) * axis
    bend.normalize()
    return sh + axis * along + bend * off


_SH = {"L": Vector((0.3, 0, 1.45)), "R": Vector((-0.3, 0, 1.45))}
_WR = {"L": carry(RIFLE_FORE), "R": carry(RIFLE_GRIP)}
_POLE = {"L": Vector((1, 0.2, -0.8)), "R": Vector((-1, 0.2, -0.8))}
ARMS = {s: dict(sh=tuple(_SH[s]), el=tuple(elbow(_SH[s], _WR[s], _POLE[s])), wr=tuple(_WR[s])) for s in ("L", "R")}


def arms():
    for side, a in ARMS.items():
        limb(a["sh"], a["el"], 0.065, 0.055, "robe", f"UpperArm_{side}")
        prim("sphere", a["el"], (0.11, 0.11, 0.11), "robe_dark", f"UpperArm_{side}", verts=10)
        limb(a["el"], a["wr"], 0.055, 0.05, "robe", f"Forearm_{side}")
        lo = tuple(Vector(a["el"]) * 0.45 + Vector(a["wr"]) * 0.55)
        hi = tuple(Vector(a["el"]) * 0.05 + Vector(a["wr"]) * 0.95)
        limb(lo, hi, 0.062, 0.062, "steel", f"Forearm_{side}", verts=12)  # armoured bracer
        torus(tuple(Vector(a["el"]) * 0.3 + Vector(a["wr"]) * 0.7), 0.065, 0.009, "bronze", f"Forearm_{side}")
        wr = Vector(a["wr"])
        d = (wr - Vector(a["el"])).normalized()
        prim("sphere", tuple(wr + d * 0.04), (0.1, 0.1, 0.1), "glove", f"Hand_{side}", verts=12)
        limb(tuple(wr + d * 0.04), tuple(wr + d * 0.13), 0.04, 0.032, "glove", f"Hand_{side}", verts=8)


RIFLE_X, RIFLE_Z = 0.0, 1.27


def rifle():
    """Transuranic arquebus: stock, receiver with a bronze scope, a very long ribbed barrel and a muzzle brake."""
    bone = "Rifle_R"
    x, z = RIFLE_X, RIFLE_Z
    prim("cube", (x, 0.02, z - 0.02), (0.07, 0.34, 0.14), "bronze_dark", bone, bevel=0.02)  # stock
    prim("cube", (x, -0.3, z), (0.12, 0.34, 0.15), "steel", bone, bevel=0.025)  # receiver
    prim("cube", (x, -0.3, z + 0.1), (0.07, 0.2, 0.05), "bronze", bone, bevel=0.01)
    prim("cube", (x, -0.3, z - 0.1), (0.05, 0.1, 0.12), "leather", bone, bevel=0.01)  # grip
    # Scope.
    prim("cyl", (x, -0.3, z + 0.17), (0.07, 0.07, 0.34), "steel_dark", bone, rot=(90, 0, 0), verts=14)
    prim("sphere", (x, -0.48, z + 0.17), (0.06, 0.03, 0.06), "lens", bone, verts=10)
    torus((x, -0.2, z + 0.17), 0.04, 0.008, "bronze", bone, rot=(90, 0, 0))
    # Barrel with cooling ribs and a muzzle brake.
    limb((x, -0.45, z), (x, -1.58, z), 0.032, 0.026, "steel_dark", bone, verts=12)
    limb((x, -0.5, z - 0.05), (x, -1.2, z - 0.05), 0.026, 0.026, "bronze_dark", bone, verts=10)  # lower rail
    for i in range(7):
        torus((x, -0.65 - 0.14 * i, z), 0.04, 0.009, "bronze", bone, rot=(90, 0, 0))
    prim("cyl", (x, -1.6, z), (0.075, 0.075, 0.12), "steel", bone, rot=(90, 0, 0), verts=12)
    torus((x, -1.64, z), 0.045, 0.01, "bronze", bone, rot=(90, 0, 0))
    prim("sphere", (x, -1.66, z), (0.04, 0.03, 0.04), "lens_soft", bone, verts=8)
    # Power cell with a hose to the backpack.
    prim("cube", (x, -0.15, z - 0.14), (0.08, 0.18, 0.1), "bronze", bone, bevel=0.015)
    prim("cube", (x + 0.045, -0.15, z - 0.14), (0.01, 0.1, 0.05), "lens", bone)


BONES = [
    ("Root", None, (0, 0, 0), (0, 0, 0.1)),
    ("Hips", "Root", (0, 0, 0.98), (0, 0, 1.08)),
    ("Spine", "Hips", (0, 0, 1.08), (0, 0, 1.5)),
    ("Head", "Spine", (0, 0, 1.55), (0, 0, 1.95)),
]
for _side, _a in ARMS.items():
    _wr = Vector(_a["wr"])
    _dir = (_wr - Vector(_a["el"])).normalized()
    BONES += [
        (f"UpperArm_{_side}", "Spine", _a["sh"], _a["el"]),
        (f"Forearm_{_side}", f"UpperArm_{_side}", _a["el"], _a["wr"]),
        (f"Hand_{_side}", f"Forearm_{_side}", _a["wr"], tuple(_wr + _dir * 0.1)),
    ]
BONES.append(("Rifle_R", "Hand_R", tuple(carry(RIFLE_GRIP)), tuple(carry(RIFLE_GRIP + Vector((0, -0.4, 0))))))  # pivots at the right hand
for _s, _side in ((1, "L"), (-1, "R")):
    BONES += [
        (f"Thigh_{_side}", "Hips", (0.11 * _s, 0, 0.98), (0.11 * _s, 0, 0.52)),
        (f"Shin_{_side}", f"Thigh_{_side}", (0.11 * _s, 0, 0.52), (0.11 * _s, 0, 0.12)),
        (f"Foot_{_side}", f"Shin_{_side}", (0.11 * _s, 0, 0.12), (0.11 * _s, -0.2, 0.03)),
    ]

legs()
torso()
backpack()
head_and_hood()
pauldrons()
arms()
_n = len(c.parts)
rifle()
for _o in c.parts[_n:]:  # tilt the whole rifle (all three bones' meshes) into the carry pose
    _o.matrix_world = CARRY @ _o.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    _o.select_set(True)
    bpy.context.view_layer.objects.active = _o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

armature = c.build_armature(BONES)
mesh = c.assemble("SkitariiRanger", armature)
c.export_fbx(OUT_FBX, armature, mesh)
if RENDER_DIR:
    c.render_previews(RENDER_DIR, focus_z=1.1, distance=0.85)
