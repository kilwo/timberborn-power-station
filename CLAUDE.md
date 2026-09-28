# CLAUDE.md: Timberborn "Cable Power Transfer" Mod

## Project summary

A Timberborn (1.0+) mod that replaces long runs of power shafts with **Power Transfer Stations** linked by **cable loops**.

- A **Power Transfer Station** is a 1×1 building. Its base connects to adjacent power shafts or powered buildings like a shaft junction. A tower rises 2 blocks above the base with a pulley wheel at the top.
- Players link two stations with a **cable loop**, drawn between the tower tops like a zipline cable.
- Linked stations behave as if a continuous power shaft joined them: both sides become **one mechanical power network**.
- A station can hold multiple cables (default max 3), so stations can chain and branch.

## Guiding principles

1. **Minimal game patching.** Use official blueprints, components, and `[Context]` configurators wherever possible. Use Harmony only for the power-graph hook, and keep every patch small, isolated, and documented.
2. **Never guess game API names.** Timberborn's internal class and method names change between versions. Before writing code that touches a game type, find it in the decompiled source (see "Decompiling the game"). If something can't be verified, stop and ask me; don't invent a plausible-looking API.
3. **Mirror the Zipline system.** The Folktails zipline already solves connection storage, clearance validation, cable rendering, the connection tool, and save/load. Study it first and copy its structure.
4. **Prove the risky part first.** The power-graph merge (Phase 3) is the main technical risk. Don't build UI or visuals until it works.
5. **I test in-game.** You can't run Timberborn. At the end of each phase, give me a short, concrete in-game test checklist, and wait for my results before moving on.

## Environment and tooling

- **Target game build:** Timberborn 1.1.2.4 (Steam stable). Decompile against the installed DLLs.
- **Build setup (hybrid, no Unity):** this repo follows my existing dotnet-only mods (`../rotting-trees`, `../seeder`, `../Logically`). Mirror their conventions:
  - `Directory.Build.props` defines `TimberbornDir`, `TimberbornManaged` and `ModsDir`, with the default Steam path and `Documents/Timberborn/Mods`.
  - `src/CablePowerTransfer/CablePowerTransfer.csproj` is `netstandard2.1` and references the game's `Managed/*.dll` with `Private="false"`.
  - `mod/` holds the manifest, blueprints, localization and models. The build copies `mod/**` plus the DLL into `Documents/Timberborn/Mods/CablePowerTransfer/`.
- **Modding repo:** `https://github.com/mechanistry/timberborn-modding` is cloned alongside at `../timberborn-modding`. It's used only as a reference for the manifest, blueprint and Timbermesh formats. The example mods ship `.timbermesh` files directly in `Data/`, so models don't need Unity. The repo pins Unity 6000.5.11f1; only install that if we ever need an asset bundle.
- **Harmony:** not shipped with the game. Depend on the Steam Workshop "Harmony" mod (manifest `Id: "Harmony"`, v2.4.1, workshop item 3284904751) through `RequiredMods`. Reference its `0Harmony.dll` at compile time with `Private="false"`, and never bundle it.
- **3D models:** Blender plus the Timbermesh plugin. I'll handle modelling; you write the specs and code that reference the models.
- **Git:** a local repo in this folder with no remote. Make small commits, one per logical step.

### Decompiling the game

Use `ilspycmd` to decompile the game assemblies into a local reference folder:

```bash
dotnet tool install -g ilspycmd
# Game DLLs live in <Steam>/steamapps/common/Timberborn/Timberborn_Data/Managed/
ilspycmd -p -o ./decompiled/<AssemblyName> "<path>/Timberborn.<AssemblyName>.dll"
```

Priority assemblies to decompile and read (exact names may differ, so list the Managed folder first):

- Mechanical/power system (nodes, graph, transputs, graph rebuild triggers)
- Zipline system (connections, stations, cable rendering, connection tool, validation)
- Power shaft building and its blueprint
- Entity panel / UI fragment system
- Persistence (entity save/load, entity references across saves)

