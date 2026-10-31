# Combat visual integration

Implemented in `Assets/Scenes/FPS.unity`, the player prefab, and the Ranged, Melee and Dodge enemy prefabs.

## Assets and actions

- Existing tt-3d Western rifle replaces the visible ProBuilder gun for the player and every enemy. Muzzle flash and tracer origins follow its muzzle.
- Sheriff player body, gunman enemies, and outlow Dodge enemy use their existing Humanoid avatars. Character materials have separate URP copies; imported originals are preserved.
- Kevin Iglesias rifle aim/fire, idle, eight-direction walk/run, hit and nonlooping death clips drive `WesternRifle.controller`. Movement remains controlled by the existing FPS controller; animation root motion is disabled.
- Jump/airborne knees and landing dip are procedural because the supplied animation library contains no jump clips. Sprint playback rate follows actual movement speed.
- WRAD forearms hold the first-person rifle with fitted hand/finger poses. A derived forearm mesh removes shoulder geometry that crossed the camera; the imported model is unchanged. Recoil, movement, sprint, airborne/landing and damage responses are cosmetic.
- Reload keeps the existing UI and weapon timing. Hands/rifle lower out of view and return after reload completes. No detailed reload clip or mechanical gun animation plays. Weapon stats retain their existing values, including assets originally named revolver.
- The local world body casts shadows in first person and becomes visible during the result camera pan. First-person hands hide on death/results.

## Environment

PolyRonin barrels, crates, buildings, cacti and rocks dress the arena. Earth/sandstone rendering replaces the grid on a derived copy of the original arena surface. All existing scene collider components and navigation geometry are retained. This is scenery dressing around the existing arena; the cover and walls still follow its prototype shapes. A full authored town/interior layout remains separate work.

## Verification

Unity compiled successfully. [Play validation](play-validation.txt) passed 17 checks covering actual shot events/ammo, reload lowering/return, last-round firing and automatic reload, movement/sprint, airborne/landing, hit/death and enemy prefab setup. Existing FPS scene UI transforms/canvases (13 components) and colliders (8 components) were compared with the original scene and are unchanged.

Visual previews were inspected for character pose, materials, rifle direction, hand fit, shoulder clipping and scenery placement. The first-person reload UI code and layout were not edited.

Open the FPS scene and use Play to inspect the complete duel. Editor tools under **Tools > Combat Visuals** can validate the setup, run the isolated Play check, render previews, or reinstall generated visuals. Save scene changes before running the Play check. **Install Rifle and Character Animations** recreates generated visual roots, materials/controller and environment dressing; it will replace manual edits inside those generated roots. It has no automatic startup behavior.

Runtime drivers: `Assets/Scripts/Visuals/`. Generated controller, materials and meshes: `Assets/visuals/Combat/`. Preview/test output: `Temp/AssetIntegration/`.
