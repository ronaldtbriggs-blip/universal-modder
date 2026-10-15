# Calradia Deck Overhaul: Master Armorer & Clan Warband (`SteamDeckOverhaul`)

A comprehensive, standalone overhaul mod for **Mount & Blade II: Bannerlord (`v1.5.5+`)**, engineered specifically for **Steam Deck (SteamOS / Proton)** and PC with zero fragile external dependencies and full controller / D-pad navigation.

---

## Key Features

### 1. Steam Deck / Proton Launcher & DLL Verification Fix (`ModVerifier.exe`)
- Fixes the root cause of C# mods failing to load on Steam Deck (`LauncherModsDLLManager.GetDLLVerifyReport` throwing `Win32Exception: ApplicationName='..\ModVerifier\ModVerifier.exe', Native error= File not found`, which marks all mod DLLs as `<IsDangerous>true</IsDangerous>` and `<LatestSizeInBytes>0</LatestSizeInBytes>` in `LauncherData.xml`).
- Built directly against Bannerlord `v1.5.5` assemblies (`TaleWorlds.Core.ImageIdentifiers.ItemImageIdentifier`, `DefaultSkills.Crafting`, `BasicCharacterObject.DefaultCharacterSkills`), replacing outdated `v1.2.x` Workshop mods that crash on `v1.5.5`.

### 2. Master Armorer's 4-Part Modular Armor Smithing Forge
Accessible from any **Town** or **Castle** menu (`Enter the Master Armorer's Forge (Armor Smithing)`), integrated with Bannerlord's Smithing skill (`DefaultSkills.Crafting`), crafting stamina, and smelting ingots (`charcoal`, `ironIngot1..6`):
1. **Part I — Cultural & Slot 3D Skinned Mesh Chassis**:
   - Choose **Head Helmets**, **Body Cuirasses & Coats of Plates**, **Gauntlets & Vambraces**, **Greaves & Sabatons**, or **Pauldrons & GuardCapes** across all 6 Calradian cultures (**Imperial**, **Vlandian**, **Sturgian**, **Battanian**, **Khuzait**, **Aserai**), backed by verified `v1.5.5` skinned `MetaMesh` assets.
2. **Part II — Outer Plate & Scale Reinforcement (Metallurgy Tier)**:
   - Select from **Crude Iron**, **Wrought Iron**, **Refined Steel**, **Fine Steel**, or **Thamaskene Steel** (`ironIngot2` through `ironIngot6`), scaling armor protection, ingot cost, and Smithing difficulty.
3. **Part III — Inner Padding, Maille & Articulation**:
   - Select **Quilted Gambeson**, **Boiled Cuir Bouilli**, **Riveted Maille Haubergeon**, **Articulated Lames**, or **Masterwork Silk-Lined Brigandine**, tuning weight, mobility, and blunt/pierce absorption.
4. **Part IV — Surface Tempering & Custom Naming**:
   - Quench & temper your armor, award massive Smithing XP, and **custom-name** your forged armor (`sdo_custom_forged_*` slots persist across save/load without breaking `TaleWorlds.SaveSystem`).
5. **Armor Smelting & Reforging**:
   - **Smelt** looted armor from your inventory directly into Smithing ingots (`ironIngot1..6` + `charcoal`) and Smithing XP.
   - **Reforge & Temper** your currently equipped hero armor in the field (+4 to +7 permanent armor rating).

### 3. Custom Clan Army Architect (Point-Based Tier 1–6 Troop Tree)
Accessible from any **Town** or **Castle** menu (`Clan Warband Architect (Point-System Custom Troops)`):
- Complete **15-unit branching Custom Clan Troop Tree** across **Tiers 1–6**:
  - **Tier 1**: Clan Levy Recruit (`100 pts` base)
  - **Tier 2**: Clan Footman / Clan Skirmisher (`160 pts` base)
  - **Tier 3**: Clan Vanguard Infantry / Clan Marksman / Clan Outrider (`240 pts` base)
  - **Tier 4**: Clan Heavy Infantry / Clan Sharpshooter / Clan Lancer (`340 pts` base)
  - **Tier 5**: Clan Shock Guard / Clan Master Archer / Clan Cataphract (`460 pts` base)
  - **Tier 6**: Clan Immortal Champion / Clan Sovereign Deadeye / Clan Royal Cataphract (`600 pts` base)
