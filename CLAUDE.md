# CLAUDE.md - AI Agent Documentation

## Important Notes for AI Agents

**⚠️ CRITICAL: When reading project assets, follow these rules:**
- **ONLY read image and audio file NAMES and FOLDERS**
- **NEVER read the actual CONTENT of image files** (`.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, etc.)
- **NEVER read the actual CONTENT of audio files** (`.mp3`, `.wav`, `.ogg`, `.m4a`, etc.)
- Reading image/audio content wastes significant tokens without providing useful information
- Focus on C# scripts, prefabs, and scene files for understanding the project

**📝 MANDATORY: Always update this documentation file (CLAUDE.md) when:**
- Logic changes are made to any script
- New features or systems are added
- File structure or architecture changes
- API methods are added, removed, or modified
- Game flow or mechanics are altered
- Keep all sections synchronized with the actual codebase

---

## Project Overview

**2D Chess-Meets-Shooter Mobile Game**

This Unity 2D project is a hybrid tactical game combining:
1. **Chess Mode**: Hexagonal grid-based tactical movement with opponent AI
2. **Standoff Mode**: 2D side-scrolling platformer with shooting mechanics and SUPERHOT-style slow motion

**Unity Version**: 6000.4.0a4

**Game Inspirations:**
- **Shotgun King: The Final Checkmate** - Chess mechanics reversed (player is the pawn, opponent is the shotgun king)
- **Fights in Tight Spaces** - Enemy modifiers and tactical card-based combat
- **SUPERHOT** - Time manipulation mechanics (time moves when you move)
- **Terraria** - Side-scrolling platformer combat style

---

## Game Flow

```
Main Menu (with Showcase System)
    → Displays random pawns moving on hex grid
    → Configurable: pawn count (default 3), movement interval (default 2s)
    → Automatically starts/stops when entering/leaving main menu
    ↓
Level Selection
    ↓
Chess Mode (Hex Grid)
    → Player: Swipe to move in 6 hex directions
    → Opponents: AI-driven pawns with guns (Basic, Handcannon, Shotgun, Sniper)
    → Combat: Walk onto opponent to capture, opponents shoot at player
    → Board cleanup before level load (removes all tiles and pawns)
    ↓
(When only 1 opponent remains)
    ↓
Standoff Mode (2D Platformer)
    → Arena generation: Hexagonal platforms at varying heights
    → Player: Joystick movement + jump button
    → Opponent: AI platforming with shooting
    → Time: Slow motion when player idle (SUPERHOT style)
    → Pause panel: Resuming re-enables slow motion until user input
    → Pawns retained from Chess mode (no cleanup during transition)
    → Win condition: Touch the opponent to capture
    ↓
Victory/Defeat Screen
    → Next level or retry
    → Board cleanup when returning to menu
```

---

## Architecture Overview

### Core Systems (16 Consolidated Scripts)

The codebase has been consolidated from 27 scripts to 16 for better maintainability:

1. **Game Manager.cs** - Unified game state and level management (merged GameStateManager + LevelManager)
   - States: MainMenu, LevelSelect, ChessMode, Standoff, Victory, Defeat, Paused
   - Handles level loading, configuration, and visual settings
   - References: Checkerboard, Platform Generator, Player Controller (auto-finds at runtime)
   - Manages standoff transition with configurable delay (default 1.5s)
   - **Showcase System**: Spawns random pawns on main menu with random movement
     - Configurable: `enableShowcase`, `showcasePawnCount` (default 3), `showcaseMovementInterval` (default 2s)
     - Automatically starts on MainMenu state, stops when leaving
     - Uses smaller grid (radius 2, no extra rows) with random pawn selection
   - **Cleanup System**: `CleanupGameBoard()` removes all tiles and pawns
     - Called before loading new level via `ApplyLevelSettings()`
     - NOT called when transitioning Chess → Standoff (pawns retained)
     - Called when leaving Standoff (except to Victory/Defeat)
   - **Modifier Application**: Applies random modifiers to opponents after spawning
     - Uses `ApplyModifiersAfterSpawn()` coroutine with 0.2s delay
     - Respects level configuration for modifier count and allowed types
     - Handles Tenacious HP boost via PawnCustomiser multiplier

2. **Level Data.cs** - ScriptableObject for level presets (3 levels)
   - Contains Chess Mode Music and Standoff Mode Music (separate tracks per mode)
   - No MenuBGM field (menu music handled by AudioManager)
   - Player HP fields present but overridden by code (always 3 MaxHP, 2 StartHP)
   - **Background toggle**: `ShowInGameBackground` (bool) - controls in-game background visibility
   - No background prefab fields (backgrounds managed by UI Manager)

3. **Pawn Customiser.cs** - ScriptableObject for AI behavior and modifier configurations
   - Chess mode weight configurations for all AI types
   - Standoff mode distance preferences
   - Modifier effect multipliers
   - Platformer movement and jumping parameters
   - **Modifier icon sprites** (stored centrally, not on individual pawns)
   - Allows tweaking AI behavior in Unity Editor without code changes
   - **REQUIRED** for Handcannon, Shotgun, and Sniper to function properly

4. **Audio Manager.cs** - Singleton for music and SFX management
   - Supports fade transitions and volume control
   - Universal Menu BGM via `PlayMenuMusic()` method
   - Separate Chess Mode and Standoff Mode BGM per level (from Level Data)
   - Prevents music restart if already playing the same clip
   - **New Methods:**
     - `PlayChessModeMusic(LevelData)` - Plays Chess BGM from level data
     - `PlayStandoffModeMusic(LevelData)` - Plays Standoff BGM from level data

5. **Time Controller.cs** - SUPERHOT-style slow motion in Standoff mode
   - Slows time when player stops moving
   - **Fixed audio pitch restoration**: `SetSlowMotionEnabled(false)` and `ResetTime()` now properly reset all time variables
   - Automatically resets when leaving Standoff mode via GameManager
   - Re-enabled when resuming from pause in Standoff mode (via UI Manager)

6. **Pawn Health.cs** - Unified health system (merged PlayerPawn + OpponentPawn)
   - Handles HP, damage, death, and visual feedback for both player and opponents
   - Player always has 3 MaxHP and starts with 2 HP (enforced in Awake() and OnEnable())
   - **Separate HP sprite arrays**: Player uses 4-sprite array (0-3 HP), Opponents use variable array
   - Physics expulsion effects with camera zoom pulse on opponent death
   - Expulsion direction calculated based on board bounds (toward closer edge)
   - **Death triggers immediately at 0 HP**: PawnController disabled, rigidbody set to Dynamic with gravity scale 1, rotation unfrozen for physics-based expulsion

7. **Spawner.cs** - Unified spawning (merged PlayerSpawner + PawnSpawner)
   - Spawns player at bottom-right, opponents in upper tiles with weighted probability
   - References: HexGridGenerator, Checkerboard (for registration)
   - Initializes opponent HP from Level Data via `SetOpponentHP()`
   - **Frame delay spawning**: Waits one frame between each pawn spawn to prevent duplicates
   - `SpawnType()` now uses coroutine `SpawnTypeCoroutine()` with `yield return null` between spawns
   - **Cleanup method**: `ClearAllPawns()` removes all player and opponent pawns
   - **Tile position accuracy**: Gets exact tile world center before instantiating pawns
   - **Coordinate parsing**: Parses axial coordinates from tile names before spawning

8. **Weapon System.cs** - Unified weapon handling (merged Firearm + Projectile + GunAiming + TargetingVisualizer)
   - Fire modes: Manual, OnLineOfSight, TrackPlayer, Timed
   - Projectile types: Single, Spread, Beam
   - Includes ProjectileBehavior as nested class for bullet physics
   - **AI type-based firing**: Each AI type has unique firing patterns
   - **fireOffset**: Shared rotation offset for both projectiles and muzzle flash spawned at firePoint
   - **Friendly fire prevention**: Bullets won't damage their source GameObject
   - **Gun rotation system**:
     - **Chess Mode**: Uses hex coordinate-based line-of-sight algorithm to find best aligned direction
     - **Standoff Mode**: Tracks player with angular velocity, locks aim during firing delay
     - **X-axis flip**: Gun flips upside-down (180° X rotation) when aiming left (angle > 90° and < 270°)
     - **Angle-based system**: Uses degrees instead of vectors for consistent rotation
   - **Integrated Targeting Visualization**:
     - **Chess Mode**: Blinks tiles red along firing direction with configurable settings
       - **Shotgun spread**: Shows 3 directions (center + adjacent hex directions)
       - **Other AI types**: Shows single direction along best hex alignment
     - **Standoff Mode**: Draws red LineRenderer from pawn to aim point with obstacle detection
     - Automatically switches between modes, seamlessly integrated
   - **Standoff firing sequence**: Interval → Tracking → Firing Delay → Fire → Repeat
   - **Modifier support**: Applies firing interval and delay multipliers from PawnController

9. **Input System.cs** - Unified input (merged MobileInputManager + VirtualJoystick)
   - Mobile touch joystick and desktop keyboard fallback
   - **Keyboard always available**: Both keyboard and mobile controls work simultaneously
   - When joystick inactive, keyboard input takes over seamlessly

10. **UI Manager.cs** - Unified UI (merged 6 UI scripts)
    - Main menu, level select, game HUD, pause menu, victory/defeat, settings
    - Automatic panel activation/deactivation based on game state
    - Level buttons rendered in sequential order (1, 2, 3...) with centre button 1.2x larger
    - Mobile controls automatically shown/hidden in Standoff mode, **hidden during Victory/Defeat screens**
    - **Removed HP UI elements**: `hpText`, `hpBar`, `heartIconsContainer`, `heartIconPrefab`, `opponentsText`, `gameModeText`
    - **Added level description**: `levelDescriptionText` displays Level Data description during gameplay
    - **Background System**:
      - `mainMenuBackground` - Scene GameObject, enabled in main menu, disabled in-game
      - `inGameBackground` - Scene GameObject, enabled in-game if `ShowInGameBackground` is true in Level Data
      - Methods: `ShowMainMenuBackground()`, `ShowInGameBackground()`, `HideMainMenuBackground()`, `HideInGameBackground()`
      - Backgrounds are toggled via SetActive() instead of instantiation/destruction
    - **Turn Indicator**: Displays "Your Turn" / "Opponent Turn" during Chess mode
      - **Fade-out animation**: Fades out after 1 second with upward slide effect (20px default)
      - Uses CanvasGroup for opacity control and RectTransform for position animation
    - **Announcer System**: Animated notifications with slide-in and fade-out
      - **Updated slide animation**: Slides from original X position (off-screen pivot) to X=0
      - Methods: `ShowAnnouncement(string)`, `ShowOpponentDeathMessage()`, `ShowDamageTakenMessage()`, `ShowStageChangeMessage()`
      - Text in `[brackets]` automatically highlighted in vibrant orange
    - **Level Selection**: Swipeable with ScrollRect support, original prefab deactivated on start, cloned buttons activated
    - **Pause resume enhancement**: Resuming from pause in Standoff re-enables slow motion until user input

11. **Player Controller.cs** - Player movement in both modes
    - Chess: Swipe-based hex movement with 6 direction arrows
    - Standoff: Rigidbody2D platformer physics with ground detection
    - References: HexGridGenerator, Checkerboard (assigned at initialization)
    - Captures opponents in Standoff mode via OnCollisionEnter2D
    - Uses EnhancedTouch for reliable mobile input
    - **Physics mode switching**: Kinematic in Chess Stage, Dynamic in Standoff Stage with adjustable gravity scale (default 2)

12. **Pawn Controller.cs** - Opponent AI in both modes
    - Chess: Weighted directional decision-making based on AI type and player position
    - Standoff: Platformer AI with jump detection and obstacle avoidance
    - References Pawn Customiser for behavior parameters (logs warning if null)
    - **Modifier icon management** via Pawn Customiser (centralized icon storage)
    - **Canvas cleanup**: Automatically destroys Canvas child when modifier is None
    - **Automatic conversion**: Basic → Handcannon when last opponent enters Standoff
    - **Physics mode switching**: Kinematic in Chess, Dynamic in Standoff with gravity
    - **Modifier system**: 5 modifier types with visual icons and gameplay effects
    - **Helper methods**: Coordinate parsing, tile existence checking, distance calculation
    - **Movement validation**: Checks tile existence and occupied status before moving

13. **Chequerboard.cs** - Turn-based coordination
    - Manages opponent turn sequence with firing and movement
    - Updates turn indicator via `UIManager.SetTurnIndicator(bool)`
    - **Opponent registration**: Tracks all opponent pawns for turn management
    - **Turn sequence**: Fire → Move (1-2 times for Fleet) → Recalculate aim → Next opponent
    - **Reflexive modifier handling**: Additional aim recalculation after all opponents move
    - **Occupied tile tracking**: Prevents multiple opponents from choosing same tile
    - **Movement validation**: Ensures valid moves and handles failed attempts
    - **Player capture detection**: Checks for player capture after opponent moves

14. **HexGrid.cs** (class: HexGridGenerator) - Procedural hex grid generation
    - Grid activation managed by GameManager.SetupChessMode()

15. **Platform.cs** - Procedural Standoff arena generation

16. **Follow Camera.cs** (class: FollowCamera) - Orthographic camera with auto-tracking
    - Auto-discovers and tracks all hex grid tiles and platform tiles
    - **Tag-based filtering**: Only tracks objects tagged "Tile" or "Wall"
    - Scales camera to fit all tiles with configurable padding
    - **Zoom pulse effects**: Aggregated kill-based camera pulses with configurable multipliers
    - **Grid discovery refresh**: Updates on game state changes (ChessMode, Standoff)
    - **Player margin constraints**: Keeps player within screen margins in Standoff mode
    - **Position clamping**: Camera position clamped to 5 units radius from origin
    - **Enhanced zoom**: Additional 5% zoom out to show more background
    - **Standoff mode tracking**: Includes Platform containers and all pawns for bounds

---

## Game Modes

### Chess Mode

**Grid System:**
- Hexagonal grid (axial coordinates: q, r)
- Two orientations: FlatTop, PointyTop
- Generated by `HexGrid.cs`

**Player:**
- `Player Controller.cs`: Swipe-based movement with visual arrow indicators
- `Pawn Health.cs` (PawnType.Player): Health with sprite-based HP display
  - **Always 3 MaxHP, 2 StartHP** (hardcoded in Awake/OnEnable, cannot be changed)
  - Uses 4-sprite array: [0 HP, 1 HP, 2 HP, 3 HP]
- 6-directional movement on hex grid
- Capture opponents by moving onto their tile (deals damage = opponent's current HP)

**Opponents:**
- `Pawn Controller.cs`: AI decision-making via Pawn Customiser
- `Pawn Health.cs` (PawnType.Opponent): Health, death effects, physics expulsion
  - Uses variable-length HP sprite array (index matches HP value)
  - Expulsion animation: scale up → physics impulse → destroy
  - Impulse direction calculated from board bounds (left/right edge detection)
  - **Death physics**: Rigidbody2D set to Dynamic with gravity scale 1, rotation unfrozen (RigidbodyConstraints2D.None) for realistic physics-based expulsion
- 4 AI types:
  - **Basic**: Moves toward player (limited to 3 bottom directions, cannot move backward/upward in world space)
  - **Handcannon**: Mid-range specialist, prefers closest tiles (requires Pawn Customiser)
  - **Shotgun**: Aggressive attacker (strongly prefers moving toward player) (requires Pawn Customiser)
  - **Sniper**: Defensive, prefers farthest tiles (requires Pawn Customiser)
- **Pawn Customiser Required**: Without it, all AI types default to Basic behavior with warning logged

**Shooting Mechanics (Chess Mode):**
- `WeaponSystem.cs`: Handles weapons, projectiles, and gun aiming
- **Basic**: No shooting (only capture by moving onto player's tile)
- **Handcannon**: Fires 1 bullet dealing 1 damage when turn starts
- **Shotgun**: Fires 3 bullets (0°, +60°, -60°) each dealing 1 damage when turn starts
- **Sniper**: Fires 1 bullet dealing 2 damage, pierces once for 1 damage when turn starts
- All pawns with firearms fire **once when their turn starts** (step 1 of opponent turn)
- **Targeting visualization**: Blinks tiles red along firing direction before shooting
- **Hex-based aiming**: Uses line-of-sight algorithm to find best aligned hex direction
- **Friendly fire prevention**: Bullets ignore collision with their source pawn
- **Projectile spawning**: Spawned at firePoint with shared fireOffset rotation

**Shooting Mechanics (Standoff Mode):**
- **Interval-based firing system** with 4 phases:
  1. **Tracking Phase**: Pawn moves based on AI type, gun follows player with angular velocity
  2. **Firing Delay**: After fire interval, lock aim position and wait for firing delay
  3. **Fire**: Shoot according to AI type (same patterns as Chess mode)
  4. **Repeat**: Restart interval timer and return to tracking phase
- **Modifier effects**: Fire interval and delay multipliers applied from PawnController
- **Gun rotation**: Matches aim angle with X-axis flip when aiming left (> 90°, < 270°)
- **Targeting visualization**: Red LineRenderer shows aim direction with obstacle detection
- **Recoil system**: Optional physical recoil force applied to shooter's Rigidbody2D

**Opponent Modifiers:**
- Visual indicator: Modifier icon displayed at top-right of each opponent pawn
- **Icons stored in Pawn Customiser** (centralized, not per-pawn)
- Icon automatically shown/hidden based on modifier via `UpdateModifierIcon()`
- 5 modifier types that enhance opponent capabilities:

1. **Tenacious** 🛡️
   - Requires two captures to remove the pawn (spawns with 2 HP instead of 1)
   - Chess: Player must capture twice
   - Standoff: Takes 2 damage to defeat

2. **Confrontational** ⚔️
   - Chess: Shoots whenever another piece enters their line-of-sight (in addition to turn start)
   - Standoff: Reduces fire interval by 25% (fires more frequently)

3. **Fleet** 💨
   - Chess: Moves an extra time per turn (moves 2 tiles but only shoots once at turn start)
   - Standoff: Moves 25% faster

4. **Observant** 👁️
   - Chess: Bullets only damage the player (won't hit other opponents)
   - Standoff: Firing delay reduced by 50% (0.5s → 0.25s)

5. **Reflexive** 🎯
   - Chess: Recalculates best aiming direction after the player moves
   - Standoff: Firearm is fixed on player (instant tracking), firing delay reduced by 25%

**Last Opponent Conversion:**
- If the last remaining opponent is **Basic** type when entering Standoff mode:
  - Inherits all stats and modifiers
  - Converts to **Handcannon** type via `ConvertBasicToHandcannon()`
  - WeaponSystem component added if not present
  - Ensures final showdown has shooting mechanics

---

### Standoff Mode

**Arena Generation:**
- `Platform.cs`: Procedural hex-based platform generation
- **Algorithm**:
  1. Spawn floor tiles starting at origin, expand right with direction constraints
  2. Generate platform 2 tiles above selected floor tile (configurable base index)
  3. Mirror all tiles left-to-right across vertical axis
  4. Add random connecting tiles for variety
- **Spawn positions**: Player at leftmost floor tile, opponent at rightmost floor tile
- **Height calculation**: Both spawn one tile height above highest tile in arena
- **Tile data tracking**: Maintains list of all tiles with coordinates and height levels

**Player:**
- Modified `Player Controller.cs` with platformer physics
- Rigidbody2D-based movement (Dynamic bodyType with adjustable gravity scale, default 2)
- Jump mechanics with ground detection
- Mobile: Virtual joystick + jump button
- Desktop: WASD/Arrows + Space

**Opponent AI:**
- Modified `Pawn Controller.cs` with platformer AI
- Rigidbody2D physics (Dynamic bodyType with adjustable gravity scale, default 2)
- Intelligent jumping (obstacles, gaps, platforms)
- Distance-based behavior:
  - Basic/Shotgun: Aggressive (always approach player)
  - Handcannon: Mid-range (maintain 2-4 unit distance)
  - Sniper: Defensive (retreat when too close)
- Edge detection to avoid falling

**Time Mechanics:**
- `TimeController.cs`: SUPERHOT-style slow motion system
- **Normal time** (1.0x) when player moves or provides input
- **Slow motion** (0.1x default) when player idle for movement threshold duration
- **Smooth transitions** with configurable transition speed
- **Audio pitch matching**: Adjusts all audio sources to match time scale
- **Input detection**: Monitors keyboard, mouse, and joystick activity
- **Automatic reset**: Properly resets when leaving Standoff mode

---

## Code Structure

### Consolidated Folder Layout
```
Assets/
├── Script/                    # 16 Consolidated Scripts
│   ├── Core Systems:
│   │   ├── Game Manager.cs     # Game state + Level management
│   │   ├── Level Data.cs       # ScriptableObject
│   │   ├── Pawn Customiser.cs  # ScriptableObject for AI behavior configs
│   │   ├── Audio Manager.cs
│   │   ├── Time Controller.cs
│   │
│   ├── Gameplay:
│   │   ├── Player Controller.cs
│   │   ├── Pawn Controller.cs
│   │   ├── Pawn Health.cs      # Player + Opponent health
│   │   ├── Chequerboard.cs
│   │   ├── HexGrid.cs          # HexGridGenerator class
│   │   ├── Spawner.cs          # Player + Opponent spawning
│   │   ├── Platform.cs
│   │   ├── Weapon System.cs    # Firearm + Projectile + GunAiming + TargetingVisualizer
│   │
│   ├── Input & UI:
│   │   ├── Input System.cs     # Mobile + Desktop input
│   │   ├── UI Manager.cs       # All UI screens
│   │
│   └── Utilities:
│       └── Follow Camera.cs    # FollowCamera class
│
├── Prefab/
│   ├── Default Tile.prefab
│   ├── Player Pawn.prefab
│   ├── Pawn.prefab (Basic)
│   ├── Pawn Hand Cannon.prefab
│   ├── Pawn Shotgun.prefab
│   └── Pawn Sniper.prefab
│
├── Sprite/
├── Audio/
└── Main Scene.unity
```

---

## Key Design Patterns

### 1. Singleton Pattern
Used for global managers:
- `GameManager.Instance` (Game Manager.cs)
- `AudioManager.Instance` (Audio Manager.cs)
- `TimeController.Instance` (Time Controller.cs)
- `InputSystem.Instance` (Input System.cs)
- `UIManager.Instance` (UI Manager.cs)
- `Checkerboard.Instance` (Chequerboard.cs)
- `FollowCamera.Instance` (Follow Camera.cs)

### 2. Component-Based Architecture
Each GameObject has focused components:
- Health: `PawnHealth.cs` (with PawnType enum)
- Movement: `Player Controller.cs`, `Pawn Controller.cs`
- Weapons: `WeaponSystem.cs`
- Visual Feedback: `TargetingVisualizer.cs` (attached to pawns with firearms)

### 3. Mode Switching
Controllers support both modes:
```csharp
// Switch player to Standoff mode
playerController.SetStandoffMode(true);

// Switch opponent to Standoff mode
pawnController.SetStandoffMode(true);
weaponSystem.SetStandoffMode(true);
```

### 4. Event-Driven Communication
Using UnityEvents:
```csharp
// Health changes
PawnHealth.OnHPChanged.AddListener(OnHealthChanged);

// Game state changes
GameManager.OnStateChanged.AddListener(HandleStateChange);
GameManager.OnVictory.AddListener(ShowVictoryScreen);
```

---

## Coordinate Systems

### Chess Mode (Axial Hexagonal)
```
Axial Coordinates (q, r):
- q: Horizontal offset
- r: Vertical offset
- Neighbor deltas (Flat-top orientation):
  Index 0: TopRight (1,0)
  Index 1: BottomRight (1,-1)
  Index 2: Bottom (0,-1)
  Index 3: BottomLeft (-1,0)
  Index 4: TopLeft (-1,1)
  Index 5: Top (0,1)

Conversion to world position:
FlatTop:
  x = sqrt(3) * tileSize * (q + r/2)
  y = (3/2) * tileSize * r

PointyTop:
  x = (3/2) * tileSize * q
  y = sqrt(3) * tileSize * (r + q/2)

World-space angles for Flat-top grid:
  Top: 90°
  TopRight: 30°
  BottomRight: -30°
  Bottom: -90°
  BottomLeft: -150° (with X-flip 180°)
  TopLeft: -210° (with X-flip 180°)
```

### Standoff Mode (World Space)
- Standard Unity 2D world coordinates
- Rigidbody2D physics
- Gravity: 2f (adjustable)

---

## AI Systems

### Chess Mode AI Weights

**Basic:**
- Allowed directions: (0,-1), (-1,0), (1,-1) - bottom 3 directions only
- **Cannot move backward** (upward in world space y-axis)
- Closest to player: weight 5
- Others: weight 1
- Behaves like chess pawns (forward only, never backward)

**Handcannon:**
- All 6 directions allowed
- Closest: weight 3
- Others: weight 1
- Mid-range specialist

**Shotgun:**
- All 6 directions allowed
- **Aggressive directional weighting:**
  - Closest to player: weight 4 (highest priority)
  - Top-right and top-left: weight 3
  - Bottom-right and bottom-left: weight 2
  - Farthest from player: weight 1
- Strongly favors moving toward player

**Sniper:**
- All 6 directions
- Farthest: weight 4
- Closest: weight 1
- Others: weight 2
- Defensive positioning

### Standoff Mode AI Behavior

**Decision-making (0.5s intervals):**
1. Calculate distance to player
2. Choose movement direction based on AI type:
   - **Basic/Shotgun**: Aggressive - always approach player
   - **Handcannon**: Mid-range - maintain 2-4 unit distance
   - **Sniper**: Defensive - retreat when player gets too close
3. Perform jump checks:
   - Obstacle detection (forward raycast)
   - Edge detection (downward raycast)
   - Gap jumping (far ground check)

---

## Mobile Controls

### Chess Mode
- Touch anywhere on screen
- Swipe in desired direction
- Visual arrow feedback during swipe
- Dead zone: 24 pixels

### Standoff Mode
- **Virtual Joystick**: Movement (left side of screen)
- **Jump Button**: Jump action (right side of screen)
- **Desktop Fallback**: WASD/Arrows + Space

---

## Inspector Configuration

All values are exposed in the Unity Inspector with tooltips:

**Example (PlayerController):**
```csharp
[Header("Chess Mode")]
[Tooltip("Movement animation duration")]
public float moveDuration = 0.12f;

[Header("Standoff Mode Settings")]
[Tooltip("Movement speed in Standoff mode")]
public float standoffMoveSpeed = 5f;

[Tooltip("Jump force")]
public float jumpForce = 10f;
```

---

## Level Configuration

Create new levels using ScriptableObjects:

1. Right-click in Project → Create → Game → Level Data
2. Configure:
   - Grid settings (radius, extraRows, tileSize)
   - Opponent counts (Basic, Handcannon, Shotgun, Sniper)
   - Opponent HP (default 1, can be increased)
   - Player HP (starting, max) - **Note: These fields are present but overridden by PawnHealth code**
   - Platform settings (floorTiles, platformBaseIndex, platformExpansions, randomTileCount)
   - Difficulty (opponentFireRate, detectionRange)
   - Audio/Visual:
     - **Chess Mode Music** (plays during Chess mode)
     - **Standoff Mode Music** (plays during Standoff mode)
     - Skybox (optional material)

3. Add to `GameManager.levels[]` array

**Important Notes:**
- Player HP is **hardcoded** to 3 MaxHP and 2 StartHP in `PawnHealth.cs`
- Level Data player HP fields are ignored by the code
- No MenuBGM field - menu music handled by `AudioManager.PlayMenuMusic()`

### Adding a New Level
1. Create `LevelData` ScriptableObject
2. Configure all parameters:
   - **Opponent Spawning**: Set pawn counts (Basic, Handcannon, Shotgun, Sniper)
   - **Opponent HP**: Set base HP for all opponents (default: 1)
   - **Modifier Configuration** (Simplified):
     - **Pawn Customiser Reference**: Assign a PawnCustomiser asset (defines all modifiers)
     - **Modifier Count**: Total number of modifiers to randomly apply to opponents
     - **Allow per Modifier**: Boolean toggle for each modifier type (default: Tenacious/Confrontational/Fleet enabled; Observant/Reflexive disabled)
     - **AllowDuplicateModifiers** (default: true) - Same modifier can appear on multiple pawns
   - **Player Settings**: Player always has 3 MaxHP and 2 StartHP (hardcoded)
   - **Grid/Platform Settings**: Board size, platform layout for Standoff
   - **Difficulty Settings**: Opponent fire rate and detection range
   - **Visual Settings**: Music and skybox
3. Add to `GameManager.levels[]` array
4. Level buttons auto-generate in UIManager

**Modifier Configuration Example:**
```csharp
// Level with 2 total modifiers randomly applied from Tenacious/Confrontational/Fleet
pawnCustomiser = defaultPawnCustomiser;  // Reference to Pawn Customiser
ModifierCount = 2;  // Apply 2 modifiers total to opponents
TenaciousModifier = true;
ConfrontationalModifier = true;
FleetModifier = true;
ObservantModifier = false;  // Disabled for this level
ReflexiveModifier = false;   // Disabled for this level
AllowDuplicateModifiers = true;   // Same modifier can appear on multiple pawns
```

**Modifier Selection API:**
- `GetAllowedModifiers()` - Returns list of allowed modifiers based on toggle settings
- `GetRandomAllowedModifier()` - Returns random modifier from allowed list
- **Usage in Spawner**:
  ```csharp
  LevelData level = GameManager.Instance.CurrentLevel;
  // Apply ModifierCount modifiers to opponents
  for (int i = 0; i < level.ModifierCount && i < totalOpponents; i++)
  {
      PawnController.Modifier randomModifier = level.GetRandomAllowedModifier();
      int targetOpponent = level.AllowDuplicateModifiers 
          ? Random.Range(0, totalOpponents) 
          : (i % totalOpponents);  // Cycle through opponents if no duplicates
      opponents[targetOpponent].SetModifier(randomModifier);
  }
  ```
---

## Performance Considerations

### Optimizations Applied
1. **Script Consolidation**: 27 scripts → 16 scripts (41% reduction)
2. **Raycasts**: Limited frequency (AI thinks every 0.5s)
3. **Coroutines**: Used for smooth animations instead of Update loops
4. **Camera bounds caching**: Follow Camera calculates bounds once
5. **Input caching**: InputSystem caches references

### Token Preservation for AI Agents
- **Never read image files**: Sprites, textures, icons
- **Never read audio files**: Music, SFX
- **Only read C# scripts** for code analysis
- **List asset names** without reading content

---

## Common Tasks

### Adding a New AI Type
1. Add enum value to `PawnController.AIType`
2. Add case to `ApplyCombinedWeights()` in `Pawn Controller.cs`
3. Add case to `MakeStandoffDecision()` in `Pawn Controller.cs`
4. Add firing logic to `WeaponSystem.Fire()` method
5. Create prefab with configured `PawnController` and `WeaponSystem`
6. Add to level configuration

### Assigning Modifiers to Opponents

**Three ways to assign modifiers:**

1. **Via LevelData Configuration** (Recommended for game designers):
   - Assign a `PawnCustomiser` reference to the level
   - Set `ModifierCount` to control how many modifiers appear
   - Toggle Allow/Deny for each modifier type
   - Modifiers are randomly selected from allowed types
   - Spawner applies modifiers to opponents during initialization
   ```csharp
   // In Spawner or modifier assignment system:
   LevelData level = GameManager.Instance.CurrentLevel;
   
   if (level.pawnCustomiser != null && level.ModifierCount > 0)
   {
       List<PawnController.Modifier> allowedModifiers = level.GetAllowedModifiers();
       if (allowedModifiers.Count > 0)
       {
           // Apply ModifierCount random modifiers to opponents
           for (int i = 0; i < level.ModifierCount && i < opponentList.Count; i++)
           {
               PawnController.Modifier randomModifier = level.GetRandomAllowedModifier();
               
               // Choose target opponent
               int targetIndex = level.AllowDuplicateModifiers 
                   ? Random.Range(0, opponentList.Count) 
                   : i;  // Cycle through opponents if no duplicates allowed
               
               opponentList[targetIndex].SetModifier(randomModifier);
           }
       }
   }
   ```

2. **In Code** (Via spawner or script):
   ```csharp
   PawnController pawn = GetComponent<PawnController>();
   pawn.SetModifier(PawnController.Modifier.Tenacious);
   // Icon automatically retrieved from Pawn Customiser and displayed
   ```

3. **In Unity Inspector**:
   - Select opponent pawn prefab or instance
   - In `PawnController` component, set `Modifier` dropdown
   - **No need to assign individual icon sprites** - they're stored centrally in Pawn Customiser
   - Assign `Modifier Icon Image` (UI Image component for display)

**Modifier UI Setup:**
- Create a Canvas child under the pawn GameObject (World Space)
- Add UI Image component positioned at top-right of pawn
- Assign this Image to `PawnController.modifierIconImage`
- Icon automatically updates via `UpdateModifierIcon()` which fetches sprite from Pawn Customiser
- Image is enabled/disabled automatically based on whether modifier is None

**Centralized Icon Storage:**
- All 5 modifier icons stored in Pawn Customiser ScriptableObject
- Method `GetModifierIcon(Modifier)` returns appropriate sprite
- This prevents duplicate icon sprite assignments across multiple pawn prefabs

**Testing Modifiers:**
- **Tenacious** (2x HP by default): Verify pawn survives first capture, dies on second
  - Configurable via `PawnCustomiser.modifierEffects.tenaciousHPMultiplier` (range: 1-5)
- **Confrontational**: Watch for firing when entering LOS (Chess) or faster firing (Standoff)
- **Fleet**: Count moves per turn (should be 2 in Chess), check movement speed (Standoff)
- **Observant**: Verify bullets only damage player in Chess, check firing delay in Standoff
- **Reflexive**: Watch aim recalculation after player moves (Chess), verify instant tracking (Standoff)

---

## UI System API Reference

### Turn Indicator

Call from Checkerboard or any script when turn changes:
```csharp
// Set turn to player
UIManager.Instance.SetTurnIndicator(true);

// Set turn to opponent
UIManager.Instance.SetTurnIndicator(false);
```

### Announcer System

Display animated announcements with highlighted text:
```csharp
// Generic announcement (text in [brackets] highlighted in orange)
UIManager.Instance.ShowAnnouncement("[Important] message here!");

// Opponent death message
UIManager.Instance.ShowOpponentDeathMessage(PawnController.AIType.Sniper);
// Output: "Sniper pawn has been captured."

// Damage taken message
UIManager.Instance.ShowDamageTakenMessage(PawnController.AIType.Shotgun, 2, 1);
// Output: "Shotgun pawn dealt 2 damage to your pawn, your pawn has 1 HP left."

// Stage change message (entering Standoff)
UIManager.Instance.ShowStageChangeMessage(PawnController.AIType.Handcannon);
// Output: "Down to one opponent pawn, you are now in a duel with Handcannon pawn."
```

### Announcer Configuration (Inspector)
- **Announcer Panel**: Slides from original X position (off-screen pivot) to X=0
- `announcerSlideInDuration`: 0.3s
- `announcerDisplayDuration`: 2.0s
- `announcerFadeOutDuration`: 0.5s
- `announcerHighlightColor`: Vibrant orange (#FF8000)

### Turn Indicator Configuration (Inspector)
- `turnIndicatorDisplayDuration`: 1.0s (time before fade-out begins)
- `turnIndicatorFadeDuration`: 0.5s (fade-out animation duration)
- `turnIndicatorSlideDistance`: 20px (upward slide distance during fade)
- Uses CanvasGroup for opacity control and RectTransform for position animation

---

## Recent Logic Changes

**Important implementation updates as of this documentation revision:**

### 1. UI Animations
- **Announcer Panel**: Changed slide-in animation from calculated screen width offset to sliding from original X position (off-screen pivot) to X=0
- **Turn Indicator**: Added fade-out after 1 second with upward slide effect (20px default), uses CanvasGroup and RectTransform for smooth animations

### 2. Physics System
- **Pawn Physics Switching**: All pawns (player and opponents) are now Kinematic in Chess Stage and Dynamic in Standoff Stage
  - Adjustable gravity scale field added (default: 2)
  - Player Controller: `standoffGravityScale` field
  - Pawn Controller: `standoffGravityScale` field
- **Death Physics**: When opponent HP drops to 0, death triggers immediately:
  - PawnController disabled to prevent further movement
  - Rigidbody2D set to Dynamic with gravity scale 1
  - Rotation unfrozen (RigidbodyConstraints2D.None) for realistic physics-based expulsion

### 3. Weapon System (Refactored December 2025)
- **Complete Refactoring**: Weapon System completely rewritten for consistent aiming
- **Angle-Based System**: Changed from vector-based to angle-based aiming (degrees)
  - `currentAimAngle` and `targetAimAngle` replace direction vectors
  - Simplifies rotation calculations and reduces floating-point errors
- **X-Axis Flip**: Changed from Y-axis to X-axis flip for gun rotation
  - Gun flips upside-down (X=180°) when angle > 90° and < 270°
  - Prevents right-side-left appearance issues
- **Hex Direction Angles**: Standardized for flat-top grids
  - Index 0: Right (0°), 1: Top-right (60°), 2: Top-left (120°)
  - Index 3: Left (180°), 4: Bottom-left (240°), 5: Bottom-right (300°)
- **Shotgun Spread**: 3 bullets at 0°, +60°, -60° in BOTH Chess and Standoff modes
  - Previously had different spread patterns per mode
- **Projectile Spawning**: `SpawnProjectile(angleOffset, damage, piercing)`
  - Takes angle offset relative to current aim angle
  - Automatically applies X-flip, fire offset, and normalizes angles
- **fireOffset**: Applies to both projectiles and muzzle flash spawning
- **Friendly Fire Prevention**: Projectiles track source GameObject to prevent self-damage
  - ProjectileBehavior.Initialize() takes GameObject parameter

### 4. Level Selection
- **Button Management**: Original level button prefab is now deactivated on start
- **Cloned Buttons**: Only cloned/instantiated buttons are activated and displayed
- **ScrollView**: Proper layout support for swipeable level selection

### 5. Showcase System (December 2025)
- **Main Menu Display**: Random pawns spawn and move on hex grid in background
- **Configuration**: `enableShowcase`, `showcasePawnCount` (default 3), `showcaseMovementInterval` (default 2s)
- **Auto-management**: Starts when entering MainMenu state, stops when leaving
- **Implementation**: Uses smaller grid (radius 2, no extra rows) with random pawn selection and hex-based movement

### 6. Cleanup and Spawning Flow (December 2025)
- **Board Cleanup**: `CleanupGameBoard()` added to Game Manager
  - Removes all pawns (via Spawner.ClearAllPawns()) and hex tiles
  - Called before loading new level in `ApplyLevelSettings()`
  - NOT called during Chess → Standoff transition (pawns retained)
  - Called when leaving Standoff (except to Victory/Defeat states)
- **Frame Delay Spawning**: Spawner now uses coroutine with 1-frame delay between spawns to prevent duplicates
  - `SpawnType()` → `SpawnTypeCoroutine()` with `yield return null`

### 7. UI Improvements (December 2025)
- **Removed HP Display**: Eliminated `hpText`, `hpBar`, `heartIconsContainer`, `heartIconPrefab`, `opponentsText`, `gameModeText`
- **Added Level Description**: New `levelDescriptionText` field displays Level Data description during gameplay
- **Background System Overhaul**:
  - Main menu background: Always shown from UI Manager's `mainMenuBackgroundPrefab`
  - In-game background: Shown from UI Manager's `inGameBackgroundPrefab` if Level Data's `ShowInGameBackground` is true
  - Centralized in UI Manager (removed from Level Data prefab fields)
- **Mobile Controls Fix**: Always visible in Standoff mode on all platforms
- **Pause Resume Enhancement**: Resuming from pause in Standoff re-enables slow motion until user provides input

### 8. Input System Enhancement (December 2025)
- **Keyboard Always Available**: Both keyboard and mobile joystick work simultaneously
- **Seamless Fallback**: When joystick is inactive, keyboard input automatically takes over
- **Fixed Issue**: Removed `!enableMobileControls` check that prevented keyboard input

### 9. Time Controller Audio Fix (December 2025)
- **Pitch Restoration**: `SetSlowMotionEnabled(false)` and `ResetTime()` now properly reset audio pitch to 1.0
- **State Reset**: Both methods reset `currentTimeScale`, `targetTimeScale`, and `slowMotionEnabled`
- **Auto-Reset**: Game Manager calls `TimeController.ResetTime()` when leaving Standoff mode

### 10. Background System Refactor (December 2025)
- **Changed from Prefabs to Scene Objects**: UI Manager now references existing GameObjects in scene
- **Main Menu Background**: Always enabled when in MainMenu state, disabled otherwise
- **In-Game Background**: Enabled when in gameplay if `ShowInGameBackground` toggle is true in Level Data
- **Implementation**: Uses `SetActive(true/false)` instead of `Instantiate()` and `Destroy()`
- **Removed Fields**: Deleted `backgroundContainer`, `mainMenuBackgroundPrefab`, `inGameBackgroundPrefab`
- **Added Fields**: `mainMenuBackground` and `inGameBackground` (direct GameObject references)

### 11. Chess Stage Gun Aiming Offset (December 2025)
- **New Field**: `chessStageAimOffset` in Weapon System (default -15 degrees)
- **Purpose**: Compensates for sprite orientation differences between Chess and Standoff modes
- **Implementation**: Added to gun rotation angle in `ApplyGunRotation()` only when NOT in Standoff mode
- **Adjustable**: Inspector-editable float field for fine-tuning per pawn type
- **Location**: Weapon System.cs:98, applied in Weapon System.cs:443

### 12. Chess Mode Firing Direction Algorithm (December 2025) - UPDATED
- **Hex Coordinate Line-of-Sight System**: Uses actual hex grid coordinates instead of world-space angles
- **Algorithm**: Hex Direction Line Casting (Weapon System.cs:480-538)
  - Casts lines from pawn's hex coordinates along each of the 6 hex directions
  - Uses axial coordinate deltas from PlayerController: `HEX_DIR_Q = {1, 1, 0, -1, -1, 0}`, `HEX_DIR_R = {0, -1, -1, 0, 1, 1}`
  - Checks up to 10 tiles in each direction for alignment with player's area (player tile + 6 surrounding tiles)
  - Scores based on proximity: closer aligned tiles score higher `(11 - step)`
  - Selects direction with highest total score
  - Returns hex direction index (0-5)
- **Angle Mapping** (Weapon System.cs:423-437):
  - `GetHexDirectionAngle(hexIndex)` converts hex index to world-space angle
  - **Flat-Top Grid** (default, gun sprites point RIGHT at 0°):
    - Index 0: Top (90°)
    - Index 1: Top-right (30°)
    - Index 2: Bottom-right (-30°)
    - Index 3: Bottom (-90°)
    - Index 4: Bottom-left (-150°)
    - Index 5: Top-left (150°)
  - **X-Axis Flip**: Applied when angle > 90° and < 270° (gun appears upside-down)
- **Implementation**:
  - `GetBestAlignedHexDirection()` (lines 480-538): Line casting algorithm
  - `GetHexDirectionAngle()` (lines 423-437): Hex index to angle conversion
  - `ApplyGunRotation()` (lines 446-477): Applies rotation with X-flip
- **Debug Support**: Enable `showDebug` in WeaponSystem to see direction scores in console

### 13. Modifier Icon Canvas Cleanup (December 2025)
- **Auto-removal of Canvas child**: Pawns with no modifier automatically destroy their Canvas child GameObject
- **Implementation**: `UpdateModifierIcon()` in Pawn Controller.cs (lines 618-653)
  - Finds Canvas component in children when modifier is None
  - Destroys entire Canvas GameObject to reduce scene clutter
  - Only applies when modifier is explicitly None (not when modifier icon is missing)

### 14. Hex Helper Methods Refactoring (December 2025)
- **Centralized hex coordinate utilities**: Added public helper methods to PlayerController for hex grid operations
- **Static Constants**: `HEX_DIR_Q` and `HEX_DIR_R` arrays now public static in PlayerController
  - Index 0: Right (1,0), 1: Top-right (1,-1), 2: Top-left (0,-1)
  - Index 3: Left (-1,0), 4: Bottom-left (-1,1), 5: Bottom-right (0,1)
- **New Public Methods in PlayerController** (lines 617-680):
  - `GetHexCoords()` - Returns player's current (q, r) as Vector2Int
  - `GetAdjacentTiles()` - Returns list of 6 hex tiles adjacent to player
  - `GetPlayerArea()` - Returns player tile + 6 surrounding tiles (total 7 tiles)
  - `IsAdjacentToPlayer(int q, int r)` - Checks if coordinates are adjacent to player
- **Code Simplification**:
  - Removed duplicate `DIR_Q` and `DIR_R` arrays from PawnController
  - WeaponSystem now uses `PlayerController.HEX_DIR_Q/R` and `GetPlayerArea()`
  - PawnController now references `PlayerController.HEX_DIR_Q/R` instead of local arrays
  - All hex direction logic now centralized in one location

### 15. UI Panel Fade-In System (December 2025)
- **Panel Fade Effects**: All UI panels now fade in over 1 second when displayed
- **Implementation**: Added CanvasGroup components to all panels (Main Menu, Level Select, Game UI, Pause, Victory, Defeat, Settings)
- **Configurable Duration**: `panelFadeInDuration` field in UI Manager (default: 1.0 second)
- **FadeInPanel Coroutine**: Uses unscaled time to work during pause states
- **Initialization**: `InitializePanelCanvasGroups()` method adds CanvasGroups to all panels on startup
- **Applied to all Show Methods**: ShowMainMenu(), ShowLevelSelect(), ShowGameUI(), ShowPauseMenu(), ShowVictory(), ShowDefeat(), ShowSettings()

### 16. Follow Camera Improvements (December 2025)
- **Tag-Based Bounds Filtering**: Camera only tracks objects tagged as "Tile" or "Wall"
  - Prevents camera drift when tiles disappear during Chess → Standoff transitions
  - Implementation in `ComputeCombinedBounds()` (Follow Camera.cs:234-286)
  - Checks SpriteRenderers, PolygonCollider2Ds, and Transforms for proper tags
- **Camera Position Clamping**: Camera position clamped to 5 units radius from origin
  - Applied in `RecalculateAndApply()` after player margin constraints (Follow Camera.cs:193-201)
  - Uses Vector2.magnitude to calculate distance from origin
- **5% Zoom Out Enhancement**: Camera zooms out by additional 5% to show more background
  - Implementation: `targetSize *= 1.05f;` after size calculations (Follow Camera.cs:220)
  - Provides better visibility of game environment and background art

### 17. Pawn Fall Detection (December 2025)
- **Fall Death System**: Pawns falling below Y = -10 are automatically killed
- **Implementation**: Added Update() method to Pawn Health.cs (lines 111-132)
- **Applies to Both Types**: Works for both Player and Opponent pawns
- **Death Trigger**: Sets HP to 0, disables PawnController, calls Death()
- **Player**: Triggers defeat via GameManager
- **Opponent**: Triggers opponent death animation and physics expulsion

### 18. Weapon System Complete Refactor (December 2025)
- **Problem Solved**: Fixed inconsistent gun aiming where firearms pointed in wrong directions
  - Gun rotation now matches firing direction in both Chess and Standoff modes
- **Architecture Changes**:
  - **Vector → Angle**: Replaced direction vectors with angle-based system (float degrees)
  - **Y-flip → X-flip**: Changed rotation flip from Y-axis (180°) to X-axis (180°)
    - X-flip makes gun appear upside-down instead of mirrored
    - Prevents visual inconsistencies with sprite orientation
- **Chess Mode Gun Angles** (Flat-Top Grid):
  - Uses `GetHexDirectionAngle(hexIndex)` to map hex directions to world angles
  - Hex Index 0 (Top): 90°
  - Hex Index 1 (Top-Right): 30°
  - Hex Index 2 (Bottom-Right): -30°
  - Hex Index 3 (Bottom): -90°
  - Hex Index 4 (Bottom-Left): -150°
  - Hex Index 5 (Top-Left): 150°
  - **Default Orientation**: All pawn gun prefabs point RIGHT (0°) by default
- **Gun Rotation Logic** (Weapon System.cs:446-477):
  ```csharp
  float zAngle = currentAimAngle;
  float xRotation = 0f;

  // X-axis flip when gun points upside-down (angle > 90 and < 270)
  if (zAngle > 90f && zAngle < 270f)
  {
      xRotation = 180f;
  }

  gunTransform.rotation = Quaternion.Euler(xRotation, 0f, zAngle);
  ```
- **Shotgun Firing Pattern** (Weapon System.cs:642-647):
  - Fires 3 bullets at 0°, +60°, -60° relative to aim angle
  - **Consistent across both stages** (Chess and Standoff)
  - Example: If aiming at 120° (Top-Left), fires at 120°, 180°, 60°
- **Projectile Spawning** (Weapon System.cs:736-783):
  - `SpawnProjectile(angleOffset, damage, piercing)`
  - Calculates `finalAngle = currentAimAngle + angleOffset + fireOffset`
  - Applies X-flip if `finalAngle > 90° && < 270°`
  - Normalizes angle to 0-360° range
  - Creates bullet direction vector from finalAngle
  - Sets projectile rotation: `Quaternion.Euler(xRotation, 0f, finalAngle)`
- **Removed Fields/Methods**:
  - `hexDirections[]` array (replaced by angle calculations)
  - `InitializeHexDirections()` method (no longer needed)
  - `GetNearestHexDirection()` method (replaced by `GetHexDirectionAngle()`)
  - `GetHexDirectionVector()` method (replaced by `GetAimDirectionVector()`)
  - `GetChessModeRotation()` method (simplified rotation logic)
  - `chessStageAimOffset` field (no longer needed with correct angles)
- **Testing**:
  - Enable `showDebug = true` in WeaponSystem inspector
  - Console logs: "Chess aim: hex index X, angle Y°"
  - Console logs: "Gun rotation: Z=X.X°, X=Y°"
  - Console logs: "Spawned projectile: angle=X°, offset=Y°, X-flip=Z°"

---

---

## Recent Updates (Player Control Fixes)

### Issue Fixed: Player Control After Level Load and Pause/Resume

**Problem**: Player could lose control of their pawn after level loading and after pausing/resuming the game in both Chess and Standoff modes.

**Root Causes Identified**:
1. Input System state not properly restored after pause/resume
2. Player Controller references could become stale after level loading
3. Mobile controls visibility not properly managed during state transitions
4. Component initialization timing issues during spawning

**Solutions Implemented**:

#### 1. Enhanced Game Manager State Management
- **New Methods**:
  - `EnsurePlayerControllerState()` - Verifies and corrects player controller state
  - `EnsureInputSystemState()` - Ensures input system is properly configured
  - `EnsurePlayerControlAfterLevelLoad()` - Coroutine for post-level-load initialization

- **Enhanced Pause/Resume**:
  ```csharp
  public void ResumeGame()
  {
      Time.timeScale = 1f;
      
      // Re-enable mobile controls when resumed
      if (InputSystem.Instance != null)
      {
          InputSystem.Instance.SetMobileControlsVisibility(InputSystem.Instance.enableMobileControls);
      }
      
      // Ensure player controller is properly configured for the current mode
      EnsurePlayerControllerState();
      
      SetState(hasTransitionedToStandoff ? GameState.Standoff : GameState.ChessMode);
  }
  ```

#### 2. Input System Robustness
- **New Methods**:
  - `ResetInputState()` - Cleans up input state after level load
  - `RefreshInputSystem()` - Forces refresh of input system state

- **Enhanced Mobile Control Management**:
  ```csharp
  public void ResetInputState()
  {
      jumpInputThisFrame = false;
      
      // Ensure mobile controls are properly configured
      if (autoDetectPlatform)
      {
          // Re-detect platform and configure controls
      }
      
      SetMobileControlsVisibility(enableMobileControls);
  }
  ```

#### 3. Player Controller Initialization
- **New Method**: `EnsureProperInitialization()` - Comprehensive state verification
  - Ensures all component references are valid (camera, rigidbody, sprite renderer)
  - Resets stuck input states
  - Configures rigidbody for current mode (Kinematic/Dynamic)
  - Validates physics settings

- **Enhanced Safety Checks**:
  ```csharp
  private void Update()
  {
      // Don't update player when game is paused
      if (Time.timeScale == 0f) return;
      
      // Safety check: ensure we have required references
      if (camera == null) camera = Camera.main;
      
      // Continue with normal update logic...
  }
  ```

#### 4. Mode Setup Improvements
- **Enhanced `SetupChessMode()` and `SetupStandoffMode()`**:
  - Now call state management helpers after mode configuration
  - Ensure proper initialization when switching between modes
  - Verify input system state after mode changes

#### 5. Level Loading Process
- **Post-Load Initialization**:
  ```csharp
  public void StartGame(int levelIndex)
  {
      // Load level data
      LoadLevel(levelIndex);
      
      // Ensure proper initialization after level load
      StartCoroutine(EnsurePlayerControlAfterLevelLoad());
      
      SetState(GameState.ChessMode);
  }
  ```

- **Coroutine ensures**:
  - Waits for spawning to complete
  - Resets input system state
  - Verifies player controller configuration
  - Refreshes input system

**Benefits**:
- ✅ Player control reliably works after level loading
- ✅ Pause/resume properly restores control in both modes
- ✅ Mobile controls correctly show/hide based on game state
- ✅ Robust error recovery if components become disabled
- ✅ Enhanced debug logging for troubleshooting
- ✅ Works consistently across Chess and Standoff modes

**Debug Features Added**:
- Comprehensive logging in all state management methods
- Component state verification with warnings
- Input system state tracking
- Player controller initialization confirmation

This ensures players have reliable control of their pawn regardless of game state transitions, level loading, or pause/resume operations.