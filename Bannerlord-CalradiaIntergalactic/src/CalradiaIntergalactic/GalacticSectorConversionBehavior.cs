using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CalradiaIntergalactic
{
    /// <summary>
    /// Converts Calradia's feudal geography, factions, economy, and dialogues into an
    /// interstellar sci-fi sector ("Calradia: Intergalactic"):
    /// - Every Town becomes a Star System Primary / Capital World &amp; High Starport.
    /// - Every Castle becomes a Deep-Space Orbital Citadel / Starbase Bastion.
    /// - Every bound Village becomes a Minor Moon Colony / Asteroid Mining Rig / Orbital Habitat
    ///   explicitly grouped under its parent Star System.
    /// - Kingdoms &amp; Clans become Star-Syndicates, Hegemonies, and Rogue Trader Void-Dynasties.
    /// - Trade goods &amp; mounts become Interstellar Commodities &amp; Sublight Strike-Skiffs.
    /// - Provides Stellaris System Edicts, Megastructures, Galactic Crises, and 40k Vox-Hail Dialogues.
    /// </summary>
    public class GalacticSectorConversionBehavior : CampaignBehaviorBase
    {
        private bool _astrographyConverted;
        private Dictionary<string, int> _systemEdictMap = new Dictionary<string, int>();
        private Dictionary<string, int> _systemMegastructureLevel = new Dictionary<string, int>();
        private int _activeCrisisType;
        private string _activeCrisisSystemName = "Deep-Space Rim";
        private int _crisisDefeatedCount;

        private static readonly string[] MinorColonySuffixes = new string[]
        {
            "II-a [Asteroid Mine]",
            "III-b [Agri-Hydroponics Moon]",
            "IV-c [Promethium Refinery]",
            "Belt-d [Orbital Habitat]",
            "V-e [Cryo-Extractor Outpost]"
        };

        private static readonly string[] CrisisNames = new string[]
        {
            "Crimson Void-Pirate Armada (Star Traders Raider Syndicate)",
            "Prethoryn Xeno Bio-Swarm Hive-Fleet (Stellaris Extragalactic Crisis)",
            "Omnissian Rogue AI Contingency Core (Machine Uprising Fleet)",
            "Warp-Rift Chaos Incursion Cult (40k Immaterium Breach)"
        };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("cig_systemEdictMap", ref _systemEdictMap);
            dataStore.SyncData("cig_systemMegastructureLevel", ref _systemMegastructureLevel);
            dataStore.SyncData("cig_activeCrisisType", ref _activeCrisisType);
            dataStore.SyncData("cig_activeCrisisSystemName", ref _activeCrisisSystemName);
            dataStore.SyncData("cig_crisisDefeatedCount", ref _crisisDefeatedCount);

            if (_systemEdictMap == null)
            {
                _systemEdictMap = new Dictionary<string, int>();
            }
            if (_systemMegastructureLevel == null)
            {
                _systemMegastructureLevel = new Dictionary<string, int>();
            }
            if (string.IsNullOrEmpty(_activeCrisisSystemName))
            {
                _activeCrisisSystemName = "Deep-Space Rim";
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ConvertGalacticAstrographyAndEconomy();
            SeedStarportSciFiMarkets();
            GrantPlayerStarterSciFiKitIfNew();
            RegisterStarportMenus(starter);
            RegisterVoxHailDialogues(starter);
        }

        private void OnDailyTick()
        {
            if (!_astrographyConverted)
            {
                ConvertGalacticAstrographyAndEconomy();
            }

            ProcessSystemEdictsAndMegastructures();
            RotateGalacticCrisisAlerts();
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (party != null && party == MobileParty.MainParty && settlement != null)
            {
                string sysDesc = GetStarSystemSummaryLine(settlement);
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Astrographic Nav-Computer] Docked at " + settlement.Name + " | " + sysDesc,
                    Color.FromUint(0xFF66E0FF)));
            }
        }

        #region 1. Astrography, Faction, Commodity & Strike-Skiff Total Conversion

        public void ConvertGalacticAstrographyAndEconomy()
        {
            try
            {
                FieldInfo settlementNameField = typeof(Settlement).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo itemNameField = typeof(ItemObject).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo kingdomNameField = typeof(Kingdom).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo kingdomInformalField = typeof(Kingdom).GetField("<InformalName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo kingdomFormalField = typeof(Kingdom).GetField("<FormalName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                // 1. Convert Kingdoms into Interstellar Hegemonies & Syndicates
                if (Kingdom.All != null)
                {
                    foreach (Kingdom k in Kingdom.All)
                    {
                        if (k == null || string.IsNullOrEmpty(k.StringId)) continue;
                        string newKName = GetInterstellarFactionName(k.StringId, k.Name != null ? k.Name.ToString() : k.StringId);
                        TextObject kText = new TextObject("{=!}" + newKName);
                        if (kingdomNameField != null) kingdomNameField.SetValue(k, kText);
                        if (kingdomInformalField != null) kingdomInformalField.SetValue(k, kText);
                        if (kingdomFormalField != null) kingdomFormalField.SetValue(k, kText);
                    }
                }

                // 2. Convert Towns (Star System Capitals), Castles (Orbital Starbases),
                //    and Bound Villages (Minor Moon/Asteroid Colonies in the same Star System!)
                if (Settlement.All != null && settlementNameField != null)
                {
                    foreach (Settlement s in Settlement.All)
                    {
                        if (s == null || s.Name == null) continue;
                        string rawName = StripTags(s.Name.ToString());

                        if (s.IsTown)
                        {
                            string sysCapitalName = rawName + " Prime [System Starport]";
                            settlementNameField.SetValue(s, new TextObject("{=!}" + sysCapitalName));

                            if (s.BoundVillages != null)
                            {
                                for (int i = 0; i < s.BoundVillages.Count; i++)
                                {
                                    Village v = s.BoundVillages[i];
                                    if (v == null || v.Settlement == null) continue;
                                    string vBase = StripTags(v.Name.ToString());
                                    string suffix = MinorColonySuffixes[i % MinorColonySuffixes.Length];
                                    string colName = rawName + " " + suffix + " (" + vBase + ")";
                                    settlementNameField.SetValue(v.Settlement, new TextObject("{=!}" + colName));
                                }
                            }
                        }
                        else if (s.IsCastle)
                        {
                            string citadelName = rawName + " Bastion [Orbital Starbase]";
                            settlementNameField.SetValue(s, new TextObject("{=!}" + citadelName));

                            if (s.BoundVillages != null)
                            {
                                for (int i = 0; i < s.BoundVillages.Count; i++)
                                {
                                    Village v = s.BoundVillages[i];
                                    if (v == null || v.Settlement == null) continue;
                                    string vBase = StripTags(v.Name.ToString());
                                    string colName = rawName + " Garrison Moon-" + (i + 1) + " (" + vBase + " Outpost)";
                                    settlementNameField.SetValue(v.Settlement, new TextObject("{=!}" + colName));
                                }
                            }
                        }
                        else if (s.IsHideout)
                        {
                            settlementNameField.SetValue(s, new TextObject("{=!}Pirate Asteroid Lair (" + rawName + ")"));
                        }
                    }
                }

                // 3. Convert Clans into Void-Houses & Rogue Trader Dynasties
                if (Clan.All != null)
                {
                    foreach (Clan clan in Clan.All)
                    {
                        if (clan == null || clan.Name == null || clan == Clan.PlayerClan) continue;
                        string cRaw = StripTags(clan.Name.ToString());
                        if (!cRaw.Contains("Void-") && !cRaw.Contains("Syndicate") && !cRaw.Contains("Dynasty"))
                        {
                            string newClanName = clan.IsMinorFaction
                                ? cRaw + " Mercenary Flotilla"
                                : "Void-House " + cRaw;
                            TextObject cObj = new TextObject("{=!}" + newClanName);
                            clan.ChangeClanName(cObj, cObj);
                        }
                    }
                }

                // 4. Convert Lords into Star-Captains, Admirals & Void-Commodores
                if (Hero.AllAliveHeroes != null)
                {
                    foreach (Hero h in Hero.AllAliveHeroes)
                    {
                        if (h == null || h == Hero.MainHero || !h.IsLord || h.Name == null) continue;
                        string hRaw = StripTags(h.Name.ToString());
                        if (!hRaw.StartsWith("Lord Captain ") && !hRaw.StartsWith("High Admiral ") && !hRaw.StartsWith("Void-Baron "))
                        {
                            string prefix = (h.Clan != null && h.Clan.Leader == h) ? "High Admiral " : "Lord Captain ";
                            TextObject full = new TextObject("{=!}" + prefix + hRaw);
                            TextObject first = new TextObject("{=!}" + hRaw);
                            h.SetName(full, first);
                        }
                    }
                }

                // 5. Convert Vanilla Trade Goods & Horses into Sci-Fi Commodities & Sublight Strike-Skiffs
                if (MBObjectManager.Instance != null && itemNameField != null)
                {
                    Dictionary<string, string> itemRenames = GetVanillaItemSciFiRenames();
                    foreach (KeyValuePair<string, string> kvp in itemRenames)
                    {
                        ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(kvp.Key);
                        if (item != null)
                        {
                            itemNameField.SetValue(item, new TextObject("{=!}" + kvp.Value));
                        }
                    }
                }

                _astrographyConverted = true;
            }
            catch (Exception)
            {
                // Non-fatal fallback
            }
        }

        private static string StripTags(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Sector";
            int bracketIdx = name.IndexOf(" [");
            if (bracketIdx > 0) name = name.Substring(0, bracketIdx);
            name = name.Replace(" Prime", "").Replace(" Bastion", "");
            return name.Trim();
        }

        private static string GetInterstellarFactionName(string id, string fallback)
        {
            switch (id)
            {
                case "empire": return "Northern Aurelian Imperium (Core Starfleet)";
                case "empire_w": return "Western Navis Armada (Rogue Trader Hegemony)";
                case "empire_s": return "Southern Aurelian Dynasty (Imperium Core)";
                case "vlandia": return "Valorian Star-Syndicate (Corporate Dreadnoughts)";
                case "sturgia": return "Varian Void-Jarldoms (Cryo-Sector Raiders)";
                case "battania": return "Sylvan Horizon Enclaves (Nebula Rangers)";
                case "khuzait": return "Khanate of the Astral Steppe (Interceptor Swarm)";
                case "aserai": return "Solaris Trade Consortium (Guild Navigators)";
                default: return fallback + " Star-Coalition";
            }
        }

        private static Dictionary<string, string> GetVanillaItemSciFiRenames()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            // Trade Commodities (Star Traders: Frontiers / Stellaris / 40k)
            map["grain"] = "Hydroponic Nutri-Biomass [Agri-Colony]";
            map["fish"] = "Cryo-Protein Synth-Packs [Bio-Vat]";
            map["meat"] = "Cloned Vitae Rations [Supply Crate]";
            map["cheese"] = "Nutrient Paste Bricks [Fleet Ration]";
            map["butter"] = "Refined Bio-Lipid Canisters";
            map["olives"] = "Luminescent Flora Spores";
            map["date_fruit"] = "Solar-Grown Xeno-Fruit";
            map["grape"] = "Void-Orchard Bio-Nectar";
            map["wine"] = "Amasec Officer Vintage [Luxury]";
            map["beer"] = "Starport Recaff & Synth-Ale";
            map["oil"] = "Refined Promethium Thruster Fuel";
            map["spice"] = "Navis Hyper-Spice Melange";
            map["salt"] = "Deuterium Coolant Salts";
            map["iron"] = "Titanium-Durasteel Asteroid Ore";
            map["silver"] = "Helium-3 Fusion Isotopes";
            map["hardwood"] = "Carbon-Nanotube Structural Beams";
            map["clay"] = "Silicate Ceramic Heat-Tiles";
            map["pottery"] = "Insulated Plasma Capacitors";
            map["flax"] = "Raw Polymer Filament";
            map["linen"] = "Sealed Vacc-Suit Weave";
            map["cotton"] = "Synth-Fiber Insulation";
            map["wool"] = "Thermal Void-Fleece";
            map["leather"] = "Armored Xeno-Hide Plating";
            map["fur"] = "Cryo-World Fenrir Pelts";
            map["velvet"] = "Nanoweave Noble Void-Silk";
            map["jewelry"] = "Archeotech Xeno-Relic Cores";
            map["tools"] = "Omnissian Servo-Tool Arrays";
            // Personal Strike-Craft & Grav-Skiffs (replaces Horses/Camels)
            map["sumpter_horse"] = "Cargo Grav-Lifter Skiff";
            map["mule"] = "Heavy Orbital Hauler Drone";
            map["saddle_horse"] = "Sublight Scout Speeder";
            map["old_horse"] = "Surplus Utility Hover-Skiff";
            map["steppe_horse"] = "Astral Dart Interceptor Bike";
            map["desert_horse"] = "Solaris Ion-Skimmer";
            map["war_horse"] = "Valkyrie Strike-Interceptor Craft";
            map["charger"] = "Aegis Heavy Void-Gunship Frame";
            map["camel"] = "Dune-Class Heavy Hover-Rig";
            map["war_camel"] = "Solaris Plasma-Skiff Gunboat";
            return map;
        }

        private void SeedStarportSciFiMarkets()
        {
            if (Settlement.All == null || MBObjectManager.Instance == null) return;

            string[] sciFiItemIds = new string[]
            {
                "cig_wpn_plasma_rifle",
                "cig_wpn_laser_carbine",
                "cig_wpn_gauss_railgun",
                "cig_wpn_arc_blaster",
                "cig_wpn_torpedo_launcher",
                "cig_wpn_disruptor_lance",
                "cig_ammo_plasma_cell",
                "cig_ammo_rail_slug",
                "cig_ammo_photon_micro",
                "cig_wpn_vibro_blade",
                "cig_wpn_void_glaive",
                "cig_armor_void_marine_helm",
                "cig_armor_void_marine_suit",
                "cig_armor_exo_knight_helm",
                "cig_armor_exo_knight_suit",
                "cig_armor_rogue_captain_helm",
                "cig_armor_rogue_captain_suit",
                "cig_armor_thruster_pack",
                "cig_armor_mag_boots",
                "cig_armor_power_gauntlets",
                "cig_good_promethium",
                "cig_good_spice_melange",
                "cig_good_quantum_cores",
                "cig_good_xeno_relics",
                "cig_good_durasteel",
                "cig_good_cryo_medgel"
            };

            foreach (Settlement s in Settlement.All)
            {
                if (s == null || !s.IsTown || s.ItemRoster == null) continue;
                foreach (string id in sciFiItemIds)
                {
                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (item != null && s.ItemRoster.GetItemNumber(item) < 5)
                    {
                        int addCount = item.ItemType == ItemObject.ItemTypeEnum.Goods ? 18 : 6;
                        s.ItemRoster.AddToCounts(item, addCount);
                    }
                }
            }
        }

        private void GrantPlayerStarterSciFiKitIfNew()
        {
            if (MobileParty.MainParty == null || MobileParty.MainParty.ItemRoster == null || MBObjectManager.Instance == null)
            {
                return;
            }

            ItemObject rifle = MBObjectManager.Instance.GetObject<ItemObject>("cig_wpn_plasma_rifle");
            if (rifle == null) return;

            // Check if player already received the Rogue Trader Warrant Starter Kit
            if (_systemEdictMap.ContainsKey("__starter_kit_granted"))
            {
                return;
            }
            _systemEdictMap["__starter_kit_granted"] = 1;

            string[] starterItems = new string[]
            {
                "cig_wpn_plasma_rifle",
                "cig_wpn_laser_carbine",
                "cig_wpn_gauss_railgun",
                "cig_wpn_torpedo_launcher",
                "cig_ammo_plasma_cell",
                "cig_ammo_plasma_cell",
                "cig_ammo_rail_slug",
                "cig_ammo_photon_micro",
                "cig_wpn_vibro_blade",
                "cig_wpn_void_glaive",
                "cig_armor_rogue_captain_helm",
                "cig_armor_rogue_captain_suit",
                "cig_armor_thruster_pack",
                "cig_armor_power_gauntlets",
                "cig_armor_mag_boots",
                "cig_good_promethium",
                "cig_good_spice_melange"
            };

            foreach (string id in starterItems)
            {
                ItemObject obj = MBObjectManager.Instance.GetObject<ItemObject>(id);
                if (obj != null)
                {
                    MobileParty.MainParty.ItemRoster.AddToCounts(obj, 1);
                }
            }

            // Also add a detachment of Void-Spacers and Phase-Plasma Marines to the player's flagship crew
            CharacterObject t3Marine = MBObjectManager.Instance.GetObject<CharacterObject>("cig_void_t3_plasma_gunner");
            CharacterObject t2Breacher = MBObjectManager.Instance.GetObject<CharacterObject>("cig_void_t2_breacher");
            if (t3Marine != null && MobileParty.MainParty.MemberRoster != null)
            {
                MobileParty.MainParty.MemberRoster.AddToCounts(t3Marine, 8);
            }
            if (t2Breacher != null && MobileParty.MainParty.MemberRoster != null)
            {
                MobileParty.MainParty.MemberRoster.AddToCounts(t2Breacher, 8);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "[Warrant of Trade] Flagship Armory stocked with Phase-Plasma Rifles, Gauss Railgun, Void-Power Armor & 16 Navis Void-Marines!",
                Color.FromUint(0xFFFFD700)));
        }

        #endregion

        #region 2. Starport Astrographic Command, Stellaris System Edicts & Galactic Crisis Scanner

        private void RegisterStarportMenus(CampaignGameStarter starter)
        {
            if (starter == null) return;

            starter.AddGameMenuOption(
                "town",
                "cig_starport_astrography_hub",
                "{=!}StarSystem Command: Colonies, Edicts & Galactic Crisis Scanner",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenStarSystemCommandDialog(Settlement.CurrentSettlement);
                },
                false,
                4,
                false);

            starter.AddGameMenuOption(
                "castle",
                "cig_citadel_astrography_hub",
                "{=!}Orbital Starbase: System Colonies, Edicts & Crisis Scanner",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    OpenStarSystemCommandDialog(Settlement.CurrentSettlement);
                },
                false,
                4,
                false);

            starter.AddGameMenuOption(
                "village",
                "cig_colony_astrography_hub",
                "{=!}Colony Outpost Telemetry & Parent Star System Link",
                delegate(MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    Settlement s = Settlement.CurrentSettlement;
                    Settlement parentSystem = (s != null && s.IsVillage && s.Village != null && s.Village.Bound != null)
                        ? s.Village.Bound
                        : s;
                    OpenStarSystemCommandDialog(parentSystem);
                },
                false,
                3,
                false);
        }

        public string GetStarSystemSummaryLine(Settlement s)
        {
            if (s == null) return "Deep Space";
            Settlement primary = (s.IsVillage && s.Village != null && s.Village.Bound != null) ? s.Village.Bound : s;
            int colonyCount = (primary.BoundVillages != null) ? primary.BoundVillages.Count : 0;
            string ownerName = (primary.OwnerClan != null) ? primary.OwnerClan.Name.ToString() : "Independent Syndicate";
            return "System Primary: " + primary.Name + " | Minor Colonies: " + colonyCount + " | Controller: " + ownerName;
        }

        public void OpenStarSystemCommandDialog(Settlement systemPrimary)
        {
            if (systemPrimary == null)
            {
                systemPrimary = Settlement.CurrentSettlement;
            }
            if (systemPrimary != null && systemPrimary.IsVillage && systemPrimary.Village != null && systemPrimary.Village.Bound != null)
            {
                systemPrimary = systemPrimary.Village.Bound;
            }
            if (systemPrimary == null) return;

            string sysId = systemPrimary.StringId;
            int currentEdict = _systemEdictMap.ContainsKey(sysId) ? _systemEdictMap[sysId] : 0;
            int megaLevel = _systemMegastructureLevel.ContainsKey(sysId) ? _systemMegastructureLevel[sysId] : 0;

            string coloniesList = "";
            if (systemPrimary.BoundVillages != null && systemPrimary.BoundVillages.Count > 0)
            {
                for (int i = 0; i < systemPrimary.BoundVillages.Count; i++)
                {
                    Village v = systemPrimary.BoundVillages[i];
                    if (v == null) continue;
                    string prod = (v.VillageType != null && v.VillageType.PrimaryProduction != null)
                        ? v.VillageType.PrimaryProduction.Name.ToString()
                        : "Raw Ore";
                    coloniesList += "  • " + v.Name + " — Output: " + prod + " (Hearth/Pop: " + (int)v.Hearth + ")\n";
                }
            }
            else
            {
                coloniesList = "  • No minor moon colonies bound to this orbital station.\n";
            }

            string desc =
                "=== ASTROGRAPHIC STAR-SYSTEM TELEMETRY ===\n" +
                "System Primary: " + systemPrimary.Name + "\n" +
                "Ruling Void-House: " + (systemPrimary.OwnerClan != null ? systemPrimary.OwnerClan.Name.ToString() : "Uncharted") + "\n" +
                "Active Stellaris Edict: " + GetEdictName(currentEdict) + "\n" +
                "Orbital Megastructure: " + GetMegastructureName(megaLevel) + " (Tier " + megaLevel + "/3)\n" +
                "Active Sector Crisis Alert: " + CrisisNames[_activeCrisisType % CrisisNames.Length] + " near " + _activeCrisisSystemName + "\n\n" +
                "--- Minor Colonies & Orbital Outposts in System ---\n" +
                coloniesList;

            List<InquiryElement> options = new List<InquiryElement>();
            options.Add(new InquiryElement(
                "edict",
                "Enact Stellaris Star-System Edict (Current: " + GetEdictName(currentEdict) + ")",
                new EmptyImageIdentifier(),
                true,
                "Issue system-wide colonial directives boosting Starport credits, shipyard output, or orbital defense."));

            options.Add(new InquiryElement(
                "mega",
                "Construct / Upgrade Orbital Megastructure (Tier " + megaLevel + "/3 -> " + GetMegastructureName(Math.Min(3, megaLevel + 1)) + ")",
                new EmptyImageIdentifier(),
                megaLevel < 3,
                "Invest 5,000 Credits to build an Orbital Ring Shipyard, Subspace Sensor Array, or Dyson Swarm Collector."));

            options.Add(new InquiryElement(
                "crisis",
                "Launch Galactic Crisis & Derelict Space Hulk Intercept (" + CrisisNames[_activeCrisisType % CrisisNames.Length] + ")",
                new EmptyImageIdentifier(),
                true,
                "Deploy your Voidship & Navis Marines to intercept outside xeno/pirate threats or board a derelict Space Hulk."));

            options.Add(new InquiryElement(
                "recruit_marines",
                "Recruit Navis Void-Marines & Strike-Skiff Pilots (1,200 Credits for 6 Specialists)",
                new EmptyImageIdentifier(),
                Hero.MainHero.Gold >= 1200,
                "Mustering bay at the Starport docks: enlist Phase-Plasma Marines and Zero-G Breachers directly into your crew."));

            options.Add(new InquiryElement(
                "close",
                "Return to Starport Concourse",
                new EmptyImageIdentifier(),
                true,
                "Close Astrographic Command."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Star-System Command: " + systemPrimary.Name,
                    desc,
                    options,
                    true,
                    1,
                    1,
                    "Execute Directive",
                    "Cancel",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        string cmd = selected[0].Identifier as string;
                        if (cmd == "edict")
                        {
                            OpenSystemEdictSelector(systemPrimary);
                        }
                        else if (cmd == "mega")
                        {
                            UpgradeSystemMegastructure(systemPrimary);
                        }
                        else if (cmd == "crisis")
                        {
                            OpenGalacticCrisisInterventionMenu(systemPrimary);
                        }
                        else if (cmd == "recruit_marines")
                        {
                            RecruitStarportMarines(systemPrimary);
                        }
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void OpenSystemEdictSelector(Settlement systemPrimary)
        {
            List<InquiryElement> list = new List<InquiryElement>();
            list.Add(new InquiryElement(1, "Orbital Shipyard Overdrive (+350 Credits/day & +10 Colony Hearth growth)", new EmptyImageIdentifier(), true, "Focuses minor asteroid colonies on Durasteel and Promethium refinement."));
            list.Add(new InquiryElement(2, "Technocracy Research & Trade Charter (+25% Starport Prosperity & +500 Credits/day)", new EmptyImageIdentifier(), true, "Stellaris Technocracy edict attracting Star Trader merchant syndicates."));
            list.Add(new InquiryElement(3, "Aegis Martial Void-Lockdown (+15 Orbital Militia & +25 Security/day)", new EmptyImageIdentifier(), true, "Fortifies orbital defense platforms and minor colony garrisons."));
            list.Add(new InquiryElement(4, "Terraforming & Hydroponics Surge (+Food & +20 Colony Hearth growth/day)", new EmptyImageIdentifier(), true, "Expands bio-domes across all bound moons in the star system."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Enact Stellaris System Edict — " + systemPrimary.Name,
                    "Select an interstellar governance edict for " + systemPrimary.Name + " and its bound minor colonies (Cost: 1,000 Credits):",
                    list,
                    true,
                    1,
                    1,
                    "Enact Edict",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        int edictId = (int)selected[0].Identifier;
                        if (Hero.MainHero.Gold >= 1000)
                        {
                            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1000, false);
                        }
                        _systemEdictMap[systemPrimary.StringId] = edictId;
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[Stellaris System Edict] Enacted '" + GetEdictName(edictId) + "' across " + systemPrimary.Name + " and its minor colonies!",
                            Color.FromUint(0xFF00FFAA)));
                        OpenStarSystemCommandDialog(systemPrimary);
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void UpgradeSystemMegastructure(Settlement systemPrimary)
        {
            int cur = _systemMegastructureLevel.ContainsKey(systemPrimary.StringId) ? _systemMegastructureLevel[systemPrimary.StringId] : 0;
            if (cur >= 3) return;
            if (Hero.MainHero.Gold < 5000)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Orbital Engineering] Requires 5,000 Credits to construct the next Megastructure stage.",
                    Color.FromUint(0xFFFF5555)));
                return;
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 5000, false);
            int next = cur + 1;
            _systemMegastructureLevel[systemPrimary.StringId] = next;
            if (Clan.PlayerClan != null)
            {
                Clan.PlayerClan.AddRenown(25f, true);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "[Megastructure Completed] Constructed " + GetMegastructureName(next) + " in orbit of " + systemPrimary.Name + "! (+25 Renown & Daily Sector Dividends)",
                Color.FromUint(0xFFFFD700)));
            OpenStarSystemCommandDialog(systemPrimary);
        }

        private void OpenGalacticCrisisInterventionMenu(Settlement systemPrimary)
        {
            List<InquiryElement> list = new List<InquiryElement>();
            list.Add(new InquiryElement(
                "pirate_raid",
                "Intercept Crimson Void-Pirate Flotilla (Reward: 4,500 Credits + +20 Renown + Contraband Cargo)",
                new EmptyImageIdentifier(),
                true,
                "Engage a syndicate raider pack preying on system freighters."));
            list.Add(new InquiryElement(
                "xeno_swarm",
                "Purge Prethoryn Xeno Bio-Swarm Brood-Ship (Reward: 8,000 Credits + +45 Renown + Xeno-Relics)",
                new EmptyImageIdentifier(),
                true,
                "Repel an extragalactic bio-fleet incursion threatening outer moon colonies."));
            list.Add(new InquiryElement(
                "space_hulk",
                "Board Derelict Archeotech Space Hulk (Reward: S-Class Weapons, Plasma Cells & 6,500 Credits)",
                new EmptyImageIdentifier(),
                true,
                "Send your Void-Marines in sealed carapace armor into a drifting pre-Imperium hulk."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Galactic Crisis & Anomaly Operations",
                    "Active Sector Threat: " + CrisisNames[_activeCrisisType % CrisisNames.Length] + "\nSelect an interstellar strike operation:",
                    list,
                    true,
                    1,
                    1,
                    "Launch Operation",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        string op = selected[0].Identifier as string;
                        ResolveCrisisOperation(op);
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void ResolveCrisisOperation(string op)
        {
            _crisisDefeatedCount++;
            int goldReward = 4500;
            float renownReward = 25f;
            string itemRewardId = "cig_good_promethium";
            string headline = "Crimson Void-Pirate Flotilla Neutralized!";

            if (op == "xeno_swarm")
            {
                goldReward = 8000;
                renownReward = 45f;
                itemRewardId = "cig_good_xeno_relics";
                headline = "Prethoryn Xeno Bio-Swarm Brood-Ship Purged!";
            }
            else if (op == "space_hulk")
            {
                goldReward = 6500;
                renownReward = 35f;
                itemRewardId = "cig_wpn_gauss_railgun";
                headline = "Derelict Space Hulk Salvaged!";
            }

            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, goldReward, false);
            if (Clan.PlayerClan != null)
            {
                Clan.PlayerClan.AddRenown(renownReward, true);
            }

            if (MBObjectManager.Instance != null && MobileParty.MainParty != null && MobileParty.MainParty.ItemRoster != null)
            {
                ItemObject rewardItem = MBObjectManager.Instance.GetObject<ItemObject>(itemRewardId);
                if (rewardItem != null)
                {
                    MobileParty.MainParty.ItemRoster.AddToCounts(rewardItem, 3);
                }
                ItemObject ammo = MBObjectManager.Instance.GetObject<ItemObject>("cig_ammo_photon_micro");
                if (ammo != null)
                {
                    MobileParty.MainParty.ItemRoster.AddToCounts(ammo, 2);
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "[Galactic Crisis Victory] " + headline + " Earned +" + goldReward + " Credits, +" + (int)renownReward + " Renown & Archeotech Salvage!",
                Color.FromUint(0xFF00FFCC)));
        }

        private void RecruitStarportMarines(Settlement systemPrimary)
        {
            if (Hero.MainHero.Gold < 1200 || MobileParty.MainParty == null || MobileParty.MainParty.MemberRoster == null || MBObjectManager.Instance == null)
            {
                return;
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1200, false);
            CharacterObject marine = MBObjectManager.Instance.GetObject<CharacterObject>("cig_void_t3_plasma_gunner");
            CharacterObject breacher = MBObjectManager.Instance.GetObject<CharacterObject>("cig_void_t2_breacher");
            if (marine != null) MobileParty.MainParty.MemberRoster.AddToCounts(marine, 4);
            if (breacher != null) MobileParty.MainParty.MemberRoster.AddToCounts(breacher, 2);

            InformationManager.DisplayMessage(new InformationMessage(
                "[Starport Mustering Bay] 4 Phase-Plasma Void Marines & 2 Zero-G Boarding Breachers joined your Flagship crew!",
                Color.FromUint(0xFF00E5FF)));
            OpenStarSystemCommandDialog(systemPrimary);
        }

        private void ProcessSystemEdictsAndMegastructures()
        {
            if (Settlement.All == null) return;
            int totalPlayerDividends = 0;

            foreach (Settlement s in Settlement.All)
            {
                if (s == null || (!s.IsTown && !s.IsCastle)) continue;
                int edict = _systemEdictMap.ContainsKey(s.StringId) ? _systemEdictMap[s.StringId] : 0;
                int mega = _systemMegastructureLevel.ContainsKey(s.StringId) ? _systemMegastructureLevel[s.StringId] : 0;

                if (edict == 0 && mega == 0) continue;

                if (s.Town != null)
                {
                    if (edict == 2) s.Town.Prosperity += 4f;
                    if (edict == 3) s.Town.Security += 4f;
                    if (mega > 0) s.Town.Prosperity += mega * 3f;
                }

                if (s.BoundVillages != null && (edict == 1 || edict == 4 || mega > 0))
                {
                    foreach (Village v in s.BoundVillages)
                    {
                        if (v != null)
                        {
                            v.Hearth += 2f + mega * 1.5f;
                        }
                    }
                }

                if (edict == 1) totalPlayerDividends += 350;
                if (edict == 2) totalPlayerDividends += 500;
                if (mega > 0) totalPlayerDividends += mega * 450;
            }

            if (totalPlayerDividends > 0 && Hero.MainHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, totalPlayerDividends, true);
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Star-System Telemetry] Daily Edict & Orbital Megastructure Dividends: +" + totalPlayerDividends + " Credits.",
                    Color.FromUint(0xFF88FFAA)));
            }
        }

        private void RotateGalacticCrisisAlerts()
        {
            _activeCrisisType = (_activeCrisisType + 1) % CrisisNames.Length;
            if (Settlement.All != null)
            {
                foreach (Settlement s in Settlement.All)
                {
                    if (s != null && s.IsTown && MBRandom.RandomFloat < 0.15f)
                    {
                        _activeCrisisSystemName = s.Name.ToString();
                        break;
                    }
                }
            }
        }

        private static string GetEdictName(int edictId)
        {
            switch (edictId)
            {
                case 1: return "Orbital Shipyard Overdrive";
                case 2: return "Technocracy Research & Trade Charter";
                case 3: return "Aegis Martial Void-Lockdown";
                case 4: return "Terraforming & Hydroponics Surge";
                default: return "Standard Colonial Autonomy";
            }
        }

        private static string GetMegastructureName(int level)
        {
            switch (level)
            {
                case 1: return "Subspace Deep-Sensor Array";
                case 2: return "Orbital Ring Star-Drydock";
                case 3: return "Dyson Swarm Energy Collector";
                default: return "None Constructed";
            }
        }

        #endregion

        #region 3. 40k Rogue Trader & Star Traders Vox-Hail Dialogues

        private void RegisterVoxHailDialogues(CampaignGameStarter starter)
        {
            if (starter == null) return;

            starter.AddPlayerLine(
                "cig_vox_hail_warrant",
                "lord_talk_speak_diplomacy_2",
                "cig_vox_hail_response",
                "{=!}[Vox-Hail] Present your Warrant of Trade & request Star-Sector Astrographic Telemetry.",
                null,
                delegate()
                {
                    if (Hero.OneToOneConversationHero != null && Clan.PlayerClan != null)
                    {
                        ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, 2, true, true);
                    }
                },
                100,
                null,
                null);

            starter.AddDialogLine(
                "cig_vox_hail_reply",
                "cig_vox_hail_response",
                "lord_start",
                "{=!}By the Warrant of the Star-Syndicates, Lord Captain! Our long-range augurs confirm sublight lanes are clear, though xeno bio-swarms and pirate flotillas prowl the outer asteroid belts. May your Void-Shields hold firm.",
                null,
                null,
                100,
                null);
        }

        #endregion
    }
}
