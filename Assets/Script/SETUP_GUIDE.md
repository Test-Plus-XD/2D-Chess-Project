# Setup Guide

This guide provides step-by-step instructions for setting up the 2D Chess-Meets-Shooter game in Unity.

---

## Initial Scene Setup

### 1. Create Core GameObjects

```
Main Scene
├── GameManager (Game Manager.cs)
├── AudioManager (Audio Manager.cs)
├── InputManager (Input System.cs)
├── TimeController (Time Controller.cs)
├── Checkerboard (Chequerboard.cs)
├── HexGrid (HexGridGenerator) [disabled initially]
│   └── Tiles container
├── PlatformGenerator (Platform.cs) [disabled initially]
├── Spawner (Spawner.cs)
├── Main Camera (Follow Camera.cs)
└── UI Canvas (UI Manager.cs)
```

### 2. Configure GameManager

- Create LevelData ScriptableObjects (Right-click → Create → Game → Level Data)
- Add to `GameManager.levels[]` array
- Assign references: Checkerboard, HexGridGenerator, PlatformGenerator

### 3. Configure UI Canvas

- Create UI panels: MainMenu, LevelSelect, GameUI, Pause, Victory, Defeat, Settings
- Assign all panel references to UIManager

---

## UI Manager Setup

### Game UI Panel Setup

```
Game UI Panel
├── Level Text (TextMeshProUGUI) → levelText
├── Pause Button (Button) → pauseButton
├── Turn Indicator Text (TextMeshProUGUI) → turnIndicatorText
└── Announcer Panel (RectTransform, anchored top-right) → announcerPanel
    └── Announcer Text (TextMeshProUGUI) → announcerText
```

### Announcer Panel Configuration

1. Create Panel at top-right anchor (pivot: 1, 1)
2. Add CanvasGroup component (auto-added if missing)
3. Add TextMeshProUGUI child
4. Position off-screen to the right (will animate in)

### Level Select Panel Setup

```
Level Select Panel
├── Title Text
├── Scroll View (optional, for swipe support)
│   └── Viewport
│       └── Content (levelButtonContainer with HorizontalLayoutGroup)
└── Back Button
```

---

## Spawner Setup

### 1. Assign References

- `gridGenerator`: HexGridGenerator
- `checkerboard`: Checkerboard
- `playerPawnPrefab`: Player prefab
- `opponentSpawnParent`: Parent transform for pawns

### 2. Assign Opponent Prefabs

- `pawnPrefab`: Basic opponent
- `handcannonPrefab`: Handcannon opponent
- `shotgunPrefab`: Shotgun opponent
- `sniperPrefab`: Sniper opponent

---

## Weapon System Setup

### Inspector Fields

- `muzzleFlashRotationOffset`: Rotation offset in degrees (e.g., -90 if flash is vertical)
- `muzzleFlashDuration`: How long flash displays (default 0.1s)
- `projectilePrefab`: Bullet prefab
- `firePoint`: Transform where bullets spawn

---

## Tags, Layers, and Physics Configuration

### Required Tags

Create these tags in Unity (Edit → Project Settings → Tags and Layers):

| Tag | Used By | Purpose |
|-----|---------|---------|
| `Player` | Player Pawn | Identifies player for opponent AI targeting |
| `Tile` | Hex tiles | Projectile collision detection |
| `Wall` | Platform walls | Projectile collision detection |
| `Obstacle` | Obstacles | Projectile collision and AI navigation |

### Required Layers

Create these layers:

| Layer # | Name | Purpose |
|---------|------|---------|
| 6 | `Ground` | Ground detection for pawns |
| 7 | `Player` | Player collision layer |
| 8 | `Opponent` | Opponent collision layer |
| 9 | `Projectile` | Bullet collision layer |
| 10 | `Tile` | Hex tile collision layer |
| 11 | `Wall` | Wall/obstacle collision layer |

### Layer Mask Configuration

**PawnController Inspector:**
- `groundLayer`: Ground (layer 6)

**WeaponSystem Inspector:**
- `targetLayer`: Player | Opponent (layers 7, 8)
- `obstacleLayer`: Wall | Tile (layers 10, 11)

---

## Physics 2D Settings

Configure in Edit → Project Settings → Physics 2D:

### Layer Collision Matrix

```
             Player  Opponent  Projectile  Tile  Wall  Ground
Player         -        ✓         ✓         ✓     ✓      ✓
Opponent       ✓        -         ✓         ✓     ✓      ✓
Projectile     ✓        ✓         -         ✓     ✓      -
Tile           ✓        ✓         ✓         -     -      -
Wall           ✓        ✓         ✓         -     -      -
Ground         ✓        ✓         -         -     -      -
```

### Recommended Settings

- Gravity: Y = -9.81 (or -20 for faster falling)
- Default Contact Offset: 0.01
- Velocity Iterations: 8
- Position Iterations: 3

---

## Prefab Layer Assignment

| Prefab | Layer | Tag |
|--------|-------|-----|
| Player Pawn | Player | Player |
| Basic Pawn | Opponent | - |
| Handcannon Pawn | Opponent | - |
| Shotgun Pawn | Opponent | - |
| Sniper Pawn | Opponent | - |
| Hex Tile | Tile | Tile |
| Platform Tile | Tile | Tile |
| Bullet | Projectile | - |

---

## Rigidbody2D Configuration

### Player Pawn

- Body Type: Dynamic (Standoff) / Kinematic (Chess)
- Gravity Scale: 2 (Standoff only)
- Freeze Rotation: Z = true
- Collision Detection: Continuous

### Opponent Pawn

- Body Type: Dynamic (Standoff) / Kinematic (Chess)
- Gravity Scale: 2 (Standoff only)
- Freeze Rotation: Z = true
- Collision Detection: Continuous

### Projectile

- Body Type: Dynamic
- Gravity Scale: 0
- Is Trigger: true (on Collider2D)

---

## Collider2D Setup

### Pawn Colliders

- CircleCollider2D or CapsuleCollider2D
- Is Trigger: false (for physical collision)

### Projectile Colliders

- CircleCollider2D (small radius)
- Is Trigger: true (for OnTriggerEnter2D detection)

### Tile Colliders

- PolygonCollider2D (matches hex shape)
- Is Trigger: false

---

## Troubleshooting

### Camera Not Following Tiles

- **Cause**: Tiles disabled when camera initializes
- **Fix**: Camera now refreshes on game state change (automatic)
- **Manual fix**: Call `FollowCamera.Instance.ForceRecalculate()`

### Pawns Spawning at Origin

- **Cause**: Grid not generated when spawning occurs
- **Fix**: Spawner now waits for grid generation (automatic)
- **Manual fix**: Ensure HexGridGenerator is active before spawning

### Music Not Playing

- **Cause**: Level data missing music clips
- **Fix**: Assign ChessModeMusic and StandoffModeMusic in LevelData
- **Fallback**: Menu music plays if level music is missing

### Turn Indicator Not Showing

- **Cause**: turnIndicatorText not assigned in UIManager
- **Fix**: Create TextMeshProUGUI in GameUI panel and assign to UIManager

### Announcer Not Animating

- **Cause**: Missing RectTransform or CanvasGroup
- **Fix**: Ensure announcerPanel has RectTransform (auto-added CanvasGroup)
