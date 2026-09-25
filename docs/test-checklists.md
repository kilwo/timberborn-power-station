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
