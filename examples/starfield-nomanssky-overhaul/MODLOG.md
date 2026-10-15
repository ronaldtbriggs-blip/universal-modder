# MODLOG — No Man's Starfield: Seamless Interplanetary Flight, Atmospheric Entry & Steam Deck Performance Overhaul

## Target Game & Environment
- **Game**: *Starfield* (Steam AppID `1716740`, Creation Engine 2, binary `Starfield.exe` + `Starfield.esm` `fver=581` + `SFBGS00D.esm` `fver=582`)
- **Inspiration Source**: *No Man's Sky* (installed alongside *Starfield* on the target Steam Deck)
- **Target Hardware / OS**: Steam Deck (SteamOS 3 / Proton `compatdata/1716740`, `AMD Custom GPU 0405` RADV VANGOGH APU, `1280x800`)
- **Install Root**: `~/.local/share/Steam/steamapps/common/Starfield`
- **Proton Config Root**: `~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/Documents/My Games/Starfield`
- **Proton Plugin List Root**: `~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/AppData/Local/Starfield`

---

## Backup & Rollback Record
Before modifying any files on the Steam Deck, all original files are backed up to `~/starfield_backup_20261015/`:
- `~/.local/share/Steam/steamapps/common/Starfield/bink2w64.dll` $\rightarrow$ copied to both `~/starfield_backup_20261015/bink2w64.dll` and `~/.local/share/Steam/steamapps/common/Starfield/bink2w64_orig.dll`
- `~/.local/share/Steam/steamapps/common/Starfield/DefaultGlobalGraphicsSettings.json` $\rightarrow$ `~/starfield_backup_20261015/DefaultGlobalGraphicsSettings.json`
- `~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/Documents/My Games/Starfield/StarfieldPrefs.ini` $\rightarrow$ `~/starfield_backup_20261015/StarfieldPrefs.ini`

**Rollback Command**:
```bash
cp ~/starfield_backup_20261015/bink2w64.dll ~/.local/share/Steam/steamapps/common/Starfield/bink2w64.dll
cp ~/starfield_backup_20261015/DefaultGlobalGraphicsSettings.json ~/.local/share/Steam/steamapps/common/Starfield/DefaultGlobalGraphicsSettings.json
cp ~/starfield_backup_20261015/StarfieldPrefs.ini "~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/Documents/My Games/Starfield/StarfieldPrefs.ini"
rm -f ~/.local/share/Steam/steamapps/common/Starfield/Data/NoMansStarfield.esm \
      ~/.local/share/Steam/steamapps/common/Starfield/bink2w64_orig.dll \
      ~/.local/share/Steam/steamapps/common/Starfield/nms_startup.txt \
      ~/.local/share/Steam/steamapps/common/Starfield/land.txt \
      "~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/Documents/My Games/Starfield/StarfieldCustom.ini" \
      "~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/AppData/Local/Starfield/Plugins.txt"
```

---

## Reverse-Engineering Findings & Overhaul Architecture

### 1. Seamless Interplanetary Pulse-Drive Flight (`NoMansStarfield.esm` + `SFBGS00D.esm`)
- **Discovery**: *Starfield* includes an internal multi-stage sublight interplanetary flight subsystem (`SpaceCruise` in `Starfield.exe` + `SFBGS00D.esm`), gated behind tutorial quest `SFBGS00D_CruiseMode_Tutorial` (`0x0107DCFA`, script `sfbgs00d_cruisemode_tutorial_script.pex`) which locks out player input via `CruiseEnableLayer` until `MQ101` stage `1310`.
- **Why Vanilla Cruise Stops You in Deep Space**:
  - `fCruiseStageNearOrbitalDistanceKM` (`0x0106D619`) and `fCruiseStageNearOrbitalDistanceToOrbitalKM` (`0x010924AD`) are set to `10000.0` km, halting the ship far outside the planet.
  - `fCruiseCelestialMagnetismDeflectionMult` (`0x010950CA` = `3.0`) and `fCruiseMagnetismSpeedClampMult` (`0x010927DA` = `0.1`) create an invisible magnetic deflection barrier that pushes the player's ship away from planets and clamps speed to 10%.
- **Mod Solution**:
  - `NoMansStarfield.esm` (a Light Master `.esm`, `fver=582`) overrides 50 `GMST` records across `Starfield.esm` and `SFBGS00D.esm`:
    - Shrinks the orbital arrival envelope from `10,000 km` down to `150 km` (`fCruiseStageNearOrbitalDistanceKM = 150.0`, `fCruiseStageNearOrbitalDistanceToOrbitalKM = 150.0`, `fCruisePOIArrivalThresholdKM = 150.0`) right at the planet's upper atmosphere.
    - Eliminates magnetic deflection (`fCruiseCelestialMagnetismDeflectionMult = 0.0`, `fCruiseMagnetismSpeedClampMult = 0.90`).
    - Tunes interplanetary travel times (`fMinSpaceCruiseOrbitalTimeToReachSeconds = 6.0`, `fMaxSpaceCruiseOrbitalTimeToReachSeconds = 14.0`, `fCruisePlanetAndMoonAccelerationMult = 950000.0`) to match *No Man's Sky*'s Pulse Jump feel.
  - `nms_startup.txt` runs `SetStage 0107DCFA 100` and `SetStage 010920A6 1000` on startup to unlock Space Cruise immediately on any save.

