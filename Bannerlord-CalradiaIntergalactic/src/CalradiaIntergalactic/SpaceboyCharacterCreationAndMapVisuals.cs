using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace CalradiaIntergalactic
{
    /// <summary>
    /// Converts Character Creation (Cultures, Cultural Feats, Narrative Menus, Options, 3D Character Loadouts,
    /// and Post-Creation Tutorial Skip) into an end-to-end "Spaceboy" sci-fi origin experience.
    /// </summary>
    public class SpaceboyCharacterCreationHandler : ICharacterCreationContentHandler
    {
        public void InitializeContent(CharacterCreationManager characterCreationManager)
        {
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllCulturesAndFeatsToSciFi();
        }

        public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
        {
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllCulturesAndFeatsToSciFi();
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertCharacterCreationEquipmentRosters();

            if (characterCreationManager == null || characterCreationManager.NarrativeMenus == null)
            {
                return;
            }

            try
            {
                FieldInfo menuTitleField = typeof(NarrativeMenu).GetField("Title", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo menuDescField = typeof(NarrativeMenu).GetField("Description", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo optTextField = typeof(NarrativeMenuOption).GetField("Text", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo optDescField = typeof(NarrativeMenuOption).GetField("DescriptionText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo onSelectField = typeof(NarrativeMenuOption).GetField("_onSelectInternal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo onConseqField = typeof(NarrativeMenuOption).GetField("_onConsequenceInternal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                MBEquipmentRoster captainRoster = SpaceboyGalacticMapAndCommissioningBehavior.CreateSpaceboyEquipmentRoster(true);
                MBEquipmentRoster officerRoster = SpaceboyGalacticMapAndCommissioningBehavior.CreateSpaceboyEquipmentRoster(false);

                int menuIndex = 0;
                foreach (NarrativeMenu menu in characterCreationManager.NarrativeMenus)
                {
                    if (menu == null) continue;

                    string menuId = menu.StringId ?? ("stage_" + menuIndex);
                    string sciFiTitle;
                    string sciFiDesc;
                    GetSciFiMenuHeader(menuId, menuIndex, out sciFiTitle, out sciFiDesc);

                    if (menuTitleField != null)
                    {
                        menuTitleField.SetValue(menu, new TextObject("{=!}" + sciFiTitle));
                    }
                    if (menuDescField != null)
                    {
                        menuDescField.SetValue(menu, new TextObject("{=!}" + sciFiDesc));
                    }

                    // Convert all 3D Character Creation preview characters into Spaceboy / Starfleet outfits
                    ApplySpaceboyOutfitsToMenuCharacters(menu, captainRoster, officerRoster);

                    // Convert all Narrative Menu Options into Spaceboy Starfleet & Syndicate backgrounds
                    if (menu.CharacterCreationMenuOptions != null)
                    {
                        for (int optIdx = 0; optIdx < menu.CharacterCreationMenuOptions.Count; optIdx++)
                        {
                            NarrativeMenuOption opt = menu.CharacterCreationMenuOptions[optIdx];
                            if (opt == null) continue;

                            string optTitle;
                            string optDesc;
                            GetSciFiOptionText(menuId, menuIndex, optIdx, out optTitle, out optDesc);

                            if (optTextField != null)
                            {
                                optTextField.SetValue(opt, new TextObject("{=!}" + optTitle));
                            }
                            if (optDescField != null)
                            {
                                optDescField.SetValue(opt, new TextObject("{=!}" + optDesc));
                            }

                            // Wrap _onSelectInternal so selecting any option keeps 3D characters in Spaceboy armor & plasma weapons
                            if (onSelectField != null)
                            {
                                NarrativeMenuOptionOnSelectDelegate origSelect = onSelectField.GetValue(opt) as NarrativeMenuOptionOnSelectDelegate;
                                opt.SetOnSelect(delegate(CharacterCreationManager mgr)
                                {
                                    if (origSelect != null)
                                    {
                                        try { origSelect(mgr); } catch { }
                                    }
                                    if (mgr != null && mgr.CurrentMenu != null)
                                    {
                                        ApplySpaceboyOutfitsToMenuCharacters(mgr.CurrentMenu, captainRoster, officerRoster);
                                    }
                                });
                            }

                            // Wrap _onConsequenceInternal so finalizing any option preserves Spaceboy armor & weapons
                            if (onConseqField != null)
                            {
                                NarrativeMenuOptionOnConsequenceDelegate origConseq = onConseqField.GetValue(opt) as NarrativeMenuOptionOnConsequenceDelegate;
                                opt.SetOnConsequence(delegate(CharacterCreationManager mgr)
                                {
                                    if (origConseq != null)
                                    {
                                        try { origConseq(mgr); } catch { }
                                    }
                                    if (mgr != null && mgr.CurrentMenu != null)
                                    {
                                        ApplySpaceboyOutfitsToMenuCharacters(mgr.CurrentMenu, captainRoster, officerRoster);
                                    }
                                });
                            }
                        }
                    }

                    menuIndex++;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ApplySpaceboyOutfitsToMenuCharacters(NarrativeMenu menu, MBEquipmentRoster captainRoster, MBEquipmentRoster officerRoster)
        {
            if (menu == null || menu.Characters == null) return;
            for (int cIdx = 0; cIdx < menu.Characters.Count; cIdx++)
            {
                NarrativeMenuCharacter nmChar = menu.Characters[cIdx];
                if (nmChar == null || !nmChar.IsHuman) continue;

                bool isMainOrPrimary = (cIdx == 0) || (nmChar.StringId != null && nmChar.StringId.Contains("player"));
                MBEquipmentRoster targetRoster = isMainOrPrimary ? captainRoster : officerRoster;
                if (targetRoster != null)
                {
                    nmChar.SetEquipment(targetRoster);
                }

                nmChar.SetRightHandItem("cig_wpn_vibro_blade");
                nmChar.SetLeftHandItem("cig_wpn_plasma_rifle");
            }
        }

        public void OnStageCompleted(CharacterCreationStageBase stage)
        {
            SpaceboyGalacticMapAndCommissioningBehavior.ConvertAllCulturesAndFeatsToSciFi();
            SpaceboyGalacticMapAndCommissioningBehavior.ApplySpaceboyEquipmentToMainHeroAndPlayerCharacter();
        }

        public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
        {
            // 1. Prevent StoryMode from forcing the medieval sword/bow training field mission
            SpaceboyGalacticMapAndCommissioningBehavior.PreventMedievalTrainingFieldMission();

            // 2. Equip PlayerCharacter & MainHero with Spaceboy Void-Captain gear before TutorialPhase backs it up
            SpaceboyGalacticMapAndCommissioningBehavior.ApplySpaceboyEquipmentToMainHeroAndPlayerCharacter();
        }

        private static void GetSciFiMenuHeader(string menuId, int index, out string title, out string desc)
        {
            string id = (menuId ?? "").ToLowerInvariant();
            if (id.Contains("parent"))
            {
                title = "Spaceboy Origin: Your Family's Star-Lineage";
                desc = "You were born in the Calradian Star-Sector as the Core Hyper-Lanes fractured into warring Star-Syndicates. Before Crimson Void-Pirates raided your orbital habitat, what was your family's calling among the stars?";
            }
            else if (id.Contains("childhood"))
            {
                title = "Zero-G Childhood: Orbital Habitat Aptitude";
                desc = "Growing up aboard a rotating deep-space station and docking ring, you learned to navigate zero-G shafts, sublight drone bays, and astrographic holo-consoles. What defined your early cadet years?";
            }
            else if (id.Contains("education"))
            {
                title = "Adolescence: Sublight Cadet Duty";
                desc = "As xeno bio-swarms and pirate flotillas prowled the outer asteroid belts, every young spacer was assigned to starport operations and defense drills. How did you spend your youth?";
            }
            else if (id.Contains("youth"))
            {
                title = "Starfleet Academy & Syndicate Service";
                desc = "Upon earning your EVA thruster wings and Void-Shield certification, you enlisted in the sector's interstellar forces. Which combat division forged your reputation?";
            }
            else if (id.Contains("adulthood"))
            {
                title = "Turning Point: The Warrant of Trade";
                desc = "Before commissioning your own Voidship Flagship, you accomplished a legendary feat across the sector's hyper-lanes that earned your Warrant of Trade:";
            }
            else if (id.Contains("escape"))
            {
                title = "The Crimson Void-Pirate Raid";
                desc = "When Crimson Syndicate corsairs breached your home orbital station, you fought through the docking bay in zero-G and seized the corvette 'ISS Sovereign Warrant'!";
            }
            else if (id.Contains("age"))
            {
                title = "Commissioning Age & Cybernetic Biometrics";
                desc = "Select your Void-Captain's cryo-chronological age as you step onto the 3D Command Bridge of your Flagship.";
            }
            else
            {
                title = "Spaceboy Astrographic Dossier — Stage " + (index + 1);
                desc = "Configure your Void-Captain's interstellar background, Starfleet training, and Warrant of Trade credentials.";
            }
        }

        private static void GetSciFiOptionText(string menuId, int menuIdx, int optIdx, out string optTitle, out string optDesc)
        {
            string id = (menuId ?? "").ToLowerInvariant();
            string[][] parentOptions = new string[][]
            {
                new string[] { "High Admiral's Retinue (Core Starfleet Officers)", "Your parents served aboard an Aurelian Capital Dreadnought, drilling you in orbital fleet tactics, Void-Shield harmonics, and command protocol." },
                new string[] { "Starport Merchant Syndicate Directors", "Your family managed hyper-spice melange and promethium freight convoys across three Star Systems, mastering interstellar trade and diplomacy." },
                new string[] { "Orbital Ring Shipyard Engineers", "Raised in the zero-G drydocks, your family built C-to-S Class Voidship reactors, hyper-gauss macro-railguns, and sealed carapace armor." },
                new string[] { "Hydroponic Bio-Dome Colonists", "Your family operated an agri-moon colony on the frontier, hardening you in survival, atmospheric terraforming, and plasma rifle marksmanship." },
                new string[] { "Asteroid Belt Promethium Miners", "Born in the high-risk zero-G mining rigs, your family cracked titanium-durasteel asteroids with industrial cutting lasers and heavy exo-suits." },
                new string[] { "Nebula Ranger & Scout Flotilla", "Your parents charted unstable warp anomalies and derelict space hulks on the sector rim, teaching you 6-DOF sublight piloting and stealth." }
            };

            string[][] childhoodOptions = new string[][]
            {
                new string[] { "Cadet Squad Leader & Tactical Prodigy", "You organized the station cadets in zero-G laser tag and boarding simulations, showing natural command authority." },
                new string[] { "Sublight Thruster & Grav-Skiff Racer", "You tuned surplus ion-skimmers and raced through asteroid trenches at breakneck sublight speeds." },
                new string[] { "Enginarium & Holo-Net Tinker", "You spent hours in the reactor core and astrographic sensor bay, recalibrating plasma capacitors and shield emitters." },
                new string[] { "Starport Concourse Trader & Negotiator", "You bartered xeno-curios and fuel cells with docking Rogue Traders and Guild Navigators." },
                new string[] { "Orbital Defense Turret Spotter", "You calibrated point-defense laser grids against micro-meteorites and pirate scout drones." },
                new string[] { "Cryo-Med & Xenobiology Apprentice", "You assisted in the station's vitae med-bays, studying cryo-recovery and xeno-physiology." }
            };

            string[][] educationOptions = new string[][]
            {
                new string[] { "Ran Asteroid Extraction & Hauler Convoys", "You piloted armed cargo skiffs between minor colony moons and the system primary starport." },
                new string[] { "Apprenticed in the Weapons & Armor Foundry", "You forged high-frequency vibro-blades, phase-plasma cells, and vacuum-sealed carapace plates." },
                new string[] { "Served on Starport Customs & Security Patrol", "You patrolled the docking rings in mag-boots, breaking up syndicate smuggling rings." },
                new string[] { "Scouted Deep-Space Nebula Anomalies", "You manned long-range augur arrays, tracking pirate raiders and Prethoryn bio-signatures." },
                new string[] { "Piloted Valkyrie Interceptor Trainers", "You logged hundreds of hours in 3D zero-G flight simulators and orbital strike craft." },
                new string[] { "Studied Astrographic Law & Syndicate Charters", "You mastered the Stellaris system edicts and Rogue Trader Warrants of the Core Worlds." }
            };

            string[][] youthOptions = new string[][]
            {
                new string[] { "Valkyrie Strike-Interceptor Wing Pilot", "You flew high-G orbital dogfights, strafing pirate frigates with twin phase-plasma lances." },
                new string[] { "Navis Void-Marine Boarding Breacher", "Clad in heavy void-carapace and thruster pack, you breached hostile hulls with vibro-sabres and arc-blasters." },
                new string[] { "Capital Ship Gunnery & Torpedo Officer", "You directed spinal lance batteries and WHHHAAAAM photon torpedo broadsides from the command bridge." },
                new string[] { "Long-Range Gauss Railgun Sharpshooter", "You provided zero-G overwatch from orbital bastions with hyper-velocity electromagnetic railguns." },
                new string[] { "Voidship Quartermaster & Fleet Tactician", "You managed reactor power pips, shield harmonics, and fleet logistics across deep-space campaigns." },
                new string[] { "Outrider Scout & Space-Hulk Pathfinder", "You led vanguard teams into derelict archeotech hulks and pirate asteroid lairs." }
            };

            string[][] adulthoodOptions = new string[][]
            {
                new string[] { "Shattered a Crimson Void-Pirate Raider Pack", "In a desperate 3D zero-G firefight, you crippled a pirate destroyer and claimed its Warrant of Trade!" },
                new string[] { "Salvaged an S-Class Archeotech Space Hulk", "You recovered pre-Imperium reactor cores and ancient star-charts from a drifting hulk in the rim nebula." },
                new string[] { "Broke a Starport Blockade at Range Band 1", "Using overcharged Void-Shields and point-blank plasma broadsides, you saved a besieged colony moon." },
                new string[] { "Purged a Xeno Bio-Swarm Infestation", "You led a strike team of Phase-Plasma Marines to cleanse an orbital mining rig of xeno brood-drones." },
                new string[] { "Outmaneuvered a Rival Syndicate Armada", "Through brilliant sublight vector piloting, you lured an enemy task force into an asteroid gravity well." },
                new string[] { "Forged a Lucrative Hyper-Spice Trade Monopoly", "You united three frontier starports under a single interstellar trade charter." }
            };

            string[][] chosenPool = parentOptions;
            if (id.Contains("childhood")) chosenPool = childhoodOptions;
            else if (id.Contains("education")) chosenPool = educationOptions;
            else if (id.Contains("youth")) chosenPool = youthOptions;
            else if (id.Contains("adulthood") || id.Contains("escape")) chosenPool = adulthoodOptions;
            else if (id.Contains("age"))
            {
                int ageVal = 20 + (optIdx * 10);
                optTitle = "Cryo-Service Age: " + ageVal + " Standard Terran Cycles (Spaceboy Prime)";
                optDesc = "Enter active command aboard your Voidship Flagship at " + ageVal + " standard cycles with full neural-link calibration.";
                return;
            }

            string[] pair = chosenPool[optIdx % chosenPool.Length];
            optTitle = pair[0];
            optDesc = pair[1];
        }
    }

    /// <summary>
    /// Handles:
    /// 1. Culture &amp; Feat conversion into Star-Empires before/during Character Creation.
    /// 2. Automatic StoryMode Tutorial skip &amp; Spaceboy Flagship Commissioning immediately after Character Creation.
    /// 3. Real-time 3D Galactic Sector Map visuals (turning the Campaign Map into a Deep-Space Nebula where
    ///    every party is a 3D glowing Voidship with shields &amp; ion thrusters, and every settlement is a
    ///    glowing 3D Star System Primary, Orbital Starbase, or Colony Moon).
    /// 4. Instant 3D Space Battle &amp; 3D Flagship Bridge launch from anywhere.
    /// </summary>
    public class SpaceboyGalacticMapAndCommissioningBehavior : CampaignBehaviorBase
    {
        public static SpaceboyGalacticMapAndCommissioningBehavior Instance { get; private set; }

        private bool _spaceboyCommissioned;
        private bool _commissioningPopupShown;
        private float _postCreationTimer;
        private bool _waitingForCommissioningPopup;

        // 3D Campaign Map Visual Attachment State
        private Scene _configuredMapScene;
        private readonly Dictionary<string, GameEntity> _settlementStarEntities = new Dictionary<string, GameEntity>();
        private readonly Dictionary<int, GameEntity> _partySpaceshipEntities = new Dictionary<int, GameEntity>();
        private float _mapVisualSpinAngle;
        private float _mapVisualRefreshTimer;

        public SpaceboyGalacticMapAndCommissioningBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(this, OnCharacterCreationInitialized);
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            Instance = this;
            dataStore.SyncData("cig_spaceboyCommissioned", ref _spaceboyCommissioned);
            dataStore.SyncData("cig_commissioningPopupShown", ref _commissioningPopupShown);
        }

        private void OnCharacterCreationInitialized(CharacterCreationManager manager)
        {
            Debug.Print("[CalradiaIntergalactic:Spaceboy] OnCharacterCreationInitialized: Registering SpaceboyCharacterCreationHandler.", 0, Debug.DebugColor.Cyan);
            ConvertAllCulturesAndFeatsToSciFi();
            ConvertAllTroopsHeroesAndItemsToSciFi();
            if (manager != null)
            {
                manager.RegisterCharacterCreationContentHandler(new SpaceboyCharacterCreationHandler(), 9999);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Debug.Print("[CalradiaIntergalactic:Spaceboy] OnSessionLaunched: Converting all cultures, feats, troops, heroes, and items to Spaceboy Sci-Fi.", 0, Debug.DebugColor.Cyan);
            ConvertAllCulturesAndFeatsToSciFi();
            ConvertAllTroopsHeroesAndItemsToSciFi();
            PreventMedievalTrainingFieldMission();
        }

        private void OnCharacterCreationIsOver(int state)
        {
            Debug.Print("[CalradiaIntergalactic:Spaceboy] OnCharacterCreationIsOver: Bypassing medieval tutorial & commissioning Spaceboy Captain!", 0, Debug.DebugColor.Cyan);
            // Character creation just finished!
            // Complete the medieval StoryMode tutorial immediately, equip our Spaceboy Captain,
            // stock the Flagship with Void-Marines & 75,000 Star-Credits, and queue the Spaceboy Holo-Briefing!
            CompleteStoryModeTutorialImmediately();
            ConvertAllTroopsHeroesAndItemsToSciFi();
            CommissionSpaceboyCaptainAndFlagship();
            _waitingForCommissioningPopup = true;
            _postCreationTimer = 0f;
        }

        private void OnCampaignTick(float dt)
        {
            // Ensure any new or loaded campaign has completed tutorial bypass & Spaceboy commissioning
            if (!_spaceboyCommissioned && Hero.MainHero != null && MobileParty.MainParty != null)
            {
                CompleteStoryModeTutorialImmediately();
                ConvertAllTroopsHeroesAndItemsToSciFi();
                CommissionSpaceboyCaptainAndFlagship();
                _waitingForCommissioningPopup = true;
            }

            if (_waitingForCommissioningPopup && !_commissioningPopupShown)
            {
                _postCreationTimer += dt;
                if (_postCreationTimer >= 0.35f && MapScreen.Instance != null)
                {
                    _waitingForCommissioningPopup = false;
                    _commissioningPopupShown = true;
                    // Re-verify equipment and position in case any late StoryMode callback ran
                    ApplySpaceboyEquipmentToMainHeroAndPlayerCharacter();
                    OpenSpaceboyCommissioningModal();
                }
            }

            // Global Hotkeys on the 3D Galactic Sector Map:
            // [B] -> Open Spaceboy Command & Voidship Drydock Hub anywhere in space!
            // [F8] -> Launch Immediate 3D Zero-G Space Battle!
            if (MapScreen.Instance != null && Mission.Current == null)
            {
                if (Input.IsKeyPressed(InputKey.B))
                {
                    OpenSpaceboyCommandQuickMenu();
                }
                else if (Input.IsKeyPressed(InputKey.F8))
                {
                    LaunchImmediate3DSpaceBattle();
                }
            }

            // Update 3D Spaceship & Star System Visuals on the Campaign Map
            TickGalacticSectorMapVisuals(dt);
        }

        // =========================================================================
        // 1. CULTURE, FEAT, TROOP, HERO & ITEM TOTAL CONVERSION INTO SCI-FI
        // =========================================================================

        public static void ConvertAllCulturesAndFeatsToSciFi()
        {
            try
            {
                if (MBObjectManager.Instance == null) return;

                FieldInfo basicCultureNameField = typeof(BasicCultureObject).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo cultureEncyclopediaField = typeof(CultureObject).GetField("<EncyclopediaText>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                MBReadOnlyList<CultureObject> cultures = MBObjectManager.Instance.GetObjectTypeList<CultureObject>();
                if (cultures != null)
                {
                    foreach (CultureObject c in cultures)
                    {
                        if (c == null || string.IsNullOrEmpty(c.StringId)) continue;
                        string newName;
                        string newDesc;
                        if (GetSciFiCultureData(c.StringId, out newName, out newDesc))
                        {
                            if (basicCultureNameField != null)
                            {
                                basicCultureNameField.SetValue(c, new TextObject("{=!}" + newName));
                            }
                            if (cultureEncyclopediaField != null)
                            {
                                cultureEncyclopediaField.SetValue(c, new TextObject("{=!}" + newDesc));
                            }
                        }
                    }
                }

                // Convert Cultural Feats into Sci-Fi Fleet & Syndicate Bonuses
                FieldInfo propNameField = typeof(PropertyObject).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo propDescField = typeof(PropertyObject).GetField("_description", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MBReadOnlyList<FeatObject> feats = MBObjectManager.Instance.GetObjectTypeList<FeatObject>();
                if (feats != null && propDescField != null)
                {
                    foreach (FeatObject feat in feats)
                    {
                        if (feat == null || string.IsNullOrEmpty(feat.StringId)) continue;
                        string fName;
                        string fDesc;
                        if (GetSciFiFeatData(feat.StringId, out fName, out fDesc))
                        {
                            if (propNameField != null) propNameField.SetValue(feat, new TextObject("{=!}" + fName));
                            propDescField.SetValue(feat, new TextObject("{=!}" + fDesc));
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static void ConvertAllTroopsHeroesAndItemsToSciFi()
        {
            try
            {
                if (MBObjectManager.Instance == null) return;

                // 1. Convert all Vanilla ItemObjects so every shop, inventory, and loot item has a Sci-Fi name
                FieldInfo itemNameField = typeof(ItemObject).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MBReadOnlyList<ItemObject> allItems = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
                if (allItems != null && itemNameField != null)
                {
                    foreach (ItemObject item in allItems)
                    {
                        if (item == null || string.IsNullOrEmpty(item.StringId) || item.StringId.StartsWith("cig_") || item.Name == null) continue;
                        string curName = item.Name.ToString();
                        if (curName.Contains("[") || curName.Contains("Plasma") || curName.Contains("Vibro") ||
                            curName.Contains("Void-") || curName.Contains("Gauss") || curName.Contains("Sublight") ||
                            curName.Contains("Omnissian") || curName.Contains("Durasteel") || curName.Contains("Servo-"))
                        {
                            continue;
                        }

                        string sciPrefix = null;
                        switch (item.ItemType)
                        {
                            case ItemObject.ItemTypeEnum.OneHandedWeapon:
                            case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                                sciPrefix = "High-Frequency Vibro-Edge: ";
                                break;
                            case ItemObject.ItemTypeEnum.Polearm:
                                sciPrefix = "Omnissian Power-Lance: ";
                                break;
                            case ItemObject.ItemTypeEnum.Bow:
                            case ItemObject.ItemTypeEnum.Crossbow:
                            case ItemObject.ItemTypeEnum.Pistol:
                            case ItemObject.ItemTypeEnum.Musket:
                                sciPrefix = "Phase-Plasma Repeater: ";
                                break;
                            case ItemObject.ItemTypeEnum.Arrows:
                            case ItemObject.ItemTypeEnum.Bolts:
                            case ItemObject.ItemTypeEnum.Bullets:
                                sciPrefix = "Plasma Capacitor Cells: ";
                                break;
                            case ItemObject.ItemTypeEnum.Thrown:
                                sciPrefix = "Arc-Disruptor Charges: ";
                                break;
                            case ItemObject.ItemTypeEnum.Shield:
                                sciPrefix = "Hard-Light Deflector Buckler: ";
                                break;
                            case ItemObject.ItemTypeEnum.HeadArmor:
                                sciPrefix = "Sealed Void-Vacc Helm: ";
                                break;
                            case ItemObject.ItemTypeEnum.BodyArmor:
                                sciPrefix = "Durasteel Void-Carapace: ";
                                break;
                            case ItemObject.ItemTypeEnum.Cape:
                                sciPrefix = "Sublight Thruster-Rig: ";
                                break;
                            case ItemObject.ItemTypeEnum.HandArmor:
                                sciPrefix = "Servo-Actuated Gauntlets: ";
                                break;
                            case ItemObject.ItemTypeEnum.LegArmor:
                                sciPrefix = "Zero-G Mag-Lock Boots: ";
                                break;
                            case ItemObject.ItemTypeEnum.Horse:
                                sciPrefix = "Sublight Strike-Skiff: ";
                                break;
                            case ItemObject.ItemTypeEnum.HorseHarness:
                                sciPrefix = "Ion-ThrusterNacelle Cowling: ";
                                break;
                        }

                        if (sciPrefix != null)
                        {
                            itemNameField.SetValue(item, new TextObject("{=!}" + sciPrefix + curName));
                        }
                    }
                }

                // 2. Build our Spaceboy Sci-Fi Equipment Templates if custom items are loaded
                ItemObject plasmaRifle = MBObjectManager.Instance.GetObject<ItemObject>("cig_wpn_plasma_rifle");
                Equipment captainEq = plasmaRifle != null ? BuildSpaceboyEquipment(true, false) : null;
                Equipment officerEq = plasmaRifle != null ? BuildSpaceboyEquipment(false, false) : null;
                Equipment marineEq = plasmaRifle != null ? BuildVoidMarineTemplateEquipment(false) : null;
                Equipment pirateEq = plasmaRifle != null ? BuildVoidMarineTemplateEquipment(true) : null;

                // 3. Convert all BasicCharacterObjects (Troops, Guards, Bandits, Custom Battle Units) to Sci-Fi Names & Spaceboy Gear
                FieldInfo basicNameField = typeof(BasicCharacterObject).GetField("_basicName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MBReadOnlyList<BasicCharacterObject> allChars = MBObjectManager.Instance.GetObjectTypeList<BasicCharacterObject>();
                if (allChars != null)
                {
                    foreach (BasicCharacterObject bco in allChars)
                    {
                        if (bco == null || string.IsNullOrEmpty(bco.StringId) || bco.StringId.StartsWith("cig_")) continue;
                        if (bco.Race != 0) continue;

                        if (!bco.IsHero && basicNameField != null && bco.Name != null)
                        {
                            string origName = bco.Name.ToString();
                            if (!origName.Contains("Void-") && !origName.Contains("Starfleet") && !origName.Contains("Syndicate") &&
                                !origName.Contains("Plasma") && !origName.Contains("Gauss") && !origName.Contains("Valkyrie") &&
                                !origName.Contains("Sublight") && !origName.Contains("Imperium") && !origName.Contains("Varian") &&
                                !origName.Contains("Solaris") && !origName.Contains("Astral") && !origName.Contains("Horizon"))
                            {
                                string sciTroopName = GetSciFiTroopName(bco, origName);
                                basicNameField.SetValue(bco, new TextObject("{=!}" + sciTroopName));
                            }
                        }

                        // Equip adult human characters with Spaceboy Void-Carapace & Plasma/Vibro Weapons
                        if (plasmaRifle != null && (bco.Age <= 0f || bco.Age >= 18f))
                        {
                            string idLow = bco.StringId.ToLowerInvariant();
                            bool isBandit = idLow.Contains("looter") || idLow.Contains("bandit") || idLow.Contains("raider") || idLow.Contains("corsair");
                            Equipment template = (bco.IsHero || bco.Level >= 26)
                                ? captainEq
                                : (isBandit ? pirateEq : (bco.IsRanged ? officerEq : marineEq));

                            if (template != null)
                            {
                                if (bco.BattleEquipments != null)
                                {
                                    foreach (Equipment eq in bco.BattleEquipments)
                                    {
                                        if (eq == null) continue;
                                        CopySpaceboySlotsIntoEquipment(eq, template, bco.IsMounted);
                                    }
                                }
                                if (bco.CivilianEquipments != null)
                                {
                                    foreach (Equipment eq in bco.CivilianEquipments)
                                    {
                                        if (eq == null) continue;
                                        CopySpaceboySlotsIntoEquipment(eq, template, false);
                                    }
                                }
                            }
                        }
                    }
                }

                // 4. Ensure all alive Campaign Heroes wear Spaceboy Void-Captain / Admiral / Officer Armor
                if (plasmaRifle != null && Hero.AllAliveHeroes != null)
                {
                    foreach (Hero h in Hero.AllAliveHeroes)
                    {
                        if (h == null || h.Age < 18f) continue;
                        Equipment targetEq = (h == Hero.MainHero || h.IsFactionLeader) ? captainEq : officerEq;
                        if (targetEq != null)
                        {
                            if (h.BattleEquipment != null) CopySpaceboySlotsIntoEquipment(h.BattleEquipment, targetEq, true);
                            if (h.CivilianEquipment != null) CopySpaceboySlotsIntoEquipment(h.CivilianEquipment, targetEq, false);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static string GetSciFiTroopName(BasicCharacterObject bco, string origName)
        {
            string idLow = (bco.StringId ?? "").ToLowerInvariant();
            if (idLow.Contains("looter") || idLow.Contains("bandit") || idLow.Contains("raider") || idLow.Contains("corsair"))
            {
                return "Crimson Void-Pirate (" + origName + ")";
            }

            string cultId = (bco.Culture != null && bco.Culture.StringId != null) ? bco.Culture.StringId.ToLowerInvariant() : "";
            string facPrefix = "Starfleet";
            if (cultId == "vlandia") facPrefix = "Syndicate";
            else if (cultId == "empire") facPrefix = "Imperium";
            else if (cultId == "sturgia") facPrefix = "Varian";
            else if (cultId == "aserai") facPrefix = "Solaris";
            else if (cultId == "khuzait") facPrefix = "Astral";
            else if (cultId == "battania") facPrefix = "Horizon";

            string role;
            if (bco.IsMounted && bco.IsRanged) role = "Valkyrie Gunship Pilot";
            else if (bco.IsMounted) role = "Sublight Strike-Lancer";
            else if (bco.IsRanged) role = bco.Level >= 21 ? "Gauss Railgun Sniper" : "Phase-Plasma Marksman";
            else role = bco.Level >= 26 ? "Exo-Terminator Shock-Marine" : (bco.Level >= 16 ? "Navis Void-Marine" : "Void-Cadet Trooper");

            return facPrefix + " " + role;
        }

        private static Equipment BuildVoidMarineTemplateEquipment(bool isPirate)
        {
            Equipment eq = new Equipment(Equipment.EquipmentType.Battle);
            if (MBObjectManager.Instance == null) return eq;

            SetSlot(eq, EquipmentIndex.Head, "cig_armor_void_marine_helm");
            SetSlot(eq, EquipmentIndex.Body, "cig_armor_void_marine_suit");
            SetSlot(eq, EquipmentIndex.Cape, "cig_armor_thruster_pack");
            SetSlot(eq, EquipmentIndex.Gloves, "cig_armor_power_gauntlets");
            SetSlot(eq, EquipmentIndex.Leg, "cig_armor_mag_boots");
            SetSlot(eq, EquipmentIndex.Weapon0, "cig_wpn_vibro_blade");
            SetSlot(eq, EquipmentIndex.Weapon1, isPirate ? "cig_wpn_arc_blaster" : "cig_wpn_void_glaive");
            SetSlot(eq, EquipmentIndex.Weapon2, isPirate ? "cig_wpn_laser_carbine" : "cig_wpn_plasma_rifle");
            SetSlot(eq, EquipmentIndex.Weapon3, "cig_ammo_plasma_cell");
            SetSlot(eq, EquipmentIndex.ArmorItemEndSlot, "war_horse");
            return eq;
        }

        private static void CopySpaceboySlotsIntoEquipment(Equipment dst, Equipment src, bool includeSkiff)
        {
            if (dst == null || src == null) return;
            dst[EquipmentIndex.Head] = src[EquipmentIndex.Head];
            dst[EquipmentIndex.Body] = src[EquipmentIndex.Body];
            dst[EquipmentIndex.Cape] = src[EquipmentIndex.Cape];
            dst[EquipmentIndex.Gloves] = src[EquipmentIndex.Gloves];
            dst[EquipmentIndex.Leg] = src[EquipmentIndex.Leg];
            dst[EquipmentIndex.Weapon0] = src[EquipmentIndex.Weapon0];
            dst[EquipmentIndex.Weapon1] = src[EquipmentIndex.Weapon1];
            dst[EquipmentIndex.Weapon2] = src[EquipmentIndex.Weapon2];
            dst[EquipmentIndex.Weapon3] = src[EquipmentIndex.Weapon3];
            if (includeSkiff && !src[EquipmentIndex.ArmorItemEndSlot].IsEmpty)
            {
                dst[EquipmentIndex.ArmorItemEndSlot] = src[EquipmentIndex.ArmorItemEndSlot];
            }
        }

        private static bool GetSciFiCultureData(string id, out string name, out string desc)
        {
            switch (id.ToLowerInvariant())
            {
                case "vlandia":
                    name = "Valorian Star-Syndicate";
                    desc = "Corporate Dreadnought conglomerates and hyper-gauss weapons combines ruling the Western Rim starports. Masters of heavy capital ship broadsides, mercenary strike-wings, and industrial asteroid mining.";
                    return true;
                case "sturgia":
                    name = "Varian Void-Jarldoms";
                    desc = "Hardened zero-G cryo-world boarders, ice-asteroid miners, and heavy torpedo destroyers of the Northern Nebula. Renowned for fearless Void-Marine drop-pod assaults and unyielding carapace armor.";
                    return true;
                case "empire":
                    name = "Aurelian Star-Imperium";
                    desc = "The ancient Core Starfleet hegemony whose Warrant of Trade, Dyson Swarms, and Orbital Ring Shipyards once united the entire Calradian Sector. Fields disciplined Phase-Plasma Marine legions and capital dreadnoughts.";
                    return true;
                case "aserai":
                    name = "Solaris Trade Consortium";
                    desc = "Master Guild Navigators, Hyper-Spice Melange barons, and ruby lance-laser battlecruisers of the Twin-Sun Sector. Their high-speed trade flotillas dominate interstellar commerce across the southern hyper-lanes.";
                    return true;
                case "khuzait":
                    name = "Astral Steppe Khanate";
                    desc = "High-velocity sublight interceptor swarms and Valkyrie strike-craft wings prowling the Eastern Star-Cluster. Unmatched in 3D zero-G maneuver warfare and rapid hit-and-run plasma strikes.";
                    return true;
                case "battania":
                    name = "Sylvan Horizon Enclaves";
                    desc = "Stealth nebula rangers, derelict space-hulk stalkers, and hyper-accurate gauss railgun sharpshooters inhabiting dense sensor-jamming dust clouds and ancient pre-Imperium orbital habitats.";
                    return true;
                case "nord":
                case "darshi":
                case "vakken":
                    name = "Outer-Rim Void Corsairs";
                    desc = "Independent deep-space privateers and frontier prospectors operating beyond the Core Starfleet beacons.";
                    return true;
                default:
                    name = null;
                    desc = null;
                    return false;
            }
        }

        private static bool GetSciFiFeatData(string id, out string name, out string desc)
        {
            string key = id.ToLowerInvariant();
            if (key.Contains("vlandia"))
            {
                name = "Syndicate Dreadnought Charter";
                desc = "[Star-Syndicate Bonus] +15% Starport production efficiency & accelerated Void-Marine combat XP across system colonies.";
                return true;
            }
            if (key.Contains("sturgia"))
            {
                name = "Cryo-Sector Boarding Doctrine";
                desc = "[Void-Jarldom Bonus] +20% Zero-G infantry recruitment & reduced sublight fleet upkeep across deep-space lanes.";
                return true;
            }
            if (key.Contains("empire"))
            {
                name = "Omnissian Orbital Engineering";
                desc = "[Star-Imperium Bonus] +20% Orbital Megastructure & Starbase construction speed; reduced garrison maintenance.";
                return true;
            }
            if (key.Contains("aserai"))
            {
                name = "Guild Navigator Spice Monopoly";
                desc = "[Solaris Consortium Bonus]Cheaper interstellar trade caravans & +10% sublight thrust in solar radiation zones.";
                return true;
            }
            if (key.Contains("khuzait"))
            {
                name = "Sublight Interceptor Swarm";
                desc = "[Astral Khanate Bonus] +10% campaign sublight speed for Strike-Skiff wings & increased colony production.";
                return true;
            }
            if (key.Contains("battania"))
            {
                name = "Nebula Sensor-Cloak Navigation";
                desc = "[Horizon Enclave Bonus] +50% sublight speed through dense cosmic dust clouds & +1 militia per Starport.";
                return true;
            }
            name = null;
            desc = null;
            return false;
        }

        // =========================================================================
        // 2. STORYMODE TUTORIAL BYPASS & SPACEBOY CAPTAIN / FLAGSHIP COMMISSIONING
        // =========================================================================

        public static void PreventMedievalTrainingFieldMission()
        {
            try
            {
                if (Campaign.Current == null || Campaign.Current.CampaignBehaviorManager == null) return;

                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "StoryMode")
                    {
                        Type tfType = asm.GetType("StoryMode.GameComponents.CampaignBehaviors.TrainingFieldCampaignBehavior");
                        if (tfType != null)
                        {
                            MethodInfo getBeh = typeof(ICampaignBehaviorManager).GetMethod("GetBehavior");
                            if (getBeh != null)
                            {
                                MethodInfo genericGet = getBeh.MakeGenericMethod(tfType);
                                object tfBehavior = genericGet.Invoke(Campaign.Current.CampaignBehaviorManager, null);
                                if (tfBehavior != null)
                                {
                                    FieldInfo skipField = tfType.GetField("SkipTutorialMission", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (skipField != null)
                                    {
                                        skipField.SetValue(tfBehavior, true);
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static void CompleteStoryModeTutorialImmediately()
        {
            try
            {
                PreventMedievalTrainingFieldMission();

                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "StoryMode") continue;

                    Type smMgrType = asm.GetType("StoryMode.StoryModeManager");
                    if (smMgrType == null) break;

                    PropertyInfo curProp = smMgrType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
                    object smCurrent = curProp != null ? curProp.GetValue(null, null) : null;
                    if (smCurrent == null) break;

                    PropertyInfo mainStoryProp = smMgrType.GetProperty("MainStoryLine", BindingFlags.Instance | BindingFlags.Public);
                    object mainStory = mainStoryProp != null ? mainStoryProp.GetValue(smCurrent, null) : null;
                    if (mainStory == null) break;

                    PropertyInfo tutPhaseProp = mainStory.GetType().GetProperty("TutorialPhase", BindingFlags.Instance | BindingFlags.Public);
                    object tutPhase = tutPhaseProp != null ? tutPhaseProp.GetValue(mainStory, null) : null;
                    if (tutPhase != null)
                    {
                        PropertyInfo isCompProp = tutPhase.GetType().GetProperty("IsCompleted", BindingFlags.Instance | BindingFlags.Public);
                        bool isCompleted = isCompProp != null && (bool)isCompProp.GetValue(tutPhase, null);
                        if (!isCompleted)
                        {
                            MethodInfo completeMethod = mainStory.GetType().GetMethod("CompleteTutorialPhase", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (completeMethod != null)
                            {
                                completeMethod.Invoke(mainStory, new object[] { true });
                            }
                        }
                    }
                    break;
                }
            }
            catch (Exception)
            {
            }
        }

        public static void ConvertCharacterCreationEquipmentRosters()
        {
            try
            {
                if (MBObjectManager.Instance == null) return;
                MBReadOnlyList<MBEquipmentRoster> rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                if (rosters == null) return;

                FieldInfo eqListField = typeof(MBEquipmentRoster).GetField("_equipments", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                FieldInfo defEqField = typeof(MBEquipmentRoster).GetField("_defaultEquipment", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (eqListField == null) return;

                Equipment capBattle = BuildSpaceboyEquipment(true, false);
                Equipment capCiv = BuildSpaceboyEquipment(true, true);
                Equipment offBattle = BuildSpaceboyEquipment(false, false);
                Equipment offCiv = BuildSpaceboyEquipment(false, true);

                foreach (MBEquipmentRoster r in rosters)
                {
                    if (r == null || string.IsNullOrEmpty(r.StringId)) continue;
                    string id = r.StringId.ToLowerInvariant();
                    if (id.Contains("char_creation") || id.Contains("parent") || id.Contains("childhood") ||
                        id.Contains("education") || id.Contains("youth") || id.Contains("adulthood") ||
                        id.Contains("mother") || id.Contains("father") || id.Contains("brother") || id.Contains("escape"))
                    {
                        bool isPlayer = id.Contains("player") || id.Contains("childhood") || id.Contains("youth") || id.Contains("adulthood");
                        MBList<Equipment> list = new MBList<Equipment>();
                        list.Add((isPlayer ? capBattle : offBattle).Clone(false));
                        list.Add((isPlayer ? capCiv : offCiv).Clone(false));
                        eqListField.SetValue(r, list);
                        if (defEqField != null)
                        {
                            defEqField.SetValue(r, list[0]);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static MBEquipmentRoster CreateSpaceboyEquipmentRoster(bool isCaptain)
        {
            try
            {
                if (MBObjectManager.Instance == null) return null;

                Equipment battleEq = BuildSpaceboyEquipment(isCaptain, false);
                Equipment civEq = BuildSpaceboyEquipment(isCaptain, true);

                MBEquipmentRoster roster = new MBEquipmentRoster();
                FieldInfo eqListField = typeof(MBEquipmentRoster).GetField("_equipments", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (eqListField != null)
                {
                    MBList<Equipment> list = new MBList<Equipment>();
                    list.Add(battleEq);
                    list.Add(civEq);
                    eqListField.SetValue(roster, list);
                }
                return roster;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Equipment BuildSpaceboyEquipment(bool isCaptain, bool isCivilian)
        {
            Equipment eq = new Equipment(isCivilian ? Equipment.EquipmentType.Civilian : Equipment.EquipmentType.Battle);
            if (MBObjectManager.Instance == null) return eq;

            string helmId = isCaptain ? "cig_armor_rogue_captain_helm" : "cig_armor_exo_knight_helm";
            string suitId = isCaptain ? "cig_armor_rogue_captain_suit" : "cig_armor_exo_knight_suit";
            string capeId = "cig_armor_thruster_pack";
            string gloveId = "cig_armor_power_gauntlets";
            string bootId = "cig_armor_mag_boots";
            string w0Id = "cig_wpn_vibro_blade";
            string w1Id = isCaptain ? "cig_wpn_disruptor_lance" : "cig_wpn_gauss_railgun";
            string w2Id = "cig_wpn_plasma_rifle";
            string w3Id = "cig_ammo_plasma_cell";

            SetSlot(eq, EquipmentIndex.Head, helmId);
            SetSlot(eq, EquipmentIndex.Body, suitId);
            SetSlot(eq, EquipmentIndex.Cape, capeId);
            SetSlot(eq, EquipmentIndex.Gloves, gloveId);
            SetSlot(eq, EquipmentIndex.Leg, bootId);
            SetSlot(eq, EquipmentIndex.Weapon0, w0Id);
            SetSlot(eq, EquipmentIndex.Weapon1, w1Id);
            SetSlot(eq, EquipmentIndex.Weapon2, w2Id);
            SetSlot(eq, EquipmentIndex.Weapon3, w3Id);

            // Sublight Strike-Skiff in horse slot so campaign speed is fast
            SetSlot(eq, EquipmentIndex.ArmorItemEndSlot, "war_horse");
            return eq;
        }

        private static void SetSlot(Equipment eq, EquipmentIndex slot, string itemId)
        {
            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item != null)
            {
                eq[slot] = new EquipmentElement(item, null, null, false);
            }
        }

        public static void ApplySpaceboyEquipmentToMainHeroAndPlayerCharacter()
        {
            try
            {
                if (MBObjectManager.Instance == null) return;

                Equipment captainBattle = BuildSpaceboyEquipment(true, false);
                Equipment captainCiv = BuildSpaceboyEquipment(true, true);

                if (Hero.MainHero != null)
                {
                    if (Hero.MainHero.BattleEquipment != null)
                    {
                        Hero.MainHero.BattleEquipment.FillFrom(captainBattle, false);
                    }
                    if (Hero.MainHero.CivilianEquipment != null)
                    {
                        Hero.MainHero.CivilianEquipment.FillFrom(captainCiv, false);
                    }
                }

                if (CharacterObject.PlayerCharacter != null)
                {
                    if (CharacterObject.PlayerCharacter.Equipment != null)
                    {
                        CharacterObject.PlayerCharacter.Equipment.FillFrom(captainBattle, false);
                    }
                    if (CharacterObject.PlayerCharacter.FirstCivilianEquipment != null)
                    {
                        CharacterObject.PlayerCharacter.FirstCivilianEquipment.FillFrom(captainCiv, false);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void CommissionSpaceboyCaptainAndFlagship()
        {
            try
            {
                if (Hero.MainHero == null || MobileParty.MainParty == null || MBObjectManager.Instance == null)
                {
                    return;
                }

                _spaceboyCommissioned = true;

                // 1. Equip MainHero & PlayerCharacter in full Spaceboy Void-Captain Armor & Plasma/Vibro Arsenal
                ApplySpaceboyEquipmentToMainHeroAndPlayerCharacter();

                // 2. Title the player as Spaceboy Void-Captain & Clan as Star-Dynasty if not yet titled
                string firstName = Hero.MainHero.FirstName != null ? Hero.MainHero.FirstName.ToString() : "Nova";
                if (!firstName.Contains("Captain") && (Hero.MainHero.Name == null || !Hero.MainHero.Name.ToString().Contains("Spaceboy")))
                {
                    Hero.MainHero.SetName(new TextObject("{=!}Spaceboy Captain " + firstName), new TextObject("{=!}" + firstName));
                }

                if (Clan.PlayerClan != null && Clan.PlayerClan.Name != null)
                {
                    string cName = Clan.PlayerClan.Name.ToString();
                    if (!cName.Contains("Star-") && !cName.Contains("Void-") && !cName.Contains("Syndicate"))
                    {
                        TextObject newClanName = new TextObject("{=!}Star-Dynasty " + cName);
                        Clan.PlayerClan.ChangeClanName(newClanName, newClanName);
                    }
                }

                // 3. Grant 75,000 Star-Credits Warrant of Trade Treasury
                if (Hero.MainHero.Gold < 75000)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 75000 - Hero.MainHero.Gold, true);
                }

                // 4. Populate Flagship Crew with Navis Void-Marines, Gauss Snipers, Breachers & Valkyrie Strike-Pilots
                if (MobileParty.MainParty.MemberRoster != null && MobileParty.MainParty.MemberRoster.TotalManCount < 20)
                {
                    AddCrew("cig_void_t2_breacher", 8);
                    AddCrew("cig_void_t3_plasma_gunner", 10);
                    AddCrew("cig_void_t4_gauss_sniper", 6);
                    AddCrew("cig_void_t5_exo_terminator", 4);
                    AddCrew("cig_void_t4_skiff_pilot", 4);
                    AddCrew("cig_void_t6_valkyrie_ace", 2);
                }

                // 5. Stock Flagship Cargo Hold with Hydroponic Rations & Full Sci-Fi Armory
                if (MobileParty.MainParty.ItemRoster != null)
                {
                    ItemObject rations = MBObjectManager.Instance.GetObject<ItemObject>("grain");
                    if (rations != null && MobileParty.MainParty.ItemRoster.GetItemNumber(rations) < 40)
                    {
                        MobileParty.MainParty.ItemRoster.AddToCounts(rations, 50);
                    }

                    string[] armoryIds = new string[]
                    {
                        "cig_wpn_plasma_rifle", "cig_wpn_laser_carbine", "cig_wpn_gauss_railgun",
                        "cig_wpn_arc_blaster", "cig_wpn_torpedo_launcher", "cig_wpn_disruptor_lance",
                        "cig_ammo_plasma_cell", "cig_ammo_rail_slug", "cig_ammo_photon_micro",
                        "cig_wpn_vibro_blade", "cig_wpn_void_glaive",
                        "cig_armor_rogue_captain_helm", "cig_armor_rogue_captain_suit",
                        "cig_armor_exo_knight_helm", "cig_armor_exo_knight_suit",
                        "cig_armor_void_marine_helm", "cig_armor_void_marine_suit",
                        "cig_armor_thruster_pack", "cig_armor_power_gauntlets", "cig_armor_mag_boots",
                        "cig_good_promethium", "cig_good_spice_melange", "cig_good_quantum_cores", "cig_good_xeno_relics"
                    };
                    foreach (string id in armoryIds)
                    {
                        ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id);
                        if (item != null && MobileParty.MainParty.ItemRoster.GetItemNumber(item) < 2)
                        {
                            MobileParty.MainParty.ItemRoster.AddToCounts(item, 2);
                        }
                    }
                }

                // 6. Position the Player's Flagship in High Orbit of a Major Capital Starport
                Settlement starport = Settlement.Find("town_V3") ?? Settlement.Find("town_EN1") ?? Settlement.Find("town_S1");
                if (starport != null)
                {
                    if (Settlement.CurrentSettlement != null)
                    {
                        LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
                    }
                    if (PlayerEncounter.Current != null)
                    {
                        PlayerEncounter.Finish(true);
                    }
                    MobileParty.MainParty.Position = starport.GatePosition;
                    MobileParty.MainParty.SetMoveModeHold();

                    MapState mapState = GameStateManager.Current != null ? GameStateManager.Current.ActiveState as MapState : null;
                    if (mapState != null && mapState.Handler != null)
                    {
                        mapState.Handler.TeleportCameraToMainParty();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void AddCrew(string troopId, int count)
        {
            CharacterObject troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
            if (troop != null && MobileParty.MainParty != null && MobileParty.MainParty.MemberRoster != null)
            {
                MobileParty.MainParty.MemberRoster.AddToCounts(troop, count);
            }
        }

        // =========================================================================
        // 3. SPACEBOY HOLO-BRIEFING & INSTANT 3D SPACE BATTLE / BRIDGE LAUNCHERS
        // =========================================================================

        public void OpenSpaceboyCommissioningModal()
        {
            string shipName = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.FlagshipName : "ISS Sovereign Warrant";
            string hullTitle = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.GetCurrentHullTitle() : "Class-II Valorian Syndicate Corvette";

            string desc =
                "=== WARRANT OF TRADE CONFIRMED: WELCOME ABOARD, SPACEBOY! ===\n\n" +
                "• Commanding Officer: " + (Hero.MainHero != null ? Hero.MainHero.Name.ToString() : "Spaceboy Captain") + "\n" +
                "• Flagship Registry: " + shipName + " (" + hullTitle + ")\n" +
                "• Personal Loadout: Rogue Trader Void-Captain Carapace, Zero-G Thruster Pack, Sunfire Phase-Plasma Rifle, Disruptor Lance & Vibro-Sabre\n" +
                "• Flagship Crew: 34 Navis Void-Marines, Gauss Snipers, Exo-Terminators & Valkyrie Strike-Pilots\n" +
                "• Syndicate Treasury: 75,000 Star-Credits\n\n" +
                "--- GALACTIC NAVIGATION & 3D SPACE COMBAT HOTKEYS ---\n" +
                "• On Galactic Sector Map: Press [B] anytime for Ship Designer & Command Hub | Press [F8] for Instant 3D Space Battle!\n" +
                "• In 3D Space Battles: [Space + W/A/S/D] 3D Zero-G Flight | [C] Descend | [F] Fire Plasma Lance (PEW-PEW!) | [Q] Photon Torpedo (WHHHAAAAM!) | [V] Void-Shield Surge | [G] Toggle Mag-Boots\n\n" +
                "Select your first Bridge Order, Captain:";

            List<InquiryElement> options = new List<InquiryElement>();
            options.Add(new InquiryElement(
                "space_battle",
                "[1] ENGAGE IMMEDIATE 3D ZERO-G SPACE BATTLE (PEW-PEW Plasma & WHHHAAAAM Torpedoes!)",
                new EmptyImageIdentifier(),
                true,
                "Intercept a hostile Crimson Void-Pirate Raider Flotilla right now in 3D zero-G orbital space with overhead Capital Voidships!"));

            options.Add(new InquiryElement(
                "walk_bridge",
                "[2] BEAM ONTO 3D WALKABLE FLAGSHIP COMMAND BRIDGE (Holographic Starmap & Warp Core)",
                new EmptyImageIdentifier(),
                true,
                "Walk around the 3D interior of your Flagship's Command Deck with rotating 3D Holographic Starmap, Orbiting Planets, and Warp Singularity Core."));

            options.Add(new InquiryElement(
                "ship_designer",
                "[3] OPEN STARPORT VOIDSHIP ARCHITECT & MODULAR SHIP DESIGNER",
                new EmptyImageIdentifier(),
                true,
                "Customize your Flagship's Hull Class, Spinal Weapons, C/B/A/S-Class Archeotech Modules, and Reactor Power Pips."));

            options.Add(new InquiryElement(
                "explore_galaxy",
                "[4] PILOT FLAGSHIP ACROSS THE 3D GALACTIC SECTOR MAP",
                new EmptyImageIdentifier(),
                true,
                "Fly your 3D glowing Voidship between Star System Capitals, Orbital Starbases, and Minor Colony Moons."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "CALRADIA: INTERGALACTIC — SPACEBOY FLAGSHIP COMMISSIONED",
                    desc,
                    options,
                    true,
                    1,
                    1,
                    "Execute Order",
                    "To Galactic Map",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        string cmd = selected[0].Identifier as string;
                        if (cmd == "space_battle")
                        {
                            LaunchImmediate3DSpaceBattle();
                        }
                        else if (cmd == "walk_bridge")
                        {
                            if (VoidshipArchitectBehavior.Instance != null)
                            {
                                VoidshipArchitectBehavior.Instance.LaunchWalkableFlagshipDeckMission();
                            }
                        }
                        else if (cmd == "ship_designer")
                        {
                            if (VoidshipArchitectBehavior.Instance != null)
                            {
                                VoidshipArchitectBehavior.Instance.OpenVoidshipDrydockHub();
                            }
                        }
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        public void OpenSpaceboyCommandQuickMenu()
        {
            List<InquiryElement> options = new List<InquiryElement>();
            options.Add(new InquiryElement("space_battle", "Intercept Hostile Raider Fleet in 3D Zero-G Space Battle! [Hotkey: F8]", new EmptyImageIdentifier(), true, "Immediately launch a 3D directional space battle with Pew-Pew lasers, WHHHAAAAM torpedoes, and overhead Capital Voidships."));
            options.Add(new InquiryElement("walk_bridge", "Step Onto 3D Walkable Flagship Command Bridge", new EmptyImageIdentifier(), true, "Enter the 3D interior of your Flagship with rotating Holographic Starmap and Warp Core."));
            options.Add(new InquiryElement("drydock", "Open Voidship Architect, Loadouts & Reactor Power Pips", new EmptyImageIdentifier(), true, "Upgrade hull class, swap spinal weapons, and route reactor power pips."));
            options.Add(new InquiryElement("astrography", "Open Star-System Telemetry, Edicts & Galactic Crisis Scanner", new EmptyImageIdentifier(), true, "Enact Stellaris system edicts, build Orbital Megastructures, or resolve Galactic Crises."));
            options.Add(new InquiryElement("rearm", "Re-Equip Spaceboy Void-Captain Loadout & Resupply Plasma Cells", new EmptyImageIdentifier(), true, "Ensures your hero and crew have full Void-Power Armor, Plasma Rifles, and Torpedo cells."));

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "SPACEBOY FLAGSHIP COMMAND CONSOLE [B]",
                    "Flagship: " + (VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.FlagshipName : "ISS Sovereign Warrant") + " | Select an interstellar operation:",
                    options,
                    true,
                    1,
                    1,
                    "Confirm",
                    "Close",
                    delegate(List<InquiryElement> selected)
                    {
                        if (selected == null || selected.Count == 0) return;
                        string cmd = selected[0].Identifier as string;
                        if (cmd == "space_battle")
                        {
                            LaunchImmediate3DSpaceBattle();
                        }
                        else if (cmd == "walk_bridge" && VoidshipArchitectBehavior.Instance != null)
                        {
                            VoidshipArchitectBehavior.Instance.LaunchWalkableFlagshipDeckMission();
                        }
                        else if (cmd == "drydock" && VoidshipArchitectBehavior.Instance != null)
                        {
                            VoidshipArchitectBehavior.Instance.OpenVoidshipDrydockHub();
                        }
                        else if (cmd == "astrography")
                        {
                            Settlement target = Settlement.CurrentSettlement ?? Settlement.Find("town_V3");
                            GalacticSectorConversionBehavior gsc = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<GalacticSectorConversionBehavior>() : null;
                            if (gsc != null)
                            {
                                gsc.OpenStarSystemCommandDialog(target);
                            }
                        }
                        else if (cmd == "rearm")
                        {
                            CommissionSpaceboyCaptainAndFlagship();
                            InformationManager.DisplayMessage(new InformationMessage(
                                "[Flagship Armory] Spaceboy Void-Captain Carapace, Plasma Rifles, Photon Torpedoes & Crew replenished!",
                                Color.FromUint(0xFF00FFAA)));
                        }
                    },
                    null,
                    "",
                    false),
                true,
                false);
        }

        public static void LaunchImmediate3DSpaceBattle()
        {
            try
            {
                if (MobileParty.MainParty == null || PartyBase.MainParty == null) return;

                ApplySpaceboyEquipmentToMainHeroAndPlayerCharacter();

                if (Settlement.CurrentSettlement != null)
                {
                    LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
                }
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Finish(true);
                }

                // Find an active Bandit/Pirate MobileParty to serve as the Crimson Void-Pirate Raider Flotilla
                MobileParty targetRaider = null;
                if (MobileParty.AllBanditParties != null)
                {
                    foreach (MobileParty bp in MobileParty.AllBanditParties)
                    {
                        if (bp != null && bp.IsActive && bp.MemberRoster != null && bp.MemberRoster.TotalHealthyCount > 0 && bp.MapEvent == null && bp.CurrentSettlement == null)
                        {
                            targetRaider = bp;
                            break;
                        }
                    }
                }

                if (targetRaider != null)
                {
                    // Augment the pirate flotilla with Crimson Void-Pirates & Xeno Brood-Drones
                    if (MBObjectManager.Instance != null)
                    {
                        CharacterObject pirate = MBObjectManager.Instance.GetObject<CharacterObject>("cig_pirate_raider");
                        CharacterObject xeno = MBObjectManager.Instance.GetObject<CharacterObject>("cig_xeno_brood_drone");
                        if (pirate != null && targetRaider.MemberRoster.TotalHealthyCount < 14)
                        {
                            targetRaider.MemberRoster.AddToCounts(pirate, 8);
                        }
                        if (xeno != null && targetRaider.MemberRoster.TotalHealthyCount < 20)
                        {
                            targetRaider.MemberRoster.AddToCounts(xeno, 4);
                        }
                    }

                    targetRaider.Position = MobileParty.MainParty.Position;
                    PlayerEncounter.RestartPlayerEncounter(targetRaider.Party, PartyBase.MainParty, true, false);
                    if (PlayerEncounter.Battle == null)
                    {
                        PlayerEncounter.StartBattle();
                    }
                    Helpers.MenuHelper.EncounterAttackConsequence(null);
                    return;
                }

                // Fallback if no bandit party is free: open a 3D Orbital Space Skirmish scene directly
                CampaignMission.OpenBattleMission("battle_terrain_a", false);
            }
            catch (Exception)
            {
                try
                {
                    CampaignMission.OpenBattleMission("battle_terrain_a", false);
                }
                catch
                {
                }
            }
        }

        // =========================================================================
        // 4. 3D GALACTIC SECTOR MAP VISUALS (3D SPACESHIPS & 3D STAR SYSTEMS)
        // =========================================================================

        private void TickGalacticSectorMapVisuals(float dt)
        {
            try
            {
                if (MapScreen.Instance == null) return;
                Scene mapScene = MapScreen.Instance.MapScene;
                if (mapScene == null) return;

                if (_configuredMapScene != mapScene)
                {
                    _configuredMapScene = mapScene;
                    _settlementStarEntities.Clear();
                    _partySpaceshipEntities.Clear();

                    // Set Campaign Map to Deep-Space Cosmic Nebula Night/Starlight!
                    try
                    {
                        mapScene.SetPhotoAtmosphereViaTod(0.015f, false);
                    }
                    catch
                    {
                    }
                }

                _mapVisualSpinAngle += dt * 1.15f;
                _mapVisualRefreshTimer += dt;

                // Always keep Player's 3D Flagship Spaceship Hull & Shield updated on the Campaign Map
                if (MobileParty.MainParty != null && MobilePartyVisualManager.Current != null)
                {
                    AttachOrUpdatePartySpaceshipEntity(mapScene, MobileParty.MainParty, true);
                }

                // Periodically attach 3D Glowing Star / Planet / Orbital Moon Spheres to Settlements
                // and 3D Voidship Hulls to nearby AI Fleets
                if (_mapVisualRefreshTimer >= 1.5f)
                {
                    _mapVisualRefreshTimer = 0f;

                    if (SettlementVisualManager.Current != null && Settlement.All != null)
                    {
                        foreach (Settlement s in Settlement.All)
                        {
                            if (s == null || (!s.IsTown && !s.IsCastle && !s.IsVillage)) continue;
                            if (_settlementStarEntities.ContainsKey(s.StringId)) continue;

                            SettlementVisual sv = SettlementVisualManager.Current.GetSettlementVisual(s);
                            if (sv != null && sv.StrategicEntity != null)
                            {
                                GameEntity starOrb = CreateStarSystemOrbOnSettlement(mapScene, sv.StrategicEntity, s);
                                if (starOrb != null)
                                {
                                    _settlementStarEntities[s.StringId] = starOrb;
                                }
                            }
                        }
                    }

                    if (MobilePartyVisualManager.Current != null && MobileParty.All != null && MobileParty.MainParty != null)
                    {
                        CampaignVec2 playerPos = MobileParty.MainParty.Position;
                        int attachedCount = 0;
                        foreach (MobileParty mp in MobileParty.All)
                        {
                            if (mp == null || mp == MobileParty.MainParty || !mp.IsActive || mp.CurrentSettlement != null) continue;
                            if (attachedCount > 45) break;
                            if (mp.Position.DistanceSquared(playerPos) < 900f)
                            {
                                AttachOrUpdatePartySpaceshipEntity(mapScene, mp, false);
                                attachedCount++;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private GameEntity CreateStarSystemOrbOnSettlement(Scene mapScene, GameEntity settlementEntity, Settlement s)
        {
            try
            {
                GameEntity orb = GameEntity.CreateEmptyDynamic(mapScene, true);
                if (orb == null) return null;

                MetaMesh sphereMesh = MetaMesh.GetCopy("barrier_sphere", false, true);
                if (sphereMesh != null)
                {
                    orb.AddMultiMesh(sphereMesh, true);
                }

                float scaleVal = s.IsTown ? 3.8f : (s.IsCastle ? 2.4f : 1.35f);
                float zHeight = s.IsTown ? 2.8f : (s.IsCastle ? 2.1f : 1.3f);
                float lightRad = s.IsTown ? 16.0f : (s.IsCastle ? 10.0f : 6.0f);

                Vec3 color = s.IsTown
                    ? new Vec3(0.05f, 0.92f, 1.0f, -1f)   // Cyan Star System Primary
                    : (s.IsCastle
                        ? new Vec3(0.75f, 0.25f, 1.0f, -1f) // Violet Orbital Starbase
                        : new Vec3(0.15f, 1.0f, 0.65f, -1f)); // Emerald Colony Moon

                Light ptLight = Light.CreatePointLight(lightRad);
                if (ptLight != null)
                {
                    ptLight.LightColor = color;
                    orb.AddLight(ptLight);
                }

                settlementEntity.AddChild(orb, false);
                MatrixFrame localFrame = MatrixFrame.Identity;
                localFrame.origin = new Vec3(0f, 0f, zHeight, -1f);
                localFrame.rotation.ApplyScaleLocal(scaleVal);
                orb.SetLocalFrame(ref localFrame, false);
                return orb;
            }
            catch
            {
                return null;
            }
        }

        private void AttachOrUpdatePartySpaceshipEntity(Scene mapScene, MobileParty party, bool isPlayerFlagship)
        {
            try
            {
                if (party == null || party.Party == null || MobilePartyVisualManager.Current == null) return;
                MobilePartyVisual pv = MobilePartyVisualManager.Current.GetPartyVisual(party.Party);
                if (pv == null || pv.StrategicEntity == null) return;

                int key = party.Party.Index;
                GameEntity shipEntity;
                if (!_partySpaceshipEntities.TryGetValue(key, out shipEntity) || shipEntity == null || shipEntity.Parent != pv.StrategicEntity)
                {
                    shipEntity = GameEntity.CreateEmptyDynamic(mapScene, true);
                    if (shipEntity == null) return;

                    // 1. 3D Voidship Hull Mesh
                    MetaMesh hullMesh = MetaMesh.GetCopy("boulder2", false, true) ?? MetaMesh.GetCopy("barrier_sphere", false, true);
                    if (hullMesh != null)
                    {
                        shipEntity.AddMultiMesh(hullMesh, true);
                    }

                    // 2. Glowing Void-Shield Envelope
                    MetaMesh shieldMesh = MetaMesh.GetCopy("barrier_sphere", false, true);
                    if (shieldMesh != null)
                    {
                        shipEntity.AddMultiMesh(shieldMesh, true);
                    }

                    // 3. Sublight Ion Thruster Glow & Particle Plume
                    Vec3 lightColor = isPlayerFlagship
                        ? new Vec3(0.0f, 0.95f, 1.0f, -1f)
                        : (party.IsBandit ? new Vec3(1.0f, 0.2f, 0.1f, -1f) : new Vec3(1.0f, 0.78f, 0.2f, -1f));
                    Light thrusterLight = Light.CreatePointLight(isPlayerFlagship ? 12.0f : 6.5f);
                    if (thrusterLight != null)
                    {
                        thrusterLight.LightColor = lightColor;
                        shipEntity.AddLight(thrusterLight);
                    }

                    if (isPlayerFlagship)
                    {
                        shipEntity.AddParticleSystemComponent("psys_game_burning_agent");
                    }

                    pv.StrategicEntity.AddChild(shipEntity, false);
                    _partySpaceshipEntities[key] = shipEntity;
                }

                // Hover and gently bob the 3D Voidship above the strategic entity
                MatrixFrame local = MatrixFrame.Identity;
                float bob = (float)Math.Sin(_mapVisualSpinAngle * 2.0f + key) * 0.12f;
                local.origin = new Vec3(0f, 0f, (isPlayerFlagship ? 1.15f : 0.85f) + bob, -1f);
                Vec3 shipScale = isPlayerFlagship
                    ? new Vec3(1.15f, 2.45f, 0.75f, -1f)
                    : new Vec3(0.75f, 1.55f, 0.55f, -1f);
                local.rotation.ApplyScaleLocal(ref shipScale);
                shipEntity.SetLocalFrame(ref local, false);
            }
            catch
            {
            }
        }
    }
}
