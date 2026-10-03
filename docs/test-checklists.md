# In-game test checklists

Build and deploy: run `dotnet build src/CablePowerTransfer -c Release`. This copies `mod/**` and `CablePowerTransfer.dll` to
`Documents/Timberborn/Mods/CablePowerTransfer/`. Logs are in
`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`.

## Phase 0: skeleton loads

1. Start Timberborn. In the mod manager, **Cable Power Transfer** (v0.0.1) is listed; enable it.
2. Load any Folktails save, or start a new game.
3. Close the game and search `Player.log` for `[CablePowerTransfer] loaded`. It should appear exactly once per
   game load.
4. Check there are no exceptions mentioning `CablePowerTransfer` in `Player.log`.

Report: pass/fail for each step, plus any log lines containing `CablePowerTransfer`.

**Result (2026-09-25): passed.** `[CablePowerTransfer] loaded` appeared in Player.log.

## Phase 1: station prototype (blueprint only)

Use a **Folktails** game. Dev mode's instant build is fine for speed.

1. **Tool:** the Power tool group has **Power Transfer Station**, with the Clutch icon as a placeholder. Its
   cost is 8 Log, 6 Plank and 4 Gear, with no science needed.
2. **Placement:** it places on flat ground and occupies 1×1×3. The placeholder is a Clutch base plus a
   zipline pylon. **Expected:** the pylon model sticks up about one block above the station's top.
3. **Construction:** the unfinished state shows a construction base, and builders can finish it.
4. **Side connections:** put a power shaft against each of the 4 sides of the base. Each shaft's model
   should show an arm connecting into the station.
5. **Straight pass-through:** generator → shaft → **station** → shaft → consumer (e.g. a Lumber Mill) on
   opposite sides. The consumer is powered, and selecting a shaft on either side shows the same network
   supply and demand.
6. **Corner and branch:** generator on one side, consumers on two other sides. All of them are powered.
7. **Demolish:** demolish the station and the consumers lose power. Rebuild it and power comes back.
8. **Save/load:** save, reload, and power still flows through the station.
9. **Cable slots (cosmetic check):** select the station. Report any odd power markers or arrows inside or
   on top of the tower. The station has 3 hidden cable connection points there.
10. **Log:** check Player.log for exceptions or warnings that mention `PowerTransferStation`,
    `CablePowerTransfer`, `Timbermesh` or `Transput`.

Report: pass/fail for each step, a screenshot of steps 2 and 9 if anything looks off, and any relevant log
lines.

**Result (2026-09-25): passed** ("looks good").

## Phase 2: persistent links (no UI, no power merge yet)

Links are **data only** in this phase. They don't carry power yet (that's Phase 3), there's no cable
visual, and no clearance or cable-block check. Everything is verified through `Player.log` lines starting
with `[CablePowerTransfer]`.

Setup: a Folktails game. The shortcuts below don't need dev mode. To build quickly, turn on dev mode
(**Alt+Shift+Z**, which logs `Dev mode enabled`) and **hold Ctrl while placing** a station to place it already
finished (vanilla `PlaceFinished`). "Newest" means most recently
**finished**; unfinished stations don't count.

Dev actions: each result appears as an on-screen notification starting "Cable Power Transfer:" and is also logged.

| Shortcut (works with or without dev mode) | Action |
|---|---|
| **Ctrl+Alt+L** | link two newest stations |
| **Ctrl+Alt+J** | link newest station to all others |
| **Ctrl+Alt+U** | unlink newest station |
| **Ctrl+Alt+K** | log all cable links (count on screen, pairs in Player.log) |

The same actions are in the dev panel. It appears **bottom-left** while dev mode is on and is collapsed
by default: click its title, then type "cable" in the filter box.

*First attempt (2026-09-25): the user found no way to trigger linking. The log showed dev mode enabled
but no Cable Power Transfer action invoked, because the panel is collapsed and wasn't found. The shortcuts and
notifications were added as a fix.*

*Second attempt (2026-09-25): the keys didn't work. The log had no `Dev mode enabled`, so the
`DevModeOnly` bindings were blocked by `DevModeKeyBindingBlocker`. The keys now work without dev mode.
On the first frame after loading a save, the log confirms
`[CablePowerTransfer] dev: debug keys active …` (or warns `key bindings not found`). Each finished station also
logs `station (x, y, z) finished`.*

