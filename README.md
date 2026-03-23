# EpidemicSim

High-performance epidemic simulation in Unity using Jobs, Burst, and GPU instanced rendering.

## Overview

EpidemicSim models a simple SIR (Susceptible, Infected, Removed) system of moving agents.

- `Assets/Scripts/Managers/SimulationManager.cs`: orchestrates simulation loops, spatial hashing, agent state updates, rendering, and SIR graphing.
- `Assets/Scripts/Jobs/*`: Burst-compiled parallel jobs for movement, grid buildup, infection spread, and healing.
- Data structure sizes can scale up to tens of thousands of agents.

## Key Concepts

- Agents live in a 2D bounded box (x, y). 3D rendering is done on the XZ plane.
- Agents move and bounce off boundaries.
- Infection spreads inside `infectionRadius` via a spatial hash grid (`BuildGridJob`) to limit neighbor checks.
- Infected agents recover after `infectionDuration` and become removed.
- Optional mask adoption affects transmission probability.

## Default parameters (in `SimulationManager`)

- `agentCount`: 20,000
- `speed`: 2
- `infectionRadius`: 0.5
- `infectionDuration`: 5
- `infectionProbabilityPerSecond`: 0.4
- `maskAdoptionRate`: 0
- `maskReduction`: 0.7

## Job breakdown

- `MovementJob`: position integration, boundary reflection.
- `BuildGridJob`: hash mapping from cell to agents (3x3 neighbors in spread stage).
- `InfectionSpreadJob`: infectious contacts, probability per frame, mask modulation.
- `HealingJob`: decrement infection timer and move to `Removed`.

## Rendering

- Uses GPU instancing via `ComputeBuffer` and `Graphics.DrawMeshInstancedIndirect`.
- Agents share `agentMesh` and `agentMaterial` (the latter must support `_Matrices` and `_States`).

## SIR graph

- Real-time normalized S/I/R history drawn in `OnGUI` using `graphRect` and `graphTitle`.

## Setup and usage

1. Open in Unity Editor (2020+ recommended).
2. Create a scene with a `SimulationManager` component.
3. Assign `agentMesh` (e.g., `Cube` mesh), `agentMaterial`, and adjust parameters.
4. Play.

## Customization

- `renderLayer`: camera layer for drawn agents.
- `boundaryMin/Max`: simulation bounds.
- `randomSeed`: deterministic behavior.
- `maxDataPoints`: graph history length.

## Performance notes

- Agent state arrays and grid are `NativeArray` / `NativeParallelMultiHashMap` for high throughput.
- All jobs are scheduled with `batchSize = 64`.

## Cleanup

`OnDestroy` disposes all native memory and buffers safely.

---

Generated from repository source code.
