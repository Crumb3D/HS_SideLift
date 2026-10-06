using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class HSSideLiftPanelEntry
{
    public string Stop = "lower";
    public int X, Y, Z;

    [JsonIgnore]
    public Vector3i Pos { get { return new Vector3i(X, Y, Z); } }
}

public class HSSideLiftFloor
{
    public string Name;
    public int Y;
}

public class HSSideLiftConfigData
{
    public string ElevatorId = "lift1";
    public string Type = "side";

    public int[] Corner1;
    public int[] Corner2;

    // Car box footprint; CurrentY is the moving origin on Axis (X or Z). ParkY is the cabin/pad world Y.
    public bool HasCar;
    public int MinX, MinZ, SizeX, SizeY, SizeZ;
    public int CurrentY;
    public int ParkY;
    public string Axis = "x";

    public void SetAlong(int along)
    {
        CurrentY = along;
        if (IsAxisZ) MinZ = along;
        else MinX = along;
    }

    // Car floor Y at each landing, kept sorted bottom to top.
    public List<HSSideLiftFloor> Floors = new List<HSSideLiftFloor>();

    // Lowest / highest floor, derived from Floors by SyncFloors (older configs only had these two stops).
    public int LowerY;
    public int UpperY;
    public bool HasUpper;

    public float SpeedBlocksPerSecond = 1.5f;
    public float[] ModelOffset = { 0.5f, 0.5f, 0.5f };

    public List<HSSideLiftPanelEntry> Panels = new List<HSSideLiftPanelEntry>();

    // Elevator doors this many blocks out from the car footprint count as lift doors.
    public int DoorReach = 2;

    // World X,Z columns inside the car box that are not part of the car (landing doors in the car's wall line).
    public List<int[]> ExcludedColumns = new List<int[]>();

    // Solid plate behind button panels so the empty part of their block isn't see-through.
    public bool PanelBacking; // leftover JSON field; ignored (old silver backing removed)

    // Block names containing any of these may sit in the shaft; the car moves past them without touching them.
    public List<string> PassThroughBlocks = new List<string> { "doorTrim", "doorTrim1m", "doorTrimCorner", "plateCorner", "sheet", "billboard", "plateDouble", "ladderRound" };

    // Vanilla sound names; empty string = silent.
    public string ArriveSound = "elevator_ding1";
    public string MoveLoopSound = "generator_large_run_lp";

    // Volume for custom clips in Sounds/ (these bypass the game's volume slider).
    public float CustomSoundVolume = 0.8f;

    public bool Debug;

    // gb (default): G, 1, 2. us: G shows as 1, 1 as 2, 2 as 3. Stored floor names stay GB.
    public string FloorScheme = "gb";

    [JsonIgnore]
    public bool IsVehicleType
    {
        get
        {
            return string.Equals(Type, "vehicle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Type, "boat", StringComparison.OrdinalIgnoreCase);
        }
    }

    [JsonIgnore]
    public bool IsBoatType { get { return string.Equals(Type, "boat", StringComparison.OrdinalIgnoreCase); } }

    [JsonIgnore]
    public bool IsAxisZ { get { return string.Equals(Axis, "z", StringComparison.OrdinalIgnoreCase); } }

    [JsonIgnore]
    public bool IsAxisX { get { return !IsAxisZ; } }

    [JsonIgnore]
    public int Reach { get { return Math.Max(1, DoorReach <= 0 ? 2 : DoorReach); } }

    // Ped: cabin height. Vehicle: wall buttons and garage doors sit above the 1-high platform.
    [JsonIgnore]
    public int DoorHeight { get { return IsVehicleType ? Math.Max(4, SizeY) : Math.Max(1, SizeY); } }

    [JsonIgnore]
    public int RideHeight { get { return IsVehicleType ? Math.Max(5, SizeY) : Math.Max(1, SizeY); } }

    // 0 = inside the car footprint; 1, 2, ... = that many blocks outside (king-move / Chebyshev).
    public int DistOutsideXZ(int x, int z)
    {
        if (!HasCar) return int.MaxValue;
        int dx = 0, dz = 0;
        if (x < MinX) dx = MinX - x;
        else if (x > MinX + SizeX - 1) dx = x - (MinX + SizeX - 1);
        if (z < MinZ) dz = MinZ - z;
        else if (z > MinZ + SizeZ - 1) dz = z - (MinZ + SizeZ - 1);
        return Math.Max(dx, dz);
    }

    public bool InOutsideRing(int x, int z)
    {
        int d = DistOutsideXZ(x, z);
        return d >= 1 && d <= Reach;
    }

    public bool InDoorRing(int x, int z)
    {
        return DistOutsideXZ(x, z) <= Reach;
    }

    public bool YOnShaft(int y)
    {
        if (!HasCar) return false;
        return y >= ParkY - 1 && y <= ParkY + DoorHeight + 1;
    }
}

public class HSSideLiftFile
{
    public string ActiveId;
    public bool Debug;
    // gb (default): G, 1, 2. us: G shows as 1, 1 as 2. Edit this; restart the game. No rebuild.
    public string FloorScheme = "gb";
    public List<HSSideLiftConfigData> Lifts = new List<HSSideLiftConfigData>();
}

public static class HSSideLiftConfiguration
{
    public static HSSideLiftConfigData Data = new HSSideLiftConfigData();
    public static List<HSSideLiftConfigData> Lifts = new List<HSSideLiftConfigData>();
    public static string ActiveId;
    public static string FileFloorScheme = "gb";

