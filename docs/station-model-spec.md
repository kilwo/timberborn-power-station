# Power Transfer Station: model spec

This is the contract between the station model and the code/blueprint. Units are blocks. Coordinates are
**Unity model space** (Y up, origin at the block corner) unless marked otherwise.

## Current model: generated
`mod/Buildings/Power/PowerTransferStation/PowerTransferStation.Folktails.Model.timbermesh` is **generated** by
`tools/TimbermeshGen` (`Models/PowerTransferStationModel.cs`). It isn't a Blender export. The tool writes the
Timbermesh format directly (see `docs/game-api-notes.md` §9) and reads every file back with the game's own
DTO, so what it writes is what the game parses. All dimensions are constants at the top of the model class.

Regenerate after changing the model code:

```bash
cd tools/TimbermeshGen
dotnet run -- station ../../mod/Buildings/Power/PowerTransferStation/PowerTransferStation.Folktails.Model.timbermesh
```

Preview renders (Blender 5.x, textures loaded from the game's example `.blend`, nothing copied into the repo):

```bash
dotnet run -- obj <model.timbermesh> <out>/station.obj 10         # optional 3rd arg: animation frame to pose
blender -b --factory-startup --python preview.py -- <out> <out>/station.obj [neighbour.obj]   # overview/pulley/base.png
blender -b --factory-startup --python preview.py -- <out> <out>/station.obj - icon           # icon.png (112x112)
```

The toolbar icon `PowerTransferStationIcon.png` is the `icon` render.

## What's in it
| Node | Pivot | Contents | Animation |
|---|---|---|---|
| `PowerTransferStation.Folktails.Model` | (0, 0, 0) | Plank deck, gearbox housing and lid, metal band and bearing collars, four-post tapered trestle with rungs and braces, top platform | none |
| `#Pulley` | (0.5, 2.85, 0.5) | Wooden wheel with a metal-lined groove, octagonal hub, four yellow straps on top, drive shaft down to the lid | one turn about +Y, angle decreasing |
| `#AxleX`, `#AxleZ` | (0.5, 0.5, 0.5) | 0.22 square axles face to face (the stubs on all four sides), metal bands | one turn about their axis |

Every animation is `Default`, 96 frames at 24 fps (4 s per turn). The pulley rim (r 0.175) then moves at
0.275 blocks/s, which matches the cable texture's 0.273 blocks/s. The spin direction matches the cable's
travel: cable leaves each station on `CableLoopModel`'s `+side` and arrives on `−side`.
`MechanicalNodeAnimatorSpec` runs the animation only while the station is powered.

Materials: `BaseWood_Brown/LightBrown/White.Folktails`, `BaseMetal.Folktails`, `PaintedMetal.Folktails`, with
all vertex colours white. The model is about 2,000 vertices and 21 KB.

## Fixed numbers (code and blueprint depend on these)
- **Footprint** 1 × 1, 3 blocks tall. Everything stays inside X/Z 0–1, Y 0–3.
- **Shaft stubs:** centre height **0.5**, square **0.22**, reaching the block faces on all four sides. These
  are the vanilla shaft axle dimensions, measured from the Clutch and Power Meter models.
- **Pulley centre** (0.5, 2.85, 0.5) = blueprint `PowerTransferStationSpec.CableAnchorPoint`. The cable groove sits
  at radius **0.175** = `PulleyRadius`, and strands attach there on either side of the wheel.
- **Cable clearance:** nothing outside radius 0.15 of the tower axis above y 2.72, other than the wheel itself,
  so cables in any direction clear the tower top.

## Replacing it with a hand-made model later
A Blender model exported with the Timbermesh plugin can replace the generated file if it keeps the numbers above.
- In the vanilla example scene, footprints sit at **negative** Blender X/Y (a 1×1 building spans X −1..0,
  Y −1..0, Z up). The exporter maps Blender (x, y, z) to Unity (−x, z, −y). This mapping is inferred from the
  example scene's positions and hasn't been checked through the plugin.
- Use Folktails material names (the list above). An unknown material name throws when the model loads.
- Name the spinning part's animation anything; the game plays the model's first animation.

## Not done yet
- `PowerTransferStation.Folktails.ConstructionStage0.Model`: the unfinished state still uses the vanilla
  Clutch stage-0 model.
- Iron Teeth: a second model with the same numbers (`Atlas.cs` would need the Iron Teeth material names).
  It's a blueprint-only addition.