### 2. Fly-Into-Planet Atmospheric Entry Autopilot (`bink2w64.dll` Native x64 Hook)
- **Discovery**:
  - When the player's ship reaches `fCruiseStageNearOrbitalDistanceKM` (`150 km`), `Starfield.exe` invokes `SpaceCruise::OnArrivalInitiated` (`0x1413F9F70`), which copies the target planet's Celestial Body ID (`[base + 0x5EB04B4]`) into `[base + 0x5EB0714]`, checks `[base + 0x5EAAA97]` (`1` = Planet/Moon arrival, `0` = Space Station/POI), and schedules the orbital arrival transition (`0x141400E70`).
  - When the orbital arrival transition completes on the Main Thread, `SpaceCruise::OnOrbitalArrivalComplete` (`0x1413FFD00`) runs and returns at `0x1413FFEF0`.
  - Meanwhile, *Starfield*'s native animated planetary landing handler (`LandOnPlanetAnimated` at `0x140C7BD20`) calls `0x1417DEC70(&outResult, planetBodyID, &lonLat[0], 1, 0, 1)` to resolve a procedural terrain landing site on `planetBodyID`, sets `BYTE PTR [base + 0x5ED40C5] = 1`, and calls `0x141897ED0(1)` to trigger the animated landing sequence.
- **Mod Solution**:
  - `tools/build_proxy_dll.py` generates a dependency-free 64-bit PE proxy `bink2w64.dll` that forwards all 79 Bink2 exports to `bink2w64_orig.dll` and hooks `0x1413FFEF0` (`SpaceCruise::OnOrbitalArrivalComplete` epilogue).
  - When the player flies into a planet's `150 km` upper atmosphere in Cruise Mode (`[base + 0x5EAAA97] != 0`), the Main-Thread trampoline automatically queries up to 6 candidate `(lon, lat)` procedural surface coordinates via `0x1417DEC70` (gracefully skipping ocean tiles and leaving the ship in orbit if the target is an unlandable gas giant) and immediately invokes `0x141897ED0(1)` to land on the planet's procedural surface without opening the Starmap UI.

### 3. No Man's Sky Exploration, Procedural Worlds, Visor & Ship/Base Freedom
- **Analysis Visor Scanning**: `fHandScannerScanRange` increased from `10m` to `75m`; flora/fauna scans required per biome reduced from `8` to `3` (`iHandScannerPlantsCountBase = 3`, `iHandScannerAnimalCountBase = 3`).
- **Procedural World Streaming**: `fPlanetContentMaxNewWorldspacesPerScan = 10.0`, `uPlanetContentMaxRequestsPerBlock = 10`, `uPlanetContentMaxWorldspaceRequestsPoints = 30`, and `uProcGenTimeSlice = 4` for rich, stutter-free procedural surface generation as you approach and explore planets.
- **Jetpack / Boostpack & Traversal**: `fJumpHeightMin = 2.25`, `fSprintOxygenDrainMult = 2.5`, `fJumpFallDistanceToDamageMult = 0.20`, and `bUsePressAndHoldControls = 1` for continuous *No Man's Sky* jetpack flight.
- **Corvette Ship Builder & Outpost Freedom**: `bShipBuilderEnableLooseRules = 1`, `fShipBuilderOverlapTolerance = -2.0`, `uSpaceshipBuilderMaxModules = 300`, `fSpaceshipMaxDockingDistance = 2500.0`, and `fWorkshopBuildHeight = 300.0`.

### 4. Graphically Simpler, Buttery-Smooth Steam Deck Rendering Profile
- **Root Cause of Steam Deck Stutter/Crashes Fixed**:
  - Repaired corrupted `uiUpscaleTech=4294967295` (`0xFFFFFFFF`) in `StarfieldPrefs.ini` $\rightarrow$ set to `uiUpscaleTech=1` (FSR2 at `0.62` render scale with `0.50`–`0.75` dynamic resolution for `1280x800`).
  - Registered `"AMD Custom GPU 0405"` (Steam Deck RADV VANGOGH APU) in `DefaultGlobalGraphicsSettings.json`.
- **Stylized Clean Visual Profile**:
  - Enables Creation Engine 2's built-in `ToggleTrijuice on` shader optimizer.
  - Strips film grain, motion blur, depth-of-field blur, contact shadows, expensive indirect lighting, and high-res shadow cascades while keeping clean terrain/flora draw distances (`fGrassStartFadeDistance = 3500.0`, `fShadowDistance = 3500.0`) so procedural planetary surfaces look crisp and run smoothly on the Steam Deck.
