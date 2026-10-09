using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum HSSideLiftInteriorKind
{
    None,
    Floor,
    Wall,
    Ceiling
}

// Escalator-style 2D maps on the moving clone, but only on faces that look into the cabin.
// Outside faces keep the cloned vanilla / paintbrush look.
public static class HSSideLiftPaint
{
    static Texture2D floorTex, wallTex, ceilTex;
    static Material floorMat, wallMat, ceilMat;
    static bool shaderMissing;
    static bool loggedPainted;
    const string InsideChild = "hs_sidelift_inside";
    const float InDot = 0.35f;
    const float ParkInset = 0.012f;

    public static bool Apply(Transform model, HSSideLiftPinnedLooks pin, HSSideLiftInteriorKind kind, Vector3 inward0, Vector3 inward1)
    {
        if (model == null || kind == HSSideLiftInteriorKind.None) return false;
        if (MaterialFor(kind) == null) return false;
        var skin = model.gameObject.AddComponent<HSSideLiftInteriorSkin>();
        skin.Pin = pin;
        skin.Kind = kind;
        skin.Inward0 = inward0;
        skin.Inward1 = inward1;
        skin.Paint();
        return true;
    }

    public static int PaintAll(Transform root, HSSideLiftPinnedLooks pin, HSSideLiftInteriorKind kind, Vector3 inward0, Vector3 inward1)
    {
        var mat = MaterialFor(kind);
        if (mat == null || root == null) return 0;
        var car = pin != null ? pin.transform : null;
        int painted = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || r.name == InsideChild) continue;
            if (r.transform.Find(InsideChild) != null) { painted++; continue; }
            var mf = r.GetComponent<MeshFilter>();
            var src = mf != null ? mf.sharedMesh : null;
            if (src == null || src.vertexCount < 3 || !src.isReadable) continue;

