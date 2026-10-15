# No Man's Starfield: Seamless Pulse-Drive Flight, Atmospheric Entry & Steam Deck Performance Overhaul

**No Man's Starfield** fuses the best exploration, seamless interplanetary flight, and atmospheric planetary entry mechanics of *No Man's Sky* directly into *Starfield*, paired with a graphically simpler, stylized, high-framerate rendering profile purpose-built for the Steam Deck (`1280x800`, `AMD Custom GPU 0405`).

---

## Key Features

### 1. Interplanetary Pulse-Drive Flight (Same-System Planet-to-Planet Flight)
- **Unlocked From Minute One**: Automatically unlocks *Starfield*'s internal `SpaceCruise` sublight engine on any new or existing save (`SetStage 0107DCFA 100`).
- **No Man's Sky Pulse-Engine Speed**: Interplanetary travel across a star system takes **6 to 14 seconds** (`fMinSpaceCruiseOrbitalTimeToReachSeconds = 6.0`, `fMaxSpaceCruiseOrbitalTimeToReachSeconds = 14.0`, `fCruisePlanetAndMoonAccelerationMult = 950000.0`) with instant `0.35s` hold engagement (`fCruiseModeActivationHoldTime = 0.35`).
- **Zero Magnetic Deflection**: Removes the vanilla invisible deflection wall (`fCruiseCelestialMagnetismDeflectionMult = 0.0`) and speed clamp (`fCruiseMagnetismSpeedClampMult = 0.90`) that normally push your ship away from planets.

### 2. Fly-Into-Planet Atmospheric Entry & Automatic Landing (`bink2w64.dll` Autopilot + `150 km` Entry Envelope)
- **Fly Right Up to the Planet's Atmosphere**: Shrinks the orbital stop threshold from `10,000 km` down to **`150 km`** (`fCruiseStageNearOrbitalDistanceKM = 150.0`), bringing the planet's procedural surface and cloud layer right across your cockpit windshield.
- **Seamless Atmospheric Entry Autopilot**: When you fly into a planet or moon's `150 km` upper atmosphere in Cruise Mode, our native x64 proxy hook (`bink2w64.dll` $\rightarrow$ `0x1413FFEF0`) intercepts `SpaceCruise::OnOrbitalArrivalComplete` on the Main Thread, resolves a valid landable procedural terrain coordinate on that planet via `0x1417DEC70` (with automatic ocean avoidance and gas-giant safety), and triggers the animated planetary landing sequence (`0x141897ED0(1)`) — **no Starmap menu or clicking "Land" required**.
- **Manual Hot-Drop Anytime**: You can also run `bat land` in the console from any orbital altitude to immediately drop onto the planet below.

### 3. No Man's Sky Surface Exploration, Analysis Visor, Jetpack & Ship/Outpost Freedom
- **Long-Range Analysis Visor**: Hand-scanner range boosted **7.5x** (`fHandScannerScanRange = 75.0`), and flora/fauna scans required to complete a planetary biome survey reduced from `8` to **`3`** (`iHandScannerPlantsCountBase = 3`, `iHandScannerAnimalCountBase = 3`).
- **Denser Procedural Worlds**: Increases procedural worldspace and POI generation density (`fPlanetContentMaxNewWorldspacesPerScan = 10.0`, `uPlanetContentMaxRequestsPerBlock = 10`, `uPlanetContentMaxWorldspaceRequestsPoints = 30`) with balanced async time-slicing (`uProcGenTimeSlice = 4`) so terrain generates without frame hitches.
- **Continuous Jetpack / Boostpack & Low-Oxygen Sprinting**: Press-and-hold Boostpack flight (`bUsePressAndHoldControls = 1`), `2.25x` jump height, `70%` lower sprint oxygen drain, and `80%` reduced fall damage.
- **Unrestricted Corvette Ship Builder & Outposts**: Loose module overlap (`bShipBuilderEnableLooseRules = 1`, `fShipBuilderOverlapTolerance = -2.0`), `300` max ship modules (`uSpaceshipBuilderMaxModules = 300`), `2.5 km` station docking distance, and `300m` Outpost build height.

### 4. Graphically Simpler, Buttery-Smooth Steam Deck Rendering Profile
- **Fixes Broken Steam Deck Upscaler & GPU Detection**: Repairs `uiUpscaleTech=4294967295` (`0xFFFFFFFF`) in `StarfieldPrefs.ini` to `uiUpscaleTech=1` (FSR2 at `0.62` scale with `0.50`–`0.75` dynamic resolution at `1280x800`) and adds `"AMD Custom GPU 0405"` to `DefaultGlobalGraphicsSettings.json`.
- **Clean Stylized Visuals (`ToggleTrijuice on`)**: Enables Creation Engine 2's internal `ToggleTrijuice` texture-filtering optimizer, disables film grain, motion blur, depth-of-field blur, and contact shadows, and streamlines volumetric/shadow/GI passes so CPU/GPU frame budget goes directly into smooth spaceflight and procedural planetary terrain.

---

## Building from Source

Both binary artifacts (`Data/NoMansStarfield.esm` and `bin/bink2w64.dll`) are generated reproducibly with standard Python 3 (zero external compiler dependencies):

```bash
python3 tools/build_esm.py
python3 tools/build_proxy_dll.py
```
