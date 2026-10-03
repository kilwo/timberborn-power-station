# Cable Power Transfer

A [Timberborn](https://store.steampowered.com/app/1062090/Timberborn/) mod that carries mechanical power over long
distances without long runs of power shafts.

Build two **Power Transfer Stations** and link them with a cable loop. Linked stations work like one continuous power
shaft: both sides join a single mechanical power network.

- **Game version:** Timberborn 1.1 (1.1.2.4 or later)
- **Factions:** Folktails and Iron Teeth
- **Requires:** [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751)

## Features

- **Power Transfer Station**, a new Power building. It has a 1×1 footprint and is 3 blocks tall. Its base connects to
  shafts and powered buildings on all four sides, like a shaft junction. It has a pulley on top.
- **Cable loops** between stations, up to 30 blocks apart. Each station holds up to 3 cables, so you can chain and
  branch power across the map.
- **Real network merging.** Generators, batteries and consumers on either side share one network, exactly as if a shaft
  joined them. Removing a cable or demolishing a station splits them again.
- **Animated.** Cables sag naturally and move while power flows. The pulley turns with the network, and each shaft stub
  turns the same way as the shaft attached to it.
- **Zipline-style tool.** It previews the cable in green or red, with the reason when a link isn't allowed.

## How to use

1. Unlock the Power Transfer Station (600 science) and build it from the Power tab (16 logs, 12 planks, 8 gears).
2. Select a station and click **Add connection** in its panel.
3. Hover another station. The preview cable is green if the link is valid. If it isn't, it's red and shows the reason:
   too far, too steep, too many connections, line obstructed, or different districts. Click to connect. Esc cancels.
4. To remove a cable, click the ✕ on its button in the station panel.

You can link to a station that's still under construction. Its cable is shown in construction mode, and it carries
power once the station is built.

## Rules

Cables follow the same rules as ziplines:

| Rule | Default |
|---|---|
| Maximum length | 30 blocks |
| Maximum steepness | 50° |
| Cables per station | 3 |
| Clear line | The straight line between the two pulleys must be free. Each cable reserves the cells it passes through, so nothing can be built through it. Cables can share cells with other cables, but cables and ziplines can't cross. |
| Districts | Stations next to roads of two different districts can't be linked. A station with no road beside it can link to anything. |

Connecting a cable is free, and cables carry power without loss or a throughput limit. Like power shafts, stations
keep working when flooded.

### Configuration

The limits are in [`mod/Configurations/CableConnectionService.blueprint.json`](mod/Configurations/CableConnectionService.blueprint.json)
(`MaxCableSpan`, `MaxCablesPerStation`, `MaxInclination`). A station has 3 cable slots, so `MaxCablesPerStation` can't
go above 3. Cable sag and smoothness are in
[`mod/Configurations/CableRenderer.blueprint.json`](mod/Configurations/CableRenderer.blueprint.json). In an installed
copy these files are in the mod folder, and mod updates replace them.

## Saves

- The mod works with existing saves.
- Removing it is safe. When you load the save, the game shows a "Loading issues" dialog and deletes the stations.
  Everything else is unaffected, and cables leave nothing behind (they aren't saved as objects).

## How it works

The whole power merge is one small Harmony postfix on `TransputMap.GetFacingTransput`
([`Patches/TransputMapGetFacingTransputPatch.cs`](src/CablePowerTransfer/Patches/TransputMapGetFacingTransputPatch.cs)).
Each station has hidden "cable slot" connection points. Normally nothing ever faces them. For a linked slot, the patch
returns the partner station's slot, so the game's own code connects the two stations and joins their networks. When a
link changes, the station's node is detached and re-attached. That's the same public path the vanilla Clutch uses, so
all merging and splitting stays vanilla.

Everything else uses the game's normal modding pieces: blueprints, components, `[Context]` configurators, and the
vanilla zipline cable look. See [`docs/game-api-notes.md`](docs/game-api-notes.md) for the game classes involved.

## Building from source

Requirements:

- the .NET SDK (any version that can build `netstandard2.1`)
- Timberborn installed through Steam
- the Harmony mod subscribed on the Steam Workshop (used as a compile-time reference only, never bundled)

```bash
dotnet build src/CablePowerTransfer -c Release
```

The build compiles against the game's DLLs and copies `mod/**` plus `CablePowerTransfer.dll` to
`Documents/Timberborn/Mods/CablePowerTransfer/`. The default paths are in [`Directory.Build.props`](Directory.Build.props).
Override them if your install is elsewhere:

```bash
dotnet build src/CablePowerTransfer -c Release -p:TimberbornDir="D:\Games\Timberborn" -p:HarmonyDir="D:\path\to\3284904751"
```

Add `-p:DeployMod=false` to build without copying.

### Models

The station models are generated in code, not exported from Blender.
[`tools/TimbermeshGen`](tools/TimbermeshGen) writes the `.timbermesh` files for both factions: the station, four
shaft stubs and the construction stage. It reads each file back with the game's own format classes to check it.

```bash
cd tools/TimbermeshGen
dotnet run -- station ../../mod/Buildings/Power/PowerTransferStation
```

The model's dimensions, and how to replace it with a hand-made model, are in
[`docs/station-model-spec.md`](docs/station-model-spec.md). The toolbar icon is drawn by
[`tools/Icons/station_icon.py`](tools/Icons/station_icon.py).

## Project layout

```
mod/                    Deployed as-is: manifest, blueprints, models, icon, localization, configuration
src/CablePowerTransfer/
  Stations/             PowerTransferStation component, registry
  Cables/               Linking, validation, cable blocks, power refresh
  Patches/              The Harmony patch
  UI/                   Station panel and the connection tool
  Rendering/            Cable loops and station animation
tools/                  TimbermeshGen (models), icon script, Blender preview
docs/                   Game API notes, model spec, test checklists, release notes
```

Decompiled game code is used locally as a reference only. It's excluded from the repository and never redistributed.

## License

[GPL-3.0](LICENSE)
