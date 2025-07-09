using System.Collections.Generic;
using UnityEngine;

public class CollisionDetectionFull : MonoBehaviour
{
    private Vector3 lastPosition;
    private static List<GameObject> allObjects = new List<GameObject>();
    private static Octree octree;
    private static bool octreeNeedsRebuild = true;
    private static float lastRebuildTime = 0f;
    private static readonly float REBUILD_INTERVAL = 0.1f; // إعادة بناء كل 0.1 ثانية

    // Cache للbounds لتجنب الحسابات المتكررة
    private static Dictionary<GameObject, CachedBounds> boundsCache = new Dictionary<GameObject, CachedBounds>();

    private struct CachedBounds
    {
        public Bounds bounds;
        public Vector3 lastPosition;
        public Quaternion lastRotation;
        public Vector3 lastScale;
    }

    void Start()
    {
        lastPosition = transform.position;

        if (allObjects.Count == 0)
        {
            var all = GameObject.FindObjectsOfType<MeshFilter>();
            foreach (var mf in all)
            {
                if (mf.sharedMesh != null)
                {
                    allObjects.Add(mf.gameObject);
                    // لا داعي لإضافة أي كومبوننت هنا
                }
            }
        }
    }

    void Update()
    {
        Vector3 velocityA = (transform.position - lastPosition) / Time.deltaTime;
      
        // تحسين: إعادة بناء الOctree بشكل دوري وليس كل فريم
        BuildOctreeIfNeeded();

        Bounds boundsA = GetCachedBounds(gameObject);

        // تحسين: استخدام broad phase collision detection
        List<GameObject> candidates = GetBroadPhaseCollisionCandidates(boundsA);

        // تحسين: فرز المرشحين حسب المسافة لتحسين الأداء
        candidates.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

        foreach (var other in candidates)
        {
            if (other == gameObject || other.transform.root == transform.root) continue;

            Bounds boundsB = GetCachedBounds(other);
            if (!boundsA.Intersects(boundsB)) continue;

            // تحسين: استخدام sphere check أولاً كـ early rejection
            if (!SphericalBoundsIntersect(boundsA, boundsB)) continue;

            Vector3[] vertsA = GetWorldVerts(gameObject);
            Vector3[] vertsB = GetWorldVerts(other);

            if (vertsA.Length == 0 || vertsB.Length == 0) continue;

            // تحسين: GJK محسّن مع early termination
            if (GJKIntersectOptimized(vertsA, vertsB))
            {
                HandleCollision(other, velocityA);
            }
        }

        lastPosition = transform.position;
    }

    void BuildOctreeIfNeeded()
    {
        if (octreeNeedsRebuild || Time.time - lastRebuildTime > REBUILD_INTERVAL)
        {
            BuildOctree();
            octreeNeedsRebuild = false;
            lastRebuildTime = Time.time;
        }
    }

    void BuildOctree()
    {
        // تحسين: حساب bounds أكثر دقة للمشهد
        Bounds sceneBounds = CalculateSceneBounds();
        octree = new Octree(sceneBounds);

        foreach (var obj in allObjects)
        {
            if (obj != null) // null check للكائنات المحذوفة
                octree.Insert(obj);
        }
    }

    Bounds CalculateSceneBounds()
    {
        if (allObjects.Count == 0)
            return new Bounds(Vector3.zero, Vector3.one * 1000f);

        Bounds sceneBounds = GetCachedBounds(allObjects[0]);
        for (int i = 1; i < allObjects.Count; i++)
        {
            if (allObjects[i] != null)
                sceneBounds.Encapsulate(GetCachedBounds(allObjects[i]));
        }

        // إضافة padding للتأكد من احتواء جميع الكائنات
        sceneBounds.Expand(sceneBounds.size.magnitude * 0.1f);
        return sceneBounds;
    }

    Bounds GetCachedBounds(GameObject obj)
    {
        if (boundsCache.TryGetValue(obj, out var cached) && !obj.transform.hasChanged)
        {
            return cached.bounds;
        }

        // إعادة حساب bounds
        var bounds = Octree.GetBounds(obj);
        boundsCache[obj] = new CachedBounds
        {
            bounds = bounds,
            lastPosition = obj.transform.position,
            lastRotation = obj.transform.rotation,
            lastScale = obj.transform.localScale
        };

        // تصفير الفلاج حتى نعرف إذا تغيّر مستقبلاً
        obj.transform.hasChanged = false;
        return bounds;
    }

    List<GameObject> GetBroadPhaseCollisionCandidates(Bounds queryBounds)
    {
        var candidates = new List<GameObject>();

        if (octree != null)
        {
            octree.Retrieve(queryBounds, candidates);
        }
        else
        {
            // fallback إذا لم يتم بناء الOctree بعد
            candidates.AddRange(allObjects);
        }

        return candidates;
    }

    bool SphericalBoundsIntersect(Bounds a, Bounds b)
    {
        float radiusA = a.size.magnitude * 0.5f;
        float radiusB = b.size.magnitude * 0.5f;
        float distance = Vector3.Distance(a.center, b.center);
        return distance <= (radiusA + radiusB);
    }