1. **Load:** the log shows `[CablePowerTransfer] loaded (max span 30, max cables 3, max inclination 50)` and **not**
   `CableConnectionService blueprint not found`.
2. **Link:** build station A, then station B about 10 blocks away. Run *link two newest* and the log
   shows `linked station (…) <-> station (…) (span …)`.
3. **Inspect:** *log all cable links* shows `2 station(s), 1 cable link(s)` and the pair.
4. **Rejections:**
   - Run *link two newest* again and you get `rejected: AlreadyLinked`.
   - Build C more than 30 blocks from B, then run *link two newest*. You get `rejected: TooLong`.
   - Optional: build a station very close to another but much higher (e.g. on a cliff 4+ blocks up,
     1 block away). *link two newest* gives `rejected: TooSteep`.
5. **Max cables:** build 4 more stations within 30 blocks of each other, all finishing after A and B.
   Run *link newest station to all others*. The newest one links to 3 and then logs `rejected: SourceFull`.
6. **Save/load:** save and reload.
   - Each link shows exactly **one** `restored link … <-> …` line.
   - *log all cable links* shows the same pairs as before saving.
   - There are no `dropped` warnings.
7. **Demolish:** demolish one linked station. The log shows `unlinked …` for each of its links, and
   *log all cable links* no longer lists them. Save, reload: nothing about it is restored and there are no
   warnings.
8. **Unlink:** run *unlink newest station* and the newest station's links are removed (`unlinked …`).
9. **District (optional, needs two districts):** put station X beside a road of district 1 and station Y
   beside a road of district 2, within 30 blocks. *link two newest* gives `rejected: DifferentDistricts`.
   A station with no road next to it links to either.
10. **Log:** no exceptions mentioning `CablePowerTransfer` or `PowerTransferStation`.

Report: pass/fail for each step, plus the `[CablePowerTransfer]` lines from steps 6 and 7.

**Partial result (2026-09-25, third attempt):**
- Steps 1–4 passed. The debug keys were active, linking and counting worked, and AlreadyLinked, TooSteep
  (1 across, 3 up) and TooLong (about 30.6 blocks) were all rejected correctly.
- No exceptions.
- Steps 5–8 were not run yet: the session ended with an autosave and no reload.

**Final result (2026-09-25): passed.** Phase 2 is done.
- Reload 1 restored 2 links, and reload 2 restored 5 links, each exactly once. There were no `dropped`
  warnings.
- Station (88, 62, 5) linked 3 times, then got `SourceFull`.
- Demolishing (83, 68, 5) logged `unlinked … <-> (88, 62, 5)`.
- Ctrl+Alt+U removed 1 link.
- No exceptions.
- Not explicitly run: a reload right after the demolish. Phase 3 step 6 covers it.
- Observation: on load, `restored link` lines come **before** the `finished` lines. `PostInitializeEntity`
  runs before `OnEnterFinishedState`, so links exist before the mechanical nodes join graphs.

## Phase 3: power graph merge ⚠️

The cables should now carry power. There's still no cable visual. Use the Ctrl+Alt keys from Phase 2 (no
dev mode needed). **Ctrl+Alt+K** now logs each link as
`cable connected: True/True | same network: True | network supply X hp, demand Y hp`.

**Setup:** use a clean area or a new Folktails game. **Demolish the old test stations** so "newest" is
unambiguous.
- Generator side: a generator (e.g. a Water Wheel in a current, or a Power Wheel) → shaft → **station A**.
- Consumer side: **station B** 10–25 blocks away with **no shafts between A and B** → shaft → a consumer
  (e.g. a Lumber Mill or Gear Workshop, staffed).
- Finish A first, then B. In dev mode, Ctrl-placing places buildings finished.

1. **Patch loaded:** Player.log shows `[CablePowerTransfer] Harmony patches applied` (at the main menu, before the
   save loads). There's no `patching failed` line.
2. **Before linking:** the consumer shows no power.
3. **Link:** press **Ctrl+Alt+L**. The consumer becomes powered within a moment. Selecting a shaft on
   B's side shows the generator's supply. Ctrl+Alt+K shows
   `cable connected: True/True | same network: True`.
4. **Unlink:** press **Ctrl+Alt+U** (B is newest). The consumer loses power, and Ctrl+Alt+K shows
   0 links. Press **Ctrl+Alt+L** again and power comes back.
