using System;
using System.Collections.Generic;

// Shared setup used by the Elevator Setup Tool (hold E) and the admin-only HSSideLift console command.
public static class HSSideLiftSetup
{
    public static bool HasForcedAim;
    public static Vector3i ForcedAim;

    public static void Tell(EntityPlayerLocal player, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (player != null) GameManager.ShowTooltip(player, msg);
        else SdtdConsole.Instance.Output(msg);
    }

    public static string Execute(string sub, string arg, string extra, EntityPlayerLocal player)
    {
        HSSideLiftConfiguration.Editing();
        if (HSSideLiftNet.IsRemoteClient && sub != "status" && sub != "list" && sub != "preview")
            return HSSideLiftNet.SendSetup(sub, arg, extra, player);
        if (sub == "floor") return FloorCommand(arg, extra, player);
        if (sub == "go") return HSSideLiftController.RequestFloor(arg, "console") ?? "Going.";
        return Run(sub, arg, player);
    }

    static string Run(string sub, string arg, EntityPlayerLocal player)
    {
        var d = HSSideLiftConfiguration.Data;
        switch (sub)
        {
            case "status":
                return HSSideLiftController.StatusText();
            case "debug":
                d.Debug = !d.Debug;
                HSSideLiftDebug.Enabled = d.Debug;
                HSSideLiftConfiguration.Save();
                return "HSSideLift debug " + (d.Debug ? "ON" : "OFF");
            case "up":
                return HSSideLiftController.RequestMove(true, "console") ?? "Moving up.";
            case "down":
                return HSSideLiftController.RequestMove(false, "console") ?? "Moving down.";
            case "preview":
                return arg == "off" ? HSSideLiftController.StopPreview() : HSSideLiftController.StartPreview();
            case "list":
                return HSSideLiftConfiguration.ListLifts() + "\nEditing " + HSSideLiftConfiguration.Summary();
            case "select":
            {
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                return HSSideLiftConfiguration.SelectNearest(p);
            }
            case "music":
            {
                bool on;
                if (!HSSideLiftSettings.TryParseOnOff(arg, out on))
                    return "Usage: hssidelift music on | off   (host / single player). Now: Music=" + HSSideLiftSettings.Music;
                return HSSideLiftSettings.SetMusic(on);
            }
            case "flicker":
            case "flickerlights":
            {
                bool on;
                if (!HSSideLiftSettings.TryParseOnOff(arg, out on))
                    return "Usage: hssidelift flicker on | off   (host / single player). Now: FlickerLights=" + HSSideLiftSettings.FlickerLights;
                return HSSideLiftSettings.SetFlickerLights(on);
            }
            case "scheme":
            case "numbering":
            {
                if (arg != "gb" && arg != "uk" && arg != "us" && arg != "usa")
                    return "Floor labels: gb (default, G stays G) or us (G becomes 1, 1 becomes 2). Current: " + HSSideLiftConfiguration.FileFloorScheme
                        + "\nEdit FloorScheme in the world-save HSSideLift.json and restart, or: HSSideLift scheme gb | us";
                HSSideLiftConfiguration.SetFloorScheme(arg);
                HSSideLiftFloorSigns.Invalidate();
                var world = GameManager.Instance.World;
                if (world != null) HSSideLiftFloorSigns.UpdateToCurrentFloor(world);
                if (HSSideLiftConfiguration.FileFloorScheme == "us")
                    return "US stops: G shows as 1st Stop, 1 as 2nd Stop, 2 as 3rd Stop. Stops before the first stay B. Signs and the stop menu use this; console names stay G/1/2.";
                return "GB stops: G, 1, 2 (First Stop, 1st Stop, 2nd Stop). Signs and the stop menu match.";
            }
            case "type":
            case "new":
            {
                if (arg != "side" && arg != "boat" && arg != "ped" && arg != "vehicle") return "Usage: hssidelift type side | boat";
                if (arg == "ped") arg = "side";
                if (arg == "vehicle") arg = "boat";
                var created = HSSideLiftConfiguration.NewLift(arg);
                var hint = arg == "boat"
                    ? " Build a cube pad any size (both corners on the same Y). Then Add Stop at the other dock — the two landings must sit in a straight line (same X or same Z). A rope through the pad links the docks."
                    : " Side cabin: opposite corners of the 3D box. Then Add Stop at the other end.";
                return "Started " + created.ElevatorId + " (" + created.Type + ")." + hint + " Other lifts were not changed.\n" + HSSideLiftConfiguration.ListLifts();
            }
        }

        if (HSSideLiftController.IsMoving) return "This lift is moving; setup is ignored until it stops.";

        switch (sub)
        {
            case "corner1":
            case "corner2":
            {
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                var c = new[] { p.x, p.y, p.z };
                if (sub == "corner1") d.Corner1 = c; else d.Corner2 = c;
                var msg = (sub == "corner1" ? "Corner 1" : "Corner 2") + " = " + p;
                if (d.Corner1 != null && d.Corner2 != null) msg += "\n" + ApplyCorners();
                HSSideLiftConfiguration.Save();
                return msg;
            }
            case "upper":
            {
                int n = 1;
                while (HSSideLiftConfiguration.FloorByName(n.ToString()) != null) n++;
                return FloorCommand("add", n.ToString(), player);
            }
            case "panel":
                return PanelCommand(arg, player);
            case "speed":
            {
                float s;
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s) || s < 0.2f || s > 10f)
                    return "Usage: HSSideLift speed <0.2 - 10>";
                d.SpeedBlocksPerSecond = s;
                HSSideLiftConfiguration.Save();
                return "Speed = " + s + " blocks/sec";
            }
            case "grow":
            {
                int n;
                if (!int.TryParse(arg, out n) || n < 1 || n > 3) n = 1;
                if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
                int sx = d.SizeX + 2 * n, sz = d.SizeZ + 2 * n;
                if (sx * d.SizeY * sz > 512) return "Car would be too big (" + sx + "x" + d.SizeY + "x" + sz + "). Limit is 512 cells.";
                d.MinX -= n; d.MinZ -= n; d.SizeX = sx; d.SizeZ = sz;
                d.Corner1 = new[] { d.MinX, d.CurrentY, d.MinZ };
                d.Corner2 = new[] { d.MinX + sx - 1, d.CurrentY + d.SizeY - 1, d.MinZ + sz - 1 };
                HSSideLiftConfiguration.Save();
                var inBox = d.Panels.FindAll(p => HSSideLiftCar.InBox(p.Pos, d.CurrentY) || (p.X >= d.MinX && p.X < d.MinX + sx && p.Z >= d.MinZ && p.Z < d.MinZ + sz));
                var check = HSSideLiftCar.ValidateCarAt(GameManager.Instance.World, d.CurrentY);
                var what = d.IsVehicleType ? "Platform" : "Car box";
                return string.Format("{0} is now {1}x{2}x{3} from X{4} Z{5}.{6}{7}", what, sx, d.SizeY, sz, d.MinX, d.MinZ,
                    inBox.Count > 0 ? "\nWarning: " + inBox.Count + " outside panel(s) are now on the platform; move them to the wall beside it." : "",
                    check != null ? "\nWarning: " + check : "");
            }
            case "exclude":
                return ExcludeCommand(arg, player);
            case "cleanup":
            {
                int y;
                if (!int.TryParse(arg, out y)) return "Usage: HSSideLift cleanup <y>  (floor Y of a leftover copy of the car)";
                return HSSideLiftCar.RemoveDuplicateAt(GameManager.Instance.World, y);
            }
            case "debris":
            {
                Vector3i p;
                if (AimedBlock(player, out p) == null) BindAimedLift(p);
                return HSSideLiftCar.ClearDebris(GameManager.Instance.World);
            }
            case "reset":
                d.Corner1 = d.Corner2 = null;
                d.HasCar = false;
                HSSideLiftConfiguration.Save();
                return "Car selection cleared. Floors and panels kept.";
        }
        return "Unknown command.";
    }

    static string ExcludeCommand(string arg, EntityPlayerLocal player)
    {
        var d = HSSideLiftConfiguration.Data;
        if (arg == "clear")
        {
            d.ExcludedColumns.Clear();
            HSSideLiftConfiguration.Save();
            return "Excluded columns cleared; the whole car box is the car again.";
        }
        if (arg == "list")
        {
            if (d.ExcludedColumns.Count == 0) return "No excluded columns.";
            var parts = new List<string>();
            foreach (var c in d.ExcludedColumns)
                parts.Add("X" + c[0] + " Z" + c[1] + (c.Length >= 4 ? " rows " + (d.CurrentY + c[2]) + "-" + (d.CurrentY + c[3]) + " at the car's floor" : " (whole column)"));
            return "Stationary landing columns: " + string.Join(", ", parts.ToArray());
        }
        int height = 3;
        if (arg != "" && (!int.TryParse(arg, out height) || height < 1 || height > 16))
            height = 3;
        if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        var world = GameManager.Instance.World;
        var bv = world.GetBlock(p);
        var parent = bv.ischild ? bv.Block.multiBlockPos.GetParentPos(p, bv) : p;
        var pbv = world.GetBlock(parent);
        var cells = new List<Vector3i> { parent };
        if (pbv.Block.isMultiBlock)
            for (int i = 0; i < pbv.Block.multiBlockPos.Length; i++) cells.Add(parent + pbv.Block.multiBlockPos.Get(i, pbv.type, pbv.rotation));
        int added = 0;
        var done = new List<string>();
        foreach (var c in cells)
        {
            bool inFootprint = c.x >= d.MinX && c.x < d.MinX + d.SizeX && c.z >= d.MinZ && c.z < d.MinZ + d.SizeZ;
            if (!inFootprint || done.Contains(c.x + "," + c.z)) continue;
            done.Add(c.x + "," + c.z);
            int lo = int.MaxValue, hi = int.MinValue;
            foreach (var o in cells)
                if (o.x == c.x && o.z == c.z) { lo = Math.Min(lo, o.y - d.CurrentY); hi = Math.Max(hi, o.y - d.CurrentY); }
            if (height > 0) hi = Math.Max(hi, lo + height - 1);
            foreach (var e in d.ExcludedColumns)
                if (e != null && e.Length >= 4 && e[0] == c.x && e[1] == c.z) { lo = Math.Min(lo, e[2]); hi = Math.Max(hi, e[3]); }
            d.ExcludedColumns.RemoveAll(e => e != null && e.Length >= 2 && e[0] == c.x && e[1] == c.z);
            d.ExcludedColumns.Add(new[] { c.x, c.z, lo, hi });
            added++;
        }
        if (added == 0) return "Nothing to exclude: the aimed block (" + pbv.Block.GetBlockName() + " at " + parent + ") is not inside the car box.";
        HSSideLiftConfiguration.Save();
        return "Marked " + pbv.Block.GetBlockName() + " at " + parent + " as a stationary landing (" + added + " column(s)). "
            + "Car blocks above or below it in that column still ride and pass the landings on other floors.";
    }

    static string FloorCommand(string action, string name, EntityPlayerLocal player)
    {
        var d = HSSideLiftConfiguration.Data;
        switch (action)
        {
            case "":
            case "list":
                return "Stops (* = car): " + HSSideLiftConfiguration.FloorList();
            case "add":
            {
                if (HSSideLiftController.IsMoving) return "Lift is moving.";
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                BindAimedLift(p);
                d = HSSideLiftConfiguration.Data;
                if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
                if (d.IsBoatType && d.Floors != null && d.Floors.Count >= 2)
                    return "A boat ferry has two docks only (A and B). Remove a dock first if you need to move one.";
                int along;
                var aimErr = PrepareStopAlong(p, out along);
                if (aimErr != null) return aimErr;
                if (string.IsNullOrEmpty(name) || name.Contains(" ")) name = NextFloorName(along);
                if (HSSideLiftConfiguration.FloorByName(name) != null) return "Stop '" + name + "' already exists. Remove it first.";
                var same = HSSideLiftConfiguration.FloorAt(along);
                if (same != null) return HSSideLiftConfiguration.FloorDisplayName(same.Name) + " is already at this stop. Aim at the next landing further along the shaft.";
                int span = d.IsAxisZ ? d.SizeZ : d.SizeX;
                var near = d.Floors.Find(f => f != null && Math.Abs(f.Y - along) < span);
                if (near != null)
                    return "Too close to " + HSSideLiftConfiguration.FloorDisplayName(near.Name) + ". The next stop has to be at least one car length further along the shaft (same floor).";
                d.Floors.Add(new HSSideLiftFloor { Name = name, Y = along });
                RelabelIfStopBelowGround();
                HSSideLiftConfiguration.SyncFloors();
                var worldAdd = GameManager.Instance.World;
                if (worldAdd != null) HSSideLiftCar.EnsureShaftRoof(worldAdd);
                HSSideLiftConfiguration.Save();
                var added = HSSideLiftConfiguration.FloorAt(along);
                var shown = added != null ? HSSideLiftConfiguration.FloorDisplayName(added.Name) : name;
                if (d.IsBoatType)
                    return shown + " set. The pad runs in a straight line between the two docks; a rope through the platform links them.\nStops: " + HSSideLiftConfiguration.FloorList();
                return shown + " added along the shaft.\nStops: " + HSSideLiftConfiguration.FloorList();
            }
            case "ground":
            case "g":
            {
                if (HSSideLiftController.IsMoving) return "Lift is moving.";
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                BindAimedLift(p);
                d = HSSideLiftConfiguration.Data;
                if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
                var same = HSSideLiftConfiguration.FloorAt(AlongFromAim(p));
                if (same != null)
                    return HSSideLiftConfiguration.FloorDisplayName(same.Name) + " is already at this position.\nStops: " + HSSideLiftConfiguration.FloorList();
                d.Floors.Add(new HSSideLiftFloor { Name = "1", Y = AlongFromAim(p) });
                HSSideLiftConfiguration.SyncFloors();
                var worldG = GameManager.Instance.World;
                if (worldG != null) HSSideLiftCar.EnsureShaftRoof(worldG);
                HSSideLiftConfiguration.Save();
                return "Stops numbered 1, 2, 3 along the shaft.\nStops: " + HSSideLiftConfiguration.FloorList();
            }
            case "remove":
            {
                if (HSSideLiftController.IsMoving) return "Lift is moving.";
                HSSideLiftFloor f = null;
                if (!string.IsNullOrEmpty(name)) f = HSSideLiftConfiguration.FloorByName(name);
                if (f == null)
                {
                    Vector3i p;
                    var err = AimedBlock(player, out p);
                    if (err != null) return err;
                    f = HSSideLiftConfiguration.FloorAt(AlongFromAim(p)) ?? HSSideLiftConfiguration.FloorForPanel(p);
                }
                if (f == null) return "Aim at a landing that is a registered stop. Stops: " + HSSideLiftConfiguration.FloorList();
                if (f.Y == d.CurrentY) return "The car is parked at " + HSSideLiftConfiguration.FloorDisplayName(f.Name) + ". Send it to another stop first.";
                d.Floors.Remove(f);
                HSSideLiftConfiguration.SyncFloors();
                var worldRm = GameManager.Instance.World;
                if (worldRm != null) HSSideLiftCar.EnsureShaftRoof(worldRm);
                HSSideLiftConfiguration.Save();
                return HSSideLiftConfiguration.FloorDisplayName(f.Name) + " removed. Stops: " + HSSideLiftConfiguration.FloorList();
            }
        }
        return "Usage: HSSideLift floor list | add <name> | ground | remove <name>  (these are stops along the shaft)";
    }

    static bool IsBasementName(string name)
    {
        return name != null && name.Length >= 2 && (name[0] == 'B' || name[0] == 'b') && char.IsDigit(name[1]);
    }

    // Aimed height becomes G. Stops above it become 1, 2, …; stops below become B1, B2, … (closest first).
    // The car stays where it is — this only renames / adds the ground stop.
    static void RelabelWithGroundAt(int groundY)
    {
        var d = HSSideLiftConfiguration.Data;
        if (d.Floors == null) return;
        d.Floors.RemoveAll(f => f == null);
        d.Floors.Sort((a, b) => a.Y.CompareTo(b.Y));
        int below = 0;
        for (int i = d.Floors.Count - 1; i >= 0; i--)
        {
            if (d.Floors[i].Y >= groundY) continue;
            below++;
            d.Floors[i].Name = "B" + below;
        }
        int above = 0;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            var f = d.Floors[i];
            if (f.Y < groundY) continue;
            if (f.Y == groundY) f.Name = "G";
            else
            {
                above++;
                f.Name = above.ToString();
            }
        }
    }

    // Corners always name the car's height G. If you then Add Floor on a landing below,
    // that lower stop becomes G and the car's floor becomes 1 (2, …). Real B1 stays B1.
    static void RelabelIfStopBelowGround()
    {
        var d = HSSideLiftConfiguration.Data;
        if (d.Floors == null || d.Floors.Count < 2) return;
        d.Floors.Sort((a, b) => a.Y.CompareTo(b.Y));
        HSSideLiftFloor g = null;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            if (string.Equals(d.Floors[i].Name, "G", StringComparison.OrdinalIgnoreCase))
            {
                g = d.Floors[i];
                break;
            }
        }
        if (g == null) return;
        bool numberedBelowG = false;
        int parsed;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            var f = d.Floors[i];
            if (f.Y < g.Y && int.TryParse(f.Name, out parsed) && parsed >= 1)
            {
                numberedBelowG = true;
                break;
            }
        }
        if (!numberedBelowG) return;
        int num = 1;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            var f = d.Floors[i];
            if (IsBasementName(f.Name)) continue;
            f.Name = num == 1 ? "G" : (num - 1).ToString();
            num++;
        }
    }

    // World Y is the cabin height and stays the same. Stops step along X or Z, the way floors step along Y on the vertical lift.
    // A block still beside this car is not a new stop, and it must not flip the travel axis.
    static string PrepareStopAlong(Vector3i p, out int along)
    {
        along = 0;
        var d = HSSideLiftConfiguration.Data;
        if (d == null || !d.HasCar) { along = p.x; return null; }
        int outX = OutsideAxis(p.x, d.MinX, d.SizeX);
        int outZ = OutsideAxis(p.z, d.MinZ, d.SizeZ);
        bool xTrip = outX >= d.SizeX && outX >= outZ;
        bool zTrip = outZ >= d.SizeZ && outZ > outX;
        if (!TravelCommitted(d))
        {
            if (!xTrip && !zTrip)
            {
                along = d.IsAxisZ ? d.MinZ : d.MinX;
                return "That block is still at " + HSSideLiftConfiguration.FloorDisplayName("G") + ". Aim at the next landing further along the shaft. Same floor is right — the stop changes on X or Z, not on height.";
            }
            int origin = d.IsAxisZ ? d.MinZ : d.MinX;
            if (d.Floors != null)
                d.Floors.RemoveAll(f => f == null || f.Y != origin);
            d.Axis = xTrip ? "x" : "z";
            d.SetAlong(d.IsAxisZ ? d.MinZ : d.MinX);
            if (d.Floors != null)
                foreach (var f in d.Floors)
                    if (f != null) f.Y = d.CurrentY;
        }
        along = d.IsAxisZ ? p.z : p.x;
        return null;
    }

    static bool TravelCommitted(HSSideLiftConfigData d)
    {
        if (d.Floors == null || d.Floors.Count < 2) return false;
        int span = Math.Max(1, d.IsAxisZ ? d.SizeZ : d.SizeX);
        int min = d.IsAxisZ ? d.MinZ : d.MinX;
        int max = min + span - 1;
        foreach (var f in d.Floors)
        {
            if (f == null) continue;
            if (OutsideAxis(f.Y, min, span) >= span) return true;
            if (f.Y < min - span || f.Y > max + span) return true;
        }
        return false;
    }

    static int OutsideAxis(int v, int min, int size)
    {
        int max = min + Math.Max(1, size) - 1;
        if (v < min) return min - v;
        if (v > max) return v - max;
        return 0;
    }

    static int AlongFromAim(Vector3i p)
    {
        int along;
        PrepareStopAlong(p, out along);
        return along;
    }

    static string NextFloorName(int y)
    {
        var d = HSSideLiftConfiguration.Data;
        if (d.Floors.Count > 0 && y < d.Floors[0].Y)
        {
            int b = 1;
            while (HSSideLiftConfiguration.FloorByName("B" + b) != null) b++;
            return "B" + b;
        }
        int n = 1;
        while (HSSideLiftConfiguration.FloorByName(n.ToString()) != null) n++;
        return n.ToString();
    }

    static string ApplyCorners()
    {
        var d = HSSideLiftConfiguration.Data;
        int minX = Math.Min(d.Corner1[0], d.Corner2[0]), maxX = Math.Max(d.Corner1[0], d.Corner2[0]);
        int minY = Math.Min(d.Corner1[1], d.Corner2[1]), maxY = Math.Max(d.Corner1[1], d.Corner2[1]);
        int minZ = Math.Min(d.Corner1[2], d.Corner2[2]), maxZ = Math.Max(d.Corner1[2], d.Corner2[2]);
        if (HSSideLiftConfiguration.IsVehicle && minY != maxY)
        {
            d.HasCar = false;
            return "Boat / ferry pad is a platform only: both corners must be on the same Y (got Y" + minY + " and Y" + maxY + ").";
        }
        bool moved = d.MinX != minX || d.MinZ != minZ || d.SizeX != maxX - minX + 1 || d.SizeZ != maxZ - minZ + 1;
        d.MinX = minX; d.MinZ = minZ;
        d.SizeX = maxX - minX + 1;
        d.SizeY = HSSideLiftConfiguration.IsVehicle ? 1 : maxY - minY + 1;
        d.SizeZ = maxZ - minZ + 1;
        d.ParkY = minY;
        string cleared = "";
        if (moved && (d.Floors.Count > 0 || d.Panels.Count > 0))
        {
            cleared = "\nNew path position: old stops and panels were cleared.";
            d.Floors.Clear();
            d.Panels.Clear();
            d.ExcludedColumns.Clear();
        }
        if (d.SizeX * d.SizeY * d.SizeZ > 512)
        {
            d.HasCar = false;
            return "Car too big (" + d.SizeX + "x" + d.SizeY + "x" + d.SizeZ + "). v0.1 limit is 512 cells.";
        }
        d.HasCar = true;
        d.SetAlong(d.IsAxisZ ? minZ : minX);
        if (HSSideLiftConfiguration.FloorAt(d.CurrentY) == null)
        {
            var name = "1";
            for (int i = 2; HSSideLiftConfiguration.FloorByName(name) != null; i++) name = i.ToString();
            d.Floors.Add(new HSSideLiftFloor { Name = name, Y = d.CurrentY });
        }
        HSSideLiftConfiguration.SyncFloors();
        var world = GameManager.Instance.World;
        if (world != null && HSSideLiftCar.AutoExcludeDoorPlatforms(world) > 0)
            HSSideLiftConfiguration.Save();
        var check = HSSideLiftCar.ValidateCarAt(GameManager.Instance.World, d.CurrentY);
        var kind = HSSideLiftConfiguration.IsVehicle ? "Boat / ferry pad" : "Side cabin";
            var extra = HSSideLiftConfiguration.IsVehicle ? " Then Add Stop at the other dock (straight line, same X or same Z). Water is not an obstruction. A rope through the pad links the two landings." : " Then Add Stop at the other end of the path (that pick sets X or Z travel).";
        return string.Format("{0} set: {1}x{2}x{3} at X{4} Y{5} Z{6} (stop {7}).\nStops: {8}{9}{10}{11}",
            kind, d.SizeX, d.SizeY, d.SizeZ, d.MinX, d.ParkY, d.MinZ, HSSideLiftConfiguration.FloorLabel(d.CurrentY), HSSideLiftConfiguration.FloorList(),
            cleared, extra, check != null ? "\nWarning: " + check : "");
    }

    static string PanelCommand(string arg, EntityPlayerLocal player)
    {
        var d = HSSideLiftConfiguration.Data;
        if (arg == "clear")
        {
            d.Panels.Clear();
            HSSideLiftConfiguration.Save();
            return "All panels forgotten.";
        }
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimedLift(p);
        d = HSSideLiftConfiguration.Data;
        var world = GameManager.Instance.World;
        var aimedBv = world.GetBlock(p);
        var aimedParent = BlockHSSideLiftOutsidePanel.ParentPos(p, aimedBv);
        if (world.GetBlock(aimedParent).Block is BlockHSSideLiftInsidePanel)
        {
            bool onPad = d.HasCar && HSSideLiftCar.InFootprint(aimedParent.x, aimedParent.z)
                && aimedParent.y >= d.ParkY && aimedParent.y <= d.ParkY + 1;
            if (onPad)
                return "Inside panel is on this platform — it rides with the lift. Do not register it. Aim at an OUTSIDE button panel at each stop, then Register Panel.";
            return "Inside panel at " + aimedParent + " is not on this platform (Y" + d.ParkY + ", X" + d.MinX + "-" + (d.MinX + d.SizeX - 1) + " Z" + d.MinZ + "-" + (d.MinZ + d.SizeZ - 1) + "). Place it on the pad.";
        }
        Vector3i found;
        if (!FindPanelNear(world, p, out found))
            return "No Side Lift Outside Button Panel at or next to the aimed block (aimed: " + aimedBv.Block.GetBlockName() + " at " + p + "). Inside panels ride with the car and are not registered.";
        p = found;
        if (!d.HasCar)
            return "Set this side lift's car corners first (Corner 1, then Corner 2).";
        var floor = HSSideLiftConfiguration.FloorForPanel(p);
        if (floor == null || d.DistOutsideParkedAt(floor.Y, p.x, p.z) > HSSideLiftConfiguration.NearLiftBlocks)
            return "Panel at " + p + " is not beside a stop on " + d.ElevatorId + ". Add the stop at that landing first, then register the panel next to the stop (the car does not have to be parked there). Stops: " + HSSideLiftConfiguration.FloorList();
        d.Panels.RemoveAll(e => e.X == p.x && e.Y == p.y && e.Z == p.z);
        d.Panels.Add(new HSSideLiftPanelEntry { Stop = floor.Name, X = p.x, Y = p.y, Z = p.z });
        HSSideLiftConfiguration.ForgetPanelOnOtherLifts(d, p);
        HSSideLiftConfiguration.SyncFloors();
        HSSideLiftConfiguration.Save();
        return "Panel registered for " + HSSideLiftConfiguration.FloorDisplayName(floor.Name) + ". Wired to power: " + HSSideLiftPower.IsPanelPowered(p)
            + " (one powered panel runs the whole lift)";
    }

    static bool FindPanelNear(World world, Vector3i aimed, out Vector3i panel)
    {
        panel = aimed;
        int best = int.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            var p = new Vector3i(aimed.x + dx, aimed.y + dy, aimed.z + dz);
            var bv = world.GetBlock(p);
            if (!(bv.Block is BlockHSSideLiftOutsidePanel)) continue;
            int dist = dx * dx + dy * dy + dz * dz;
            if (dist >= best) continue;
            best = dist;
            panel = BlockHSSideLiftOutsidePanel.ParentPos(p, bv);
        }
        return best != int.MaxValue;
    }

    static void BindAimedLift(Vector3i p)
    {
        // Aiming at another shaft does not change the side lift being edited.
        HSSideLiftConfiguration.Editing();
    }

    public static string AimedBlock(EntityPlayerLocal player, out Vector3i pos)
    {
        pos = Vector3i.zero;
        if (HasForcedAim)
        {
            pos = ForcedAim;
            return null;
        }
        var world = GameManager.Instance.World;
        if (player == null) player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null) return "No local player.";
        var hit = player.HitInfo;
        if (hit == null || !hit.bHitValid) return "Aim at a block first.";
        pos = hit.hit.blockPos;
        if (world.GetBlock(pos).isair) return "Aim at a block first.";
        return null;
    }
}
