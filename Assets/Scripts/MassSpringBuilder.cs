using UnityEngine;
using System.Collections.Generic;

public class MassSpringBuilder : MonoBehaviour
{
    private MassSpringSystem system;
    private List<MassPoint> points;
    private List<Spring> springs;
    private Vector3[] originalVertices;
    private int[] originalTriangles;

    public void Initialize(MassSpringSystem system, List<MassPoint> points, List<Spring> springs, Vector3[] vertices, int[] triangles)
    {
        this.system = system;
        this.points = points;
        this.springs = springs;
        this.originalVertices = vertices;
        this.originalTriangles = triangles;
    }

    public void BuildMassSpringFull(int fillResolution, Color structuralColor, Color shearColor, Color bendingColor, Color couplingColor, bool enableShearRuntime, bool enableBendingRuntime)
    {
        var mf = system.GetComponent<MeshFilter>();
        var mesh = mf.mesh;
        var verts = mesh.vertices;
        var tris = originalTriangles;

        points.Clear();
        springs.Clear();

        var weld = new Dictionary<Vector3, MassPoint>();
        var idx2pt = new MassPoint[verts.Length];

        // Create mass points from mesh vertices (surface)
        for (int i = 0; i < verts.Length; i++)
        {
            var local = verts[i];
            if (!weld.TryGetValue(local, out var mp))
            {
                var world = system.transform.TransformPoint(local);
                mp = new MassPoint(world, local);
                weld[local] = mp;
                points.Add(mp);
                if (system.massPointPrefab != null)
                {
                    GameObject visual = Instantiate(system.massPointPrefab, mp.position, Quaternion.identity, system.transform);
                    mp.visual = visual.transform; // احفظي Transform لتحدثيه لاحقاً
                }
            }
          

            idx2pt[i] = mp;
        }

        // Voxel fill interior points inside the mesh
        var bounds = mesh.bounds;
        Vector3 minW = system.transform.TransformPoint(bounds.min);
        Vector3 maxW = system.transform.TransformPoint(bounds.max);
        Vector3 step = (maxW - minW) / fillResolution;

        for (int x = 0; x < fillResolution; x++)
            for (int y = 0; y < fillResolution; y++)
                for (int z = 0; z < fillResolution; z++)
                {
                    Vector3 wP = minW + new Vector3((x + 0.5f) * step.x, (y + 0.5f) * step.y, (z + 0.5f) * step.z);
                    if (!PointInside(wP, verts, tris)) continue;

                    Vector3 local = system.transform.InverseTransformPoint(wP);
                    if (!weld.TryGetValue(local, out var ip))
                    {
                        ip = new MassPoint(wP, local);
                        weld[local] = ip;
                        points.Add(ip);
                    }
                }

        var connected = new HashSet<(MassPoint, MassPoint)>();

        // Structural springs (edges of mesh triangles)
        for (int i = 0; i < tris.Length; i += 3)
        {
            TryAddSpring(idx2pt[tris[i + 0]], idx2pt[tris[i + 1]], structuralColor, connected);
            TryAddSpring(idx2pt[tris[i + 1]], idx2pt[tris[i + 2]], structuralColor, connected);
            TryAddSpring(idx2pt[tris[i + 2]], idx2pt[tris[i + 0]], structuralColor, connected);
        }

        // Shear springs if enabled
        if (enableShearRuntime)
            AddShearSprings(idx2pt, tris, connected);

        // Bending springs if enabled
        if (enableBendingRuntime)
            CreateBendingSprings(connected);

        // Grid-based springs (coupling)
        var grid = new Dictionary<Vector3Int, MassPoint>();
        foreach (var mp in points)
        {
            var idx = new Vector3Int(
                Mathf.FloorToInt(mp.restPosition.x / step.x),
                Mathf.FloorToInt(mp.restPosition.y / step.y),
                Mathf.FloorToInt(mp.restPosition.z / step.z)
            );
            grid[idx] = mp;
        }

        var offs = new[] { Vector3Int.right, Vector3Int.up, new Vector3Int(0, 0, 1) };
        foreach (var kv in grid)
            foreach (var d in offs)
                if (grid.TryGetValue(kv.Key + d, out var nb))
                    TryAddSpring(kv.Value, nb, shearColor, connected);

        // Extra springs for solid materials to hold shape better
        if (system.material == MassSpringSystem.MaterialType.Solid)
        {
            var interior = new List<MassPoint>(grid.Values);
            int k = 12;
            float cutoff = step.magnitude * 1.5f;

            for (int i = 0; i < interior.Count; i++)
            {
                var a = interior[i];
                var dists = new List<(float d2, MassPoint mp)>(interior.Count - 1);
                for (int j = 0; j < interior.Count; j++)
                {
                    if (i == j) continue;
                    var b = interior[j];
                    float d2 = (a.restPosition - b.restPosition).sqrMagnitude;
                    if (d2 <= cutoff * cutoff)
                        dists.Add((d2, b));
                }
                dists.Sort((x, y) => x.d2.CompareTo(y.d2));
                int take = Mathf.Min(k, dists.Count);
                for (int n = 0; n < take; n++)
                    TryAddSpring(a, dists[n].mp, structuralColor, connected);
            }
          
        }

        // Coupling springs to connect surface to interior
        float maxd2 = (step.magnitude * 2f);
        maxd2 *= maxd2;
        foreach (var surf in weld.Values)
        {
            Vector3 sw = system.transform.TransformPoint(surf.restPosition);
            MassPoint best = null;
            float bd = maxd2;
            foreach (var ip in points)
            {
                Vector3 iw = system.transform.TransformPoint(ip.restPosition);
                float d2 = (sw - iw).sqrMagnitude;
                if (d2 > 0f && d2 < bd)
                {
                    bd = d2;
                    best = ip;
                }
            }
            if (best != null)
                TryAddSpring(surf, best, couplingColor, connected);
        }
    }

