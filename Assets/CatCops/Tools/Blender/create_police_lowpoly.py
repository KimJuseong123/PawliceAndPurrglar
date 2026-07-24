import argparse
import math
import os

import bpy
from mathutils import Vector


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", required=True)
    parser.add_argument("--blend", required=True)
    parser.add_argument("--preview", required=True)
    if "--" in os.sys.argv:
        return parser.parse_args(os.sys.argv[os.sys.argv.index("--") + 1 :])
    return parser.parse_args()


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def material(name, color, roughness=0.78, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return mat


def apply_material(obj, mat):
    obj.data.materials.append(mat)
    return obj


def apply_scale(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)


def bevelled_cube(name, location, scale, mat, bevel=0.04, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_scale(obj)
    if bevel > 0.0:
        modifier = obj.modifiers.new("LowPolyBevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return apply_material(obj, mat)


def ico(name, location, scale, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_scale(obj)
    return apply_material(obj, mat)


def cylinder(name, location, radius, depth, mat, vertices=12, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    return apply_material(obj, mat)


def tapered_prism(name, location, top_scale, bottom_scale, depth, mat):
    top_x, top_z = top_scale
    bottom_x, bottom_z = bottom_scale
    half_depth = depth * 0.5
    vertices = [
        (-bottom_x, -half_depth, -bottom_z),
        (bottom_x, -half_depth, -bottom_z),
        (bottom_x, half_depth, -bottom_z),
        (-bottom_x, half_depth, -bottom_z),
        (-top_x, -half_depth, top_z),
        (top_x, -half_depth, top_z),
        (top_x, half_depth, top_z),
        (-top_x, half_depth, top_z),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (4, 0, 3, 7),
    ]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return apply_material(obj, mat)


def star(name, location, outer_radius, inner_radius, depth, mat, points=5, rotation=0.0):
    outline = []
    for index in range(points * 2):
        radius = outer_radius if index % 2 == 0 else inner_radius
        angle = rotation + math.pi * 0.5 + index * math.pi / points
        outline.append((math.cos(angle) * radius, math.sin(angle) * radius))

    front_y = -depth * 0.5
    back_y = depth * 0.5
    vertices = [(0.0, front_y, 0.0), (0.0, back_y, 0.0)]
    vertices += [(x, front_y, z) for x, z in outline]
    vertices += [(x, back_y, z) for x, z in outline]

    faces = []
    count = len(outline)
    front_start = 2
    back_start = 2 + count
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((0, front_start + index, front_start + next_index))
        faces.append((1, back_start + next_index, back_start + index))
        faces.append(
            (
                front_start + index,
                back_start + index,
                back_start + next_index,
                front_start + next_index,
            )
        )

    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return apply_material(obj, mat)


def triangle(name, location, width, height, depth, mat, flip=False):
    direction = -1.0 if flip else 1.0
    vertices = [
        (-width * 0.5, -depth * 0.5, height * 0.5),
        (width * 0.5, -depth * 0.5, height * 0.5),
        (direction * width * 0.16, -depth * 0.5, -height * 0.5),
        (-width * 0.5, depth * 0.5, height * 0.5),
        (width * 0.5, depth * 0.5, height * 0.5),
        (direction * width * 0.16, depth * 0.5, -height * 0.5),
    ]
    faces = [
        (0, 2, 1),
        (3, 4, 5),
        (0, 1, 4, 3),
        (1, 2, 5, 4),
        (2, 0, 3, 5),
    ]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return apply_material(obj, mat)


def create_palette():
    return {
        "skin": material("Skin_Warm", (0.93, 0.55, 0.32, 1.0)),
        "skin_light": material("Skin_Light", (1.0, 0.68, 0.43, 1.0)),
        "nose": material("Nose_Rose", (0.95, 0.36, 0.24, 1.0)),
        "hair": material("Hair_Brown", (0.16, 0.075, 0.035, 1.0)),
        "blue": material("Police_Blue", (0.045, 0.18, 0.52, 1.0)),
        "blue_light": material("Police_Blue_Light", (0.08, 0.30, 0.70, 1.0)),
        "blue_dark": material("Police_Blue_Dark", (0.018, 0.06, 0.15, 1.0)),
        "shirt": material("Shirt_Blue", (0.09, 0.26, 0.58, 1.0)),
        "black": material("Near_Black", (0.012, 0.016, 0.023, 1.0)),
        "white": material("Eye_White", (0.97, 0.97, 0.92, 1.0)),
        "gold": material("Badge_Gold", (1.0, 0.55, 0.055, 1.0), roughness=0.48, metallic=0.08),
        "gold_light": material("Badge_Highlight", (1.0, 0.78, 0.18, 1.0), roughness=0.42),
        "mouth": material("Mouth", (0.09, 0.025, 0.02, 1.0)),
        "drool": material("Drool", (0.25, 0.74, 0.95, 1.0), roughness=0.28),
    }


def create_police(mats):
    parts = []

    # Short legs and oversized shoes establish the compact chibi silhouette.
    parts += [
        bevelled_cube("Shoe_L", (-0.245, -0.08, 0.13), (0.22, 0.31, 0.13), mats["black"], 0.055),
        bevelled_cube("Shoe_R", (0.245, -0.08, 0.13), (0.22, 0.31, 0.13), mats["black"], 0.055),
        tapered_prism("Trouser_L", (-0.23, 0.02, 0.43), (0.17, 0.21), (0.20, 0.22), 0.42, mats["blue"]),
        tapered_prism("Trouser_R", (0.23, 0.02, 0.43), (0.17, 0.21), (0.20, 0.22), 0.42, mats["blue"]),
    ]

    parts += [
        bevelled_cube("Torso", (0.0, 0.02, 0.88), (0.53, 0.34, 0.45), mats["shirt"], 0.11),
        bevelled_cube("Belt", (0.0, -0.005, 0.60), (0.56, 0.36, 0.075), mats["blue_dark"], 0.025),
        bevelled_cube("Belt_Buckle", (0.0, -0.385, 0.60), (0.13, 0.035, 0.105), mats["gold"], 0.018),
        bevelled_cube("Buckle_Inset", (0.0, -0.424, 0.60), (0.068, 0.012, 0.052), mats["black"], 0.008),
        triangle("Collar_L", (-0.19, -0.355, 1.13), 0.28, 0.24, 0.035, mats["blue_light"], flip=True),
        triangle("Collar_R", (0.19, -0.355, 1.13), 0.28, 0.24, 0.035, mats["blue_light"], flip=False),
        triangle("Tie_Knot", (0.0, -0.405, 1.08), 0.18, 0.17, 0.045, mats["blue_dark"]),
        tapered_prism("Tie", (0.0, -0.405, 0.88), (0.07, 0.16), (0.115, 0.18), 0.055, mats["blue_dark"]),
        bevelled_cube("Pocket_L", (-0.31, -0.355, 0.91), (0.14, 0.025, 0.12), mats["blue"], 0.018),
        bevelled_cube("Pocket_R", (0.31, -0.355, 0.91), (0.14, 0.025, 0.12), mats["blue"], 0.018),
        star("Chest_Badge", (0.31, -0.405, 1.04), 0.14, 0.065, 0.04, mats["gold"], rotation=math.radians(18)),
    ]

    # Arms hang slightly away from the torso so the silhouette survives a top-down camera.
    parts += [
        bevelled_cube("Sleeve_L", (-0.62, 0.01, 0.91), (0.18, 0.26, 0.30), mats["blue"], 0.10, rotation=(0.0, 0.0, math.radians(-7))),
        bevelled_cube("Sleeve_R", (0.62, 0.01, 0.91), (0.18, 0.26, 0.30), mats["blue"], 0.10, rotation=(0.0, 0.0, math.radians(7))),
        ico("Hand_L", (-0.65, -0.04, 0.55), (0.18, 0.16, 0.18), mats["skin_light"], 1),
        ico("Hand_R", (0.65, -0.04, 0.55), (0.18, 0.16, 0.18), mats["skin_light"], 1),
    ]

    parts += [
        ico("Head", (0.0, 0.0, 1.58), (0.58, 0.49, 0.52), mats["skin_light"], 2),
        ico("Ear_L", (-0.565, 0.015, 1.58), (0.16, 0.11, 0.19), mats["skin"], 1),
        ico("Ear_R", (0.565, 0.015, 1.58), (0.16, 0.11, 0.19), mats["skin"], 1),
        bevelled_cube("Hair_Back", (0.0, 0.39, 1.69), (0.43, 0.105, 0.30), mats["hair"], 0.075),
        bevelled_cube("Hair_Side_L", (-0.49, 0.02, 1.75), (0.08, 0.29, 0.23), mats["hair"], 0.055),
        bevelled_cube("Hair_Side_R", (0.49, 0.02, 1.75), (0.08, 0.29, 0.23), mats["hair"], 0.055),
    ]

    for side, label in ((-1, "L"), (1, "R")):
        eye_x = side * 0.225
        parts.append(ico(f"Eye_{label}", (eye_x, -0.438, 1.67), (0.245, 0.145, 0.265), mats["white"], 2))
        parts.append(ico(f"Pupil_{label}", (eye_x + side * 0.018, -0.568, 1.66), (0.075, 0.035, 0.088), mats["black"], 1))
        parts.append(
            bevelled_cube(
                f"Eyebrow_{label}",
                (eye_x, -0.505, 1.93),
                (0.17, 0.025, 0.035),
                mats["hair"],
                0.018,
                rotation=(0.0, side * math.radians(5), side * math.radians(4)),
            )
        )

    parts += [
        ico("Nose", (0.0, -0.535, 1.49), (0.12, 0.085, 0.10), mats["nose"], 1),
        bevelled_cube("Mouth", (0.0, -0.508, 1.35), (0.16, 0.022, 0.025), mats["mouth"], 0.015),
        ico("Drool", (0.17, -0.525, 1.29), (0.035, 0.026, 0.075), mats["drool"], 1),
    ]

    # A broad cap and two gold emblems make the role readable even at small scale.
    parts += [
        ico("Cap_Crown", (0.0, 0.015, 2.04), (0.64, 0.51, 0.31), mats["blue"], 2),
        bevelled_cube("Cap_Band", (0.0, -0.34, 1.91), (0.55, 0.095, 0.085), mats["blue_dark"], 0.035),
        bevelled_cube("Cap_Brim", (0.0, -0.49, 1.87), (0.64, 0.19, 0.07), mats["blue_dark"], 0.045),
        star("Cap_Badge", (0.0, -0.492, 2.10), 0.17, 0.082, 0.045, mats["gold"], rotation=math.radians(18)),
        star("Cap_Badge_Inner", (0.0, -0.52, 2.10), 0.09, 0.043, 0.018, mats["gold_light"], rotation=math.radians(18)),
    ]

    root = bpy.data.objects.new("Police_LowPoly", None)
    bpy.context.collection.objects.link(root)
    for part in parts:
        part.parent = root

    return root, parts


def create_studio(mats):
    studio_mat = material("Studio_Floor", (0.22, 0.25, 0.29, 1.0), roughness=0.9)
    backdrop_mat = material("Studio_Backdrop", (0.055, 0.075, 0.105, 1.0), roughness=1.0)

    floor = cylinder("Preview_Plint", (0.0, 0.0, -0.09), 1.32, 0.16, studio_mat, vertices=32)
    floor.hide_render = False

    bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0.0, 0.0, -0.18))
    ground = bpy.context.object
    ground.name = "Preview_Ground"
    apply_material(ground, backdrop_mat)

    world = bpy.context.scene.world
    world.color = (0.025, 0.035, 0.055)
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.04, 0.07, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.38


def add_area_light(name, location, energy, color, size):
    data = bpy.data.lights.new(name, type="AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    point_at(obj, Vector((0.0, 0.0, 1.05)))
    return obj


def point_at(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_preview(preview_path):
    create_studio({})
    add_area_light("Key_Light", (-3.2, -4.0, 5.0), 950.0, (1.0, 0.78, 0.60), 3.2)
    add_area_light("Fill_Light", (3.5, -2.0, 2.6), 700.0, (0.45, 0.68, 1.0), 2.8)
    add_area_light("Rim_Light", (1.8, 3.0, 4.1), 850.0, (0.30, 0.55, 1.0), 2.4)

    camera_data = bpy.data.cameras.new("Preview_Camera")
    camera = bpy.data.objects.new("Preview_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (3.7, -6.0, 3.05)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 3.35
    camera_data.lens = 52.0
    point_at(camera, Vector((0.0, 0.0, 1.12)))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = preview_path
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_percentage = 100
    scene.render.use_file_extension = True

    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)


def export_fbx(root, parts, fbx_path):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = root

    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL",
        use_mesh_modifiers=True,
        path_mode="AUTO",
    )


def main():
    args = parse_args()
    for path in (args.fbx, args.blend, args.preview):
        os.makedirs(os.path.dirname(path), exist_ok=True)

    clear_scene()
    mats = create_palette()
    root, parts = create_police(mats)

    export_fbx(root, parts, args.fbx)
    setup_preview(args.preview)
    bpy.ops.wm.save_as_mainfile(filepath=args.blend)


if __name__ == "__main__":
    main()
