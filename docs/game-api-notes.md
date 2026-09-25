# Game API notes

Written against **Timberborn 1.1.2.4** (Steam stable, `StreamingAssets/Version.txt` = `1.1.2.4-52e959e-sw`).
The code was decompiled with `ilspycmd` into `decompiled/` (gitignored). The vanilla blueprints come from
`Timberborn_Data/StreamingAssets/Modding/Blueprints.zip`, extracted to `decompiled/Blueprints/`.

Access levels matter here: we can only call `public` members without reflection or Harmony. Every member
listed below has its access level noted.

---

## 1. Summary: the power hook

**Hook:** a Harmony **postfix on `TransputMap.GetFacingTransput(Transput)`** (public class, public method,
`Timberborn.MechanicalSystem.dll`).

- If the queried transput is one of a station's **rope-slot transputs** and that slot is assigned to a rope
  link, return the partner station's paired rope-slot transput. In every other case, leave `__result` alone.
- The game's own `MechanicalGraphManager.AddNode` then does the rest: it calls `Transput.Connect` on both
  ends and **joins the two graphs** with `MechanicalGraphFactory.Join`. Because the connection is a real
  transput connection, `MechanicalGraphReorganizer` follows it whenever *any* node is removed. Graph
  splits and merges therefore stay correct without further patches.

**Rebuild trigger (no patch):** `MechanicalNode.SetDetached(bool)` is public. After adding or removing a
link, call `SetDetached(true)` and then `SetDetached(false)` on one station's node:

- `SetDetached(true)` runs `MechanicalGraphManager.RemoveNode`. This disconnects **all** of the node's
  transputs, rope slots included, and reorganizes the old graph.
- `SetDetached(false)` runs `MechanicalGraphManager.AddNode`. This re-queries `GetFacingTransput` for every
  transput, so our postfix reconnects the ropes that still exist and joins the graphs.

The game uses this same public method for the Clutch (`Timberborn.PowerManagement.Clutch`), so it is a
supported path. Our station is not a clutch, so nothing else toggles its detached state.

This is a single, small, isolated patch on a public method, and all merge/split logic stays vanilla.
Plan B (paired generator/consumer) should not be needed. Phase 3 still has to verify this in-game.

### Rope-slot transputs (how the station exposes them)

A `MechanicalNode` builds its `Transputs` array from `TransputProviderSpec` in the blueprint, so the rope
slots are ordinary blueprint transputs:

- Use K extra `TransputSpec` entries (K = hard cap on ropes per station, e.g. 3). Each has a single
  direction and a coordinate **inside the station's own tower**, facing **another block of the station
  itself**, e.g. `Coordinates (0,0,2)`, `Directions "Bottom"`, with target `(0,0,1)`.
- Because the target block is occupied by the station, no other building can ever have a transput there
  that "faces" it. Vanilla `GetFacingTransput` therefore always returns `null` for the slots, so they're
  inert until our postfix pairs them.
- Don't add `MechanicalConnectorTargetSpec` to the station, or the game would try to show shaft connector
  stubs on horizontal and top transputs.
- The max-ropes setting must be ≤ K (the slot count in the blueprint). Raising K later is a
  blueprint-only change.
- Cosmetic: `MechanicalNodeSelfMarkerDrawer` draws a marker for every transput while the building is
  selected or previewed, so the slots get markers inside the tower. This is probably hidden by the model;
  revisit in Phase 5.

Slot assignment doesn't need to be persisted. On load we restore the link list, give each link a free slot
on each end, and trigger the rebuild.

---

## 2. Mechanical (power) system: `Timberborn.MechanicalSystem.dll`

