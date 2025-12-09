# 2D Chess-Meets-Shooter Mobile Game

A tactical 2D mobile game combining hexagonal chess mechanics with side-scrolling platformer action and SUPERHOT-style time manipulation.

![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)
![Platform](https://img.shields.io/badge/Platform-Mobile%20%7C%20Desktop-blue)
![License](https://img.shields.io/badge/License-Educational-green)

---

## 🎮 Game Overview

Experience a unique blend of strategic chess and fast-paced action in this 2D mobile game prototype.

### **Chess Mode**
- Move your pawn on a hexagonal grid
- Face AI opponents with different behaviors (Basic, Handcannon, Shotgun, Sniper)
- Capture enemies by moving onto their tiles
- Avoid enemy fire from their directional weapons

### **Standoff Mode** (SUPERHOT-Inspired)
- When only one opponent remains, the game transforms into a 2D side-scroller
- Time slows when you stop moving (SUPERHOT mechanic)
- Navigate procedurally generated hex-based platforms
- Touch the enemy to win!

---

## ✨ Key Features

### 🎯 Dual Game Modes
- **Chess Mode**: Turn-based tactical movement on hexagonal grid
- **Standoff Mode**: Real-time 2D platformer with time manipulation

### 🤖 Smart AI
- **4 AI Types**: Basic (aggressive), Handcannon (close-range), Shotgun (medium-range), Sniper (long-range)
- **Intelligent Behavior**: Chess mode uses weighted decision-making
- **Platformer AI**: Opponents can jump over obstacles, avoid edges, and maintain optimal distance

### 🎨 Dynamic Gameplay
- **SUPERHOT Time Mechanics**: Time flows normally when moving, slows when idle
- **Procedural Arena Generation**: Each Standoff stage is uniquely generated with symmetrical platforms
- **Health System**: Visual HP representation with sprite swapping

### 📱 Mobile-First Design
- **Virtual Joystick**: Smooth on-screen controls
- **Swipe Movement**: Intuitive gesture-based controls in Chess mode
- **Responsive UI**: Adapts to different screen sizes
- **Desktop Support**: Keyboard/mouse fallback for testing

### 🎵 Complete Audio System
- Background music with fade transitions
- Sound effects for movement, shooting, and victories
- Volume controls (Master, Music, SFX)

### 🏆 3 Challenging Levels
- Progressive difficulty
- Configurable via ScriptableObjects
- Different opponent compositions per level

---

## 🕹️ Controls

### Chess Mode
| Input | Action |
|-------|--------|
| **Touch/Swipe** | Move pawn in swipe direction |
| **Mouse (Desktop)** | Click and drag to move |

### Standoff Mode
| Input | Action |
|-------|--------|
| **Virtual Joystick** | Move left/right |
| **Jump Button** | Jump |
| **WASD/Arrows (Desktop)** | Move |
| **Space (Desktop)** | Jump |

### Universal
| Input | Action |
|-------|--------|
| **ESC** | Pause game |

---

## 🚀 Getting Started

### Prerequisites
- Unity 2021.3 or later
- TextMeshPro package
- Unity Input System (new)
- 2D Physics package

### Installation

1. **Clone the Repository**
```bash
git clone https://github.com/Test-Plus-XD/2D-Chess-Project.git
cd 2D-Chess-Project
```

2. **Open in Unity**
- Launch Unity Hub
- Click "Add" and select the project folder
- Open the project with Unity 2021.3+

3. **Install Required Packages**
- Unity will auto-import required packages
- If prompted, import TextMeshPro essentials

4. **Play**
- Open `Main Scene.unity`
- Press Play in Unity Editor

---

## 🎮 How to Play

### Starting a Game
1. Launch the game from Main Menu
2. Select a level (1-3)
3. Game starts in Chess Mode

### Chess Mode Tips
- **Swipe** in the direction you want to move
- **Watch enemy patterns** - each AI type behaves differently
- **Plan ahead** - enemies take turns after you move
- **Capture smartly** - walk onto enemy tiles to capture them

### Standoff Mode Tips
- **Keep moving** to maintain normal time speed
- **Stop moving** to activate slow motion for precise dodging
- **Jump carefully** - platforming is key
- **Touch the enemy** to win

---

## 📊 Project Structure

```
Assets/
├── Script/               # All C# scripts (25+ files)
│   ├── Core/            # Managers and systems
│   ├── Chess/           # Chess mode controllers
│   ├── Standoff/        # Standoff mode systems
│   ├── UI/              # All UI screens
│   └── Input/           # Mobile input system
│
├── Prefab/              # Game object prefabs
│   ├── Tiles/
│   └── Pawns/
│
├── Sprite/              # Visual assets
├── Audio/               # Sound effects and music
└── Main Scene.unity     # Main gameplay scene
```

---

## 🎯 Technical Highlights

### Core Systems
- **Game State Manager**: Handles flow between menus, Chess, and Standoff modes
- **Level Manager**: Configures and loads levels from ScriptableObjects
- **Time Controller**: Manages SUPERHOT-style time manipulation
- **Audio Manager**: Centralized sound and music management

### Advanced Features
- Hexagonal grid with axial coordinates
- Weighted AI decision-making
- Procedural platform generation
- Real-time physics-based platforming
- Mobile-first input system

---

## 📚 Documentation

- **CLAUDE.md**: Comprehensive technical documentation for AI agents and developers
- **README.md**: This file - project overview and user guide
- **Inline Comments**: Extensive code documentation throughout all scripts

---

## 📝 Credits & Inspiration

### Game Inspirations
- **Shotgun King: The Final Checkmate**: Chess + shooting mechanics
- **Fights in Tight Spaces**: Enemy modifiers and tactical gameplay
- **SUPERHOT**: Time manipulation mechanics
- **Terraria**: Side-scrolling platformer style

---

## 📄 License

This project is created for educational purposes as part of a mobile game development course.

---

## 📞 Contact

For questions or feedback about this project:
- **Repository**: [2D-Chess-Project](https://github.com/Test-Plus-XD/2D-Chess-Project)
- **Issues**: Use GitHub Issues for bug reports

---

**Built with ❤️ for 2D Mobile Game Development**

*Last Updated: 2025-12-09*