    void HandleCollision(GameObject other, Vector3 velocityA)
    {
        Debug.Log($"💥 Collision Detected between {name} and {other.name}!");

        Vector3 velocityB = Vector3.zero;
        if (other.TryGetComponent<CollisionDetectionFull>(out var otherCol))
            velocityB = otherCol.GetCurrentVelocity();

        float force = EstimateImpactForce(velocityA, velocityB);
        Vector3 contactPoint = (transform.position + other.transform.position) * 0.5f;
        Vector3 impulse = (velocityA - velocityB) * force;

        // تطبيق القوة على النظام
        ApplyCollisionForce(contactPoint, impulse);
    }

    void ApplyCollisionForce(Vector3 contactPoint, Vector3 impulse)
    {
        var massSpringSystem = GetComponent<MassSpringSystem>();
        var massSpringPhysics = GetComponent<MassSpringPhysics>();

        if (massSpringSystem != null && massSpringPhysics != null)
        {
            List<MassPoint> points = massSpringSystem.GetPoints();
            massSpringPhysics.ApplyImpactForce(points, contactPoint, impulse);
        }
        else
        {
            Debug.LogWarning("MassSpringSystem or MassSpringPhysics component not found!");
        }
    }

    Vector3[] GetWorldVerts(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return new Vector3[0];

        Vector3[] local = mf.sharedMesh.vertices;
        Vector3[] world = new Vector3[local.Length];
        Matrix4x4 transformMatrix = go.transform.localToWorldMatrix;

        for (int i = 0; i < local.Length; i++)
            world[i] = transformMatrix.MultiplyPoint3x4(local[i]);

        return world;
    }

    public Vector3 GetCurrentVelocity()
    {
        return (transform.position - lastPosition) / Time.deltaTime;
    }

    float EstimateImpactForce(Vector3 velA, Vector3 velB)
    {
        Vector3 relVel = velA - velB;
        float relSpeed = relVel.magnitude;
        float massEstimate = 1f; // يمكن تحسينها لاحقاً
        return 0.5f * massEstimate * relSpeed * relSpeed;
    }

    // GJK محسّن مع early termination
    bool GJKIntersectOptimized(Vector3[] A, Vector3[] B)
    {
        if (A.Length == 0 || B.Length == 0) return false;

        List<Vector3> simplex = new List<Vector3>(4); // pre-allocate
        Vector3 dir = (GetCenterOfMass(B) - GetCenterOfMass(A)).normalized;

        // إذا كان الاتجاه صفر، استخدم اتجاه افتراضي
        if (dir.magnitude < 0.001f)
            dir = Vector3.right;

        simplex.Add(Support(A, B, dir));
        dir = -simplex[0];

        const int MAX_ITERATIONS = 30; // تقليل عدد التكرارات
        for (int i = 0; i < MAX_ITERATIONS; i++)
        {
            Vector3 p = Support(A, B, dir);
            if (Vector3.Dot(p, dir) <= 0f) return false;

            simplex.Add(p);
            if (HandleSimplex(ref simplex, ref dir)) return true;
        }

        return false;
    }

    Vector3 GetCenterOfMass(Vector3[] vertices)
    {
        Vector3 center = Vector3.zero;
        foreach (var vertex in vertices)
            center += vertex;
        return center / vertices.Length;
    }

    Vector3 Support(Vector3[] A, Vector3[] B, Vector3 d) => Farthest(A, d) - Farthest(B, -d);

    Vector3 Farthest(Vector3[] pts, Vector3 d)
    {
        float max = float.MinValue;
        Vector3 best = Vector3.zero;

        for (int i = 0; i < pts.Length; i++)
        {
            float dot = Vector3.Dot(pts[i], d);
            if (dot > max)
            {
                max = dot;
                best = pts[i];
            }
        }

        return best;
    }

    bool HandleSimplex(ref List<Vector3> s, ref Vector3 d)
    {
        if (s.Count == 2)
        {
            Vector3 a = s[1], b = s[0];
            Vector3 ab = b - a, ao = -a;
            d = Vector3.Cross(Vector3.Cross(ab, ao), ab);

            // تحقق من أن الاتجاه صالح
            if (d.sqrMagnitude < 0.001f)
                d = Vector3.Cross(ab, Vector3.up).normalized;
        }
        else if (s.Count == 3)
        {
            Vector3 a = s[2], b = s[1], c = s[0];
            Vector3 ab = b - a, ac = c - a, ao = -a;
            Vector3 abc = Vector3.Cross(ab, ac);

            if (Vector3.Dot(Vector3.Cross(abc, ac), ao) > 0f)
            {
                s.RemoveAt(1);
                d = Vector3.Cross(Vector3.Cross(ac, ao), ac);
            }
            else if (Vector3.Dot(Vector3.Cross(ab, abc), ao) > 0f)
            {
                s.RemoveAt(0);
                d = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            }
            else
            {
                d = (Vector3.Dot(abc, ao) > 0f) ? abc : -abc;
            }
        }
        else if (s.Count == 4)
        {
            return true;
        }

        return false;
    }

    // تنظيف الmemory عند إزالة الكائن
    void OnDestroy()
    {
        if (boundsCache.ContainsKey(gameObject))
            boundsCache.Remove(gameObject);

        allObjects.Remove(gameObject);
        octreeNeedsRebuild = true;
    }
}
