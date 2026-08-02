"""Decimates an FBX to a triangle budget and writes it beside the original.

Run headless:

    blender --background --python Tools/decimate_fbx.py -- <in.fbx> <out.fbx> <target_triangles>

Why a script rather than doing it by hand: there are twenty of these and every
one of them wants the same three decisions made the same way. Doing it by hand
is twenty chances to pick a different ratio.

Two things this is careful about.

**The ratio is computed, not typed.** Decimate takes a fraction, and the models
arrive at wildly different densities — nine hundred thousand triangles for an
icon, nine hundred thousand for a building. A fixed fraction would flatten the
icon and leave the building heavy. The target is a triangle count and the
fraction is whatever reaches it.

**UVs are protected.** These meshes carry a single baked atlas and nothing else;
lose the texture coordinates and the model is grey. Collapse mode with
`use_collapse_triangulate` keeps the unwrap intact where a planar decimation
would not.
"""

import sys
import bpy


def argv_after_double_dash():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1:]


def triangle_count(objects):
    total = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        total += len(mesh.loop_triangles)
    return total


def main():
    args = argv_after_double_dash()
    if len(args) < 3:
        print("[DECIMATE] usage: <in.fbx> <out.fbx> <target_triangles>")
        sys.exit(2)

    source, destination, target = args[0], args[1], int(args[2])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print(f"[DECIMATE] {source}: no mesh to decimate")
        sys.exit(3)

    before = triangle_count(meshes)
    if before <= target:
        print(f"[DECIMATE] {source}: {before} already under {target}, copying")
        bpy.ops.export_scene.fbx(
            filepath=destination,
            use_selection=False,
            path_mode="COPY",
            embed_textures=False,
        )
        print(f"[DECIMATE] RESULT {source} {before} -> {before}")
        return

    # Shared across every mesh in the file so one object is not flattened while
    # another keeps its detail. The file is one object as far as the game is
    # concerned.
    ratio = max(0.0005, float(target) / float(before))

    for obj in meshes:
        modifier = obj.modifiers.new(name="Decimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio

        # Keeps the unwrap. Without it the collapse is free to weld across a UV
        # seam, and a mesh carrying one baked atlas has seams everywhere.
        modifier.use_collapse_triangulate = True

        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier="Decimate")

    after = triangle_count(
        [o for o in bpy.context.scene.objects if o.type == "MESH"])

    bpy.ops.export_scene.fbx(
        filepath=destination,
        use_selection=False,
        path_mode="COPY",
        embed_textures=False,
    )
    print(f"[DECIMATE] RESULT {source} {before} -> {after}")


main()
