using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
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
    public class ArmorSmithingBehavior : CampaignBehaviorBase
    {
        // Format per entry: slotItemId|customName|meshName|head|body|leg|arm|weight|value
        private List<string> _savedForgedArmors = new List<string>();

        // Current 4-part forge session state
        private ItemObject.ItemTypeEnum _selectedSlotType = ItemObject.ItemTypeEnum.BodyArmor;
        private string _selectedCultureCode = "empire";
        private ItemObject _selectedChassisTemplate;
        private int _selectedPlatingIndex;
        private int _selectedLiningIndex;

        private static readonly string[] CustomForgeSlotIds = new string[]
        {
            "sdo_custom_forged_head_1", "sdo_custom_forged_head_2",
            "sdo_custom_forged_cape_1", "sdo_custom_forged_cape_2",
            "sdo_custom_forged_body_1", "sdo_custom_forged_body_2",
            "sdo_custom_forged_hand_1", "sdo_custom_forged_hand_2",
            "sdo_custom_forged_leg_1", "sdo_custom_forged_leg_2",
            "sdo_custom_forged_harness_1", "sdo_custom_forged_harness_2",
            "sdo_forged_head_empire", "sdo_forged_cape_empire", "sdo_forged_body_empire",
            "sdo_forged_hand_empire", "sdo_forged_leg_empire", "sdo_forged_harness_empire",
            "sdo_forged_head_vlandia", "sdo_forged_cape_vlandia", "sdo_forged_body_vlandia",
            "sdo_forged_hand_vlandia", "sdo_forged_leg_vlandia", "sdo_forged_harness_vlandia"
        };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("sdo_saved_forged_armors_v1", ref _savedForgedArmors);
            if (_savedForgedArmors == null)
            {
                _savedForgedArmors = new List<string>();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RestoreSavedForgedArmors();
            RegisterMenus(starter);
        }

        private void RegisterMenus(CampaignGameStarter starter)
        {
            if (starter == null)
            {
                return;
            }

            starter.AddGameMenuOption(
                "town",
                "sdo_open_armor_forge",
                "{=!}Master Armorer's Forge (Craft & Smelt Armor)",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenArmorForgeHub();
                },
                false,
                4,
                false,
                null);
        }

        public void OpenArmorForgeHub()
        {
            Hero smith = Hero.MainHero;
            int smithingSkill = smith != null ? smith.GetSkillValue(DefaultSkills.Crafting) : 50;
            int stamina = GetHeroStamina(smith);
            int maxStamina = GetHeroMaxStamina(smith);

            int charcoal = GetMaterialCount("charcoal");
            int iron3 = GetMaterialCount("ironIngot3");
            int steel4 = GetMaterialCount("ironIngot4");
            int fineSteel5 = GetMaterialCount("ironIngot5");
            int thamaskene6 = GetMaterialCount("ironIngot6");

            string desc = String.Format(
                "Master Armorer's Anvil — {0}\n" +
                "• Smithing Skill: {1}  |  Stamina: {2}/{3}\n" +
                "• Materials: Charcoal ({4}) | Iron ({5}) | Steel ({6}) | Fine Steel ({7}) | Thamaskene ({8})\n\n" +
                "Select an operation at the Armor Forge:",
                smith != null ? smith.Name.ToString() : "Player",
                smithingSkill, stamina, maxStamina,
                charcoal, iron3, steel4, fineSteel5, thamaskene6);

            List<InquiryElement> options = new List<InquiryElement>();
            options.Add(new InquiryElement(
                "forge_new",
                "1. Forge Custom Armor (4-Part Modular Assembly)",
                new EmptyImageIdentifier(),
                true,
                "Assemble a custom Helmet, Pauldrons, Cuirass, Gauntlets, Greaves, or Horse Barding from cultural 3D skins, outer plating, and inner lining."));

            options.Add(new InquiryElement(
                "temper_equipped",
                "2. Temper & Masterwork Reforge Equipped Armor",
                new EmptyImageIdentifier(),
                true,
                "Reinforce the armor your Hero is currently wearing using Fine Steel and Thamaskene Steel."));

            options.Add(new InquiryElement(
                "smelt_armor",
                "3. Smelt Inventory Armor into Smithing Ingots",
                new EmptyImageIdentifier(),
                true,
                "Break down looted or unwanted armor pieces into Charcoal, Iron, Steel, Fine Steel, and Thamaskene ingots + Smithing XP."));

            options.Add(new InquiryElement(
                "restock_materials",
                "4. Requisition Master Smithing Ingot Bundle (1,500 Denars)",
                new EmptyImageIdentifier(),
                Hero.MainHero != null && Hero.MainHero.Gold >= 1500,
                "Purchase 10 Charcoal, 6 Iron, 6 Steel, 4 Fine Steel, and 2 Thamaskene Steel from the local guild."));

            options.Add(new InquiryElement(
                "rest_stamina",
                "5. Quench & Rest at the Forge (Restore 100% Smithing Stamina)",
                new EmptyImageIdentifier(),
                true,
                "Restore full Smithing Stamina immediately so you can continue forging."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Master Armorer's Forge",
                desc,
                options,
                true,
                1,
                1,
                "Select",
                "Leave Forge",
                delegate(List<InquiryElement> selected)
                {
                    if (selected == null || selected.Count == 0)
                    {
                        return;
                    }
                    string cmd = selected[0].Identifier as string;
                    if (cmd == "forge_new")
                    {
                        OpenStage1SelectSlot();
                    }
                    else if (cmd == "temper_equipped")
                    {
                        OpenTemperEquippedArmorMenu();
                    }
                    else if (cmd == "smelt_armor")
                    {
                        OpenSmeltArmorMenu();
                    }
                    else if (cmd == "restock_materials")
                    {
                        BuySmithingBundle();
                    }
                    else if (cmd == "rest_stamina")
                    {
                        SetHeroStamina(Hero.MainHero, GetHeroMaxStamina(Hero.MainHero));
                        InformationManager.DisplayMessage(new InformationMessage("Smithing stamina fully restored at the forge.", Color.FromUint(0xFF4ADE80)));
                        OpenArmorForgeHub();
                    }
                },
                null,
                "",
                false), true, false);
        }

        private void OpenStage1SelectSlot()
        {
            List<InquiryElement> slots = new List<InquiryElement>();
            slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.HeadArmor, "Head Armor (Helmets, Great Helms, Coifs & Masks)", new EmptyImageIdentifier(), true, "Forge custom head protection."));
            slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.Cape, "Shoulder Armor (Pauldrons, Mantles, Fur & Scale Cloaks)", new EmptyImageIdentifier(), true, "Forge custom shoulder and neck protection."));
            slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.BodyArmor, "Body Armor (Cuirasses, Hauberks, Lamellar & Coat-of-Plates)", new EmptyImageIdentifier(), true, "Forge custom torso, arm, and leg protection."));
            slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.HandArmor, "Hand Armor (Plate Gauntlets, Mail Mittens & Vambraces)", new EmptyImageIdentifier(), true, "Forge custom hand and forearm protection."));
            slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.LegArmor, "Leg Armor (Plated Greaves, Mail Chausses & Warlord Boots)", new EmptyImageIdentifier(), true, "Forge custom leg and foot protection."));
            slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.HorseHarness, "Mount Barding (Cataphract Scale, Half-Chain & Plate Harness)", new EmptyImageIdentifier(), true, "Forge custom warhorse armor."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Step 1/4: Select Armor Slot",
                "Choose the type of armor piece you want to forge on the anvil:",
                slots,
                true,
                1,
                1,
                "Next: Cultural Style",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        _selectedSlotType = (ItemObject.ItemTypeEnum)selected[0].Identifier;
                        OpenStage1BSelectCulture();
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenArmorForgeHub(); },
                "",
                false), true, false);
        }

        private void OpenStage1BSelectCulture()
        {
            List<InquiryElement> cultures = new List<InquiryElement>();
            cultures.Add(new InquiryElement("empire", "Imperial (Cataphract Lamellar, Scale & Plated Steel)", new EmptyImageIdentifier(), true, "Balanced high torso protection & regal Imperial aesthetics."));
            cultures.Add(new InquiryElement("vlandia", "Vlandian (Chivalric Plate, Visored Bascinets & Heavy Mail)", new EmptyImageIdentifier(), true, "Maximum heavy plate defense for shock knights and vanguard infantry."));
            cultures.Add(new InquiryElement("sturgia", "Sturgian (Northern Huscarl Hauberks, Goggled Helms & Fur)", new EmptyImageIdentifier(), true, "Superior arm and shoulder coverage built for shieldwalls."));
            cultures.Add(new InquiryElement("battania", "Battanian (Highland Warlord Brigandine, Bronze & Wolf Pelts)", new EmptyImageIdentifier(), true, "High mobility and lighter weight for ambushes and archers."));
            cultures.Add(new InquiryElement("khuzait", "Khuzait (Steppe Khanate Masked Helms & Articulated Lamellar)", new EmptyImageIdentifier(), true, "Flexible articulated armor ideal for mounted archery and lancers."));
            cultures.Add(new InquiryElement("aserai", "Aserai (Sultanate Gilded Mail, Closed Helms & Desert Scale)", new EmptyImageIdentifier(), true, "Heat-tempered mail-and-plate with high mobility."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Step 1/4: Select Cultural Tradition & Skin Archive",
                "Choose the cultural armor tradition to browse its 3D skinned meshes:",
                cultures,
                true,
                1,
                1,
                "Next: Pick 3D Chassis",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        _selectedCultureCode = selected[0].Identifier as string;
                        OpenStage1CSelectChassisModel();
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenStage1SelectSlot(); },
                "",
                false), true, false);
        }

        private void OpenStage1CSelectChassisModel()
        {
            List<ItemObject> candidates = GetValidArmorModels(_selectedSlotType, _selectedCultureCode);
            List<InquiryElement> elements = new List<InquiryElement>();

            for (int i = 0; i < candidates.Count; i++)
            {
                ItemObject item = candidates[i];
                ArmorComponent ac = item.ArmorComponent;
                if (ac == null)
                {
                    continue;
                }
                string label = String.Format("{0} [Base: H:{1} B:{2} A:{3} L:{4} | Wt:{5:0.0}]",
                    item.Name != null ? item.Name.ToString() : item.StringId,
                    ac.HeadArmor, ac.BodyArmor, ac.ArmArmor, ac.LegArmor, item.Weight);
                string hint = String.Format("3D Mesh: {0} | Material: {1} | Base Value: {2}",
                    item.MultiMeshName, ac.MaterialType, item.Value);
                elements.Add(new InquiryElement(item, label, new ItemImageIdentifier(item, ""), true, hint));
            }

            if (elements.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("No matching armor templates found for that filter.", Color.FromUint(0xFFF87171)));
                OpenStage1SelectSlot();
                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Step 1/4: Select Core 3D Armor Chassis & Skin",
                "Pick the base 3D armor model & skin to shape on the anvil (search enabled):",
                elements,
                true,
                1,
                1,
                "Next: Outer Plating",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        _selectedChassisTemplate = selected[0].Identifier as ItemObject;
                        OpenStage2SelectPlating();
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenStage1BSelectCulture(); },
                "",
                true), true, false);
        }

        private void OpenStage2SelectPlating()
        {
            List<InquiryElement> platings = new List<InquiryElement>();
            platings.Add(new InquiryElement(0, "1. Standard Wrought Shell (+4 Primary Armor | +0.0 Wt | Cost: 2 Iron)", new EmptyImageIdentifier(), true, "Balanced traditional forging."));
            platings.Add(new InquiryElement(1, "2. Articulated Overlapping Lames (+8 Primary, +3 Limb | +0.5 Wt | Cost: 3 Steel)", new EmptyImageIdentifier(), true, "Sliding steel lames reinforce joints and torso."));
            platings.Add(new InquiryElement(2, "3. Double-Riveted Mail & Scale Backing (+11 Primary, +5 Limb | +1.2 Wt | Cost: 4 Steel, 2 Fine Steel)", new EmptyImageIdentifier(), true, "Heavy mail-and-scale reinforcement under the main shell."));
            platings.Add(new InquiryElement(3, "4. Fluted Glancing Plate Surfaces (+15 Primary, +6 Limb | +1.8 Wt | Cost: 4 Fine Steel)", new EmptyImageIdentifier(), true, "Angled ridges deflect arrows and couched lances."));
            platings.Add(new InquiryElement(4, "5. Thamaskene Carburized Master Plate (+20 Primary, +9 Limb | -0.5 Wt | Cost: 3 Fine Steel, 2 Thamaskene)", new EmptyImageIdentifier(), true, "Legendary rippled Thamaskene steel — unmatched protection at reduced weight."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Step 2/4: Select Outer Reinforcement & Metallurgy",
                "Choose how to reinforce the outer armor shell:",
                platings,
                true,
                1,
                1,
                "Next: Inner Lining",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        _selectedPlatingIndex = (int)selected[0].Identifier;
                        OpenStage3SelectLining();
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenStage1CSelectChassisModel(); },
                "",
                false), true, false);
        }

        private void OpenStage3SelectLining()
        {
            List<InquiryElement> linings = new List<InquiryElement>();
            linings.Add(new InquiryElement(0, "1. Quilted Linen Gambeson (+2 Primary Armor | Standard Weight)", new EmptyImageIdentifier(), true, "Breathable multi-layered linen padding."));
            linings.Add(new InquiryElement(1, "2. Boiled Cuir Bouilli Sub-Layer (+4 Primary, +3 Limb | +0.4 Wt)", new EmptyImageIdentifier(), true, "Hardened wax-boiled leather absorbs blunt shock."));
            linings.Add(new InquiryElement(2, "3. Dense Brigandine Felt & Wool (+6 Primary, +4 Limb | +0.8 Wt)", new EmptyImageIdentifier(), true, "Heavy shock-deadening padding against maces and bolts."));
            linings.Add(new InquiryElement(3, "4. Sturgian Fur-Insulated Padding (+5 Primary, +6 Limb | +0.5 Wt)", new EmptyImageIdentifier(), true, "Thick northern pelts protecting arms, neck, and legs."));
            linings.Add(new InquiryElement(4, "5. Lightweight Silk Skirmisher Weave (+3 Primary, +2 Limb | -20% Total Weight)", new EmptyImageIdentifier(), true, "Tightly woven silk resists arrow penetration while slashing armor weight by 20%."));

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Step 3/4: Select Inner Padding & Structural Lining",
                "Choose the internal padding and lining assembly:",
                linings,
                true,
                1,
                1,
                "Next: Review & Forge",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        _selectedLiningIndex = (int)selected[0].Identifier;
                        OpenStage4ConfirmAndName();
                    }
                },
                delegate(List<InquiryElement> cancel) { OpenStage2SelectPlating(); },
                "",
                false), true, false);
        }

        private void OpenStage4ConfirmAndName()
        {
            if (_selectedChassisTemplate == null || _selectedChassisTemplate.ArmorComponent == null)
            {
                OpenArmorForgeHub();
                return;
            }

            int head;
            int body;
            int leg;
            int arm;
            float weight;
            int value;
            string qualityPrefix;
            CalculateForgedStats(_selectedChassisTemplate, _selectedPlatingIndex, _selectedLiningIndex, out head, out body, out leg, out arm, out weight, out value, out qualityPrefix);

            string defaultName = String.Format("{0} {1}", qualityPrefix, _selectedChassisTemplate.Name != null ? _selectedChassisTemplate.Name.ToString() : "Forged Armor");
            string summary = String.Format(
                "Ready to Quench & Temper:\n" +
                "• Base Model: {0} (Mesh: {1})\n" +
                "• Forged Stats: Head {2} | Body {3} | Arm {4} | Leg {5} | Weight {6:0.1} kg\n" +
                "• Estimated Value: {7} Denars\n\n" +
                "Enter a custom name for your forged armor piece:",
                _selectedChassisTemplate.Name,
                _selectedChassisTemplate.MultiMeshName,
                head, body, arm, leg, weight, value);

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Step 4/4: Name & Forge Custom Armor",
                summary,
                true,
                true,
                "Strike the Anvil!",
                "Back",
                delegate(string customName)
                {
                    string finalName = String.IsNullOrEmpty(customName) ? defaultName : customName.Trim();
                    ExecuteForgeCustomArmor(finalName, head, body, leg, arm, weight, value);
                },
                delegate() { OpenStage3SelectLining(); },
                false,
                null,
                "",
                defaultName), true, false);
        }

        private void CalculateForgedStats(
            ItemObject baseItem,
            int platingIdx,
            int liningIdx,
            out int head,
            out int body,
            out int leg,
            out int arm,
            out float weight,
            out int value,
            out string qualityPrefix)
        {
            ArmorComponent ac = baseItem.ArmorComponent;
            head = ac.HeadArmor;
            body = ac.BodyArmor;
            leg = ac.LegArmor;
            arm = ac.ArmArmor;
            weight = baseItem.Weight;

            int primaryBonus = 4 + platingIdx * 4 + (liningIdx == 2 ? 6 : (liningIdx == 3 ? 5 : 3));
            int limbBonus = platingIdx * 2 + (liningIdx == 3 ? 6 : (liningIdx >= 1 ? 3 : 0));

            Hero smith = Hero.MainHero;
            int smithSkill = smith != null ? smith.GetSkillValue(DefaultSkills.Crafting) : 60;
            int skillBonus = smithSkill / 25;

            if ( smithSkill >= 225 || platingIdx == 4)
            {
                qualityPrefix = "Legendary Thamaskene";
                primaryBonus += 6 + skillBonus;
                limbBonus += 4;
            }
            else if (smithSkill >= 150 || platingIdx == 3)
            {
                qualityPrefix = "Lordly Masterwork";
                primaryBonus += 4 + skillBonus;
                limbBonus += 2;
            }
            else if (smithSkill >= 90 || platingIdx == 2)
            {
                qualityPrefix = "Reinforced";
                primaryBonus += 2 + skillBonus;
            }
            else
            {
                qualityPrefix = "Hand-Forged";
                primaryBonus += skillBonus;
            }

            if (baseItem.Type == ItemObject.ItemTypeEnum.HeadArmor)
            {
                head = Math.Max(18, head + primaryBonus);
            }
            else if (baseItem.Type == ItemObject.ItemTypeEnum.Cape)
            {
                body = Math.Max(10, body + primaryBonus / 2 + 2);
                arm = Math.Max(6, arm + limbBonus + 2);
            }
            else if (baseItem.Type == ItemObject.ItemTypeEnum.BodyArmor)
            {
                body = Math.Max(28, body + primaryBonus);
                arm = Math.Max(10, arm + limbBonus);
                leg = Math.Max(10, leg + limbBonus);
            }
            else if (baseItem.Type == ItemObject.ItemTypeEnum.HandArmor)
            {
                arm = Math.Max(14, arm + primaryBonus / 2 + limbBonus);
            }
            else if (baseItem.Type == ItemObject.ItemTypeEnum.LegArmor)
            {
                leg = Math.Max(14, leg + primaryBonus / 2 + limbBonus);
            }
            else if (baseItem.Type == ItemObject.ItemTypeEnum.HorseHarness)
            {
                body = Math.Max(35, body + primaryBonus + 4);
            }

            float wtDelta = (platingIdx == 4 ? -0.5f : platingIdx * 0.45f) + (liningIdx == 4 ? -weight * 0.20f : liningIdx * 0.2f);
            weight = Math.Max(0.5f, weight + wtDelta);
            int totalArmor = head + body + leg + arm;
            value = Math.Max(baseItem.Value + 1500, totalArmor * totalArmor * 8 + platingIdx * 2500);
        }

        private void ExecuteForgeCustomArmor(string finalName, int head, int body, int leg, int arm, float weight, int value)
        {
            if (_selectedChassisTemplate == null)
            {
                return;
            }

            // Consume materials if available (graceful fallback if player is short so testing is never blocked)
            ConsumeMaterialIfPresent("charcoal", 1);
            if (_selectedPlatingIndex <= 0)
            {
                ConsumeMaterialIfPresent("ironIngot3", 2);
            }
            else if (_selectedPlatingIndex == 1)
            {
                ConsumeMaterialIfPresent("ironIngot4", 3);
            }
            else if (_selectedPlatingIndex == 2)
            {
                ConsumeMaterialIfPresent("ironIngot4", 3);
                ConsumeMaterialIfPresent("ironIngot5", 2);
            }
            else if (_selectedPlatingIndex == 3)
            {
                ConsumeMaterialIfPresent("ironIngot5", 4);
            }
            else
            {
                ConsumeMaterialIfPresent("ironIngot5", 3);
                ConsumeMaterialIfPresent("ironIngot6", 2);
            }

            // Stamina & Crafting XP
            if (Hero.MainHero != null)
            {
                int curStamina = GetHeroStamina(Hero.MainHero);
                SetHeroStamina(Hero.MainHero, Math.Max(0, curStamina - 25));
                int xpGain = 450 + (_selectedPlatingIndex + 1) * 350 + (head + body + leg + arm) * 12;
                Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, (float)xpGain);
            }

            // Pick a save-persistent custom forge slot for this armor type and configure it
            ItemObject forgedItem = GetTargetForgeSlotItem(_selectedChassisTemplate.Type);
            if (forgedItem == null)
            {
                forgedItem = _selectedChassisTemplate;
            }
            else
            {
                ApplyCustomArmorData(forgedItem, finalName, _selectedChassisTemplate, head, body, leg, arm, weight, value);
                SaveForgedArmorRecord(forgedItem.StringId, finalName, _selectedChassisTemplate.MultiMeshName, head, body, leg, arm, weight, value);
            }

            if (MobileParty.MainParty != null && MobileParty.MainParty.ItemRoster != null)
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(forgedItem, 1);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                String.Format("[Master Armorer] Forged '{0}' (H:{1} B:{2} A:{3} L:{4}, Wt:{5:0.1}) and added to your inventory!",
                    finalName, head, body, arm, leg, weight),
                Color.FromUint(0xFFFACC15)));

            OpenArmorForgeHub();
        }

        private ItemObject GetTargetForgeSlotItem(ItemObject.ItemTypeEnum slotType)
        {
            string prefix = "sdo_custom_forged_body_";
            if (slotType == ItemObject.ItemTypeEnum.HeadArmor) prefix = "sdo_custom_forged_head_";
            else if (slotType == ItemObject.ItemTypeEnum.Cape) prefix = "sdo_custom_forged_cape_";
            else if (slotType == ItemObject.ItemTypeEnum.BodyArmor) prefix = "sdo_custom_forged_body_";
            else if (slotType == ItemObject.ItemTypeEnum.HandArmor) prefix = "sdo_custom_forged_hand_";
            else if (slotType == ItemObject.ItemTypeEnum.LegArmor) prefix = "sdo_custom_forged_leg_";
            else if (slotType == ItemObject.ItemTypeEnum.HorseHarness) prefix = "sdo_custom_forged_harness_";

            string slot1 = prefix + "1";
            string slot2 = prefix + "2";

            ItemObject item1 = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(slot1) : null;
            ItemObject item2 = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(slot2) : null;

            bool slot1Used = _savedForgedArmors.Any(s => s.StartsWith(slot1 + "|", StringComparison.Ordinal));
            if (!slot1Used && item1 != null)
            {
                return item1;
            }
            if (item2 != null)
            {
                return item2;
            }
            return item1;
        }

        private void ApplyCustomArmorData(
            ItemObject target,
            string customName,
            ItemObject sourceTemplate,
            int head,
            int body,
            int leg,
            int arm,
            float weight,
            int value)
        {
            if (target == null)
            {
                return;
            }

            SetBackingField(target, "<Name>k__BackingField", new TextObject("{=!}" + customName, null));
            if (sourceTemplate != null && !String.IsNullOrEmpty(sourceTemplate.MultiMeshName))
            {
                SetBackingField(target, "<MultiMeshName>k__BackingField", sourceTemplate.MultiMeshName);
            }
            SetBackingField(target, "<Weight>k__BackingField", weight);
            SetBackingField(target, "<Value>k__BackingField", value);
            SetBackingField(target, "<IsCraftedByPlayer>k__BackingField", true);
            SetBackingField(target, "<TierfOverride>k__BackingField", 6.0f);

            ArmorComponent ac = target.ArmorComponent;
            if (ac != null)
            {
                SetBackingField(ac, "<HeadArmor>k__BackingField", head);
                SetBackingField(ac, "<BodyArmor>k__BackingField", body);
                SetBackingField(ac, "<LegArmor>k__BackingField", leg);
                SetBackingField(ac, "<ArmArmor>k__BackingField", arm);

                if (sourceTemplate != null && sourceTemplate.ArmorComponent != null)
                {
                    ArmorComponent srcAc = sourceTemplate.ArmorComponent;
                    SetBackingField(ac, "<MaterialType>k__BackingField", srcAc.MaterialType);
                    SetBackingField(ac, "<BodyMeshType>k__BackingField", srcAc.BodyMeshType);
                    SetBackingField(ac, "<BodyDeformType>k__BackingField", srcAc.BodyDeformType);
                    SetBackingField(ac, "<HairCoverType>k__BackingField", srcAc.HairCoverType);
                    SetBackingField(ac, "<BeardCoverType>k__BackingField", srcAc.BeardCoverType);
                    SetBackingField(ac, "<MeshesMask>k__BackingField", srcAc.MeshesMask);
                    SetBackingField(ac, "<MultiMeshHasGenderVariations>k__BackingField", srcAc.MultiMeshHasGenderVariations);
                    if (!String.IsNullOrEmpty(srcAc.ReinsMesh))
                    {
                        SetBackingField(ac, "<ReinsMesh>k__BackingField", srcAc.ReinsMesh);
                    }
                }
            }
        }

        private void SaveForgedArmorRecord(string itemId, string name, string mesh, int head, int body, int leg, int arm, float weight, int value)
        {
            string cleanName = (name ?? "Forged Armor").Replace("|", "-");
            string entry = String.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7:0.00}|{8}",
                itemId, cleanName, mesh ?? "", head, body, leg, arm, weight, value);

            _savedForgedArmors.RemoveAll(s => s.StartsWith(itemId + "|", StringComparison.Ordinal));
            _savedForgedArmors.Add(entry);
        }

        private void RestoreSavedForgedArmors()
        {
            if (_savedForgedArmors == null || MBObjectManager.Instance == null)
            {
                return;
            }

            for (int i = 0; i < _savedForgedArmors.Count; i++)
            {
                string[] parts = _savedForgedArmors[i].Split('|');
                if (parts.Length < 9)
                {
                    continue;
                }
                ItemObject target = MBObjectManager.Instance.GetObject<ItemObject>(parts[0]);
                if (target == null)
                {
                    continue;
                }

                int head;
                int body;
                int leg;
                int arm;
                float weight;
                int value;
                Int32.TryParse(parts[3], out head);
                Int32.TryParse(parts[4], out body);
                Int32.TryParse(parts[5], out leg);
                Int32.TryParse(parts[6], out arm);
                Single.TryParse(parts[7], out weight);
                Int32.TryParse(parts[8], out value);

                SetBackingField(target, "<Name>k__BackingField", new TextObject("{=!}" + parts[1], null));
                if (!String.IsNullOrEmpty(parts[2]))
                {
                    SetBackingField(target, "<MultiMeshName>k__BackingField", parts[2]);
                }
                SetBackingField(target, "<Weight>k__BackingField", weight);
                SetBackingField(target, "<Value>k__BackingField", value);
                SetBackingField(target, "<TierfOverride>k__BackingField", 6.0f);

                if (target.ArmorComponent != null)
                {
                    SetBackingField(target.ArmorComponent, "<HeadArmor>k__BackingField", head);
                    SetBackingField(target.ArmorComponent, "<BodyArmor>k__BackingField", body);
                    SetBackingField(target.ArmorComponent, "<LegArmor>k__BackingField", leg);
                    SetBackingField(target.ArmorComponent, "<ArmArmor>k__BackingField", arm);
                }
            }
        }

        private void OpenTemperEquippedArmorMenu()
        {
            if (Hero.MainHero == null || Hero.MainHero.BattleEquipment == null)
            {
                return;
            }

            Equipment eq = Hero.MainHero.BattleEquipment;
            EquipmentIndex[] armorSlots = new EquipmentIndex[]
            {
                EquipmentIndex.Head,
                EquipmentIndex.Cape,
                EquipmentIndex.Body,
                EquipmentIndex.Gloves,
                EquipmentIndex.Leg,
                EquipmentIndex.HorseHarness
            };

            List<InquiryElement> elements = new List<InquiryElement>();
            for (int i = 0; i < armorSlots.Length; i++)
            {
                EquipmentIndex idx = armorSlots[i];
                EquipmentElement elem = eq[idx];
                if (!elem.IsEmpty && elem.Item != null && elem.Item.ArmorComponent != null)
                {
                    ArmorComponent ac = elem.Item.ArmorComponent;
                    string title = String.Format("{0}: {1} [H:{2} B:{3} A:{4} L:{5}] -> +6 Masterwork Temper",
                        idx, elem.Item.Name, ac.HeadArmor, ac.BodyArmor, ac.ArmArmor, ac.LegArmor);
                    elements.Add(new InquiryElement(idx, title, new ItemImageIdentifier(elem.Item, ""), true, "Reinforce this equipped armor piece on the anvil (+6 Primary, +4 Secondary Armor, +Smithing XP)."));
                }
            }

            if (elements.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("Your Hero has no armor equipped in Battle Equipment.", Color.FromUint(0xFFF87171)));
                OpenArmorForgeHub();
                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Temper & Masterwork Reforge Equipped Armor",
                "Select an equipped armor piece to temper on the anvil:",
                elements,
                true,
                1,
                1,
                "Temper Armor",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        EquipmentIndex slot = (EquipmentIndex)selected[0].Identifier;
                        ItemObject item = eq[slot].Item;
                        if (item != null && item.ArmorComponent != null)
                        {
                            ArmorComponent ac = item.ArmorComponent;
                            int newHead = ac.HeadArmor > 0 ? ac.HeadArmor + 6 : 0;
                            int newBody = ac.BodyArmor > 0 ? ac.BodyArmor + 6 : 0;
                            int newLeg = ac.LegArmor > 0 ? ac.LegArmor + 4 : 0;
                            int newArm = ac.ArmArmor > 0 ? ac.ArmArmor + 4 : 0;

                            SetBackingField(ac, "<HeadArmor>k__BackingField", newHead);
                            SetBackingField(ac, "<BodyArmor>k__BackingField", newBody);
                            SetBackingField(ac, "<LegArmor>k__BackingField", newLeg);
                            SetBackingField(ac, "<ArmArmor>k__BackingField", newArm);

                            ConsumeMaterialIfPresent("ironIngot5", 1);
                            Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, 650f);

                            InformationManager.DisplayMessage(new InformationMessage(
                                String.Format("[Master Armorer] Tempered {0}! New ratings: H:{1} B:{2} A:{3} L:{4}.",
                                    item.Name, newHead, newBody, newArm, newLeg),
                                Color.FromUint(0xFF4ADE80)));
                        }
                    }
                    OpenArmorForgeHub();
                },
                delegate(List<InquiryElement> cancel) { OpenArmorForgeHub(); },
                "",
                false), true, false);
        }

        private void OpenSmeltArmorMenu()
        {
            if (MobileParty.MainParty == null || MobileParty.MainParty.ItemRoster == null)
            {
                return;
            }

            ItemRoster roster = MobileParty.MainParty.ItemRoster;
            List<InquiryElement> smeltable = new List<InquiryElement>();

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement ire = roster.GetElementCopyAtIndex(i);
                ItemObject item = ire.EquipmentElement.Item;
                if (item != null && item.ArmorComponent != null && ire.Amount > 0)
                {
                    ArmorComponent ac = item.ArmorComponent;
                    int totalArmor = ac.HeadArmor + ac.BodyArmor + ac.ArmArmor + ac.LegArmor;
                    string yieldDesc = GetSmeltYieldDescription(item, totalArmor);
                    string label = String.Format("{0} (x{1}) [Armor:{2}] -> {3}",
                        item.Name, ire.Amount, totalArmor, yieldDesc);
                    smeltable.Add(new InquiryElement(item, label, new ItemImageIdentifier(item, ""), true, "Smelt 1x " + item.Name + " into ingots and Smithing XP."));
                }
            }

            if (smeltable.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("No armor items found in your party inventory to smelt.", Color.FromUint(0xFFF87171)));
                OpenArmorForgeHub();
                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Smelt Inventory Armor into Smithing Ingots",
                "Select one or more armor pieces from your inventory to smelt down at the furnace:",
                smeltable,
                true,
                1,
                Math.Min(10, smeltable.Count),
                "Smelt Selected",
                "Back",
                delegate(List<InquiryElement> selected)
                {
                    if (selected != null && selected.Count > 0)
                    {
                        int countSmelted = 0;
                        for (int i = 0; i < selected.Count; i++)
                        {
                            ItemObject item = selected[i].Identifier as ItemObject;
                            if (item != null && roster.GetItemNumber(item) > 0)
                            {
                                roster.AddToCounts(item, -1);
                                AwardSmeltIngots(item);
                                countSmelted++;
                            }
                        }
                        if (countSmelted > 0)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                String.Format("[Master Armorer] Smelted {0} armor piece(s) into Smithing ingots & earned Smithing XP!", countSmelted),
                                Color.FromUint(0xFF4ADE80)));
                        }
                    }
                    OpenArmorForgeHub();
                },
                delegate(List<InquiryElement> cancel) { OpenArmorForgeHub(); },
                "",
                true), true, false);
        }

        private string GetSmeltYieldDescription(ItemObject item, int totalArmor)
        {
            if (totalArmor >= 75) return "2 Fine Steel, 2 Steel, 1 Thamaskene";
            if (totalArmor >= 50) return "3 Steel, 1 Fine Steel";
            if (totalArmor >= 30) return "2 Iron, 2 Steel";
            return "2 Crude/Wrought Iron, 1 Charcoal";
        }

        private void AwardSmeltIngots(ItemObject item)
        {
            ArmorComponent ac = item.ArmorComponent;
            int totalArmor = ac != null ? (ac.HeadArmor + ac.BodyArmor + ac.ArmArmor + ac.LegArmor) : 15;

            if (totalArmor >= 75)
            {
                AddMaterial("ironIngot4", 2);
                AddMaterial("ironIngot5", 2);
                AddMaterial("ironIngot6", 1);
            }
            else if (totalArmor >= 50)
            {
                AddMaterial("ironIngot4", 3);
                AddMaterial("ironIngot5", 1);
            }
            else if (totalArmor >= 30)
            {
                AddMaterial("ironIngot3", 2);
                AddMaterial("ironIngot4", 2);
            }
            else
            {
                AddMaterial("ironIngot2", 2);
                AddMaterial("charcoal", 1);
            }

            if (Hero.MainHero != null)
            {
                Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, 180f + totalArmor * 8f);
            }
        }

        private void BuySmithingBundle()
        {
            if (Hero.MainHero == null || Hero.MainHero.Gold < 1500)
            {
                return;
            }
            Hero.MainHero.ChangeHeroGold(-1500);
            AddMaterial("charcoal", 10);
            AddMaterial("ironIngot3", 6);
            AddMaterial("ironIngot4", 6);
            AddMaterial("ironIngot5", 4);
            AddMaterial("ironIngot6", 2);
            InformationManager.DisplayMessage(new InformationMessage("Purchased Master Smithing Ingot Bundle (-1,500 Denars).", Color.FromUint(0xFF4ADE80)));
            OpenArmorForgeHub();
        }

        private List<ItemObject> GetValidArmorModels(ItemObject.ItemTypeEnum slotType, string cultureCode)
        {
            List<ItemObject> results = new List<ItemObject>();
            if (MBObjectManager.Instance == null)
            {
                return results;
            }

            MBReadOnlyList<ItemObject> allItems = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            HashSet<string> seenMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < allItems.Count; i++)
            {
                ItemObject it = allItems[i];
                if (it == null || it.Type != slotType || it.ArmorComponent == null || String.IsNullOrEmpty(it.MultiMeshName))
                {
                    continue;
                }
                if (it.StringId.StartsWith("sdo_custom_forged_", StringComparison.Ordinal))
                {
                    continue;
                }

                string itemCulture = it.Culture != null ? it.Culture.StringId : "";
                if (!String.IsNullOrEmpty(cultureCode) && !String.Equals(itemCulture, cultureCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seenMeshes.Contains(it.MultiMeshName))
                {
                    seenMeshes.Add(it.MultiMeshName);
                    results.Add(it);
                }
            }

            results.Sort(delegate(ItemObject a, ItemObject b)
            {
                int scoreA = a.ArmorComponent.HeadArmor + a.ArmorComponent.BodyArmor + a.ArmorComponent.ArmArmor + a.ArmorComponent.LegArmor;
                int scoreB = b.ArmorComponent.HeadArmor + b.ArmorComponent.BodyArmor + b.ArmorComponent.ArmArmor + b.ArmorComponent.LegArmor;
                return scoreB.CompareTo(scoreA);
            });

            if (results.Count > 30)
            {
                results = results.GetRange(0, 30);
            }
            return results;
        }

        public static List<ItemObject> GetAllForgedCustomArmors()
        {
            List<ItemObject> list = new List<ItemObject>();
            if (MBObjectManager.Instance == null)
            {
                return list;
            }
            for (int i = 0; i < CustomForgeSlotIds.Length; i++)
            {
                ItemObject obj = MBObjectManager.Instance.GetObject<ItemObject>(CustomForgeSlotIds[i]);
                if (obj != null)
                {
                    list.Add(obj);
                }
            }
            return list;
        }

        private int GetMaterialCount(string itemId)
        {
            if (MobileParty.MainParty == null || MobileParty.MainParty.ItemRoster == null || MBObjectManager.Instance == null)
            {
                return 0;
            }
            ItemObject mat = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            return mat != null ? MobileParty.MainParty.ItemRoster.GetItemNumber(mat) : 0;
        }

        private void AddMaterial(string itemId, int count)
        {
            if (MobileParty.MainParty == null || MobileParty.MainParty.ItemRoster == null || MBObjectManager.Instance == null)
            {
                return;
            }
            ItemObject mat = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (mat != null)
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(mat, count);
            }
        }

        private void ConsumeMaterialIfPresent(string itemId, int count)
        {
            if (MobileParty.MainParty == null || MobileParty.MainParty.ItemRoster == null || MBObjectManager.Instance == null)
            {
                return;
            }
            ItemObject mat = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (mat != null)
            {
                int have = MobileParty.MainParty.ItemRoster.GetItemNumber(mat);
                int take = Math.Min(have, count);
                if (take > 0)
                {
                    MobileParty.MainParty.ItemRoster.AddToCounts(mat, -take);
                }
            }
        }

        private int GetHeroStamina(Hero hero)
        {
            if (hero == null || Campaign.Current == null)
            {
                return 100;
            }
            CraftingCampaignBehavior ccb = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            return ccb != null ? ccb.GetHeroCraftingStamina(hero) : 100;
        }

        private int GetHeroMaxStamina(Hero hero)
        {
            if (hero == null || Campaign.Current == null)
            {
                return 100;
            }
            CraftingCampaignBehavior ccb = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            return ccb != null ? ccb.GetMaxHeroCraftingStamina(hero) : 100;
        }

        private void SetHeroStamina(Hero hero, int val)
        {
            if (hero == null || Campaign.Current == null)
            {
                return;
            }
            CraftingCampaignBehavior ccb = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            if (ccb != null)
            {
                ccb.SetHeroCraftingStamina(hero, val);
            }
        }

        internal static void SetBackingField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }
            Type t = target.GetType();
            while (t != null)
            {
                FieldInfo fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null)
                {
                    fi.SetValue(target, value);
                    return;
                }
                t = t.BaseType;
            }
        }
    }
}
