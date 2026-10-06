# Make the moving cabin or ferry pad

You build it. The mod only **moves** what is inside the two corners.

## Side cabin

Opposite corners of the **3D box**. Floor, plate walls, ceiling. If the top layer is empty, the mod copies your floor (or an existing roof block) into those cells.

Then **Add Stop** at the far landing. That stop chooses whether the car travels on X or Z.

## Boat / ferry

Two landings on the lake (docks). You pick the pad size.

1. Build a **cube platform** at dock A (any size you want). Both corners on the **same Y**.
2. Setup tool → **New Boat Bridge** → **Set Corner 1** / **Set Corner 2** on that pad.
3. Build dock B in a **straight line** from dock A (same X **or** same Z, across the water).
4. Aim at dock B → **Add Stop**. That is the only other stop (Dock A and Dock B).

The pad travels in a straight line between the two docks. A **rope** is drawn through the platform, linking the landings. Water is not an obstruction. If you place your own rope/chain blocks along the line, they stay put and the pad slides past them.
