# Pathfinding Visualizer (Unity)

Interactive visualization of 8 classic pathfinding / graph algorithms on a 16×16 grid map,
built with **Unity (URP)** + TextMeshPro.

![Platform](https://img.shields.io/badge/Unity-2022%2B-333333) ![Algorithms](https://img.shields.io/badge/algorithms-8-blue)

---

## ✨ Features

- **8 algorithms** with per-step animation:
  BFS · DFS · Greedy Best-First · Dijkstra · A* · Bidirectional BFS · Floyd-Warshall · Bellman-Ford
- **Grid editor**: place start / end / obstacles by raycast; long-press to paint walls, second pass erases
- **Terrain cost**: raise / lower each cell's cost (−5 … +5); cost shown in black labels, cells shaded darker as they get more expensive
- **Visualization**: visited cells rise with a wave-like animation, live distance numbers spread during search (white), final path numbered 1..N with total **cost** and **search time** shown in the info panel
- **Negative weights support**: Dijkstra / A* fail (greedy assumption broken), Floyd stays correct without negative cycles, Bellman-Ford detects the negative cycle and reports it
- **Sound**: original Minecraft SFX mapped to search / path trace / button / wall-paint events
- **Controls**: Clear (keep start/end/weights, remove search traces) and Restart (full reset)

## 🎮 Controls

| Input | Action |
|---|---|
| Click right tool cube | choose tool (Start / End / Obstacle / Weight+ / Weight− / Run) |
| Click left algorithm cube | choose algorithm (or press **1–8**) |
| **Space** | run search |
| Left-drag on grid | paint obstacles / adjust weights |
| Clear / Restart buttons | clean search traces / full reset |

Algorithm hotkeys: `1`=BFS `2`=DFS `3`=Greedy `4`=Dijkstra `5`=A* `6`=Bi-BFS `7`=Floyd `8`=Bellman-Ford

## 🧠 Algorithm notes

| Algorithm | Strategy | Optimal? | Handles negative weights? |
|---|---|---|---|
| BFS | level order | yes (unweighted) | — |
| DFS | depth first | no | — |
| Greedy Best-First | h(n) only | no | — |
| Dijkstra | g(n) only | yes | **no** |
| A* | g(n)+h(n) | yes (consistent h) | **no** |
| Bidirectional BFS | both ends meet | yes | — |
| Floyd-Warshall | all-pairs DP | yes | yes (no neg. cycle) |
| Bellman-Ford | relax V−1 rounds | yes | yes + detects neg. cycle |

## 🚀 How to run

1. Unity **2022+** (URP template recommended)
2. Open the project folder, open scene `Assets/Scenes/Main.unity`
3. Press **Play**, paint a map and run an algorithm

## 🗂 Project structure

```
Assets/
├── Scripts/
│   ├── GridMap.cs           # grid build, node states, cost/color/material logic, wave animation
│   └── DemoController.cs    # tools, input, 8 search algorithms, info panel, audio
├── Audio/                   # minecraft-style SFX (dig/stone, orb, click, dig/grass)
├── Materials/               # state materials (idle/start/end/obstacle/visited/frontier/path)
├── Prefabs/                 # cell prefab (cube + TextMeshPro label)
└── Scenes/Main.unity        # main demo scene
```

## ⚖️ License

Educational demo project. Minecraft sound effects belong to Mojang / Microsoft and are used here for learning purposes only.
