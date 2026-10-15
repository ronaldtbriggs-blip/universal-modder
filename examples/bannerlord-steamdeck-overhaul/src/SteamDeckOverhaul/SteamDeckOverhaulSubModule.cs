using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SteamDeckOverhaul
{
    public class SteamDeckOverhaulSubModule : MBSubModuleBase
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
                    "[Calradia Deck Overhaul v1.5.5] Master Armorer's Forge, Tiered Point Clan Armies & Steam Deck Optimizations Active.",
                    Color.FromUint(0xFF4ADE80)));
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new ArmorSmithingBehavior());
                campaignStarter.AddBehavior(new ClanArmyPointSystemBehavior());
                campaignStarter.AddBehavior(new SteamDeckCampaignOverhaulBehavior());
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (mission != null)
            {
                mission.AddMissionBehavior(new SteamDeckCombatMissionLogic());
            }
        }
    }
}
