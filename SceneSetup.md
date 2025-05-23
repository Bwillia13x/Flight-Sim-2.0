# Scene Setup Guide

## Main Scene Configuration: "DogfightDemo"

### Hierarchy Structure
```
DogfightDemo Scene
├── Environment
│   ├── Lighting
│   │   └── Directional Light (Sun)
│   ├── Skybox
│   │   └── Sky Material (Procedural or HDRI)
│   └── Terrain
│       └── Terrain GameObject (optional, or simple ground plane)
├── Player
│   ├── PlayerJet
│   │   ├── Model (aircraft mesh)
│   │   ├── Colliders (Box/Mesh colliders)
│   │   ├── Scripts
│   │   │   ├── FlightController
│   │   │   ├── WeaponSystem
│   │   │   └── HealthSystem
│   │   └── FirePoints (empty GameObjects for weapon origins)
│   └── Main Camera
│       └── FlightCamera script
├── Enemies
│   ├── EnemyJet_01
│   ├── EnemyJet_02
│   └── EnemyJet_03
├── AI Waypoints
│   ├── PatrolRoute_01
│   │   ├── Waypoint_01
│   │   ├── Waypoint_02
│   │   └── Waypoint_03
│   └── PatrolRoute_02
│       ├── Waypoint_01
│       └── Waypoint_02
├── UI
│   ├── Canvas (Screen Space - Overlay)
│   │   ├── HUD
│   │   │   ├── FlightHUD script
│   │   │   ├── SpeedIndicator
│   │   │   ├── AltitudeDisplay
│   │   │   ├── HealthBar
│   │   │   ├── AmmoCounter
│   │   │   ├── Crosshair
│   │   │   └── MiniRadar
│   │   ├── PauseMenu (initially inactive)
│   │   └── GameOverMenu (initially inactive)
└── Game Management
    └── GameManager
```

### Step-by-Step Setup

#### 1. Environment Setup
```
1. Create Directional Light:
   - Position: (0, 10, 0)
   - Rotation: (50, -30, 0)
   - Intensity: 1.0
   - Color: Warm white

2. Setup Skybox:
   - Window → Rendering → Lighting Settings
   - Skybox Material: Use Procedural or import HDRI
   - Ambient Source: Skybox
   - Ambient Intensity: 1.0

3. Ground/Terrain (optional):
   - Simple plane at Y=0 with large scale (100, 1, 100)
   - Or use Unity Terrain system for realistic landscape
   - Apply appropriate ground material
```

#### 2. Player Aircraft Setup
```
1. Create PlayerJet GameObject:
   - Add aircraft model (primitive cube if no model available)
   - Scale: (2, 0.5, 4) for basic aircraft shape
   - Tag: "Player"

2. Add Components:
   - Rigidbody: Mass=5000, Drag=0.1, Angular Drag=5
   - Box Collider: Size=(2, 0.5, 4)
   - FlightController script
   - WeaponSystem script  
   - HealthSystem script

3. Create Fire Points:
   - Empty GameObjects as children
   - Position at wing tips or nose
   - Assign to WeaponSystem firePoints array

4. Camera Setup:
   - Main Camera position: (0, 5, -15) relative to player
   - Add FlightCamera script
   - Assign player as target
```

#### 3. Enemy Aircraft Setup
```
1. Create EnemyJet prefab:
   - Similar setup to player but with EnemyAI script
   - Tag: "Enemy"
   - Different material/color for identification

2. Place multiple enemies in scene:
   - Spread around map at different altitudes
   - Set initial patrol waypoints
   - Configure detection ranges

3. Setup Waypoints:
   - Create empty GameObjects for patrol points
   - Position at strategic locations
   - Assign to EnemyAI patrolWaypoints arrays
```

#### 4. UI Configuration
```
1. Create Canvas:
   - Render Mode: Screen Space - Overlay
   - Canvas Scaler: Scale with Screen Size
   - Reference Resolution: 1920x1080

2. HUD Elements Setup:
   - Speed Text: Top-left corner
   - Altitude Text: Top-right corner
   - Health Bar: Bottom-left
   - Ammo Counter: Bottom-right
   - Crosshair: Screen center
   - Mini Radar: Bottom-center

3. Menu Setup:
   - Pause Menu: Center screen, initially inactive
   - Game Over Menu: Center screen, initially inactive
   - Use Unity UI buttons with appropriate scripts
```

#### 5. Game Manager Setup
```
1. Create GameManager GameObject:
   - Add GameManager script
   - Configure game mode and settings
   - Assign UI references
   - Set spawn points for players and enemies

2. Configure Spawn Points:
   - Create empty GameObjects at strategic locations
   - Player spawn: Safe starting position
   - Enemy spawns: Distributed around map
```

### Layer Configuration
```
Default Layers:
- Layer 8: "Player"
- Layer 9: "Enemy" 
- Layer 10: "Projectile"
- Layer 11: "Environment"

Physics Settings:
- Player vs Enemy: Collide
- Player vs Projectile: Collide
- Enemy vs Projectile: Collide
- Projectile vs Environment: Collide
- Player vs Environment: Collide
```

### Input Manager Settings
```
Add Custom Axes:
1. "Yaw":
   - Positive Button: e
   - Negative Button: q
   - Sensitivity: 3
   - Type: Key or Mouse Button

2. "Throttle":
   - Positive Button: left shift
   - Negative Button: left ctrl
   - Sensitivity: 3
   - Type: Key or Mouse Button
```

### Audio Setup (Optional)
```
1. Create Audio Mixer:
   - Master group
   - SFX group (weapons, explosions)
   - Engine group (aircraft sounds)
   - UI group (interface sounds)

2. Audio Sources:
   - Player engine sound (looping)
   - Weapon fire sounds (one-shot)
   - Ambient wind/atmosphere
```

### Testing Checklist
```
✓ Player aircraft responds to all controls
✓ Camera follows player smoothly
✓ Enemy AI patrols and engages player
✓ Weapons fire and cause damage
✓ HUD displays correct information
✓ Health system works (damage/respawn)
✓ Game manager handles scoring
✓ Pause/resume functionality works
```

### Performance Optimization
```
1. Occlusion Culling:
   - Bake occlusion data for complex scenes
   - Enable in Camera settings

2. LOD Groups:
   - Setup for detailed aircraft models
   - Multiple detail levels based on distance

3. Audio Optimization:
   - Compress audio files appropriately
   - Use 3D audio settings for spatial sound
```

---

*This scene setup provides a complete, playable dogfighting experience that can be immediately tested and expanded upon.*
