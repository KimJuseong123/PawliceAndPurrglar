# Missing Assets

## Core blocker

`building_house_2f` is missing. No FBX, GLB, Blender source, prefab, or independently reusable asset matching a 2-storey house was found. The reference contains **five 2-storey houses**. For the approved map-shape pass, each is represented by an unscaled `building_house_1f_with_interior` instance. This preserves the road/building layout without falsely presenting the substitute as a 2-storey final asset.

## Missing independent environment assets

| Required reference element | Status | Notes |
|---|---|---|
| Bench | Missing | Needed for visible plaza seating and bench hiding locations. |
| Bush | Missing as standalone asset | Some commercial/building FBXs contain fixed shrub submeshes, but they cannot be placed/reused independently through the current asset structure. |
| Tree | Missing | Needed for plaza and perimeter readability. |
| Fence | Missing | Needed to define yards and alley boundaries. |
| Street lamp | Missing as standalone asset | Decorative lamps are embedded in some building FBXs only. |
| Fountain | Missing | Current `Game` scene fountain is greybox geometry, not a reusable art asset. |
| Road/sidewalk module | Missing as model | `Road.mat` exists, but no road, curb, pavement, or intersection model module exists. |

## Available but requires gameplay setup

- `object_ladder.fbx` is available, but every use still needs a `LadderTraversal` interaction trigger plus bottom/top anchors.
- `object_trash_can.fbx` is available, but raccoon locations must be scene anchors associated with each selected bin.
- All available building/prop FBXs import with automatic colliders disabled. Separate collision geometry is mandatory.

No substitute models were created or modified. The documented building-only fallback reuses existing FBX instances unchanged.
