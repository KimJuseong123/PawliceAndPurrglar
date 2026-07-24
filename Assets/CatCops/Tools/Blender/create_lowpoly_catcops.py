import argparse
import math
import os

import bpy


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--blend", required=True)
    if "--" in os.sys.argv:
        return parser.parse_args(os.sys.argv[os.sys.argv.index("--") + 1 :])
    return parser.parse_args()


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_mat(name, color):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    return material


def assign(obj, material):
    obj.data.materials.append(material)
    return obj


def cube(name, loc, scale, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return assign(obj, material)


def sphere(name, loc, scale, material, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return assign(obj, material)


def cylinder(name, loc, radius, depth, material, vertices=8):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, material)


def cone(name, loc, radius1, radius2, depth, material, vertices=8):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, material)


def parent_all(name, objects):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for obj in objects:
        obj.parent = root
    return root, objects


def eye_pair(prefix, z, y, x_offset, mats, scale=(0.13, 0.08, 0.13)):
    objects = []
    for side in (-1, 1):
        objects.append(sphere(f"{prefix}_Eye_{side}", (side * x_offset, y, z), scale, mats["white"], 1))
        objects.append(sphere(f"{prefix}_Pupil_{side}", (side * x_offset, y - 0.055, z + 0.005), (0.045, 0.025, 0.045), mats["black"], 1))
    return objects


def make_police(mats):
    objects = [
        sphere("Police_Head", (0, -0.02, 1.42), (0.38, 0.34, 0.34), mats["skin"], 2),
        cube("Police_Body", (0, 0, 0.86), (0.48, 0.32, 0.42), mats["police_blue"]),
        cube("Police_Belt", (0, -0.01, 0.72), (0.54, 0.34, 0.06), mats["black"]),
        cylinder("Police_Cap", (0, -0.02, 1.75), 0.36, 0.18, mats["police_blue"], 10),
        cube("Police_Cap_Brim", (0, -0.31, 1.66), (0.42, 0.12, 0.05), mats["black"]),
        sphere("Police_Nose", (0, -0.36, 1.38), (0.08, 0.06, 0.07), mats["nose"], 1),
        sphere("Police_Badge", (0.18, -0.33, 0.98), (0.08, 0.03, 0.08), mats["gold"], 1),
        cube("Police_Left_Leg", (-0.15, 0, 0.33), (0.14, 0.14, 0.35), mats["police_blue"]),
        cube("Police_Right_Leg", (0.15, 0, 0.33), (0.14, 0.14, 0.35), mats["police_blue"]),
        sphere("Police_Left_Hand", (-0.34, -0.04, 0.82), (0.11, 0.1, 0.1), mats["skin"], 1),
        sphere("Police_Right_Hand", (0.34, -0.04, 0.82), (0.11, 0.1, 0.1), mats["skin"], 1),
    ]
    objects += eye_pair("Police", 1.48, -0.33, 0.16, mats)
    return parent_all("Police_LowPoly", objects)


def make_thief(mats):
    objects = [
        sphere("Thief_Head", (0, -0.02, 1.36), (0.37, 0.33, 0.33), mats["skin"], 2),
        cube("Thief_Body", (0, 0, 0.82), (0.48, 0.32, 0.42), mats["thief_dark"]),
        cylinder("Thief_Beanie", (0, -0.02, 1.63), 0.35, 0.26, mats["beanie"], 8),
        cube("Thief_Mask", (0, -0.34, 1.39), (0.35, 0.04, 0.13), mats["black"]),
        sphere("Thief_Nose", (0, -0.38, 1.31), (0.08, 0.05, 0.07), mats["nose"], 1),
        sphere("Thief_Bag", (0.45, 0.12, 0.91), (0.25, 0.22, 0.33), mats["sack"], 1),
        sphere("Thief_Gem", (0.54, -0.09, 0.99), (0.08, 0.04, 0.08), mats["jewel"], 1),
        cube("Thief_Left_Leg", (-0.15, 0, 0.31), (0.14, 0.14, 0.34), mats["black"]),
        cube("Thief_Right_Leg", (0.15, 0, 0.31), (0.14, 0.14, 0.34), mats["black"]),
    ]
    objects += eye_pair("Thief", 1.43, -0.38, 0.15, mats)
    return parent_all("Thief_LowPoly", objects)


def make_dog(mats):
    objects = [
        sphere("Dog_Body", (0, 0, 0.54), (0.48, 0.28, 0.28), mats["dog"], 2),
        sphere("Dog_Head", (0, -0.42, 0.79), (0.34, 0.28, 0.29), mats["dog"], 2),
        cone("Dog_Left_Ear", (-0.2, -0.42, 1.1), 0.12, 0.02, 0.36, mats["dog_dark"], 4),
        cone("Dog_Right_Ear", (0.2, -0.42, 1.1), 0.12, 0.02, 0.36, mats["dog_dark"], 4),
        sphere("Dog_Nose", (0, -0.71, 0.78), (0.08, 0.05, 0.06), mats["black"], 1),
        cube("Dog_Collar", (0, -0.44, 0.56), (0.36, 0.08, 0.06), mats["police_blue"]),
        sphere("Dog_Badge", (0, -0.52, 0.49), (0.07, 0.03, 0.07), mats["gold"], 1),
        cone("Dog_Tail", (0, 0.33, 0.69), 0.04, 0.12, 0.44, mats["dog_dark"], 6),
    ]
    objects += eye_pair("Dog", 0.88, -0.68, 0.12, mats, (0.11, 0.065, 0.11))
    return parent_all("Dog_LowPoly", objects)