`./decompiled/` is reference only. Add it to `.gitignore` and never commit or redistribute it.

Keep a running notes file at `docs/game-api-notes.md`. For each relevant game class, record its name, assembly, purpose, and the members we use. Update it whenever you learn something.

## Architecture

### Station building (blueprint)
- New blueprint based on the vanilla power shaft junction.
- 1×1 footprint, 3 blocks tall (base plus 2 tower blocks).
- Mechanical transput faces on the four sides of the base block only.
- Reasonable build cost to start (logs + planks + gears); tune later.
- Faction: Folktails first. Structure it so an Iron Teeth variant is a blueprint-only addition.

### `PowerTransferStation` component (C#)
- Holds `CablePartners`: a list of linked stations, persisted by entity ID.
- Exposes an anchor point (the world position of the pulley top) for rendering.
- On demolish or deletion, removes all its cables from both ends and triggers a power-graph rebuild.
- Tolerates a missing partner on load: drop the link and log a warning.

### `CableConnectionService` (C#)
- Single place that adds and removes links, keeping both ends in sync.
- Validates new links:
  - The target is a different station.
  - The two stations aren't already linked.
  - Both stations are under the max-cables limit.
  - The span is within the maximum (configurable; start at the vanilla zipline range of 30).
  - **Steepness:** the same inclination rule and limit as ziplines (`MaxCableInclination`, 50° in 1.1.2.4, computed as in `ZiplineConnectionService.InclinationIsValid`).
  - **Same district (lenient, as for ziplines):** a station's district is the district road on any tile horizontally adjacent to its base. Linking fails only when both ends have a district and they differ. A station with no adjacent district road can link to anything.
  - **Clearance and blocks along the cable (same as ziplines):** take the Bresenham voxel line between the anchors, excluding each station's own cells. Every cell must pass `BlockValidator.BlocksValid` for a 1×1×1 cable block or already hold a cable block. On connect, place an invisible cable block entity in each cell so nothing can be built through the cable. On disconnect, remove the blocks no other cable still uses.
  - **Our own `PowerCableBlock` blueprint, not the vanilla zipline block.** Cables can share cells with other cables, but cables and ziplines can't cross each other.
- Triggers a power-graph rebuild after every change.

### Power graph hook (Harmony)
- Goal: cable partners are treated as neighbours when the game builds mechanical networks.
- Expected approach: a **postfix** on the method that collects a mechanical node's connected/adjacent nodes, adding cable partners' nodes. Confirm the real method via decompilation before writing it.
- All Harmony code lives in `Patches/`, one patch per file, each with a header comment naming the game method and version it was written against.
- **Fallback (plan B, only if a graph merge proves impossible or unstable):** paired transfer, where one station acts as a consumer and the partner acts as a generator of equal output. Discuss with me before switching.

### Connection tool and UI
- Entity panel fragment on a selected station that lists its connections (partner, distance) with remove buttons, plus an "Add cable" button.
- "Add cable" enters a picking mode: hover a station to preview the cable, green if valid and red with a reason if not. Click to confirm; Esc cancels.
- Mirror the zipline connection UX as closely as practical.

### Visuals
- Station model: base + tower + pulley wheel (my Blender work, exported as Timbermesh).
- Cable loop: two parallel lines between the pulley tops with slight catenary sag, drawn as a generated mesh or LineRenderer (match whatever ziplines use if feasible).
- When powered, scroll the cable texture and spin the pulleys, with speed tied to network power if the game exposes it. When unpowered, keep them static.

### Settings (configurable, for balancing)
- Max cable span
- Max cables per station
- Optional per-station power loss in hp (default 0)
- Optional throughput cap (default off)

**Open decision:** whether cables carry a gameplay cost (loss or cap) or are a pure long-distance shaft replacement. Implement both as settings defaulting to off, and I'll decide after playtesting.

## Layout

