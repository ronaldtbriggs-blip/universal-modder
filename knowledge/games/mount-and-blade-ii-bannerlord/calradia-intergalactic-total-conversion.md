---
kind: game
title: "Calradia: Intergalactic (v1.5.5): Sci-Fi Total Conversion — Star Systems, Modular Voidships, Walkable 3D Bridge & 3D Zero-G Space Combat"
game: "Mount & Blade II: Bannerlord"
game_version: "v1.5.5.109536 (SteamOS 3.x / Proton 10.0–11.0 & Windows x64)"
platform: linux
engine: native
route: loader-api
tools: ["Bannerlord module system (SubModule.xml, ModuleData XML, C# net472 SubModule)", "C# 5 csc.exe (.NET Framework 4.0.30319)", "Mono.Cecil 0.11", "OpenSSH / SCP to Steam Deck", "SteamWorkshop_CLI.exe"]
anti_cheat: "Singleplayer only; uses official TaleWorlds MBSubModuleBase, CampaignBehaviorBase, MissionLogic, and GameEntity scene APIs"
status: working
agents: ["Antigravity (Gemini)"]
humans: ["@ronaldtbriggs-blip"]
date: 2026-10-15
links:
  - https://github.com/ronaldtbriggs-blip/universal-modder
  - https://steamcommunity.com/sharedfiles/filedetails/?id=3820239682
tags: [bannerlord, total-conversion, sci-fi, space-combat, zero-g, starfield, no-mans-sky, stellaris, star-traders, rogue-trader, ship-designer, steam-deck, proton, workshop]
---

# Calradia: Intergalactic (v1.5.5): Sci-Fi Total Conversion — Star Systems, Modular Voidships, Walkable 3D Bridge & 3D Zero-G Space Combat

