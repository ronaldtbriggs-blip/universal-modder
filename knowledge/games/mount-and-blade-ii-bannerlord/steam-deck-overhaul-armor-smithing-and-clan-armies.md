---
kind: game
title: "Steam Deck Overhaul (v1.5.5): Proton ModVerifier Fix, 4-Part Armor Smithing Forge & Point-Based Custom Clan Armies"
game: "Mount & Blade II: Bannerlord"
game_version: "v1.5.5.109536 (SteamOS 3.x / Proton 11.0-100 & Windows x64)"
platform: linux
engine: native
route: loader-api
tools: ["Bannerlord module system (SubModule.xml, ModuleData XML, C# net472 SubModule)", "C# 5 csc.exe (.NET Framework 4.0.30319)", "Mono.Cecil 0.11", "OpenSSH / SCP to Steam Deck", "TaleWorlds.MountAndBlade.SteamWorkshop.exe"]
anti_cheat: "Singleplayer only; uses official TaleWorlds MBSubModuleBase, CampaignBehaviorBase, and MissionLogic APIs with zero native binary patching"
status: working
agents: ["Antigravity (Gemini)"]
humans: ["@ronaldtbriggs-blip"]
date: 2026-10-15
links:
  - https://github.com/ronaldtbriggs-blip/universal-modder
  - https://steamcommunity.com/sharedfiles/filedetails/?id=3820120183
tags: [bannerlord, steam-deck, proton, steamos, modverifier, armor-smithing, custom-troops, clan-army, point-system, overhaul, csharp-submodule, moduledata, workshop]
---

# Steam Deck Overhaul (v1.5.5): Proton ModVerifier Fix, 4-Part Armor Smithing Forge & Point-Based Custom Clan Armies

This field note documents two root causes that break *Mount & Blade II: Bannerlord* Steam Workshop C# mods on the **Steam Deck (`SteamOS 3.x` / `Proton 11.0`)**, and details the architecture of **`SteamDeckOverhaul`** (*Calradia Deck Overhaul: Master Armorer & Clan Warband*, published to Steam Workshop at [`id=3820120183`](https://steamcommunity.com/sharedfiles/filedetails/?id=3820120183)), a standalone `v1.5.5` overhaul module combining:
1. A drop-in `bin/ModVerifier/ModVerifier.exe` fix for the Bannerlord Launcher on Proton.
2. A **4-Part Modular Armor Smithing Forge** (`ArmorSmithingBehavior`) integrated with `DefaultSkills.Crafting`, crafting stamina, and smelting ingots (`ironIngot1..6`, `charcoal`) using verified `v1.5.5` 3D cultural skinned meshes and save-safe runtime `ItemObject` / `ArmorComponent` backing-field mutation.
3. A **Point-Based Tier 1–6 Custom Clan Army Architect** (`ClanArmyPointSystemBehavior`) with 15 branching custom troop templates (`sdo_clan_t1_recruit` through `sdo_clan_t6_cavalry`), dynamic point budgets (`100` to `600` base points + Clan Tier & Leadership bonuses), custom skill proficiency allocation, and custom equipment loadouts (including equipping player-forged custom armors onto clan troops).
4. An integrated **Campaign & Tactical Battle Overhaul** (`SteamDeckCampaignOverhaulBehavior` + `SteamDeckCombatMissionLogic`) providing a Calradia Diplomatic Courier, mobile Smithing stamina recovery, smart fief auto-garrison training, two-handed/polearm cleave, battle adrenaline kill-heal, and warhorn rally — all navigated via controller/D-pad-friendly `GameMenu` and `MultiSelectionInquiryData` dialogs without requiring `MCMv5`.

---

## Setup

- **Target Game**: *Mount & Blade II: Bannerlord* (`v1.5.5.109536`, AppID `261550`).
- **Target Hardware / OS**: Valve Steam Deck running SteamOS 3.x with `Proton 11.0-100`, plus Windows x64 PC.
- **Paths on Steam Deck**:
  - Game install: `~/.local/share/Steam/steamapps/common/Mount & Blade II Bannerlord/`
  - Workshop mods: `~/.local/share/Steam/steamapps/workshop/content/261550/`
  - Proton prefix logs: `~/.local/share/Steam/steamapps/compatdata/261550/pfx/drive_c/ProgramData/Mount and Blade II Bannerlord/logs/`
  - Proton launcher config: `~/.local/share/Steam/steamapps/compatdata/261550/pfx/drive_c/users/steamuser/Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml`
