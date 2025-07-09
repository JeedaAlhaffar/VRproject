using System.Collections.Generic;
using UnityEngine;

public class Octree
{
    private Bounds bounds;
    private int maxObjects;
    private int maxDepth;
    private int depth;
    private List<GameObject> objects;
    private Octree[] children;
    private bool isDivided;

    // تحسين: pooling للقوائم لتقليل GC
    private static readonly Stack<List<GameObject>> listPool = new Stack<List<GameObject>>();

    public Octree(Bounds bounds, int maxObjects = 10, int maxDepth = 6, int depth = 0)
    {
        this.bounds = bounds;
        this.maxObjects = maxObjects;
        this.maxDepth = maxDepth;
        this.depth = depth;
        this.objects = GetPooledList();
        this.children = null;
        this.isDivided = false;
    }

    private static List<GameObject> GetPooledList()
    {
        if (listPool.Count > 0)
        {
            var list = listPool.Pop();
            list.Clear();
            return list;
        }
        return new List<GameObject>();
    }

    private static void ReturnToPool(List<GameObject> list)
    {
        if (list.Count < 100) // تجنب تخزين قوائم كبيرة جداً
        {
            list.Clear();
            listPool.Push(list);
        }
    }

    public void Insert(GameObject obj)
    {
        if (obj == null) return;

        Bounds objBounds = GetBounds(obj);
        if (!bounds.Intersects(objBounds))
            return;

        // تحسين: تحقق من حالة التقسيم قبل الإدراج
        if (!isDivided && (objects.Count < maxObjects || depth >= maxDepth))
        {
            objects.Add(obj);
            return;
        }

        if (!isDivided)
        {
            Subdivide();
        }

        // تحسين: استخدام for loop بدلاً من foreach للأداء
        bool inserted = false;
        for (int i = 0; i < 8; i++)
        {
            if (children[i].bounds.Intersects(objBounds))
            {
                children[i].Insert(obj);
                inserted = true;
            }
        }

        // إذا لم يتم إدراج الكائن في أي child، اتركه في الparent
        if (!inserted)
        {
            objects.Add(obj);
        }
    }

    public void Subdivide()
    {
        if (isDivided) return;

        children = new Octree[8];
        Vector3 size = bounds.size / 2f;
        Vector3 center = bounds.center;

        // تحسين: حساب أكثر دقة للمراكز
        Vector3 quarterSize = size / 2f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 offset = new Vector3(
                ((i & 1) == 0 ? -1 : 1) * quarterSize.x,
                ((i & 2) == 0 ? -1 : 1) * quarterSize.y,
                ((i & 4) == 0 ? -1 : 1) * quarterSize.z
            );

            Vector3 newCenter = center + offset;
            children[i] = new Octree(
                new Bounds(newCenter, size),
                maxObjects,
                maxDepth,
                depth + 1
            );
        }

        isDivided = true;

        // إعادة توزيع الكائنات الموجودة
        var objectsToRedistribute = new List<GameObject>(objects);
        objects.Clear();

        foreach (var obj in objectsToRedistribute)
        {
            Insert(obj);
        }
    }

    public void Retrieve(Bounds queryBounds, List<GameObject> result)
    {
        if (!bounds.Intersects(queryBounds))
            return;

        // إضافة الكائنات في هذا المستوى
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (obj != null && GetBounds(obj).Intersects(queryBounds))
            {
                result.Add(obj);
            }
        }

        // البحث في الأطفال
        if (isDivided)
        {
            for (int i = 0; i < 8; i++)
            {
                children[i].Retrieve(queryBounds, result);
            }
        }
    }

    // تحسين: استعلام أكثر تخصصاً لل collision detection
    public void RetrieveCollisionCandidates(GameObject queryObject, List<GameObject> result)
    {
        if (queryObject == null) return;

        Bounds queryBounds = GetBounds(queryObject);
        if (!bounds.Intersects(queryBounds))
            return;

        // إضافة الكائنات في هذا المستوى (مع تجنب الself-collision)
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (obj != null && obj != queryObject &&
                obj.transform.root != queryObject.transform.root &&
                GetBounds(obj).Intersects(queryBounds))
            {
                result.Add(obj);
            }
        }

        // البحث في الأطفال
        if (isDivided)
        {
            for (int i = 0; i < 8; i++)
            {
                children[i].RetrieveCollisionCandidates(queryObject, result);
            }
        }
    }

    public void Clear()
    {
        if (objects != null)
        {
            ReturnToPool(objects);
            objects = null;
        }

        if (isDivided && children != null)
        {
            for (int i = 0; i < 8; i++)
            {
                children[i]?.Clear();
            }
        }

        children = null;
        isDivided = false;
    }

    // تحسين: إضافة معلومات إحصائية للتصحيح
    public void GetStatistics(out int totalObjects, out int totalNodes, out int maxDepthReached)
    {
        totalObjects = objects.Count;
        totalNodes = 1;
        maxDepthReached = depth;

        if (isDivided)
        {
            for (int i = 0; i < 8; i++)
            {
                int childObjects, childNodes, childMaxDepth;
                children[i].GetStatistics(out childObjects, out childNodes, out childMaxDepth);

                totalObjects += childObjects;
                totalNodes += childNodes;
                maxDepthReached = Mathf.Max(maxDepthReached, childMaxDepth);
            }
        }
    }

    // تحسين: إضافة تصور للOctree في المحرر
    public void DrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (isDivided)
        {
            for (int i = 0; i < 8; i++)
            {
                children[i].DrawGizmos();
            }
        }
    }

    public static Bounds GetBounds(GameObject obj)
    {
        if (obj == null) return new Bounds();

        // تحسين: تحقق من وجود الRenderer أولاً
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }

        // fallback للMeshFilter
        var mf = obj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            return TransformBounds(mf.sharedMesh.bounds, obj.transform.localToWorldMatrix);
        }

        // fallback للCollider
        var collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            return collider.bounds;
        }

        // fallback أخير
        return new Bounds(obj.transform.position, Vector3.one);
    }

    public static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        var center = matrix.MultiplyPoint3x4(bounds.center);
        var extents = bounds.extents;

        // تحسين: استخدام طريقة أكثر دقة لحساب الextents
        Vector3[] axes = {
            matrix.MultiplyVector(new Vector3(extents.x, 0, 0)),
            matrix.MultiplyVector(new Vector3(0, extents.y, 0)),
            matrix.MultiplyVector(new Vector3(0, 0, extents.z))
        };

        Vector3 newExtents = new Vector3(
            Mathf.Abs(axes[0].x) + Mathf.Abs(axes[1].x) + Mathf.Abs(axes[2].x),
            Mathf.Abs(axes[0].y) + Mathf.Abs(axes[1].y) + Mathf.Abs(axes[2].y),
            Mathf.Abs(axes[0].z) + Mathf.Abs(axes[1].z) + Mathf.Abs(axes[2].z)
        );

        return new Bounds(center, newExtents * 2);
    }
}

// مساعد لإدارة الOctree
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