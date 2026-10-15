using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CalradiaIntergalactic
{
    /// <summary>
    /// Root SubModule for "Calradia: Intergalactic — Void Traders, Starfleets &amp; 3D Space Combat".
    /// Integrates:
    /// 1. GalacticSectorConversionBehavior (Star Systems, Capital Worlds, Minor Moon/Asteroid Colonies,
    ///    Interstellar Syndicates, Star Traders Commodities, Stellaris System Edicts &amp; Galactic Crises,
    ///    and 40k Rogue Trader Vox-Hail Dialogues).
    /// 2. VoidshipArchitectBehavior (Starfield + Star Traders: Frontiers + No Man's Sky Modular Ship
    ///    Designer, Procedural C/B/A/S-Class Archeotech Drydock Components, Reactor Power Pip Allocation,
    ///    and Ship-to-Ship Range Band 5-to-1 Tactical Combat).
    /// 3. VoidSpaceCombatMissionLogic (3D Directional Zero-G Space Battles with 6-DOF Thrusters,
    ///    3D Capital Voidship Superstructures, Real-Time Pew-Pew Glowing Laser/Plasma/Railgun/Torpedo
    ///    Projectiles, WHHHAAAAM Plasma Shockwaves, Recharging Void-Shields, and Walkable 3D Flagship
    ///    Bridge &amp; Command Deck View).
    /// </summary>
    public class CalradiaIntergalacticSubModule : MBSubModuleBase
    {
        private bool _loadedNotified;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (!_loadedNotified)
            {
                _loadedNotified = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Calradia: Intergalactic] Star-Sector Conversion, Modular Voidship Drydock, Walkable Ship Deck & 3D Space Combat Active.",
                    Color.FromUint(0xFF00E5FF)));
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new GalacticSectorConversionBehavior());
                campaignStarter.AddBehavior(new VoidshipArchitectBehavior());
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (mission == null)
            {
                return;
            }

            if (mission.GetMissionBehavior<VoidSpaceCombatMissionLogic>() == null)
            {
                mission.AddMissionBehavior(new VoidSpaceCombatMissionLogic());
            }
        }
    }
}
