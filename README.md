# HS Side Lift

Horizontal cabins and boat / ferry pads for **7 Days to Die 3.2**. You build the cabin or deck from vanilla blocks. This mod **moves** that structure along **X or Z**. It does not spawn a premade car.

Separate from **[HS Lift](https://github.com/Crumb3D/HS_Lift)** (vertical elevators). You can run both.

**Version 1.0.1**. License: [MIT](LICENSE). Author: Crumb.

## Types

- **Side lift** — 3D cabin. Opposite corners of the box, then **Add Stop** at the other end. First extra stop picks travel on X or Z (whichever is farther from the cabin origin).
- **Boat / ferry** — flat pad, both corners on the **same Y** (the deck). Add Stop at the other dock. Water is not treated as a blockage.

If the side cabin has no ceiling, empty top cells are filled from your floor / existing roof so they ride.

## Setup

Hold the **Side Lift Setup Tool**, aim at a placed block, hold **E**:

1. **New Side Lift** or **New Boat Bridge**
2. **Set Corner 1** / **Set Corner 2**
3. **Add Stop** at the far landing / dock
4. **Register Panel** on outside buttons only
5. Inside panel rides; hold **E** to pick a stop

Admin console: `hssidelift`.

Do **not** put `HSSideLift.json` in the Mods folder. The list lives in the world save so dedicated clients keep a matching mod hash.

Needs Harmony (`0_TFP_Harmony`). Restart after installing the DLL.