5. **Save/load:** save and reload while linked. The consumer is powered straight after load, and
   Ctrl+Alt+K shows `True/True`, `same network: True`.
6. **Demolish and rebuild B:**
   - Demolish B. The consumer loses power, the log shows `unlinked`, and A's side still runs.
   - Build a new B in the same spot, then press **Ctrl+Alt+L** (links the new B with A) and power returns.
   - Save and reload, and it's still powered. Nothing about the old B is restored.
7. **Demolish and rebuild A:** the same as step 6, but from the generator side. After rebuilding A,
   press **Ctrl+Alt+L**.
8. **Other rebuilds:** while linked:
   - add a shaft branch with a second consumer on A's side
   - remove and re-add the shaft between B and the consumer
   - build and then demolish an unrelated shaft touching A's network

   Power keeps flowing to the consumer after each change.
9. **Chain:** build station C 10–25 blocks beyond B, with a consumer attached. Press **Ctrl+Alt+L**, which
   links C to B (the two newest). C's consumer is powered from A's generator through A→B→C. Demolish B
   and both consumers lose power.
10. **Log:** no exceptions and no `GetFacingTransput patch failed`.

Report: pass/fail for each step, and copy Player.log into the project folder. If power doesn't flow in
step 3, run Ctrl+Alt+K first so the log captures the link state.


**Result (2026-09-25): core merge passed.** The user reported "all looked ok". Log evidence:
- Step 1: `Harmony patches applied`.
- Step 3: A (83,58,5) and B (85,64,5), 6.3 apart, showed `cable connected: True/True | same network: True
  | network supply 79 hp`.
- Step 5: the link was restored after save/load.
- Step 6: B was demolished (unlinked), rebuilt and relinked.
- Step 7: A was demolished, rebuilt and relinked.
- A third station C (89,62,5) was linked to **both** A and B with Ctrl+Alt+J. That made a loop of cables,
  and it caused no errors.
- Step 10: no exceptions, errors or warnings anywhere in the log.

Gaps in the log evidence:
- Step 4 (Ctrl+Alt+U power drop) never ran. There's no `removed N link(s)` line. This is the only path
  where *our* refresh splits a network (on demolish, vanilla node removal does it). The Phase 4 remove
  button uses the same path.
- Ctrl+Alt+K always reported `demand 0 hp`, so the log doesn't show the consumer drawing power. Only the
  user's visual check covers that; the consumer was probably idle or unstaffed on day 1.
- Step 9 became a loop instead of a chain, so "demolish the middle station splits the chain" wasn't
  exercised.

## Phase 4: connection tool, station panel, cable blocks (+ deferred Phase 3 checks)

The debug keys are **gone**. Everything now goes through the station panel. Each link change writes a
`[CablePowerTransfer] power … cable connected …/… same network …` line to Player.log as evidence.

Setup: a Folktails game. Dev mode with Ctrl-placing is fine for speed. Start from a clean area, because
old test cables still work but there's no key to list them any more.

**Panel and tool**
1. Select a finished station. The panel has a **"Cables:"** section with an **"Add connection"** button
   (plus icon) and 2 greyed empty slots.
2. Click **Add connection**:
   - the station is highlighted
   - the cursor changes
   - the bottom text says "Select another Power Transfer Station to connect with a cable"
3. Hover another station within range. A **green** preview cable appears, and the tooltip shows distance
   `x / 30` and inclination `y / 50` with green ticks.
4. Hover invalid targets. The preview turns **red** and the tooltip gives the reason:
   - too far: `Too far!`
   - too steep: `Too steep!`
   - a station that already has 3 cables: `Too many connections!`
   - a building in the cable's straight line: `Line obstructed!`, with the blocking building highlighted
   - optional: stations beside two different districts' roads give the districts warning
5. Press **Esc** in picking mode. The tool exits and the origin station is selected again.
6. Click a valid target:
   - the cable is created: two straight strands between the tower tops, using the zipline cable look as a
     placeholder until Phase 5
   - the target becomes selected, and the tool continues from it if it has free slots (like ziplines)
   - Esc stops
7. The station panel lists the cable: partner icon plus length. Hovering the button highlights the
   partner and the cable. Clicking it selects and focuses the partner.

**Power (including the Phase 3 gaps)**

8. Generator → shaft → A, then B → shaft → consumer, with **no shafts between A and B**. Link A→B with
   the tool. The consumer is powered.
   - Let it run until the consumer is **working**. Then unlink and relink, and check the log's `power …`
     line shows **demand > 0 hp**.