def make_cat(mats):
    objects = [
        sphere("Cat_Body", (0, 0, 0.48), (0.4, 0.25, 0.25), mats["cat"], 2),
        sphere("Cat_Head", (0, -0.36, 0.76), (0.31, 0.25, 0.26), mats["cat"], 2),
        cone("Cat_Left_Ear", (-0.19, -0.38, 1.04), 0.11, 0.02, 0.3, mats["cat_dark"], 4),
        cone("Cat_Right_Ear", (0.19, -0.38, 1.04), 0.11, 0.02, 0.3, mats["cat_dark"], 4),
        sphere("Cat_Nose", (0, -0.62, 0.75), (0.055, 0.035, 0.045), mats["pink"], 1),
        sphere("Cat_Backpack", (0.3, 0.07, 0.58), (0.16, 0.12, 0.2), mats["sack"], 1),
        sphere("Cat_Gem", (0.0, -0.6, 0.52), (0.07, 0.035, 0.07), mats["jewel"], 1),
        cone("Cat_Tail", (0, 0.26, 0.62), 0.04, 0.1, 0.42, mats["cat_dark"], 6),
    ]
    objects += eye_pair("Cat", 0.84, -0.6, 0.11, mats, (0.1, 0.06, 0.1))
    return parent_all("Cat_LowPoly", objects)


def make_merchant(mats):
    objects = [
        sphere("Merchant_Head", (0, -0.02, 1.05), (0.34, 0.3, 0.3), mats["merchant"], 2),
        cube("Merchant_Body", (0, 0, 0.56), (0.46, 0.3, 0.32), mats["merchant_coat"]),
        cylinder("Merchant_Hat", (0, -0.02, 1.34), 0.3, 0.12, mats["beanie"], 8),
        sphere("Merchant_Bag", (-0.34, -0.08, 0.7), (0.18, 0.12, 0.2), mats["sack"], 1),
        sphere("Merchant_Coin", (0.28, -0.23, 0.76), (0.08, 0.035, 0.08), mats["gold"], 1),
    ]
    objects += eye_pair("Merchant", 1.1, -0.3, 0.12, mats, (0.1, 0.06, 0.1))
    return parent_all("Merchant_LowPoly", objects)


def export_model(root, objects, out_dir, file_name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, file_name),
        use_selection=True,
        add_leaf_bones=False,
        bake_space_transform=True,
        object_types={"EMPTY", "MESH"},
    )


def main():
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)
    os.makedirs(os.path.dirname(args.blend), exist_ok=True)
    clear_scene()

    mats = {
        "skin": make_mat("Skin", (1.0, 0.66, 0.43, 1)),
        "nose": make_mat("Nose", (1.0, 0.46, 0.34, 1)),
        "white": make_mat("EyeWhite", (0.96, 0.96, 0.92, 1)),
        "black": make_mat("Black", (0.02, 0.02, 0.025, 1)),
        "police_blue": make_mat("PoliceBlue", (0.04, 0.2, 0.58, 1)),
        "gold": make_mat("Gold", (1.0, 0.68, 0.14, 1)),
        "thief_dark": make_mat("ThiefDark", (0.05, 0.05, 0.06, 1)),
        "beanie": make_mat("Beanie", (0.09, 0.085, 0.09, 1)),
        "sack": make_mat("Sack", (0.45, 0.31, 0.16, 1)),
        "jewel": make_mat("Jewel", (0.56, 0.25, 1.0, 1)),
        "dog": make_mat("DogTan", (0.92, 0.52, 0.14, 1)),
        "dog_dark": make_mat("DogDark", (0.45, 0.22, 0.08, 1)),
        "cat": make_mat("CatPurple", (0.31, 0.25, 0.37, 1)),
        "cat_dark": make_mat("CatDark", (0.16, 0.15, 0.19, 1)),
        "pink": make_mat("Pink", (1.0, 0.45, 0.58, 1)),
        "merchant": make_mat("MerchantFur", (0.28, 0.27, 0.24, 1)),
        "merchant_coat": make_mat("MerchantCoat", (0.2, 0.24, 0.27, 1)),
    }

    makers = [
        (make_police, "Police_LowPoly.fbx"),
        (make_thief, "Thief_LowPoly.fbx"),
        (make_dog, "Dog_LowPoly.fbx"),
        (make_cat, "Cat_LowPoly.fbx"),
        (make_merchant, "Merchant_LowPoly.fbx"),
    ]

    for maker, file_name in makers:
        root, objects = maker(mats)
        export_model(root, objects, args.out, file_name)
        for obj in objects:
            obj.location.x += 2.4
        root.location.x += 2.4

    bpy.ops.wm.save_as_mainfile(filepath=args.blend)


if __name__ == "__main__":
    main()
