using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class VoxelMeshFiller : MonoBehaviour
{
    [Header("Sampling (local space)")]
    [Tooltip("Distance between voxel centers in local units.")]
    public float voxelSize = 0.15f;
    [Range(0, 1), Tooltip("Random jitter fraction of voxelSize.")]
    public float jitterAmount = 0.3f;

    [Header("Mass Point Prefab & Spring Color")]
    public GameObject massPointPrefab;
    public Color springColor = Color.yellow;

    [HideInInspector] public List<Transform> spawnedPoints = new List<Transform>();
    [HideInInspector] public List<(Transform a, Transform b)> spawnedSprings = new List<(Transform, Transform)>();

    private Mesh _mesh;
    private Vector3[] _verts;
    private int[] _tris;

    void Awake()
    {
        // Cache mesh data in local space
        _mesh = GetComponent<MeshFilter>().sharedMesh;
        _verts = _mesh.vertices;
        _tris = _mesh.triangles;
    }

    [ContextMenu("Rebuild Voxel Fill")]
    public void BuildFill()
    {
        ClearFill();

        // 1) Sample in mesh.bounds (local coordinates)
        Bounds b = _mesh.bounds;
        var cellToPoint = new Dictionary<Vector3Int, Transform>();

        for (float x = b.min.x; x <= b.max.x; x += voxelSize)
            for (float y = b.min.y; y <= b.max.y; y += voxelSize)
                for (float z = b.min.z; z <= b.max.z; z += voxelSize)
                {
                    // jitter in local space
                    Vector3 localSample = new Vector3(
                        x + (Random.value - 0.5f) * jitterAmount * voxelSize,
                        y + (Random.value - 0.5f) * jitterAmount * voxelSize,
                        z + (Random.value - 0.5f) * jitterAmount * voxelSize
                    );

                    Vector3 worldSample = transform.TransformPoint(localSample);

                    if (!PointIsInsideMesh(worldSample))
                        continue;

                    var go = Instantiate(massPointPrefab, worldSample, Quaternion.identity, transform);
                    spawnedPoints.Add(go.transform);

                    var cell = new Vector3Int(
                        Mathf.FloorToInt(localSample.x / voxelSize),
                        Mathf.FloorToInt(localSample.y / voxelSize),
                        Mathf.FloorToInt(localSample.z / voxelSize)
                    );
                    cellToPoint[cell] = go.transform;
                }

        // 2) Connect only axis-aligned neighbors
        var dirs = new[] { Vector3Int.right, Vector3Int.up, Vector3Int.forward };
        foreach (var kv in cellToPoint)
            foreach (var d in dirs)
                if (cellToPoint.TryGetValue(kv.Key + d, out var nb))
                    spawnedSprings.Add((kv.Value, nb));
    }

    bool PointIsInsideMesh(Vector3 worldP)
    {
        // simple ray‑cast up
        var ray = new Ray(worldP, Vector3.up);
        int hits = 0;
        for (int i = 0; i < _tris.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(_verts[_tris[i]]);
            Vector3 b = transform.TransformPoint(_verts[_tris[i + 1]]);
            Vector3 c = transform.TransformPoint(_verts[_tris[i + 2]]);
            if (RayTriangle(ray, a, b, c)) hits++;
        }
        return (hits & 1) == 1;
    }

    static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c)
    {
        // Möller–Trumbore
        const float EPS = 1e-6f;
        Vector3 e1 = b - a, e2 = c - a;
        Vector3 p = Vector3.Cross(ray.direction, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < EPS) return false;
        float inv = 1f / det;
        Vector3 t = ray.origin - a;
        float u = Vector3.Dot(t, p) * inv;
        if (u < 0 || u > 1) return false;
        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(ray.direction, q) * inv;
        if (v < 0 || u + v > 1) return false;
        float dist = Vector3.Dot(e2, q) * inv;
        return dist > EPS;
    }

    void ClearFill()
    {
        foreach (var pt in spawnedPoints)
            if (pt) DestroyImmediate(pt.gameObject);
        spawnedPoints.Clear();
        spawnedSprings.Clear();
    }
}
