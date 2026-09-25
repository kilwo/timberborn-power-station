# In-game test checklists

Build and deploy: run `dotnet build src/RopePower -c Release`. This copies `mod/**` and `RopePower.dll` to
`Documents/Timberborn/Mods/RopePower/`. Logs are in
`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`.

## Phase 0: skeleton loads

1. Start Timberborn. In the mod manager, **Rope Power** (v0.0.1) is listed; enable it.
2. Load any Folktails save, or start a new game.
3. Close the game and search `Player.log` for `[RopePower] loaded`. It should appear exactly once per
   game load.
4. Check there are no exceptions mentioning `RopePower` in `Player.log`.

Report: pass/fail for each step, plus any log lines containing `RopePower`.

**Result (2026-09-25): passed.** `[RopePower] loaded` appeared in Player.log.

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
9. **Rope slots (cosmetic check):** select the station. Report any odd power markers or arrows inside or
   on top of the tower. The station has 3 hidden rope connection points there.
10. **Log:** check Player.log for exceptions or warnings that mention `PowerTransferStation`,
    `RopePower`, `Timbermesh` or `Transput`.

Report: pass/fail for each step, a screenshot of steps 2 and 9 if anything looks off, and any relevant log
lines.

**Result (2026-09-25): passed** ("looks good").

## Phase 2: persistent links (no UI, no power merge yet)

Links are **data only** in this phase. They don't carry power yet (that's Phase 3), there's no rope
visual, and no clearance or rope-block check. Everything is verified through `Player.log` lines starting
with `[RopePower]`.

Setup: a Folktails game. The shortcuts below don't need dev mode. To build quickly, turn on dev mode
(**Alt+Shift+Z**, which logs `Dev mode enabled`) and **hold Ctrl while placing** a station to place it already
finished (vanilla `PlaceFinished`). "Newest" means most recently
**finished**; unfinished stations don't count.

Dev actions: each result appears as an on-screen notification starting "Rope Power:" and is also logged.

| Shortcut (works with or without dev mode) | Action |
|---|---|
| **Ctrl+Alt+L** | link two newest stations |
| **Ctrl+Alt+J** | link newest station to all others |
| **Ctrl+Alt+U** | unlink newest station |
| **Ctrl+Alt+K** | log all rope links (count on screen, pairs in Player.log) |

The same actions are in the dev panel. It appears **bottom-left** while dev mode is on and is collapsed
by default: click its title, then type "rope" in the filter box.

*First attempt (2026-09-25): the user found no way to trigger linking. The log showed dev mode enabled
but no Rope Power action invoked, because the panel is collapsed and wasn't found. The shortcuts and
notifications were added as a fix.*

*Second attempt (2026-09-25): the keys didn't work. The log had no `Dev mode enabled`, so the
`DevModeOnly` bindings were blocked by `DevModeKeyBindingBlocker`. The keys now work without dev mode.
On the first frame after loading a save, the log confirms
`[RopePower] dev: debug keys active …` (or warns `key bindings not found`). Each finished station also
logs `station (x, y, z) finished`.*

1. **Load:** the log shows `[RopePower] loaded (max span 30, max ropes 3, max inclination 50)` and **not**
   `RopeConnectionService blueprint not found`.
2. **Link:** build station A, then station B about 10 blocks away. Run *link two newest* and the log
   shows `linked station (…) <-> station (…) (span …)`.
3. **Inspect:** *log all rope links* shows `2 station(s), 1 rope link(s)` and the pair.
4. **Rejections:**
   - Run *link two newest* again and you get `rejected: AlreadyLinked`.
   - Build C more than 30 blocks from B, then run *link two newest*. You get `rejected: TooLong`.
   - Optional: build a station very close to another but much higher (e.g. on a cliff 4+ blocks up,
     1 block away). *link two newest* gives `rejected: TooSteep`.
5. **Max ropes:** build 4 more stations within 30 blocks of each other, all finishing after A and B.
   Run *link newest station to all others*. The newest one links to 3 and then logs `rejected: SourceFull`.
6. **Save/load:** save and reload.
   - Each link shows exactly **one** `restored link … <-> …` line.
   - *log all rope links* shows the same pairs as before saving.
   - There are no `dropped` warnings.
7. **Demolish:** demolish one linked station. The log shows `unlinked …` for each of its links, and
   *log all rope links* no longer lists them. Save, reload: nothing about it is restored and there are no
   warnings.
8. **Unlink:** run *unlink newest station* and the newest station's links are removed (`unlinked …`).
9. **District (optional, needs two districts):** put station X beside a road of district 1 and station Y
   beside a road of district 2, within 30 blocks. *link two newest* gives `rejected: DifferentDistricts`.
   A station with no road next to it links to either.
10. **Log:** no exceptions mentioning `RopePower` or `PowerTransferStation`.

Report: pass/fail for each step, plus the `[RopePower]` lines from steps 6 and 7.

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

The ropes should now carry power. There's still no rope visual. Use the Ctrl+Alt keys from Phase 2 (no
dev mode needed). **Ctrl+Alt+K** now logs each link as
`rope connected: True/True | same network: True | network supply X hp, demand Y hp`.

**Setup:** use a clean area or a new Folktails game. **Demolish the old test stations** so "newest" is
unambiguous.
- Generator side: a generator (e.g. a Water Wheel in a current, or a Power Wheel) → shaft → **station A**.
- Consumer side: **station B** 10–25 blocks away with **no shafts between A and B** → shaft → a consumer
  (e.g. a Lumber Mill or Gear Workshop, staffed).
- Finish A first, then B. In dev mode, Ctrl-placing places buildings finished.

1. **Patch loaded:** Player.log shows `[RopePower] Harmony patches applied` (at the main menu, before the
   save loads). There's no `patching failed` line.
2. **Before linking:** the consumer shows no power.
3. **Link:** press **Ctrl+Alt+L**. The consumer becomes powered within a moment. Selecting a shaft on
   B's side shows the generator's supply. Ctrl+Alt+K shows
   `rope connected: True/True | same network: True`.
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
- Step 3: A (83,58,5) and B (85,64,5), 6.3 apart, showed `rope connected: True/True | same network: True
  | network supply 79 hp`.
- Step 5: the link was restored after save/load.
- Step 6: B was demolished (unlinked), rebuilt and relinked.
- Step 7: A was demolished, rebuilt and relinked.
- A third station C (89,62,5) was linked to **both** A and B with Ctrl+Alt+J. That made a loop of ropes,
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
