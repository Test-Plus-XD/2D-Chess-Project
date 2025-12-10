# Complete Unity Scene Setup Guide
## 2D Chess-Meets-Shooter Mobile Game

This guide will walk you through setting up the entire game scene from scratch, assuming you have an empty Unity scene.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Project Setup](#project-setup)
3. [Scene Hierarchy Setup](#scene-hierarchy-setup)
4. [Creating GameObjects](#creating-gameobjects)
5. [Component Configuration](#component-configuration)
6. [Prefab Setup](#prefab-setup)
7. [Level Data Configuration](#level-data-configuration)
8. [UI Setup](#ui-setup)
9. [Input System Setup](#input-system-setup)
10. [Layer & Tag Configuration](#layer--tag-configuration)
11. [Final Testing](#final-testing)

---

## Prerequisites

### Unity Version
- **Unity 6000.4.0a4** (Unity 6 Alpha)

### Required Packages
Install these via **Window → Package Manager**:

1. **TextMeshPro**
   - Unity's UI text rendering system
   - Import TMP Essentials when prompted

2. **Input System** (new)
   - Window → Package Manager → Unity Registry
   - Search "Input System"
   - Install version 1.7.0 or later
   - **Important**: When prompted, allow Unity to restart to enable the new Input System

3. **2D Physics**
   - Should be included by default in Unity 6
   - Verify: Edit → Project Settings → Physics 2D exists

### Project Structure Verification
Ensure you have these folders in your Assets directory:
```
Assets/
├── Script/          # All 15 C# scripts
├── Prefab/          # Pawn and tile prefabs
├── Sprite/          # Visual assets
└── Audio/           # Sound effects and music
```

---

## Project Setup

### Step 1: Configure Project Settings

#### 1.1 Physics 2D Settings
1. Go to **Edit → Project Settings → Physics 2D**
2. Configure layers for collision:
   - **Layer 8**: `Ground` (for platforms and tiles)
   - **Layer 9**: `Player` (for player pawn)
   - **Layer 10**: `Opponent` (for enemy pawns)
   - **Layer 11**: `Projectile` (for bullets)

3. Set up **Layer Collision Matrix** (bottom of Physics 2D settings):
   - ✅ **Player** collides with: Ground, Opponent, Projectile
   - ✅ **Opponent** collides with: Ground, Player, Projectile
   - ✅ **Projectile** collides with: Player, Opponent, Ground
   - ❌ **Projectile** does NOT collide with: Projectile (bullets don't hit each other)

#### 1.2 Tags Configuration
1. Go to **Edit → Project Settings → Tags and Layers**
2. Add these tags (click + to add new tags):
   - `Player`
   - `Opponent`
   - `Tile`
   - `Wall`
   - `Obstacle`

#### 1.3 Input System Settings
1. Go to **Edit → Project Settings → Player**
2. Under **Active Input Handling**, select:
   - **Both** (allows old and new input systems)
   - Or **Input System Package (New)** if you only want the new system

#### 1.4 Display Settings
1. Go to **Edit → Project Settings → Player → Resolution and Presentation**
2. Set **Default Orientation** to `Portrait` or `Landscape` (your choice)
3. For mobile builds:
   - iOS: Set **Target Device** appropriately
   - Android: Configure **Minimum API Level** (API 24+ recommended)

---

## Scene Hierarchy Setup

Create a new scene (**File → New Scene → 2D**) and set up the following hierarchy:

```
Main Scene
├── Camera
│   └── Main Camera (with FollowCamera component)
│
├── Managers
│   ├── GameManager
│   ├── AudioManager
│   ├── TimeController
│   ├── InputSystem
│   └── UIManager
│
├── GameSystems
│   ├── Checkerboard
│   ├── HexGridGenerator
│   ├── SpawnerSystem
│   └── Platform
│
├── Canvas (UI)
│   ├── MainMenuPanel
│   ├── LevelSelectPanel
│   ├── GameHUDPanel
│   ├── PauseMenuPanel
│   ├── VictoryPanel
│   ├── DefeatPanel
│   ├── SettingsPanel
│   └── EventSystem
│
└── Pawns (runtime spawned)
    ├── Player Pawn (spawned at runtime)
    └── Opponent Pawns (spawned at runtime)
```

---

## Creating GameObjects

### Step 1: Create Manager Objects

#### 1.1 Create "Managers" Empty GameObject
1. **Hierarchy → Right Click → Create Empty**
2. Name it: `Managers`
3. Position: `(0, 0, 0)`

#### 1.2 Add GameManager
1. **Hierarchy → Right Click on Managers → Create Empty**
2. Name it: `GameManager`
3. **Inspector → Add Component → Game Manager** (script)
4. Leave default values for now (we'll configure later)

#### 1.3 Add AudioManager
1. Create empty child under **Managers** named `AudioManager`
2. **Add Component → Audio Manager** (script)
3. **Add Component → Audio Source** (Unity built-in)
4. Configure Audio Source:
   - ✅ **Play On Awake**: OFF
   - ✅ **Loop**: OFF (AudioManager handles this)
   - **Volume**: 1.0

#### 1.4 Add TimeController
1. Create empty child under **Managers** named `TimeController`
2. **Add Component → Time Controller** (script)
3. Configure:
   - **Slow Motion Scale**: 0.1 (10% speed)
   - **Transition Speed**: 5.0
   - **Slow Motion Enabled**: OFF (will be enabled in Standoff mode)

#### 1.5 Add InputSystem
1. Create empty child under **Managers** named `InputSystem`
2. **Add Component → Input System** (script)
3. Configure:
   - **Use Touch Input**: ON (for mobile)
   - **Show Debug**: OFF

#### 1.6 Add UIManager
1. Create empty child under **Managers** named `UIManager`
2. **Add Component → UI Manager** (script)
3. Leave empty for now (we'll assign UI panels later)

---

### Step 2: Create Game Systems

#### 2.1 Create "GameSystems" Empty GameObject
1. **Hierarchy → Right Click → Create Empty**
2. Name it: `GameSystems`
3. Position: `(0, 0, 0)`

#### 2.2 Add Checkerboard
1. Create empty child under **GameSystems** named `Checkerboard`
2. **Add Component → Checkerboard** (script)
3. Configure:
   - **Player Turn Duration**: 0.5 (seconds)
   - **Opponent Turn Duration**: 0.5 (seconds)
   - **Show Debug**: ON (helpful during testing)

#### 2.3 Add HexGridGenerator
1. Create empty child under **GameSystems** named `HexGridGenerator`
2. **Add Component → Hex Grid Generator** (script)
3. Configure:
   - **Radius**: 3
   - **Extra Rows**: 2
   - **Orientation**: FlatTop
   - **Tile Size**: 1.0
   - **Tile Prefab**: Drag **Assets/Prefab/Default Tile.prefab** here
   - **Parent Container**: Leave empty (auto-creates)
   - **Show Debug**: ON

#### 2.4 Add SpawnerSystem
1. Create empty child under **GameSystems** named `SpawnerSystem`
2. **Add Component → Spawner System** (script)
3. Configure:
   - **Grid Generator**: Drag HexGridGenerator from hierarchy
   - **Checkerboard**: Drag Checkerboard from hierarchy
   - **Player Prefab**: Drag **Assets/Prefab/Player Pawn.prefab**
   - **Basic Pawn Prefab**: Drag **Assets/Prefab/Pawn.prefab**
   - **Handcannon Prefab**: Drag **Assets/Prefab/Pawn Hand Cannon.prefab**
   - **Shotgun Prefab**: Drag **Assets/Prefab/Pawn Shotgun.prefab**
   - **Sniper Prefab**: Drag **Assets/Prefab/Pawn Sniper.prefab**
   - Counts (will be overridden by level data):
     - **Basic Count**: 2
     - **Handcannon Count**: 1
     - **Shotgun Count**: 1
     - **Sniper Count**: 1
   - **Show Debug**: ON

#### 2.5 Add Platform
1. Create empty child under **GameSystems** named `Platform`
2. **Add Component → Platform** (script)
3. Configure:
   - **Tile Size**: 1.0
   - **Floor Tiles Count**: 6
   - **Platform Expansions**: 2
   - **Tile Prefab**: Drag **Assets/Prefab/Default Tile.prefab**
   - **Show Debug**: ON
4. **Disable this GameObject** (Active checkbox OFF)
   - Platform is only active during Standoff mode
   - GameManager will enable it when needed

---

### Step 3: Configure Main Camera

#### 3.1 Camera Setup
1. Select **Main Camera** in Hierarchy
2. **Add Component → Follow Camera** (script)
3. Configure Camera component:
   - **Projection**: Orthographic
   - **Size**: 5 (adjust based on your tile size)
   - **Clipping Planes**:
     - Near: -10
     - Far: 10
4. Configure Follow Camera component:
   - **Target**: Leave empty (will auto-find player at runtime)
   - **Smooth Speed**: 5.0
   - **Offset**: (0, 2, -10)
   - **Enable Zoom Pulse**: ON
   - **Pulse Intensity**: 0.2
   - **Pulse Duration**: 0.3

---

### Step 4: Create UI Canvas

#### 4.1 Create Canvas
1. **Hierarchy → Right Click → UI → Canvas**
2. Configure Canvas component:
   - **Render Mode**: Screen Space - Overlay
   - **Pixel Perfect**: OFF
   - **Sort Order**: 0
3. Configure Canvas Scaler component:
   - **UI Scale Mode**: Scale With Screen Size
   - **Reference Resolution**: 1080 x 1920 (portrait) or 1920 x 1080 (landscape)
   - **Screen Match Mode**: Match Width Or Height
   - **Match**: 0.5 (balanced)

#### 4.2 Add EventSystem
1. Should be created automatically with Canvas
2. If not: **Hierarchy → Right Click → UI → Event System**
3. Verify components:
   - EventSystem (Unity built-in)
   - Standalone Input Module (for new Input System)

#### 4.3 Create UI Panels

Create these panels as children of Canvas (I'll provide a simplified version - you can expand based on your design):

##### **MainMenuPanel**
1. **Hierarchy → Right Click on Canvas → UI → Panel**
2. Name: `MainMenuPanel`
3. Add child: **UI → Button → TextMeshPro**
   - Name: `PlayButton`
   - Text: "Play"
4. Add child: **UI → Button → TextMeshPro**
   - Name: `SettingsButton`
   - Text: "Settings"
5. Add child: **UI → Button → TextMeshPro**
   - Name: `QuitButton`
   - Text: "Quit"

##### **LevelSelectPanel**
1. Create Panel: `LevelSelectPanel`
2. **Disable by default** (uncheck Active)
3. Add children:
   - **UI → Button → TextMeshPro** (Name: `Level1Button`, Text: "Level 1")
   - **UI → Button → TextMeshPro** (Name: `Level2Button`, Text: "Level 2")
   - **UI → Button → TextMeshPro** (Name: `Level3Button`, Text: "Level 3")
   - **UI → Button → TextMeshPro** (Name: `BackButton`, Text: "Back")

##### **GameHUDPanel**
1. Create Panel: `GameHUDPanel`
2. **Disable by default**
3. Add children:
   - **UI → Text → TextMeshPro** (Name: `HPText`, Text: "HP: 5/5")
   - **UI → Text → TextMeshPro** (Name: `LevelText`, Text: "Level 1")
   - **UI → Button → TextMeshPro** (Name: `PauseButton`, Text: "||")

##### **PauseMenuPanel**
1. Create Panel: `PauseMenuPanel`
2. **Disable by default**
3. Add children:
   - **UI → Button → TextMeshPro** (Name: `ResumeButton`, Text: "Resume")
   - **UI → Button → TextMeshPro** (Name: `RestartButton`, Text: "Restart")
   - **UI → Button → TextMeshPro** (Name: `MainMenuButton`, Text: "Main Menu")

##### **VictoryPanel**
1. Create Panel: `VictoryPanel`
2. **Disable by default**
3. Add children:
   - **UI → Text → TextMeshPro** (Name: `VictoryText`, Text: "Victory!")
   - **UI → Button → TextMeshPro** (Name: `NextLevelButton`, Text: "Next Level")
   - **UI → Button → TextMeshPro** (Name: `MainMenuButton`, Text: "Main Menu")

##### **DefeatPanel**
1. Create Panel: `DefeatPanel`
2. **Disable by default**
3. Add children:
   - **UI → Text → TextMeshPro** (Name: `DefeatText`, Text: "Defeat")
   - **UI → Button → TextMeshPro** (Name: `RetryButton`, Text: "Retry")
   - **UI → Button → TextMeshPro** (Name: `MainMenuButton`, Text: "Main Menu")

##### **SettingsPanel**
1. Create Panel: `SettingsPanel`
2. **Disable by default**
3. Add children:
   - **UI → Text → TextMeshPro** (Name: `SettingsTitle`, Text: "Settings")
   - **UI → Slider** (Name: `MusicVolumeSlider`)
   - **UI → Slider** (Name: `SFXVolumeSlider`)
   - **UI → Button → TextMeshPro** (Name: `BackButton`, Text: "Back")

#### 4.4 Mobile Controls (Standoff Mode)

Create mobile controls as children of Canvas:

##### **Virtual Joystick**
1. Create Panel: `VirtualJoystick`
2. **Disable by default** (enabled automatically in Standoff mode)
3. Position: Bottom-left of screen
4. Add children:
   - **UI → Image** (Name: `JoystickBackground`)
     - Source Image: Circle sprite
     - Color: White with low alpha (50)
     - Size: 200x200
   - **UI → Image** (Name: `JoystickHandle`)
     - Parent it under JoystickBackground
     - Source Image: Circle sprite
     - Color: White with higher alpha (150)
     - Size: 100x100

##### **Jump Button**
1. **UI → Button → TextMeshPro**
2. Name: `JumpButton`
3. **Disable by default**
4. Position: Bottom-right of screen
5. Text: "JUMP"
6. Size: 150x150 (large enough for thumb)

---

### Step 5: Connect UI to UIManager

1. Select **UIManager** GameObject in Hierarchy
2. In **Inspector → UI Manager** component, assign all UI panels:
   - **Main Menu Panel**: Drag MainMenuPanel
   - **Level Select Panel**: Drag LevelSelectPanel
   - **Game HUD Panel**: Drag GameHUDPanel
   - **Pause Menu Panel**: Drag PauseMenuPanel
   - **Victory Panel**: Drag VictoryPanel
   - **Defeat Panel**: Drag DefeatPanel
   - **Settings Panel**: Drag SettingsPanel
   - **Virtual Joystick**: Drag VirtualJoystick
   - **Jump Button**: Drag JumpButton

3. Assign UI text elements (for dynamic updates):
   - **HP Text**: Drag HPText from GameHUDPanel
   - **Level Text**: Drag LevelText from GameHUDPanel

4. Assign buttons and hook up events (in Inspector):
   - **PlayButton**: OnClick() → GameManager.OpenLevelSelect
   - **Level1Button**: OnClick() → GameManager.StartGame(0)
   - **Level2Button**: OnClick() → GameManager.StartGame(1)
   - **Level3Button**: OnClick() → GameManager.StartGame(2)
   - **PauseButton**: OnClick() → GameManager.PauseGame
   - **ResumeButton**: OnClick() → GameManager.ResumeGame
   - **RestartButton**: OnClick() → GameManager.ReloadCurrentLevel
   - **JumpButton**: OnClick() → InputSystem.OnJumpButtonPressed

---

## Component Configuration

### Step 1: Configure GameManager

1. Select **GameManager** in Hierarchy
2. Assign references in Inspector:
   - **Checkerboard**: Drag Checkerboard GameObject
   - **Platform Generator**: Drag Platform GameObject
   - **Player Controller**: Leave empty (auto-finds at runtime)
   - **Grid Generator**: Drag HexGridGenerator GameObject
   - **Spawner System**: Drag SpawnerSystem GameObject
3. Configure settings:
   - **Standoff Transition Delay**: 1.5 seconds
   - **Standoff Trigger Count**: 1 (when 1 opponent remains)
4. Assign Level Data (we'll create these next):
   - **Levels** array size: 3
   - **Levels[0]**: Drag Level1Data (create next)
   - **Levels[1]**: Drag Level2Data (create next)
   - **Levels[2]**: Drag Level3Data (create next)

### Step 2: Connect Systems

#### 2.1 Checkerboard Connections
1. Select **Checkerboard** GameObject
2. Auto-finds references at runtime, but you can manually assign:
   - **Grid Generator**: Drag HexGridGenerator
   - **Game Manager**: Drag GameManager

#### 2.2 SpawnerSystem Connections
Already configured in Step 2.4 above.

---

## Prefab Setup

### Step 1: Verify Existing Prefabs

Check that these prefabs exist in **Assets/Prefab/**:

#### Default Tile.prefab
- **Components**:
  - SpriteRenderer (hex tile sprite)
  - PolygonCollider2D (auto-generated from sprite)
  - Tag: `Tile`
  - Layer: `Ground`

#### Player Pawn.prefab
- **Components**:
  - SpriteRenderer (player sprite)
  - CircleCollider2D or PolygonCollider2D
  - Rigidbody2D (Kinematic for Chess mode, Dynamic for Standoff)
  - **Player Controller** (script)
  - **PawnHealth** (script)
    - Pawn Type: Player
    - Max HP: 5 (configurable per level)
  - Tag: `Player`
  - Layer: `Player`
- **Children**:
  - 6 arrow GameObjects (for direction indicators)
    - Name them: `Arrow_0` through `Arrow_5`
    - Position them in a circle around the pawn
    - Assign to **Player Controller → Direction Arrows** array

#### Pawn.prefab (Basic)
- **Components**:
  - SpriteRenderer (basic pawn sprite)
  - CircleCollider2D or PolygonCollider2D
  - Rigidbody2D (Kinematic for Chess, Dynamic for Standoff)
  - **Pawn Controller** (script)
    - AI Type: Basic
    - Modifier: None
  - **PawnHealth** (script)
    - Pawn Type: Opponent
    - Max HP: 1
  - Tag: `Opponent`
  - Layer: `Opponent`

#### Pawn Hand Cannon.prefab
- Same as Pawn.prefab, but:
  - **Pawn Controller**:
    - AI Type: Handcannon
  - **WeaponSystem** (script):
    - Fire Mode: TrackPlayer
    - Projectile Type: Single
    - Damage: 1
    - Fire Interval: 3.0
    - Firing Delay: 0.5
    - Projectile Prefab: Drag Projectile prefab
  - **Children**:
    - `Gun` GameObject (visual representation)
      - Position: Offset from pawn center (0.2, 0)
    - `FirePoint` GameObject (spawn point for bullets)
      - Position: At gun tip

#### Pawn Shotgun.prefab
- Same as Handcannon, but:
  - **Pawn Controller**:
    - AI Type: Shotgun
  - **WeaponSystem**:
    - Spread Count: 3
    - Spread Angle: 60

#### Pawn Sniper.prefab
- Same as Handcannon, but:
  - **Pawn Controller**:
    - AI Type: Sniper
  - **WeaponSystem**:
    - Damage: 2
    - Fire Interval: 4.0

### Step 2: Create Projectile Prefab

If not already created:

1. **Hierarchy → Right Click → 2D Object → Sprite**
2. Name: `Projectile`
3. Configure:
   - **Sprite**: Small circle or bullet sprite
   - **Color**: Yellow or white
   - **Scale**: (0.2, 0.2, 1)
4. **Add Component → Circle Collider 2D**:
   - Is Trigger: ON
   - Radius: 0.1
5. **Add Component → Rigidbody2D**:
   - Body Type: Dynamic
   - Gravity Scale: 0
   - Collision Detection: Continuous
6. **Add Component → Projectile Behavior** (auto-added by WeaponSystem)
7. Set Layer: `Projectile`
8. Drag to **Assets/Prefab/** to create prefab
9. Delete from scene

### Step 3: Assign Projectile to Weapon Systems

1. Open each weapon-wielding pawn prefab
2. Select the prefab in Hierarchy
3. Find **WeaponSystem** component
4. Assign:
   - **Projectile Prefab**: Drag Projectile prefab
   - **Fire Point**: Drag FirePoint child GameObject

---

## Level Data Configuration

### Step 1: Create Level Data Assets

1. **Project → Assets → Right Click → Create → Game → Level Data**
2. Name it: `Level1Data`
3. Repeat for `Level2Data` and `Level3Data`

### Step 2: Configure Level 1

Select **Level1Data** in Project, configure in Inspector:

#### Grid Settings
- **Level Name**: "Level 1"
- **Grid Radius**: 3
- **Extra Rows**: 2
- **Tile Size**: 1.0

#### Opponent Configuration
- **Basic Pawn Count**: 3
- **Handcannon Count**: 1
- **Shotgun Count**: 1
- **Sniper Count**: 0

#### Player Configuration
- **Starting HP**: 5
- **Max HP**: 5

#### Platform Settings (Standoff Mode)
- **Floor Tiles**: 6
- **Platform Expansions**: 2

#### Difficulty Settings
- **Fire Rate**: 3.0
- **Detection Range**: 10.0

#### Visual Settings
- **Background Music**: Drag your music AudioClip here (optional)
- **Skybox**: Drag skybox material here (optional)

### Step 3: Configure Level 2

Similar to Level 1, but increase difficulty:
- **Basic Pawn Count**: 2
- **Handcannon Count**: 2
- **Shotgun Count**: 1
- **Sniper Count**: 1
- **Fire Rate**: 2.5 (faster)
- **Starting HP**: 4 (lower HP)

### Step 4: Configure Level 3

Hardest difficulty:
- **Basic Pawn Count**: 1
- **Handcannon Count**: 2
- **Shotgun Count**: 2
- **Sniper Count**: 1
- **Fire Rate**: 2.0 (very fast)
- **Starting HP**: 3 (low HP)

### Step 5: Assign Levels to GameManager

1. Select **GameManager** in Hierarchy
2. In Inspector, expand **Levels** array
3. Drag level data assets:
   - **Element 0**: Level1Data
   - **Element 1**: Level2Data
   - **Element 2**: Level3Data

---

## Input System Setup

### Step 1: Configure InputSystem Component

1. Select **InputSystem** GameObject in Hierarchy
2. Configure in Inspector:
   - **Use Touch Input**: ON (for mobile)
   - **Show Debug**: OFF (turn ON for testing)

### Step 2: Mobile Controls Setup

InputSystem automatically handles:
- Virtual joystick for movement (Standoff mode)
- Jump button (Standoff mode)
- Touch swipes (Chess mode)
- Desktop keyboard fallback (WASD/Arrows + Space)

No additional setup required!

---

## Layer & Tag Configuration

### Tags (Edit → Project Settings → Tags and Layers)

Create these tags:
- `Player`
- `Opponent`
- `Tile`
- `Wall`
- `Obstacle`
- `Projectile`

### Layers

Create these layers:
- **Layer 8**: `Ground` (tiles, platforms)
- **Layer 9**: `Player` (player pawn)
- **Layer 10**: `Opponent` (enemy pawns)
- **Layer 11**: `Projectile` (bullets)

### Layer Collision Matrix (Physics 2D Settings)

Configure collisions (✅ = collide, ❌ = ignore):

|              | Ground | Player | Opponent | Projectile |
|--------------|--------|--------|----------|------------|
| **Ground**   | ✅     | ✅     | ✅       | ✅         |
| **Player**   | ✅     | ❌     | ✅       | ✅         |
| **Opponent** | ✅     | ✅     | ❌       | ✅         |
| **Projectile**| ✅    | ✅     | ✅       | ❌         |

---

## Final Testing

### Step 1: Verify Scene Setup

**Checklist:**
- ✅ All manager GameObjects exist and have components
- ✅ GameSystems are configured with references
- ✅ UI Canvas is set up with all panels
- ✅ Prefabs exist and are properly configured
- ✅ Level data assets are created and assigned
- ✅ Camera has FollowCamera component
- ✅ Tags and Layers are configured
- ✅ Collision matrix is set up

### Step 2: Play Mode Test (Chess Mode)

1. **Press Play** in Unity Editor
2. You should see:
   - Main menu appears
   - Click "Play" → Level Select appears
   - Click "Level 1" → Chess mode starts
3. Verify:
   - ✅ Hexagonal grid generates
   - ✅ Player spawns at bottom-right
   - ✅ Opponents spawn at top
   - ✅ Swipe to move player
   - ✅ Arrows show during swipe
   - ✅ Opponents take turns after player
   - ✅ Shooting works for armed opponents
   - ✅ HP decreases when hit
   - ✅ Capturing opponents works (walk onto them)

### Step 3: Play Mode Test (Standoff Mode)

1. Continue playing until only 1 opponent remains
2. Verify transition:
   - ✅ "Transitioning to Standoff mode..." message
   - ✅ Arena generates with platforms
   - ✅ Player and opponent repositioned
   - ✅ Chess grid disappears
   - ✅ Virtual joystick appears (mobile)
   - ✅ Jump button appears
3. Test Standoff mechanics:
   - ✅ Movement with joystick/WASD
   - ✅ Jump works
   - ✅ Slow motion activates when idle
   - ✅ Opponent AI moves and shoots
   - ✅ Touching opponent triggers victory
   - ✅ Victory screen appears

### Step 4: Test UI Flow

1. **Main Menu**:
   - ✅ Play button works
   - ✅ Settings button opens settings
   - ✅ Quit button works (in build)

2. **Level Select**:
   - ✅ All 3 level buttons work
   - ✅ Back button returns to main menu

3. **Game HUD**:
   - ✅ HP displays correctly
   - ✅ Updates when damaged
   - ✅ Level name shows

4. **Pause Menu**:
   - ✅ Pause button works
   - ✅ Time stops when paused
   - ✅ Resume works
   - ✅ Restart works
   - ✅ Main Menu button works

5. **Victory/Defeat**:
   - ✅ Victory shows when opponent captured
   - ✅ Defeat shows when player dies
   - ✅ Next Level button works
   - ✅ Retry button works

### Step 5: Test Mobile Controls (Optional)

1. **Build → Build Settings**
2. Switch platform to **Android** or **iOS**
3. Build and run on device
4. Verify:
   - ✅ Touch input works
   - ✅ Virtual joystick responsive
   - ✅ Jump button works
   - ✅ Swipes work in Chess mode

---

## Troubleshooting

### Common Issues

#### 1. "NullReferenceException" errors
- **Cause**: Missing references in Inspector
- **Fix**: Check all manager GameObjects and ensure references are assigned
- Look for red text in Console showing which component is missing

#### 2. Grid doesn't generate
- **Cause**: Missing tile prefab or HexGridGenerator not configured
- **Fix**:
  - Verify **Tile Prefab** is assigned in HexGridGenerator
  - Check Console for errors
  - Enable **Show Debug** in HexGridGenerator

#### 3. Player can't move
- **Cause**: Checkerboard not detecting player turn
- **Fix**:
  - Verify PlayerController has **Initialise()** called by SpawnerSystem
  - Check **IsPlayerTurn()** in Checkerboard component
  - Enable **Show Debug** in Checkerboard

#### 4. Opponents don't spawn
- **Cause**: Missing prefabs or SpawnerSystem misconfigured
- **Fix**:
  - Verify all 4 opponent prefabs are assigned in SpawnerSystem
  - Check level data has opponent counts > 0
  - Enable **Show Debug** in SpawnerSystem

#### 5. Shooting doesn't work
- **Cause**: WeaponSystem missing projectile prefab
- **Fix**:
  - Open opponent prefabs
  - Check **WeaponSystem → Projectile Prefab** is assigned
  - Verify **FirePoint** child exists

#### 6. Standoff mode doesn't trigger
- **Cause**: GameManager not detecting opponent count
- **Fix**:
  - Verify **Standoff Trigger Count** is 1 in GameManager
  - Check Checkerboard is tracking opponents correctly
  - Enable **Show Debug** in GameManager

#### 7. UI doesn't show
- **Cause**: UIManager missing panel references
- **Fix**:
  - Select UIManager GameObject
  - Verify all panel fields are assigned
  - Check Canvas is set to "Screen Space - Overlay"

#### 8. Input doesn't respond
- **Cause**: Input System not enabled or misconfigured
- **Fix**:
  - Edit → Project Settings → Player → Active Input Handling = "Both"
  - Verify InputSystem GameObject exists
  - Check EventSystem exists in Canvas

#### 9. Collisions don't work
- **Cause**: Layer collision matrix misconfigured
- **Fix**:
  - Edit → Project Settings → Physics 2D
  - Check Layer Collision Matrix at bottom
  - Verify prefabs have correct layers assigned

#### 10. Slow motion doesn't work in Standoff
- **Cause**: TimeController not enabled or player movement not detected
- **Fix**:
  - Verify TimeController GameObject exists
  - Check **SetSlowMotionEnabled(true)** is called by GameManager
  - Test by standing still in Standoff mode

---

## Optional Enhancements

### Modifiers Setup

To enable opponent modifiers (Tenacious, Confrontational, Fleet, Observant, Reflexive):

1. **Create Modifier Icons**:
   - Create 5 sprite icons (shield, sword, wing, eye, crosshair)
   - Place in **Assets/Sprite/Modifiers/**

2. **Assign to Pawn Prefabs**:
   - Open each opponent prefab
   - In **PawnController** component:
     - Assign modifier icon sprites
   - Add UI Image child:
     - Name: `ModifierIcon`
     - Position: Top-right of pawn
     - Assign to **Modifier Icon Image** field

3. **Random Modifier Assignment** (optional):
   - Edit **SpawnerSystem.cs**
   - In `SpawnOpponent()` method, add random modifier logic:
   ```csharp
   if (Random.value > 0.5f) // 50% chance
   {
       pawnController.SetModifier((PawnController.Modifier)Random.Range(1, 6));
   }
   ```

### Audio Setup

1. **Add Music**:
   - Import audio files to **Assets/Audio/**
   - Create 3 music tracks for levels
   - Assign to Level Data assets

2. **Add Sound Effects**:
   - Import SFX (shoot, jump, capture, hit)
   - Assign to WeaponSystem, PlayerController, PawnHealth components

---

## Build & Deployment

### Building for PC/Mac/Linux

1. **File → Build Settings**
2. Select platform: **PC, Mac & Linux Standalone**
3. **Add Open Scenes** (adds current scene)
4. **Player Settings**:
   - Company Name
   - Product Name: "2D Chess Shooter"
   - Version: 0.1.0
5. **Build** → Choose output folder

### Building for Mobile (Android)

1. **File → Build Settings**
2. **Switch Platform** to **Android**
3. **Player Settings**:
   - **Company Name**: Your name
   - **Product Name**: "2D Chess Shooter"
   - **Package Name**: com.yourcompany.chessshoter
   - **Version**: 0.1.0
   - **Minimum API Level**: API 24 (Android 7.0)
   - **Orientation**: Portrait or Landscape
4. **Build** or **Build and Run**

### Building for Mobile (iOS)

1. **File → Build Settings**
2. **Switch Platform** to **iOS**
3. **Player Settings**:
   - Same as Android
   - **Bundle Identifier**: com.yourcompany.chessshoter
4. **Build** → Creates Xcode project
5. Open in Xcode and build to device

---

## Summary Checklist

Before considering setup complete, verify:

- ✅ All 15 scripts are in **Assets/Script/**
- ✅ All prefabs exist in **Assets/Prefab/**
- ✅ Scene hierarchy matches the structure above
- ✅ All manager GameObjects have components
- ✅ All GameSystems are configured
- ✅ UI Canvas is fully set up
- ✅ 3 Level Data assets are created and assigned
- ✅ Tags and Layers are configured
- ✅ Collision matrix is set up
- ✅ Camera has FollowCamera component
- ✅ InputSystem is enabled in Project Settings
- ✅ TextMeshPro is imported
- ✅ Play mode works without errors
- ✅ Chess mode gameplay functions
- ✅ Standoff mode transition works
- ✅ UI navigation functions
- ✅ Mobile controls work (if building for mobile)

---

## Next Steps

1. **Art & Polish**:
   - Replace placeholder sprites with final art
   - Add particle effects (muzzle flash, impact, death)
   - Add animations (idle, walk, shoot, death)

2. **Sound Design**:
   - Add background music for each level
   - Add SFX for all actions
   - Implement audio mixing

3. **Level Design**:
   - Create more levels (expand beyond 3)
   - Design difficulty curve
   - Add special challenge levels

4. **Save System**:
   - Implement level unlocking
   - Save player progress
   - Track high scores

5. **Playtesting**:
   - Test on target devices
   - Balance difficulty
   - Fix bugs and edge cases

---

## Support & Resources

- **Unity Manual**: https://docs.unity3d.com/Manual/index.html
- **Unity Scripting API**: https://docs.unity3d.com/ScriptReference/
- **Input System Documentation**: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/index.html
- **TextMeshPro Documentation**: https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html

---

**Setup Guide Version**: 1.0
**Last Updated**: 2025-12-10
**Compatible Unity Version**: 6000.4.0a4
**Total Setup Time**: ~2-3 hours (for experienced developers)

Good luck with your 2D Chess-Meets-Shooter game! 🎮
