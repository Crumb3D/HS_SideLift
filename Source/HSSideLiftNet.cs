using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// Server owns HSSideLift.json, block capture/place, and doors. Clients send uses/setup
// and play a moving copy so everyone sees and rides the same cars.
public static class HSSideLiftNet
{
    public const byte Config = 1;
    public const byte Setup = 2;
    public const byte UsePanel = 3;
    public const byte UseInside = 4;
    public const byte UseDoor = 5;
    public const byte UseFloor = 6;
    public const byte Doors = 7;
    public const byte MoveStart = 8;
    public const byte MoveEnd = 9;
    public const byte Tip = 10;

    public static bool IsAuthority
    {
        get
        {
            try
            {
                if (GameManager.IsDedicatedServer) return true;
                var cm = ConnectionManager.Instance;
                if (cm != null) return cm.IsServer;
            }
            catch { }
            return true;
        }
    }

    public static bool IsRemoteClient
    {
        get
        {
            try
            {
                var cm = ConnectionManager.Instance;
                return cm != null && cm.IsClient && !cm.IsServer;
            }
            catch { }
            return false;
        }
    }

    static Type pkgType;

    public static void RegisterPackage()
    {
        try
        {
            var t = HSGameVersion.Is33
                ? HSGameApi.NetPackageType33("NetPackageHSSideLift", typeof(NetPackageHSSideLiftCore))
                : HSGameApi.NetPackageType32("NetPackageHSSideLift", typeof(NetPackageHSSideLiftCore));
            pkgType = t;
            var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            var dict = f.GetValue(null) as IDictionary;
            if (dict == null) return;
            var args = f.FieldType.GetGenericArguments();
            if (args != null && args.Length >= 1 && args[0] == typeof(string))
                dict[t.Name] = t;
            else if (args != null && args.Length >= 1 && args[0] == typeof(Type))
                dict[t] = t.Name;
            else
                dict[t.Name] = t;
            HSSideLiftDebug.Info("Registered " + t.Name + " (" + (HSGameVersion.Is33 ? "3.3 emit" : "3.2 emit") + ")");
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Net package register failed", e);
        }
    }

    static NetPackageHSSideLiftCore Pkg()
    {
        if (pkgType == null) RegisterPackage();
        return (NetPackageHSSideLiftCore)HSGameApi.GetNetPackage(pkgType);
    }

