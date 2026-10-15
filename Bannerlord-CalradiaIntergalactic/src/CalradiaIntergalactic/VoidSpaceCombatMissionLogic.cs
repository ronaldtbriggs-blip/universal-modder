using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace CalradiaIntergalactic
{
    /// <summary>
    /// 3D Directional Space Combat, Pew-Pew Energy Weapons, WHHHAAAAM Torpedo Shockwaves,
    /// Overhead 3D Capital Voidships, Recharging Void-Shields, and Walkable 3D Spaceship Command Deck.
    /// </summary>
    public class VoidSpaceCombatMissionLogic : MissionLogic
    {
        private class ActiveEnergyBolt
        {
            public GameEntity BoltEntity;
            public Vec3 Position;
            public Vec3 Velocity;
            public Vec3 Color;
            public float RemainingLife;
            public float BaseDamage;
            public bool IsWhamTorpedo;
            public Agent Shooter;
        }

        private class ActiveWhamShockwave
        {
            public GameEntity SphereEntity;
            public Vec3 Origin;
            public Vec3 Color;
            public float Elapsed;
            public float Duration;
            public float MaxRadius;
        }

        private class OverheadVoidshipEntity
        {
            public GameEntity HullRoot;
            public GameEntity ShieldSphere;
            public GameEntity ThrusterGlow;
            public Vec3 BasePosition;
            public Vec3 ForwardDir;
            public bool IsPlayerFleet;
            public float BroadsideCooldown;
            public float OrbitPhase;
        }

        private readonly List<ActiveEnergyBolt> _activeBolts = new List<ActiveEnergyBolt>();
        private readonly List<ActiveWhamShockwave> _activeShockwaves = new List<ActiveWhamShockwave>();
        private readonly List<OverheadVoidshipEntity> _overheadShips = new List<OverheadVoidshipEntity>();
        private readonly Dictionary<int, float> _agentVoidShields = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _agentMaxVoidShields = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _agentTargetHoverAltitude = new Dictionary<int, float>();

        // Walkable 3D Flagship Bridge Entities
        private bool _isWalkableBridgeMode;
        private bool _isSpaceBattleMode;
        private bool _sceneConfigured;
        private GameEntity _bridgeHoloStarmapCore;
        private readonly List<GameEntity> _bridgeHoloOrbitPlanets = new List<GameEntity>();
        private GameEntity _bridgeWarpReactorCore;
        private float _bridgeHoloAngle;

        // 3D Zero-G & Flight Thruster State
        private bool _zeroGThrustersEnabled = true;
        private Vec3 _playerZeroGVelocity = Vec3.Zero;
        private float _playerBaseGroundZ;
        private bool _playerBaseGroundInitialized;
        private float _whamTorpedoCooldown;
        private float _pewBlasterCooldown;
        private float _shieldOverchargeCooldown;
        private float _aiHoverUpdateTimer;
        private float _hudStatusTimer;

        public override void AfterStart()
        {
            base.AfterStart();
            _activeBolts.Clear();
            _activeShockwaves.Clear();
            _overheadShips.Clear();
            _agentVoidShields.Clear();
            _agentMaxVoidShields.Clear();
            _agentTargetHoverAltitude.Clear();
            _bridgeHoloOrbitPlanets.Clear();
            _sceneConfigured = false;
            _playerBaseGroundInitialized = false;
            _playerZeroGVelocity = Vec3.Zero;
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (agent == null || !agent.IsHuman)
            {
                return;
            }

            // Assign Recharging Personal Void-Shields to every combatant
            float shieldCap = agent.IsMainAgent ? 180f : (agent.IsHero ? 120f : 65f);
            if (agent.IsMainAgent && VoidshipArchitectBehavior.Instance != null)
            {
                shieldCap += VoidshipArchitectBehavior.Instance.ShieldPowerPips * 35f;
            }
            _agentMaxVoidShields[agent.Index] = shieldCap;
            _agentVoidShields[agent.Index] = shieldCap;

            // Equip combatants in field/space battles with sci-fi blasters & Void-Carapaces if they lack them
            if (!IsIndoorOrBridgeScene())
            {
                EquipAgentWithSciFiLoadout(agent);
            }
        }

        private bool IsIndoorOrBridgeScene()
        {
            if (VoidshipArchitectBehavior.Instance != null && VoidshipArchitectBehavior.Instance.IsFlagshipBridgeWalkActive)
            {
                return true;
            }
            if (Mission.Current == null)
            {
                return false;
            }
            if (Mission.Current.IsFieldBattle || Mission.Current.IsSiegeBattle || Mission.Current.IsSallyOutBattle)
            {
                return false;
            }
            string sceneName = Mission.Current.SceneName != null ? Mission.Current.SceneName.ToLowerInvariant() : string.Empty;
            if (sceneName.Contains("tavern") || sceneName.Contains("lordshall") || sceneName.Contains("keep") || sceneName.Contains("interior") || sceneName.Contains("prison"))
            {
                return true;
            }
            return false;
        }

        private void EquipAgentWithSciFiLoadout(Agent agent)
        {
            try
            {
                if (MBObjectManager.Instance == null || agent.SpawnEquipment == null)
                {
                    return;
                }

                // Give a sci-fi blaster / railgun + energy cells to ranged or hero units, or 55% of line infantry
                bool shouldHaveBlaster = agent.IsMainAgent || agent.IsHero || (agent.Index % 2 == 0);
                if (shouldHaveBlaster)
                {
                    string gunId = "cig_wpn_plasma_rifle";
                    string ammoId = "cig_ammo_plasma_cell";
                    if (agent.IsMainAgent)
                    {
                        gunId = "cig_wpn_disruptor_lance";
                        ammoId = "cig_ammo_plasma_cell";
                    }
                    else if (agent.Index % 6 == 0)
                    {
                        gunId = "cig_wpn_torpedo_launcher";
                        ammoId = "cig_ammo_photon_micro";
                    }
                    else if (agent.Index % 4 == 0)
                    {
                        gunId = "cig_wpn_gauss_railgun";
                        ammoId = "cig_ammo_rail_slug";
                    }
                    else if (agent.Index % 3 == 0)
                    {
                        gunId = "cig_wpn_arc_blaster";
                        ammoId = "cig_ammo_plasma_cell";
                    }

                    ItemObject gunItem = MBObjectManager.Instance.GetObject<ItemObject>(gunId);
                    ItemObject ammoItem = MBObjectManager.Instance.GetObject<ItemObject>(ammoId);
                    if (gunItem != null && ammoItem != null)
                    {
                        MissionWeapon gunWeapon = new MissionWeapon(gunItem, null, agent.Origin != null ? agent.Origin.Banner : null);
                        MissionWeapon ammoWeapon = new MissionWeapon(ammoItem, null, agent.Origin != null ? agent.Origin.Banner : null);
                        agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon2, ref gunWeapon);
                        agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon3, ref ammoWeapon);
                    }
                }

                // Give glowing Plasma Torch-Blade to melee slot if main agent or officer
                if (agent.IsMainAgent || agent.IsHero || (agent.Index % 5 == 0))
                {
                    ItemObject bladeItem = MBObjectManager.Instance.GetObject<ItemObject>("cig_wpn_vibro_blade");
                    if (bladeItem != null)
                    {
                        MissionWeapon bladeWeapon = new MissionWeapon(bladeItem, null, agent.Origin != null ? agent.Origin.Banner : null);
                        agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon0, ref bladeWeapon);
                    }
                }
            }
            catch
            {
                // Safely ignore equipment override edge cases on non-standard agents
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (Mission.Current == null || Mission.Current.Scene == null)
            {
                return;
            }

            Agent mainAgent = Mission.Current.MainAgent;
            if (!_sceneConfigured && mainAgent != null && mainAgent.IsActive())
            {
                ConfigureSceneForIntergalacticMode(mainAgent);
                _sceneConfigured = true;
            }

            if (_isWalkableBridgeMode)
            {
                TickWalkableFlagshipBridge(dt, mainAgent);
                return;
            }

            if (_isSpaceBattleMode)
            {
                TickSpaceBattleMode(dt, mainAgent);
            }
        }

        // =========================================================================
        // 1. SCENE INITIALIZATION (WALKABLE FLAGSHIP BRIDGE vs 3D SPACE BATTLE)
        // =========================================================================

        private void ConfigureSceneForIntergalacticMode(Agent mainAgent)
        {
            Scene scene = Mission.Current.Scene;
            bool forceBridge = VoidshipArchitectBehavior.Instance != null && VoidshipArchitectBehavior.Instance.IsFlagshipBridgeWalkActive;

            if (forceBridge || IsIndoorOrBridgeScene())
            {
                _isWalkableBridgeMode = true;
                _isSpaceBattleMode = false;
                if (VoidshipArchitectBehavior.Instance != null)
                {
                    VoidshipArchitectBehavior.Instance.IsFlagshipBridgeWalkActive = false;
                }

                SetupWalkableFlagshipBridgeEntities(scene, mainAgent);
                return;
            }

            _isWalkableBridgeMode = false;
            _isSpaceBattleMode = true;

            // Deep Space Nebula Starlight Atmosphere (midnight void sky + cosmic lighting)
            try
            {
                scene.SetPhotoAtmosphereViaTod(0.02f, false);
            }
            catch
            {
            }

            _playerBaseGroundZ = mainAgent.Position.z;
            _playerBaseGroundInitialized = true;

            // Enable 3D Zero-G exclusion from gravity for MainAgent and orbital jump-troops
            if (_zeroGThrustersEnabled)
            {
                try
                {
                    mainAgent.SetExcludedFromGravity(true, false);
                }
                catch
                {
                }
            }

            // Spawn Overhead 3D Capital Voidships exchanging orbital broadsides in the sky
            SpawnOverheadCapitalVoidships(scene, mainAgent.Position);

            string shipName = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.FlagshipName : "ISS Calradia Nova";
            string hullName = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.GetCurrentHullTitle() : "Strike Frigate";

            InformationManager.DisplayMessage(new InformationMessage(
                string.Format("[3D VOID COMBAT ENGAGED] Flagship '{0}' ({1}) in High Orbit!", shipName, hullName),
                Color.FromUint(0xFF00F0FF)));
            InformationManager.DisplayMessage(new InformationMessage(
                "[3D THRUSTER & WEAPON CONTROLS] [Space/W] 3D Look-Vector Flight | [C] Descend | [Shift] Sublight Boost | [G] Toggle Zero-G/Mag-Boots | [F] Fire Plasma Lance (PEW-PEW!) | [Q] Launch Photon Torpedo (WHHHAAAAM!) | [V] Void-Shield Overcharge",
                Color.FromUint(0xFFFFD700)));
        }

        // =========================================================================
        // 2. WALKABLE 3D SPACESHIP COMMAND DECK & HOLOGRAPHIC STARMAP
        // =========================================================================

        private void SetupWalkableFlagshipBridgeEntities(Scene scene, Agent mainAgent)
        {
            Vec3 playerPos = mainAgent.Position;
            Vec3 forward = mainAgent.LookDirection;
            forward.z = 0f;
            if (forward.LengthSquared < 0.001f)
            {
                forward = new Vec3(0f, 1f, 0f, -1f);
            }
            forward.Normalize();

            // 1. Spawn Rotating 3D Holographic Sector Starmap 3.5m ahead of the Captain
            Vec3 holoCenter = playerPos + forward * 3.5f + new Vec3(0f, 0f, 1.35f, -1f);
            _bridgeHoloStarmapCore = CreateGlowingSphereEntity(
                scene,
                holoCenter,
                new Vec3(0.85f, 0.85f, 0.85f, -1f),
                new Vec3(0.05f, 0.85f, 1.0f, -1f),
                9.5f,
                false);

            // Spawn 3 orbiting holographic system planets around the starmap core
            for (int i = 0; i < 3; i++)
            {
                float scale = 0.22f + (i * 0.06f);
                Vec3 planetColor = i == 0
                    ? new Vec3(0.1f, 1.0f, 0.6f, -1f)
                    : (i == 1 ? new Vec3(1.0f, 0.55f, 0.1f, -1f) : new Vec3(0.6f, 0.3f, 1.0f, -1f));
                GameEntity planet = CreateGlowingSphereEntity(
                    scene,
                    holoCenter + new Vec3(1.2f + i * 0.5f, 0f, 0f, -1f),
                    new Vec3(scale, scale, scale, -1f),
                    planetColor,
                    4.0f,
                    false);
                if (planet != null)
                {
                    _bridgeHoloOrbitPlanets.Add(planet);
                }
            }

            // 2. Spawn Pulsing Warp-Core Singularity Column 7.5m behind/ahead on the deck
            Vec3 reactorPos = playerPos + forward * 7.2f + new Vec3(0f, 0f, 1.8f, -1f);
            _bridgeWarpReactorCore = CreateGlowingSphereEntity(
                scene,
                reactorPos,
                new Vec3(1.45f, 1.45f, 2.6f, -1f),
                new Vec3(0.75f, 0.15f, 1.0f, -1f),
                14.0f,
                true);

            // 3. Outfit nearby indoor NPCs as Flagship Bridge Officers in Void-Weave Uniforms
            try
            {
                ItemObject officerSuit = MBObjectManager.Instance != null
                    ? MBObjectManager.Instance.GetObject<ItemObject>("cig_armor_officer_uniform")
                    : null;
                ItemObject sidearm = MBObjectManager.Instance != null
                    ? MBObjectManager.Instance.GetObject<ItemObject>("cig_wpn_arc_blaster")
                    : null;

                foreach (Agent agent in Mission.Current.Agents)
                {
                    if (agent != null && agent.IsHuman && !agent.IsMainAgent && sidearm != null)
                    {
                        MissionWeapon w = new MissionWeapon(sidearm, null, null);
                        agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon0, ref w);
                    }
                }
            }
            catch
            {
            }

            string shipName = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.FlagshipName : "ISS Calradia Nova";
            string hullName = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.GetCurrentHullTitle() : "Class-II Strike Frigate";

            InformationManager.DisplayMessage(new InformationMessage(
                string.Format("[FLAGSHIP COMMAND DECK] Aboard '{0}' ({1}). Walk to the glowing 3D Holographic Starmap or press [B] to open the Ship Designer & Reactor Console!", shipName, hullName),
                Color.FromUint(0xFF00FFAA)));
        }

        private void TickWalkableFlagshipBridge(float dt, Agent mainAgent)
        {
            _bridgeHoloAngle += dt * 1.35f;

            // Animate the rotating 3D Holographic Starmap & Orbiting Colony Worlds
            if (_bridgeHoloStarmapCore != null)
            {
                MatrixFrame coreFrame = _bridgeHoloStarmapCore.GetGlobalFrame();
                coreFrame.rotation.RotateAboutUp(dt * 0.8f);
                _bridgeHoloStarmapCore.SetGlobalFrame(ref coreFrame, true);

                Vec3 center = coreFrame.origin;
                for (int i = 0; i < _bridgeHoloOrbitPlanets.Count; i++)
                {
                    GameEntity planet = _bridgeHoloOrbitPlanets[i];
                    if (planet == null)
                    {
                        continue;
                    }
                    float radius = 1.15f + (i * 0.55f);
                    float speed = 1.6f - (i * 0.35f);
                    float angle = _bridgeHoloAngle * speed + (i * 2.1f);
                    float scale = 0.22f + (i * 0.06f);

                    MatrixFrame pFrame = MatrixFrame.Identity;
                    pFrame.origin = center + new Vec3(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius,
                        (float)Math.Sin(angle * 2f) * 0.22f,
                        -1f);
                    pFrame.rotation.ApplyScaleLocal(scale);
                    planet.SetGlobalFrame(ref pFrame, true);
                }
            }

            // Animate the pulsing Warp-Drive Singularity Core
            if (_bridgeWarpReactorCore != null)
            {
                MatrixFrame rFrame = _bridgeWarpReactorCore.GetGlobalFrame();
                rFrame.rotation.RotateAboutUp(dt * 2.4f);
                _bridgeWarpReactorCore.SetGlobalFrame(ref rFrame, true);
            }

            // Allow opening the Flagship Architect / Reactor / Navigation Console from the Bridge via [B]
            if (Input.IsKeyPressed(InputKey.B) || Input.IsKeyPressed(InputKey.ControllerRBumper))
            {
                if (VoidshipArchitectBehavior.Instance != null)
                {
                    VoidshipArchitectBehavior.Instance.OpenVoidshipDrydockHub();
                }
            }

            // Allow testing a personal holo-blaster pulse on deck with [F]
            if (mainAgent != null && mainAgent.IsActive() && Input.IsKeyPressed(InputKey.F))
            {
                SpawnEnergyBolt(
                    mainAgent,
                    mainAgent.Position + new Vec3(0f, 0f, 1.55f, -1f) + mainAgent.LookDirection * 1.2f,
                    mainAgent.LookDirection,
                    new Vec3(0.0f, 0.95f, 1.0f, -1f),
                    45f,
                    false);
            }
        }

        // =========================================================================
        // 3. 3D DIRECTIONAL ZERO-G SPACE BATTLE, PEW-PEW LASERS & WHHHAAAAM TORPEDOES
        // =========================================================================

        private void SpawnOverheadCapitalVoidships(Scene scene, Vec3 battleCenter)
        {
            // Spawn 2 allied & 2 enemy 3D Capital Voidship superstructures in low orbit above the battle
            for (int i = 0; i < 4; i++)
            {
                bool isAllied = i < 2;
                float sideOffset = (i % 2 == 0) ? -38f : 38f;
                float forwardOffset = isAllied ? -55f : 55f;
                float altitude = 26f + (i * 5.5f);

                Vec3 shipPos = battleCenter + new Vec3(sideOffset, forwardOffset, altitude, -1f);
                Vec3 forwardDir = isAllied ? new Vec3(0f, 1f, 0f, -1f) : new Vec3(0f, -1f, 0f, -1f);
                Vec3 shieldColor = isAllied
                    ? new Vec3(0.05f, 0.85f, 1.0f, -1f)
                    : new Vec3(1.0f, 0.2f, 0.1f, -1f);

                // Hull superstructure mesh
                GameEntity hull = GameEntity.CreateEmptyDynamic(scene, true);
                MetaMesh hullMesh = MetaMesh.GetCopy("boulder2", false, true);
                if (hullMesh == null)
                {
                    hullMesh = MetaMesh.GetCopy("barrier_sphere", false, true);
                }
                if (hullMesh != null)
                {
                    hull.AddMultiMesh(hullMesh, true);
                }
                MatrixFrame hullFrame = MatrixFrame.Identity;
                hullFrame.origin = shipPos;
                Vec3 hullScale = new Vec3(4.5f, 14.0f, 2.8f, -1f);
                hullFrame.rotation.ApplyScaleLocal(ref hullScale);
                hull.SetGlobalFrame(ref hullFrame, true);

                // Glowing Void-Shield Envelope around the Capital Ship
                GameEntity shieldSphere = CreateGlowingSphereEntity(
                    scene,
                    shipPos,
                    new Vec3(6.5f, 16.0f, 4.2f, -1f),
                    shieldColor,
                    22f,
                    false);

                // Sublight Thruster Plume
                Vec3 thrusterPos = shipPos - (forwardDir * 12f);
                GameEntity thruster = CreateGlowingSphereEntity(
                    scene,
                    thrusterPos,
                    new Vec3(2.5f, 2.5f, 2.5f, -1f),
                    isAllied ? new Vec3(0.2f, 0.9f, 1.0f, -1f) : new Vec3(1.0f, 0.45f, 0.05f, -1f),
                    18f,
                    true);

                OverheadVoidshipEntity ship = new OverheadVoidshipEntity();
                ship.HullRoot = hull;
                ship.ShieldSphere = shieldSphere;
                ship.ThrusterGlow = thruster;
                ship.BasePosition = shipPos;
                ship.ForwardDir = forwardDir;
                ship.IsPlayerFleet = isAllied;
                ship.BroadsideCooldown = 1.2f + (i * 0.75f);
                ship.OrbitPhase = i * 1.57f;
                _overheadShips.Add(ship);
            }
        }

        private void TickSpaceBattleMode(float dt, Agent mainAgent)
        {
            if (_whamTorpedoCooldown > 0f)
            {
                _whamTorpedoCooldown -= dt;
            }
            if (_pewBlasterCooldown > 0f)
            {
                _pewBlasterCooldown -= dt;
            }
            if (_shieldOverchargeCooldown > 0f)
            {
                _shieldOverchargeCooldown -= dt;
            }

            // 1. Tick Player 3D Zero-G Thrusters & Direct Space Weapon Hotkeys
            if (mainAgent != null && mainAgent.IsActive())
            {
                TickPlayerZeroGFlightAndWeapons(dt, mainAgent);
            }

            // 2. Tick AI Combatant 3D Orbital Hovering & Void-Shield Regeneration
            _aiHoverUpdateTimer += dt;
            if (_aiHoverUpdateTimer >= 0.08f)
            {
                TickAICombatantsZeroGHoverAndShields(_aiHoverUpdateTimer);
                _aiHoverUpdateTimer = 0f;
            }

            // 3. Tick Overhead 3D Capital Voidships & Automated Turret Broadsides
            TickOverheadCapitalShips(dt, mainAgent);

            // 4. Tick Active 3D Pew-Pew Energy Bolts
            TickActiveEnergyBolts(dt);

            // 5. Tick Active WHHHAAAAM Expanding Plasma Shockwave Spheres
            TickActiveWhamShockwaves(dt);
        }

        private void TickPlayerZeroGFlightAndWeapons(float dt, Agent mainAgent)
        {
            if (!_playerBaseGroundInitialized)
            {
                _playerBaseGroundZ = mainAgent.Position.z;
                _playerBaseGroundInitialized = true;
            }

            // [G] Toggle 3D Zero-G Flight Thrusters vs Mag-Boots Gravity Lock
            if (Input.IsKeyPressed(InputKey.G))
            {
                _zeroGThrustersEnabled = !_zeroGThrustersEnabled;
                _playerZeroGVelocity = Vec3.Zero;
                try
                {
                    mainAgent.SetExcludedFromGravity(_zeroGThrustersEnabled, false);
                }
                catch
                {
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    _zeroGThrustersEnabled
                        ? "[EVA FLIGHT THRUSTERS: ONLINE] Full 3D 6-DOF Zero-G vector flight enabled!"
                        : "[MAG-BOOTS: LOCKED] Gravity boots engaged for surface boarding combat.",
                    Color.FromUint(_zeroGThrustersEnabled ? 0xFF00F0FF : 0xFFFFAA00)));
            }

            // 3D Directional Flight Physics when Zero-G Thrusters are active
            if (_zeroGThrustersEnabled)
            {
                int thrusterPips = VoidshipArchitectBehavior.Instance != null
                    ? VoidshipArchitectBehavior.Instance.ThrusterPowerPips
                    : 2;
                float thrustMultiplier = 1.0f + (thrusterPips * 0.22f);
                if (Input.IsKeyDown(InputKey.LeftShift))
                {
                    thrustMultiplier *= 1.85f;
                }

                Vec3 lookDir = mainAgent.LookDirection;
                lookDir.Normalize();
                Vec3 rightDir = Vec3.CrossProduct(lookDir, new Vec3(0f, 0f, 1f, -1f));
                if (rightDir.LengthSquared > 0.001f)
                {
                    rightDir.Normalize();
                }

                Vec3 accel = Vec3.Zero;
                // Ascend / Thrust along 3D pitch vector
                if (Input.IsKeyDown(InputKey.Space))
                {
                    accel += new Vec3(0f, 0f, 14.5f, -1f) + (lookDir * 6.0f);
                }
                // Descend along Z axis
                if (Input.IsKeyDown(InputKey.C))
                {
                    accel += new Vec3(0f, 0f, -14.5f, -1f);
                }
                // 3D Look-Vector Forward / Reverse / Strafe Thrusters while airborne
                float currentAltitude = mainAgent.Position.z - _playerBaseGroundZ;
                if (currentAltitude > 0.65f || Input.IsKeyDown(InputKey.Space))
                {
                    if (Input.IsKeyDown(InputKey.W))
                    {
                        accel += lookDir * 18.0f;
                    }
                    if (Input.IsKeyDown(InputKey.S))
                    {
                        accel -= lookDir * 14.0f;
                    }
                    if (Input.IsKeyDown(InputKey.D))
                    {
                        accel += rightDir * 14.0f;
                    }
                    if (Input.IsKeyDown(InputKey.A))
                    {
                        accel -= rightDir * 14.0f;
                    }
                }

                _playerZeroGVelocity += accel * thrustMultiplier * dt;
                // Inertial dampeners (smooth space drag)
                _playerZeroGVelocity *= (1.0f - Math.Min(0.9f, dt * 2.4f));

                if (_playerZeroGVelocity.LengthSquared > 0.01f || currentAltitude > 0.5f)
                {
                    Vec3 nextPos = mainAgent.Position + (_playerZeroGVelocity * dt);
                    if (nextPos.z < _playerBaseGroundZ + 0.15f)
                    {
                        nextPos.z = _playerBaseGroundZ + 0.15f;
                        if (_playerZeroGVelocity.z < 0f)
                        {
                            _playerZeroGVelocity.z = 0f;
                        }
                    }
                    else if (nextPos.z > _playerBaseGroundZ + 45.0f)
                    {
                        nextPos.z = _playerBaseGroundZ + 45.0f;
                        if (_playerZeroGVelocity.z > 0f)
                        {
                            _playerZeroGVelocity.z = 0f;
                        }
                    }
                    mainAgent.TeleportToPosition(nextPos);
                }
            }

            // [F] or Left-Bumper: Fire 3D Directed Plasma-Lance Bolt (PEW-PEW!)
            if ((Input.IsKeyPressed(InputKey.F) || Input.IsKeyPressed(InputKey.ControllerLBumper)) && _pewBlasterCooldown <= 0f)
            {
                int weaponPips = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.WeaponPowerPips : 2;
                _pewBlasterCooldown = Math.Max(0.14f, 0.32f - (weaponPips * 0.04f));
                float dmg = 65f + (weaponPips * 18f);

                Vec3 muzzlePos = mainAgent.Position + new Vec3(0f, 0f, 1.55f, -1f) + (mainAgent.LookDirection * 1.35f);
                SpawnEnergyBolt(
                    mainAgent,
                    muzzlePos,
                    mainAgent.LookDirection,
                    new Vec3(0.05f, 0.95f, 1.0f, -1f),
                    dmg,
                    false);
            }

            // [Q] or Right-Bumper: Fire Heavy Photon Torpedo Salvo (WHHHAAAAM!)
            if ((Input.IsKeyPressed(InputKey.Q) || Input.IsKeyPressed(InputKey.ControllerRBumper)) && _whamTorpedoCooldown <= 0f)
            {
                int weaponPips = VoidshipArchitectBehavior.Instance != null ? VoidshipArchitectBehavior.Instance.WeaponPowerPips : 2;
                _whamTorpedoCooldown = 3.5f;
                float torpedoDmg = 165f + (weaponPips * 35f);

                Vec3 muzzlePos = mainAgent.Position + new Vec3(0f, 0f, 1.75f, -1f) + (mainAgent.LookDirection * 1.6f);
                SpawnEnergyBolt(
                    mainAgent,
                    muzzlePos,
                    mainAgent.LookDirection,
                    new Vec3(1.0f, 0.35f, 0.05f, -1f),
                    torpedoDmg,
                    true);

                InformationManager.DisplayMessage(new InformationMessage(
                    "[PHOTON TORPEDO LAUNCHED] Twin-linked plasma warhead away — stand by for WHHHAAAAM detonation!",
                    Color.FromUint(0xFFFF5500)));
            }

            // [V]: Void-Shield Overcharge & Reactor Surge
            if (Input.IsKeyPressed(InputKey.V) && _shieldOverchargeCooldown <= 0f)
            {
                _shieldOverchargeCooldown = 8.0f;
                float maxShield = 220f;
                if (_agentMaxVoidShields.ContainsKey(mainAgent.Index))
                {
                    maxShield = _agentMaxVoidShields[mainAgent.Index];
                }
                _agentVoidShields[mainAgent.Index] = maxShield * 1.5f;
                mainAgent.Health = Math.Min(mainAgent.HealthLimit, mainAgent.Health + 35f);

                SpawnWhamShockwave(
                    mainAgent.Position + new Vec3(0f, 0f, 1.0f, -1f),
                    new Vec3(0.0f, 0.9f, 1.0f, -1f),
                    4.5f,
                    0.45f,
                    null,
                    0f);

                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("[VOID-SHIELD OVERCHARGED] Personal Aegis Barrier surged to {0:0} MJ!", _agentVoidShields[mainAgent.Index]),
                    Color.FromUint(0xFF00FFAA)));
            }

            // [B]: Open Tactical Ship Power & Range-Band Console in Battle
            if (Input.IsKeyPressed(InputKey.B) && VoidshipArchitectBehavior.Instance != null)
            {
                VoidshipArchitectBehavior.Instance.OpenVoidshipDrydockHub();
            }

            // Periodic HUD readout of Player Void-Shield & Altitude
            _hudStatusTimer += dt;
            if (_hudStatusTimer >= 12.0f)
            {
                _hudStatusTimer = 0f;
                float curShield = _agentVoidShields.ContainsKey(mainAgent.Index) ? _agentVoidShields[mainAgent.Index] : 180f;
                float alt = Math.Max(0f, mainAgent.Position.z - _playerBaseGroundZ);
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("[VOID TELEMETRY] Personal Void-Shield: {0:0} MJ | 3D Orbital Altitude: +{1:0.0}m | Active Bolts: {2}",
                        curShield, alt, _activeBolts.Count),
                    Color.FromUint(0xFF88DDFF)));
            }
        }

        private void TickAICombatantsZeroGHoverAndShields(float dt)
        {
            if (Mission.Current == null)
            {
                return;
            }

            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent == null || !agent.IsActive() || !agent.IsHuman)
                {
                    continue;
                }

                // Regenerate Personal Void-Shields continuously
                int idx = agent.Index;
                if (_agentVoidShields.ContainsKey(idx) && _agentMaxVoidShields.ContainsKey(idx))
                {
                    float maxS = _agentMaxVoidShields[idx];
                    if (_agentVoidShields[idx] < maxS)
                    {
                        _agentVoidShields[idx] = Math.Min(maxS, _agentVoidShields[idx] + (dt * 6.5f));
                    }
                }

                if (agent.IsMainAgent)
                {
                    continue;
                }

                // Give a subset of Void-Marines & Strike-Pilots 3D Zero-G Jet-Pack Hovering at tiered orbital altitudes
                if (idx % 3 == 0)
                {
                    float targetOffset;
                    if (!_agentTargetHoverAltitude.TryGetValue(idx, out targetOffset))
                    {
                        targetOffset = 2.2f + ((idx % 4) * 1.65f);
                        _agentTargetHoverAltitude[idx] = targetOffset;
                        try
                        {
                            agent.SetExcludedFromGravity(true, false);
                        }
                        catch
                        {
                        }
                    }

                    float desiredZ = _playerBaseGroundZ + targetOffset + (float)Math.Sin((Mission.Current.CurrentTime * 2.0f) + idx) * 0.45f;
                    Vec3 pos = agent.Position;
                    if (Math.Abs(pos.z - desiredZ) > 0.35f)
                    {
                        pos.z = pos.z + (desiredZ - pos.z) * Math.Min(1.0f, dt * 3.5f);
                        agent.TeleportToPosition(pos);
                    }
                }
            }
        }

        private void TickOverheadCapitalShips(float dt, Agent mainAgent)
        {
            for (int i = 0; i < _overheadShips.Count; i++)
            {
                OverheadVoidshipEntity ship = _overheadShips[i];
                if (ship == null || ship.HullRoot == null)
                {
                    continue;
                }

                ship.OrbitPhase += dt * 0.35f;
                Vec3 driftPos = ship.BasePosition + new Vec3(
                    (float)Math.Cos(ship.OrbitPhase) * 6.0f,
                    (float)Math.Sin(ship.OrbitPhase) * 9.0f,
                    (float)Math.Sin(ship.OrbitPhase * 1.5f) * 1.8f,
                    -1f);

                MatrixFrame hullFrame = MatrixFrame.Identity;
                hullFrame.origin = driftPos;
                Vec3 hullScale = new Vec3(4.5f, 14.0f, 2.8f, -1f);
                hullFrame.rotation.ApplyScaleLocal(ref hullScale);
                ship.HullRoot.SetGlobalFrame(ref hullFrame, true);

                if (ship.ShieldSphere != null)
                {
                    MatrixFrame sFrame = MatrixFrame.Identity;
                    sFrame.origin = driftPos;
                    Vec3 shieldScale = new Vec3(6.5f, 16.0f, 4.2f, -1f);
                    sFrame.rotation.ApplyScaleLocal(ref shieldScale);
                    ship.ShieldSphere.SetGlobalFrame(ref sFrame, true);
                }

                if (ship.ThrusterGlow != null)
                {
                    MatrixFrame tFrame = MatrixFrame.Identity;
                    tFrame.origin = driftPos - (ship.ForwardDir * 12f);
                    tFrame.rotation.ApplyScaleLocal(2.5f);
                    ship.ThrusterGlow.SetGlobalFrame(ref tFrame, true);
                }

                // Fire overhead 3D orbital broadside laser / torpedo salvos across the sky!
                ship.BroadsideCooldown -= dt;
                if (ship.BroadsideCooldown <= 0f)
                {
                    ship.BroadsideCooldown = 2.6f + ((i % 2) * 0.9f);
                    Vec3 targetPos = driftPos + (ship.ForwardDir * 95f) + new Vec3((i % 2 == 0) ? 18f : -18f, 0f, -6f, -1f);
                    Vec3 fireDir = targetPos - driftPos;
                    fireDir.Normalize();

                    Vec3 beamColor = ship.IsPlayerFleet
                        ? new Vec3(0.05f, 0.95f, 1.0f, -1f)
                        : new Vec3(1.0f, 0.15f, 0.1f, -1f);

                    SpawnEnergyBolt(
                        mainAgent,
                        driftPos + (ship.ForwardDir * 6.5f),
                        fireDir,
                        beamColor,
                        0f,
                        i == 0);
                }
            }
        }

        // =========================================================================
        // 4. MISSILE & MELEE HOOKS -> PEW-PEW BOLTS, WHHHAAAAM SHOCKWAVES & VOID-SHIELDS
        // =========================================================================

        public override void OnAgentShootMissile(
            Agent shooterAgent,
            EquipmentIndex weaponIndex,
            Vec3 position,
            Vec3 velocity,
            Mat3 orientation,
            bool hasRigidBody,
            int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);
            if (!_isSpaceBattleMode || shooterAgent == null)
            {
                return;
            }

            Vec3 dir = velocity;
            if (dir.LengthSquared < 0.001f)
            {
                dir = shooterAgent.LookDirection;
            }
            dir.Normalize();

            bool isMainOrAlly = shooterAgent.IsMainAgent ||
                (Mission.Current != null && Mission.Current.MainAgent != null && shooterAgent.Team == Mission.Current.MainAgent.Team);

            // Check if shooter fired a heavy torpedo launcher or gauss railgun
            bool isTorpedo = false;
            float bonusDamage = 48f;
            try
            {
                MissionWeapon wielded = shooterAgent.Equipment[weaponIndex];
                if (!wielded.IsEmpty && wielded.Item != null && wielded.Item.StringId != null)
                {
                    string id = wielded.Item.StringId;
                    if (id.Contains("torpedo") || id.Contains("disruptor"))
                    {
                        isTorpedo = true;
                        bonusDamage = 135f;
                    }
                    else if (id.Contains("railgun"))
                    {
                        bonusDamage = 85f;
                    }
                }
            }
            catch
            {
            }

            Vec3 boltColor = isTorpedo
                ? new Vec3(1.0f, 0.4f, 0.05f, -1f)
                : (isMainOrAlly ? new Vec3(0.05f, 0.95f, 1.0f, -1f) : new Vec3(1.0f, 0.12f, 0.2f, -1f));

            SpawnEnergyBolt(shooterAgent, position + dir * 0.8f, dir, boltColor, bonusDamage, isTorpedo);
        }

        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            base.OnMissileHit(attacker, victim, isCanceled, collisionData);
            if (!_isSpaceBattleMode)
            {
                return;
            }

            if (victim != null && victim.IsActive() && victim.IsHuman)
            {
                HandleVoidShieldAbsorption(victim, 28f, new Vec3(0.1f, 0.9f, 1.0f, -1f));
            }
        }

        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            base.OnMeleeHit(attacker, victim, isCanceled, collisionData);
            if (!_isSpaceBattleMode || victim == null || !victim.IsActive())
            {
                return;
            }

            // Vibro-Blade / Plasma Glaive energy discharge spark + Void-Shield absorption
            HandleVoidShieldAbsorption(victim, 22f, new Vec3(0.6f, 0.2f, 1.0f, -1f));
        }

        private void HandleVoidShieldAbsorption(Agent victim, float incomingEnergyLoad, Vec3 flashColor)
        {
            int idx = victim.Index;
            float shieldLeft;
            if (!_agentVoidShields.TryGetValue(idx, out shieldLeft) || shieldLeft <= 0f)
            {
                return;
            }

            float absorbed = Math.Min(shieldLeft, incomingEnergyLoad);
            _agentVoidShields[idx] = shieldLeft - absorbed;

            // Restore health absorbed by the Void-Shield and flash a personal barrier sphere!
            if (victim.Health > 1f)
            {
                victim.Health = Math.Min(victim.HealthLimit, victim.Health + (absorbed * 0.75f));
            }

            SpawnWhamShockwave(
                victim.Position + new Vec3(0f, 0f, 1.15f, -1f),
                flashColor,
                2.1f,
                0.22f,
                null,
                0f);
        }

        // =========================================================================
        // 5. 3D ENERGY BOLT & WHHHAAAAM PLASMA SHOCKWAVE SIMULATION
        // =========================================================================

        private void SpawnEnergyBolt(
            Agent shooter,
            Vec3 origin,
            Vec3 direction,
            Vec3 color,
            float damage,
            bool isWhamTorpedo)
        {
            if (Mission.Current == null || Mission.Current.Scene == null || _activeBolts.Count > 64)
            {
                return;
            }

            Vec3 dir = direction;
            if (dir.LengthSquared < 0.001f)
            {
                dir = new Vec3(0f, 1f, 0f, -1f);
            }
            dir.Normalize();

            float scale = isWhamTorpedo ? 0.65f : 0.28f;
            float lightRadius = isWhamTorpedo ? 14.0f : 6.5f;

            GameEntity entity = CreateGlowingSphereEntity(
                Mission.Current.Scene,
                origin,
                new Vec3(scale, scale, scale, -1f),
                color,
                lightRadius,
                isWhamTorpedo);

            if (entity == null)
            {
                return;
            }

            ActiveEnergyBolt bolt = new ActiveEnergyBolt();
            bolt.BoltEntity = entity;
            bolt.Position = origin;
            bolt.Velocity = dir * (isWhamTorpedo ? 58f : 96f);
            bolt.Color = color;
            bolt.RemainingLife = isWhamTorpedo ? 2.6f : 1.45f;
            bolt.BaseDamage = damage;
            bolt.IsWhamTorpedo = isWhamTorpedo;
            bolt.Shooter = shooter;
            _activeBolts.Add(bolt);
        }

        private void TickActiveEnergyBolts(float dt)
        {
            for (int i = _activeBolts.Count - 1; i >= 0; i--)
            {
                ActiveEnergyBolt bolt = _activeBolts[i];
                bolt.RemainingLife -= dt;
                bolt.Position += bolt.Velocity * dt;

                if (bolt.BoltEntity != null)
                {
                    float scale = bolt.IsWhamTorpedo ? 0.65f : 0.28f;
                    MatrixFrame frame = MatrixFrame.Identity;
                    frame.origin = bolt.Position;
                    frame.rotation.ApplyScaleLocal(scale);
                    bolt.BoltEntity.SetGlobalFrame(ref frame, true);
                }

                bool detonated = false;

                // Check proximity hit against enemy agents if bolt deals tactical damage
                if (bolt.BaseDamage > 0f && Mission.Current != null)
                {
                    float hitRadiusSq = bolt.IsWhamTorpedo ? 9.0f : 3.2f;
                    foreach (Agent candidate in Mission.Current.Agents)
                    {
                        if (candidate == null || !candidate.IsActive() || !candidate.IsHuman || candidate == bolt.Shooter)
                        {
                            continue;
                        }
                        if (bolt.Shooter != null && bolt.Shooter.Team != null && candidate.Team == bolt.Shooter.Team)
                        {
                            continue;
                        }

                        Vec3 center = candidate.Position + new Vec3(0f, 0f, 1.1f, -1f);
                        if (center.DistanceSquared(bolt.Position) <= hitRadiusSq)
                        {
                            detonated = true;
                            break;
                        }
                    }
                }

                // Check ground / hull floor impact
                if (!detonated && _playerBaseGroundInitialized && bolt.Position.z <= _playerBaseGroundZ + 0.1f)
                {
                    detonated = true;
                }

                if (detonated || bolt.RemainingLife <= 0f)
                {
                    if (detonated || bolt.IsWhamTorpedo)
                    {
                        SpawnWhamShockwave(
                            bolt.Position,
                            bolt.Color,
                            bolt.IsWhamTorpedo ? 9.5f : 3.2f,
                            bolt.IsWhamTorpedo ? 0.65f : 0.28f,
                            bolt.Shooter,
                            bolt.BaseDamage);
                    }

                    if (bolt.BoltEntity != null)
                    {
                        try
                        {
                            bolt.BoltEntity.Remove(0);
                        }
                        catch
                        {
                        }
                    }
                    _activeBolts.RemoveAt(i);
                }
            }
        }

        private void SpawnWhamShockwave(
            Vec3 origin,
            Vec3 color,
            float maxRadius,
            float duration,
            Agent attacker,
            float radialDamage)
        {
            if (Mission.Current == null || Mission.Current.Scene == null)
            {
                return;
            }

            if (_activeShockwaves.Count < 32)
            {
                GameEntity sphere = CreateGlowingSphereEntity(
                    Mission.Current.Scene,
                    origin,
                    new Vec3(0.4f, 0.4f, 0.4f, -1f),
                    color,
                    maxRadius * 2.2f,
                    maxRadius >= 6.0f);

                if (sphere != null)
                {
                    ActiveWhamShockwave wave = new ActiveWhamShockwave();
                    wave.SphereEntity = sphere;
                    wave.Origin = origin;
                    wave.Color = color;
                    wave.Elapsed = 0f;
                    wave.Duration = Math.Max(0.15f, duration);
                    wave.MaxRadius = maxRadius;
                    _activeShockwaves.Add(wave);
                }
            }

            // Apply radial plasma / photon torpedo shockwave damage & Zero-G knockback
            if (radialDamage > 0f)
            {
                float radiusSq = maxRadius * maxRadius;
                foreach (Agent target in Mission.Current.Agents)
                {
                    if (target == null || !target.IsActive() || !target.IsHuman || target == attacker)
                    {
                        continue;
                    }
                    if (attacker != null && attacker.Team != null && target.Team == attacker.Team)
                    {
                        continue;
                    }

                    Vec3 targetCenter = target.Position + new Vec3(0f, 0f, 1.0f, -1f);
                    float distSq = targetCenter.DistanceSquared(origin);
                    if (distSq <= radiusSq)
                    {
                        float falloff = 1.0f - ((float)Math.Sqrt(distSq) / Math.Max(1.0f, maxRadius));
                        float finalDmg = Math.Max(15f, radialDamage * falloff);

                        // Drain target Void-Shield first
                        int idx = target.Index;
                        if (_agentVoidShields.ContainsKey(idx) && _agentVoidShields[idx] > 0f)
                        {
                            float shieldAbsorb = Math.Min(_agentVoidShields[idx], finalDmg * 0.6f);
                            _agentVoidShields[idx] -= shieldAbsorb;
                            finalDmg -= shieldAbsorb;
                        }

                        if (finalDmg > 0f)
                        {
                            target.Health = Math.Max(0f, target.Health - finalDmg);
                            // Zero-G shockwave impulse
                            Vec3 pushDir = targetCenter - origin;
                            if (pushDir.LengthSquared > 0.01f)
                            {
                                pushDir.Normalize();
                                target.TeleportToPosition(target.Position + (pushDir * (1.8f * falloff)) + new Vec3(0f, 0f, 0.85f * falloff, -1f));
                            }
                        }

                        if (attacker != null && attacker.IsMainAgent && maxRadius >= 6.0f)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                string.Format("[WHHHAAAAM! PLASMA IMPACT] {0} hit for {1:0} Void-Energy damage!", target.Name, finalDmg),
                                Color.FromUint(0xFFFF6600)));
                        }
                    }
                }
            }
        }

        private void TickActiveWhamShockwaves(float dt)
        {
            for (int i = _activeShockwaves.Count - 1; i >= 0; i--)
            {
                ActiveWhamShockwave wave = _activeShockwaves[i];
                wave.Elapsed += dt;
                float t = wave.Elapsed / wave.Duration;

                if (t >= 1.0f)
                {
                    if (wave.SphereEntity != null)
                    {
                        try
                        {
                            wave.SphereEntity.Remove(0);
                        }
                        catch
                        {
                        }
                    }
                    _activeShockwaves.RemoveAt(i);
                    continue;
                }

                if (wave.SphereEntity != null)
                {
                    float currentRadius = 0.35f + (wave.MaxRadius * t);
                    MatrixFrame frame = MatrixFrame.Identity;
                    frame.origin = wave.Origin;
                    frame.rotation.ApplyScaleLocal(currentRadius);
                    wave.SphereEntity.SetGlobalFrame(ref frame, true);
                }
            }
        }

        private GameEntity CreateGlowingSphereEntity(
            Scene scene,
            Vec3 position,
            Vec3 scale,
            Vec3 lightColor,
            float lightRadius,
            bool addPlasmaParticles)
        {
            try
            {
                GameEntity entity = GameEntity.CreateEmptyDynamic(scene, true);
                if (entity == null)
                {
                    return null;
                }

                MetaMesh sphereMesh = MetaMesh.GetCopy("barrier_sphere", false, true);
                if (sphereMesh == null)
                {
                    sphereMesh = MetaMesh.GetCopy("prt_mesh_fire_1", false, true);
                }
                if (sphereMesh != null)
                {
                    entity.AddMultiMesh(sphereMesh, true);
                }

                Light pointLight = Light.CreatePointLight(lightRadius);
                if (pointLight != null)
                {
                    pointLight.LightColor = lightColor;
                    entity.AddLight(pointLight);
                }

                if (addPlasmaParticles)
                {
                    entity.AddParticleSystemComponent("psys_game_burning_agent");
                }

                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = position;
                Vec3 localScale = scale;
                frame.rotation.ApplyScaleLocal(ref localScale);
                entity.SetGlobalFrame(ref frame, true);
                return entity;
            }
            catch
            {
                return null;
            }
        }
    }
}
