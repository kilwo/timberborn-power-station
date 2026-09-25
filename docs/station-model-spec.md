# Power Transfer Station: model spec

This is what the code and blueprint expect from the Blender model. Units are blocks (1 block = 1 Blender
unit). Blender is Z-up; the game converts to Y-up on export.

## Reference material
- The vanilla example scene is `<Timberborn>/Timberborn_Data/StreamingAssets/Modding/TimberbornExampleModels.blend`.
- The Timbermesh Blender plugin and export workflow come from `../timberborn-modding` (see its README).
- Use vanilla material names, so the game swaps in its own materials: for example `BaseWood_DarkBrown`,
  `BaseWood_Grey` and `BaseMetal`, as in `ShantySpeaker/Placeholders/*.mat`. Match the Folktails look of
  the Power Shaft and Clutch.

## Footprint and origin
- **1 × 1 footprint, 3 blocks tall.** The model sits in X 0–1, Y 0–1 (Blender) and height 0–3.
- The **origin is the block corner** at (0, 0, 0), not the centre. That's the vanilla convention: blueprint
  collider centres are at 0.5.
- Keep everything inside the 1×1×3 volume. The block above the station is free for other buildings, and
  the rope blocks start right beside the tower top.

## Base (block 1, height 0–1)
- It works like a shaft junction, with an axle stub on **all four sides**: shafts connect on the four
  horizontal faces.
- Put the axle stubs at the same height and size as the vanilla Power Shaft, so adjacent shafts line up.
  Compare with the Power Shaft or Clutch in the example scene.
- Nothing is needed on the top or bottom faces. There are no vertical shaft connections.

## Tower (blocks 2–3, height 1–3)
- A post or frame rising to the pulley. Keep it slender (about 0.3 wide) so ropes and neighbours read clearly.

## Pulley
- It's a **horizontal wheel**: its axis is vertical and it spins around the Z axis.
- Its **centre is at (0.5, 0.5, 2.85)**, where 2.85 is the height of the rope line. That matches the
  blueprint's `RopeAnchorPoint {X 0.5, Y 2.85, Z 0.5}`, which is in Y-up game space.
- The rope strands attach at **radius 0.175** from the centre (`PulleyRadius`). Each rope is a loop:
  one strand leaves each side of the wheel, perpendicular to the rope direction, so any heading works.
- Give the wheel a groove at that radius, at height 2.85.
- If you want a different height or radius, tell me the numbers. They are two blueprint values
  (`RopeAnchorPoint.Y`, `PulleyRadius`). Keep the height between 2.5 and 2.95 so the rope stays in the
  top block.

## Animation
- Add a **looping animation of the pulley spinning** around its vertical axis, embedded in the Timbermesh
  export, the same way vanilla mechanical buildings such as the Power Meter do it. One full turn per loop
  cycle is fine.
- The blueprint already has the vanilla `MechanicalNodeAnimatorSpec`. The game plays the animation only
  while the station is powered, and scales its speed with network power efficiency. No code is needed.
- Optionally, animate the base axle stubs spinning as well.

## Files to deliver
Put these in `mod/Buildings/Power/PowerTransferStation/`:

| File | Purpose |
|---|---|
| `PowerTransferStation.Folktails.Model.timbermesh` | Finished model with the spin animation |
| `PowerTransferStation.Folktails.ConstructionStage0.Model.timbermesh` | Optional; the scaffold shown while under construction. Without it I keep the vanilla Clutch stage-0 model. |
| `PowerTransferStationIcon.png` + `PowerTransferStationIcon.png.meta.json` (`{"isSprite": true}`) | Toolbar icon, the same format as `../seeder` |

When they're in, I'll:
- point the blueprint's `TimbermeshSpec` at them
- remove the placeholder Clutch and pylon children
- fit the colliders
- set `RopeAnchorPoint` and `PulleyRadius` to the final numbers
- switch the icon

## Iron Teeth (later)
A second model `PowerTransferStation.IronTeeth.Model.timbermesh` with the same dimensions and pulley
position. It's a blueprint-only addition.
