# MODLOG — Calradia: Intergalactic (`CalradiaIntergalactic`)

- **Game**: *Mount & Blade II: Bannerlord* (`v1.5.5.109536`, Steam Deck SteamOS/Proton & Windows x64)
- **Module ID**: `CalradiaIntergalactic`
- **Binary**: `Modules/CalradiaIntergalactic/bin/Win64_Shipping_Client/CalradiaIntergalactic.dll` (`77,312` bytes)
- **Data Catalogs**:
  - `ModuleData/cig_sci_fi_items.xml` (27 sci-fi blasters, railguns, torpedo launchers, energy cells, plasma torch-blades, Void-Power Carapaces, Vacc-Helms, Thruster-Packs, and interstellar trade commodities)
  - `ModuleData/cig_void_troops.xml` (15 Tier 1–6 Void-Marine, Exo-Shock, and Strike-Pilot troop templates)

## Verified Architecture & Implementation Log
1. **`GalacticSectorConversionBehavior.cs`**:
   - Groups every `Settlement` into Star Systems where `IsTown` = Star System Primary / Starport, `IsCastle` = Orbital Citadel / Starbase, and `BoundVillages` = Minor Colony Moons (`[Moon Mining Colony]`, `[Hydroponics Dome]`, `[Promethium Refinery]`, `[Orbital Hab-Station]`).
   - Converts all 6 Kingdoms into Star-Empires/Syndicates, Clans into Void-Houses, Lords into Lord Captains/High Admirals, and vanilla goods/mounts into sci-fi commodities and Sublight Strike-Skiffs.
   - Adds **Galactic Sector Command** menu (Astrographic System Registry, Stellaris System Edicts, Megastructure Construction, Galactic Crisis Dispatch, and Void-Marine Recruitment) and **40k Rogue Trader Vox-Hail Dialogues**.
2. **`VoidshipArchitectBehavior.cs`**:
   - Implements 7 Hull Classes (`Class-I Viper Scout-Corvette` to `Class-VII Retribution Dreadnought`), procedural `[C / B / A / S-Class Archeotech]` Drydock upgrades, Spinal/Secondary/Internal module bays, Starfield Reactor Power Pip allocation (`WPN / SHD / ENG`), Star Traders Range-Band 5-to-1 ship combat, and the Walkable 3D Flagship Bridge launcher.
3. **`VoidSpaceCombatMissionLogic.cs`**:
   - Implements 3D Zero-G 6-DOF flight thrusters (`Agent.SetExcludedFromGravity(true, false)` + 3D camera-vector velocity), overhead 3D Capital Voidship superstructures exchanging orbital broadsides, real-time 3D `PEW-PEW` energy bolts (`96 m/s`), `WHHHAAAAM` Photon Torpedo expanding plasma shockwaves (`barrier_sphere` + point light + radial Void-Energy AoE), recharging personal Void-Shields, and the Walkable 3D Flagship Command Deck with rotating 3D Holographic Starmap and Warp Singularity Core.
