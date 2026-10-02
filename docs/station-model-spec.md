# Power Transfer Station: model spec

This is the contract between the station model and the code/blueprint. Units are blocks. Coordinates are
**Unity model space** (Y up, origin at the block corner) unless marked otherwise.

## Current model: generated
`mod/Buildings/Power/PowerTransferStation/PowerTransferStation.Folktails.Model.timbermesh`, the four input-stub
models `PowerTransferStation.Folktails.Stub{Right,Left,Up,Down}.Model.timbermesh` and the construction stage
`PowerTransferStation.Folktails.ConstructionStage0.Model.timbermesh` are **generated** by
`tools/TimbermeshGen` (`Models/PowerTransferStationModel.cs`). It isn't a Blender export. The tool writes the
Timbermesh format directly (see `docs/game-api-notes.md` §9) and reads every file back with the game's own
DTO, so what it writes is what the game parses. All dimensions are constants at the top of the model class.

Regenerate after changing the model code:

```bash
cd tools/TimbermeshGen
dotnet run -- station ../../mod/Buildings/Power/PowerTransferStation      # writes all six model files
```

Preview renders (Blender 5.x, textures loaded from the game's example `.blend`, nothing copied into the repo):

```bash
dotnet run -- obj <out>/station.obj 10 <model.timbermesh>...      # merges models; 10 = animation frame to pose
blender -b --factory-startup --python preview.py -- <out> <out>/station.obj [neighbour.obj]   # overview/pulley/base.png
```

The toolbar icon `PowerTransferStationIcon.png` is flat line art in the vanilla icon style, drawn by
`tools/Icons/station_icon.py`. The style matches the vanilla power icons: gold (186, 160, 107), ~2.5 px rounded
strokes at 112 px, soft black shadow, transparent background. To redraw it:

```bash
python tools/Icons/station_icon.py mod/Buildings/Power/PowerTransferStation/PowerTransferStationIcon.png
```

## What's in it
| Node | Pivot | Contents | Animation |
|---|---|---|---|
| `PowerTransferStation.Folktails.Model` | (0, 0, 0) | Plank deck, gearbox housing and lid, metal band and bearing collars, four-post tapered trestle with rungs and braces, top platform | none |
| `#Pulley` | (0.5, 2.85, 0.5) | Wooden wheel with a metal-lined groove, octagonal hub, four yellow straps on top, drive shaft down to the lid | one turn about +Y, angle decreasing |
| `#Stub` in each `...Stub<Face>.Model` (Face = the base `Direction3D`: Right +X, Left −X, Up +Z, Down −Z) | (0.5, 0.5, 0.5) | One 0.22 square input stub, from inside the housing to its block face, with a metal band | one turn about its **outward** axis, which is "Normal" rotation in the game's transput convention; `StationAnimator` plays it backwards when the connected shaft needs "Reversed" |
| `PowerTransferStation.Folktails.ConstructionStage0.Model` | (0, 0, 0) | Unfinished state, on top of the vanilla `ConstructionBase1x1`: deck, gearbox housing without its lid, the four posts up to y 1.4 and the first ring of rungs. Built from the same parts as the finished model | none |

Every animation is `Default`, 96 frames at 24 fps (4 s per turn). The pulley rim (r 0.175) then moves at
0.275 blocks/s, which matches the cable texture's 0.273 blocks/s. The spin direction matches the cable's
travel: cable leaves each station on `CableLoopModel`'s `+side` and arrives on `−side`.
The stubs are separate models so each has its own animator. `StationAnimator` (`StationAnimatorSpec` in the
blueprint, replacing the vanilla `MechanicalNodeAnimatorSpec`, which drives only one animator) runs all five while the
station is powered. It turns each stub the same way as the shaft or generator on that side (see
`docs/game-api-notes.md` §10).

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
- Iron Teeth: a second model with the same numbers (`Atlas.cs` would need the Iron Teeth material names).
  It's a blueprint-only addition.