9. **Unlink split (deferred from Phase 3):** click the red ✕ on the cable button:
   - the cable disappears
   - **the consumer loses power**
   - the log shows `linked False, cable connected False/False, same network False`
   - Relink and power returns.
10. **Chain split (deferred from Phase 3):** A (generator) → B → C (consumer), linking A–B and then
    B–C. C's consumer is powered. **Demolish B**: C's consumer loses power, and both cables disappear.

**Cable blocks**

11. With a cable linked, try to place a building (e.g. a shaft on a platform, or a tall building) in a
    cell the cable passes through. Placement is refused.
12. Unlink that cable, and the same cell is buildable again.
13. A cable and a vanilla zipline can't cross, in either direction.
14. **Save/load:** cables are drawn again after load, power still flows, and cells are still blocked.
    Old test saves whose cables pass through buildings may log `cell(s) along the cable are occupied and
    were not reserved`. That's expected and fine.
15. **Unfinished:** link a finished station to one still under construction. The cable is drawn grey,
    and it turns normal colour and carries power once construction finishes.
16. **Log:** no exceptions and no `patch failed` / `could not be loaded` / `not found` errors from
    `[CablePowerTransfer]`.

Report: pass/fail for each step, screenshots of the panel, tool preview and cables (steps 1, 3, 4, 6),
and Player.log in the project folder.

**Result (2026-10-03): power steps passed** (user in-game check plus Player.log):
- Steps 1–7: the panel, tool and preview work (visual checks passed).
- Step 9: removing (83,58,5)↔(89,62,5) gave `linked False … same network False`. (89,62,5)'s side dropped from 50 hp
  to 0 hp and (83,58,5) kept 50 hp. Relinking restored `True/True, same network True`. It was repeated on
  (83,58,5)↔(85,64,5) at 278 hp: the far side dropped to 0 hp, and relinking restored it.
- Step 10: demolishing the middle station (89,62,5) of (85,64,5)–(89,62,5)–(83,58,5) logged both `unlinked` lines,
  and in-game the far side lost power. After a reload nothing about (89,62,5) was restored and there were no
  warnings.
- Steps 14 and 15: save/load restores cables, and cables to unbuilt stations wait (`3 waiting for a station to be built`).
- Not yet shown: step 8 `demand > 0 hp`. Every line still has demand 0, because no consumer was drawing power at the
  time.

*Phase 4 bug (2026-09-25): hovering a target whose cable crossed a zipline threw a NullReferenceException in
`Highlighter.HighlightPrimary`, called from `CablePreviewRenderer.Draw`. The cause was zipline cable blocks
(not highlightable) being passed as "blocking objects". They're now filtered out, and the tool has a
fail-safe that logs once and exits. Retest step 13: the cable turns red with `Line obstructed!` and there's
no error. Nothing is highlighted, because the zipline's blocks are invisible.*


*Phase 4 visual note (2026-09-25): the cables meet the placeholder pylon pole about one block below its
crossbar. This is expected. The cable anchor (`CableAnchorPoint` Y = 2.85) is set for the real 3-block
station, while the placeholder zipline pylon is 4 blocks tall. **Decision: leave it** until the real
model arrives in Phase 5, then set `CableAnchorPoint` to match the model's pulley top exactly.*

## Phase 5a: cable visuals (before the real station model)

Phase 4 results are still to come. You can test both in one run.

1. **Sag:** a linked cable is two strands, one each side of the tower top, forming a loop. Each strand
   sags slightly in the middle. Try short (≈5), medium (≈15) and long (≈28) cables, plus ones with a height
   difference. Long cables sag more, up to about ½ block. The curve should look smooth: 8 straight
   pieces per strand, which you can tune in `mod/Configurations/CableRenderer.blueprint.json` without
   rebuilding.
2. **Still misaligned with the placeholder:** strands meet the pylon pole about one block below its
   crossbar (decided: fixed with the real model).
3. **Powered motion:** with generator power flowing through the cable, the cable texture **moves**, and the
   two strands move in opposite directions. Unpower it (stop or remove the generator, or unlink) and within
   about half a second the cable is **still**. *This relies on the vanilla zipline cable shader animating
   when `_IsOperative` = 1. It hasn't been verified yet, so report what you see.*
