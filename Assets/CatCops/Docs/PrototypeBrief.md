# Cat Cops Prototype Brief

## Direction

- Title: Cat VS Cop: Treasure Chase
- Format: 2.5D top-down, low-poly, friendly rounded proportions
- Base: Unity 6000.5.4f1 with TopDown Engine preserved under `Assets/TopDownEngine`
- Prototype role: one playable police-side slice that demonstrates chase, throw, AI partner command, thief escape, cat delivery, and black-market scoring

## Core Loop

1. The thief starts with loot near the treasure shop.
2. The police player moves with WASD from the police station.
3. The dog follows a simulated voice command and chases the thief.
4. The player throws a stone with `R` to stun the thief for a short window.
5. The thief attempts to reach the black-market trash-can merchant.
6. If the thief sells loot, thief money increases. If police or dog catches the thief, police score increases.

## AI Command Slice

- `1` or `V`: police voice command, dog starts chasing.
- `2`: cat hide/roof command demonstration.
- `3`: bag-open command demonstration near the black market.
- `B`: thief-side bone throw test, distracting the dog.

## Art Targets

- Characters are generated from Blender as simple low-poly FBX models.
- The scene uses compact toy-diorama buildings, rooftops, ladders, alleys, props, and warm 2.5D lighting.
- TopDown Engine is left untouched and bridged through level/score events.
