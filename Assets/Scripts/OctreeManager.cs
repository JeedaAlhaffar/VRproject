// مساعد لإدارة الOctree
using System.Collections.Generic;
using UnityEngine;

public class OctreeManager : MonoBehaviour
{
    [Header("Octree Settings")]
    public int maxObjectsPerNode = 10;
    public int maxDepth = 6;
    public bool showDebugInfo = true;
    public bool drawGizmos = false;

    private Octree octree;
    private List<GameObject> allObjects = new List<GameObject>();

    void Start()
    {
        RebuildOctree();
    }

    public void RebuildOctree()
    {
        // تنظيف الOctree السابق
        octree?.Clear();

        // جمع جميع الكائنات
        allObjects.Clear();
        var meshFilters = FindObjectsOfType<MeshFilter>();
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh != null)
                allObjects.Add(mf.gameObject);
        }

        // حساب bounds المشهد
        Bounds sceneBounds = CalculateSceneBounds();

        // بناء الOctree الجديد
        octree = new Octree(sceneBounds, maxObjectsPerNode, maxDepth);

        foreach (var obj in allObjects)
        {
            octree.Insert(obj);
        }

        if (showDebugInfo)
        {
            int totalObjects, totalNodes, maxDepthReached;
            octree.GetStatistics(out totalObjects, out totalNodes, out maxDepthReached);
            Debug.Log($"Octree rebuilt: {totalObjects} objects, {totalNodes} nodes, max depth: {maxDepthReached}");
        }
    }

    private Bounds CalculateSceneBounds()
    {
        if (allObjects.Count == 0)
            return new Bounds(Vector3.zero, Vector3.one * 1000f);

        Bounds sceneBounds = Octree.GetBounds(allObjects[0]);
        for (int i = 1; i < allObjects.Count; i++)
        {
            sceneBounds.Encapsulate(Octree.GetBounds(allObjects[i]));
        }

        // إضافة padding
        sceneBounds.Expand(sceneBounds.size.magnitude * 0.1f);
        return sceneBounds;
    }

    public List<GameObject> GetCollisionCandidates(GameObject queryObject)
    {
        var candidates = new List<GameObject>();
        octree?.RetrieveCollisionCandidates(queryObject, candidates);
        return candidates;
    }

    void OnDrawGizmos()
    {
        if (drawGizmos && octree != null)
        {
            octree.DrawGizmos();
        }
    }
}