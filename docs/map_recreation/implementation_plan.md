# Village Map Recreation Implementation Plan

## Target

Recreate the playable portion of the supplied village-map reference in `Assets/_Project/Scenes/Game.unity`, preserving current scene loading, `GreyboxMapDefinition`, `CharacterController` collision, and waypoint-route architecture. The map-shape target is `80 x 68m`, 15 buildings total, and 12 homes. It contains roads, building volumes, and only existing building assets; missing environment props are excluded.

## Implementation order

1. Decide the compatible placement/purpose of the currently required `JewelryStore` anchor.
2. Capture the current map root/anchor conventions and create the planned road, block, lot, and empty-plaza geometry inside the existing `Game` scene. Keep all through-routes at or above `2.4m` clear width.
3. Place the police station, supermarket, bookstore, seven 1F homes, and five 2F-lot substitutes from existing FBXs. Use `building_house_1f_with_interior` unchanged for every missing 2F home; keep all models at scale `1`.
4. Add separate box/compound colliders for the roads' obstacles and building volumes. Preserve clearance for the `2.0m` high, `0.45m` radius player capsule and companions.
5. Leave the plaza empty and omit all missing bench/tree/bush/fence/street-lamp/fountain/road-module art. Do not create substitute props.
6. Serialize police patrol/track and thief escape routes as `GreyboxRouteReference` waypoint children. Retain cat-roof route data as deferred design data until ladders/roofs are enabled.
7. Rebind the required `PoliceSpawn`, `ThiefSpawn`, `Supermarket`, `Bookstore`, `CentralPlaza`, `RaccoonMarket`, and decided `JewelryStore` anchors. Do not replace the current `PlayerRoleSpawnResolver` or scene loader.
8. Perform the project-required Unity validation after implementation: compile, relevant edit/play mode tests, route clearance, collision, camera framing, spawn, and gameplay-flow regression.

## Existing systems to preserve

- `GameSceneCatalog`/`GameSceneLoader` with `Game.unity` as the map scene.
- `GreyboxMapDefinition` map dimensions, location anchors, `GreyboxRouteReference` waypoint lists, and required feature counts.
- `PlayerMovementMotor`/`CharacterController` collision and `PlayerInteractionScanner` interaction radius.
- `LadderTraversal` for all vertical movement.
- Direct FBX model-prefab instances; do not reference `.blend` files in Unity.
- No NavMesh bake or `NavMeshAgent` addition: companions currently use `CharacterController` movement and waypoint data.

## Explicitly excluded from map modeling

No 3D mesh should be created for the reference-image arrows, labels, home-floor labels, iconography, legend, title panels, play tips, or spawn-outline UI.

## Pre-production approvals required

1. Mapping of the existing `JewelryStore` gameplay anchor to a reference-compatible location or game-rule revision that removes the requirement.
2. Whether the building-only map-shape pass is sufficient for playtesting or missing props should later be added as approved assets.
3. Whether all six visual ladders in the reference should be gameplay-enabled in the later traversal pass. The current validation requires at least three.
4. Whether building doors stay decorative or a door gameplay system is intended; no such system currently exists.
