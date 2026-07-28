# Reference Map Landmark Interpretation

## Coordinate convention

All normalized coordinates in the layout specification use the playable village rectangle only: `x=0` is west/left, `x=1` is east/right, `y=0` is north/top, and `y=1` is south/bottom. The image legend, title, tips, labels, arrows, coloured route strokes, spawn outlines, and icons are excluded from physical geometry.

For the planned `80 x 68m` map, convert a normalized point `(x, y)` to Unity ground coordinates with:

```text
unity_x = (x - 0.5) * 80
unity_z = (0.5 - y) * 68
unity_y = 0
```

## Physical landmarks to recreate

| Landmark | Reference position | Layout interpretation |
|---|---|---|
| Police station | Upper centre | Main police landmark, front facing the central road; police spawn immediately in front. |
| Supermarket | Left of police station | Primary reward building and western route obstruction. |
| Bookstore | Right of police station | Primary reward building and eastern route obstruction. |
| Plaza/fountain | Directly south of police station | Central open landmark with a fountain, four benches, trees/bushes, and ring-road crossings. |
| Homes | Perimeter, four corners, outer blocks | 12 homes total, enclosing alleys and forming roof-route segments. |
| Roads/intersections | Central grid plus perimeter lanes | Main loop favours patrol; narrow outer alleys create thief detours. |
| Ladders | House edges and roof-route entry points | Three minimum gameplay ladder locations; the reference visually shows six. |
| Trash/raccoon spots | Outer alleys | Place bins in cover-rich alleys and attach raccoon spawn anchors to them. |

## Home count visible in the reference

| Type | Count | Asset availability |
|---|---:|---|
| 1-storey house | 7 | `building_house_1f.fbx` available |
| 2-storey house | 5 | Missing `building_house_2f`; map-shape pass uses unscaled `building_house_1f_with_interior` substitutes |
| Total homes | 12 | Road/building silhouette is achievable now; final elevation silhouette awaits 2F asset |

## Gameplay data, not 3D UI

The following should become scene transforms/waypoints using the existing `GreyboxMapDefinition` pattern:

- Police and thief spawn anchors.
- `GreyboxRouteReference` waypoint chains for police patrol/track, thief escape, and cat rooftop routes.
- Ladder bottom/top endpoints and `LadderTraversal` trigger bounds.
- Raccoon spawn anchors attached to selected trash-bin locations.
- Hiding-zone anchors and collider volumes for alley, bush, and bench cover.

The following are UI-only and must not be modeled: route arrows, building-name labels, home-floor labels, start outlines, character icons, legend, play tips, and title panels.

## Planned landmark count

- Buildings: 15 total (`3` named commercial/civic landmarks + `12` homes).
- Building-only map-shape pass: roads and building volumes only; ladders, trash bins, fountain, foliage, benches, fences, and lamps are excluded until their assets are available.
- Hiding anchors: retain alley anchors only; bush/bench hiding locations remain design data until their assets exist.
- Major route families: police central patrol, thief outer escape, cat rooftop traversal.
