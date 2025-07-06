using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelGridTester : MonoBehaviour
{
    [Header("Voxel Settings")]
    [Tooltip("Number of voxels along each axis")]
    public int resolution = 16;
    [Tooltip("Size of the voxel grid (world units)")]
    public Vector3 gridSize = Vector3.one;

    [Header("Surface Data (read-only)")]
    public Vector3[] worldVerts;
    public int[] tris;

    void Start()
    {
        // 1) Grab surface
        var mesh = GetComponent<MeshFilter>().mesh;
        tris = mesh.triangles;
        var localVerts = mesh.vertices;

        // 2) World-space
        worldVerts = new Vector3[localVerts.Length];
        for (int i = 0; i < localVerts.Length; i++)
            worldVerts[i] = transform.TransformPoint(localVerts[i]);

        // 3) Compute voxel centers & test inside
        GenerateVoxelGrid();
    }

    void GenerateVoxelGrid()
    {
        // AABB of mesh in world-space
        var bounds = GetComponent<MeshFilter>().mesh.bounds;
        var min = transform.TransformPoint(bounds.min);
        var max = transform.TransformPoint(bounds.max);

        // Voxel step per axis
        Vector3 step = new Vector3(
            (max.x - min.x) / resolution,
            (max.y - min.y) / resolution,
            (max.z - min.z) / resolution);

        int insideCount = 0;
        for (int x = 0; x < resolution; x++)
            for (int y = 0; y < resolution; y++)
                for (int z = 0; z < resolution; z++)
                {
                    // 4) Voxel center
                    Vector3 center = new Vector3(
                        min.x + (x + 0.5f) * step.x,
                        min.y + (y + 0.5f) * step.y,
                        min.z + (z + 0.5f) * step.z);

                    // 5) Even-odd raycast along +Y
                    if (IsPointInside(center))
                    {
                        insideCount++;
                        // you could Instantiate a debug sphere:
                        Debug.DrawLine(center, center + Vector3.up * 0.01f, Color.green, 10f);
                    }
                    else
                    {
                        Debug.DrawLine(center, center + Vector3.up * 0.01f, Color.red, 10f);
                    }
                }

        Debug.Log($"VoxelGridTester: {insideCount}/{resolution * resolution * resolution} voxels inside.");
    }

    // Count intersections of a ray going up from `p` against the mesh
    bool IsPointInside(Vector3 p)
    {
        var dir = Vector3.up;
        int hitCount = 0;
        // For each triangle
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = worldVerts[tris[i]];
            Vector3 b = worldVerts[tris[i + 1]];
            Vector3 c = worldVerts[tris[i + 2]];
            if (RayIntersectsTriangle(p, dir, a, b, c))
                hitCount++;
        }
        return (hitCount & 1) == 1;
    }

    // Möller-Trumbore
    bool RayIntersectsTriangle(Vector3 orig, Vector3 dir,
                               Vector3 v0, Vector3 v1, Vector3 v2)
    {
        const float EPS = 1e-6f;
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 p = Vector3.Cross(dir, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < EPS) return false;
        float invDet = 1f / det;
        Vector3 tvec = orig - v0;
        float u = Vector3.Dot(tvec, p) * invDet;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(tvec, e1);
        float v = Vector3.Dot(dir, q) * invDet;
        if (v < 0f || u + v > 1f) return false;
        float t = Vector3.Dot(e2, q) * invDet;
        return t > EPS;
    }
}
