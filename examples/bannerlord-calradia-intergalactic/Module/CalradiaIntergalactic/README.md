# Calradia: Intergalactic — Void Traders, Starfleets & 3D Space Combat (`CalradiaIntergalactic`)

A sci-fi **Total Conversion Mod** for *Mount & Blade II: Bannerlord* (`v1.5.5+`, Steam Deck & PC) that synthesizes the best mechanics, visuals, and concepts from **Starfield**, **No Man's Sky**, **Stellaris**, **Star Traders: Frontiers**, and **Warhammer 40,000: Rogue Trader**.

---

## Key Features

### 1. Galactic Astrography: Fiefdoms $\rightarrow$ Star Systems & Colony Moons
- **Towns $\rightarrow$ Star System Capitals & Orbital Starports (`[System Starport]`)**: Every major town becomes a primary planetary capital (e.g., *Pravend Prime*, *Zea- Diathma Prime*, *Sanala Solaris*).
- **Castles $\rightarrow$ Deep-Space Orbital Citadels (`[Orbital Starbase]`)**: Fortified sector defense stations and warp-gate bastions.
- **Bound Villages $\rightarrow$ System Minor Colonies**: Every village bound to a Town or Castle (`Settlement.BoundVillages` / `Village.Bound`) is grouped into that same Star System as a specialized **Moon Mining Outpost**, **Hydroponics Dome**, **Promethium Refinery**, or **Orbital Hab-Station** (e.g., *Pravend II-a [Moon Mining Colony]*).
- **Interstellar Factions & Void-Houses**: Converts the 6 Kingdoms into **Star-Empires & Syndicates** (*Galactic Core Imperium*, *Vlandian Warrant Consortium*, *Sturgian Void-born Jarldoms*, *Aserai Solaris Trade League*, *Khuzait Nomad Star-Flotilla*, *Battanian Nebula Freeholds*), Clans into **Void-Houses**, Lords into **Lord Captains / High Admirals**, and horses into **Sublight Strike-Skiffs & Interceptors**.

### 2. Modular & Procedural Spaceship Designer + Star Traders Loadout System
Accessible at any Starport or Orbital Starbase via **Voidship Drydock: Ship Designer, Loadouts & 3D Flagship Deck** (or anytime in flight/battle via `[B]`):
- **7 Hull Classes (*Star Traders: Frontiers* style)**:
  1. `Class-I Viper Scout-Corvette`
  2. `Class-II Sword-Pattern Strike Frigate`
  3. `Class-III Nomad Void-Clipper`
  4. `Class-IV Aegis Heavy Destroyer`
  5. `Class-V Constellation Cruiser`
  6. `Class-VI Sovereign Battlecruiser`
  7. `Class-VII Retribution Dreadnought`
- **Procedural `[C / B / A / S-Class Archeotech]` Drydock Upgrades (*No Man's Sky* + *Starfield*)**: Upgrade your **Warp Singularity Reactor**, **Sublight Grav-Drive**, **Void-Shield Projector**, **Spinal Weapon Battery**, **Secondary Turret/Hangar Bay**, and **Internal Ship Module**.
- **Starfield Reactor Power Pip Routing**: Dynamically route reactor pips between **Weapons (`WPN`)**, **Void-Shields (`SHD`)**, and **Sublight Thrusters (`ENG`)** to boost blaster/torpedo damage, personal & ship shield capacity, or campaign warp speed & 3D flight velocity.
- **Star Traders Range-Band 5-to-1 Ship Combat**: Execute pre-boarding orbital maneuvers across Range Bands 5 (Extreme Torpedo/Lance Range) down to Range Band 1 (Grapple & Drop-Pod Boarding Assault).

### 3. 3D Directional Space Battles (`PEW-PEW` Energy Weapons & `WHHHAAAAM` Torpedoes)
- **Full 3D 6-DOF Zero-G Thruster Flight**: Uses `Agent.SetExcludedFromGravity(true, false)` + 3D look-vector velocity physics so the player and Void-Marine jump-troops fly through 3D space:
  - `[Space]` / `[W]`: Ascend & thrust along your 3D camera pitch/yaw vector
  - `[C]`: Descend along vertical Z-axis
  - `[LeftShift]`: Sublight Afterburner Boost
  - `[G]`: Toggle between **3D Zero-G Flight Thrusters** and **Mag-Boots Gravity Lock**
- **`PEW-PEW` 3D Directed Energy Bolts**: Every ranged shot and `[F]` / `LB` press spawns high-velocity glowing 3D Plasma/Laser/Railgun bolts (`GameEntity` + dynamic colored `Light` + `barrier_sphere` mesh) streaking across the void at `96 m/s`.
- **`WHHHAAAAM` Photon Torpedo Plasma Shockwaves**: Firing heavy launchers or pressing `[Q]` / `RB` launches twin-linked Photon Torpedoes that detonate on impact into expanding 3D plasma shockwave spheres (`barrier_sphere` + `"psys_game_burning_agent"` + point light flash + radial AoE Void-Energy damage & Zero-G knockback).
- **Overhead 3D Capital Voidship Broadsides**: Allied and enemy Capital Voidships hover in low orbit above the battlefield (`Z + 26m–42m`) with glowing Void-Shield envelopes and thruster plumes, exchanging automated 3D laser and torpedo salvos across the sky.
- **Recharging Personal Void-Shields**: Every combatant has a recharging personal Void-Shield that absorbs incoming energy/melee hits with a glowing cyan/violet barrier flash (`[V]` triggers Void-Shield Overcharge).

### 4. Walkable 3D Spaceship Command Deck View
- Select **Walk Aboard 3D Flagship Command Deck (Interior View)** from any Starport menu (or enter any indoor mission) to walk around your Flagship's Bridge in first/third person, complete with:
  - A rotating **3D Holographic Sector Starmap** with 3 orbiting colony worlds
  - A pulsing **Warp-Core Singularity Column**
  - Uniformed **Flagship Bridge Officers**
  - Direct `[B]` access to the Ship Designer & Reactor Console
