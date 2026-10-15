using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CalradiaIntergalactic
{
    /// <summary>
    /// Combines Starfield's modular ship builder &amp; reactor power allocation,
    /// Star Traders: Frontiers' hull/slot architecture &amp; Range-Band 5-to-1 ship combat,
    /// No Man's Sky's procedural C/B/A/S-Class component upgrades, and a 3D Walkable
    /// Flagship Bridge &amp; Command Deck view.
    /// </summary>
    public class VoidshipArchitectBehavior : CampaignBehaviorBase
    {
        public static VoidshipArchitectBehavior Instance { get; private set; }

        public string FlagshipName = "ISS Sovereign Warrant";
        public int HullClassIndex = 1;          // 0..6 (Scout Interceptor -> Retribution Dreadnought)
        public int ReactorTier = 1;             // 1..4 (C -> B -> A -> S-Class Archeotech)
        public int DriveTier = 1;               // 1..4
        public int ShieldTier = 1;              // 1..4
        public int PrimaryWeaponType = 0;       // 0=Plasma, 1=Lance Laser, 2=Gauss Rail, 3=Photon Torpedo, 4=Nova Cannon
        public int PrimaryWeaponTier = 1;       // 1..4
        public int SecondaryBayType = 0;        // 0=Flak Grid, 1=Strike-Craft Wing, 2=Boarding Pods, 3=Subspace ECM
        public int InternalModuleType = 0;      // 0=Cryo-Med Bay, 1=Cargo Vaults, 2=Astropathic Choir, 3=Munitions Forge
        public int WeaponPowerPips = 4;         // 1..8
        public int ShieldPowerPips = 4;         // 1..8
        public int ThrusterPowerPips = 4;       // 1..8
        public int CurrentHullIntegrity = 1100;

        public bool IsFlagshipBridgeWalkActive;

        public static readonly string[] HullClassNames = new string[]
        {
            "Class-I Horizon Scout Interceptor [Small Hull | 4 Bays]",
            "Class-II Valorian Syndicate Corvette [Fast Patrol | 5 Bays]",
            "Class-III Sword-Pattern Strike Frigate [Escort | 6 Bays]",
            "Class-IV Varian Broadside Destroyer [Heavy Line | 7 Bays]",
            "Class-V Dauntless Rogue Trader Light Cruiser [Flagship | 8 Bays]",
            "Class-VI Solaris Carrier Battlecruiser [Fleet Carrier | 9 Bays]",
            "Class-VII Aurelian Retribution Dreadnought [Capital Titan | 10 Bays]"
        };

        public static readonly int[] HullMaxIntegrity = new int[] { 750, 1100, 1750, 2500, 3600, 4800, 6800 };
        public static readonly int[] HullUpgradeCosts = new int[] { 0, 3500, 8000, 15000, 26000, 42000, 68000 };

        public static readonly string[] TierBadges = new string[]
        {
            "[C-Class Standard]",
            "[C-Class Standard]",
            "[B-Class Military]",
            "[A-Class Syndicate Prototype]",
            "[S-Class Exotic Archeotech]"
        };

        public static readonly string[] PrimaryWeaponNames = new string[]
        {
            "Twin Phase-Plasma Battery (Cyan Rapid Plasma Salvos)",
            "Solaris Ruby Lance-Laser Array (Crimson High-Velocity Beams)",
            "Valorian Hyper-Gauss Macro-Railguns (Emerald Armor-Piercing Slugs)",
            "Varian Photon-Torpedo Broadside Tubes (Violet Explosive Warheads)",
            "Archeotech Spinal Nova-Cannon (Golden Miniature-Star Detonation)"
        };

        public static readonly string[] SecondaryBayNames = new string[]
        {
            "Automated Point-Defense Flak-Laser Grid (+Turret Fire Rate)",
            "Valkyrie Strike-Craft Hangar Wing (+Air/Orbital Escort Fire)",
            "Assault Boarding Torpedo & Drop-Pod Bay (+Boarding Shock Damage)",
            "Subspace ECM & Void-Shield Harmonic Booster (+25% Shield Capacity)"
        };

        public static readonly string[] InternalModuleNames = new string[]
        {
            "Vitae Cryo-Med Bay (Heals Wounded Crew Daily & Post-Battle)",
            "Zero-G Cryo-Cargo Vaults (+600 Credits/day Syndicate Freight)",
            "Astropathic Choir & Nav-Telemetry (+Renown & Tactical Advantage)",
            "Omnissian Munitions Auto-Forge (Synthesizes Plasma/Rail/Photon Ammo Daily)"
        };

        public VoidshipArchitectBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            Instance = this;
            dataStore.SyncData("cig_flagshipName", ref FlagshipName);
            dataStore.SyncData("cig_hullClassIndex", ref HullClassIndex);
            dataStore.SyncData("cig_reactorTier", ref ReactorTier);
            dataStore.SyncData("cig_driveTier", ref DriveTier);
            dataStore.SyncData("cig_shieldTier", ref ShieldTier);
            dataStore.SyncData("cig_primaryWeaponType", ref PrimaryWeaponType);
            dataStore.SyncData("cig_primaryWeaponTier", ref PrimaryWeaponTier);
            dataStore.SyncData("cig_secondaryBayType", ref SecondaryBayType);
            dataStore.SyncData("cig_internalModuleType", ref InternalModuleType);
            dataStore.SyncData("cig_weaponPips", ref WeaponPowerPips);
            dataStore.SyncData("cig_shieldPips", ref ShieldPowerPips);
            dataStore.SyncData("cig_thrusterPips", ref ThrusterPowerPips);
            dataStore.SyncData("cig_hullIntegrity", ref CurrentHullIntegrity);

            if (string.IsNullOrEmpty(FlagshipName)) FlagshipName = "ISS Sovereign Warrant";
            HullClassIndex = Clamp(HullClassIndex, 0, HullClassNames.Length - 1);
            ReactorTier = Clamp(ReactorTier, 1, 4);
            DriveTier = Clamp(DriveTier, 1, 4);
            ShieldTier = Clamp(ShieldTier, 1, 4);
            PrimaryWeaponType = Clamp(PrimaryWeaponType, 0, PrimaryWeaponNames.Length - 1);
            PrimaryWeaponTier = Clamp(PrimaryWeaponTier, 1, 4);
            SecondaryBayType = Clamp(SecondaryBayType, 0, SecondaryBayNames.Length - 1);
            InternalModuleType = Clamp(InternalModuleType, 0, InternalModuleNames.Length - 1);
        }

        public string GetCurrentHullTitle()
        {
            return HullClassNames[Clamp(HullClassIndex, 0, HullClassNames.Length - 1)];
        }

        public int GetMaxHullIntegrity()
        {
            return HullMaxIntegrity[Clamp(HullClassIndex, 0, HullMaxIntegrity.Length - 1)] + (ReactorTier * 150);
        }

        public float GetMaxPlayerVoidShieldHp()
        {
            float baseShield = 120f + (ShieldTier * 95f) + (HullClassIndex * 45f);
            if (SecondaryBayType == 3) baseShield *= 1.25f;
            float pipMult = 0.7f + (ShieldPowerPips * 0.12f);
            return baseShield * pipMult;
        }

        public float GetWeaponDamageMultiplier()
        {
            float baseMult = 1.0f + (PrimaryWeaponTier * 0.22f) + (HullClassIndex * 0.08f);
            float pipMult = 0.75f + (WeaponPowerPips * 0.12f);
            return baseMult * pipMult;
        }

        public float GetThrusterSpeedMultiplier()
        {
            float baseMult = 1.0f + (DriveTier * 0.20f) + (ThrusterPowerPips * 0.10f);
            return baseMult;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (starter == null) return;

            string[] menus = new string[] { "town", "castle", "village" };
            foreach (string menuId in menus)
            {
                starter.AddGameMenuOption(
                    menuId,
                    "cig_voidship_drydock_" + menuId,
                    "{=!}Voidship Drydock: Ship Designer, Loadouts & 3D Flagship Deck",
                    delegate(MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                        return true;
                    },
                    delegate(MenuCallbackArgs args)
                    {
                        OpenVoidshipDrydockHub();
                    },
                    false,
                    5,
                    false);
            }
        }

        private void OnHourlyTickParty(MobileParty party)
        {
            if (party == null || party != MobileParty.MainParty) return;

            // Sublight & Warp Drive keeps crew morale high and heals wounded crew if Cryo-Med Bay is installed
            if (InternalModuleType == 0 && party.MemberRoster != null && MBRandom.RandomFloat < 0.25f)
            {
                for (int i = 0; i < party.MemberRoster.Count; i++)
                {
                    TroopRosterElement elem = party.MemberRoster.GetElementCopyAtIndex(i);
                    if (elem.WoundedNumber > 0)
                    {
                        party.MemberRoster.AddToCountsAtIndex(i, 0, -1, 0, true);
                        break;
                    }
                }
            }
        }

        private void OnDailyTick()
        {
            // Apply daily Internal Deck Module benefits
            CurrentHullIntegrity = Math.Min(GetMaxHullIntegrity(), CurrentHullIntegrity + 150);

            if (Hero.MainHero == null || MobileParty.MainParty == null) return;

            if (InternalModuleType == 1)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 600 + HullClassIndex * 150, true);
            }
            else if (InternalModuleType == 2 && Clan.PlayerClan != null)
            {
                Clan.PlayerClan.AddRenown(8f + HullClassIndex * 2f, true);
            }
            else if (InternalModuleType == 3 && MobileParty.MainParty.ItemRoster != null && MBObjectManager.Instance != null)
            {
                ItemObject plasma = MBObjectManager.Instance.GetObject<ItemObject>("cig_ammo_plasma_cell");
                ItemObject rail = MBObjectManager.Instance.GetObject<ItemObject>("cig_ammo_rail_slug");
                ItemObject photon = MBObjectManager.Instance.GetObject<ItemObject>("cig_ammo_photon_micro");
                if (plasma != null) MobileParty.MainParty.ItemRoster.AddToCounts(plasma, 1);
                if (rail != null) MobileParty.MainParty.ItemRoster.AddToCounts(rail, 1);
                if (photon != null) MobileParty.MainParty.ItemRoster.AddToCounts(photon, 1);
            }
        }

        public void OpenVoidshipDrydockHub()
        {
            int maxHull = GetMaxHullIntegrity();
            if (CurrentHullIntegrity > maxHull) CurrentHullIntegrity = maxHull;

            // Procedural Starport Tech Quality Seed (No Man's Sky / Starfield style)
            string starportName = Settlement.CurrentSettlement != null ? Settlement.CurrentSettlement.Name.ToString() : "Orbital Dock";
            int procSeed = Math.Abs(starportName.GetHashCode() + (int)CampaignTime.Now.ToDays);
            int availableTechTier = 2 + (procSeed % 3); // B-Class (2), A-Class (3), or S-Class Archeotech (4)!

            string summary =
                "=== FLAGSHIP TELEMETRY: " + FlagshipName.ToUpperInvariant() + " ===\n" +
                "• Hull Class: " + HullClassNames[HullClassIndex] + " (Integrity: " + CurrentHullIntegrity + " / " + maxHull + ")\n" +
                "• Reactor Core: " + TierBadges[ReactorTier] + " (" + (40 + ReactorTier * 40) + " MW Output)\n" +
                "• Warp & Sublight Drive: " + TierBadges[DriveTier] + " (3D Flight Thrust: x" + GetThrusterSpeedMultiplier().ToString("0.00") + ")\n" +
                "• Void-Shield Array: " + TierBadges[ShieldTier] + " (Personal/Ship Shield Pool: " + (int)GetMaxPlayerVoidShieldHp() + " HP)\n" +
                "• Spinal Weapon Bay: " + TierBadges[PrimaryWeaponTier] + " " + PrimaryWeaponNames[PrimaryWeaponType] + " (Dmg: x" + GetWeaponDamageMultiplier().ToString("0.00") + ")\n" +
                "• Secondary Turret/Hangar Bay: " + SecondaryBayNames[SecondaryBayType] + "\n" +
                "• Internal Deck Module: " + InternalModuleNames[InternalModuleType] + "\n" +
                "• Reactor Power Pips: [Weapons: " + WeaponPowerPips + "] [Void-Shields: " + ShieldPowerPips + "] [Thrusters: " + ThrusterPowerPips + "]\n" +
                "• Local Starport Procedural Tech Roll: Up to " + TierBadges[availableTechTier] + " Modules in Stock!";

            List<InquiryElement> options = new List<InquiryElement>();

            options.Add(new InquiryElement(
                "walk_bridge",
                "Step onto 3D Flagship Bridge & Command Deck (Walkable Spaceship View!)",
                new EmptyImageIdentifier(),
                true,
                "Enter a real-time 3D walkable interior of your Flagship's Command Bridge with Holo-Starmap, Warp Reactor Core, and Bridge Officers."));

            options.Add(new InquiryElement(
                "upgrade_hull",
                "Upgrade Flagship Hull Class (Current: Class-" + (HullClassIndex + 1) + ")",
                new EmptyImageIdentifier(),
                HullClassIndex < HullClassNames.Length - 1,
                "Refit your Flagship to a larger Star Traders / Rogue Trader capital hull class."));

            options.Add(new InquiryElement(
                "swap_weapons",
                "Swap Spinal & Secondary Weapon Loadout (" + PrimaryWeaponNames[PrimaryWeaponType] + ")",
                new EmptyImageIdentifier(),
                true,
                "Configure your ship's Pew-Pew Plasma, Lance Laser, Gauss Railgun, Photon Torpedo, or Nova-Cannon bays."));

            options.Add(new InquiryElement(
                "procedural_upgrades",
                "Install Procedural Drydock Component Upgrades (Local Roll: " + TierBadges[availableTechTier] + ")",
                new EmptyImageIdentifier(),
                true,
                "Upgrade Reactor Core, Sublight/Warp Drive, Void-Shield Generator, and Weapon Tiers."));

            options.Add(new InquiryElement(
                "internal_bay",
                "Configure Internal Deck Module (" + InternalModuleNames[InternalModuleType] + ")",
                new EmptyImageIdentifier(),
                true,
                "Swap between Vitae Cryo-Med Bay, Zero-G Cargo Vaults, Astropathic Choir, or Omnissian Ammo Forge."));

            options.Add(new InquiryElement(
                "power_pips",
                "Allocate Reactor Power Pips (WPN " + WeaponPowerPips + " / SHD " + ShieldPowerPips + " / THR " + ThrusterPowerPips + ")",
                new EmptyImageIdentifier(),
                true,
                "Starfield-style power distribution between Weapons, Void-Shields, and 3D Flight Thrusters."));

            options.Add(new InquiryElement(
                "range_band_sim",
                "Run Star Traders Range-Band 5-to-1 Broadside Combat Drill (+XP & Credits)",
                new EmptyImageIdentifier(),
                true,
                "Test your Flagship's loadout in a Star Traders: Frontiers Range-Band 5-to-1 ship-to-ship tactical duel."));

            options.Add(new InquiryElement(
                "rename_ship",
                "Christen / Rename Flagship (" + FlagshipName + ")",
                new EmptyImageIdentifier(),
                true,
                "Enter a custom registry name for your Voidship."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Starport Drydock & Voidship Architect",
                    summary,
                    options,
                    true,
                    1,
                    1,
                    "Select System",
                    "Exit Drydock",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        string choice = selected[0].Identifier as string;
                        if (choice == "walk_bridge")
                        {
                            LaunchWalkableFlagshipDeckMission();
                        }
                        else if (choice == "upgrade_hull")
                        {
                            OpenHullUpgradeMenu();
                        }
                        else if (choice == "swap_weapons")
                        {
                            OpenWeaponLoadoutMenu();
                        }
                        else if (choice == "procedural_upgrades")
                        {
                            OpenProceduralComponentUpgradeMenu(availableTechTier);
                        }
                        else if (choice == "internal_bay")
                        {
                            OpenInternalDeckModuleMenu();
                        }
                        else if (choice == "power_pips")
                        {
                            OpenReactorPowerPipMenu();
                        }
                        else if (choice == "range_band_sim")
                        {
                            OpenRangeBandTacticalDuel(1);
                        }
                        else if (choice == "rename_ship")
                        {
                            InformationManager.ShowTextInquiry(
                                new TextInquiryData(
                                    "Christen Voidship",
                                    "Enter a new interstellar registry name for your Flagship:",
                                    true,
                                    true,
                                    "Confirm Name",
                                    "Cancel",
                                    delegate(string newName)
                                    {
                                        if (!string.IsNullOrEmpty(newName))
                                        {
                                            FlagshipName = newName.Trim();
                                            InformationManager.DisplayMessage(new InformationMessage(
                                                "[Starport Registry] Flagship christened: " + FlagshipName,
                                                Color.FromUint(0xFF00E5FF)));
                                        }
                                        OpenVoidshipDrydockHub();
                                    },
                                    null,
                                    false,
                                    null,
                                    "",
                                    FlagshipName),
                                true,
                                false);
                        }
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void OpenHullUpgradeMenu()
        {
            List<InquiryElement> list = new List<InquiryElement>();
            for (int i = 0; i < HullClassNames.Length; i++)
            {
                int cost = HullUpgradeCosts[i];
                bool canAfford = Hero.MainHero.Gold >= cost;
                string label = HullClassNames[i] + " — Max Hull: " + HullMaxIntegrity[i] + " (" + (i == HullClassIndex ? "INSTALLED" : cost + " Credits") + ")";
                list.Add(new InquiryElement(i, label, new EmptyImageIdentifier(), canAfford && i != HullClassIndex, "Refit flagship hull frame."));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Starport Capital Hull Refit",
                    "Select a new Starship Hull Class for " + FlagshipName + ":",
                    list,
                    true,
                    1,
                    1,
                    "Commission Hull",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected != null && selected.Count > 0)
                        {
                            int idx = (int)selected[0].Identifier;
                            int cost = HullUpgradeCosts[idx];
                            if (Hero.MainHero.Gold >= cost)
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);
                                HullClassIndex = idx;
                                CurrentHullIntegrity = GetMaxHullIntegrity();
                                InformationManager.DisplayMessage(new InformationMessage(
                                    "[Drydock Refit] " + FlagshipName + " upgraded to " + HullClassNames[HullClassIndex] + "!",
                                    Color.FromUint(0xFFFFD700)));
                            }
                        }
                        OpenVoidshipDrydockHub();
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void OpenWeaponLoadoutMenu()
        {
            List<InquiryElement> list = new List<InquiryElement>();
            for (int i = 0; i < PrimaryWeaponNames.Length; i++)
            {
                list.Add(new InquiryElement(
                    "pri_" + i,
                    "[Spinal Weapon] " + PrimaryWeaponNames[i] + (PrimaryWeaponType == i ? " (ACTIVE)" : " (1,500 Credits)"),
                    new EmptyImageIdentifier(),
                    true,
                    "Determines your Flagship's primary orbital turret salvos & Pew-Pew beam color in 3D space battles."));
            }
            for (int j = 0; j < SecondaryBayNames.Length; j++)
            {
                list.Add(new InquiryElement(
                    "sec_" + j,
                    "[Secondary Bay] " + SecondaryBayNames[j] + (SecondaryBayType == j ? " (ACTIVE)" : " (1,200 Credits)"),
                    new EmptyImageIdentifier(),
                    true,
                    "Configures point-defense turrets, strike-craft wings, boarding pods, or shield harmonics."));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Voidship Weapons & Turret Loadout",
                    "Select Spinal Battery or Secondary Bay to equip on " + FlagshipName + ":",
                    list,
                    true,
                    1,
                    1,
                    "Install Loadout",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected != null && selected.Count > 0)
                        {
                            string id = selected[0].Identifier as string;
                            if (id != null && id.StartsWith("pri_"))
                            {
                                int idx = int.Parse(id.Substring(4));
                                if (Hero.MainHero.Gold >= 1500 && PrimaryWeaponType != idx)
                                {
                                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1500, false);
                                }
                                PrimaryWeaponType = idx;
                                InformationManager.DisplayMessage(new InformationMessage(
                                    "[Weapons Bay] Installed " + PrimaryWeaponNames[PrimaryWeaponType] + " on " + FlagshipName + "!",
                                    Color.FromUint(0xFF00E5FF)));
                            }
                            else if (id != null && id.StartsWith("sec_"))
                            {
                                int idx = int.Parse(id.Substring(4));
                                if (Hero.MainHero.Gold >= 1200 && SecondaryBayType != idx)
                                {
                                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1200, false);
                                }
                                SecondaryBayType = idx;
                                InformationManager.DisplayMessage(new InformationMessage(
                                    "[Secondary Bay] Installed " + SecondaryBayNames[SecondaryBayType] + " on " + FlagshipName + "!",
                                    Color.FromUint(0xFF00E5FF)));
                            }
                        }
                        OpenVoidshipDrydockHub();
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void OpenProceduralComponentUpgradeMenu(int maxLocalTier)
        {
            List<InquiryElement> list = new List<InquiryElement>();
            int nextReactor = Math.Min(maxLocalTier, ReactorTier + 1);
            int nextDrive = Math.Min(maxLocalTier, DriveTier + 1);
            int nextShield = Math.Min(maxLocalTier, ShieldTier + 1);
            int nextWeapon = Math.Min(maxLocalTier, PrimaryWeaponTier + 1);

            list.Add(new InquiryElement("reactor", "Upgrade Reactor Core -> " + TierBadges[nextReactor] + " (3,000 Credits)", new EmptyImageIdentifier(), ReactorTier < maxLocalTier && Hero.MainHero.Gold >= 3000, "Increases total MW power output and +150 Hull Integrity."));
            list.Add(new InquiryElement("drive", "Upgrade Warp & Sublight Grav-Drive -> " + TierBadges[nextDrive] + " (2,800 Credits)", new EmptyImageIdentifier(), DriveTier < maxLocalTier && Hero.MainHero.Gold >= 2800, "Boosts 3D space flight thrust speed and campaign sublight speed."));
            list.Add(new InquiryElement("shield", "Upgrade Void-Shield Generator -> " + TierBadges[nextShield] + " (3,200 Credits)", new EmptyImageIdentifier(), ShieldTier < maxLocalTier && Hero.MainHero.Gold >= 3200, "Increases recharging Void-Shield HP in 3D space battles and ship duels."));
            list.Add(new InquiryElement("weapon", "Upgrade Spinal Weapon Capacitors -> " + TierBadges[nextWeapon] + " (3,500 Credits)", new EmptyImageIdentifier(), PrimaryWeaponTier < maxLocalTier && Hero.MainHero.Gold >= 3500, "Increases Pew-Pew energy weapon & orbital turret damage by +22%."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Procedural Drydock Component Upgrades",
                    "This Starport's engineers currently stock up to " + TierBadges[maxLocalTier] + " components:",
                    list,
                    true,
                    1,
                    1,
                    "Install Upgrade",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected != null && selected.Count > 0)
                        {
                            string slot = selected[0].Identifier as string;
                            if (slot == "reactor")
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 3000, false);
                                ReactorTier = nextReactor;
                                CurrentHullIntegrity = GetMaxHullIntegrity();
                            }
                            else if (slot == "drive")
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 2800, false);
                                DriveTier = nextDrive;
                            }
                            else if (slot == "shield")
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 3200, false);
                                ShieldTier = nextShield;
                            }
                            else if (slot == "weapon")
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 3500, false);
                                PrimaryWeaponTier = nextWeapon;
                            }
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Drydock Engineering] Installed " + TierBadges[maxLocalTier] + " upgrade on " + FlagshipName + "!",
                                Color.FromUint(0xFF00FFAA)));
                        }
                        OpenVoidshipDrydockHub();
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void OpenInternalDeckModuleMenu()
        {
            List<InquiryElement> list = new List<InquiryElement>();
            for (int i = 0; i < InternalModuleNames.Length; i++)
            {
                list.Add(new InquiryElement(i, InternalModuleNames[i] + (InternalModuleType == i ? " (INSTALLED)" : " (1,000 Credits)"), new EmptyImageIdentifier(), true, "Reconfigures your Flagship's lower engineering & crew decks."));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Internal Deck & Crew Module",
                    "Select an Internal Deck Module for " + FlagshipName + ":",
                    list,
                    true,
                    1,
                    1,
                    "Install Module",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected != null && selected.Count > 0)
                        {
                            int idx = (int)selected[0].Identifier;
                            if (InternalModuleType != idx && Hero.MainHero.Gold >= 1000)
                            {
                                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1000, false);
                            }
                            InternalModuleType = idx;
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Internal Deck] Installed " + InternalModuleNames[InternalModuleType] + "!",
                                Color.FromUint(0xFF00E5FF)));
                        }
                        OpenVoidshipDrydockHub();
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        private void OpenReactorPowerPipMenu()
        {
            List<InquiryElement> list = new List<InquiryElement>();
            list.Add(new InquiryElement("atk", "Overcharge Weapons Array [WPN: 7 | SHD: 3 | THR: 2] (+59% Pew-Pew & Lance Damage)", new EmptyImageIdentifier(), true, "Diverts reactor plasma to spinal lances, turrets, and phase-rifles."));
            list.Add(new InquiryElement("shd", "Fortress Void-Shields [WPN: 3 | SHD: 7 | THR: 2] (+54% Void-Shield Capacity & Recharge)", new EmptyImageIdentifier(), true, "Maximizes Void-Shield dome strength for heavy boarding & orbital fire."));
            list.Add(new InquiryElement("thr", "Interceptor Afterburners [WPN: 3 | SHD: 2 | THR: 7] (+70% 3D Space Flight Thrust)", new EmptyImageIdentifier(), true, "Maximizes 3D zero-G flight velocity and sublight evasion."));
            list.Add(new InquiryElement("bal", "Balanced Fleet Trim [WPN: 4 | SHD: 4 | THR: 4] (All-Around Equilibrium)", new EmptyImageIdentifier(), true, "Standard Navis Imperialis reactor balance."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Reactor Core Power Pip Distribution",
                    "Current Pips: [Weapons: " + WeaponPowerPips + "] [Void-Shields: " + ShieldPowerPips + "] [Thrusters: " + ThrusterPowerPips + "]",
                    list,
                    true,
                    1,
                    1,
                    "Route Reactor Power",
                    "Back",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected != null && selected.Count > 0)
                        {
                            string mode = selected[0].Identifier as string;
                            if (mode == "atk") { WeaponPowerPips = 7; ShieldPowerPips = 3; ThrusterPowerPips = 2; }
                            else if (mode == "shd") { WeaponPowerPips = 3; ShieldPowerPips = 7; ThrusterPowerPips = 2; }
                            else if (mode == "thr") { WeaponPowerPips = 3; ShieldPowerPips = 2; ThrusterPowerPips = 7; }
                            else { WeaponPowerPips = 4; ShieldPowerPips = 4; ThrusterPowerPips = 4; }

                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Enginarium Telemetry] Reactor Pips routed -> WPN:" + WeaponPowerPips + " SHD:" + ShieldPowerPips + " THR:" + ThrusterPowerPips,
                                Color.FromUint(0xFFFFAA00)));
                        }
                        OpenVoidshipDrydockHub();
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        #region Star Traders: Frontiers Range-Band 5-to-1 Ship Combat

        public void OpenRangeBandTacticalDuel(int currentRangeBandInt)
        {
            int rangeBand = Clamp(5 - (currentRangeBandInt - 1), 1, 5);
            int enemyHull = 450 * rangeBand;
            int playerDmg = (int)(260 * GetWeaponDamageMultiplier());
            int playerShield = (int)GetMaxPlayerVoidShieldHp();

            string desc =
                "=== STAR TRADERS VOIDSHIP COMBAT: RANGE BAND " + rangeBand + " ===\n" +
                "Your Flagship: " + FlagshipName + " (Hull: " + CurrentHullIntegrity + " | Void-Shields: " + playerShield + ")\n" +
                "Hostile Vessel: Syndicate Corsair Frigate (Est. Hull: " + enemyHull + " | Range Band: " + rangeBand + ")\n" +
                "Spinal Battery Ready: " + PrimaryWeaponNames[PrimaryWeaponType] + " (Salvo Output: ~" + playerDmg + " Dmg)\n\n" +
                "Choose your Bridge Order for Range Band " + rangeBand + ":";

            List<InquiryElement> orders = new List<InquiryElement>();
            orders.Add(new InquiryElement(
                "fire_advance",
                "Fire Spinal Battery & Advance 1 Range Band Closer (Dealt ~" + playerDmg + " Hull/Shield Dmg)",
                new EmptyImageIdentifier(),
                true,
                "Fires " + PrimaryWeaponNames[PrimaryWeaponType] + " and closes distance for boarding/point-blank broadsides."));

            orders.Add(new InquiryElement(
                "broadside_hold",
                "Full Macro-Broadside & Overcharge Void-Shields (+Shield Absorption & Heavy Salvo)",
                new EmptyImageIdentifier(),
                true,
                "Maintains range while unleashing all broadside turrets and reinforcing deflector screens."));

            orders.Add(new InquiryElement(
                "boarding_finish",
                "Launch Assault Boarding Pods & Photon Torpedo Coup-de-Grâce (Resolve Engagement)",
                new EmptyImageIdentifier(),
                true,
                "Cripples enemy reactor and boards their command deck for salvage & bounty."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Voidship Tactical Bridge — Range Band " + rangeBand,
                    desc,
                    orders,
                    true,
                    1,
                    1,
                    "Execute Bridge Order",
                    "Disengage",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        string cmd = selected[0].Identifier as string;
                        if (cmd == "boarding_finish" || rangeBand <= 1)
                        {
                            int bounty = 2200 + (HullClassIndex * 600) + (PrimaryWeaponTier * 400);
                            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, bounty, false);
                            if (Clan.PlayerClan != null) Clan.PlayerClan.AddRenown(15f, true);
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Voidship Victory] Hostile vessel crippled & boarded at Range Band " + rangeBand + "! Salvaged +" + bounty + " Credits & +15 Renown!",
                                Color.FromUint(0xFF00FFAA)));
                            OpenVoidshipDrydockHub();
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Range Band " + rangeBand + " Salvo] " + FlagshipName + " unleashed " + PrimaryWeaponNames[PrimaryWeaponType] + " for " + playerDmg + " damage!",
                                Color.FromUint(0xFF00E5FF)));
                            OpenRangeBandTacticalDuel(currentRangeBandInt + 1);
                        }
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        #endregion

        #region Walkable 3D Flagship Bridge & Command Deck View

        public void LaunchWalkableFlagshipDeckMission()
        {
            IsFlagshipBridgeWalkActive = true;

            try
            {
                Settlement current = Settlement.CurrentSettlement;
                if (current != null && current.LocationComplex != null)
                {
                    Location bridgeLoc = current.LocationComplex.GetLocationWithId("lordshall");
                    if (bridgeLoc == null)
                    {
                        bridgeLoc = current.LocationComplex.GetLocationWithId("tavern");
                    }
                    if (bridgeLoc != null)
                    {
                        string sceneName = bridgeLoc.GetSceneName(3);
                        if (!string.IsNullOrEmpty(sceneName))
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Flagship Deck Turbolift] Stepping onto the 3D Command Bridge of " + FlagshipName + "! (Press [B] on deck for Ship Systems)",
                                Color.FromUint(0xFF00E5FF)));
                            CampaignMission.OpenIndoorMission(sceneName, 3, bridgeLoc, null);
                            return;
                        }
                    }
                }

                // Fallback if in a village colony without an indoor LocationComplex: open a 3D Flagship Deck scene directly
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Flagship Deck Turbolift] Boarding " + FlagshipName + " Command Deck! (Press [B] in 3D view for Ship Systems)",
                    Color.FromUint(0xFF00E5FF)));
                CampaignMission.OpenBattleMission("empire_interior_tavern_a", false);
            }
            catch (Exception)
            {
                IsFlagshipBridgeWalkActive = false;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Flagship Bridge] Dock at any Starport or Orbital Starbase to walk the 3D Command Deck.",
                    Color.FromUint(0xFFFFAA00)));
            }
        }

        #endregion

        private static int Clamp(int val, int min, int max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }
    }
}