This field note documents **`CalradiaIntergalactic`** (*Calradia: Intergalactic — Void Traders, Starfleets & 3D Space Combat*, published to the Bannerlord Steam Workshop at [`id=3820239682`](https://steamcommunity.com/sharedfiles/filedetails/?id=3820239682)), a sci-fi Total Conversion mod for *Mount & Blade II: Bannerlord* (`v1.5.5.109536`) that fuses mechanics, visuals, and progression systems from *Starfield*, *No Man's Sky*, *Stellaris*, *Star Traders: Frontiers*, and *Warhammer 40,000: Rogue Trader*:

1. **Galactic Astrography (`GalacticSectorConversionBehavior`)**: Groups Bannerlord's settlement hierarchy (`Settlement.All`) into **Star Systems** where every **Town** (`Settlement.IsTown`) is a **Star System Primary / Capital World & Starport**, every **Castle** (`Settlement.IsCastle`) is a **Deep-Space Orbital Citadel / Starbase**, and every bound **Village** (`Settlement.BoundVillages` / `Village.Bound`) is a **Minor Moon Colony / Asteroid Mining Outpost / Hydroponics Dome / Promethium Refinery** belonging to its parent Star System. Converts all 6 Kingdoms into Interstellar Hegemonies/Syndicates, Clans into Void-Houses, Lords into Lord Captains/High Admirals, trade goods into sci-fi commodities, and horses into Sublight Strike-Skiffs, while adding Stellaris System Edicts, Megastructures, Galactic Crisis Operations, and 40k Rogue Trader Vox-Hail dialogues.
2. **Modular & Procedural Spaceship Designer + Star Traders Loadout System (`VoidshipArchitectBehavior`)**: Combines *Star Traders: Frontiers*'s 7 Hull Classes (`Class-I Viper Scout-Corvette` through `Class-VII Retribution Dreadnought`) and Range-Band 5-to-1 tactical ship combat, *No Man's Sky*'s procedural `[C / B / A / S-Class Archeotech]` Drydock upgrades, and *Starfield*'s Reactor Power Pip allocation (`WPN / SHD / ENG`).
3. **3D Directional Space Combat (`VoidSpaceCombatMissionLogic`)**: Transforms field, siege, and sally-out missions into **3D 6-DOF Zero-G Space Battles** using `Agent.SetExcludedFromGravity(true, false)`, 3D camera-vector thruster physics (`[Space]`/`[W]`/`[C]`/`[LeftShift]`/`[G]`), overhead 3D Capital Voidship superstructures exchanging automated orbital broadsides, real-time `PEW-PEW` 3D energy bolts (`96 m/s`), `WHHHAAAAM` Photon Torpedo expanding plasma shockwaves (`barrier_sphere` + point light + radial Void-Energy AoE), and recharging personal Void-Shields.
4. **Walkable 3D Spaceship Command Deck View**: Allows the player to step onto a 3D walkable Flagship Bridge interior (`LocationComplex.Current.GetLocationWithId("lordshall" | "tavern")` + runtime 3D `GameEntity` spawning) featuring a rotating **3D Holographic Sector Starmap** with 3 orbiting colony moons, a pulsing **Warp-Core Singularity Column**, uniformed Bridge Officers, and interactive `[B]` access to the Ship Designer & Reactor Console.

---

## Setup

- **Target Game**: *Mount & Blade II: Bannerlord* (`v1.5.5.109536`, AppID `261550`).
- **Target Hardware / OS**: Valve Steam Deck (`SteamOS 3.x` / `Proton 10.0–11.0`) and Windows x64 PC.
- **Reproducible Example Package**: `examples/bannerlord-calradia-intergalactic/` (`CalradiaIntergalactic.dll`, `77,312` bytes).

---

## Route

### 1. Grouping Feudal Fiefdoms into Star Systems (`GalacticSectorConversionBehavior`)
Bannerlord's settlement graph naturally links every village to a trade-bound or parent fortification via `Settlement.BoundVillages` (on `Town` / `Castle` settlements) and `Village.Bound` / `Village.TradeBound`. On `OnNewGameCreated` and `OnGameLoaded`, `GalacticSectorConversionBehavior` iterates `Settlement.All` and maps:
- **Primary Star System Capitals (`Settlement.IsTown`)**: Assigned an astrographic designation (`Pravend Prime [System Starport]`, `Zea-Diathma Prime [System Starport]`, `Sanala Solaris [System Starport]`, etc.).
- **Orbital Starbases (`Settlement.IsCastle`)**: Renamed to `<Prefix> Citadel [Orbital Starbase]`.
- **System Colony Moons (`Settlement.BoundVillages`)**: Each bound village `j` under parent system `<StarName>` receives planet/moon Roman-numeral orbital coordinates (`<StarName> II-a [Moon Mining Colony]`, `<StarName> III-b [Hydroponics Dome]`, `<StarName> IV-c [Promethium Refinery]`, `<StarName> V-d [Orbital Hab-Station]`).

Runtime renaming is applied via reflection on the `<Name>k__BackingField` of `Settlement`, `Kingdom`, `Clan`, `Hero`, `CharacterObject`, and `ItemObject` so both campaign map nameplates and encyclopedias display the sci-fi astrography without requiring core XML overwrites.

### 2. Modular Voidship Architect, Reactor Pips & Range-Band 5-to-1 Combat (`VoidshipArchitectBehavior`)
- **Hull Classes & Slots**: 7 hull tiers scale base hull integrity (`700` to `4,200`) and unlock spinal/secondary/internal bays.
- **Procedural `[C / B / A / S-Class Archeotech]` Drydock Upgrades**: Upgrades for the Warp Singularity Reactor, Sublight Grav-Drive, Void-Shield Projector, and Spinal Weapon Battery scale ship combat ratings and directly feed into 3D mission combat multipliers.
- **Starfield Reactor Power Pips**: A 6–10 pip reactor pool (`WeaponPowerPips`, `ShieldPowerPips`, `ThrusterPowerPips`) lets the player dynamically route power between weapon damage/cooldown, personal & ship Void-Shield capacity, and campaign/3D flight thruster speed.
- **Range-Band 5-to-1 Tactical Ship Combat**: Before or during encounters, players can open the **Range-Band 5-to-1 Tactical Ship Combat Console** (`Range 5: Extreme Lance/Torpedo` $\rightarrow$ `Range 1: Grapple & Drop-Pod Boarding Assault`) to exchange broadsides, repair hull breaches, or launch a 3D Zero-G boarding battle.

### 3. 3D Directional Zero-G Space Combat & Walkable 3D Bridge (`VoidSpaceCombatMissionLogic`)
- **Zero-G 6-DOF Flight**: Calling `agent.SetExcludedFromGravity(true, false)` disables standard gravity on the player and AI jump-troops. On each `OnMissionTick(float dt)`, `TickPlayerZeroGFlightAndWeapons` computes 3D acceleration along `mainAgent.LookDirection` (`[W]`/`[S]`), `Vec3.CrossProduct(lookDir, Vec3.Up)` (`[A]`/`[D]`), and vertical Z (`[Space]`/`[C]`), integrates `_playerZeroGVelocity` with inertial dampening, and updates position via `mainAgent.TeleportToPosition(nextPos)`.
- **Real-Time 3D `PEW-PEW` Energy Bolts & `WHHHAAAAM` Torpedo Shockwaves**:
  - `SpawnEnergyBolt` dynamically constructs a `GameEntity` via `GameEntity.CreateEmptyDynamic(scene, true)`, attaches a `"barrier_sphere"` `MetaMesh`, attaches a colored `Light.CreatePointLight(lightRadius)` (`cyan` for allied plasma lances, `orange-gold` for photon torpedoes, `crimson` for enemy disruptors), and advances it at `58–96 m/s`.
  - On proximity or surface impact, `SpawnWhamShockwave` spawns an expanding `barrier_sphere` shockwave entity (scaled via `MatrixFrame.rotation.ApplyScaleLocal(currentRadius)` over `0.28–0.65s` with `"psys_game_burning_agent"` plasma particles), drains target Void-Shields, applies radial Void-Energy damage, and imparts a 3D Zero-G knockback impulse.
- **Overhead 3D Capital Voidships**: Four 3D Capital Ship superstructures (`HullRoot` + `ShieldSphere` + `ThrusterGlow`) orbit at `Z + 26m..42m` above the battle center and fire automated 3D broadside beams across the sky every `2.6s`.
- **Walkable 3D Flagship Bridge**: When `IsFlagshipBridgeWalkActive` is triggered from a Starport (or inside an indoor scene), `SetupWalkableFlagshipBridgeEntities` spawns a rotating 3D Holographic Starmap (`_bridgeHoloStarmapCore` + 3 orbiting planet entities at radii `1.15m..2.25m`) and a pulsing violet Warp-Core Singularity Column (`_bridgeWarpReactorCore`) in front of the player, with `[B]` bound to open the Ship Designer & Reactor Console.

---

## Verification

- **Compilation & Static ABI Verification**: Compiled `CalradiaIntergalactic.dll` (`77,312` bytes) with `csc.exe` (`v4.8.9221.0`) against the Steam Deck's `v1.5.5.109536` `TaleWorlds.*.dll` assemblies and verified all 4 core types (`CalradiaIntergalacticSubModule`, `GalacticSectorConversionBehavior`, `VoidshipArchitectBehavior`, `VoidSpaceCombatMissionLogic`) plus 27 sci-fi items (`cig_sci_fi_items.xml`) and 15 Tier 1–6 Void-Marine troops (`cig_void_troops.xml`) via `Mono.Cecil`.
- **Steam Deck Deployment**: Deployed `Modules/CalradiaIntergalactic/` to `~/.local/share/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/CalradiaIntergalactic` on the Steam Deck (`deck@192.168.1.219`) and registered `CalradiaIntergalactic` (`IsSelected = true`, `LatestSizeInBytes = 77312`) in `LauncherData.xml`.
- **Steam Workshop Live Upload**: Published directly from the Steam Deck via `SteamWorkshop_CLI.exe` under Proton (`Item created. Item ID is 3820239682`, `192,855 / 192,855 bytes`, `Uploading done!`).
- **Publishing Lint**: Verified with `python -m um kb index`, `python -m um kb check`, and `python -m um publish check`.

---

## Gotchas

1. **`Mat3.ApplyScaleLocal` Overload Resolution in C# 5**:
   - In `TaleWorlds.Library.Mat3` (`v1.5.5`), uniform scaling takes a `float` (`ApplyScaleLocal(float scaleAmount)`), whereas non-uniform 3D scaling requires a `ref Vec3` (`ApplyScaleLocal(ref Vec3 scaleAmount)`). Passing `new Vec3(...)` by value fails with `CS1620: Argument 1 must be passed with the 'ref' keyword`.
2. **`LocationComplex` Namespace in `v1.5.5`**:
   - `LocationComplex` and `Location` reside in `TaleWorlds.CampaignSystem.Settlements.Locations` (not `TaleWorlds.CampaignSystem.Locations`).
3. **`OnAgentShootMissile` Base Parameter Order**:
   - `MissionBehavior.OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)` takes `position` third and `velocity` fourth; swapping them in `base.OnAgentShootMissile(...)` passes velocity as the origin to downstream mission listeners.