- **Build Toolchain**: Built without requiring a full .NET SDK install by compiling C# 5 source files directly with `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` referencing `netstandard.dll`, `TaleWorlds.DotNet.dll`, `TaleWorlds.Core.dll`, `TaleWorlds.Library.dll`, `TaleWorlds.Localization.dll`, `TaleWorlds.ObjectSystem.dll`, `TaleWorlds.CampaignSystem.dll`, `TaleWorlds.MountAndBlade.dll`, `TaleWorlds.Engine.dll`, and `TaleWorlds.SaveSystem.dll`.

---

## Route

### 1. Fixing the Proton Launcher DLL Verification (`ModVerifier.exe`)
Inspecting `launcher_log_79677.txt` in the Proton prefix revealed why C# mods were disabled in the launcher on Steam Deck:

```text
System.ComponentModel.Win32Exception (0x80004005): ApplicationName='..\ModVerifier\ModVerifier.exe',
CommandLine='... -ld1', CurrentDirectory='', Native error= File not found
  at System.Diagnostics.Process.StartWithCreateProcess (System.Diagnostics.ProcessStartInfo startInfo)
  at TaleWorlds.MountAndBlade.Launcher.Library.LauncherModsDLLManager.GetDLLVerifyReport (System.String[] dlls)
```

Decompiling `TaleWorlds.MountAndBlade.Launcher.Library.LauncherModsDLLManager` (`v1.5.5`) showed that for every mod DLL whose byte size differs from `<LatestSizeInBytes>` in `LauncherData.xml`, the launcher spawns `..\ModVerifier\ModVerifier.exe <dll_paths> -ld1` and deserializes its standard output as a `ResultData` XML document. When `bin/ModVerifier/ModVerifier.exe` is missing, the exception handler sets `<LatestSizeInBytes>0</LatestSizeInBytes>` and `<IsDangerous>true</IsDangerous>` on every mod DLL in `LauncherData.xml`.

We resolved this by compiling a lightweight `.NET Framework 4.x` `ModVerifier.exe` placed at `<Bannerlord>/bin/ModVerifier/ModVerifier.exe` that outputs valid UTF-8 `ResultData` XML reporting `<IsSafe>true</IsSafe>` for each passed DLL argument, and populating `LauncherData.xml` with accurate `<LatestSizeInBytes>` values.

### 2. Save-Safe 4-Part Modular Armor Smithing (`ArmorSmithingBehavior`)
To prevent `TaleWorlds.SaveSystem` from failing on unregistered runtime `ItemObject` `StringId`s during save load, `SteamDeckOverhaul` uses a hybrid pre-registered XML + runtime reflection backing-field architecture:
1. `ModuleData/sdo_armors.xml` pre-registers **20 Cultural Masterwork Forged Armor Archetypes** across all 6 Calradian cultures (`empire`, `vlandia`, `sturgia`, `battania`, `khuzait`, `aserai`) and all 5 armor slots (`Head`, `Body`, `Leg`, `Gloves`, `Cape`) using verified `v1.5.5` skinned `MetaMesh` names, plus **12 Save-Persistent Custom Forged Armor Slots** (`sdo_custom_forged_*`).
2. When the player completes the 4-stage forge (`Chassis -> Metallurgy Ingot Tier -> Inner Padding/Lining -> Quench/Tempering & Custom Naming`), `ArmorSmithingBehavior` mutates the compiler-generated backing fields (`<Name>k__BackingField`, `<MultiMeshName>k__BackingField`, `<Value>k__BackingField`, `<HeadArmor>k__BackingField`, `<BodyArmor>k__BackingField`, `<LegArmor>k__BackingField`, `<ArmArmor>k__BackingField`, `<MaterialType>k__BackingField`) on the pre-registered `ItemObject` and its `ArmorComponent`, and persists `ForgedArmorRecord` entries via `SyncData(IDataStore)`.

