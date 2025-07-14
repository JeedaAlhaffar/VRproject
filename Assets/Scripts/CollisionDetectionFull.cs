using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Full collision detection + response for your mass–spring objects.
/// Uses octree + AABB + Sphere (broad), GJK → EPA (narrow),
/// and then impulse distribution to mass points via invMass.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class CollisionDetectionFull : MonoBehaviour
{
    [Header("References")]
    public OctreeManager octreeManager;

    [Header("Response Settings")]
    [Tooltip("Coefficient of restitution.")]
    public float restitution = 0.6f;
    [Tooltip("Friction (tangent impulse) scale.")]
    public float friction = 0.3f;
    [Tooltip("Minimum relative normal velocity to apply friction.")]
    public float minVelocity = 0.05f;
    [Tooltip("Skip impulses below this magnitude.")]
    public float impulseThreshold = 0.001f;

    [Header("Position Correction")]
    [Tooltip("Percent of penetration to correct each frame.")]
    public float correctionFactor = 1f;
    [Tooltip("Allowable penetration slop.")]
    public float slop = 0.01f;

    [Header("Weight Falloff")]
    [Tooltip("Max radius around contact to distribute impulse.")]
    public float impulseRadius = 0.5f;

    [Header("Debug")]
    public bool showGizmos = true;
    public Color boundsColor = Color.magenta;
    public Color centerColor = Color.black;


    Vector3 lastPos;
    Vector3 velocity;
    Vector3 lastRegisteredPos;

    void Start()
    {
        lastPos = transform.position;
    }

    void Update()
    {
        // Compute current body‐COM velocity approx
        velocity = ComputeBodyCOMVelocity();
        lastPos = transform.position;

        // Broad phase via octree
        if (octreeManager == null)
        {
            Debug.LogWarning($"{name}: octreeManager not assigned");
            return;
        }

        // Only update octree if moved enough
        if (Vector3.Distance(transform.position, lastRegisteredPos) > 0.01f)
        {
            octreeManager.UpdateObject(gameObject);
            lastRegisteredPos = transform.position;
        }

        var candidates = octreeManager.GetCollisionCandidates(gameObject);
        if (candidates == null) return;

        // Get this body’s AABB & sphere
        var aabbA = ComputeBoundsFromPoints();
        var sphA = aabbA.extents.magnitude;

        foreach (var other in candidates)
        {
            if (other == null ||
                other == this.gameObject ||
                other.transform.root == transform.root)
                continue;

            // 1) AABB
            var otherScript = other.GetComponent<CollisionDetectionFull>();
            if (otherScript == null) continue;
            var aabbB = otherScript.ComputeBoundsFromPoints();
            if (!aabbA.Intersects(aabbB)) continue;

            // 2) bounding sphere
            var sphB = aabbB.extents.magnitude;
            if ((aabbA.center - aabbB.center).magnitude > (sphA + sphB))
                continue;

            // 3) Narrow: GJK + EPA
            CollisionInfo info = CheckCollision(other);
            if (!info.hasCollision)
                continue;

            // 4) Resolve
            ApplyResponse(other, otherScript, info);
        }
    }

    /// <summary>
    /// Build an AABB from your MassPoints
    /// </summary>
    public Bounds ComputeBoundsFromPoints()
    {
        var ms = GetComponent<MassSpringSystem>();
        if (ms == null)
            return new Bounds(transform.position, Vector3.one * 0.1f);

        var pts = ms.GetPoints();
        if (pts == null || pts.Count == 0)
            return new Bounds(transform.position, Vector3.one * 0.1f);

        Vector3 min = pts[0].position, max = pts[0].position;
        foreach (var p in pts)
        {
            min = Vector3.Min(min, p.position);
            max = Vector3.Max(max, p.position);
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

    struct CollisionInfo
    {
        public bool hasCollision;
        public Vector3 point;    // world‐space contact point
        public Vector3 normal;   // from this → other
        public float depth;      // penetration depth
    }

    /// <summary>
    /// GJK + EPA stub. Replace EPA with your implementation if available.
    /// Here we approximate contact point + depth by simple center approach.
    /// </summary>
    CollisionInfo CheckCollision(GameObject other)
    {
        CollisionInfo info = new CollisionInfo { hasCollision = false };

        // 1) gather support polys
        Vector3[] A = GetWorldVerts(gameObject);
        Vector3[] B = GetWorldVerts(other);
        if (A.Length == 0 || B.Length == 0) return info;

        // 2) GJK
        if (!GJKIntersect(A, B)) return info;

        // 3) EPA - Expansion Polytope Algorithm (not implemented here)
        //    You can call your EPA code to get exact normal+depth.
        //    For now we approximate:
        info.normal = (transform.position - other.transform.position).normalized;
        float rA = ComputeBoundsFromPoints().extents.magnitude;
        float rB = other.GetComponent<CollisionDetectionFull>()
                     .ComputeBoundsFromPoints().extents.magnitude;
        float centerDist = (transform.position - other.transform.position).magnitude;
        info.depth = Mathf.Max(0f, (rA + rB) - centerDist);

        // 4) contact point halfway
        info.point = (transform.position + other.transform.position) * 0.5f;
        info.hasCollision = true;
        return info;
    }

    //void ApplyResponse(
    //    GameObject other,
    //    CollisionDetectionFull otherScript,
    //    CollisionInfo info)
    //{
    //    // 1) relative velocity along normal
    //    Vector3 vRel = velocity - otherScript.velocity;
    //    float vn = Vector3.Dot(vRel, info.normal);
    //    if (vn > 0f) return;   // separating

    //    // 2) compute total invMass of each body
    //    var msA = GetComponent<MassSpringSystem>().GetPoints();
    //    var msB = other.GetComponent<MassSpringSystem>().GetPoints();
    //    float invMA = 0f, invMB = 0f;
    //    foreach (var p in msA) invMA += p.invMass;
    //    foreach (var p in msB) invMB += p.invMass;

    //    // 3) scalar impulse
    //    float e = restitution;
    //    float j = -(1 + e) * vn / (invMA + invMB);
    //    Vector3 impulse = j * info.normal;
    //    if (impulse.magnitude < impulseThreshold) return;

    //    // 4) positional correction
    //    float corr = Mathf.Max(info.depth - slop, 0f) / (invMA + invMB) * correctionFactor;
    //    Vector3 offset = info.normal * corr;
    //    transform.position += offset * invMA;   // move this body
    //    other.transform.position -= offset * invMB; // move other

    //    // 5) distribute impulse to each MassPoint
    //    DistributeImpulse(msA, info.point, impulse, +1);
    //    DistributeImpulse(msB, info.point, -impulse, +1);

    //    // 6) optional: friction (tangent)
    //    Vector3 tangent = vRel - vn * info.normal;
    //    if (tangent.magnitude > minVelocity)
    //    {
    //        tangent.Normalize();
    //        Vector3 ft = -j * friction * tangent;
    //        DistributeImpulse(msA, info.point, ft, +1);
    //        DistributeImpulse(msB, info.point, -ft, +1);
    //    }
    //}

    /// <summary>
    /// Distribute a world‐space impulse to mass points.
    /// weight = clamp(1 - dist/R,0,1)
    /// scaled by invMass.
    /// </summary>
    void DistributeImpulse(
        List<MassPoint> pts,
        Vector3 contact,
        Vector3 impulse,
        float scale = 1f)
    {
        foreach (var p in pts)
        {
            float dist = Vector3.Distance(p.position, contact);
            if (dist > impulseRadius) continue;
            // linear falloff
            float w = Mathf.Clamp01(1f - dist / impulseRadius);
            p.velocity += (impulse * (w * p.invMass * scale));
        }
    }

    // ------------------------
    // GJK Implementation
    // ------------------------

    bool GJKIntersect(Vector3[] A, Vector3[] B)
    {
        var simplex = new List<Vector3>();
        Vector3 dir = B[0] - A[0];
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
        simplex.Add(Support(A, B, dir));
        dir = -simplex[0];

        for (int i = 0; i < 50; i++)
        {
            var p = Support(A, B, dir);
            if (Vector3.Dot(p, dir) <= 0f) return false;
            simplex.Add(p);
            if (GJKHandleSimplex(ref simplex, ref dir))
                return true;
        }
        return false;
    }

    Vector3 Support(Vector3[] A, Vector3[] B, Vector3 d)
        => Farthest(A, d) - Farthest(B, -d);

    Vector3 Farthest(Vector3[] pts, Vector3 d)
    {
        float best = float.MinValue;
        Vector3 outv = Vector3.zero;
        foreach (var v in pts)
        {
            float dot = Vector3.Dot(v, d);
            if (dot > best) { best = dot; outv = v; }
        }
        return outv;
    }

    bool GJKHandleSimplex(ref List<Vector3> s, ref Vector3 d)
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
                d = Vector3.Dot(abc, ao) > 0f ? abc : -abc;
            }
        }
        else if (s.Count == 4)
            return true;

        return false;
    }

    Vector3[] GetWorldVerts(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return new Vector3[0];
        var local = mf.sharedMesh.vertices;
        var w = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            w[i] = go.transform.TransformPoint(local[i]);
        return w;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        var b = ComputeBoundsFromPoints();
        Gizmos.color = boundsColor;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = centerColor;
        Gizmos.DrawWireSphere(b.center, 0.05f);
    }
    /// <summary>
    /// Full response: guaranteed to push bodies apart + bounce + friction.
    /// </summary>
    void ApplyResponse(
        GameObject other,
        CollisionDetectionFull otherScript,
        CollisionInfo info)
    {
        // 0) grab mass points
        var msA = GetComponent<MassSpringSystem>().GetPoints();
        var msB = other.GetComponent<MassSpringSystem>().GetPoints();
        if (otherScript == null || msA == null || msB == null) return;

        // ---- STEP 1: compute true COM velocities  ----
        // (instead of using transform deltas)
        Vector3 vA = ComputeCOMVelocity(msA);
        Vector3 vB = otherScript.ComputeCOMVelocity(msB);
        Vector3 vRel = vA - vB;
        float vn = Vector3.Dot(vRel, info.normal);

        Debug.Log($"vn = {vn:F4}, depth = {info.depth:F4}");

        // ---- STEP 2: sum invMass of each body ----
        float invMA = 0f, invMB = 0f;
        foreach (var p in msA) invMA += p.invMass;
        foreach (var p in msB) invMB += p.invMass;
        if (invMA + invMB <= 0f) return;  // avoid division by zero

        // ---- STEP 3: decide whether to apply bounce impulse ----
        bool separating = (vn > 0f);
        bool deepPen = (info.depth > slop * 1.1f);

        // if separating AND not deepPen ⇒ bodies are moving apart and tiny or no penetration ⇒ skip
        if (separating && !deepPen)
            return;

        // compute impulse magnitude j
        float j;
        if (!separating)
        {
            // normal bounce formula
            float e = restitution;
            j = -(1 + e) * vn / (invMA + invMB);
        }
        else
        {
            // vn > 0 (separating) but deep penetration ⇒ force push‐apart
            j = info.depth * 0.5f;
        }

        // clamp to a minimum so we always push if deeply penetrated
        j = Mathf.Max(j, 0.05f);

        Vector3 impulse = info.normal * j;
        Debug.Log($"Applying impulse j={j:F4}, |impulse|={impulse.magnitude:F4}");

        // ---- STEP 4: positional correction (full depth carve) ----
        // carve them apart by exactly info.depth
        Vector3 sep = info.normal * (info.depth + 0.001f);
        transform.position += sep * 0.5f;
        other.transform.position -= sep * 0.5f;

        // ---- STEP 5: distribute bounce impulse to mass points ----
        DistributeImpulse(msA, info.point, impulse);
        DistributeImpulse(msB, info.point, -impulse);

        // ---- STEP 6: friction impulse (tangent) ----
        Vector3 tangent = vRel - vn * info.normal;
        if (tangent.sqrMagnitude > (minVelocity * minVelocity))
        {
            tangent.Normalize();
            Vector3 frictionImp = -j * friction * tangent;
            DistributeImpulse(msA, info.point, frictionImp);
            DistributeImpulse(msB, info.point, -frictionImp);
        }

        // ---- STEP 7: clamp tiny COM velocities to zero (stop forever bounce) ----
        vA = ComputeCOMVelocity(msA);
        if (vA.magnitude < 0.02f)
            ZeroVelocities(msA);

        vB = ComputeCOMVelocity(msB);
        if (vB.magnitude < 0.02f)
            ZeroVelocities(msB);
    }

    // Optional helpers you already have:
    Vector3 ComputeCOMVelocity(List<MassPoint> pts)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p.velocity;
        return sum / Mathf.Max(1, pts.Count);
    }

    void ZeroVelocities(List<MassPoint> pts)
    {
        for (int i = 0; i < pts.Count; i++)
            pts[i].velocity = Vector3.zero;
    }

    void DistributeImpulse(
        List<MassPoint> pts,
        Vector3 contact,
        Vector3 impulse)
    {
        foreach (var p in pts)
        {
            float d = Vector3.Distance(p.position, contact);
            if (d > impulseRadius) continue;
            float w = Mathf.Clamp01(1f - (d / impulseRadius));
            p.velocity += impulse * (w * p.invMass);
        }
    }


    Vector3 ComputeBodyCOMVelocity()
    {
        var pts = GetComponent<MassSpringSystem>().GetPoints();
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p.velocity;
        return sum / pts.Count;
    }


}
