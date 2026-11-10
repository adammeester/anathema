"""Shared Blender helpers for the character scripts (run inside Blender, not plain Python).

Conventions: characters face -Y in Blender (imports into Unity facing +Z), "L" is +X and "R" is -X.
All coordinates passed to the helpers are in design units, multiplied by the global scale `S`
(set it with `configure`).
"""
import math
import os

import bpy
from mathutils import Euler, Matrix, Vector

S = 1.0
MATS = {}
parts = []


def configure(scale):
    global S
    S = scale
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    parts.clear()


def mat(name, color, metallic=0.0, rough=0.55, emit=None, emit_strength=4.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit_strength
    m.diffuse_color = (*color, 1.0)
    MATS[name] = m


def v(x, y, z):
    return Vector((x, y, z)) * S


def finish(obj, material, bone, bevel=0.0, xf=None):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if xf is not None:  # scale is already baked, so the pivot rotation can not shear the mesh
        obj.matrix_world = xf @ obj.matrix_world
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        m = obj.modifiers.new("bevel", "BEVEL")
        m.width = bevel * S
        m.segments = 3
        m.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
    obj.data.materials.append(MATS[material])
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    parts.append(obj)
    return obj


def prim(kind, loc, size, material, bone, rot=(0, 0, 0), bevel=0.0, xf=None, verts=20):
    """kind: cube | sphere | cyl. `size` is the full extent on each axis (cylinders run along Z)."""
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=verts, ring_count=max(8, verts // 2))
    else:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.5, depth=1, vertices=verts)
    o = bpy.context.active_object
    o.location = v(*loc)
    o.scale = Vector(size) * S
    o.rotation_euler = Euler([math.radians(a) for a in rot])
    return finish(o, material, bone, bevel, xf)


def limb(a, b, r1, r2, material, bone, xf=None, verts=16):
    """Tapered cylinder from point a (radius r1) to point b (radius r2)."""
    a, b = v(*a), v(*b)
    d = b - a
    bpy.ops.mesh.primitive_cone_add(radius1=r1 * S, radius2=r2 * S, depth=d.length, vertices=verts)
    o = bpy.context.active_object
    o.location = (a + b) / 2
    up = "Y" if abs(d.y) < 0.9 * d.length else "X"
    o.rotation_euler = d.to_track_quat("Z", up).to_euler()
    return finish(o, material, bone, 0.0, xf)


def flat_poly(pts, y, thick, material, bone, xf=None):
    """Extrudes a polygon drawn in the XZ plane (list of (x, z) design units, any winding) to `thick` along Y at `y`."""
    n = len(pts)
    verts = [v(x, y - thick / 2, z) for x, z in pts] + [v(x, y + thick / 2, z) for x, z in pts]
    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]
    faces += [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    me = bpy.data.meshes.new("poly")
    me.from_pydata([tuple(p) for p in verts], [], faces)
    me.update()
    me.validate()
    o = bpy.data.objects.new("poly", me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return finish(o, material, bone, 0.0, xf)


def torus(loc, major, minor, material, bone, squash_y=1.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major * S, minor_radius=minor * S, major_segments=24, minor_segments=8)
    o = bpy.context.active_object
    o.location = v(*loc)
    o.scale = (1.0, squash_y, 1.0)
    o.rotation_euler = Euler([math.radians(a) for a in rot])
    return finish(o, material, bone)


def pivot_rotation(pivot, rot_deg):
    p = v(*pivot)
    r = Euler([math.radians(a) for a in rot_deg]).to_matrix().to_4x4()
    return Matrix.Translation(p) @ r @ Matrix.Translation(-p)


def build_armature(bones):
    """bones: list of (name, parent_name_or_None, head, tail) in design units."""
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = bpy.context.active_object
    arm.name = "Armature"
    arm.data.name = "Armature"
    eb = arm.data.edit_bones
    for b in list(eb):
        eb.remove(b)
    for name, parent, head, tail in bones:
        b = eb.new(name)
        b.head, b.tail = v(*head), v(*tail)
        if parent:
            b.parent = eb[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def assemble(mesh_name, arm):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.active_object
    body.name = body.data.name = mesh_name
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")

    body.parent = arm
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return body


def export_fbx(path, arm, body):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="OFF",
        path_mode="AUTO",
    )
    print("Exported", path)


def render_previews(out_dir, focus_z=1.1, distance=1.0):
    """Front, back and in-game-camera (55 deg pitch) previews. `distance` scales the camera distances."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene

    bpy.ops.mesh.primitive_circle_add(radius=4, fill_type="NGON", location=(0, 0, -0.001))
    floor = bpy.context.active_object
    floor_mat = bpy.data.materials.new("floor")
    floor_mat.use_nodes = True
    floor_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.26, 0.3, 1)
    floor.data.materials.append(floor_mat)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.52, 0.58, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world

    for name, energy, rot in (("key", 4.0, (50, 0, 35)), ("fill", 1.5, (60, 0, -120))):
        light = bpy.data.lights.new(name, "SUN")
        light.energy = energy
        lo = bpy.data.objects.new(name, light)
        lo.rotation_euler = Euler([math.radians(a) for a in rot])
        scene.collection.objects.link(lo)

    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True

    def shot(name, pos, target, lens, res):
        cam.location = Vector(pos)
        cam_data.lens = lens
        cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.filepath = os.path.join(out_dir, name + ".png")
        bpy.ops.render.render(write_still=True)

    k = distance
    target = (0, 0, focus_z)
    shot("front", (3.0 * k, -5.0 * k, focus_z + 0.8), target, 50, (640, 800))
    shot("back", (-3.0 * k, 5.0 * k, focus_z + 0.8), target, 50, (640, 800))
    d = 14.0 * k  # game camera: pitch 55 deg, looking along the character's facing direction
    shot("game", (0, d * math.cos(math.radians(55)), focus_z + d * math.sin(math.radians(55))), target, 60, (800, 600))


def clipped_ellipsoid(loc, size, zmin, zmax, material, bone, rot=(0, 0, 0), bevel=0.0, half=0):
    """An ellipsoid (full extent `size`) sliced flat at local heights zmin and zmax (None = uncut), capped and closed.
    half=+1 / -1 also keeps only the local +Y (rear) / -Y (front) half, cut flat down the middle."""
    import bmesh
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=32, ring_count=16)
    o = bpy.context.active_object
    o.scale = Vector(size) * S
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    cuts = [((0, 0, zmin * S), (0, 0, 1), True) if zmin is not None else None,
            ((0, 0, zmax * S), (0, 0, 1), False) if zmax is not None else None,
            ((0, 0, 0), (0, 1, 0), half > 0) if half else None]  # half<0 keeps front (-Y); half>0 keeps rear (+Y)
    for cut_def in cuts:
        if cut_def is None:
            continue
        co, no, clear_inner = cut_def
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        res = bmesh.ops.bisect_plane(bm, geom=geom, dist=0.0001, plane_co=co, plane_no=no,
                                     clear_inner=clear_inner, clear_outer=not clear_inner)
        cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        bmesh.ops.triangle_fill(bm, use_beauty=True, edges=cut)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(o.data)
    bm.free()
    o.location = v(*loc)
    o.rotation_euler = Euler([math.radians(a) for a in rot])
    return finish(o, material, bone, bevel)


def pauldron_block(x, z, s, w, d, h, bone, mat="armor", trim="gold", tilt=10, crest=True):
    """Space Marine pauldron: a bulging dome (curved on every side, like the reference) whose bottom is cut off flat so
    it is squared underneath, with a trim band round the lower edge. s is +1 (left) / -1 (right); (x, z) is the centre
    of the plate; it tilts down toward the outside. w is across the shoulder, d front-to-back, h is the total height."""
    rot = (0, tilt * s, 0)
    zc = z - 0.1 * h                    # ellipsoid centre, so the plate spans z - h/2 .. z + h/2
    cut = -0.4 * h                      # the flat bottom, relative to the ellipsoid centre
    clipped_ellipsoid((x, 0, zc), (w, d, 1.2 * h), cut, None, mat, bone, rot=rot, bevel=0.0)
    clipped_ellipsoid((x, 0, zc), (w * 1.04, d * 1.04, 1.2 * h * 1.04), cut * 1.04 - 0.005, cut + 0.045, trim, bone, rot=rot)
