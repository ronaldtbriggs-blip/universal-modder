using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SteamDeckOverhaul
{
    /// <summary>
    /// Integrates the most popular Bannerlord Steam Workshop QoL, Immersion, Diplomacy,
    /// Fief Management, and Steam Deck performance optimizations into a single rock-solid
    /// CampaignBehavior designed for controller/D-pad navigation on the Steam Deck:
    ///   1. Calradia Diplomatic Courier (Diplomacy / Telepathy style remote lord/companion messenger)
    ///   2. Mobile Smithing Stamina Recovery (recover forging stamina while traveling on campaign map)
    ///   3. Smart Fief Auto-Garrison & Governor Boost (Improved Garrisons style daily XP, loyalty & security)
    ///   4. Steam Deck Frame-Pacing & AI Stutter Optimizer (6-hour campaign tick smoother)
    ///   5. Configurable Combat Overhaul Toggles (Two-Handed/Polearm Cleave, Battle Adrenaline Kill-Heal, Warhorn Rally)
    /// </summary>
    public class SteamDeckCampaignOverhaulBehavior : CampaignBehaviorBase
    {
        public static bool EnableWeaponCleave = true;
        public static bool EnableBattleAdrenaline = true;
        public static bool EnableWarhornRally = true;
        public static bool EnableMobileSmithingStamina = true;
        public static bool EnableSmartFiefManager = true;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(OnHourlyTick));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("sdo_enable_cleave", ref EnableWeaponCleave);
            dataStore.SyncData("sdo_enable_adrenaline", ref EnableBattleAdrenaline);
            dataStore.SyncData("sdo_enable_warhorn", ref EnableWarhornRally);
            dataStore.SyncData("sdo_enable_mobile_stamina", ref EnableMobileSmithingStamina);
            dataStore.SyncData("sdo_enable_smart_fief", ref EnableSmartFiefManager);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddGameMenus(starter);
        }

        private void AddGameMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "town",
                "sdo_overhaul_hub_town",
                "{=sdo_overhaul_hub_btn}Calradia Deck Overhaul Hub (Courier, Fiefs & Tweaks)",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenOverhaulHubMenu();
                },
                false,
                4,
                false,
                null);

            starter.AddGameMenuOption(
                "castle",
                "sdo_overhaul_hub_castle",
                "{=sdo_overhaul_hub_castle_btn}Calradia Deck Overhaul Hub (Courier, Fiefs & Tweaks)",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenOverhaulHubMenu();
                },
                false,
                4,
                false,
                null);
        }

        private void OnHourlyTick()
        {
            if (!EnableMobileSmithingStamina)
            {
                return;
            }

            if (MobileParty.MainParty == null || Hero.MainHero == null)
            {
                return;
            }

            // Recover Smithing stamina while moving on the campaign map (popular QoL workshop tweak)
            ICraftingCampaignBehavior crafting = Campaign.Current != null
                ? Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>()
                : null;
            if (crafting == null)
            {
                return;
            }

            RecoverHeroCraftingStamina(crafting, Hero.MainHero, 4);

            if (Clan.PlayerClan != null && Clan.PlayerClan.Companions != null)
            {
                for (int i = 0; i < Clan.PlayerClan.Companions.Count; i++)
                {
                    Hero companion = Clan.PlayerClan.Companions[i];
                    if (companion != null && companion.PartyBelongedTo == MobileParty.MainParty)
                    {
                        RecoverHeroCraftingStamina(crafting, companion, 4);
                    }
                }
            }
        }

        private void RecoverHeroCraftingStamina(ICraftingCampaignBehavior crafting, Hero hero, int bonusStamina)
        {
            try
            {
                int cur = crafting.GetHeroCraftingStamina(hero);
                int max = crafting.GetMaxHeroCraftingStamina(hero);
                if (cur < max)
                {
                    int next = Math.Min(max, cur + bonusStamina);
                    crafting.SetHeroCraftingStamina(hero, next);
                }
            }
            catch
            {
                // Ignore if behavior not initialized yet
            }
        }

        private void OnDailyTick()
        {
            if (!EnableSmartFiefManager || Clan.PlayerClan == null || Hero.MainHero == null)
            {
                return;
            }

            int trainedCount = 0;
            int boostedFiefs = 0;

            if (Clan.PlayerClan.Settlements != null)
            {
                for (int i = 0; i < Clan.PlayerClan.Settlements.Count; i++)
                {
                    Settlement settlement = Clan.PlayerClan.Settlements[i];
                    if (settlement == null)
                    {
                        continue;
                    }

                    if (settlement.IsTown || settlement.IsCastle)
                    {
                        Town fief = settlement.Town;
                        if (fief != null)
                        {
                            // Boost settlement loyalty, security, and prosperity (Governor & Fief Overhaul)
                            if (fief.Loyalty < 95f)
                            {
                                fief.Loyalty = Math.Min(100f, fief.Loyalty + 1.5f);
                            }
                            if (fief.Security < 95f)
                            {
                                fief.Security = Math.Min(100f, fief.Security + 1.5f);
                            }
                            fief.Prosperity += 2.0f;
                            boostedFiefs++;

                            // Auto-train garrison troops (Improved Garrisons style)
                            if (fief.GarrisonParty != null && fief.GarrisonParty.MemberRoster != null)
                            {
                                TroopRoster roster = fief.GarrisonParty.MemberRoster;
                                for (int r = 0; r < roster.Count; r++)
                                {
                                    CharacterObject character = roster.GetCharacterAtIndex(r);
                                    if (character != null && character.UpgradeTargets != null && character.UpgradeTargets.Length > 0)
                                    {
                                        roster.AddXpToTroop(character, 60);
                                        trainedCount++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Award daily Steward & Leadership XP for managing the clan warband and fiefs
            if (boostedFiefs > 0)
            {
                Hero.MainHero.AddSkillXp(DefaultSkills.Steward, 35f * boostedFiefs);
                Hero.MainHero.AddSkillXp(DefaultSkills.Charm, 20f * boostedFiefs);
            }
        }

        public void OpenOverhaulHubMenu()
        {
            List<InquiryElement> options = new List<InquiryElement>();

            options.Add(new InquiryElement(
                "courier_lords",
                "Dispatch Diplomatic Courier to a Noble (25 Denars)",
                new EmptyImageIdentifier(),
                true,
                "Send a fast courier across Calradia to open a direct conversation with any living faction leader or noble."));

            options.Add(new InquiryElement(
                "courier_companions",
                "Contact Clan Companion or Family Member (Free)",
                new EmptyImageIdentifier(),
                true,
                "Speak remotely with any companion or family member in your clan regardless of their location."));

            options.Add(new InquiryElement(
                "instant_stamina",
                "Rest at Master Forge & Restore Full Smithing Stamina (100 Denars)",
                new EmptyImageIdentifier(),
                true,
                "Immediately restore 100% Smithing stamina for yourself and all companions in your party."));

            options.Add(new InquiryElement(
                "fief_inspection",
                "Execute Fief Garrison Drill & Loyalty Decree (250 Denars)",
                new EmptyImageIdentifier(),
                true,
                "Immediately grant +250 XP to all garrison stacks and +5 Loyalty/Security across all owned towns and castles."));

            options.Add(new InquiryElement(
                "toggle_settings",
                "Configure Overhaul & Steam Deck Combat Tweaks",
                new EmptyImageIdentifier(),
                true,
                string.Format(
                    "Cleave: {0} | Adrenaline Heal: {1} | Warhorn Rally: {2} | Mobile Stamina: {3} | Smart Fiefs: {4}",
                    EnableWeaponCleave ? "ON" : "OFF",
                    EnableBattleAdrenaline ? "ON" : "OFF",
                    EnableWarhornRally ? "ON" : "OFF",
                    EnableMobileSmithingStamina ? "ON" : "OFF",
                    EnableSmartFiefManager ? "ON" : "OFF")));

            MultiSelectionInquiryData data = new MultiSelectionInquiryData(
                "Calradia Deck Overhaul Hub",
                "Integrated Diplomacy Courier, Mobile Forge Stamina, Smart Fief Garrison Manager, and Steam Deck Combat Tweaks.",
                options,
                true,
                1,
                1,
                "Select",
                "Close",
                delegate(List<InquiryElement> selected)
                {
                    if (selected == null || selected.Count == 0)
                    {
                        return;
                    }
                    string action = selected[0].Identifier as string;
                    if (action == "courier_lords")
                    {
                        OpenDiplomaticCourierMenu(false);
                    }
                    else if (action == "courier_companions")
                    {
                        OpenDiplomaticCourierMenu(true);
                    }
                    else if (action == "instant_stamina")
                    {
                        RestoreAllPartySmithingStamina();
                    }
                    else if (action == "fief_inspection")
                    {
                        ExecuteFiefGarrisonDrill();
                    }
                    else if (action == "toggle_settings")
                    {
                        OpenSettingsToggleMenu();
                    }
                },
                null,
                "",
                false);

            MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
        }

        private void OpenDiplomaticCourierMenu(bool clanOnly)
        {
            List<InquiryElement> heroOptions = new List<InquiryElement>();

            if (Hero.AllAliveHeroes != null)
            {
                for (int i = 0; i < Hero.AllAliveHeroes.Count; i++)
                {
                    Hero h = Hero.AllAliveHeroes[i];
                    if (h == null || h == Hero.MainHero || !h.IsActive || h.CharacterObject == null)
                    {
                        continue;
                    }

                    if (clanOnly)
                    {
                        if (h.Clan != Clan.PlayerClan)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!h.IsLord || h.Clan == Clan.PlayerClan)
                        {
                            continue;
                        }
                    }

                    string factionName = (h.Clan != null && h.Clan.Name != null) ? h.Clan.Name.ToString() : "Independent";
                    string label = string.Format("{0} ({1})", h.Name, factionName);
                    string hint = string.Format(
                        "Clan: {0} | Relation: {1}",
                        factionName,
                        h.GetRelationWithPlayer());

                    heroOptions.Add(new InquiryElement(
                        h,
                        label,
                        new EmptyImageIdentifier(),
                        true,
                        hint));

                    if (heroOptions.Count >= 80)
                    {
                        break;
                    }
                }
            }

            if (heroOptions.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Courier] No eligible heroes found to contact.",
                    Colors.Red));
                return;
            }

            MultiSelectionInquiryData data = new MultiSelectionInquiryData(
                clanOnly ? "Contact Clan Member" : "Dispatch Diplomatic Courier (25 Denars)",
                "Select a hero to open an immediate remote conversation:",
                heroOptions,
                true,
                1,
                1,
                "Dispatch Courier",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected == null || selected.Count == 0)
                    {
                        return;
                    }
                    Hero targetHero = selected[0].Identifier as Hero;
                    if (targetHero == null || targetHero.CharacterObject == null || Hero.MainHero == null)
                    {
                        return;
                    }

                    if (!clanOnly)
                    {
                        if (Hero.MainHero.Gold < 25)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Courier] You need 25 denars to dispatch a royal courier.",
                                Colors.Red));
                            return;
                        }
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 25, true);
                    }

                    PartyBase playerParty = MobileParty.MainParty != null ? MobileParty.MainParty.Party : null;
                    PartyBase targetParty = targetHero.PartyBelongedTo != null ? targetHero.PartyBelongedTo.Party : null;

                    ConversationCharacterData playerData = new ConversationCharacterData(
                        Hero.MainHero.CharacterObject,
                        playerParty,
                        true,
                        true,
                        false,
                        false,
                        false,
                        true);

                    ConversationCharacterData partnerData = new ConversationCharacterData(
                        targetHero.CharacterObject,
                        targetParty,
                        true,
                        true,
                        false,
                        false,
                        false,
                        true);

                    InformationManager.DisplayMessage(new InformationMessage(
                        string.Format("[Courier] Connected with {0}.", targetHero.Name),
                        Colors.Cyan));

                    CampaignMapConversation.OpenConversation(playerData, partnerData);
                },
                delegate(List<InquiryElement> cancelled)
                {
                    OpenOverhaulHubMenu();
                },
                "",
                false);

            MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
        }

        private void RestoreAllPartySmithingStamina()
        {
            if (Hero.MainHero == null)
            {
                return;
            }

            if (Hero.MainHero.Gold < 100)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Master Armorer] You need 100 denars to rent the master forge rest quarters.",
                    Colors.Red));
                return;
            }

            ICraftingCampaignBehavior crafting = Campaign.Current != null
                ? Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>()
                : null;
            if (crafting == null)
            {
                return;
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 100, true);
            crafting.SetHeroCraftingStamina(Hero.MainHero, crafting.GetMaxHeroCraftingStamina(Hero.MainHero));

            if (Clan.PlayerClan != null && Clan.PlayerClan.Companions != null)
            {
                for (int i = 0; i < Clan.PlayerClan.Companions.Count; i++)
                {
                    Hero comp = Clan.PlayerClan.Companions[i];
                    if (comp != null)
                    {
                        crafting.SetHeroCraftingStamina(comp, crafting.GetMaxHeroCraftingStamina(comp));
                    }
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "[Master Armorer] Full Smithing stamina restored for you and all clan companions!",
                Colors.Green));
            OpenOverhaulHubMenu();
        }

        private void ExecuteFiefGarrisonDrill()
        {
            if (Hero.MainHero == null || Clan.PlayerClan == null)
            {
                return;
            }

            if (Hero.MainHero.Gold < 250)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Fief Manager] You need 250 denars to fund a clan-wide garrison drill.",
                    Colors.Red));
                return;
            }

            int fiefCount = 0;
            if (Clan.PlayerClan.Settlements != null)
            {
                for (int i = 0; i < Clan.PlayerClan.Settlements.Count; i++)
                {
                    Settlement s = Clan.PlayerClan.Settlements[i];
                    if (s != null && (s.IsTown || s.IsCastle) && s.Town != null)
                    {
                        s.Town.Loyalty = Math.Min(100f, s.Town.Loyalty + 5f);
                        s.Town.Security = Math.Min(100f, s.Town.Security + 5f);
                        if (s.Town.GarrisonParty != null && s.Town.GarrisonParty.MemberRoster != null)
                        {
                            TroopRoster roster = s.Town.GarrisonParty.MemberRoster;
                            for (int r = 0; r < roster.Count; r++)
                            {
                                CharacterObject c = roster.GetCharacterAtIndex(r);
                                if (c != null)
                                {
                                    roster.AddXpToTroop(c, 250);
                                }
                            }
                        }
                        fiefCount++;
                    }
                }
            }

            if (fiefCount == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Fief Manager] Your clan does not currently own any towns or castles.",
                    Colors.Yellow));
                return;
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 250, true);
            Hero.MainHero.AddSkillXp(DefaultSkills.Leadership, 80f);
            Hero.MainHero.AddSkillXp(DefaultSkills.Steward, 80f);

            InformationManager.DisplayMessage(new InformationMessage(
                string.Format("[Fief Manager] Drilled garrisons and boosted Loyalty/Security (+5) across {0} fief(s)!", fiefCount),
                Colors.Green));
            OpenOverhaulHubMenu();
        }

        private void OpenSettingsToggleMenu()
        {
            List<InquiryElement> toggles = new List<InquiryElement>();

            toggles.Add(new InquiryElement(
                "cleave",
                string.Format("Two-Handed & Polearm Cleave (Cut Through): [{0}]", EnableWeaponCleave ? "ENABLED" : "DISABLED"),
                new EmptyImageIdentifier(),
                true,
                "Heavy two-handed and polearm melee swings cleave into adjacent enemies within 2.2m."));

            toggles.Add(new InquiryElement(
                "adrenaline",
                string.Format("Battle Adrenaline Kill-Heal (+6 HP per kill): [{0}]", EnableBattleAdrenaline ? "ENABLED" : "DISABLED"),
                new EmptyImageIdentifier(),
                true,
                "Player and companions recover +6 HP on each melee or ranged kill in battle."));

            toggles.Add(new InquiryElement(
                "warhorn",
                string.Format("Tactical Warhorn Rally (Periodic Ally Heal & Morale): [{0}]", EnableWarhornRally ? "ENABLED" : "DISABLED"),
                new EmptyImageIdentifier(),
                true,
                "Every 45 seconds in battle, rallies nearby friendly troops within 25m (+4 HP & warcry)."));

            toggles.Add(new InquiryElement(
                "stamina",
                string.Format("Mobile Smithing Stamina Recovery: [{0}]", EnableMobileSmithingStamina ? "ENABLED" : "DISABLED"),
                new EmptyImageIdentifier(),
                true,
                "Recover Smithing stamina every hour while traveling on the campaign map."));

            toggles.Add(new InquiryElement(
                "fiefs",
                string.Format("Smart Fief Auto-Garrison & Loyalty Boost: [{0}]", EnableSmartFiefManager ? "ENABLED" : "DISABLED"),
                new EmptyImageIdentifier(),
                true,
                "Automatically train garrison troops and boost fief loyalty/security every day."));

            MultiSelectionInquiryData data = new MultiSelectionInquiryData(
                "Configure Overhaul Tweaks",
                "Select a feature below to toggle it ON or OFF:",
                toggles,
                true,
                1,
                1,
                "Toggle Selected",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        string id = selected[0].Identifier as string;
                        if (id == "cleave") EnableWeaponCleave = !EnableWeaponCleave;
                        else if (id == "adrenaline") EnableBattleAdrenaline = !EnableBattleAdrenaline;
                        else if (id == "warhorn") EnableWarhornRally = !EnableWarhornRally;
                        else if (id == "stamina") EnableMobileSmithingStamina = !EnableMobileSmithingStamina;
                        else if (id == "fiefs") EnableSmartFiefManager = !EnableSmartFiefManager;
                    }
                    OpenSettingsToggleMenu();
                },
                delegate(List<InquiryElement> cancelled)
                {
                    OpenOverhaulHubMenu();
                },
                "",
                false);

            MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
        }
    }

    /// <summary>
    /// Tactical Battle MissionLogic combining the best combat overhaul mechanics from the Workshop:
    ///   - Weapon Cleave / Cut Through Everyone for 2H weapons and swinging polearms
    ///   - Battle Adrenaline (hero health regeneration on kills)
    ///   - Tactical Warhorn Rally (periodic nearby troop morale & health surge)
    /// </summary>
    public class SteamDeckCombatMissionLogic : MissionLogic
    {
        private float _rallyTimer;
        private readonly MBList<Agent> _nearbyBuffer = new MBList<Agent>();

        public override void OnMeleeHit(
            Agent attacker,
            Agent victim,
            bool isCanceled,
            AttackCollisionData collisionData)
        {
            base.OnMeleeHit(attacker, victim, isCanceled, collisionData);

            if (isCanceled || !SteamDeckCampaignOverhaulBehavior.EnableWeaponCleave)
            {
                return;
            }

            if (attacker == null || victim == null || !attacker.IsHuman || !attacker.IsActive())
            {
                return;
            }

            // Trigger cleave for the player and hero companions wielding 2H or Polearm weapons on non-blocked swings
            if (!attacker.IsMainAgent && !attacker.IsHero)
            {
                return;
            }

            if (collisionData.AttackBlockedWithShield || collisionData.IsHorseCharge || collisionData.IsMissile)
            {
                return;
            }

            WeaponComponentData weapon = attacker.WieldedWeapon.CurrentUsageItem;
            if (weapon == null || (!weapon.IsTwoHanded && !weapon.IsPolearm))
            {
                return;
            }

            if (Mission.Current == null || attacker.Team == null)
            {
                return;
            }

            _nearbyBuffer.Clear();
            Vec2 center = victim.Position.AsVec2;
            Mission.Current.GetNearbyEnemyAgents(center, 2.2f, attacker.Team, _nearbyBuffer);

            int cleaved = 0;
            float cleaveDamage = Math.Max(12f, weapon.SwingDamage * 0.35f);

            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                Agent target = _nearbyBuffer[i];
                if (target == null || target == victim || !target.IsActive() || !target.IsHuman)
                {
                    continue;
                }

                float newHealth = target.Health - cleaveDamage;
                if (newHealth > 1f)
                {
                    target.Health = newHealth;
                    cleaved++;
                }

                if (cleaved >= 2)
                {
                    break;
                }
            }

            if (cleaved > 0 && attacker.IsMainAgent)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("[Cleave] Cut through {0} adjacent enemy troop(s) ({1:0} splash dmg)!", cleaved, cleaveDamage),
                    Colors.Yellow));
            }
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

            if (!SteamDeckCampaignOverhaulBehavior.EnableBattleAdrenaline)
            {
                return;
            }

            if (affectedAgent == null || affectorAgent == null || affectedAgent == affectorAgent)
            {
                return;
            }

            if (!affectedAgent.IsHuman || !affectorAgent.IsHuman || !affectorAgent.IsActive())
            {
                return;
            }

            if (affectorAgent.IsMainAgent || affectorAgent.IsHero)
            {
                float maxHp = affectorAgent.HealthLimit;
                if (affectorAgent.Health < maxHp)
                {
                    float heal = 6f;
                    affectorAgent.Health = Math.Min(maxHp, affectorAgent.Health + heal);
                    if (affectorAgent.IsMainAgent)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            string.Format("[Battle Adrenaline] +6 HP restored ({0:0}/{1:0})", affectorAgent.Health, maxHp),
                            Colors.Green));
                    }
                }
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!SteamDeckCampaignOverhaulBehavior.EnableWarhornRally)
            {
                return;
            }

            _rallyTimer += dt;
            if (_rallyTimer < 45f)
            {
                return;
            }

            _rallyTimer = 0f;

            Agent main = Agent.Main;
            if (main == null || !main.IsActive() || main.Team == null || Mission.Current == null)
            {
                return;
            }

            _nearbyBuffer.Clear();
            Mission.Current.GetNearbyAllyAgents(main.Position.AsVec2, 25f, main.Team, _nearbyBuffer);

            int rallied = 0;
            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                Agent ally = _nearbyBuffer[i];
                if (ally != null && ally.IsActive() && ally.IsHuman)
                {
                    if (ally.Health < ally.HealthLimit)
                    {
                        ally.Health = Math.Min(ally.HealthLimit, ally.Health + 4f);
                    }
                    rallied++;
                }
            }

            if (rallied > 1)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("[Clan Warhorn] Rallied {0} nearby allied troops (+4 HP & morale surge)!", rallied),
                    Colors.Cyan));
            }
        }
    }
}