4. **Placeholder spin:** when powered, the placeholder Clutch base may now animate, because the blueprint
   gained the vanilla `MechanicalNodeAnimatorSpec`. That's fine either way. The real pulley animation
   comes with the model.
5. **Level slider:** lower the visible-level slider below a station's height. Its cables cast shadows but
   aren't drawn. Raise it again and they're back.
6. **Preview:** the tool's green/red preview cable has the same sag.
7. **Highlights and greyscale:**
   - hovering a cable button in the panel highlights the whole cable, all pieces
   - a cable to an unfinished station is grey, and turns normal colour when construction finishes
8. **Performance:** with about 10 cables there's no noticeable frame drop.
9. **Log:** no `[CablePowerTransfer]` errors and no `CableRenderer blueprint not found` warning.

## Phase 5b: real station model

The placeholder Clutch and pylon are replaced by the generated model (`docs/station-model-spec.md`). You can
run this together with the Phase 4 and 5a lists.

1. **Loads:** the Power tab shows the new icon: gold line art in the same style as the other power icons (a tower with a
   wheel, a cable loop to a smaller station, and shaft arrows at the base). The log has no
   `Material ... not found in repository` or `Incorrect Zlib compression file header` errors.
2. **Looks:** a built station is a plank deck and gearbox, square shaft stubs on all four sides, a slender
   trestle, and a wooden pulley with yellow straps on top. Nothing sticks out of its 1×1 tile.
3. **Shaft line-up:** put shafts against each of the four sides. Their axles meet the station's stubs at the
   same height and size, with no visible step or gap. Take a screenshot.
4. **Cable fit:** linked cables enter the pulley's groove from both sides, between the two wooden rims, instead of
   meeting the pole below it (the Phase 4/5a misalignment should be gone). Check:
   - short and long cables, in several directions, including diagonals
   - cables to a higher and a lower station
   - a station with 3 cables
5. **Spin when powered:** with power flowing, the pulley (and its yellow straps), the vertical drive shaft and
   the four stubs turn. **Each stub turns the same way as the shaft connected to it** (retest below). Stubs with
   nothing attached follow the first connected one.
   The pulley's rim should move **the same way as the cable texture**, with cable running into the groove on one
   side and out on the other. If the cable and pulley visibly disagree, report it and I'll
   flip one sign.
6. **Still when unpowered:** stop the generator or unlink. Everything stops, and the stubs don't turn either.
   Slow power (a weak generator, efficiency < 100 %) turns it more slowly.
7. **Construction:** a placed but unbuilt station still shows the vanilla Clutch scaffold (expected for now),
   then switches to the new model when finished.
8. **Selection and highlight:** clicking the tower or pulley selects the station. The hover highlight outlines
   the whole model.
9. **Level slider:** lowering the slider through the station's height cuts the model like other buildings.

Report: pass/fail for each step, screenshots for steps 2, 3, 4 and 5, and Player.log.

## Rename: "Rope Power" → "Cable Power Transfer" (2026-09-28)

The mod Id is now `Elum.CablePowerTransfer`, deployed to `Mods/CablePowerTransfer/` (the old `Mods/RopePower/`
folder was removed). In-game wording, code and blueprints say "cable" instead of "rope". The save keys are
unchanged.

1. In the mod manager, **Cable Power Transfer** is listed (enable it if needed), and there's no Rope Power entry.
2. Load a save made before the rename. The game may warn that `Elum.RopePower` is missing; continue anyway.
   Stations and their links (now cables) come back, and power still flows.
3. Station panel header reads "Cables:". The tool prompt reads "Select another Power Transfer Station to connect
   with a cable".
4. The log uses the `[CablePowerTransfer]` prefix, with no spec or blueprint errors (for example
   `CableConnectionServiceSpec`, `CableRendererSpec`, `PowerCableBlock`).

## Input stubs match their shafts (2026-09-28)

Each input stub is now its own model and turns the same way as the shaft or generator on its side, using the same
rule vanilla shafts use (`docs/game-api-notes.md` §10).

1. A straight shaft line into one side: the stub and the shaft's axle turn together, with no visible reversal at
   the joint.
2. Shafts on all four sides, fed from different directions (for example one line from a generator, the others
   leading to consumers): every stub matches its own shaft, even if that means neighbouring stubs turn opposite ways.
3. Corners and junction shafts next to the station, and a generator placed directly against a stub (for example a
   Power Wheel output facing the station): the stub still matches.
