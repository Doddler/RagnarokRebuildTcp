# Ragnarok Copy Utility profiles

Import a selected subset of Ragnarok resources without deleting assets that are already present.

1. Extract an original Ragnarok `data.grf` with Korean filenames preserved.
2. Run `updateclient.bat` from the repository root.
3. Open `RebuildClient` in Unity.
4. Select **Ragnarok > Copy data from Profile**.
5. Click **Locate Data folder** and find your Ragnarok data folder.
6. Choose a profile, then select **Import Profile**.
7. Optional: in the Lighting Manager window, bake the imported maps if you need baked lighting.
8. Optional: create the minimap for the imported maps.
9. Start the server. For Minimum, use the **Minimal** launch profile: `./run-server.cmd Minimal`.
10. In Unity, select **Ragnarok > Open Main Scene**, enter Play mode, and connect normally.

Minimum imports `prt_fild08` and Novice resources. Medium adds every first job. Full performs the exhaustive client-data import and can take more than an hour. The Minimal server intentionally loads only `prt_fild08`.

## Custom profiles

Profiles live in `RebuildClient/Assets/StreamingAssets/ProjectConfig`. Add another `.json` file there and reopen or refresh the profile window.

```json
{
  "name": "My Profile",
  "description": "Resources used by my feature.",
  "all": false,
  "resources": {
    "maps": ["prt_fild08"],
    "items": [501],
    "jobs": ["Novice"],
    "monsters": ["PORING"],
    "npcs": [],
    "effects": ["RedPotion"],
    "skills": [1]
  }
}
```

Use map codes, monster codes, job/effect names, and numeric item/NPC/skill IDs from the generated client configuration. Maps automatically add their monsters and NPCs; monsters add drops; NPCs add vendor items; jobs add inherited skills and equipment; effects add sounds. Every non-full profile also receives cursors, emotions, damage numbers, and a default hairstyle.

Set `"all": true` for an exhaustive profile. Resource lists are ignored in that mode. Applying a smaller profile after a larger one keeps the previously imported assets.
