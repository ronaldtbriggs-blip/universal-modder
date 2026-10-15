using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SteamDeckOverhaul
{
    public class ClanArmyPointSystemBehavior : CampaignBehaviorBase
    {
        // Format: troopId|customName|ath,rid,1h,2h,pol,bow,xbow,thr|item0,item1,item2,item3,head,cape,body,gloves,leg,horse,harness
        private List<string> _savedClanTroops = new List<string>();

        private static readonly string[] ClanTroopIds = new string[]
        {
            "sdo_clan_t1_recruit",
            "sdo_clan_t2_infantry", "sdo_clan_t2_ranged",
            "sdo_clan_t3_infantry", "sdo_clan_t3_ranged", "sdo_clan_t3_cavalry",
            "sdo_clan_t4_infantry", "sdo_clan_t4_ranged", "sdo_clan_t4_cavalry",
            "sdo_clan_t5_infantry", "sdo_clan_t5_ranged", "sdo_clan_t5_cavalry",
            "sdo_clan_t6_infantry", "sdo_clan_t6_ranged", "sdo_clan_t6_cavalry"
        };

        private static readonly EquipmentIndex[] EditableSlots = new EquipmentIndex[]
        {
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3,
            EquipmentIndex.Head,
            EquipmentIndex.Cape,
            EquipmentIndex.Body,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg,
            EquipmentIndex.Horse,
            EquipmentIndex.HorseHarness
        };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("sdo_saved_clan_troops_v1", ref _savedClanTroops);
            if (_savedClanTroops == null)
            {
                _savedClanTroops = new List<string>();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RestoreSavedClanTroops();
            BindPlayerClanBasicTroop();
            RegisterMenus(starter);
        }

        private void BindPlayerClanBasicTroop()
        {
            if (Clan.PlayerClan != null && MBObjectManager.Instance != null)
            {
                CharacterObject recruit = MBObjectManager.Instance.GetObject<CharacterObject>("sdo_clan_t1_recruit");
                if (recruit != null)
                {
                    Clan.PlayerClan.BasicTroop = recruit;
                }
            }
        }

        private void RegisterMenus(CampaignGameStarter starter)
        {
            if (starter == null)
            {
                return;
            }

            starter.AddGameMenuOption(
                "town",
                "sdo_open_clan_army_architect",
                "{=!}Clan Warband Architect (Point-System Custom Troops)",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenClanArmyHub();
                },
                false,
                5,
                false,
                null);

            starter.AddGameMenuOption(
                "castle",
                "sdo_open_clan_army_architect_castle",
                "{=!}Clan Warband Architect (Point-System Custom Troops)",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenClanArmyHub();
                },
                false,
                4,
                false,
                null);
        }

        public void OpenClanArmyHub()
        {
            int clanTier = Clan.PlayerClan != null ? Clan.PlayerClan.Tier : 1;
            int leadership = Hero.MainHero != null ? Hero.MainHero.GetSkillValue(DefaultSkills.Leadership) : 50;
            int bonusPts = GetClanPointBonus();

            string desc = String.Format(
                "Clan Warband Point-System Architect — {0}\n" +
                "• Clan Tier: {1}  |  Leadership: {2}  |  Bonus Point Pool per Tier: +{3} pts\n" +
                "• Tier Point Budgets: T1 ({4}) | T2 ({5}) | T3 ({6}) | T4 ({7}) | T5 ({8}) | T6 ({9})\n\n" +
                "Customize your 15-unit Tier 1–6 Clan Troop Tree or muster troops into your army:",
                Clan.PlayerClan != null ? Clan.PlayerClan.Name.ToString() : "Player Clan",
                clanTier, leadership, bonusPts,
                GetTierMaxPoints(1), GetTierMaxPoints(2), GetTierMaxPoints(3),
                GetTierMaxPoints(4), GetTierMaxPoints(5), GetTierMaxPoints(6));

            List<InquiryElement> options = new List<InquiryElement>();
            options.Add(new InquiryElement(
                "customize_tree",
                "1. Customize Clan Troop Tree (Spend Tier Points on Skills, Gear & Forged Armor)",
                new EmptyImageIdentifier(),
                true,
                "Inspect and customize any of your 15 Tier 1–6 Clan Troops within their Point Budget."));

            options.Add(new InquiryElement(
                "recruit_warband",
                "2. Muster & Recruit Custom Clan Troops into Main Party",
                new EmptyImageIdentifier(),
                true,
                "Recruit Tier 1 Levies, Tier 3 Regulars, or Tier 6 Immortal Champions using Denars."));

            options.Add(new InquiryElement(
                "convert_recruits",
                "3. Induct & Convert All Party Recruits into Custom Clan Troops",
                new EmptyImageIdentifier(),
                true,
                "Convert standard imperial/vlandian/etc. Tier 1–2 recruits in your party into your Custom Clan Troop tree."));

            options.Add(new InquiryElement(
                "outfit_forged_set",
                "4. One-Click Outfit Tier 6 Champions with Your Custom Forged Armor",
                new EmptyImageIdentifier(),
                true,
                "Automatically equip your Tier 6 Clan Champions with your latest Master Armorer forged helmets, cuirasses, pauldrons, gauntlets, and greaves."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Clan Warband Point-System Architect",
                desc,
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
                    string cmd = selected[0].Identifier as string;
                    if (cmd == "customize_tree")
                    {
                        OpenTroopTreeSelector();
                    }
                    else if (cmd == "recruit_warband")
                    {
                        OpenRecruitWarbandMenu();
                    }
                    else if (cmd == "convert_recruits")
                    {
                        ConvertVanillaRecruitsToClanTroops();
                    }
                    else if (cmd == "outfit_forged_set")
                    {
                        OutfitTier6WithForgedArmor();
                    }
                },
                null,
                "",
                false), true, false);
        }

        private void OpenTroopTreeSelector()
        {
            if (MBObjectManager.Instance == null)
            {
                return;
            }

            List<InquiryElement> elements = new List<InquiryElement>();
            for (int i = 0; i < ClanTroopIds.Length; i++)
            {
                CharacterObject troop = MBObjectManager.Instance.GetObject<CharacterObject>(ClanTroopIds[i]);
                if (troop == null)
                {
                    continue;
                }
                int tier = GetTroopTier(troop);
                int usedPts = CalculateTotalPointsUsed(troop);
                int maxPts = GetTierMaxPoints(tier);
                string label = String.Format("[Tier {0}] {1} ({2}) — Points: {3}/{4} pts",
                    tier, troop.Name, troop.DefaultFormationClass, usedPts, maxPts);
                string hint = String.Format("Skill Cost: {0} pts | Gear Cost: {1} pts | Remaining Budget: {2} pts",
                    CalculateSkillPointsUsed(troop), CalculateGearPointsUsed(troop), Math.Max(0, maxPts - usedPts));
                elements.Add(new InquiryElement(troop, label, new EmptyImageIdentifier(), true, hint));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Select Clan Troop Node (Tiers 1 to 6)",
                "Choose a troop in your Clan Troop Tree to edit its Point-Budget Skills, Loadout, or Name:",
                elements,
                true,
                1,
                1,
                "Edit Troop",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        CharacterObject troop = selected[0].Identifier as CharacterObject;
                        if (troop != null)
                        {
                            OpenTroopEditorMenu(troop);
                        }
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenClanArmyHub(); },
                "",
                false), true, false);
        }

        private void OpenTroopEditorMenu(CharacterObject troop)
        {
            int tier = GetTroopTier(troop);
            int skillPts = CalculateSkillPointsUsed(troop);
            int gearPts = CalculateGearPointsUsed(troop);
            int usedPts = skillPts + gearPts;
            int maxPts = GetTierMaxPoints(tier);
            int remainingPts = Math.Max(0, maxPts - usedPts);

            string summary = String.Format(
                "Editing: {0} (Tier {1} {2})\n" +
                "• Point Budget: {3} / {4} pts used ({5} pts available)\n" +
                "• Breakdown: Combat Skills ({6} pts) + Equipment Loadout ({7} pts)",
                troop.Name, tier, troop.DefaultFormationClass,
                usedPts, maxPts, remainingPts, skillPts, gearPts);

            List<InquiryElement> actions = new List<InquiryElement>();
            actions.Add(new InquiryElement("edit_gear", "1. Edit Equipment Loadout (Weapons, Shield, Mount & Forged Armor)", new EmptyImageIdentifier(), true, "Swap any equipment slot using your remaining Tier Point Budget."));
            actions.Add(new InquiryElement("edit_skills", "2. Allocate Combat Skill Points (Proficiencies & Athletics/Riding)", new EmptyImageIdentifier(), true, "Raise or lower combat skills to free up or spend Tier Points."));
            actions.Add(new InquiryElement("rename_troop", "3. Rename Custom Clan Troop", new EmptyImageIdentifier(), true, "Give this troop tier a custom regiment/unit name."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                String.Format("Troop Architect: {0}", troop.Name),
                summary,
                actions,
                true,
                1,
                1,
                "Select",
                "Back to Tree",
                delegate(List<InquiryElement> selected)
                {
                    if (selected == null || selected.Count == 0)
                    {
                        return;
                    }
                    string act = selected[0].Identifier as string;
                    if (act == "edit_gear")
                    {
                        OpenTroopSlotSelector(troop);
                    }
                    else if (act == "edit_skills")
                    {
                        OpenTroopSkillAllocationMenu(troop);
                    }
                    else if (act == "rename_troop")
                    {
                        InformationManager.ShowTextInquiry(new TextInquiryData(
                            "Rename Custom Clan Troop",
                            "Enter a new regiment or unit title for " + troop.Name + ":",
                            true,
                            true,
                            "Save Name",
                            "Cancel",
                            delegate(string newName)
                            {
                                if (!String.IsNullOrEmpty(newName))
                                {
                                    ArmorSmithingBehavior.SetBackingField(troop, "_basicName", new TextObject("{=!}" + newName.Trim(), null));
                                    SaveTroopState(troop);
                                    InformationManager.DisplayMessage(new InformationMessage("Renamed unit to " + newName.Trim(), Color.FromUint(0xFF4ADE80)));
                                }
                                OpenTroopEditorMenu(troop);
                            },
                            delegate() { OpenTroopEditorMenu(troop); },
                            false,
                            null,
                            "",
                            troop.Name.ToString()), true, false);
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenTroopTreeSelector(); },
                "",
                false), true, false);
        }

        private void OpenTroopSlotSelector(CharacterObject troop)
        {
            int tier = GetTroopTier(troop);
            int maxPts = GetTierMaxPoints(tier);
            int usedPts = CalculateTotalPointsUsed(troop);
            int remPts = Math.Max(0, maxPts - usedPts);

            Equipment eq = troop.FirstBattleEquipment;
            List<InquiryElement> slots = new List<InquiryElement>();

            for (int i = 0; i < EditableSlots.Length; i++)
            {
                EquipmentIndex idx = EditableSlots[i];
                EquipmentElement elem = eq != null ? eq[idx] : EquipmentElement.Invalid;
                ItemObject curItem = elem.Item;
                int curCost = curItem != null ? CalculateSingleItemPointCost(curItem) : 0;
                string itemName = curItem != null ? curItem.Name.ToString() : "(Empty Slot)";
                string label = String.Format("{0}: {1} [{2} pts]", GetSlotDisplayName(idx), itemName, curCost);
                ImageIdentifier icon = curItem != null ? (ImageIdentifier)new ItemImageIdentifier(curItem, "") : new EmptyImageIdentifier();
                slots.Add(new InquiryElement(idx, label, icon, true, "Click to swap or unequip this slot within your Point Budget."));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                String.Format("Loadout Editor: {0} ({1} pts remaining)", troop.Name, remPts),
                "Select an equipment slot to swap its weapon, armor, or mount:",
                slots,
                true,
                1,
                1,
                "Change Slot",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        EquipmentIndex slotIdx = (EquipmentIndex)selected[0].Identifier;
                        OpenItemPickerForSlot(troop, slotIdx);
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenTroopEditorMenu(troop); },
                "",
                false), true, false);
        }

        private void OpenItemPickerForSlot(CharacterObject troop, EquipmentIndex slotIdx)
        {
            int tier = GetTroopTier(troop);
            int maxPts = GetTierMaxPoints(tier);
            int usedPts = CalculateTotalPointsUsed(troop);

            Equipment eq = troop.FirstBattleEquipment;
            ItemObject currentItem = (eq != null && !eq[slotIdx].IsEmpty) ? eq[slotIdx].Item : null;
            int currentSlotCost = currentItem != null ? CalculateSingleItemPointCost(currentItem) : 0;
            int availableForSlot = Math.Max(0, maxPts - (usedPts - currentSlotCost));

            List<InquiryElement> choices = new List<InquiryElement>();
            choices.Add(new InquiryElement("unequip", String.Format("[Unequip Slot] (0 pts — Frees {0} pts)", currentSlotCost), new EmptyImageIdentifier(), true, "Clear this slot and return its points to your budget."));

            List<ItemObject> candidates = GetCandidateItemsForSlot(slotIdx, tier);
            for (int i = 0; i < candidates.Count; i++)
            {
                ItemObject item = candidates[i];
                int cost = CalculateSingleItemPointCost(item);
                bool canAfford = cost <= availableForSlot;
                bool isCustomForged = item.StringId.StartsWith("sdo_", StringComparison.Ordinal);
                string prefix = isCustomForged ? "[FORGED MASTERWORK] " : "";
                string label = String.Format("{0}{1} (T{2}) — {3} pts", prefix, item.Name, (int)item.Tier + 1, cost);
                string hint = canAfford
                    ? String.Format("Point Cost: {0} pts (Fits your {1} pt available budget)", cost, availableForSlot)
                    : String.Format("Requires {0} pts (You have {1} pts available — lower another slot or skill first)", cost, availableForSlot);
                choices.Add(new InquiryElement(item, label, new ItemImageIdentifier(item, ""), canAfford, hint));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                String.Format("Select {0} (Available Budget: {1} pts)", GetSlotDisplayName(slotIdx), availableForSlot),
                "Custom-forged armors appear at the top! Items exceeding your point budget are greyed out:",
                choices,
                true,
                1,
                1,
                "Equip Item",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        object idObj = selected[0].Identifier;
                        if (idObj is string && (string)idObj == "unequip")
                        {
                            SetTroopEquipmentSlot(troop, slotIdx, null);
                        }
                        else if (idObj is ItemObject)
                        {
                            SetTroopEquipmentSlot(troop, slotIdx, (ItemObject)idObj);
                        }
                        SaveTroopState(troop);
                    }
                    OpenTroopSlotSelector(troop);
                },
                delegate(List<InquiryElement> cancel) { OpenTroopSlotSelector(troop); },
                "",
                true), true, false);
        }

        private void OpenTroopSkillAllocationMenu(CharacterObject troop)
        {
            int tier = GetTroopTier(troop);
            int maxPts = GetTierMaxPoints(tier);
            int usedPts = CalculateTotalPointsUsed(troop);
            int remPts = Math.Max(0, maxPts - usedPts);
            int skillCap = 60 + tier * 35;

            SkillObject[] combatSkills = GetEditableSkills();
            List<InquiryElement> elements = new List<InquiryElement>();

            for (int i = 0; i < combatSkills.Length; i++)
            {
                SkillObject sk = combatSkills[i];
                int curVal = troop.GetSkillValue(sk);
                bool canRaise = curVal + 25 <= skillCap && remPts >= 6;
                string labelPlus = String.Format("+25 {0} (Current: {1}/{2}) [Costs 6 pts]", sk.Name, curVal, skillCap);
                elements.Add(new InquiryElement("+" + sk.StringId, labelPlus, new EmptyImageIdentifier(), canRaise, "Increase this combat proficiency by +25."));

                if (curVal >= 35)
                {
                    string labelMinus = String.Format("-25 {0} (Current: {1}) [Refunds 6 pts]", sk.Name, curVal);
                    elements.Add(new InquiryElement("-" + sk.StringId, labelMinus, new EmptyImageIdentifier(), true, "Lower this combat proficiency by 25 to free up 6 Tier Points."));
                }
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                String.Format("Combat Skills: {0} ({1} pts available)", troop.Name, remPts),
                String.Format("Tier {0} Skill Cap is {1}. Spend or refund points across combat proficiencies:", tier, skillCap),
                elements,
                true,
                1,
                1,
                "Apply Change",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        string token = selected[0].Identifier as string;
                        if (!String.IsNullOrEmpty(token))
                        {
                            int delta = token.StartsWith("+", StringComparison.Ordinal) ? 25 : -25;
                            string skId = token.Substring(1);
                            SkillObject targetSkill = combatSkills.FirstOrDefault(s => s.StringId == skId);
                            if (targetSkill != null)
                            {
                                ModifyTroopSkill(troop, targetSkill, delta);
                                SaveTroopState(troop);
                            }
                        }
                    }
                    OpenTroopSkillAllocationMenu(troop);
                },
                delegate(List<InquiryElement> cancel) { OpenTroopEditorMenu(troop); },
                "",
                false), true, false);
        }

        private void OpenRecruitWarbandMenu()
        {
            if (MBObjectManager.Instance == null || MobileParty.MainParty == null)
            {
                return;
            }

            int gold = Hero.MainHero != null ? Hero.MainHero.Gold : 0;
            List<InquiryElement> packages = new List<InquiryElement>();

            packages.Add(new InquiryElement("t1_10", "Recruit 10x Tier 1 Clan Levy Recruits (500 Denars)", new EmptyImageIdentifier(), gold >= 500, "Add 10 Tier 1 Custom Clan Recruits to your party."));
            packages.Add(new InquiryElement("t3_6", "Muster 6x Tier 3 Clan Regulars (2 Inf, 2 Ranged, 2 Cav — 1,500 Denars)", new EmptyImageIdentifier(), gold >= 1500, "Add 6 battle-ready Tier 3 Custom Clan soldiers to your party."));
            packages.Add(new InquiryElement("t5_5", "Call 5x Tier 5 Clan Veterans (2 Ironclad, 2 Bowman, 1 Cataphract — 3,000 Denars)", new EmptyImageIdentifier(), gold >= 3000, "Muster 5 veteran Tier 5 Custom Clan soldiers."));
            packages.Add(new InquiryElement("t6_3", "Summon 3x Tier 6 Clan Champions (1 Immortal, 1 Sentinel, 1 Royal Cataphract — 4,500 Denars)", new EmptyImageIdentifier(), gold >= 4500, "Summon your elite Tier 6 Clan Champions wearing custom forged armor."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Muster Custom Clan Warband",
                String.Format("Your Gold: {0} Denars. Select a Clan Warband detachment to muster into your party:", gold),
                packages,
                true,
                1,
                1,
                "Muster Troops",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        string pkg = selected[0].Identifier as string;
                        if (pkg == "t1_10" && gold >= 500)
                        {
                            Hero.MainHero.ChangeHeroGold(-500);
                            AddClanTroopToParty("sdo_clan_t1_recruit", 10);
                        }
                        else if (pkg == "t3_6" && gold >= 1500)
                        {
                            Hero.MainHero.ChangeHeroGold(-1500);
                            AddClanTroopToParty("sdo_clan_t3_infantry", 2);
                            AddClanTroopToParty("sdo_clan_t3_ranged", 2);
                            AddClanTroopToParty("sdo_clan_t3_cavalry", 2);
                        }
                        else if (pkg == "t5_5" && gold >= 3000)
                        {
                            Hero.MainHero.ChangeHeroGold(-3000);
                            AddClanTroopToParty("sdo_clan_t5_infantry", 2);
                            AddClanTroopToParty("sdo_clan_t5_ranged", 2);
                            AddClanTroopToParty("sdo_clan_t5_cavalry", 1);
                        }
                        else if (pkg == "t6_3" && gold >= 4500)
                        {
                            Hero.MainHero.ChangeHeroGold(-4500);
                            AddClanTroopToParty("sdo_clan_t6_infantry", 1);
                            AddClanTroopToParty("sdo_clan_t6_ranged", 1);
                            AddClanTroopToParty("sdo_clan_t6_cavalry", 1);
                        }
                        InformationManager.DisplayMessage(new InformationMessage("[Clan Warband] Custom Clan soldiers have joined your party!", Color.FromUint(0xFF4ADE80)));
                    }
                    OpenClanArmyHub();
                },
                delegate(List<InquiryElement> cancel) { OpenClanArmyHub(); },
                "",
                false), true, false);
        }

        private void ConvertVanillaRecruitsToClanTroops()
        {
            if (MobileParty.MainParty == null || MobileParty.MainParty.MemberRoster == null || MBObjectManager.Instance == null)
            {
                return;
            }

            CharacterObject clanRecruit = MBObjectManager.Instance.GetObject<CharacterObject>("sdo_clan_t1_recruit");
            CharacterObject clanFootman = MBObjectManager.Instance.GetObject<CharacterObject>("sdo_clan_t2_infantry");
            if (clanRecruit == null || clanFootman == null)
            {
                return;
            }

            TroopRoster roster = MobileParty.MainParty.MemberRoster;
            int convertedCount = 0;

            for (int i = roster.Count - 1; i >= 0; i--)
            {
                TroopRosterElement elem = roster.GetElementCopyAtIndex(i);
                CharacterObject ch = elem.Character;
                if (ch == null || ch.IsHero || ch.StringId.StartsWith("sdo_clan_", StringComparison.Ordinal))
                {
                    continue;
                }
                if (ch.Tier <= 2 && elem.Number > 0)
                {
                    int num = elem.Number;
                    roster.AddToCounts(ch, -num, false, 0, 0, true, -1);
                    CharacterObject target = ch.Tier <= 1 ? clanRecruit : clanFootman;
                    roster.AddToCounts(target, num, false, 0, 0, true, -1);
                    convertedCount += num;
                }
            }

            if (convertedCount > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    String.Format("[Clan Warband] Inducted {0} recruits into your Custom Clan Army!", convertedCount),
                    Color.FromUint(0xFF4ADE80)));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No vanilla Tier 1–2 recruits found in your party to convert.",
                    Color.FromUint(0xFFFACC15)));
            }
            OpenClanArmyHub();
        }

        private void OutfitTier6WithForgedArmor()
        {
            if (MBObjectManager.Instance == null)
            {
                return;
            }

            ItemObject head = MBObjectManager.Instance.GetObject<ItemObject>("sdo_custom_forged_head_1") ?? MBObjectManager.Instance.GetObject<ItemObject>("sdo_forged_head_empire");
            ItemObject cape = MBObjectManager.Instance.GetObject<ItemObject>("sdo_custom_forged_cape_1") ?? MBObjectManager.Instance.GetObject<ItemObject>("sdo_forged_cape_empire");
            ItemObject body = MBObjectManager.Instance.GetObject<ItemObject>("sdo_custom_forged_body_1") ?? MBObjectManager.Instance.GetObject<ItemObject>("sdo_forged_body_empire");
            ItemObject hand = MBObjectManager.Instance.GetObject<ItemObject>("sdo_custom_forged_hand_1") ?? MBObjectManager.Instance.GetObject<ItemObject>("sdo_forged_hand_empire");
            ItemObject leg = MBObjectManager.Instance.GetObject<ItemObject>("sdo_custom_forged_leg_1") ?? MBObjectManager.Instance.GetObject<ItemObject>("sdo_forged_leg_empire");

            string[] t6Ids = new string[] { "sdo_clan_t6_infantry", "sdo_clan_t6_ranged", "sdo_clan_t6_cavalry" };
            for (int i = 0; i < t6Ids.Length; i++)
            {
                CharacterObject troop = MBObjectManager.Instance.GetObject<CharacterObject>(t6Ids[i]);
                if (troop == null)
                {
                    continue;
                }
                if (head != null) SetTroopEquipmentSlot(troop, EquipmentIndex.Head, head);
                if (cape != null) SetTroopEquipmentSlot(troop, EquipmentIndex.Cape, cape);
                if (body != null) SetTroopEquipmentSlot(troop, EquipmentIndex.Body, body);
                if (hand != null) SetTroopEquipmentSlot(troop, EquipmentIndex.Gloves, hand);
                if (leg != null) SetTroopEquipmentSlot(troop, EquipmentIndex.Leg, leg);
                SaveTroopState(troop);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "[Clan Warband] All Tier 6 Clan Champions are now outfitted with your Custom Forged Masterwork Armor!",
                Color.FromUint(0xFFFACC15)));
            OpenClanArmyHub();
        }

        private void AddClanTroopToParty(string troopId, int count)
        {
            if (MBObjectManager.Instance == null || MobileParty.MainParty == null)
            {
                return;
            }
            CharacterObject troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
            if (troop != null)
            {
                MobileParty.MainParty.MemberRoster.AddToCounts(troop, count, false, 0, 0, true, -1);
            }
        }

        public static int GetTroopTier(CharacterObject troop)
        {
            if (troop == null) return 1;
            return Math.Max(1, Math.Min(6, troop.Tier));
        }

        public static int GetClanPointBonus()
        {
            int clanTier = Clan.PlayerClan != null ? Clan.PlayerClan.Tier : 1;
            int leadership = Hero.MainHero != null ? Hero.MainHero.GetSkillValue(DefaultSkills.Leadership) : 50;
            return clanTier * 15 + (leadership / 5);
        }

        public static int GetTierMaxPoints(int tier)
        {
            int[] baseBudgets = new int[] { 0, 100, 160, 240, 340, 460, 600 };
            int t = Math.Max(1, Math.Min(6, tier));
            return baseBudgets[t] + GetClanPointBonus();
        }

        public static int CalculateTotalPointsUsed(CharacterObject troop)
        {
            return CalculateSkillPointsUsed(troop) + CalculateGearPointsUsed(troop);
        }

        public static int CalculateSkillPointsUsed(CharacterObject troop)
        {
            if (troop == null) return 0;
            SkillObject[] skills = GetEditableSkills();
            int totalSkill = 0;
            for (int i = 0; i < skills.Length; i++)
            {
                totalSkill += Math.Max(0, troop.GetSkillValue(skills[i]) - 15);
            }
            return totalSkill / 5;
        }

        public static int CalculateGearPointsUsed(CharacterObject troop)
        {
            if (troop == null || troop.FirstBattleEquipment == null) return 0;
            Equipment eq = troop.FirstBattleEquipment;
            int total = 0;
            for (int i = 0; i < EditableSlots.Length; i++)
            {
                EquipmentElement elem = eq[EditableSlots[i]];
                if (!elem.IsEmpty && elem.Item != null)
                {
                    total += CalculateSingleItemPointCost(elem.Item);
                }
            }
            return total;
        }

        public static int CalculateSingleItemPointCost(ItemObject item)
        {
            if (item == null) return 0;
            int tier = Math.Max(0, (int)item.Tier);
            int baseCost = (tier + 1) * 6;
            if (item.ArmorComponent != null)
            {
                int armorSum = item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + item.ArmorComponent.ArmArmor + item.ArmorComponent.LegArmor;
                baseCost += armorSum / 5;
            }
            else if (item.PrimaryWeapon != null)
            {
                baseCost += 4;
            }
            else if (item.HorseComponent != null)
            {
                baseCost += 8;
            }
            return Math.Max(4, Math.Min(65, baseCost));
        }

        private static SkillObject[] GetEditableSkills()
        {
            return new SkillObject[]
            {
                DefaultSkills.Athletics,
                DefaultSkills.Riding,
                DefaultSkills.OneHanded,
                DefaultSkills.TwoHanded,
                DefaultSkills.Polearm,
                DefaultSkills.Bow,
                DefaultSkills.Crossbow,
                DefaultSkills.Throwing
            };
        }

        private List<ItemObject> GetCandidateItemsForSlot(EquipmentIndex slotIdx, int troopTier)
        {
            List<ItemObject> list = new List<ItemObject>();
            if (MBObjectManager.Instance == null)
            {
                return list;
            }

            // First add our custom forged armors if matching slot
            List<ItemObject> forged = ArmorSmithingBehavior.GetAllForgedCustomArmors();
            for (int i = 0; i < forged.Count; i++)
            {
                if (DoesItemFitSlot(forged[i], slotIdx))
                {
                    list.Add(forged[i]);
                }
            }

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            List<ItemObject> standard = new List<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject it = all[i];
                if (it == null || it.IsCraftedByPlayer || it.StringId.StartsWith("sdo_", StringComparison.Ordinal))
                {
                    continue;
                }
                if ((int)it.Tier > troopTier + 1)
                {
                    continue;
                }
                if (DoesItemFitSlot(it, slotIdx))
                {
                    standard.Add(it);
                }
            }

            standard.Sort(delegate(ItemObject a, ItemObject b)
            {
                return b.Value.CompareTo(a.Value);
            });

            int limit = Math.Min(35, standard.Count);
            for (int i = 0; i < limit; i++)
            {
                list.Add(standard[i]);
            }
            return list;
        }

        private bool DoesItemFitSlot(ItemObject item, EquipmentIndex slotIdx)
        {
            if (item == null) return false;
            ItemObject.ItemTypeEnum t = item.Type;
            if (slotIdx == EquipmentIndex.Head) return t == ItemObject.ItemTypeEnum.HeadArmor;
            if (slotIdx == EquipmentIndex.Cape) return t == ItemObject.ItemTypeEnum.Cape;
            if (slotIdx == EquipmentIndex.Body) return t == ItemObject.ItemTypeEnum.BodyArmor;
            if (slotIdx == EquipmentIndex.Gloves) return t == ItemObject.ItemTypeEnum.HandArmor;
            if (slotIdx == EquipmentIndex.Leg) return t == ItemObject.ItemTypeEnum.LegArmor;
            if (slotIdx == EquipmentIndex.Horse) return t == ItemObject.ItemTypeEnum.Horse;
            if (slotIdx == EquipmentIndex.HorseHarness) return t == ItemObject.ItemTypeEnum.HorseHarness;
            if (slotIdx >= EquipmentIndex.Weapon0 && slotIdx <= EquipmentIndex.Weapon3)
            {
                return t == ItemObject.ItemTypeEnum.OneHandedWeapon ||
                       t == ItemObject.ItemTypeEnum.TwoHandedWeapon ||
                       t == ItemObject.ItemTypeEnum.Polearm ||
                       t == ItemObject.ItemTypeEnum.Shield ||
                       t == ItemObject.ItemTypeEnum.Bow ||
                       t == ItemObject.ItemTypeEnum.Crossbow ||
                       t == ItemObject.ItemTypeEnum.Arrows ||
                       t == ItemObject.ItemTypeEnum.Bolts ||
                       t == ItemObject.ItemTypeEnum.Thrown;
            }
            return false;
        }

        private string GetSlotDisplayName(EquipmentIndex idx)
        {
            if (idx == EquipmentIndex.Weapon0) return "Weapon Slot 1";
            if (idx == EquipmentIndex.Weapon1) return "Weapon Slot 2";
            if (idx == EquipmentIndex.Weapon2) return "Weapon Slot 3";
            if (idx == EquipmentIndex.Weapon3) return "Weapon Slot 4";
            if (idx == EquipmentIndex.Head) return "Head Armor";
            if (idx == EquipmentIndex.Cape) return "Shoulder / Cape";
            if (idx == EquipmentIndex.Body) return "Body Armor";
            if (idx == EquipmentIndex.Gloves) return "Hand Armor";
            if (idx == EquipmentIndex.Leg) return "Leg Armor";
            if (idx == EquipmentIndex.Horse) return "Warhorse / Mount";
            if (idx == EquipmentIndex.HorseHarness) return "Mount Barding";
            return idx.ToString();
        }

        private void SetTroopEquipmentSlot(CharacterObject troop, EquipmentIndex slotIdx, ItemObject newItem)
        {
            if (troop == null || troop.FirstBattleEquipment == null)
            {
                return;
            }

            List<Equipment> battleList = troop.BattleEquipments.ToList();
            List<Equipment> civList = troop.CivilianEquipments.ToList();
            EquipmentElement newElem = newItem != null ? new EquipmentElement(newItem, null, null, false) : EquipmentElement.Invalid;

            if (battleList.Count > 0)
            {
                battleList[0][slotIdx] = newElem;
            }

            List<Equipment> combined = new List<Equipment>();
            combined.AddRange(battleList);
            combined.AddRange(civList);

            MBEquipmentRoster newRoster = new MBEquipmentRoster();
            ArmorSmithingBehavior.SetBackingField(newRoster, "_equipments", new MBList<Equipment>(combined));
            ArmorSmithingBehavior.SetBackingField(troop, "_equipmentRoster", newRoster);
            troop.InitializeEquipmentsOnLoad(troop);
        }

        private void ModifyTroopSkill(CharacterObject troop, SkillObject targetSkill, int delta)
        {
            if (troop == null || targetSkill == null || MBObjectManager.Instance == null)
            {
                return;
            }

            SkillObject[] allSkills = GetEditableSkills();
            MBCharacterSkills newSkillsContainer = MBObjectManager.Instance.CreateObject<MBCharacterSkills>(troop.StringId);
            for (int i = 0; i < allSkills.Length; i++)
            {
                SkillObject sk = allSkills[i];
                int val = troop.GetSkillValue(sk);
                if (sk == targetSkill)
                {
                    val = Math.Max(5, Math.Min(300, val + delta));
                }
                newSkillsContainer.Skills.SetPropertyValue(sk, val);
            }
            ArmorSmithingBehavior.SetBackingField(troop, "DefaultCharacterSkills", newSkillsContainer);
        }

        private void SaveTroopState(CharacterObject troop)
        {
            if (troop == null)
            {
                return;
            }

            SkillObject[] skills = GetEditableSkills();
            string[] skillVals = new string[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                skillVals[i] = troop.GetSkillValue(skills[i]).ToString();
            }

            Equipment eq = troop.FirstBattleEquipment;
            string[] gearIds = new string[EditableSlots.Length];
            for (int i = 0; i < EditableSlots.Length; i++)
            {
                EquipmentElement elem = eq != null ? eq[EditableSlots[i]] : EquipmentElement.Invalid;
                gearIds[i] = (!elem.IsEmpty && elem.Item != null) ? elem.Item.StringId : "";
            }

            string cleanName = (troop.Name != null ? troop.Name.ToString() : troop.StringId).Replace("|", "-");
            string record = String.Format("{0}|{1}|{2}|{3}",
                troop.StringId,
                cleanName,
                String.Join(",", skillVals),
                String.Join(",", gearIds));

            _savedClanTroops.RemoveAll(s => s.StartsWith(troop.StringId + "|", StringComparison.Ordinal));
            _savedClanTroops.Add(record);
        }

        private void RestoreSavedClanTroops()
        {
            if (_savedClanTroops == null || MBObjectManager.Instance == null)
            {
                return;
            }

            SkillObject[] skills = GetEditableSkills();
            for (int i = 0; i < _savedClanTroops.Count; i++)
            {
                string[] parts = _savedClanTroops[i].Split('|');
                if (parts.Length < 4)
                {
                    continue;
                }

                CharacterObject troop = MBObjectManager.Instance.GetObject<CharacterObject>(parts[0]);
                if (troop == null)
                {
                    continue;
                }

                if (!String.IsNullOrEmpty(parts[1]))
                {
                    ArmorSmithingBehavior.SetBackingField(troop, "_basicName", new TextObject("{=!}" + parts[1], null));
                }

                string[] skillTokens = parts[2].Split(',');
                if (skillTokens.Length == skills.Length)
                {
                    MBCharacterSkills newSkillsContainer = MBObjectManager.Instance.CreateObject<MBCharacterSkills>(troop.StringId);
                    for (int s = 0; s < skills.Length; s++)
                    {
                        int v;
                        if (Int32.TryParse(skillTokens[s], out v))
                        {
                            newSkillsContainer.Skills.SetPropertyValue(skills[s], v);
                        }
                    }
                    ArmorSmithingBehavior.SetBackingField(troop, "DefaultCharacterSkills", newSkillsContainer);
                }

                string[] gearTokens = parts[3].Split(',');
                if (gearTokens.Length == EditableSlots.Length)
                {
                    for (int g = 0; g < EditableSlots.Length; g++)
                    {
                        string itemId = gearTokens[g];
                        ItemObject item = !String.IsNullOrEmpty(itemId) ? MBObjectManager.Instance.GetObject<ItemObject>(itemId) : null;
                        SetTroopEquipmentSlot(troop, EditableSlots[g], item);
                    }
                }
            }
        }
    }
}