```
power-station/
  CLAUDE.md
  Directory.Build.props         # game + Mods paths (same as my other mods)
  docs/game-api-notes.md
  docs/test-checklists.md
  mod/                          # deployed as-is to Documents/Timberborn/Mods/CablePowerTransfer/
    manifest.json
    Buildings/, TemplateCollections/, Localizations/  # blueprints + .timbermesh, same as ../seeder/mod
  src/CablePowerTransfer/
    CablePowerTransfer.csproj
    Stations/                   # PowerTransferStation, configurators
    Cables/                      # CableConnectionService, validation, persistence
    Patches/                    # Harmony patches only
    UI/                         # panel fragment, connection tool
    Rendering/                  # cable mesh / animation
  tools/TimbermeshGen/          # offline model generator/inspector (.timbermesh), Blender preview script
  decompiled/                   # gitignored reference only
```

Inside `mod/`, data folders sit at the mod root, as in `../seeder/mod`: `Buildings/<Group>/<Name>/...blueprint.json` plus `.timbermesh` models, `TemplateCollections/`, and `Localizations/enUS.csv`. The vanilla blueprints come from `<Timberborn>/Timberborn_Data/StreamingAssets/Modding/Blueprints.zip`, extracted for reference into `decompiled/Blueprints/`.

## Phases

Each phase ends with: a summary of what changed, any API assumptions made, and an in-game test checklist for me.

### Phase 0: Setup and research
- Set up the dotnet project skeleton and confirm a minimal mod (one configurator that logs `[CablePowerTransfer] loaded`) builds, deploys and loads in-game.
- Decompile the priority assemblies and write `docs/game-api-notes.md`, covering how the mechanical graph is built and rebuilt, how zipline connections are stored and validated, and how zipline cables render.
- **Done when:** the notes file clearly identifies the method to patch for the power hook, or explains why no clean hook exists.

### Phase 1: Station prototype
- Blueprint-only station, using a placeholder model (a scaled vanilla shaft or simple box).
- **Done when:** the station can be built, connects to shafts on its sides like a junction, and passes power through locally.

### Phase 2: Persistent links (no UI)
- `PowerTransferStation` component and `CableConnectionService`, with save/load of links.
- Temporary debug trigger (a debug key or console command) that links the two most recently built stations.
- **Done when:** links survive save/load and are cleaned up on demolition (verified via logs).

### Phase 3: Power graph merge ⚠️ key risk
- Implement the Harmony patch and graph rebuild triggers.
- **Done when:** a generator connected to station A powers a building connected to station B, with no shafts between them. This must still work after save/load, after demolishing and rebuilding either station, and after the game rebuilds its networks for any other reason.

### Phase 4: Connection tool and panel UI
- Replace the debug trigger with the real tool and panel, including validation feedback and localization strings.
- **Done when:** the whole flow is playable with no debug tools.

### Phase 5: Visuals
- Cable rendering with sag, anchored to the tower tops. Powered animation for cable and pulleys. Integrate the real station model once I provide it.
- **Done when:** cables look right at various distances and height differences, and the animation reflects power state.

### Phase 6: Balance, polish, release
- Settings, cost tuning, and edge-case testing: flooding, large networks, many stations, loading saves made without the mod, and removing the mod from a save.
- Prepare the Steam Workshop and mod.io description and thumbnail checklist.

## Coding conventions

- Use dependency injection via the game's configurator / `[Context]` pattern, as in the example mods. No static singletons except Harmony patch classes.
- Log with a consistent `[CablePowerTransfer]` prefix.
- Fail safe: an invalid or corrupt cable link is dropped with a warning, never a crash.
- Keep game-version-sensitive code (patches, reflection) isolated and commented.
- Small, reviewable commits, one per logical step.

## When to stop and ask me

- A needed game API can't be found or verified.
- The Phase 3 hook looks impossible without heavy patching (before switching to plan B).
- Any design choice that changes gameplay feel: costs, limits, faction availability.
- Any change that would require editing game files or redistributing game code (never do this).
