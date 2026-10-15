---
kind: game
title: "No Man's Starfield: Seamless Interplanetary Space Cruise, 150km Atmospheric Entry Autopilot & Steam Deck Performance Overhaul"
game: "Starfield"
games_also: ["No Man's Sky"]
game_version: "Creation Engine 2 (fver=581 Starfield.esm / fver=582 SFBGS00D.esm, SteamOS 3.x Proton & Windows x64)"
platform: linux
engine: creation
route: native-hook
tools: ["Python 3 Light Master ESM Builder (tools/build_esm.py)", "Python 3 PE64 Proxy DLL Compiler (tools/build_proxy_dll.py)", "GNU binutils objdump", "OpenSSH / SCP to Steam Deck"]
anti_cheat: "Singleplayer only; combines a Light Master .esm (fver=582) with a Bink2 PE export-forwarding proxy DLL (bink2w64.dll -> bink2w64_orig.dll)"
status: working
agents: ["Antigravity (Gemini)"]
humans: ["@ronaldtbriggs-blip"]
date: 2026-10-15
links:
  - https://github.com/ronaldtbriggs-blip/universal-modder
tags: [starfield, no-mans-sky, steam-deck, proton, creation-engine-2, space-cruise, atmospheric-entry, planetary-landing, esm, bink2w64-proxy, performance, graphics]
---

# No Man's Starfield: Seamless Interplanetary Space Cruise, 150km Atmospheric Entry Autopilot & Steam Deck Performance Overhaul

This field note documents the reverse engineering of *Starfield*'s internal `SpaceCruise` (`SFBGS00D.esm` + `Starfield.exe`) and animated planetary landing (`LandOnPlanetAnimated`) subsystems, and details the architecture of **No Man's Starfield** (`examples/starfield-nomanssky-overhaul`), an overhaul for the **Steam Deck (`1280x800`, `AMD Custom GPU 0405`)** that fuses *No Man's Sky*'s best space-to-planet mechanics into *Starfield*:
1. **Seamless Interplanetary Pulse-Drive Flight**: Fly freely between planets and moons within the same star system in 6–14 seconds with zero magnetic orbital deflection.
2. **Fly-Into-Planet Atmospheric Entry & Automatic Landing**: Approach planets down to their `150 km` upper atmosphere and automatically transition into animated landing on their procedural terrain without opening the Starmap UI or clicking "Land".
3. **No Man's Sky Surface Exploration Mashup**: Long-range Analysis Visor scanning (`75m` base range, `3`-sample biome survey completion), denser procedural worldspace/POI generation (`fPlanetContentMaxNewWorldspacesPerScan = 10.0`, `uPlanetContentMaxRequestsPerBlock = 10`), sustained press-and-hold Boostpack flight, and unrestricted Corvette ship/outpost building.
4. **Graphically Simpler, Buttery-Smooth Steam Deck Rendering Profile**: Repairs the corrupted `uiUpscaleTech=4294967295` (`0xFFFFFFFF`) setting in `StarfieldPrefs.ini`, registers the Steam Deck APU (`AMD Custom GPU 0405`) in `DefaultGlobalGraphicsSettings.json`, activates Creation Engine 2's built-in `ToggleTrijuice on` shader optimizer, and strips heavy volumetric/shadow/post-FX passes for smooth framerates during high-speed space flight and procedural terrain exploration.

---

## Setup

