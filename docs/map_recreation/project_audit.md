# Map Recreation Project Audit

## Scope and evidence

This audit is read-only. No Unity scene, prefab, game data, Blender source, or runtime code was changed. The layout target is the playable village area in `C:\Users\SSAFY\Desktop\NHN 게임 공모전\village_map_reference.png`; its left legend and lower/right instruction panels are UI, not world geometry.

## Engine and scene structure

| Item | Current project state |
|---|---|
| Engine | Unity `6000.5.4f1` (Unity 6.5) |
| Render/input/navigation packages | URP `17.5.0`, Input System `1.19.0`, AI Navigation `2.0.13`, Cinemachine `3.1.7`, Netcode for GameObjects `2.13.0` |
| Scene format | Unity text YAML (`.unity`) |
| Build scene flow | `Bootstrap.unity` -> `Game.unity` -> `Result.unity` |
| Main playable level | `Assets/_Project/Scenes/Game.unity` |
| Final map save path | `Assets/_Project/Scenes/Game.unity`; `GameSceneCatalog` and Build Settings already resolve this exact name/path. |
| Existing map definition | `GreyboxMapDefinition` component in `Game.unity`, currently `80 x 68m` |

`GameSceneLoader` calls `SceneManager.LoadScene(..., Single)` unless the network bridge owns the load. The map is therefore scene-resident, not addressable-loaded or procedurally streamed.

## Map, assets, and linked-object usage

- The current map is an in-scene greybox with direct imported-FBX model instances, box/capsule colliders, anchors, and child `Waypoint NN` transforms.
- `GreyboxMapDefinition` serializes location anchors, route waypoint lists, rooftops, ladders, and trash-bin references. It requires seven named logical locations, at least three rooftops/ladders, four trash bins, and route clear widths of at least `2.4m`.
- Imported FBX assets act as Unity model-prefab sources (`m_SourcePrefab` references in `Game.unity`). There is no authored reusable village-building prefab layer under `Assets/_Project/Prefabs/`; that folder currently holds only technical/network prefabs.
- Blender originals are kept below `ArtSource/Blender/`; Unity imports FBX below `Assets/_Project/Art/`. The project does not directly reference `.blend` files at runtime.
- Model importers use `globalScale: 1`, `useFileUnits: 1`, and `addColliders: 0`. Building collision must remain separate gameplay collision geometry rather than automatic mesh colliders.

## Coordinates, player, camera, and collisions

| Concern | Current implementation |
|---|---|
| World axes/units | Unity metres; ground plane is `X/Z`, elevation is `Y`. Blender source uses `Z` up and is imported with the Unity axis conversion. |
| Source model front | Doors lie on Blender negative-Y. With Unity import conversion, the intended unrotated entrance direction is Unity `+Z`; verify once in-editor before mass placement. |
| Player capsule | Both Police and Thief use `CharacterController`, height `2.0m`, radius `0.45m`, step offset `0.35m`, slope limit `45` degrees. |
| Player height | Controller bottom is at the player transform in `Game.unity`; visual meshes are children. Gameplay clearance must use the `2.0m x 0.9m` capsule, not mesh silhouette. |
| Movement/collision | `PlayerMovementMotor` drives `CharacterController.Move`; gravity is applied there. Buildings/props use separate colliders. Companions also use `CharacterController` movement and a stuck recovery path. |
| Camera | Perspective camera, FOV `50`, fixed follow offset `(0, 12.36, -10.82)`, look offset `(0, 1, 0)`, smooth time `0.12s`; it is a tilted top-down camera, not orthographic/isometric. |
| Interaction | `PlayerInteractionScanner` uses an overlap sphere at `PlayerConfig.interactionRange` (`2m` default) and `IPlayerInteractable`. |
| Doors | No dedicated door interaction script or door state system exists. Visible model doors must remain decorative until an `IPlayerInteractable` implementation is explicitly approved. |

## Navigation, routes, and spawning

- AI Navigation is installed, but `Game.unity` has no baked NavMesh data and runtime companion movement intentionally does not use `NavMeshAgent`.
- The current route system is `GreyboxRouteReference`: ordered scene `Transform` waypoints, route IDs, endpoint anchors, and physics-clearance validation. This is the correct format for the new police, thief, and roof-route data.
- Existing required logical anchors are `PoliceSpawn`, `ThiefSpawn`, `Supermarket`, `Bookstore`, `JewelryStore`, `RaccoonMarket`, and `CentralPlaza`. `PlayerRoleSpawnResolver` maps Police and Thief directly to the first two.
- Current ladder interaction uses `LadderTraversal` with a trigger area plus bottom/top transforms; it is bidirectional, takes `0.6s`, and disables the player controller while moving.
- Current raccoon placement is tied to the market/bin area. The reference-image raccoon markers should be represented as empties/anchors adjacent to trash bins, not as UI icons or physical arrows.

## Existing naming conventions

| Type | Convention/examples |
|---|---|
| Scenes | PascalCase: `Bootstrap`, `Game`, `Result`, `TechnicalTest` |
| Assets | lower snake case: `building_house_1f.fbx`, `object_trash_can.fbx` |
| Map anchors | PascalCase semantic names: `PoliceSpawn`, `CentralPlaza`, `RaccoonMarket` |
| Routes | `FromTo_Descriptor`, e.g. `PoliceToJewelry_Plaza`, `SupermarketToBookstore_NorthLoop` |
| Waypoints | Child transforms named `Waypoint 00`, `Waypoint 01`, ... |
| Traversal | `Name Ladder`, `Name Ladder Climb`, `Name Rooftop` |

## Reuse recommendation

Implement the recreation inside the existing `Game` scene and preserve the `GreyboxMapDefinition` contract. Use imported FBXs as model-prefab instances under a map root, add simple separate colliders, and serialize all planned path data as route/waypoint transforms. Do not introduce a NavMesh dependency or a second scene unless the scene-loading contract is deliberately changed.

## Constraints discovered

1. The reference requires five 2-storey homes, but no `building_house_2f` asset exists. For the map-shape pass, use unscaled `building_house_1f_with_interior` instances as explicitly documented visual substitutes; this does not resolve the final-art gap.
2. There are no independent bench, tree, fence, fountain, or road/sidewalk module assets. The map-shape pass excludes these props and consists of roads plus buildings only. Some buildings contain decorative shrubs or lamps, but those submeshes are not separately reusable project assets.
3. The game map currently requires legacy `JewelryStore` and `RaccoonMarket` anchors while the reference map depicts supermarket/bookstore reward zones and no jewelry-store landmark. Their final semantic placement needs a product decision before implementation.