    static void ToServer(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null) return;
        cm.SendToClientsOrServer(pkg);
    }

    static void ToClients(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer) return;
        cm.SendPackage(pkg);
    }

    static void ToClient(ClientInfo ci, NetPackage pkg)
    {
        if (ci != null) ci.SendPackage(pkg);
        else ToClients(pkg);
    }

    public static string SendSetup(string sub, string arg, string extra, EntityPlayerLocal player)
    {
        Vector3i aim = Vector3i.zero;
        bool hasAim = HSSideLiftSetup.AimedBlock(player, out aim) == null;
        ToServer(Pkg().SetupCmd(Setup, sub ?? "", arg ?? "", extra ?? "", "", 0, 0, hasAim, aim, false, null, 0f));
        return null;
    }

    public static void SendUsePanel(Vector3i pos)
    {
        ToServer(Pkg().SetupCmd(UsePanel, "", "", "", "", 0, 0, true, pos, false, null, 0f));
    }

    public static void SendUseDoor(Vector3i pos)
    {
        ToServer(Pkg().SetupCmd(UseDoor, "", "", "", "", 0, 0, true, pos, false, null, 0f));
    }

    public static void SendUseInside(Vector3i pos, int floorIndex)
    {
        HSSideLiftDebug.Info("Send UseInside floor " + floorIndex + " at " + pos);
        ToServer(Pkg().SetupCmd(UseInside, "", "", "", "", floorIndex, 0, true, pos, false, null, 0f));
    }

    public static void SendUseFloor(string liftId, int targetY, int floorIndex)
    {
        HSSideLiftDebug.Info("Send UseFloor " + liftId + " floor " + floorIndex + " Y" + targetY);
        ToServer(Pkg().SetupCmd(UseFloor, "", "", "", liftId ?? "", floorIndex, targetY, false, Vector3i.zero, false, null, 0f));
    }

    public static void SendDoors(string liftId, bool open)
    {
        ToServer(Pkg().SetupCmd(Doors, "", "", "", liftId ?? "", 0, 0, false, Vector3i.zero, open, null, 0f));
    }

    public static void BroadcastConfig()
    {
        if (!IsAuthority) return;
        try
        {
            var json = HSSideLiftConfiguration.ToSyncJson();
            ToClients(Pkg().SetupCmd(Config, json, "", "", "", 0, 0, false, Vector3i.zero, false, null, 0f));
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Broadcast lift list failed", e);
        }
    }

    public static void SendConfigTo(ClientInfo ci)
    {
        if (!IsAuthority || ci == null) return;
        try
        {
            var json = HSSideLiftConfiguration.ToSyncJson();
            ToClient(ci, Pkg().SetupCmd(Config, json, "", "", "", 0, 0, false, Vector3i.zero, false, null, 0f));
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Send lift list failed", e);
        }
    }

    public static void BroadcastMoveStart(string liftId, int fromY, int targetY, float curY, List<HSSideLiftCell> cells, string musicFile)
    {
        if (!IsAuthority) return;
        ToClients(Pkg().SetupCmd(MoveStart, musicFile ?? "", "", "", liftId ?? "", 0, targetY, true, new Vector3i(0, fromY, 0), false, cells, curY));
    }

    public static void SendMoveStartTo(ClientInfo ci, string liftId, int fromY, int targetY, float curY, List<HSSideLiftCell> cells, string musicFile)
    {
        if (!IsAuthority || ci == null) return;
        ToClient(ci, Pkg().SetupCmd(MoveStart, musicFile ?? "", "", "", liftId ?? "", 0, targetY, true, new Vector3i(0, fromY, 0), false, cells, curY));
    }

    public static void BroadcastMoveEnd(string liftId, int y)
    {
        if (!IsAuthority) return;
        ToClients(Pkg().SetupCmd(MoveEnd, "", "", "", liftId ?? "", 0, y, false, Vector3i.zero, false, null, 0f));
    }

    public static void ReplyTip(ClientInfo ci, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (ci != null)
        {
            ToClient(ci, Pkg().SetupCmd(Tip, msg, "", "", "", 0, 0, false, Vector3i.zero, false, null, 0f));
            return;
        }
        TellLocal(msg);
    }

    public static string NotReady(string problem)
    {
        if (string.IsNullOrEmpty(problem)) return null;
        return string.Format(Localization.Get("HSSideLiftNotReady"), problem);
    }

    public static void TellLocal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var locals = world != null ? world.GetLocalPlayers() : null;
            if (locals == null) return;
            for (int i = 0; i < locals.Count; i++)
            {
                var p = locals[i] as EntityPlayerLocal;
                if (p != null) GameManager.ShowTooltip(p, msg);
            }
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Warn("Local tip failed: " + e.Message);
        }
    }

    public static void TellPlayer(EntityPlayer player, string msg)
    {
        if (string.IsNullOrEmpty(msg) || player == null) return;
        var local = player as EntityPlayerLocal;
        if (local != null)
        {
            GameManager.ShowTooltip(local, msg);
            return;
        }
        try { GameManager.ShowTooltipMP(player, msg); }
        catch { }
    }

    public static EntityPlayer PlayerFromSender(ClientInfo ci, World world)
    {
        if (ci == null || world == null) return null;
        return world.GetEntity(ci.entityId) as EntityPlayer;
    }

    public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!IsAuthority || data.ClientInfo == null) return;
        SendConfigTo(data.ClientInfo);
        HSSideLiftController.SendActiveMoves(data.ClientInfo);
    }
}

