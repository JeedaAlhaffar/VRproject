using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class CollisionDetectionFull : MonoBehaviour
{
    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    //private bool isConcaveCached;
    //private bool concaveChecked = false;

    // حل المشكلة الأولى: مركز الجسم
    private Vector3 meshCenter;
    private Bounds meshBounds;
    private bool boundsCalculated = false;

    private static Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();
    private static Dictionary<GameObject, Vector3[]> vertsCache = new Dictionary<GameObject, Vector3[]>();
    private OctreeManager octreeManager;

    [Header("Collision Response Settings")]
    public float restitution = 0.6f; // معامل الارتداد
    public float friction = 0.3f; // معامل الاحتكاك
    public float minimumVelocity = 0.1f; // الحد الأدنى للسرعة

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public Color boundsColor = Color.green;
    public Color centerColor = Color.red;

    void Start()
    {
        lastPosition = transform.position;
        CalculateMeshBounds(); // حساب الحدود الفعلية للـ mesh
        octreeManager = OctreeManager.Instance;
        if (octreeManager == null)
            Debug.LogError("❌ No OctreeManager instance found in scene!");

    }

    void Update()
    {
        currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;

        // إعادة حساب الحدود إذا تغير الكائن
        if (transform.hasChanged)
        {
            boundsCalculated = false;
            CalculateMeshBounds();
        }

        // Broad phase
        List<GameObject> candidates = octreeManager.GetCollisionCandidates(gameObject);

        foreach (var other in candidates)
        {
            if (other == gameObject || other.transform.root == transform.root) continue;

            // استخدام الحدود الفعلية للـ mesh
            Bounds boundsA = GetActualMeshBounds(gameObject);
            Bounds boundsB = GetActualMeshBounds(other);

            if (!boundsA.Intersects(boundsB)) continue;

            if (!SphericalIntersect(boundsA, boundsB)) continue;

            // فحص التصادم باستخدام الخوارزميات المُحسنة
            CollisionInfo collisionInfo = CheckCollision(gameObject, other);

            if (collisionInfo.hasCollision)
            {
                // تطبيق استجابة التصادم
                ApplyCollisionResponse(other, collisionInfo);
            }
        }
    }

    #region حساب الحدود الفعلية للـ Mesh

    void CalculateMeshBounds()
    {
        if (boundsCalculated) return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        if (vertices.Length == 0) return;

        // حساب الحدود الفعلية في الـ local space
        Vector3 min = vertices[0];
        Vector3 max = vertices[0];

        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
        }

        // حساب المركز الفعلي للـ mesh
        meshCenter = (min + max) * 0.5f;

        // إنشاء الحدود في الـ local space
        Vector3 size = max - min;
        meshBounds = new Bounds(meshCenter, size);

        boundsCalculated = true;
    }

    Bounds GetActualMeshBounds(GameObject obj)
    {
        CollisionDetectionFull collisionScript = obj.GetComponent<CollisionDetectionFull>();
        if (collisionScript != null)
        {
            // تحويل الحدود إلى world space
            Bounds localBounds = collisionScript.meshBounds;
            Vector3 worldCenter = obj.transform.TransformPoint(localBounds.center);
            Vector3 worldSize = Vector3.Scale(localBounds.size, obj.transform.lossyScale);
            return new Bounds(worldCenter, worldSize);
        }

        // ❌ لا تعيد GetCachedBounds لأنك جاي منها أصلاً، فقط ارجع قيمة مبدئية أو احذف هذا الشرط
        Debug.LogWarning($"⚠️ Object {obj.name} has no CollisionDetectionFull. Returning default bounds.");
        return new Bounds(obj.transform.position, Vector3.one * 0.1f); // بديل آمن
    }

    #endregion

    #region فحص التصادم المُحسن

    public struct CollisionInfo
    {
        public bool hasCollision;
        public Vector3 contactPoint;
        public Vector3 contactNormal;
        public float penetrationDepth;
        public CollisionType type;
    }

    public enum CollisionType
    {
        ConvexConvex,
        ConvexConcave,
        ConcaveConcave
    }

    CollisionInfo CheckCollision(GameObject objA, GameObject objB)
    {
        CollisionInfo info = new CollisionInfo();

        // تحديد نوع الكائنات
        bool isAConcave = IsConcave(objA.transform);
        bool isBConcave = IsConcave(objB.transform);

        Vector3[] vertsA = GetCachedVerts(objA);
        Vector3[] vertsB = GetCachedVerts(objB);

        if (!isAConcave && !isBConcave)
        {
            // كلاهما محدب - استخدام GJK + EPA
            info.type = CollisionType.ConvexConvex;
            info.hasCollision = GJKIntersect(vertsA, vertsB);

            if (info.hasCollision)
            {
                // حساب نقطة التلامس والعمق باستخدام EPA
                CalculateContactInfoEPA(vertsA, vertsB, out info.contactPoint,
                                      out info.contactNormal, out info.penetrationDepth);
            }
        }
        else
        {
            // أحدهما أو كلاهما مقعر - استخدام Triangle-Triangle
            info.type = isAConcave && isBConcave ? CollisionType.ConcaveConcave : CollisionType.ConvexConcave;
            info.hasCollision = TriangleTriangleCollision(objA.transform, objB.transform, out info);
        }

        return info;
    }

    // تطبيق EPA لحساب معلومات التلامس
    void CalculateContactInfoEPA(Vector3[] vertsA, Vector3[] vertsB,
                                out Vector3 contactPoint, out Vector3 contactNormal, out float penetrationDepth)
    {
        // EPA Algorithm (مبسط)
        // في التطبيق الحقيقي، يجب تطبيق EPA كاملاً

        Vector3 centerA = GetCenter(vertsA);
        Vector3 centerB = GetCenter(vertsB);

        contactNormal = (centerA - centerB).normalized;
        contactPoint = (centerA + centerB) * 0.5f;
        penetrationDepth = 0.1f; // قيمة تقريبية - يجب حسابها بدقة
    }

    #endregion

    #region استجابة التصادم

    void ApplyCollisionResponse(GameObject other, CollisionInfo collisionInfo)
    {
        // الحصول على معلومات الكائن الآخر
        Vector3 otherVelocity = Vector3.zero;
        CollisionDetectionFull otherCollision = other.GetComponent<CollisionDetectionFull>();
        if (otherCollision != null)
            otherVelocity = otherCollision.currentVelocity;

        // حساب السرعة النسبية
        Vector3 relativeVelocity = currentVelocity - otherVelocity;
        float relativeSpeed = Vector3.Dot(relativeVelocity, collisionInfo.contactNormal);

        // إذا كانت الكائنات تتحرك بعيداً عن بعضها، لا تطبق استجابة
        if (relativeSpeed > 0) return;

        // حساب قوة الاصطدام
        float impulseMagnitude = -(1 + restitution) * relativeSpeed;

        // تطبيق الاستجابة على الكائن الحالي
        Vector3 impulse = impulseMagnitude * collisionInfo.contactNormal;

        // تطبيق القوة على نظام Mass-Spring
        ApplyImpactForce(collisionInfo.contactPoint, impulse);

        // تطبيق الاحتكاك
        ApplyFriction(collisionInfo.contactPoint, collisionInfo.contactNormal, relativeVelocity);

        // فصل الكائنات إذا كانت متداخلة
        SeparateObjects(other, collisionInfo);

        Debug.Log($"💥 Collision Response: {name} ↔ {other.name}, Impulse: {impulse.magnitude:F2}");
    }

    void ApplyFriction(Vector3 contactPoint, Vector3 contactNormal, Vector3 relativeVelocity)
    {
        // حساب السرعة الجانبية (عمودية على العادي)
        Vector3 tangentialVelocity = relativeVelocity - Vector3.Dot(relativeVelocity, contactNormal) * contactNormal;

        if (tangentialVelocity.magnitude > 0.01f)
        {
            Vector3 frictionForce = -tangentialVelocity.normalized * friction * 10f;
            ApplyImpactForce(contactPoint, frictionForce);
        }
    }

    void SeparateObjects(GameObject other, CollisionInfo collisionInfo)
    {
        // فصل الكائنات بناءً على عمق التداخل
        if (collisionInfo.penetrationDepth > 0.001f)
        {
            Vector3 separation = collisionInfo.contactNormal * collisionInfo.penetrationDepth * 0.5f;

            // تحريك الكائن الحالي
            transform.position += separation;

            // تحريك الكائن الآخر
            other.transform.position -= separation;
        }
    }

    #endregion

    #region خوارزميات محسنة

    // تطبيق صحيح لـ Triangle-Triangle Collision
    bool TriangleTriangleCollision(Transform objA, Transform objB, out CollisionInfo info)
    {
        info = new CollisionInfo();

        MeshFilter mfA = objA.GetComponent<MeshFilter>();
        MeshFilter mfB = objB.GetComponent<MeshFilter>();

        if (mfA == null || mfB == null) return false;

        Vector3[] verticesA = mfA.sharedMesh.vertices;
        Vector3[] verticesB = mfB.sharedMesh.vertices;
        int[] trianglesA = mfA.sharedMesh.triangles;
        int[] trianglesB = mfB.sharedMesh.triangles;

        // فحص كل مثلث مع كل مثلث (يمكن تحسينه باستخدام BVH)
        for (int i = 0; i < trianglesA.Length; i += 3)
        {
            Vector3 a0 = objA.TransformPoint(verticesA[trianglesA[i]]);
            Vector3 a1 = objA.TransformPoint(verticesA[trianglesA[i + 1]]);
            Vector3 a2 = objA.TransformPoint(verticesA[trianglesA[i + 2]]);

            for (int j = 0; j < trianglesB.Length; j += 3)
            {
                Vector3 b0 = objB.TransformPoint(verticesB[trianglesB[j]]);
                Vector3 b1 = objB.TransformPoint(verticesB[trianglesB[j + 1]]);
                Vector3 b2 = objB.TransformPoint(verticesB[trianglesB[j + 2]]);

                if (MollerTrumboreIntersection(a0, a1, a2, b0, b1, b2, out Vector3 contactPoint))
                {
                    info.hasCollision = true;
                    info.contactPoint = contactPoint;
                    info.contactNormal = Vector3.Cross(a1 - a0, a2 - a0).normalized;
                    info.penetrationDepth = 0.1f; // تقريبي
                    return true;
                }
            }
        }

        return false;
    }

    // تطبيق Möller-Trumbore Algorithm
    bool MollerTrumboreIntersection(Vector3 v0, Vector3 v1, Vector3 v2,
                                   Vector3 u0, Vector3 u1, Vector3 u2, out Vector3 contactPoint)
    {
        contactPoint = Vector3.zero;

        // حساب الـ normals
        Vector3 n1 = Vector3.Cross(v1 - v0, v2 - v0).normalized;
        Vector3 n2 = Vector3.Cross(u1 - u0, u2 - u0).normalized;

        // فحص إذا كانت المثلثات متوازية
        if (Mathf.Abs(Vector3.Dot(n1, n2)) > 0.999f) return false;

        // حساب خط التقاطع
        Vector3 direction = Vector3.Cross(n1, n2).normalized;

        // فحص تقاطع المثلثات على الخط
        float[] intervalsT1 = ProjectTriangleOntoLine(v0, v1, v2, direction);
        float[] intervalsT2 = ProjectTriangleOntoLine(u0, u1, u2, direction);

        // فحص تداخل الفترات
        bool overlap = (intervalsT1[0] <= intervalsT2[1] && intervalsT2[0] <= intervalsT1[1]);

        if (overlap)
        {
            contactPoint = (v0 + v1 + v2 + u0 + u1 + u2) / 6f; // نقطة تقريبية
        }

        return overlap;
    }

    float[] ProjectTriangleOntoLine(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 direction)
    {
        float p0 = Vector3.Dot(v0, direction);
        float p1 = Vector3.Dot(v1, direction);
        float p2 = Vector3.Dot(v2, direction);

        float min = Mathf.Min(p0, Mathf.Min(p1, p2));
        float max = Mathf.Max(p0, Mathf.Max(p1, p2));

        return new float[] { min, max };
    }

    #endregion

    #region Helper Methods المحسنة

    Bounds GetCachedBounds(GameObject obj)
    {
        if (boundsCache.TryGetValue(obj, out var cached) && !obj.transform.hasChanged)
            return cached;

        // حساب الحدود الفعلية
        Bounds b = GetActualMeshBounds(obj);
        boundsCache[obj] = b;
        obj.transform.hasChanged = false;
        return b;
    }

    Vector3[] GetCachedVerts(GameObject obj)
    {
        if (vertsCache.TryGetValue(obj, out var verts) && !obj.transform.hasChanged)
            return verts;

        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return new Vector3[0];

        Vector3[] local = mf.sharedMesh.vertices;
        Vector3[] world = new Vector3[local.Length];
        Matrix4x4 matrix = obj.transform.localToWorldMatrix;

        for (int i = 0; i < local.Length; i++)
            world[i] = matrix.MultiplyPoint3x4(local[i]);

        vertsCache[obj] = world;
        obj.transform.hasChanged = false;
        return world;
    }

    bool SphericalIntersect(Bounds a, Bounds b)
    {
        float rA = a.size.magnitude * 0.5f;
        float rB = b.size.magnitude * 0.5f;
        float d = Vector3.Distance(a.center, b.center);
        return d <= (rA + rB);
    }

    bool GJKIntersect(Vector3[] A, Vector3[] B)
    {
        List<Vector3> simplex = new List<Vector3>();
        Vector3 dir = (GetCenter(B) - GetCenter(A)).normalized;
        if (dir.magnitude < 0.001f) dir = Vector3.right;

        simplex.Add(Support(A, B, dir));
        dir = -simplex[0];

        for (int i = 0; i < 30; i++)
        {
            Vector3 p = Support(A, B, dir);
            if (Vector3.Dot(p, dir) <= 0f) return false;
            simplex.Add(p);
            if (HandleSimplex(ref simplex, ref dir)) return true;
        }
        return false;
    }

    Vector3 Support(Vector3[] A, Vector3[] B, Vector3 d) =>
        Farthest(A, d) - Farthest(B, -d);

    Vector3 Farthest(Vector3[] pts, Vector3 d)
    {
        float max = float.MinValue;
        Vector3 best = Vector3.zero;
        foreach (var p in pts)
        {
            float dot = Vector3.Dot(p, d);
            if (dot > max) { max = dot; best = p; }
        }
        return best;
    }

    Vector3 GetCenter(Vector3[] verts)
    {
        Vector3 c = Vector3.zero;
        foreach (var v in verts) c += v;
        return c / verts.Length;
    }

    bool HandleSimplex(ref List<Vector3> s, ref Vector3 d)
    {
        if (s.Count == 2)
        {
            Vector3 a = s[1], b = s[0];
            Vector3 ab = b - a, ao = -a;
            d = Vector3.Cross(Vector3.Cross(ab, ao), ab);
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
        else if (s.Count == 4) return true;
        return false;
    }

    bool IsConcave(Transform obj)
    {
        // طريقة محسنة لتحديد إذا كان الكائن مقعراً
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;

        // فحص الزوايا الداخلية للمش
        Vector3[] vertices = mf.sharedMesh.vertices;
        int[] triangles = mf.sharedMesh.triangles;

        // إذا كان عدد المثلثات قليل، اعتبره محدب
        if (triangles.Length < 12) return false;

        // فحص عينة من الزوايا
        int concaveCount = 0;
        for (int i = 0; i < triangles.Length; i += 9) // كل 3 مثلثات
        {
            if (i + 2 < triangles.Length)
            {
                Vector3 v0 = obj.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = obj.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = obj.TransformPoint(vertices[triangles[i + 2]]);

                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                Vector3 center = obj.transform.position;

                // إذا كان العادي يشير للداخل، الزاوية مقعرة
                if (Vector3.Dot(normal, (v0 + v1 + v2) / 3f - center) < 0)
                    concaveCount++;
            }
        }

        return concaveCount > (triangles.Length / 9) * 0.3f; // إذا كان أكثر من 30% مقعر
    }

    Vector3 GetCurrentVelocity() => currentVelocity;

    void ApplyImpactForce(Vector3 point, Vector3 impulse)
    {
        if (TryGetComponent<UnifiedMassSpringSystem>(out var ms))
        {
            ms.ApplyImpactForce(point, impulse);
        }
        else
        {
            // إذا لم يكن هناك نظام Mass-Spring، طبق على الـ Rigidbody
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
            }
        }
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // رسم حدود الـ mesh الفعلية
        if (boundsCalculated)
        {
            Gizmos.color = boundsColor;
            Bounds worldBounds = GetActualMeshBounds(gameObject);
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
        }

        // رسم مركز الـ mesh
        Gizmos.color = centerColor;
        if (meshCenter != Vector3.zero)
        {
            Vector3 worldCenter = transform.TransformPoint(meshCenter);
            Gizmos.DrawWireSphere(worldCenter, 0.1f);
        }

        // رسم اتجاه السرعة
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, currentVelocity);
    }

    #endregion
}