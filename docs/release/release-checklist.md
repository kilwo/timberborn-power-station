# Release checklist: Cable Power Transfer 1.0

## 1. Thumbnail

One image serves both stores.

- **File:** `mod/thumbnail.jpg` (or `.png`). The build deploys it to the mod folder. The in-game Steam uploader looks for
  `thumbnail.png`, `thumbnail.jpg` or `thumbnail.jpeg` in the mod's folder, in that order.
- **Size:** 1280 × 720 (16:9).
  - Steam (the in-game uploader's own rule): **under 1 MB**, 16:9 preferred. Save as JPG at about 85 % quality to stay
    under 1 MB. A 1280×720 PNG screenshot usually won't fit.
  - mod.io logo: png, jpg or gif, at least 512 × 288, at most 8 MB, 16:9 recommended. mod.io makes 320×180, 640×360 and
    1280×720 thumbnails from it ([mod.io docs](https://docs.mod.io/unityref/class_mod_i_o_1_1_mod_profile_details.html)).
- **Content:** a sunny overview of a cable chain doing real work. For example: a water wheel or wind turbines on one side,
  a station, then 2 or 3 cable spans over water or a valley to a powered workshop. Show at least one height difference
  so the sag reads.
  - Hide the game UI.
  - Keep it readable at 320 × 180, the smallest store tile. Make the stations and cables big in the frame, and avoid
    fine detail at the edges.
  - Optional: the mod name as large text in one corner. Leave the centre clear.
- Before uploading, open the in-game uploader once and press its refresh-thumbnail button to check it loads.

## 2. Gallery screenshots

Steam (the item page's images) and mod.io (the media gallery), 16:9, 1920 × 1080 or 1280 × 720:

1. **Hero:** the thumbnail scene without text.
2. **Station close-up:** a Folktails station with shafts on two or three sides and two cables arriving at the pulley.
3. **Linking:** the Add connection tool with a **green** preview and the distance/inclination tooltip.
4. **Validation:** a **red** preview with a reason (e.g. "Line obstructed!" with the blocking building highlighted).
5. **Station panel:** the "Cables:" list with 2 or 3 cables.
6. **Iron Teeth:** the Iron Teeth station (dark wood, blue straps) linked into an Iron Teeth base.
7. **Network:** a long chain or branch across the map (the 18-station test layout works) with a consumer's power panel
   showing supply from far away.
8. Optional: a station under construction, with its grey cable shown in construction mode.

## 3. Build and upload

1. Run `dotnet build src/CablePowerTransfer -c Release`. Check that `Documents/Timberborn/Mods/CablePowerTransfer/` has
   `manifest.json` (Version **1.0**), `CablePowerTransfer.dll`, the thumbnail and the data folders, and **no**
   `0Harmony.dll`. The Copy step doesn't delete old files, so remove any stray ones.
2. Start the game with only Harmony and Cable Power Transfer enabled. Load a save and make one link, then check
   Player.log for `[CablePowerTransfer]` errors.
3. **Steam Workshop:** use the main menu's mod manager upload button (the in-game uploader).
   - Name: `Cable Power Transfer`.
   - Description: paste `steam-workshop-description.txt`. The field is pre-filled with the manifest's short
     description; replace it.
   - Tags: **Update 1.1**, **New content**, **Buildings**.
   - Changelog: "1.0: first release."
   - Visibility: start as Hidden or Friends only, subscribe from a clean profile to test, then make it Public.
   - After uploading, on the Workshop page add **Harmony** under *Required items*. The in-game uploader doesn't set it.
   - For later updates, untick **Update description** unless you changed the text, so the full description isn't
     replaced.
4. **mod.io:** create the mod on the website. Use the summary, tags and description from `modio-description.md`, the
   thumbnail as the logo, and the gallery images. For the file, zip the deployed `CablePowerTransfer` folder. Check
   mod.io's Timberborn upload guidance for whether `manifest.json` must sit at the zip root or inside the folder. Add
   Harmony as a dependency if the page offers it.
5. Tag the release in git: `git tag v1.0`.

## 4. After release

- Test the Workshop copy: unsubscribe your dev copy (or disable the local `Mods/CablePowerTransfer` folder), subscribe
  to the published item, and load a save with stations.
- Watch the comments for conflicts with other power or mechanical mods (the Harmony patch is on
  `TransputMap.GetFacingTransput`).