| Class | Access | Purpose / members we care about |
|---|---|---|
| `MechanicalNode` | public, `BaseComponent` | One per powered/shaft building. `Transputs` (ImmutableArray, built in `InitializeTransputs()` from `TransputProviderSpec`), `Graph` (internal set), `IsShaft/IsGenerator/IsConsumer/IsIntermediary`, `IgnoreRotation`, `Powered`, `PowerEfficiency`, `ActiveAndPowered`, **`SetDetached(bool)`** (public), events `AddedToGraph`, `TransputsInitialized`. Lifecycle: `OnEnterFinishedState` → `_transputMap.AddNode` + `AddOrRemoveFromGraph`; `OnExitFinishedState` → `RemoveFromGraph` + `_transputMap.RemoveNode`. Unfinished buildings register transputs in the map but join no graph. |
| `Transput` | public | `ParentNode`, `ConnectedTransput`, `ConnectedNode`, `Connected`, `Coordinates`, `Direction`, `Target` (= coords + direction), `IsFinished`, `Connect(Transput)` / `Disconnect()` (public), `Faces(Transput)` (adjacency + opposite direction), `ReversedRotation`, `RotationMatches`. A transput holds **one** connection. Public ctor `(MechanicalNode, TransputSpec, Direction3D, BlockObject)`. |
| `TransputMap` | public, `ILoadableSingleton` | 3D grid of transputs. `AddNode`, `RemoveNode`, **`GetFacingTransput(Transput)`**: returns the transput at `transput.Target` that `Faces` it, or null. Events `TransputAdded`, `TransputRemoved`. **This is the method we patch.** |
| `MechanicalGraphManager` | internal | `AddNode(node)`: creates a 1-node graph; for each transput, if `GetFacingTransput` is non-null, finished and has a `Graph`, it connects both ends and collects graphs, then calls `Join`. `RemoveNode(node)`: disconnects all connected transputs, removes the node from its graph, calls `Reorganizer.Reorganize(graph)`. |
| `MechanicalGraphReorganizer` | internal | Flood-fills the old graph's nodes via `node.Transputs.Select(t => t.ConnectedNode)` into new graphs. This is why rope connections survive unrelated rebuilds. |
| `MechanicalGraphFactory` | internal | `Create()`, `Join(IEnumerable<MechanicalGraph>)`: moves all nodes into a fresh graph. |
| `MechanicalGraph` | public | `Nodes`, `Generators`, `Batteries`, `PowerSupply`, `PowerDemand`, `PowerSurplus`, `Powered`, `PowerEfficiency`, `BatteryCharge/Capacity`, `Valid`. `AddNode`/`RemoveNode` are internal. The graph doesn't check rotation, so a rotation mismatch never blocks power. |
| `MechanicalGraphRegistry` | public | List of live graphs. Posts `MechanicalGraphCreatedEvent` / `MechanicalGraphRemovedEvent` when a graph becomes 1-node or empty. |
| `MechanicalNodeSpec` | public record | Blueprint: `PowerOutput`, `PowerInput`, `IsShaft`. Intermediary = none of the three. |
| `TransputProviderSpec` / `TransputSpec` | public records | Blueprint: `Transputs[] { Coordinates, Directions ("Down, Left, Up, Right", "Bottom", "Top"…), ReverseRotation }`, `IgnoreRotation`. |
| `MechanicalSystemConfigurator` | internal | Binds the above. |

### Other code that reads transput connections (checked, compatible with long-range connections)

- `Timberborn.MechanicalSystemUI.MechanicalGraphModelUpdater` (internal) walks connected transputs through
  **shafts only** to set rotation directions and refresh models. It never assumes the transputs are
  adjacent. A station with `IsShaft: false` stops the walk, which is harmless.
- `Timberborn.MechanicalSystemUI.MechanicalModel` is auto-decorated on every `MechanicalNode`. Its
  `UpdateModel()` is null-safe when there's no `IMechanicalModelUpdater`.
- `Timberborn.ModularShafts.ModularShaftVariantFinder` looks at connected neighbours to pick shaft model
  variants. It only applies to modular shafts, not to our station's rope slots.
- `Timberborn.AutomationBuildings.PowerMeter` calls `GetFacingTransput(Transputs[0])` for its own
  transput. Our postfix leaves non-rope-slot transputs untouched.
- `Timberborn.MechanicalConnectorSystem.*` only handles visual connector stubs. It only activates on
  buildings with `MechanicalConnectorTargetSpec`. It isn't a linking mechanism.
- `Timberborn.PowerManagement.Clutch` calls `SetDetached(!IsEngaged)`. That's precedent for our rebuild
  trigger.

---

## 3. Station blueprint reference

- In 1.1 there's **no separate "shaft junction" building**. `PowerShaft.<Faction>` is a *modular* shaft
  (`ModularShaftSpec`) that auto-renders straight, corner and junction variants. Transputs: `(0,0,0)`
  `"Down, Left, Up, Right"` (ReverseRotation true) plus `"Bottom"`. `VerticalPowerShaft` adds `"Top"`.
- We shouldn't copy `ModularShaftSpec`, because the station needs its own model. The best template is the
  **Clutch** (`Buildings/Power/Clutch/Clutch.Folktails.blueprint.json`): a 1×1×1 non-modular intermediary
  with `MechanicalNodeSpec {0,0,IsShaft:false}`, `TransputProviderSpec` with `"Left, Right"` and
  `IgnoreRotation: true`, and its own Timbermesh model.