    bool PointInside(Vector3 wP, Vector3[] verts, int[] tris)
    {
        var ray = new Ray(wP, Vector3.up);
        int hits = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 A = system.transform.TransformPoint(verts[tris[i]]);
            Vector3 B = system.transform.TransformPoint(verts[tris[i + 1]]);
            Vector3 C = system.transform.TransformPoint(verts[tris[i + 2]]);
            if (RayTri(ray, A, B, C)) hits++;
        }
        return (hits & 1) == 1;
    }

    static bool RayTri(Ray ray, Vector3 a, Vector3 b, Vector3 c)
    {
        const float EPS = 1e-6f;
        var e1 = b - a; var e2 = c - a;
        var P = Vector3.Cross(ray.direction, e2);
        var det = Vector3.Dot(e1, P);
        if (Mathf.Abs(det) < EPS) return false;
        var inv = 1f / det;
        var T = ray.origin - a;
        var u = Vector3.Dot(T, P) * inv;
        if (u < 0 || u > 1) return false;
        var Q = Vector3.Cross(T, e1);
        var v = Vector3.Dot(ray.direction, Q) * inv;
        if (v < 0 || u + v > 1) return false;
        var t = Vector3.Dot(e2, Q) * inv;
        return t > EPS;
    }

    public void DestroySystem()
    {
        if (points != null && springs != null)
        {
            points.Clear();
            springs.Clear();
        }
    }

    void TryAddSpring(MassPoint a, MassPoint b, Color color, HashSet<(MassPoint, MassPoint)> connected)
    {
        var key = (a, b);
        var rev = (b, a);
        if (connected.Contains(key) || connected.Contains(rev))
            return;
        connected.Add(key);
        springs.Add(new Spring(a, b, color));
    }

    void CreateBendingSprings(HashSet<(MassPoint, MassPoint)> connected)
    {
        var adjacencyMap = new Dictionary<MassPoint, List<MassPoint>>();
        foreach (var spring in springs)
        {
            if (!adjacencyMap.ContainsKey(spring.p1))
                adjacencyMap[spring.p1] = new List<MassPoint>();
            if (!adjacencyMap.ContainsKey(spring.p2))
                adjacencyMap[spring.p2] = new List<MassPoint>();
            adjacencyMap[spring.p1].Add(spring.p2);
            adjacencyMap[spring.p2].Add(spring.p1);
        }
        foreach (var kvp in adjacencyMap)
        {
            var center = kvp.Key;
            var neighbors = kvp.Value;
            for (int i = 0; i < neighbors.Count; i++)
            {
                for (int j = i + 1; j < neighbors.Count; j++)
                {
                    TryAddSpring(neighbors[i], neighbors[j], system.bendingColor, connected);
                }
            }
        }
    }

    void AddShearSprings(MassPoint[] indexToPoint, int[] triangles, HashSet<(MassPoint, MassPoint)> connected)
    {
        var edgeToTris = new Dictionary<(int, int), List<int>>();
        void AddEdge(int x, int y, int t)
        {
            var e = x < y ? (x, y) : (y, x);
            if (!edgeToTris.TryGetValue(e, out var lst)) { lst = new List<int>(); edgeToTris[e] = lst; }
            lst.Add(t);
        }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            AddEdge(triangles[i + 0], triangles[i + 1], i);
            AddEdge(triangles[i + 1], triangles[i + 2], i);
            AddEdge(triangles[i + 2], triangles[i + 0], i);
        }
        foreach (var kv in edgeToTris)
        {
            var shared = kv.Value;
            if (shared.Count != 2) continue;
            int t1 = shared[0], t2 = shared[1];
            var tri1 = new[] { triangles[t1 + 0], triangles[t1 + 1], triangles[t1 + 2] };
            var tri2 = new[] { triangles[t2 + 0], triangles[t2 + 1], triangles[t2 + 2] };
            var common = new HashSet<int>(tri1);
            common.IntersectWith(tri2);
            if (common.Count != 2) continue;
            int v1 = -1, v2 = -1;
            foreach (var v in tri1) if (!common.Contains(v)) v1 = v;
            foreach (var v in tri2) if (!common.Contains(v)) v2 = v;
            if (v1 >= 0 && v2 >= 0)
                TryAddSpring(indexToPoint[v1], indexToPoint[v2], system.shearColor, connected);
        }
    }
}
