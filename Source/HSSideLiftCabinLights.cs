using System;
using System.Collections.Generic;
using UnityEngine;

public static class HSSideLiftCabinLights
{
    class Flicker
    {
        public float Next = 8f;
        public float Until;
    }

    static readonly Dictionary<string, Flicker> flickers = new Dictionary<string, Flicker>();
    static readonly Dictionary<string, bool> lastOn = new Dictionary<string, bool>();

    public static bool IsLightBlock(Block b)
    {
        return b is BlockPoweredLight || b is BlockSpotlight;
    }

    public static bool InCar(HSSideLiftConfigData d, Vector3i p)
    {
        if (d == null || !d.HasCar) return false;
        int x0 = d.IsAxisZ ? d.MinX : d.CurrentY;
        int z0 = d.IsAxisZ ? d.CurrentY : d.MinZ;
        if (p.x < x0 || p.x >= x0 + d.SizeX || p.z < z0 || p.z >= z0 + d.SizeZ) return false;
        int h = d.IsVehicleType ? Math.Max(2, d.SizeY) : Math.Max(1, d.SizeY);
        if (p.y < d.ParkY || p.y >= d.ParkY + h) return false;
        if (d.ExcludedColumns == null) return true;
        for (int i = 0; i < d.ExcludedColumns.Count; i++)
        {
            var c = d.ExcludedColumns[i];
            if (c != null && c.Length >= 2 && c[0] == p.x && c[1] == p.z) return false;
        }
        return true;
    }

    public static void Tick(HSSideLiftConfigData d, bool parkedWorld)
    {
        if (d == null || !d.HasCar || string.IsNullOrEmpty(d.ElevatorId)) return;
        bool powered = HSSideLiftPower.CabinHasPower(d);
        bool dim = HSSideLiftSettings.FlickerLights && Pulse(d.ElevatorId, powered);
        bool want = powered && !dim;
        if (!parkedWorld)
        {
            lastOn.Remove(d.ElevatorId);
            return;
        }
        ApplyWorld(d, want);
    }

    public static bool WantOn(HSSideLiftConfigData d)
    {
        if (d == null || !HSSideLiftPower.CabinHasPower(d)) return false;
        if (!HSSideLiftSettings.FlickerLights) return true;
        Flicker f;
        if (flickers.TryGetValue(d.ElevatorId, out f) && Time.time < f.Until) return false;
        return true;
    }

    static bool Pulse(string id, bool powered)
    {
        Flicker f;
        if (!flickers.TryGetValue(id, out f))
        {
            f = new Flicker();
            f.Next = Time.time + UnityEngine.Random.Range(6f, 18f);
            flickers[id] = f;
        }
        if (!powered)
        {
            f.Until = 0f;
            return false;
        }
        if (Time.time >= f.Next)
        {
            f.Until = Time.time + UnityEngine.Random.Range(0.04f, 0.11f);
            f.Next = Time.time + UnityEngine.Random.Range(8f, 32f);
            if (UnityEngine.Random.value < 0.35f)
                f.Next = Time.time + UnityEngine.Random.Range(0.12f, 0.35f);
        }
        return Time.time < f.Until;
    }

    static void ApplyWorld(HSSideLiftConfigData d, bool wantOn)
    {
        bool prev;
        if (lastOn.TryGetValue(d.ElevatorId, out prev) && prev == wantOn) return;
        lastOn[d.ElevatorId] = wantOn;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        int x0 = d.IsAxisZ ? d.MinX : d.CurrentY;
        int z0 = d.IsAxisZ ? d.CurrentY : d.MinZ;
        int h = d.IsVehicleType ? Math.Max(2, d.SizeY) : Math.Max(1, d.SizeY);
        for (int dx = 0; dx < d.SizeX; dx++)
            for (int dz = 0; dz < d.SizeZ; dz++)
                for (int dy = 0; dy < h; dy++)
                {
                    var p = new Vector3i(x0 + dx, d.ParkY + dy, z0 + dz);
                    if (!InCar(d, p)) continue;
                    var bv = world.GetBlock(p);
                    if (!IsLightBlock(bv.Block)) continue;
                    var te = world.GetTileEntity(p) as TileEntityPowered;
                    if (te == null) continue;
                    var tog = te as TileEntityPoweredBlock;
                    if (wantOn && tog != null && !tog.IsToggled) continue;
                    try { te.Activate(wantOn); }
                    catch { }
                }
    }
}

public class HSSideLiftMovingCabinLights : MonoBehaviour
{
    public string LiftId;
    Light[] lights;
    float[] bases;
    bool captured;

    void LateUpdate()
    {
        if (!captured)
        {
            lights = GetComponentsInChildren<Light>(true);
            bases = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                bases[i] = lights[i] != null ? lights[i].intensity : 1f;
            captured = true;
        }
        var d = string.IsNullOrEmpty(LiftId) ? HSSideLiftConfiguration.Data : HSSideLiftConfiguration.ById(LiftId);
        bool on = HSSideLiftCabinLights.WantOn(d);
        if (lights == null) return;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            lights[i].enabled = on;
            lights[i].intensity = on ? bases[i] : bases[i] * 0.08f;
        }
    }
}