- Proposed station: Clutch-like, 1×1×3, base transputs `(0,0,0)` `"Down, Left, Up, Right"`,
  `IgnoreRotation: true`, plus K rope slots as in §1.
- Mod data layout follows `../seeder/mod`: `Buildings/<Group>/<Name>/<Name>.Folktails.blueprint.json` plus
  `.timbermesh` files beside it, and `TemplateCollections/TemplateCollection.Buildings.Folktails.blueprint.json`
  using `"Blueprints#append": [...]`. `.timbermesh` files load straight from the mod folder, with no Unity
  and no asset bundle.
- Blueprint model reference: `"TimbermeshSpec": { "Model": "Buildings/.../X.Folktails.Model" }` (path
  without extension). `TimbermeshSpec.Model` is an `AssetRef<BinaryData>` resolved lazily through
  `IAssetLoader` (`AssetRefDeserializer`). The same loader serves vanilla resources and mod files, so a
  mod blueprint can reference **vanilla** models by path. The Phase 1 placeholder does this with the
  Clutch and ZiplinePylon models. Unfinished state uses a nested `ConstructionBases/ConstructionBase1x1/...` blueprint.

---

## 4. Zipline system: `Timberborn.ZiplineSystem.dll` / `ZiplineSystemUI.dll`

### Connection storage and persistence: `ZiplineTower` (public, `BaseComponent`)

- Implements `IInitializableEntity`, `IPostInitializableEntity`, `IDeletableEntity`, `IFinishedStateListener`,
  and `IPersistentEntity`.
- `List<ZiplineTower> _connectionTargets`. Each end stores the other end. `AddConnection`/`RemoveConnection`
  raise a `ConnectionTargetsChanged` event.
- **Save:** `entitySaver.GetComponent(new ComponentKey("ZiplineTower")).Set(new ListKey<ZiplineTower>("ConnectionTargets"), list, _referenceSerializer.Of<ZiplineTower>())`.
- **Load:** reads the list into `_loadedConnectionTargets` in `Load()`, then in **`PostInitializeEntity()`**
  re-validates with `CanBeConnected` and calls `Connect`. Deleted or invalid partners are skipped with
  `Debug.LogWarning`. This is exactly our "tolerate missing partner" behaviour.
- **Delete:** `DeleteEntity()` removes from the registry and disconnects every target.
- `OnEnterFinishedState` activates the connections (unfinished towers show grey cables).
- Spec (`ZiplineTowerSpec`, **internal**): `CableAnchorPoint` (grid-space Vector3; pylon `0.5, 3.85, 0.5`),
  `MaxConnections` (2), `MaxDistance` (**30**), `UnobstructedCoordinates`.

### Service and validation: `ZiplineConnectionService` (public singleton, but methods take `ZiplineTower`)

- `CanBeConnected(a, b)` checks: different tower, free slots, not already connected, same district, then
  `InclinationIsValid` (`< MaxCableInclination` = **50°**, from `Configurations/ZiplineConnectionService.blueprint.json`),
  `DistanceIsValid` (anchor-to-anchor distance ≤ min(MaxDistance)), and **clearance**.
- **Clearance:** `BresenhamLineDrawer.DrawLine(startInt, endInt, set)` (**public** singleton) gives the
  voxel line between anchors, minus the anchors and each tower's `UnobstructedCoordinates`. Each cell must
  pass `BlockValidator.BlocksValid(connectionBlockSpec, new Placement(cell))` (**public**, `Timberborn.BlockSystem`)
  or already hold a `CableBlock`.
- **Blocks reserved along the cable:** `Connect` creates an invisible `ZiplineConnectionBlock` entity
  (`Models/ZiplineCable/ZiplineConnectionBlock.blueprint`) in each line cell, so nothing can later be
  built through a cable. `Disconnect` deletes them.
- We can't reuse the service directly (it's tied to `ZiplineTower`, and its spec is internal), but we can
  inject `BresenhamLineDrawer` and `BlockValidator` and repeat the same algorithm.

### Rendering: `ZiplineCableRenderer` (public singleton) and `ZiplineCableModel` (public)

- Each connection is **two straight cables** (left and right). Each is an instance of the
  `Models/ZiplineCable/ZiplineCable.blueprint` template, created with `TemplateInstantiator.Instantiate(blueprint, root)`
  from `ISpecService.GetBlueprint(path)`.
- Placement: `CoordinateSystem.GridToWorld(anchor)`, offset sideways by `ZiplineCalculator.CalculateWorldConnections`
  (public static; front 0.38, side 0.175). Position is the midpoint, rotation is `LookRotation`, and
  `localScale.z` is the length. The shader gets properties `_Length` and `_IsOperative` (the latter animates
  the cable).