- **Dynamic Point Budget System**:
  - Base Tier Points + **Clan Tier Bonus** (`+15 pts` per Clan Tier) + **Leadership Skill Bonus** (`+1 pt` per 5 Leadership).
  - Spend points on **Combat Skill Proficiencies** (One-Handed, Two-Handed, Polearm, Bow, Crossbow, Throwing, Athletics, Riding) and **Equipment Loadouts** (including equipping your **custom player-forged armors** onto your clan's troops!).
- **Direct Recruitment & Vanilla Recruit Induction**:
  - Muster custom clan troops directly for Denars, or induct vanilla Tier 1–2 recruits in your party into your custom Clan Levy Recruits.

### 4. Calradia Deck Overhaul Hub (Diplomacy Courier, Fiefs & Combat Overhaul)
Accessible from any **Town** or **Castle** menu (`Calradia Deck Overhaul Hub (Courier, Fiefs & Tweaks)`):
- **Calradia Diplomatic Courier**: Dispatch a courier (25 Denars) to open an immediate remote conversation with any living noble, faction leader, or clan companion across Calradia.
- **Mobile Smithing Stamina Recovery**: Automatically recover Smithing stamina (`+4/hr`) while traveling on the campaign map, or rest at the Master Forge for an instant 100% stamina restore.
- **Smart Fief Auto-Garrison & Governor Boost**: Daily automatic garrison training (`+60 XP/day` per stack) and settlement Loyalty/Security/Prosperity boost (`+1.5/day`) across all player-owned towns and castles.
- **Tactical Battle Overhaul (`SteamDeckCombatMissionLogic`)**:
  - **Two-Handed & Polearm Cleave (Cut Through Everyone)**: Heavy 2H and polearm melee swings cleave into up to 2 adjacent enemies within `2.2m`.
  - **Battle Adrenaline Kill-Heal**: Player and hero companions restore `+6 HP` per kill in battle.
  - **Tactical Warhorn Rally**: Every 45 seconds in battle, rallies nearby allied troops within `25m` (`+4 HP` & morale surge).

---

## Installation (Steam Deck & PC)

1. Copy the `SteamDeckOverhaul` folder into `<Bannerlord Install>/Modules/SteamDeckOverhaul/`.
2. *(Steam Deck / Proton Users)* Copy `bin/ModVerifier/ModVerifier.exe` into `<Bannerlord Install>/bin/ModVerifier/ModVerifier.exe` so the Bannerlord Launcher can verify C# mod DLLs under Proton.
3. Enable **`Calradia Deck Overhaul: Master Armorer & Clan Warband`** (`SteamDeckOverhaul`) after the official modules (`Native`, `SandBoxCore`, `Sandbox`, `CustomBattle`, `StoryMode`, `BirthAndDeath`).

---

## Requirements & Compatibility
- **Game Version**: Mount & Blade II: Bannerlord `v1.5.5` (compatible with `v1.3.x–v1.5.x`).
- **Dependencies**: Official TaleWorlds modules only (`Native`, `SandBoxCore`, `Sandbox`, `StoryMode`). Works standalone or alongside `Bannerlord.Harmony`.
- **Save Compatibility**: Safe to add to existing saves or new campaigns.

## Credits & License
- Authored with [Universal Modder (`um`)](https://github.com/ronaldtbriggs-blip/universal-modder).
- License: MIT (contains zero proprietary TaleWorlds game binaries or assets).
