using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelMassSpringifier : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Number of voxels per axis")]
    public int resolution = 32;

    [Header("Mass Point Settings")]
    public GameObject massPointPrefab;

    [Header("Spring Settings")]
    [Tooltip("Width of the spring lines")]
    public float springLineWidth = 0.02f;
    [Tooltip("Color of the structural springs")]
    public Color springColor = Color.yellow;

    [Header("Debug")]
    [Tooltip("Draw up to this many debug rays")]
    public int debugDrawCount = 200;
    [Tooltip("Seconds to display debug gizmos")]
    public float debugDuration = 5f;

    // world‐space mesh
    private Vector3[] worldVerts;
    private int[] tris;

    // storage for all instantiated mass‐points
    private Vector3 worldMin, worldMax, step;
    private Dictionary<int, Transform> pointMap = new Dictionary<int, Transform>();

    // neighbor offsets in grid (±X,±Y,±Z)
    private readonly Vector3Int[] neighborOffsets = new[]{
        new Vector3Int( 1,  0,  0),
        new Vector3Int(-1,  0,  0),
        new Vector3Int( 0,  1,  0),
        new Vector3Int( 0, -1,  0),
        new Vector3Int( 0,  0,  1),
        new Vector3Int( 0,  0, -1),
        // // uncomment these for full 26‐neighborhood (shear/bend)
        // new Vector3Int( 1,  1,  0), … etc.
    };

    void Start()
    {
        if (massPointPrefab == null)
        {
            Debug.LogError("Please assign a MassPoint Prefab!");
            enabled = false;
            return;
        }

        // 1) grab mesh in world‐space
        var mesh = GetComponent<MeshFilter>().mesh;
        tris = mesh.triangles;
        worldVerts = new Vector3[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; i++)
            worldVerts[i] = transform.TransformPoint(mesh.vertices[i]);

        // 2) compute grid bounds & step
        var b = mesh.bounds;
        worldMin = transform.TransformPoint(b.min);
        worldMax = transform.TransformPoint(b.max);
        step = new Vector3(
            (worldMax.x - worldMin.x) / resolution,
            (worldMax.y - worldMin.y) / resolution,
            (worldMax.z - worldMin.z) / resolution
        );

        // 3) voxelize & instantiate
        VoxelizeAndInstantiate();

        // 4) connect springs between neighbors
        BuildSprings();
    }

    void VoxelizeAndInstantiate()
    {
        int total = resolution * resolution * resolution;
        int dbg = 0;
        for (int x = 0; x < resolution; x++)
            for (int y = 0; y < resolution; y++)
                for (int z = 0; z < resolution; z++)
                {
                    Vector3 center = new Vector3(
                        worldMin.x + (x + 0.5f) * step.x,
                        worldMin.y + (y + 0.5f) * step.y,
                        worldMin.z + (z + 0.5f) * step.z
                    );

                    if (!IsPointInside(center)) continue;

                    // flat index for lookup
                    int idx = x * resolution * resolution + y * resolution + z;

                    // instantiate mass‐point
                    var go = Instantiate(massPointPrefab, center, Quaternion.identity, transform);
                    pointMap[idx] = go.transform;

                    // debug: draw a tiny ray
                    if (dbg++ < debugDrawCount)
                        Debug.DrawRay(center, Vector3.up * (step.magnitude * 0.5f), Color.green, debugDuration);
                }
        Debug.Log($"Instantiated {pointMap.Count}/{total} mass points.");
    }

    void BuildSprings()
    {
        foreach (var kv in pointMap)
        {
            int flat = kv.Key;
            Transform t = kv.Value;

            // recover grid coords
            int z = flat % resolution;
            int y = (flat / resolution) % resolution;
            int x = flat / (resolution * resolution);

            foreach (var off in neighborOffsets)
            {
                int nx = x + off.x, ny = y + off.y, nz = z + off.z;
                if (nx < 0 || nx >= resolution ||
                    ny < 0 || ny >= resolution ||
                    nz < 0 || nz >= resolution)
                    continue;

                int nidx = nx * resolution * resolution + ny * resolution + nz;
                if (!pointMap.TryGetValue(nidx, out Transform tn))
                    continue;

                // create a LineRenderer to visualize the spring
                var lrGO = new GameObject("Spring");
                lrGO.transform.parent = transform;
                var lr = lrGO.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPositions(new Vector3[] { t.position, tn.position });
                lr.startWidth = springLineWidth;
                lr.endWidth = springLineWidth;
                lr.material = new Material(Shader.Find("Unlit/Color"));
                lr.material.color = springColor;
            }
        }

        Debug.Log($"Built springs for {pointMap.Count} mass points.");
    }

    // point‐inside using ray‐cast count
    bool IsPointInside(Vector3 p)
    {
        int hits = 0;
        Vector3 dir = Vector3.up;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = worldVerts[tris[i + 0]];
            Vector3 b = worldVerts[tris[i + 1]];
            Vector3 c = worldVerts[tris[i + 2]];
            if (RayIntersectsTriangle(p, dir, a, b, c))
                hits++;
        }
        return (hits & 1) != 0;
    }

    // Möller–Trumbore intersection
    bool RayIntersectsTriangle(Vector3 orig, Vector3 dir,
                               Vector3 v0, Vector3 v1, Vector3 v2)
    {
        const float EPS = 1e-6f;
        Vector3 e1 = v1 - v0, e2 = v2 - v0;
        Vector3 h = Vector3.Cross(dir, e2);
        float a = Vector3.Dot(e1, h);
        if (a > -EPS && a < EPS) return false;
        float f = 1 / a;
        Vector3 s = orig - v0;
        float u = f * Vector3.Dot(s, h);
        if (u < 0 || u > 1) return false;
        Vector3 q = Vector3.Cross(s, e1);
        float v = f * Vector3.Dot(dir, q);
        if (v < 0 || u + v > 1) return false;
        float t = f * Vector3.Dot(e2, q);
        return t > EPS;
    }

}