public abstract class NetPackageHSSideLiftCore : NetPackage
{
    protected byte kind;
    protected string text;
    protected string arg;
    protected string extra;
    protected string liftId;
    protected int floorIndex;
    protected int targetY;
    protected bool hasPos;
    protected Vector3i pos;
    protected bool flag;
    protected float curY;
    protected List<HSSideLiftCell> cells;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.Both; } }

    public NetPackageHSSideLiftCore SetupCmd(byte k, string t, string a, string e, string id, int floor, int y, bool has, Vector3i p, bool f, List<HSSideLiftCell> c, float cy)
    {
        kind = k;
        text = t ?? "";
        arg = a ?? "";
        extra = e ?? "";
        liftId = id ?? "";
        floorIndex = floor;
        targetY = y;
        hasPos = has;
        pos = p;
        flag = f;
        curY = cy;
        cells = c;
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        kind = br.ReadByte();
        text = br.ReadString();
        arg = br.ReadString();
        extra = br.ReadString();
        liftId = br.ReadString();
        floorIndex = br.ReadInt32();
        targetY = br.ReadInt32();
        hasPos = br.ReadBoolean();
        pos = new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
        flag = br.ReadBoolean();
        curY = br.ReadSingle();
        int n = br.ReadInt32();
        int texN = br.ReadInt32();
        cells = null;
        if (n <= 0) return;
        cells = new List<HSSideLiftCell>(n);
        for (int i = 0; i < n; i++)
        {
            var cell = new HSSideLiftCell();
            cell.Dx = br.ReadInt32();
            cell.Dy = br.ReadInt32();
            cell.Dz = br.ReadInt32();
            uint raw = br.ReadUInt32();
            int dmg = br.ReadInt32();
            cell.Bv = new BlockValue(raw, dmg);
            cell.Density = br.ReadSByte();
            var tex = TextureFullArray.Default;
            for (int t = 0; t < texN; t++)
            {
                long v = br.ReadInt64();
                if (t < 32) tex[t] = v;
            }
            cell.Tex = tex;
            cells.Add(cell);
        }
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(kind);
        bw.Write(text ?? "");
        bw.Write(arg ?? "");
        bw.Write(extra ?? "");
        bw.Write(liftId ?? "");
        bw.Write(floorIndex);
        bw.Write(targetY);
        bw.Write(hasPos);
        bw.Write(pos.x);
        bw.Write(pos.y);
        bw.Write(pos.z);
        bw.Write(flag);
        bw.Write(curY);
        int n = cells != null ? cells.Count : 0;
        bw.Write(n);
        int texN = n > 0 ? HSSideLiftCar.NetTexChannels : 0;
        bw.Write(texN);
        if (n == 0) return;
        for (int i = 0; i < n; i++)
        {
            var c = cells[i];
            bw.Write(c.Dx);
            bw.Write(c.Dy);
            bw.Write(c.Dz);
            bw.Write(c.Bv.rawData);
            bw.Write(c.Bv.damage);
            bw.Write(c.Density);
            for (int t = 0; t < texN; t++)
                bw.Write(c.Tex[t]);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        try
        {
            if (world == null) return;
            switch (kind)
            {
                case HSSideLiftNet.Config:
                    if (HSSideLiftNet.IsAuthority) return;
                    HSSideLiftConfiguration.ApplyFromServer(text);
                    break;
                case HSSideLiftNet.Setup:
                    if (!HSSideLiftNet.IsAuthority) return;
                    HSSideLiftSetup.HasForcedAim = hasPos;
                    HSSideLiftSetup.ForcedAim = pos;
                    string result;
                    try { result = HSSideLiftSetup.Execute(text, arg, extra, null); }
                    finally { HSSideLiftSetup.HasForcedAim = false; }
                    HSSideLiftNet.ReplyTip(Sender, result);
                    break;
                case HSSideLiftNet.UsePanel:
                    if (!HSSideLiftNet.IsAuthority) return;
                    HSSideLiftNet.ReplyTip(Sender, HSSideLiftNet.NotReady(HSSideLiftController.RequestFromOutsidePanel(pos)));
                    break;
                case HSSideLiftNet.UseInside:
                    if (!HSSideLiftNet.IsAuthority) return;
                    HSSideLiftDebug.Info("Got UseInside floor " + floorIndex + " at " + pos);
                    HSSideLiftNet.ReplyTip(Sender, HSSideLiftNet.NotReady(HSSideLiftController.RequestFromInsidePanel(pos, floorIndex)));
                    break;
                case HSSideLiftNet.UseDoor:
                    if (!HSSideLiftNet.IsAuthority) return;
                    HSSideLiftNet.ReplyTip(Sender, HSSideLiftNet.NotReady(HSSideLiftController.RequestFromLockedDoor(pos)));
                    break;
                case HSSideLiftNet.UseFloor:
                    if (!HSSideLiftNet.IsAuthority) return;
                    if (!string.IsNullOrEmpty(liftId))
                    {
                        var lift = HSSideLiftConfiguration.ById(liftId);
                        if (lift != null) HSSideLiftConfiguration.Use(lift);
                    }
                    if (floorIndex >= 0)
                        HSSideLiftNet.ReplyTip(Sender, HSSideLiftNet.NotReady(HSSideLiftController.RequestInsideFloor(floorIndex, "remote")));
                    else
                        HSSideLiftNet.ReplyTip(Sender, HSSideLiftNet.NotReady(HSSideLiftController.RequestMoveToY(targetY, "remote")));
                    break;
                case HSSideLiftNet.Doors:
                    if (!HSSideLiftNet.IsAuthority) return;
                    if (!string.IsNullOrEmpty(liftId))
                    {
                        var doorLift = HSSideLiftConfiguration.ById(liftId);
                        if (doorLift != null) HSSideLiftConfiguration.Use(doorLift);
                    }
                    if (flag) HSSideLiftDoors.OpenAtCar(world);
                    else HSSideLiftDoors.CloseAtCar(world);
                    break;
                case HSSideLiftNet.MoveStart:
                    if (HSSideLiftNet.IsAuthority) return;
                    HSSideLiftController.BeginRemoteMove(liftId, pos.y, targetY, curY, cells, text);
                    break;
                case HSSideLiftNet.MoveEnd:
                    if (HSSideLiftNet.IsAuthority) return;
                    HSSideLiftController.FinishRemoteMove(liftId, targetY);
                    break;
                case HSSideLiftNet.Tip:
                    HSSideLiftNet.TellLocal(text);
                    break;
            }
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Net package failed (" + kind + ")", e);
        }
    }

}

[HarmonyPatch(typeof(NetPackageManager), "SetupBaseMapping")]
public static class HSSideLiftNetRegister
{
    static void Postfix()
    {
        HSSideLiftNet.RegisterPackage();
    }
}
