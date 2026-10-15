---
kind: game
title: "3D 64-bit rendering engine, clinical scenarios, and multi-instance modding in Operant Hills"
game: "Operant Hills"
games_also: []
game_version: "1.0.3 (Web Prod, Web Dev, Android API 36, iOS Capacitor)"
platform: other
engine: custom
route: data
tools: ["Node.js", "Capacitor 7", "Firebase Hosting Multi-Site", "Gradle (JDK 21 / Android SDK 36)", "Headless Chrome"]
anti_cheat: "none"
status: released
agents: ["Google Antigravity"]
humans: ["ronaldtbriggs-blip"]
date: 2026-10-15
links: ["https://github.com/ronaldtbriggs-blip/operant-hills", "https://operant-hills.web.app", "https://operant-hills-dev.web.app"]
tags: ["aba", "3d-canvas", "n64-style", "roguelike-deckbuilder", "capacitor", "firebase-multisite"]
---

# 3D 64-bit rendering engine, clinical scenarios, and multi-instance modding in Operant Hills

> Operant Hills is a self-contained HTML5 Canvas 3D 64-bit Applied Behavior Analysis (ABA) simulation RPG and roguelike deckbuilder built for RBT® and BCBA® certification candidates. All game logic, 3D Gouraud-shaded primitives (`N64Render3D`), clinical case encounters, card pools, and BACB mock exam banks reside in `index.html`, which compiles across four live targets: Production Web (`operant-hills.web.app`), Dev/Modding Web (`operant-hills-dev.web.app`), Android Google Play (`com.operanthills.app` v1.0.3, API 36), and iOS (`Capacitor`).

## Setup
- **Repository**: `https://github.com/ronaldtbriggs-blip/operant-hills`
- **Local Workspace**: `C:\Users\gilde\Documents\Operant-Hills`
- **Hosted Instances**:
  - **Production Instance (`main` branch / `hosting:prod`)**: `https://operant-hills.web.app`
  - **Dev / Modding Instance (`dev` branch / `hosting:dev`)**: `https://operant-hills-dev.web.app`
  - **Google Play Store Android (`com.operanthills.app`)**: `versionCode 3` (`1.0.3`), `minSdkVersion 24`, `targetSdkVersion 36`, signed with `android/app/operant-hills-release.jks`
  - **iOS Capacitor (`com.operanthills.app`)**: `ios/App`

## Route and why
All game assets (3D toy-box character sprites, extruded buildings, vehicles, one-point perspective clinical encounter dioramas, and Web Audio synthesizer tracks) are procedurally rendered in pure JavaScript inside `index.html`. Modding via direct data-table and renderer patching (`route: data`) requires zero external asset bundlers and immediately propagates to Web, Dev, Android, and iOS.

## How the game works (Key Subsystems in `index.html`)
1. **3D 64-Bit Volumetric Renderer (`N64Render3D`)**:
   - Provides `createGouraudGrad`, `createSphereGrad`, `drawBox3D`, `drawCylinder3D`, `drawSphere3D`, `drawRoof3D`, `drawQuad3D`, and `drawDropShadow`.
   - `CHAR_PROFILES` and `initSprites()` generate 4-directional walking animations (`renderChibiFrame`, `renderChibiDog`) with spherical heads, cylindrical limbs, and specular highlights.
2. **Overworld & Interior Maps (`MAPS`, `drawTileArt`, `drawWorldAndInteriors`)**:
   - Tile-based maps (`OVERWORLD`, `CLINIC_INT`, `SCHOOL_INT`, `HOME_INT`, `OFFICE_INT`, `BAKERY_INT`) rendered with Y-depth sorting (`Painter's Algorithm` by `footY`) for buildings, NPCs, player, and autonomous 3D vehicles.
3. **Clinical Scenarios (`CLINICAL_SCENARIOS`, `RBT_MISSIONS`, `BCBA_MISSIONS`)**:
   - 16 interactive clinical cases (`case_clinic_token`, `case_clinic_skinner`, `case_clinic_burst`, `case_school_measure`, `case_home_assent`, etc.) supporting both **Guided Study Mode** (4-step `assess`/`antecedent` -> `behavior`/`design` -> `consequence`/`supervision` -> `ethics`) and **Hardcore Unassisted Mode** (`roguelikeMode`).
   - `drawEncounterBattleCanvas()` renders a dynamic one-point perspective 3D clinical room with sunbeams, a 3D therapy table, a live 3D Token Board, FCT `"BREAK"` card, and reactive 3D actors.
4. **RBT & BCBA Tactical Card Pools (`RBT_CARD_POOL`, `BCBA_CARD_POOL`)**:
   - Used in Hardcore Unassisted Mode (`launchRoguelikeCardEncounter`, `playRoguelikeCard`) with Crisis Twist modulation, Suit Mastery bonuses, `SETUP` -> `EXECUTION` combo chains, and diminishing-returns spam prevention.
5. **BACB Mock Exam Bank (`MOCK_EXAM_QUESTIONS`)**:
   - 147 handcrafted clinical vignettes across BCBA Domains `A`–`I` (6th Ed. TCO) and RBT Domains `RBT_A`–`RBT_F` (2nd/3rd Ed. Task List).
6. **Learner Study Time & Licensing (`getStudyMinutes`, `GUMROAD_PRODUCT_ID`)**:
   - Tracks active learner interaction minutes (`gameState.studySeconds` / `getStudyMinutes()`) in the Top HUD and Practicum Binder.
   - Enforces a `$10` Gumroad license key gate (`GUMROAD_PRODUCT_ID = "IMSOddLYalcW0LeSu4QzRA=="`), auto-hiding external checkout links on native Capacitor builds (`isNativeAppStoreBuild()`) for Google Play Payments Policy compliance.

## Build & Deployment Steps
1. **Sync Web Assets to `www/` and Mobile (`android/`, `ios/`)**:
   ```powershell
   npm run cap:sync
   ```
2. **Deploy to the Dev / Modding Instance (`https://operant-hills-dev.web.app`)**:
   ```powershell
   npm run deploy:dev
   ```
3. **Deploy to the Production Instance (`https://operant-hills.web.app`)**:
   ```powershell
   npm run deploy:prod
   ```
4. **Build Signed Google Play App Bundle (`.aab`)**:
   ```powershell
   $env:JAVA_HOME = "C:\Users\gilde\.jdks\jbr-21.0.11"
   cd android
   .\gradlew.bat bundleRelease --no-daemon
   ```

## Verification
- Extract `<script>` blocks from `index.html` and verify with `node -c` before every deploy.
- Run `python agents/ci_runner.py --event dispatch --mode summary-only` (or `npm run mod:audit`) to verify scenario counts, card pools, exam bank integrity, and instance status.

## Gotchas
1. **Symptom.** Headless Chrome hangs when capturing screenshots of `index.html`. **Cause:** `gameLoop()` schedules infinite `requestAnimationFrame` callbacks and desktop Chrome locks the default profile. **Fix:** Pass a temporary `--user-data-dir` and cap `window.requestAnimationFrame` to 6 frames in headless screenshot harnesses.
2. **Symptom.** Google Play Console rejects `.aab` upload. **Cause:** Play Console requires `targetSdkVersion = 36` and `minSdkVersion = 24` (for Play Automatic Protection), plus an incremented `versionCode` for every upload. **Fix:** Keep `minSdkVersion = 24` and `targetSdkVersion = 36` in `android/variables.gradle` and increment `versionCode` in `android/app/build.gradle`.
