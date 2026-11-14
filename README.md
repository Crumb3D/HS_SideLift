# HS Side Lift

Horizontal cabins and boat / ferry pads for **7 Days to Die 3.2, 3.3 and 3.4**. You build the cabin or deck from vanilla blocks. This mod **moves** that structure along **X or Z**. It does not spawn a premade car.

Separate from **[HS Lift](https://github.com/Crumb3D/HS_Lift)** (vertical elevators). You can run both.

**Version 1.0.21**. One download for **7DTD 3.2, 3.3 and 3.4**. License: [MIT](LICENSE). Author: Crumb.

Stops, not floors. They are numbered 1st Stop, 2nd Stop, 3rd Stop along the shaft, all on the same floor height. An outside panel registers to the stop it sits beside, even when the car is parked at another stop. When the shaft walls are high enough, the mod places a half-cube roof over the whole horizontal run. Journal challenges say HS Side Lift. Changelog: [CHANGELOG.md](CHANGELOG.md).

## Types

- **Side lift** — 3D cabin. Opposite corners of the box, then **Add Stop** at the other end. First extra stop picks travel on X or Z (whichever is farther from the cabin origin).
- **Boat / ferry** — flat pad, both corners on the **same Y** (the deck). Add Stop at the other dock. Water is not treated as a blockage.

When the shaft walls reach the top of the cabin, the mod places the roof. You do not build that ceiling.

## Setup

Hold the **Side Lift Setup Tool**, aim at a placed block, hold **E**:

1. **New Side Lift** or **New Boat Bridge**
2. **Set Corner 1** / **Set Corner 2**
3. **Add Stop** at the far landing / dock
4. **Register Panel** on outside buttons only
5. Inside panel rides. Two stops: press **E** for the next stop. More than two: hold **E** and pick a stop.

Admin console: `hssidelift`. Paint the cabin with the paintbrush (Metal: Lift Floor, Lift Wall, Lift Ceiling). Cabin music is a host setting (`hssidelift music on|off`). Joining clients follow the host.

Do **not** put `HSSideLift.json` in the Mods folder. The list lives in the world save so dedicated clients keep a matching mod hash.

Needs Harmony (`0_TFP_Harmony`). Restart after installing the DLL.

Cabin music is a **host** setting (`hssidelift music on|off`). Put `.mp3` files in `assets/music`. Empty folder is silent. Paint the cabin with the paintbrush.