4. After changing the network (adding or removing a shaft on the far side, which can flip a shaft line's direction),
   the stubs follow within a moment.
5. After save and load, the stubs are still right.
6. Unpowered: the stubs and pulley stop. Construction still shows the Clutch scaffold. Log: no `[CablePowerTransfer]`
   `found N of 4 input stubs` warning.

If a stub is consistently wrong in one situation, note the shaft shape next to it (straight, corner, T or cross) and
which side of the station it's on, with a screenshot.

## Science unlock (2026-09-28)

The station now costs **600 science** to unlock (`BuildingSpec.ScienceCost`, compared with 400 for the Clutch and
Gravity Battery).

1. In a new game, or a save where it isn't unlocked, the station's Power toolbar button shows as locked with a
   600 science cost, and it can't be placed until unlocked.
2. With 600 or more science, unlocking it deducts 600 and the building can then be placed.
3. Older test saves: the station may now show as locked (unlocks are saved per building). Already-built stations
   and their cables keep working. Unlock it (or use dev mode) to build more.

## Cable stops with the shafts (2026-10-03)

*Bug: the cable texture kept moving after the network's shafts had stopped. The cable used `ActiveAndPowered`, which
stays true while a battery holds charge even with no supply and no demand. It now uses the vanilla shaft rule
(`ActiveAndPowered` and `PowerEfficiency > 0`), the same as the pulley and stubs.*

1. A network with a generator, a cable and a gravity battery. Charge the battery, then stop the generator and switch
   off or pause every consumer. The shafts stop, and within about half a second the cable and pulley stop too.
2. Unpause a consumer, so the battery supplies power. The shafts, pulley and cable all move again.
3. A generator running with no consumers (supply above 0, demand 0): the shafts turn, so the cable moves too.

**Result (2026-10-03): passed** ("that worked").

## Cables to unfinished stations hidden outside construction mode (2026-10-03)

*Bug (ss-1.png): a cable to an unbuilt station was always drawn, hanging in mid-air above the construction site. Vanilla
ziplines only show such a cable in construction mode (`ZiplineCableRenderer` inactive connections). Cables now do the same.*

1. Link a finished station to a placed but unbuilt one. While the unbuilt station stays selected (the tool selects it
   after linking), the grey cable is visible.
2. Deselect it (Esc or click empty ground). The cable disappears.
3. Select the unbuilt station again, open the builder-priority or demolish tools, or start "Add connection" from any station. The grey
   cable shows again. Selecting a **finished** station on its own does not show it.
4. When the station is finished, the cable stays visible, in normal colour.
5. Save and reload with the station still unbuilt: the cable is hidden until one of the step 3 actions.

**Result (2026-10-03): passed.**

## Station cost doubled and load log fixed (2026-10-03)

1. **Cost:** the station's tooltip and construction site ask for **16 Log, 12 Plank, 8 Gear** (was 8/6/4). The 600
   science unlock is unchanged. Adding a cable is still free.
2. **Load log:** load a save with linked stations. Player.log now has no
   `power …` lines between `restored link` and `finished`. Instead, as soon as the game appears there is one
   `power …` line per cable whose stations are both built, then
   `after load: N cable link(s) between M station(s), K waiting for a station to be built`. Cables that carry power
   show `cable connected True/True, same network True`.

*First run (2026-10-03): every cable in the save touched an unbuilt station, so there were no `power` lines, and the
summary didn't say why. In the session before, the game never ticked (it stayed paused), so the log keyed on the
first tick never ran and linking with the tool logged no `power` lines either. Now the log runs on the first frame
(paused or not), the summary counts cables waiting for construction, and a station finishing logs its cables.*

3. **Finish logs power:** a cable to an unbuilt station logs its `power …` line once that station is finished.

## Station construction model (2026-10-03)

The unfinished station no longer borrows the Clutch scaffold. It now shows its own half-built model on the vanilla
construction base: the plank deck, the gearbox without its lid, and the four trestle posts up to about 1.4 blocks
with the first ring of rungs.

1. Place a station without finishing it. The construction site shows the half-built trestle described above, standing
   inside its tile. There's no pulley or shaft stubs yet.
2. Click the half-built posts: the construction site is selected (its collider now covers them).
3. When construction finishes, it switches to the full model.
4. Log: no `Material ... not found` or Timbermesh errors.

**Result (2026-10-03): visual checks passed** (cost, construction model, earlier visual lists).