    // Never write into Mods/. The game hashes that folder; a server-created HSSideLift.json
    // makes joining clients look like they have the wrong mod.
    public static string RuntimeDir
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return save;
            }
            catch { }
            return string.IsNullOrEmpty(HSSideLiftMod.UserDataPath) ? "." : HSSideLiftMod.UserDataPath;
        }
    }

    static string FilePath { get { return Path.Combine(RuntimeDir, "HSSideLift.json"); } }

    public static void EvacuateRuntimeFilesFromModFolder()
    {
        var mod = HSSideLiftMod.ModPath;
        var dest = HSSideLiftMod.UserDataPath;
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(dest) || !Directory.Exists(mod)) return;
        Directory.CreateDirectory(dest);
        MoveRuntimeFile(Path.Combine(mod, "HSSideLift.json"), Path.Combine(dest, "HSSideLift.json"));
        foreach (var f in Directory.GetFiles(mod, "HSSideLift*.journal.json"))
            MoveRuntimeFile(f, Path.Combine(dest, Path.GetFileName(f)));
    }

    static void MoveRuntimeFile(string from, string to)
    {
        if (!File.Exists(from)) return;
        try
        {
            if (!File.Exists(to)) File.Copy(from, to);
            File.Delete(from);
            HSSideLiftDebug.Info("Moved " + Path.GetFileName(from) + " out of Mods so server and clients keep the same folder.");
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Could not move " + from + " out of the mod folder", e);
        }
    }

    static void AdoptPendingSaveIfNeeded()
    {
        var pending = string.IsNullOrEmpty(HSSideLiftMod.UserDataPath) ? null : Path.Combine(HSSideLiftMod.UserDataPath, "HSSideLift.json");
        if (string.IsNullOrEmpty(pending) || !File.Exists(pending)) return;
        if (File.Exists(FilePath)) return;
        var dir = RuntimeDir;
        if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(HSSideLiftMod.UserDataPath), StringComparison.OrdinalIgnoreCase)) return;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.Copy(pending, FilePath);
        HSSideLiftDebug.Info("Copied lift list into this world save.");
    }

    public static void Use(HSSideLiftConfigData d)
    {
        if (d == null) return;
        Data = d;
        ActiveId = d.ElevatorId;
    }

    public static string ToSyncJson()
    {
        var file = new HSSideLiftFile { ActiveId = ActiveId, Debug = HSSideLiftDebug.Enabled, FloorScheme = FileFloorScheme, Lifts = Lifts };
        return JsonConvert.SerializeObject(file);
    }

    public static void ApplyFromServer(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var file = JsonConvert.DeserializeObject<HSSideLiftFile>(json, settings) ?? new HSSideLiftFile();
            Lifts = file.Lifts != null ? file.Lifts : new List<HSSideLiftConfigData>();
            ActiveId = file.ActiveId;
            FileFloorScheme = NormalizeFloorScheme(string.IsNullOrEmpty(file.FloorScheme) ? "gb" : file.FloorScheme);
            if (Lifts.Count == 0) Lifts.Add(new HSSideLiftConfigData());
            foreach (var d in Lifts) Normalize(d);
            var active = ById(ActiveId) ?? Lifts[0];
            Use(active);
            HSSideLiftDebug.Enabled = file.Debug;
            foreach (var d in Lifts)
            {
                if (d.Debug) HSSideLiftDebug.Enabled = true;
                HSSideLiftController.Ensure(d);
            }
            HSSideLiftDebug.Info("Got " + Lifts.Count + " lift(s) from server. Editing " + Data.ElevatorId);
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Could not apply lift list from server", e);
        }
    }

    public static void Load()
    {
        if (HSSideLiftNet.IsRemoteClient)
        {
            Lifts = new List<HSSideLiftConfigData>();
            Lifts.Add(new HSSideLiftConfigData());
            FileFloorScheme = "gb";
            Use(Lifts[0]);
            HSSideLiftDebug.Info("Client: waiting for the server lift list");
            return;
        }
        Lifts = new List<HSSideLiftConfigData>();
        FileFloorScheme = "gb";
        try
        {
            AdoptPendingSaveIfNeeded();
            if (File.Exists(FilePath))
            {
                var raw = File.ReadAllText(FilePath);
                var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
                if (raw.IndexOf("\"Lifts\"", StringComparison.Ordinal) >= 0)
                {
                    var file = JsonConvert.DeserializeObject<HSSideLiftFile>(raw, settings) ?? new HSSideLiftFile();
                    if (file.Lifts != null) Lifts.AddRange(file.Lifts);
                    ActiveId = file.ActiveId;
                    FileFloorScheme = ResolveFloorScheme(file.FloorScheme, Lifts);
                    Data = new HSSideLiftConfigData { Debug = file.Debug };
                }
                else
                {
                    var one = JsonConvert.DeserializeObject<HSSideLiftConfigData>(raw, settings);
                    if (one != null)
                    {
                        Lifts.Add(one);
                        FileFloorScheme = ResolveFloorScheme(one.FloorScheme, Lifts);
                    }
                }
            }
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Config load failed, using defaults", e);
            Lifts.Clear();
        }
        if (Lifts.Count == 0) Lifts.Add(new HSSideLiftConfigData());
        FileFloorScheme = NormalizeFloorScheme(string.IsNullOrEmpty(FileFloorScheme) ? ResolveFloorScheme(null, Lifts) : FileFloorScheme);
        foreach (var d in Lifts) Normalize(d);
        var active = ById(ActiveId) ?? Lifts[0];
        Use(active);
        HSSideLiftDebug.Enabled = Data.Debug;
        foreach (var d in Lifts)
            if (d.Debug) HSSideLiftDebug.Enabled = true;
        Save();
        HSSideLiftDebug.Info("Config loaded: " + Lifts.Count + " lift(s), active " + Data.ElevatorId + " — " + Summary());
    }

    static void Normalize(HSSideLiftConfigData d)
    {
        if (d.Panels == null) d.Panels = new List<HSSideLiftPanelEntry>();
        if (d.Floors == null) d.Floors = new List<HSSideLiftFloor>();
        if (d.PassThroughBlocks == null) d.PassThroughBlocks = new List<string>();
        if (d.ExcludedColumns == null) d.ExcludedColumns = new List<int[]>();
        d.PassThroughBlocks.RemoveAll(p => string.Equals(p, "plate", StringComparison.OrdinalIgnoreCase));
        d.PassThroughBlocks = new List<string>(new HashSet<string>(d.PassThroughBlocks, StringComparer.OrdinalIgnoreCase));
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "sheet", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("sheet");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "billboard", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("billboard");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "signLetter", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("signLetter");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "signNumber", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("signNumber");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "plateDouble", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("plateDouble");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "ladderRound", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("ladderRound");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "doorTrim", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("doorTrim");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "doorTrim1m", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("doorTrim1m");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "doorTrimCorner", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("doorTrimCorner");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "plateCorner", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("plateCorner");
        if (string.IsNullOrEmpty(d.ElevatorId)) d.ElevatorId = "lift1";
        d.FloorScheme = FileFloorScheme;
        if (d.Floors.Count == 0 && d.HasCar)
        {
            d.Floors.Add(new HSSideLiftFloor { Name = "G", Y = d.LowerY });
            if (d.HasUpper && d.UpperY != d.LowerY) d.Floors.Add(new HSSideLiftFloor { Name = "1", Y = d.UpperY });
        }
        if (d.ModelOffset == null || d.ModelOffset.Length != 3) d.ModelOffset = new[] { 0.5f, 0.5f, 0.5f };
        if (d.SpeedBlocksPerSecond <= 0.05f) d.SpeedBlocksPerSecond = 1.5f;
        if (d.DoorReach <= 0) d.DoorReach = 2;
        var saved = Data;
        Data = d;
        SyncFloors();
        HSSideLiftCar.DropInteriorFloorExcludes();
        Data = saved;
    }

    public static void Save()
    {
        try
        {
            if (Data != null)
            {
                var i = Lifts.FindIndex(l => l.ElevatorId == Data.ElevatorId);
                if (i >= 0) Lifts[i] = Data;
                else if (!Lifts.Contains(Data)) Lifts.Add(Data);
                ActiveId = Data.ElevatorId;
            }
            if (HSSideLiftNet.IsRemoteClient) return;
            var dir = RuntimeDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var file = new HSSideLiftFile { ActiveId = ActiveId, Debug = HSSideLiftDebug.Enabled, FloorScheme = FileFloorScheme, Lifts = Lifts };
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            HSSideLiftNet.BroadcastConfig();
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Config save failed", e);
        }
    }

    public static HSSideLiftConfigData ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Lifts.Find(l => string.Equals(l.ElevatorId, id, StringComparison.OrdinalIgnoreCase));
    }

    public static HSSideLiftConfigData NewLift(string type)
    {
        if (type != "side" && type != "boat" && type != "ped" && type != "vehicle") type = "side";
        if (type == "ped") type = "side";
        if (type == "vehicle") type = "boat";
        var prefix = type == "boat" ? "boat" : "side";
        int n = 1;
        while (ById(prefix + n) != null) n++;
        var d = new HSSideLiftConfigData
        {
            ElevatorId = prefix + n,
            Type = type,
            DoorReach = 2,
            PassThroughBlocks = type == "boat"
                ? new List<string> { "doorTrim", "doorTrim1m", "doorTrimCorner", "plateCorner", "GarageDoor", "rollUpDoor", "rollUpGate", "sheet", "billboard", "plateDouble", "ladderRound", "water", "rope", "chain", "cable" }
                : new List<string> { "doorTrim", "doorTrim1m", "doorTrimCorner", "plateCorner", "sheet", "billboard", "plateDouble", "ladderRound" },
            ArriveSound = "elevator_ding1",
            MoveLoopSound = "generator_large_run_lp",
            CustomSoundVolume = 0.8f,
            FloorScheme = FileFloorScheme
        };
        Lifts.Add(d);
        Use(d);
        HSSideLiftController.Ensure(d);
        Save();
        return d;
    }

    public static string SelectNearest(Vector3i pos)
    {
        var d = LiftAt(pos);
        if (d == null) return "No lift at this block (2 blocks outside a registered car). Start one: hssidelift type side | boat, then set corners.";
        Use(d);
        Save();
        return "Now editing " + d.ElevatorId + " (" + d.Type + "). " + Summary();
    }

    // Registered outside panel, or the lift whose 2-block outside ring contains this cell.
    // Only a panel that was registered to a lift belongs to it. Nearby unregistered panels stay unlinked.
    public static HSSideLiftConfigData RegisteredPanelOwner(Vector3i pos)
    {
        foreach (var d in Lifts)
            if (d.Panels != null && d.Panels.Exists(p => p.X == pos.x && p.Y == pos.y && p.Z == pos.z))
                return d;
        return null;
    }

    public static HSSideLiftConfigData LiftForOutsidePanel(Vector3i pos)
    {
        return RegisteredPanelOwner(pos) ?? LiftNearOutside(pos);
    }

    public static string ListLifts()
    {
        if (Lifts.Count == 0) return "No lifts.";
        var parts = new List<string>();
        foreach (var d in Lifts)
        {
            var mark = d.ElevatorId == ActiveId ? "* " : "  ";
            var car = d.HasCar ? ("X" + d.MinX + " Z" + d.MinZ + " parkY" + d.ParkY + " along" + d.CurrentY + " " + d.Axis + " " + d.SizeX + "x" + d.SizeY + "x" + d.SizeZ) : "no car yet";
            parts.Add(mark + d.ElevatorId + " (" + d.Type + ") " + car);
        }
        return "Lifts:\n" + string.Join("\n", parts.ToArray());
    }

    public static HSSideLiftConfigData LiftNearOutside(Vector3i pos)
    {
        HSSideLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (!d.HasCar || !d.YOnShaft(pos.y) || !d.InOutsideRing(pos.x, pos.z)) continue;
            int dist = d.DistOutsideXZ(pos.x, pos.z);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    public static HSSideLiftConfigData LiftForInsidePanel(Vector3i pos)
    {
        foreach (var d in Lifts)
        {
            if (!d.HasCar) continue;
            var saved = Data;
            Data = d;
            bool inCar = HSSideLiftCar.InBox(pos, d.CurrentY);
            if (!inCar && d.IsVehicleType && HSSideLiftCar.InFootprint(pos.x, pos.z) && pos.y >= d.CurrentY && pos.y <= d.CurrentY + 1)
            {
                var world = GameManager.Instance.World;
                inCar = world != null && world.GetBlock(pos).Block is BlockHSSideLiftInsidePanel;
            }
            Data = saved;
            if (inCar) return d;
        }
        return null;
    }

    public static HSSideLiftConfigData LiftForDoor(Vector3i pos)
    {
        HSSideLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (!d.HasCar || !d.YOnShaft(pos.y) || !d.InDoorRing(pos.x, pos.z)) continue;
            int dist = d.DistOutsideXZ(pos.x, pos.z);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    public static HSSideLiftConfigData LiftAt(Vector3i pos)
    {
        return LiftForInsidePanel(pos) ?? LiftForDoor(pos) ?? LiftForOutsidePanel(pos);
    }

    public static HSSideLiftConfigData LiftNearXZ(Vector3i pos)
    {
        HSSideLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (!d.HasCar) continue;
            int dist = d.DistOutsideXZ(pos.x, pos.z);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    public static HSSideLiftConfigData LiftForPlayer(EntityPlayerLocal player)
    {
        if (player == null) return null;
        var feet = new Vector3i(UnityEngine.Mathf.FloorToInt(player.position.x), UnityEngine.Mathf.FloorToInt(player.position.y + 0.1f), UnityEngine.Mathf.FloorToInt(player.position.z));
        foreach (var d in Lifts)
        {
            if (!d.HasCar) continue;
            var saved = Data;
            Data = d;
            bool inCar = HSSideLiftCar.InFootprint(feet.x, feet.z) && feet.y >= d.ParkY && feet.y <= d.ParkY + d.RideHeight;
            Data = saved;
            if (inCar) return d;
        }
        return null;
    }

    public static bool IsVehicle { get { return Data != null && Data.IsVehicleType; } }

    // Sort floors, refresh the derived lowest/highest stop, and relabel panels with the floor they serve.
    public static void SyncFloors()
    {
        var d = Data;
        d.Floors.RemoveAll(f => f == null || string.IsNullOrEmpty(f.Name));
        d.Floors.Sort((a, b) => a.Y.CompareTo(b.Y));
        d.HasUpper = d.Floors.Count >= 2;
        if (d.Floors.Count > 0)
        {
            d.LowerY = d.Floors[0].Y;
            d.UpperY = d.Floors[d.Floors.Count - 1].Y;
        }
        foreach (var p in d.Panels)
        {
            var f = FloorForPanel(p.Pos);
            p.Stop = f != null ? f.Name : "?";
        }
    }

    public static HSSideLiftFloor FloorAt(int y)
    {
        return Data.Floors.Find(f => f.Y == y);
    }

    public static HSSideLiftFloor FloorByName(string name)
    {
        return Data.Floors.Find(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static int FloorIndexAt(int y)
    {
        return Data.Floors.FindIndex(f => f.Y == y);
    }

    // A panel serves the highest floor at or below it, as long as it sits within the car's height of that floor.
    public static HSSideLiftFloor FloorForPanel(int panelY)
    {
        return FloorAt(panelY);
    }

    public static HSSideLiftFloor FloorForPanel(HSSideLiftConfigData d, int panelY)
    {
        if (d == null || d.Floors == null) return null;
        return d.Floors.Find(f => f != null && f.Y == panelY);
    }

    public static HSSideLiftFloor FloorForPanel(Vector3i p)
    {
        return FloorForPanel(Data, p);
    }

    public static HSSideLiftFloor FloorForPanel(HSSideLiftConfigData d, Vector3i p)
    {
        if (d == null || d.Floors == null || d.Floors.Count == 0) return null;
        int along = d.IsAxisZ ? p.z : p.x;
        HSSideLiftFloor best = null;
        int bestDist = int.MaxValue;
        foreach (var f in d.Floors)
        {
            if (f == null) continue;
            int dist = Math.Abs(f.Y - along);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = f;
            }
        }
        int reach = Math.Max(d.Reach, Math.Max(d.SizeX, d.SizeZ));
        if (best != null && bestDist > reach) return null;
        return best;
    }

    public static string FloorLabel(int y)
    {
        var f = FloorAt(y);
        return f != null ? FloorDisplayName(f.Name) : "Y" + y + " (between floors)";
    }

    public static bool IsUsFloorScheme()
    {
        return string.Equals(FileFloorScheme, "us", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeFloorScheme(string scheme)
    {
        if (string.IsNullOrEmpty(scheme)) return "gb";
        var s = scheme.Trim().ToLowerInvariant();
        if (s == "us" || s == "usa") return "us";
        return "gb";
    }

    static string ResolveFloorScheme(string fileScheme, List<HSSideLiftConfigData> lifts)
    {
        if (!string.IsNullOrEmpty(fileScheme)) return NormalizeFloorScheme(fileScheme);
        if (lifts != null)
        {
            foreach (var d in lifts)
                if (d != null && !string.IsNullOrEmpty(d.FloorScheme))
                    return NormalizeFloorScheme(d.FloorScheme);
        }
        return "gb";
    }

    public static void SetFloorScheme(string scheme)
    {
        FileFloorScheme = NormalizeFloorScheme(scheme);
        foreach (var d in Lifts)
            if (d != null) d.FloorScheme = FileFloorScheme;
        if (Data != null) Data.FloorScheme = FileFloorScheme;
        Save();
    }

    static bool IsGroundName(string n)
    {
        return n.Equals("G", StringComparison.OrdinalIgnoreCase)
            || n.Equals("GF", StringComparison.OrdinalIgnoreCase)
            || n.Equals("ground", StringComparison.OrdinalIgnoreCase);
    }

    // Wall letter/number for this stored floor name (G stays G in GB, becomes 1 in US).
    public static string FloorSignToken(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var n = name.Trim();
        if (IsUsFloorScheme())
        {
            if (IsGroundName(n)) return "1";
            int shifted;
            if (int.TryParse(n, out shifted) && shifted >= 0)
            {
                shifted++;
                if (shifted > 9) shifted = 9;
                return shifted.ToString();
            }
        }
        return n[0].ToString();
    }

    // Menu and tooltips. Console still uses the short stored name (G, 1, B1).
    public static string FloorDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown Floor";
        var n = name.Trim();
        if (Data != null && Data.IsBoatType)
        {
            if (IsGroundName(n)) return "Dock A";
            int boatNum;
            if (int.TryParse(n, out boatNum) && boatNum >= 1) return "Dock B";
        }
        bool us = IsUsFloorScheme();
        if (IsGroundName(n))
            return us ? "1st Floor" : "Ground Floor";
        if (n.Length >= 2 && (n[0] == 'B' || n[0] == 'b') && char.IsDigit(n[1]))
        {
            int basement;
            if (int.TryParse(n.Substring(1), out basement) && basement > 0)
                return "Basement " + basement;
        }
        int num;
        if (int.TryParse(n, out num) && num >= 0)
            return Ordinal(us ? num + 1 : num) + " Floor";
        return n;
    }

    static string Ordinal(int n)
    {
        int m = n % 100;
        if (m >= 11 && m <= 13) return n + "th";
        switch (n % 10)
        {
            case 1: return n + "st";
            case 2: return n + "nd";
            case 3: return n + "rd";
            default: return n + "th";
        }
    }

    public static string FloorList()
    {
        var d = Data;
        if (d.Floors.Count == 0) return "no floors";
        var parts = new List<string>();
        for (int i = d.Floors.Count - 1; i >= 0; i--)
            parts.Add(d.Floors[i].Name + "=Y" + d.Floors[i].Y + (d.Floors[i].Y == d.CurrentY ? "*" : ""));
        return string.Join(", ", parts.ToArray());
    }

    public static string Summary()
    {
        var d = Data;
        var car = d.HasCar
            ? string.Format("car {0}x{1}x{2} at X{3} Z{4}, now Y{5}", d.SizeX, d.SizeY, d.SizeZ, d.MinX, d.MinZ, d.CurrentY)
            : "car not set";
        return string.Format("id={0} type={1} {2}, floors [{3}] {4}, speed {5}, panels {6}, debug {7}",
            d.ElevatorId, d.Type, car, FloorList(), d.FloorScheme, d.SpeedBlocksPerSecond, d.Panels.Count, d.Debug);
    }
}
