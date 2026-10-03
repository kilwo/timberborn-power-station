# mod.io listing: Cable Power Transfer

## Summary (the 250-character field)

Link Power Transfer Stations with cable loops to carry mechanical power up to 30 blocks without power shafts. Linked stations join one power network. Folktails and Iron Teeth. Requires Harmony.

## Tags

Buildings, New content (or whatever mod.io's Timberborn tag list offers closest to these).

## Description

Tired of laying a hundred power shafts? Build two **Power Transfer Stations** and link them with a cable loop. Linked stations work like one continuous power shaft: both sides join a single mechanical power network.

### Features

- New Power building: the **Power Transfer Station**. It has a 1×1 footprint and is 3 blocks tall. Its base connects to shafts and powered buildings on all four sides, like a shaft junction.
- Link stations with cable loops up to **30 blocks** apart. Each station holds up to **3 cables**, so you can chain and branch power across the map.
- Linked networks merge completely. Generators, batteries and consumers on either side share one network, exactly as if a shaft joined them. Removing a cable or demolishing a station splits them again.
- Cables sag naturally and move while power flows. The pulley and the station's shaft stubs turn with the connected shafts.
- Folktails and Iron Teeth versions.

### How to use

1. Unlock the station for 600 science, then build it for 16 logs, 12 planks and 8 gears.
2. Select a station and click **Add connection** in its panel.
3. Hover another station. The preview cable is green if the link is valid. If it isn't, it's red and shows the reason: too far, too steep, too many connections, line obstructed, or different districts. Click to connect. Esc cancels.
4. To remove a cable, click the ✕ on its button in the station panel.

You can link to a station that's still under construction. Its cable is shown while you're in construction mode, and it carries power once the station is built.

### Rules

Cables follow the same rules as ziplines:

- Up to 30 blocks long, and no steeper than 50°.
- The straight line between the two pulleys must be clear. Each cable reserves the cells it passes through, so nothing can be built through it. Cables can share cells with other cables, but cables and ziplines can't cross.
- Stations next to roads of two different districts can't be linked. A station with no road beside it can link to anything.

Connecting a cable is free, and cables carry power without loss or a throughput limit. Like power shafts, stations keep working when flooded.

### Requirements

- Timberborn 1.1 (1.1.2.4 or later).
- **Harmony** (required).

### Saves and compatibility

- Works with existing saves.
- Removing the mod is safe. When you load the save, the game shows a "Loading issues" dialog and deletes the stations. Everything else is unaffected, and cables leave nothing behind.
- The mod uses one small Harmony patch on how mechanical buildings find their connected neighbours. Other mods that change that may conflict.

### Configuration

The cable limits are in `Configurations/CableConnectionService.blueprint.json` in the mod folder: `MaxCableSpan` (30), `MaxCablesPerStation` (3, which is also the most a station can hold) and `MaxInclination` (50). Mod updates replace this file, so make your changes again after an update.

### Languages

English.
