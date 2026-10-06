using System.Collections.Generic;
using UnityEngine;

// Cable-ferry rope: a line from dock A to dock B through the pad. Docks stay put; the pad slides along it.
public static class HSSideLiftCable
{
    static readonly Dictionary<string, LineRenderer> lines = new Dictionary<string, LineRenderer>();
    static Material mat;

    public static void Tick(HSSideLiftConfigData d)
    {
        if (d == null || GameManager.IsDedicatedServer)
        {
            if (d != null) Hide(d.ElevatorId);
            return;
        }
        Vector3 a, b;
        if (!d.IsBoatType || !d.HasCar || !Ends(d, out a, out b))
        {
            Hide(d.ElevatorId);
            return;
        }
        var lr = GetOrCreate(d.ElevatorId);
        if (lr == null) return;
        var origin = Origin.position;
        lr.positionCount = 2;
        lr.SetPosition(0, a - origin);
        lr.SetPosition(1, b - origin);
        if (!lr.gameObject.activeSelf) lr.gameObject.SetActive(true);
    }

    public static void DisposeAll()
    {
        foreach (var kv in lines)
        {
            if (kv.Value == null) continue;
            var go = kv.Value.gameObject;
            if (go != null) Object.Destroy(go);
        }
        lines.Clear();
    }

    static bool Ends(HSSideLiftConfigData d, out Vector3 a, out Vector3 b)
    {
        a = Vector3.zero;
        b = Vector3.zero;
        if (d.Floors == null || d.Floors.Count < 2) return false;
        int lo = d.Floors[0].Y;
        int hi = d.Floors[d.Floors.Count - 1].Y;
        if (lo == hi) return false;
        float y = d.ParkY + 0.5f;
        if (d.IsAxisZ)
        {
            float x = d.MinX + d.SizeX * 0.5f;
            a = new Vector3(x, y, lo + d.SizeZ * 0.5f);
            b = new Vector3(x, y, hi + d.SizeZ * 0.5f);
        }
        else
        {
            float z = d.MinZ + d.SizeZ * 0.5f;
            a = new Vector3(lo + d.SizeX * 0.5f, y, z);
            b = new Vector3(hi + d.SizeX * 0.5f, y, z);
        }
        return true;
    }

    static LineRenderer GetOrCreate(string id)
    {
        LineRenderer lr;
        if (lines.TryGetValue(id, out lr) && lr != null) return lr;
        var go = new GameObject("HSSideLiftCable_" + id);
        Object.DontDestroyOnLoad(go);
        lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCapVertices = 4;
        lr.textureMode = LineTextureMode.Stretch;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.material = RopeMaterial();
        lr.startColor = RopeColor();
        lr.endColor = RopeColor();
        lines[id] = lr;
        return lr;
    }

    static void Hide(string id)
    {
        LineRenderer lr;
        if (string.IsNullOrEmpty(id) || !lines.TryGetValue(id, out lr) || lr == null) return;
        if (lr.gameObject.activeSelf) lr.gameObject.SetActive(false);
    }

    static Color RopeColor()
    {
        return new Color(0.38f, 0.24f, 0.12f, 1f);
    }

    static Material RopeMaterial()
    {
        if (mat != null) return mat;
        var shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
        mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", RopeColor());
        return mat;
    }
}
