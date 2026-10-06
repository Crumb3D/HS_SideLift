# HS Side Lift — quick start

Drop **HS_SideLift** into `Mods` (and the dedicated server Mods). Do **not** put `HSSideLift.json` in that folder.

```
giveself HSSideLiftTool
giveself HSSideLiftHandbook
giveself HSSideLiftOutsidePanel 4
giveself HSSideLiftInsidePanel
```

## Sideways cabin

1. Build the 3D cabin (floor, plate walls, ceiling). Missing roof cells get filled from the floor.
2. Setup tool → **New Side Lift** → opposite corners of the cabin.
3. At the other end of the path, aim at a block → **Add Stop**. That pick sets travel on X or Z.
4. Outside panels at each end → **Register Panel**. Wire one of them.
5. Inside panel in the cabin. Hold E to pick a stop.

## Boat / cable-ferry pad

1. Build a **cube deck** at one dock — any size. Both setup corners on the **same Y**.
2. **New Boat Bridge**.
3. Build the other dock in a **straight line** (same X or same Z).
4. Aim there → **Add Stop**. Two docks only. A rope through the pad links them.
5. Water between docks is ignored. Do not leave solid junk in the pad’s path.

Admin: `hssidelift type side | boat`
