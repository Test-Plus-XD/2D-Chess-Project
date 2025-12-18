# CLAUDE.md - AI Agent Documentation

## Important Notes for AI Agents

**⚠️ CRITICAL: When reading project assets, follow these rules:**
- **ONLY read image and audio file NAMES and FOLDERS**
- **NEVER read the actual CONTENT of image files** (`.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, etc.)
- **NEVER read the actual CONTENT of audio files** (`.mp3`, `.wav`, `.ogg`, `.m4a`, etc.)
- Reading image/audio content wastes significant tokens without providing useful information
- Focus on C# scripts, prefabs, and scene files for understanding the project

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
Main Menu
    ↓
Level Selection
    ↓
Chess Mode (Hex Grid)
    → Player: Swipe to move in 6 hex directions
    → Opponents: AI-driven pawns with guns (Basic, Handcannon, Shotgun, Sniper)
    → Combat: Walk onto opponent to capture, opponents shoot at player
    ↓
(When only 1 opponent remains)
    ↓
Standoff Mode (2D Platformer)
    → Arena generation: Hexagonal platforms at varying heights
    → Player: Joystick movement + jump button
    → Opponent: AI platforming with shooting
    → Time: Slow motion when player idle (SUPERHOT style)
    → Win condition: Touch the opponent to capture
    ↓
Victory/Defeat Screen
    → Next level or retry
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

2. **Level Data.cs** - ScriptableObject for level presets (3 levels)
   - Contains Chess Mode Music and Standoff Mode Music (separate tracks per mode)
   - No MenuBGM field (menu music handled by AudioManager)
   - Player HP fields present but overridden by code (always 3 MaxHP, 2 StartHP)

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

6. **Pawn Health.cs** - Unified health system (merged PlayerPawn + OpponentPawn)
   - Handles HP, damage, death, and visual feedback for both player and opponents
   - Player always has 3 MaxHP and starts with 2 HP (enforced in Awake() and OnEnable())
   - **Separate HP sprite arrays**: Player uses 4-sprite array (0-3 HP), Opponents use variable array
   - Physics expulsion effects with camera zoom pulse on opponent death
   - Expulsion direction calculated based on board bounds (toward closer edge)

7. **Spawner.cs** - Unified spawning (merged PlayerSpawner + PawnSpawner)
   - Spawns player at bottom-right, opponents in upper tiles with weighted probability
   - References: HexGridGenerator, Checkerboard (for registration)
   - Initializes opponent HP from Level Data via `SetOpponentHP()`

8. **Weapon System.cs** - Unified weapon handling (merged Firearm + Projectile + GunAiming)
   - Fire modes: Manual, OnLineOfSight, TrackPlayer, Timed
   - Projectile types: Single, Spread, Beam
   - Includes ProjectileBehavior as nested class
   - **AI type-based firing**: Each AI type has unique firing patterns
   - **Muzzle flash rotation control**: Adjustable rotation offset for muzzle flash orientation

9. **Input System.cs** - Unified input (merged MobileInputManager + VirtualJoystick)
   - Mobile touch joystick and desktop keyboard fallback

10. **UI Manager.cs** - Unified UI (merged 6 UI scripts)
    - Main menu, level select, game HUD, pause menu, victory/defeat, settings
    - Automatic panel activation/deactivation based on game state
    - Level buttons rendered in sequential order (1, 2, 3...) with centre button 1.2x larger
    - Mobile controls automatically shown/hidden in Standoff mode
    - **Turn Indicator**: Displays "Your Turn" / "Opponent Turn" during Chess mode
    - **Announcer System**: Animated notifications with slide-in and fade-out
      - Methods: `ShowAnnouncement(string)`, `ShowOpponentDeathMessage()`, `ShowDamageTakenMessage()`, `ShowStageChangeMessage()`
      - Text in `[brackets]` automatically highlighted in vibrant orange
    - **Level Selection**: Swipeable with ScrollRect support

11. **Player Controller.cs** - Player movement in both modes
    - Chess: Swipe-based hex movement with 6 direction arrows
    - Standoff: Rigidbody2D platformer physics with ground detection
    - References: HexGridGenerator, Checkerboard (assigned at initialization)
    - Captures opponents in Standoff mode via OnCollisionEnter2D
    - Uses EnhancedTouch for reliable mobile input

12. **Pawn Controller.cs** - Opponent AI in both modes
    - Chess: Weighted directional decision-making
    - Standoff: Platformer AI with jump detection and obstacle avoidance
    - References Pawn Customiser for behavior parameters
    - **Logs warning if Pawn Customiser is null** (defaults to Basic AI behavior)
    - **Modifier icon management** via Pawn Customiser (not individual sprite fields)
    - Automatic conversion: Basic → Handcannon when last opponent enters Standoff

13. **Chequerboard.cs** - Turn-based coordination
    - Updates turn indicator via `UIManager.SetTurnIndicator(bool)`
    - Manages opponent turn sequence with firing and movement

14. **HexGrid.cs** (class: HexGridGenerator) - Procedural hex grid generation
    - Grid activation managed by GameManager.SetupChessMode()

15. **Platform.cs** - Procedural Standoff arena generation

16. **Follow Camera.cs** (class: FollowCamera) - Orthographic camera with auto-tracking
    - Auto-discovers and tracks all hex grid tiles (active tiles only)
    - Scales camera to fit all tiles with minimal border spacing
    - Zoom pulse effects on opponent defeat with kill aggregation
    - **Refreshes grid discovery** on game state changes (ChessMode, Standoff)
    - Includes Platform containers for Standoff mode bounds

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
- All pawns with firearms fire **once when their turn starts**
- All bullets destroy on hitting chess pieces unless from Sniper or affected by modifier
- Bullets damage both player and opponents

**Shooting Mechanics (Standoff Mode):**
- Interval-based firing system:
  1. **Tracking Phase**: Pawn moves based on AI type, line-of-sight follows player with angular velocity
  2. **Firing Delay**: After fire interval, stop tracking and hold position/aim for delay time (default 0.5s)
  3. **Fire**: Shoot according to AI type and modifiers
  4. **Repeat**: Restart interval until player or pawn dies
- Gun angle matches line-of-sight angle
- Fire interval and delay are adjustable per pawn and affected by modifiers

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
- Algorithm:
  1. Spawn floor tiles (6 default, with direction constraints)
  2. Generate platform 2 tiles above selected floor tile
  3. Mirror all tiles left-to-right
  4. Add 3 random connecting tiles

**Player:**
- Modified `Player Controller.cs` with platformer physics
- Rigidbody2D-based movement
- Jump mechanics with ground detection
- Mobile: Virtual joystick + jump button
- Desktop: WASD/Arrows + Space

**Opponent AI:**
- Modified `Pawn Controller.cs` with platformer AI
- Intelligent jumping (obstacles, gaps, platforms)
- Distance-based behavior:
  - Basic/Shotgun: Aggressive (always approach player)
  - Handcannon: Mid-range (maintain 2-4 unit distance)
  - Sniper: Defensive (retreat when too close)
- Edge detection to avoid falling

**Time Mechanics:**
- `TimeController.cs`: Slow motion system
- Normal time when player moves
- Slow motion (0.1x) when player idle
- Smooth transitions with adjustable speed

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
│   │   ├── Weapon System.cs    # Firearm + Projectile + GunAiming
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
- Neighbor deltas: {(1,0), (1,-1), (0,-1), (-1,0), (-1,1), (0,1)}

Conversion to world position:
FlatTop:
  x = sqrt(3) * tileSize * (q + r/2)
  y = (3/2) * tileSize * r

PointyTop:
  x = (3/2) * tileSize * q
  y = sqrt(3) * tileSize * (r + q/2)
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
1. **Script Consolidation**: 27 scripts → 15 scripts (44% reduction)
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
- `announcerSlideDistance`: 0.25 (25% of screen width, or 500px fallback)
- `announcerSlideInDuration`: 0.3s
- `announcerDisplayDuration`: 2.0s
- `announcerFadeOutDuration`: 0.5s
- `announcerHighlightColor`: Vibrant orange (#FF8000)

---

## Setup Guide

For detailed setup instructions including:
- Initial scene setup
- UI Manager configuration
- Spawner setup
- Weapon System setup
- Tags, layers, and physics configuration
- Troubleshooting

**See: [Assets/Script/SETUP_GUIDE.md](Assets/Script/SETUP_GUIDE.md)**