- There's **no sag**: cables are straight. For catenary sag we need our own generated mesh or
  LineRenderer. We could still reuse the zipline cable material.
- It handles grayscale for inactive cables (`MaterialColorer`), highlight (`Highlighter`), shadow-only when
  the level-visibility slider hides either end, and visibility in construction mode.

### UI: `Timberborn.ZiplineSystemUI` (all internal, so we copy the structure)

- `ZiplineTowerFragment`: entity panel middle fragment (`EntityPanelModule.Builder.AddMiddleFragment`),
  with a list of connections plus add/remove buttons (`ZiplineConnectionButtonFactory`).
- `ZiplineConnectionAddingTool`: `ITool, IToolDescriptor, IInputProcessor, IConstructionModeEnabler`.
  - It uses `SelectableObjectRaycaster.TryHitSelectableObject`, `InputService.MainMouseButtonDown` and `MouseOverUI`.
  - On a click it validates and connects, then plays `UISoundController.PlayClickSound` or `PlayCantDoSound`.
  - It shows a preview with `ZiplinePreviewCableRenderer`, sets the cursor to `"PickObjectCursor"`, and has the loc key `Zipline.PickDestination`.
  - After connecting, it re-enters the tool on the target if that tower has free slots.
- `ConnectionCandidates` highlights valid targets, and `ZiplinePreviewTooltip` gives the reason text.
- `ZiplineConnectionDevModule`: an **`IDevModule`** (`Timberborn.Debugging`, public) with
  `DevModuleDefinition.Builder().AddMethod(DevMethod.Create("label", action))`. It's registered with
  `MultiBind<IDevModule>().To<...>().AsSingleton()`. **We'll use this pattern for the Phase 2 debug
  trigger** (it appears in the in-game dev menu).

---

## 5. Entity lifecycle and persistence

| Interface / class | Assembly | Notes |
|---|---|---|
| `IAwakableComponent.Awake()` | BaseComponentSystem | Get sibling components with `GetComponent<T>()`. |
| `IInitializableEntity.InitializeEntity()` | EntitySystem | Register in registries. |
| `IPostInitializableEntity.PostInitializeEntity()` | EntitySystem | Resolve loaded references (partners exist by now). |
| `IDeletableEntity.DeleteEntity()` | EntitySystem | Demolish/delete cleanup. |
| `IFinishedStateListener` (`OnEnterFinishedState`/`OnExitFinishedState`) | BlockSystem | Construction finished, or the building leaves the finished state. |
| `IPersistentEntity` (`Save(IEntitySaver)` / `Load(IEntityLoader)`) | WorldPersistence | Use `ComponentKey` (struct, WorldPersistence) and `ListKey<T>` (struct, Persistence), with `GetComponent(key).Set/Get`. |
| `ReferenceSerializer.Of<T>() where T : BaseComponent` | WorldPersistence | Serializes entity references across saves (public, injectable). |
| `EntityComponent.Deleted` | EntitySystem | Check before reconnecting a loaded partner. |

Persistence details (checked while writing Phase 2):
- `IObjectSaver.Set<T>(ListKey<T>, IReadOnlyCollection<T>, IValueSerializer<T>)` and
  `IObjectLoader.Get<T>(ListKey<T>, IValueSerializer<T>)`. `PropertyKey<int>` works for plain values.
  `IObjectLoader.Has(key)` and `IEntityLoader.TryGetComponent(key, out loader)` exist.
- `ReferenceSerializer.Of<T>()` saves the partner's `EntityComponent.EntityId` (a Guid string) and
  resolves it through `EntityRegistry` on load.
- **Missing references are dropped silently:** `SaveConversions.DeconvertList` skips entries whose entity
  or component can't be resolved. To warn about dropped links, `PowerTransferStation` also saves
  `RopePartnerCount` and compares it on load.

Global config specs:
- `SpecService.Load` enumerates every blueprint from every asset provider, including mod files, and
  indexes them by spec type. A mod blueprint such as `mod/Configurations/RopeConnectionService.blueprint.json`
  containing `RopeConnectionServiceSpec` is therefore found by `ISpecService.GetSpecs<T>()`.
- `GetSingleSpec<T>()` throws when there are zero matches (actually an NRE) or several. We use
  `GetSpecs<T>().FirstOrDefault()` with built-in defaults instead.
- Custom spec records (`record X : ComponentSpec` with `[Serialize] { get; init; }`) compile fine on
  `netstandard2.1`, with no `IsExternalInit` polyfill needed.

