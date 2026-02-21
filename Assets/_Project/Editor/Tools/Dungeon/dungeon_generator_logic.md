# Dungeon Generator System Logic & Architecture

This document explains the technical implementation and design decisions behind the `RefinedGenerationScript.cs` to ensure consistency in future iterations.

## 1. Core Architecture: Coordinate-Based Grid
Instead of a simple ASCII-to-String map, the generator uses a **Room-ID Matrix** (`int[,] roomMap = new int[45, 45]`).

- **World Scale:** 90m x 90m total area.
- **Resolution:** 45x45 Grid.
- **Cell Size:** 2m per cell.
- **Mapping Logic:**
    - `0`: Void/Empty space (no floors).
    - `1-26`: Specific Room IDs.
    - `99`: Plaza.
    - `100`: Corridors (Forbidden Zones for rooms).

## 2. Shared Wall Logic (Transition Detection)
The script produces high-fidelity walls by detecting transitions between different cell values.

- **Internal Walls:** A wall is instantiated if `cellA` and `cellB` are different AND neither is `0`. This allows rooms to share a single thin wall.
- **Boundary Walls:** A wall is instantiated if one cell is a room/corridor and the other is `0` (Void).
- **Instantiation:** Walls are placed exactly on the edges of the cells, not centered inside them, preventing "double-layer" thickness.

## 3. Intelligent Column Placement (Junctions)
To avoid a "forest" of columns, we use a 2x2 neighborhood check around every grid vertex:

- **Logic:** A column is placed at a vertex if it sits at a structural **junction**.
- **Junction Trigger:** If the vertex is a meeting point for an L-Corner, T-Intersection, or North-South/East-West wall crossing.
- **Exclusion:** Columns are **never** placed along straight wall segments (where connections are exactly 180° apart).

## 4. Corridor Precedence (Forbidden Zones)
The blue corridor path acts as a "Ladder" structure constraint.

- **Workflow:** The matrix is filled with Corridor ID (`100`) based on the blueprint markings first.
- **Room Placement:** Rooms are then defined in the remaining space. This ensures passthrough areas are never obstructed by random room instantiation.

## 5. Prefab Randomization
Each logic unit (Floor, Wall, Column) is pulled from the `DungeonTheme` object.
- The script iterates through the `Variants` array of each category and picks a random prefab.
- This ensures organic visual diversity while maintaining a strict technical layout.

## Future Usage Recommendation
When starting a new generation or refactoring:
1. Define the **Corridor Ladder** first.
2. Group **Cellblocks** as contiguous IDs to allow shared walls.
3. Use `FillRoom(x, z, w, h, id)` for all rectangular definitions.