- **Target Game**: *Starfield* (AppID `1716740`, `Starfield.exe` `102,476,200` bytes, `Starfield.esm` `fver=581`, `SFBGS00D.esm` `fver=582`).
- **Reference Game Inspected on Target Device**: *No Man's Sky* (`~/.local/share/Steam/steamapps/common/No Man's Sky`).
- **Target Hardware / OS**: Valve Steam Deck (`AMD Custom GPU 0405` RADV VANGOGH APU, `1280x800`, SteamOS 3.x / Proton `compatdata/1716740`).
- **Paths on Steam Deck**:
  - Game root: `~/.local/share/Steam/steamapps/common/Starfield/`
  - Data directory: `~/.local/share/Steam/steamapps/common/Starfield/Data/`
  - Proton config directory: `~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/Documents/My Games/Starfield/`
  - Proton plugin list: `~/.local/share/Steam/steamapps/compatdata/1716740/pfx/drive_c/users/steamuser/AppData/Local/Starfield/Plugins.txt`

---

## Route

### 1. Unlocking & Re-Tuning `SpaceCruise` (`SFBGS00D.esm` + `NoMansStarfield.esm`)
Inspecting `SFBGS00D.esm` (`fver=582`) and `Starfield.exe` revealed an internal multi-stage interplanetary sublight engine (`SpaceCruise::OutsidePlanetOrbitalCruise` $\rightarrow$ `SpaceCruise::PlanetAndMoonCruise` $\rightarrow$ `SpaceCruise::NearPlanetCruise` $\rightarrow$ `SpaceCruise::NearOrbitalCruise`) with two vanilla bottlenecks:
1. **Tutorial Input Lock**: Quest `SFBGS00D_CruiseMode_Tutorial` (FormID `0x0107DCFA`, Papyrus script `sfbgs00d_cruisemode_tutorial_script.pex`) creates an `InputEnableLayer` (`CruiseEnableLayer`) that disables Cruise Mode input until `MQ101` stage `1310` is completed. Executing `SetStage 0107DCFA 100` (and `SetStage 010920A6 1000` on `SFBGS00D_CruiseMode_TestS`) invokes `EnableCruiseModeAccess()` and unlocks Cruise Mode immediately on any save.
2. **10,000 km Orbital Wall & Magnetic Deflection**: In `SFBGS00D.esm`, `fCruiseStageNearOrbitalDistanceKM` (`0x0106D619`) and `fCruiseStageNearOrbitalDistanceToOrbitalKM` (`0x010924AD`) are set to `10000.0` km, while `fCruiseCelestialMagnetismDeflectionMult` (`0x010950CA` = `3.0`) and `fCruiseMagnetismSpeedClampMult` (`0x010927DA` = `0.1`) push the ship away from planets and clamp speed to 10%.

We built `tools/build_esm.py` to generate **`NoMansStarfield.esm`** (`4,041` bytes, `TES4` flags `0x00000101` = Light Master, `fver=582`, masters `Starfield.esm` and `SFBGS00D.esm`), overriding 50 verified `GMST` FormIDs:
- **150 km Atmospheric Entry Envelope**: `fCruiseStageNearOrbitalDistanceKM` (`0x0106D619`) = `150.0`, `fCruiseStageNearOrbitalDistanceToOrbitalKM` (`0x010924AD`) = `150.0`, `fCruisePOIArrivalThresholdKM` (`0x0106D611`) = `150.0`, `fCruiseStageNearPlanetDistanceKM` (`0x0106D613`) = `12000.0`.
- **Zero Magnetic Deflection**: `fCruiseCelestialMagnetismDeflectionMult` (`0x010950CA`) = `0.0`, `fCruisePlanetMagnetismDeflectionOrbitAltitudeMult` (`0x010950CB`) = `0.01`, `fCruiseMagnetismSpeedClampMult` (`0x010927DA`) = `0.90`.
- **Pulse-Jump Travel Speed**: `fMinSpaceCruiseOrbitalTimeToReachSeconds` (`0x0106D617`) = `6.0`, `fMaxSpaceCruiseOrbitalTimeToReachSeconds` (`0x0106D618`) = `14.0`, `fCruisePlanetAndMoonAccelerationMult` (`0x01089C2D`) = `950000.0`.
- **Analysis Visor & Procedural Worlds**: `fHandScannerScanRange` (`0x000DF135`) = `75.0`, `iHandScannerPlantsCountBase` (`0x00249B84`) = `3`, `iHandScannerAnimalCountBase` (`0x0038D142`) = `3`, `fPlanetContentMaxNewWorldspacesPerScan` (`0x002BCA26`) = `10.0`, `uPlanetContentMaxRequestsPerBlock` (`0x001627F4`) = `10`, `uPlanetContentMaxWorldspaceRequestsPoints` (`0x0039100B`) = `30`.

### 2. Native x64 Atmospheric Entry Autopilot (`bink2w64.dll` Proxy Hook)
Disassembling `Starfield.exe` revealed the exact two-stage state machine for Cruise Arrival and Animated Planetary Landing:
- **Stage 1 (`0x1413F9F70` — `Cruise Mode Arrival Initiated`)**: When the player's ship crosses `fCruiseStageNearOrbitalDistanceKM` (`150 km`), `0x1413FA016` copies the target planet's Celestial Body ID from `[base + 0x5EB04B4]` into `[base + 0x5EB0714]`, checks `BYTE PTR [base + 0x5EAAA97]` (`1` = Planet/Moon arrival, `0` = Space Station/POI arrival), and calls `0x141400E70` to transition the ship into that planet's orbital space.
- **Stage 2 (`0x1413FFD00` — `SpaceCruise::OnOrbitalArrivalComplete`)**: Registered via the event listener vtables (`0x144C52A70`..`0x144C52AB8`), `0x1413FFD00` executes on the **Main Game Thread** as soon as the orbital transition finishes, reaching its epilogue at `0x1413FFEF0` while `[base + 0x5EB0714]` (`planetBodyID`) and `[base + 0x5EAAA97]` (`isPlanetArrival`) are still populated.
- **Animated Landing Sequence (`LandOnPlanetAnimated` at `0x140C7BD20`..`0x140C7BF5F`)**: At `0x140C7BEE1`, the engine calls `0x1417DEC70(&outResult, planetBodyID, &lonLat[0], 1, 0, 1)`. If `outResult != *(uint32_t*)(base + 0x61FAEB0)` (valid landable procedural terrain found), `0x140C7BF2F` sets `BYTE PTR [base + 0x5ED40C5] = 1` and calls `0x141897ED0(1)` to initiate the animated landing sequence onto the planet.

We created `tools/build_proxy_dll.py` to generate a self-contained `8,192`-byte 64-bit Windows PE proxy `bink2w64.dll` that:
1. Forwards all 79 Bink2 exports to `bink2w64_orig.<ExportName>` in the PE Export Directory (`IMAGE_EXPORT_DIRECTORY`).
2. On `DLL_PROCESS_ATTACH`, verifies the instruction signature at `base + 0x13FFEF0` (`0x0510FAC5`) and installs a 14-byte jump to `AtmosphereEntryTrampoline`.
3. When `0x1413FFD00` finishes on the Main Thread after arriving at a planet (`[base + 0x5EAAA97] != 0`), clears `[base + 0x5EAAA97] = 0`, loops over 6 candidate `(lon, lat)` procedural surface coordinates via `0x1417DEC70` (automatically bypassing ocean tiles and leaving the ship in orbit if the target is an unlandable gas giant), and calls `0x141897ED0(1)` to seamlessly land the ship on the procedural surface.

### 3. Simplified Steam Deck Graphics & Upscaler Fix
Inspecting the Steam Deck's `StarfieldPrefs.ini` and `DefaultGlobalGraphicsSettings.json` uncovered why *Starfield* suffered severe frame drops:
- `uiUpscaleTech=4294967295` (`0xFFFFFFFF` / `-1` unsigned int overflow) was written into `StarfieldPrefs.ini`, alongside `uGlobalRendererQuality=1`, while `DefaultGlobalGraphicsSettings.json` lacked any entry for `"AMD Custom GPU 0405"` (the Steam Deck RADV VANGOGH APU).
- Fixing `uiUpscaleTech=1` (FSR2 at `0.62` render resolution scale with `0.50`–`0.75` dynamic resolution at `1280x800`), registering `"AMD Custom GPU 0405": 0` in `DefaultGlobalGraphicsSettings.json`, running `ToggleTrijuice on` via `sStartingConsoleCommand=bat nms_startup`, and disabling film grain, motion blur, depth of field, and contact shadows produces a clean, stylized visual presentation with smooth frame pacing across spaceflight and procedural planetary terrain.

---

## Verification

- **ESM Binary Structure Verification**: Built `Data/NoMansStarfield.esm` (`4,041` bytes, 50 `GMST` overrides) and verified `TES4` (`fver=582`, `flags=0x00000101`) and `GRUP` (`GMST`, `3,717` bytes) headers against `SFBGS00D.esm` on the Steam Deck.
- **PE64 Proxy DLL Verification**: Verified `bin/bink2w64.dll` (`8,192` bytes) on the Steam Deck with `objdump -x -d -M intel`, confirming all 79 `bink2w64_orig.*` export forwarders, the `Ordinal/Name Pointer` table, `KERNEL32.dll` imports (`GetModuleHandleA`, `VirtualProtect`), and the x64 disassembly of `DllMain` (`0x180001000`) and `AtmosphereEntryTrampoline` (`0x1800010C0`).
- **Steam Deck Deployment**: Backed up original `bink2w64.dll`, `DefaultGlobalGraphicsSettings.json`, and `StarfieldPrefs.ini` to `~/starfield_backup_20261015/` and deployed `NoMansStarfield.esm`, `bink2w64.dll`, `bink2w64_orig.dll`, `nms_startup.txt`, `land.txt`, `StarfieldCustom.ini`, `StarfieldPrefs.ini`, and `Plugins.txt`.

---

## Gotchas

1. **`IMAGE_EXPORT_DIRECTORY` Field Widths**: `MajorVersion` (`offset 0x08`) and `MinorVersion` (`offset 0x0A`) in `IMAGE_EXPORT_DIRECTORY` are two 16-bit `uint16` fields (`<IIHHIIIIIII`), not a single `uint32` with trailing `uint16` padding. Misaligning `MajorVersion`/`MinorVersion` shifts `Name` and `AddressOfNames` by 4 bytes and causes `objdump -x` / `ntdll` to reject the Export Name Pointer Table.
2. **Thread-Local Storage (`gs:0x58`) in Creation Engine 2 Landing Functions**: `LandOnPlanetAnimated` (`0x140C7BD20`) and `LandOnPlanetBiome` (`0x140C7BF60`) check Thread-Local Storage (`gs:0x58`) and the main-thread job queue (`0x1423FE260`). Invoking `0x1417DEC70` and `0x141897ED0(1)` from a background `CreateThread` worker risks TLS assertions; hooking the epilogue of `SpaceCruise::OnOrbitalArrivalComplete` (`0x1413FFEF0`) executes synchronously on the Main Game Thread right after orbital transition completes.
3. **Gas Giants & Ocean Equatorial Tiles**: Calling `0x1417DEC70` at a single fixed `(0.0, 0.0)` coordinate returns the invalid sentinel (`*(uint32_t*)(base + 0x61FAEB0)`) if `(0.0, 0.0)` is an ocean tile or if the celestial body is a gas giant. Testing a small table of distributed `(lon, lat)` coordinates and only calling `0x141897ED0(1)` when `outResult != sentinel` handles ocean worlds and gas giants safely.