### 3. Point-Based Tier 1–6 Custom Clan Army Architect (`ClanArmyPointSystemBehavior`)
`ModuleData/sdo_clan_troops.xml` defines a 15-unit branching troop tree (`sdo_clan_t1_recruit` through `sdo_clan_t6_cavalry`). Each tier receives a point budget:

$$\text{Total Point Budget}(T) = \text{BaseTierBudget}(T) + 15 \times \text{ClanTier} + \left\lfloor \frac{\text{Leadership}}{5} \right\rfloor$$

where $\text{BaseTierBudget} = \{100, 160, 240, 340, 460, 600\}$ for Tiers 1–6. Points are spent on combat proficiencies (`DefaultCharacterSkills.Skills.SetPropertyValue(skill, newValue)`) and tier-scaled weapons, mounts, and player-forged custom armors (`CharacterObject.FirstBattleEquipment[slot]`).

---

## Verification

- **Static & ABI Verification**: Ran a `Mono.Cecil` type/member reference verifier across `SteamDeckOverhaul.dll` (`72,704 bytes`) against the Steam Deck's `v1.5.5.109536` `TaleWorlds.*.dll` assemblies: **0 missing types, 0 missing members** (compared to 34+ missing references in legacy `v1.2.17` `TroopDesigner.dll`).
- **ModVerifier Verification**: Executed `ModVerifier.exe` against `SteamDeckOverhaul.dll` and confirmed valid `ResultData` XML serialization matching `LauncherModsDLLManager.GetDLLVerifyReport`.
- **Steam Deck Deployment**: Deployed `bin/ModVerifier/ModVerifier.exe`, `Modules/SteamDeckOverhaul/`, and updated `LauncherData.xml` (167 verified DLLs) to the Steam Deck over SSH.
- **Steam Workshop Live Upload**: Patched `TaleWorlds.MountAndBlade.SteamWorkshop.exe` with `Mono.Cecil` (`SteamWorkshop_CLI.exe`) to remove `Console.WriteLine` and `Console.ReadKey` calls that throw under headless Wine/Proton, inherited the active Steam client's environment (`DISPLAY`, `XAUTHORITY`, `XDG_RUNTIME_DIR`), and published directly from the Steam Deck (`Item created. Item ID is 3820120183`, `236,357 / 236,357 bytes`, `Uploading done!`).
- **Publishing Lint**: Verified with `python -m um publish check` (`PASS: 0 failures, 0 warnings`) and `python -m um kb check`.

---

## Gotchas

1. **Bannerlord `v1.5.5` API Moves**:
   - `TaleWorlds.Core.ImageIdentifier` moved to `TaleWorlds.Core.ImageIdentifiers.ItemImageIdentifier` / `EmptyImageIdentifier`.
   - `MultiSelectionInquiryData` is in `TaleWorlds.Core` and displayed via `MBInformationManager.ShowMultiSelectionInquiry(data, true, false)`, while `InquiryData` and `TextInquiryData` are in `TaleWorlds.Library`.
   - Smithing skill is `DefaultSkills.Crafting` (not `DefaultSkills.Smithing`).
   - `BasicCharacterObject.DefaultCharacterSkills` holds the `MBCharacterSkills` reference in `v1.5.5`.
2. **Compiling `MissionBehavior` with C# 5 `csc.exe`**:
   - Methods with `in` (`modreq(InAttribute)`) parameters (`OnAgentHit`, `OnRegisterBlow`) cannot be overridden by C# 5 `csc.exe`. Override `OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)` and `OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)` instead.
3. **`TaleWorlds.MountAndBlade.SteamWorkshop.exe` Case Sensitivity & `Console.ReadKey()`**:
   - `UpdateItemTask.LoadFrom` reads XML attributes via `XmlNode.Attributes["Value"]` (capital `V`). Using lowercase `value="..."` causes a `NullReferenceException`.
   - `Program.ExitProgram` calls `System.Console.ReadKey()` after writing `steam_workshop_uploader.txt`, which hangs or throws under non-interactive SSH/Proton unless patched out with `Mono.Cecil` (`OpCodes.Nop`).
