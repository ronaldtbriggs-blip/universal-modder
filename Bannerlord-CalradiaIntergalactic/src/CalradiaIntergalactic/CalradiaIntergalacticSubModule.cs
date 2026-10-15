using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace CalradiaIntergalactic
{
    /// <summary>
    /// Root SubModule for "Calradia: Intergalactic — Spaceboy Total Conversion".
    /// Integrates:
    /// 1. SpaceboyCharacterCreationHandler &amp; SpaceboyGalacticMapAndCommissioningBehavior:
    ///    - Renames Main Menu options into Spaceboy Campaign / Void Trader Sandbox.
    ///    - Converts all 6 Cultures &amp; Cultural Feats into Interstellar Hegemonies &amp; Star-Syndicates.
    ///    - Rewrites all Character Creation Narrative Menus, Options, and 3D Character Outfits into
    ///      a complete Spaceboy Origin &amp; Starfleet Academy experience.
    ///    - Automatically completes/bypasses the medieval StoryMode tutorial upon finishing Character
    ///      Creation, commissions the player's Flagship ('ISS Sovereign Warrant') with 75,000 Credits
    ///      and 34 Navis Void-Marines, equips full Void-Captain Power Carapace &amp; Plasma/Vibro weapons,
    ///      and opens the Spaceboy Commissioning Holo-Briefing.
    ///    - Transforms the 3D Campaign Map into a Deep-Space Nebula where every settlement has a
    ///      3D glowing Star/Planet/Colony Moon orb and every party is a 3D glowing Voidship!
    /// 2. GalacticSectorConversionBehavior (Star Systems, Capital Worlds, Minor Moon/Asteroid Colonies,
    ///    Star Traders Commodities, Stellaris System Edicts &amp; Galactic Crises, 40k Vox-Hail Dialogues).
    /// 3. VoidshipArchitectBehavior (Modular Ship Designer, Procedural C/B/A/S-Class Archeotech Drydock,
    ///    Reactor Power Pips, Range-Band 5-to-1 Ship Combat, and Walkable 3D Flagship Bridge).
    /// 4. VoidSpaceCombatMissionLogic (3D Directional Zero-G Space Battles with 6-DOF Thrusters,
    ///    3D Capital Voidships, Pew-Pew Plasma Lances, WHHHAAAAM Photon Torpedoes, and Void-Shields).
    /// </summary>
    public class CalradiaIntergalacticSubModule : MBSubModuleBase
    {
        private bool _loadedNotified;
        private bool _mainMenuOptionAdded;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Debug.Print("[CalradiaIntergalactic:Spaceboy] OnSubModuleLoad: Initializing Spaceboy Total Conversion.", 0, Debug.DebugColor.Cyan);

            try
            {
                if (TaleWorlds.MountAndBlade.Module.CurrentModule != null && !_mainMenuOptionAdded)
                {
                    _mainMenuOptionAdded = true;
                    TaleWorlds.MountAndBlade.Module.CurrentModule.AddInitialStateOption(
                        new InitialStateOption(
                            "SpaceboyHoloCodex",
                            new TextObject("{=!}[SPACEBOY HOLO-CODEX & CONTROLS]"),
                            1,
                            delegate
                            {
                                OpenMainMenuSpaceboyCodexPopup();
                            },
                            delegate
                            {
                                return new ValueTuple<bool, TextObject>(false, TextObject.GetEmpty());
                            },
                            null,
                            null));
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[CalradiaIntergalactic:Spaceboy] OnSubModuleLoad Option Error: " + ex.Message, 0, Debug.DebugColor.Red);
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllCulturesAndFeatsToSciFi();
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllTroopsHeroesAndItemsToSciFi();
            ConvertMainMenuInitialStateOptions();

            Debug.Print("[CalradiaIntergalactic:Spaceboy] OnBeforeInitialModuleScreenSetAsRoot: Main Menu & Cultures converted to Spaceboy Sci-Fi.", 0, Debug.DebugColor.Cyan);

            if (!_loadedNotified)
            {
                _loadedNotified = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[CALRADIA: INTERGALACTIC — SPACEBOY EDITION] Total Conversion Active: Sci-Fi Cutscenes, Spaceboy Character Creation, 3D Galactic Starmap, Walkable Flagship Bridge & 3D Zero-G Space Battles!",
                    Color.FromUint(0xFF00E5FF)));
            }
        }

        private void ConvertMainMenuInitialStateOptions()
        {
            try
            {
                if (TaleWorlds.MountAndBlade.Module.CurrentModule == null) return;

                FieldInfo nameField = typeof(InitialStateOption).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                IEnumerable<InitialStateOption> options = TaleWorlds.MountAndBlade.Module.CurrentModule.GetInitialStateOptions();
                if (options != null && nameField != null)
                {
                    foreach (InitialStateOption opt in options)
                    {
                        if (opt == null || string.IsNullOrEmpty(opt.Id)) continue;
                        string id = opt.Id;
                        if (id == "StoryModeNewGame")
                        {
                            nameField.SetValue(opt, new TextObject("{=!}SPACEBOY CAMPAIGN (INTERGALACTIC STORY)"));
                        }
                        else if (id == "SandBoxNewGame")
                        {
                            nameField.SetValue(opt, new TextObject("{=!}VOID TRADER SANDBOX (OPEN GALAXY)"));
                        }
                        else if (id == "CustomBattle")
                        {
                            nameField.SetValue(opt, new TextObject("{=!}3D SPACE BATTLE SIMULATOR"));
                        }
                        else if (id == "ContinueCampaignFile")
                        {
                            nameField.SetValue(opt, new TextObject("{=!}RESUME FLAGSHIP LOG (CONTINUE)"));
                        }
                        else if (id == "LoadCampaign")
                        {
                            nameField.SetValue(opt, new TextObject("{=!}LOAD ASTROGRAPHIC SAVE"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("[CalradiaIntergalactic:Spaceboy] ConvertMainMenuInitialStateOptions Error: " + ex.Message, 0, Debug.DebugColor.Red);
            }
        }

        private void OpenMainMenuSpaceboyCodexPopup()
        {
            string body =
                "=== CALRADIA: INTERGALACTIC — SPACEBOY TOTAL CONVERSION ===\n\n" +
                "1. SPACEBOY ORIGIN & AUTOMATIC FLAGSHIP COMMISSIONING:\n" +
                "   • Start 'SPACEBOY CAMPAIGN' or 'VOID TRADER SANDBOX' to select your Interstellar Hegemony/Syndicate and Starfleet Academy origin in a 3D Holographic Starmap Chamber.\n" +
                "   • The medieval training field is automatically bypassed! You immediately commission the Corvette 'ISS Sovereign Warrant' with 75,000 Star-Credits, 34 Void-Marines, and full Spaceboy Void-Captain Power Armor.\n\n" +
                "2. 3D GALACTIC SECTOR MAP:\n" +
                "   • Every Party is a 3D glowing Voidship with Void-Shields & Ion Thrusters; every Settlement is a glowing 3D Star System Primary, Orbital Starbase, or Minor Colony Moon.\n" +
                "   • Press [B] anywhere on the Galactic Map for the Spaceboy Command Console & Ship Designer!\n" +
                "   • Press [F8] anywhere on the Galactic Map to launch an IMMEDIATE 3D ZERO-G SPACE BATTLE!\n\n" +
                "3. 3D DIRECTIONAL ZERO-G SPACE BATTLES & WALKABLE FLAGSHIP BRIDGE:\n" +
                "   • [Space + W/A/S/D] 3D Zero-G Thruster Flight | [C] Dive | [F] PEW-PEW Plasma Lance | [Q] WHHHAAAAM Photon Torpedo | [V] Void-Shield Surge | [G] Mag-Boots\n" +
                "   • Dock at any Starport or press [B] to walk around your 3D Flagship Bridge with a rotating 3D Holographic Starmap & Warp Core!";

            InformationManager.ShowInquiry(
                new InquiryData(
                    "CALRADIA: INTERGALACTIC — SPACEBOY HOLO-CODEX",
                    body,
                    true,
                    false,
                    "Engage Hyperdrive",
                    "",
                    null,
                    null,
                    "",
                    0f,
                    null,
                    null,
                    null),
                false,
                false);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            Debug.Print("[CalradiaIntergalactic:Spaceboy] OnGameStart: Registering Spaceboy Galactic Campaign Behaviors.", 0, Debug.DebugColor.Cyan);
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllCulturesAndFeatsToSciFi();
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllTroopsHeroesAndItemsToSciFi();

            CampaignGameStarter campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new SpaceboyGalacticMapAndCommissioningBehavior());
                campaignStarter.AddBehavior(new GalacticSectorConversionBehavior());
                campaignStarter.AddBehavior(new VoidshipArchitectBehavior());
            }
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllCulturesAndFeatsToSciFi();
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllTroopsHeroesAndItemsToSciFi();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (mission == null)
            {
                return;
            }

            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllTroopsHeroesAndItemsToSciFi();

            if (mission.GetMissionBehavior<VoidSpaceCombatMissionLogic>() == null)
            {
                mission.AddMissionBehavior(new VoidSpaceCombatMissionLogic());
            }
        }
    }
}