            Mesh outside;
            Mesh inside;
            if (!SplitInside(src, r.transform, car, inward0, inward1, MeshTag(kind), out outside, out inside))
                continue;
            if (outside != null)
            {
                mf.sharedMesh = outside;
                if (pin != null) pin.Own(outside);
            }
            if (inside != null)
            {
                var go = new GameObject(InsideChild);
                go.transform.SetParent(r.transform, false);
                var imf = go.AddComponent<MeshFilter>();
                imf.sharedMesh = inside;
                var ir = go.AddComponent<MeshRenderer>();
                ir.sharedMaterial = mat;
                ir.SetPropertyBlock(null);
                if (pin != null) pin.Own(inside);
                if (!loggedPainted)
                {
                    loggedPainted = true;
                    HSSideLiftDebug.Info("Cabin " + kind + " on inside faces only");
                }
            }
            painted++;
        }
        return painted;
    }

    public static string Describe(Transform root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            var m = mf != null ? mf.sharedMesh : null;
            sb.Append(r.GetType().Name).Append(" '").Append(r.name).Append("' mesh=")
              .Append(m == null ? "none" : m.name + " v" + m.vertexCount + (m.isReadable ? "" : " unreadable"))
              .Append(" mat=").Append(r.sharedMaterial != null ? r.sharedMaterial.name : "none").Append("; ");
        }
        return sb.Length == 0 ? "no renderers" : sb.ToString();
    }

    static string MeshTag(HSSideLiftInteriorKind kind)
    {
        if (kind == HSSideLiftInteriorKind.Wall) return "wall";
        if (kind == HSSideLiftInteriorKind.Ceiling) return "ceiling";
        return "floor";
    }

    static bool SplitInside(Mesh src, Transform model, Transform car, Vector3 in0, Vector3 in1, string tag, out Mesh outside, out Mesh inside)
    {
        outside = null;
        inside = null;
        var verts = src.vertices;
        var norms = src.normals;
        var uvs = src.uv;
        var cols = src.colors;
        bool haveN = norms != null && norms.Length == verts.Length;
        bool haveUv = uvs != null && uvs.Length == verts.Length;
        bool haveC = cols != null && cols.Length == verts.Length;
        var b = src.bounds;
        float dx = Mathf.Max(0.0001f, b.size.x);
        float dy = Mathf.Max(0.0001f, b.size.y);
        float dz = Mathf.Max(0.0001f, b.size.z);

        var outV = new List<Vector3>();
        var outN = new List<Vector3>();
        var outUv = new List<Vector2>();
        var outC = new List<Color>();
        var outI = new List<int>();
        var inV = new List<Vector3>();
        var inN = new List<Vector3>();
        var inUv = new List<Vector2>();
        var inC = new List<Color>();
        var inI = new List<int>();

        int subs = Math.Max(1, src.subMeshCount);
        for (int s = 0; s < subs; s++)
        {
            var tris = src.GetTriangles(s);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                if (i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length) continue;
                var fn = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
                if (fn.sqrMagnitude < 1e-12f && haveN)
                    fn = norms[i0] + norms[i1] + norms[i2];
                bool inner = FacesInside(fn, model, car, in0, in1);
                if (inner)
                    PushTri(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i0, i1, i2, true, inV, inN, inUv, inC, inI);
                else
                    PushTri(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i0, i1, i2, false, outV, outN, outUv, outC, outI);
            }
        }

        if (outI.Count >= 3)
        {
            outside = MakeMesh("hs_sidelift_out_" + tag, outV, outN, outUv, outC, outI);
            if (!haveN) outside.RecalculateNormals();
        }
        if (inI.Count >= 3)
        {
            inside = MakeMesh("hs_sidelift_in_" + tag, inV, inN, inUv, inC, inI);
            if (!haveN) inside.RecalculateNormals();
        }
        return outside != null || inside != null;
    }

    static bool FacesInside(Vector3 localFace, Transform model, Transform car, Vector3 in0, Vector3 in1)
    {
        var n = model != null ? model.TransformDirection(localFace) : localFace;
        if (car != null) n = car.InverseTransformDirection(n);
        if (n.sqrMagnitude < 1e-10f) return false;
        n.Normalize();
        if (in0.sqrMagnitude > 0.01f && Vector3.Dot(n, in0.normalized) > InDot) return true;
        if (in1.sqrMagnitude > 0.01f && Vector3.Dot(n, in1.normalized) > InDot) return true;
        return false;
    }

    static void PushTri(Vector3[] verts, Vector3[] norms, Vector2[] uvs, Color[] cols,
        bool haveN, bool haveUv, bool haveC, Bounds b, float dx, float dy, float dz,
        int i0, int i1, int i2, bool inner,
        List<Vector3> dv, List<Vector3> dn, List<Vector2> du, List<Color> dc, List<int> di)
    {
        int baseI = dv.Count;
        PushVert(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i0, inner, dv, dn, du, dc);
        PushVert(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i1, inner, dv, dn, du, dc);
        PushVert(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i2, inner, dv, dn, du, dc);
        di.Add(baseI);
        di.Add(baseI + 1);
        di.Add(baseI + 2);
    }

    static void PushVert(Vector3[] verts, Vector3[] norms, Vector2[] uvs, Color[] cols,
        bool haveN, bool haveUv, bool haveC, Bounds b, float dx, float dy, float dz,
        int i, bool inner,
        List<Vector3> dv, List<Vector3> dn, List<Vector2> du, List<Color> dc)
    {
        var p = verts[i];
        dv.Add(p);
        var n = haveN ? norms[i] : (p - b.center);
        dn.Add(n);
        if (inner)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az)
                du.Add(new Vector2((p.x - b.min.x) / dx, (p.z - b.min.z) / dz));
            else if (ax >= az)
                du.Add(new Vector2((p.z - b.min.z) / dz, (p.y - b.min.y) / dy));
            else
                du.Add(new Vector2((p.x - b.min.x) / dx, (p.y - b.min.y) / dy));
            dc.Add(Color.white);
        }
        else
        {
            du.Add(haveUv ? uvs[i] : Vector2.zero);
            dc.Add(haveC ? cols[i] : Color.white);
        }
    }

    static Mesh MakeMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<Color> c, List<int> tris)
    {
        var mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetColors(c);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    public static GameObject BuildParked(World world, HSSideLiftConfigData d, int along)
    {
        if (world == null || d == null || !d.HasCar) return null;
        var floors = new List<Vector3>();
        var floorUv = new List<Vector2>();
        var floorI = new List<int>();
        var walls = new List<Vector3>();
        var wallUv = new List<Vector2>();
        var wallI = new List<int>();
        var ceils = new List<Vector3>();
        var ceilUv = new List<Vector2>();
        var ceilI = new List<int>();
        int h = HSSideLiftCar.ParkedHeight;
        int x0 = HSSideLiftCar.OriginX(along);
        int z0 = HSSideLiftCar.OriginZ(along);
        for (int dx = 0; dx < d.SizeX; dx++)
        for (int dz = 0; dz < d.SizeZ; dz++)
        for (int dy = 0; dy < h; dy++)
        {
            if (HSSideLiftCar.IsExcludedCell(x0 + dx, dy, z0 + dz)) continue;
            var pos = new Vector3i(x0 + dx, d.ParkY + dy, z0 + dz);
            var bv = world.GetBlock(pos);
            if (bv.isair || bv.ischild || bv.Block == null) continue;
            if (bv.Block is BlockHSSideLiftInsidePanel || bv.Block is BlockHSSideLiftOutsidePanel) continue;
            if (HSSideLiftDoors.IsElevatorDoor(bv.Block)) continue;
            Vector3 in0, in1;
            var kind = KindAt(d, dx, dy, dz, out in0, out in1);
            if (kind == HSSideLiftInteriorKind.None) continue;
            var o = new Vector3(dx, dy, dz);
            if (kind == HSSideLiftInteriorKind.Floor)
                PushFace(floors, floorUv, floorI, o, Vector3.up);
            else if (kind == HSSideLiftInteriorKind.Ceiling)
                PushFace(ceils, ceilUv, ceilI, o, Vector3.down);
            else
            {
                if (in0.sqrMagnitude > 0.01f) PushFace(walls, wallUv, wallI, o, in0.normalized);
                if (in1.sqrMagnitude > 0.01f) PushFace(walls, wallUv, wallI, o, in1.normalized);
            }
        }
        var root = new GameObject("HSSideLiftParkedInterior_" + d.ElevatorId);
        int n = 0;
        n += AttachMesh(root.transform, "floor", floors, floorUv, floorI, MaterialFor(HSSideLiftInteriorKind.Floor));
        n += AttachMesh(root.transform, "wall", walls, wallUv, wallI, MaterialFor(HSSideLiftInteriorKind.Wall));
        n += AttachMesh(root.transform, "ceiling", ceils, ceilUv, ceilI, MaterialFor(HSSideLiftInteriorKind.Ceiling));
        if (n == 0)
        {
            UnityEngine.Object.Destroy(root);
            return null;
        }
        return root;
    }

    static HSSideLiftInteriorKind KindAt(HSSideLiftConfigData d, int dx, int dy, int dz, out Vector3 inward0, out Vector3 inward1)
    {
        inward0 = Vector3.zero;
        inward1 = Vector3.zero;
        bool west = dx == 0;
        bool east = d.SizeX > 1 && dx == d.SizeX - 1;
        bool south = dz == 0;
        bool north = d.SizeZ > 1 && dz == d.SizeZ - 1;
        if (dy <= 0)
        {
            inward0 = Vector3.up;
            return HSSideLiftInteriorKind.Floor;
        }
        if (d.SizeY >= 2 && dy >= d.SizeY - 1)
        {
            inward0 = Vector3.down;
            return HSSideLiftInteriorKind.Ceiling;
        }
        if (!west && !east && !south && !north) return HSSideLiftInteriorKind.None;
        if (west) inward0 = Vector3.right;
        else if (east) inward0 = Vector3.left;
        if (south)
        {
            if (inward0.sqrMagnitude < 0.01f) inward0 = Vector3.forward;
            else inward1 = Vector3.forward;
        }
        else if (north)
        {
            if (inward0.sqrMagnitude < 0.01f) inward0 = Vector3.back;
            else inward1 = Vector3.back;
        }
        return HSSideLiftInteriorKind.Wall;
    }

    static void PushFace(List<Vector3> v, List<Vector2> uv, List<int> tris, Vector3 cell, Vector3 inward)
    {
        float e = ParkInset;
        Vector3 a, b, c, d;
        if (inward.y > 0.5f)
        {
            a = cell + new Vector3(e, 1f - e, e);
            b = cell + new Vector3(e, 1f - e, 1f - e);
            c = cell + new Vector3(1f - e, 1f - e, 1f - e);
            d = cell + new Vector3(1f - e, 1f - e, e);
        }
        else if (inward.y < -0.5f)
        {
            a = cell + new Vector3(e, e, e);
            b = cell + new Vector3(1f - e, e, e);
            c = cell + new Vector3(1f - e, e, 1f - e);
            d = cell + new Vector3(e, e, 1f - e);
        }
        else if (inward.x > 0.5f)
        {
            a = cell + new Vector3(1f - e, e, e);
            b = cell + new Vector3(1f - e, e, 1f - e);
            c = cell + new Vector3(1f - e, 1f - e, 1f - e);
            d = cell + new Vector3(1f - e, 1f - e, e);
        }
        else if (inward.x < -0.5f)
        {
            a = cell + new Vector3(e, e, e);
            b = cell + new Vector3(e, 1f - e, e);
            c = cell + new Vector3(e, 1f - e, 1f - e);
            d = cell + new Vector3(e, e, 1f - e);
        }
        else if (inward.z > 0.5f)
        {
            a = cell + new Vector3(e, e, 1f - e);
            b = cell + new Vector3(1f - e, e, 1f - e);
            c = cell + new Vector3(1f - e, 1f - e, 1f - e);
            d = cell + new Vector3(e, 1f - e, 1f - e);
        }
        else
        {
            a = cell + new Vector3(e, e, e);
            b = cell + new Vector3(e, 1f - e, e);
            c = cell + new Vector3(1f - e, 1f - e, e);
            d = cell + new Vector3(1f - e, e, e);
        }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        uv.Add(new Vector2(0f, 0f));
        uv.Add(new Vector2(0f, 1f));
        uv.Add(new Vector2(1f, 1f));
        uv.Add(new Vector2(1f, 0f));
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }

    static int AttachMesh(Transform parent, string name, List<Vector3> v, List<Vector2> uv, List<int> tris, Material mat)
    {
        if (mat == null || tris.Count < 3) return 0;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        var mesh = new Mesh();
        mesh.name = "hs_sidelift_parked_" + name;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetPropertyBlock(null);
        return 1;
    }

    static Material MaterialFor(HSSideLiftInteriorKind kind)
    {
        if (kind == HSSideLiftInteriorKind.Floor) return Mat(ref floorMat, ref floorTex, "hs_lift_floor.png", "hs_lift_floor");
        if (kind == HSSideLiftInteriorKind.Wall) return Mat(ref wallMat, ref wallTex, "hs_lift_wall.png", "hs_lift_wall");
        if (kind == HSSideLiftInteriorKind.Ceiling) return Mat(ref ceilMat, ref ceilTex, "hs_lift_ceiling.png", "hs_lift_ceiling");
        return null;
    }

    static Material Mat(ref Material slot, ref Texture2D tex, string file, string name)
    {
        if (slot != null) return slot;
        if (shaderMissing) return null;
        if (tex == null) tex = Load(file, name);
        if (tex == null) return null;
        var sh = StepShader();
        if (sh == null)
        {
            shaderMissing = true;
            HSSideLiftDebug.Warn("No 2D shader for cabin interior; blocks keep the vanilla look.");
            return null;
        }
        var m = new Material(sh) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        m.name = name + "_mat";
        m.mainTexture = tex;
        m.renderQueue = 3000;
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        slot = m;
        HSSideLiftDebug.Info("Cabin material " + name + " " + sh.name);
        return slot;
    }

    static Texture2D Load(string file, string name)
    {
        try
        {
            var root = HSSideLiftMod.ModPath ?? "";
            var path = Path.Combine(root, "Resources", file);
            if (!File.Exists(path))
            {
                HSSideLiftDebug.Warn("Cabin texture missing: " + path);
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            HSSideLiftDebug.Info("Loaded cabin " + name + " " + tex.width + "x" + tex.height);
            return tex;
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Warn("Could not load " + file + ": " + e.Message);
            return null;
        }
    }

    static Shader StepShader()
    {
        var names = new[]
        {
            "Unlit/Texture",
            "Legacy Shaders/Diffuse",
            "Mobile/Diffuse",
            "Legacy Shaders/VertexLit"
        };
        for (int i = 0; i < names.Length; i++)
        {
            var s = Shader.Find(names[i]);
            if (s != null && s.isSupported && s.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) < 0)
                return s;
        }
        return null;
    }
}

public class HSSideLiftInteriorSkin : MonoBehaviour
{
    public HSSideLiftPinnedLooks Pin;
    public HSSideLiftInteriorKind Kind;
    public Vector3 Inward0;
    public Vector3 Inward1;
    float nextCheck;
    float born = -1f;
    bool reported;
    static int reports;

    public void Paint()
    {
        if (born < 0f) born = Time.time;
        int n = HSSideLiftPaint.PaintAll(transform, Pin, Kind, Inward0, Inward1);
        if (n == 0 && !reported && Time.time - born > 3f)
        {
            reported = true;
            if (reports++ < 3)
                HSSideLiftDebug.Warn("Cabin clone has nothing to paint (" + Kind + "): " + HSSideLiftPaint.Describe(transform));
        }
    }

    void LateUpdate()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        Paint();
    }
}