Dev menu:
- Dev mode is toggled with **Alt+Shift+Z** (`KeyBindings/Dev/KeyBinding.ToggleDevMode`, unchangeable).
- `IDevModule` methods appear in the dev panel while dev mode is on.

Component wiring: in a `[Context("Game")]` `Configurator`, use
`MultiBind<TemplateModule>().ToProvider(...)` with `TemplateModule.Builder.AddDecorator<TSpec, TComponent>()`.
This attaches our component to any template with the spec. A custom `ComponentSpec` record with `[Serialize]`
properties (see `ShantySpeaker/FinishableBuildingSoundPlayerSpec`) makes it available from blueprint JSON.

---

## 6. Mod loading and Harmony

- The game discovers code through `[Context("Game")]` `Configurator` classes in the mod DLL (mod root).
  `ILoadableSingleton.Load()` runs on game load.
- **Mod entry point:** `Timberborn.ModManagerScene.IModStarter.StartMod(IModEnvironment env)`. It's
  instantiated with `Activator.CreateInstance` at mod-manager time, and `env.ModPath` is the mod folder.
  This is the place to call `new Harmony("Elum.RopePower").PatchAll()`.
- **Harmony isn't shipped with the game.** Use the Workshop mod "Harmony" (`Id: "Harmony"`, v2.4.1,
  item 3284904751, `0Harmony.dll`). Add it to our manifest's `RequiredMods` and reference its DLL with
  `Private="false"` (`$(HarmonyDir)` in `Directory.Build.props`). The exact `RequiredMods` entry format
  still needs checking in Phase 3 against a mod that uses it.

---

## 7. Design decisions and open questions

**Decided (2026-09-25):** ropes copy zipline rules for clearance and blocks along the rope, the same-district
check, and the inclination limit (50°).

### Rope blocks along the path

- The vanilla block is `Models/ZiplineCable/ZiplineConnectionBlock.blueprint`: `BlockObjectSpec` 1×1×1,
  `MatterBelow: Any`, `Occupations: "Bottom, Top, Corners, Path, Middle"`, plus `CableBlockSpec`. It's
  created with `BlockObjectFactory.CreateAsPreview(spec, parent, new Placement(cell))` followed by
  `blockObject.MarkAsFinishedAndAddToServices()` (both public, `Timberborn.BlockSystem`).
- **We can't reuse the vanilla block entity.** `ZiplineConnectionService.Disconnect` deletes a cell's block
  whenever *its own* connection list has nothing left at that cell, so it would delete blocks a rope still
  needs, and the reverse would happen too. We need our own `RopeBlock` blueprint: the same `BlockObjectSpec`
  with our own marker spec instead of `CableBlockSpec`.
- Consequence: ropes can share cells with other ropes, but vanilla zipline validation only accepts
  `CableBlock` cells, so **a zipline can't cross a rope**. Whether a rope may cross a zipline is open
  (question B).

### District: open question A

- For ziplines, `ZiplineConnectionService.DistrictCentersAreCompatible` uses
  `PathDistrictRetriever.GetAnyDistrictCenter()` (public, `Timberborn.BuildingsNavigation`). That reads
  the district road at the tower's `PathSpec.MainPathCoordinates`, because zipline towers are path buildings.
- **If either end has no district, the check passes.** It only fails when both ends have a district and
  the districts differ.
- Power buildings have no path (`PathSpec`) and no `BuildingAccessible`, so they get neither
  `PathDistrictRetriever` nor `DistrictBuilding` (`Timberborn.GameDistricts`; decorated only onto
  `BuildingAccessible`). **A power station has no district by default.**
- **Decided rule (lenient, adjacent road):** a station's district is the first `DistrictCenter` in
  `DistrictCenterRegistry.AllDistrictCenters` (public) for which `IsOnPreviewDistrictRoad(pos)` or
  `IsOnInstantDistrictRoad(pos)` (public, `Timberborn.GameDistricts.DistrictCenter`) is true. `pos` is
  `CoordinateSystem.GridToWorld(cell)` for each of the 4 cells horizontally adjacent to the station base.
  This mirrors `PathDistrictRetriever.GetAnyDistrictCenter`. Linking fails only if both ends have a
  district and they differ. No district is allowed, the same as the zipline rule.
- Implementation risk: several adjacent roads could belong to different districts. Take the first match,
  as the zipline code effectively does.

### Crossings (decided)

- **No crossings between ropes and ziplines.** They block each other like any other obstacle. Ropes may
  share cells with other ropes: a cell holding our `RopeBlock` counts as clear for a new rope. No patching
  is needed.
