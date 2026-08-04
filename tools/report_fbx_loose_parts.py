"""Reports how many separable pieces each FBX's meshes fall into.

Read-only as far as the repository is concerned: the split happens in Blender's
own scene and nothing is exported. The question this answers has to be settled
before any pipeline is built on it - whether the model *can* be split at all.

The interiors that arrive now are a single mesh object each, which is why a room
can no longer hide one wall at a time: `InteriorShellScreen` assigns renderers to
faces by position, and one renderer can only ever belong to one face. Splitting
helps only if the mesh is several disconnected shells welded into one object
rather than a single continuous surface, and Blender's "separate by loose parts"
answers exactly that.

Run:
    blender --background --python Tools/report_fbx_loose_parts.py -- <fbx>...
"""

import sys

import bpy


def argv_after_double_dash():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1:]


def clear_scene():
    for object in list(bpy.data.objects):
        bpy.data.objects.remove(object, do_unlink=True)


def split_into_loose_parts(mesh_objects):
    """Separates every mesh by loose parts and returns the resulting count."""
    bpy.ops.object.select_all(action="DESELECT")
    for mesh_object in mesh_objects:
        mesh_object.select_set(True)

    # An active object is required for the mode switch, and separate is an
    # edit-mode operator rather than an object-mode one.
    bpy.context.view_layer.objects.active = mesh_objects[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    return len([
        object for object in bpy.context.scene.objects
        if object.type == "MESH"
    ])


def main():
    paths = argv_after_double_dash()
    if not paths:
        print("[PARTS] no fbx paths given")
        return

    for path in paths:
        clear_scene()
        try:
            bpy.ops.import_scene.fbx(filepath=path)
        except Exception as error:  # noqa: BLE001 - reported, not handled
            print(f"[PARTS] {path}: import failed: {error}")
            continue

        meshes = [
            object for object in bpy.context.scene.objects
            if object.type == "MESH"
        ]
        name = path.replace("\\", "/").rsplit("/", 1)[-1]
        if not meshes:
            print(f"[PARTS] {name}: no meshes")
            continue

        faces = sum(len(object.data.polygons) for object in meshes)
        pieces = split_into_loose_parts(meshes)
        print(
            f"[PARTS] {name}: {len(meshes)} object(s), {faces} face(s) "
            f"-> {pieces} loose piece(s)")


main()
