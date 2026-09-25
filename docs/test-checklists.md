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
