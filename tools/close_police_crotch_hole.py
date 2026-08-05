"""Closes the openings left in the police model's hips and legs.

Run headless:

    blender --background --python Tools/close_police_crotch_hole.py -- <in.fbx> <out.fbx>

An earlier pass (`repair_police_lower_body_hole.py`) closed one opening on the
left hip and left five more behind, so the model still showed daylight between
the legs — and more of it when the character jumped, because jumping spreads the
legs and widens the gap.

Two things this is careful about.

**Only the lower body.** The mesh has open boundaries higher up too, where the
sleeves meet the jacket and where the collar meets the neck. Those are inside
other geometry and nobody sees them; filling them risks visible triangles across
a shoulder for no gain. Everything below the waist is fair game and nothing above
it is touched.

**Faces from the vertices that are already there.** New geometry gets its skin
weights from the vertices it is built on, so patching a hole with the loop that
surrounds it leaves the rig alone. Creating fresh vertices would put unweighted
geometry on a skinned character, and it would stay behind when the legs moved.
"""

import json
import sys
from pathlib import Path

import bmesh
import bpy


def argv_after_double_dash():
    if "--" not in sys.argv:
        raise SystemExit("Pass -- <in.fbx> <out.fbx>")
    return sys.argv[sys.argv.index("--") + 1:]


def only_mesh():
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise SystemExit(f"Expected one mesh, found {len(meshes)}.")
    return meshes[0]


def boundary_loops(bm):
    """Open edges, grouped into the rings they form."""
    seen = set()
    loops = []
    for edge in bm.edges:
        if len(edge.link_faces) != 1 or edge.index in seen:
            continue

        stack = [edge]
        group = []
        while stack:
            current = stack.pop()
            if current.index in seen:
                continue

            seen.add(current.index)
            group.append(current)
            for vertex in current.verts:
                for neighbour in vertex.link_edges:
                    if (len(neighbour.link_faces) == 1
                            and neighbour.index not in seen):
                        stack.append(neighbour)

        loops.append(group)

    return loops


def close_chain(bm, loop):
    """Joins the loose ends of an open run of boundary edges.

    A boundary that is a ring can be filled; one that is a chain cannot,
    because there is no enclosed area. Chains happen where the surrounding
    topology is broken — this model has one non-manifold edge and the chain
    starts there.

    Returns the loop unchanged when it is already a ring, or when it has more
    than two loose ends: a branching boundary is a different problem and
    guessing at it would weld together parts that are not meant to touch.
    """
    inside = set(loop)
    ends = [
        vertex for vertex in {v for e in loop for v in e.verts}
        if sum(1 for e in vertex.link_edges if e in inside) == 1
    ]
    if len(ends) != 2:
        return loop

    # Deduped, because the fill refuses a list holding the same edge twice and
    # the bridge may already be there.
    bridge = bm.edges.get(ends) or bm.edges.new(ends)
    closed = list(loop)
    if bridge not in closed:
        closed.append(bridge)

    return closed


def describe(bm):
    loops = boundary_loops(bm)
    return {
        "vertices": len(bm.verts),
        "faces": len(bm.faces),
        "boundaryEdges": sum(len(loop) for loop in loops),
        "boundaryLoops": len(loops),
    }


def main():
    arguments = argv_after_double_dash()
    source = Path(arguments[0])
    target = Path(arguments[1])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))

    mesh_object = only_mesh()
    bm = bmesh.new()
    bm.from_mesh(mesh_object.data)
    bm.verts.ensure_lookup_table()

    heights = [vertex.co.z for vertex in bm.verts]
    floor, ceiling = min(heights), max(heights)
    # Half way up is the waist on this model. Chibi proportions put the head
    # in the top third and the legs in the bottom quarter, so a midpoint cut
    # separates "hips and legs" from "sleeves and collar" with room to spare —
    # the lowest opening left alone sits at 55% and the highest one closed at
    # 32%.
    waist = floor + (ceiling - floor) * 0.5

    before = describe(bm)

    # Repeated until it stops helping, then finished by hand.
    #
    # holes_fill closes what it recognises as a ring and gives up on the rest
    # without saying so — the largest opening here went from thirteen edges to
    # ten and stayed there, which reads as success if only the return value is
    # checked. The loop below watches the edge count instead, and whatever
    # survives that gets triangulated across, which does not care whether the
    # boundary is a tidy ring.
    filled = []
    for attempt in range(6):
        low = [
            loop for loop in boundary_loops(bm)
            if max(v.co.z for e in loop for v in e.verts) <= waist
        ]
        if not low:
            break

        opened = sum(len(loop) for loop in low)
        for loop in low:
            vertices = {vertex for edge in loop for vertex in edge.verts}
            if attempt < 2:
                bmesh.ops.holes_fill(bm, edges=loop, sides=0)
            else:
                # Closed into a ring first, when it is not one.
                #
                # The stubborn opening had ten edges and eleven vertices, and a
                # ring of eleven has eleven edges — so it was an open chain with
                # two loose ends, and neither filler will touch one. There is
                # nothing to fill until the ends are joined.
                loop = close_chain(bm, loop)
                bmesh.ops.triangle_fill(
                    bm,
                    use_beauty=True,
                    use_dissolve=False,
                    edges=loop)

            filled.append({
                "pass": attempt,
                "how": "holes_fill" if attempt < 3 else "triangle_fill",
                "edges": len(loop),
                "z": [
                    round(min(v.co.z for v in vertices), 4),
                    round(max(v.co.z for v in vertices), 4),
                ],
            })

        remaining = sum(
            len(loop) for loop in boundary_loops(bm)
            if max(v.co.z for e in loop for v in e.verts) <= waist)
        if remaining == 0:
            break

        # No progress and no tricks left is worth saying out loud rather than
        # exporting a model with a hole in it and a report that looks fine.
        if remaining >= opened and attempt >= 3:
            print(
                f"CLOSE| gave up with {remaining} open edges below the waist")
            break

    # Triangulated, because a hole filled with one many-sided face renders
    # unpredictably and this mesh is triangles everywhere else.
    new_faces = [face for face in bm.faces if len(face.verts) > 3]
    if new_faces:
        bmesh.ops.triangulate(bm, faces=new_faces)

    after = describe(bm)
    bm.to_mesh(mesh_object.data)
    bm.free()
    mesh_object.data.update()

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=str(target),
        use_selection=True,
        object_types={"MESH", "ARMATURE"},
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        use_armature_deform_only=False,
        add_leaf_bones=False,
        bake_anim=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        mesh_smooth_type="FACE",
        path_mode="RELATIVE",
        embed_textures=False,
        use_custom_props=True,
    )

    print("CLOSE|" + json.dumps({
        "before": before,
        "after": after,
        "filled": filled,
        "waistZ": round(waist, 4),
    }))


main()
