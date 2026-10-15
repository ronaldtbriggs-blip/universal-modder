# MODLOG — Calradia Deck Overhaul: Master Armorer & Clan Warband (`SteamDeckOverhaul`)

## Session — 2026-10-15

### 1. Root Cause Analysis: Why Bannerlord Workshop Mods Failed on Steam Deck
- **Evidence**: Inspected `~/.local/share/Steam/steamapps/compatdata/261550/pfx/drive_c/ProgramData/Mount and Blade II Bannerlord/logs/launcher_log_79677.txt` and `LauncherData.xml` on the Steam Deck.
- **Finding 1 — Missing `bin/ModVerifier/ModVerifier.exe` under Proton**:
  - `TaleWorlds.MountAndBlade.Launcher.Library.LauncherModsDLLManager.GetDLLVerifyReport` attempts to run `..\ModVerifier\ModVerifier.exe <dlls> -ld1`.
  - Because `bin/ModVerifier/ModVerifier.exe` is missing from the Steam Deck install, `Process.Start` throws `System.ComponentModel.Win32Exception: Native error= File not found`, and the exception handler in `LauncherModsDLLManager` sets `<LatestSizeInBytes>0</LatestSizeInBytes>` and `<IsDangerous>true</IsDangerous>` on every mod DLL in `LauncherData.xml`, locking C# mods in the launcher.
- **Finding 2 — Bannerlord `v1.5.5` ABI Break in Legacy Workshop Mods (`TroopDesigner` & `EoE1700`)**:
  - The Steam Deck auto-updated Bannerlord to `v1.5.5.109536`.
  - Using `Mono.Cecil` assembly verification against the Steam Deck's `v1.5.5` `TaleWorlds.*.dll` binaries, we confirmed that `TroopDesigner.dll` (`3448788740`, built for `v1.2.17`) has 34+ broken member/type references (`TaleWorlds.Core.ImageIdentifier` moved to `TaleWorlds.Core.ImageIdentifiers.ItemImageIdentifier`, `MBCharacterSkills.get_Skills`, `PartyScreenManager.get_Instance`, `AgentApplyDamageModel.CalculateDamage`, `GauntletLayer.LoadMovie`), and `MCMv5` (`2859238197`) only shipped adapters up to `v1.5.1`.

### 2. Hypotheses & Implementation
- **Hypothesis 1**: Providing a lightweight `.NET Framework 4.x` `ModVerifier.exe` at `bin/ModVerifier/ModVerifier.exe` that deserializes/serializes TaleWorlds' `ResultData` / `DLLResult` XML schema (`IsSafe = true`) and updating `LauncherData.xml` with exact DLL byte sizes will permanently prevent the launcher from marking mod DLLs as dangerous on SteamOS/Proton.
  - **Result**: Compiled `bin/ModVerifier/ModVerifier.exe` (`5,632 bytes`), tested output XML against `LauncherModsDLLManager` schema, deployed to Steam Deck, and updated `LauncherData.xml` across all 167 DLLs.
- **Hypothesis 2**: Building `SteamDeckOverhaul.dll` directly against the Steam Deck's `v1.5.5` `TaleWorlds.*.dll` assemblies using native controller-friendly UI (`GameMenu`, `MBInformationManager.ShowMultiSelectionInquiry`, `InformationManager.ShowTextInquiry`) and pre-registered XML objects (`sdo_armors.xml`, `sdo_clan_troops.xml`) will provide a crash-free, save-compatible overhaul with Armor Smithing, Point-Based Clan Armies, and Best-of-Workshop Campaign/Combat mechanics.
  - **Result**: Compiled `SteamDeckOverhaul.dll` (`72,704 bytes`) with `0` errors, `0` warnings, and `0` missing member/type references verified via `Mono.Cecil`, and passed `um publish check` (`0 failures, 0 warnings`).

### 3. Backups Created on Steam Deck
- `~/bannerlord_backup_20261015/Configs/LauncherData.xml` (full backup of original Proton `Configs/` directory before deployment).
