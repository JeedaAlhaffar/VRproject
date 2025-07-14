using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    public float impulseThreshold = 0.00001f;

    [Header("Position Correction")]
    [Tooltip("Percent of penetration to correct each frame.")]
    public float correctionFactor = 1f;
    [Tooltip("Allowable penetration slop.")]
    public float slop = 0.0001f;

    [Header("Weight Falloff")]
    [Tooltip("Max radius around contact to distribute impulse.")]
    public float impulseRadius = 0.5f;

    [Header("Debug")]
    public bool showGizmos = true;
    public Color boundsColor = Color.magenta;
    public Color centerColor = Color.black;

    [Header("Audio")]
    public AudioSource audioSrc;
    public AudioClip collisionClip;

    Vector3 lastPos;
    Vector3 velocity;
    Vector3 lastRegisteredPos;


    void Start()
    {
        lastPos = transform.position;
        if (audioSrc == null)
        {
            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null)
            {
                Debug.LogWarning($"{name}: no AudioSource found – adding one.");
                audioSrc = gameObject.AddComponent<AudioSource>();
                audioSrc.playOnAwake = false;
            }
        }

        if (collisionClip == null)
            Debug.LogWarning($"{name}: no collisionClip assigned in the Inspector!");
    }

    void Update()
    {
    }
    /// <summary>
    /// World‑space AABB from the MeshFilter’s sharedMesh.bounds.
    /// </summary>
    public Bounds ComputeBoundsFromMesh()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return new Bounds(transform.position, Vector3.one * 0.1f);

        // local bounds
        Bounds local = mf.sharedMesh.bounds;
        // world center
        Vector3 worldCenter = transform.TransformPoint(local.center);
        // world extents (scale by lossyScale)
        Vector3 worldExtents = Vector3.Scale(local.extents, transform.lossyScale);
        return new Bounds(worldCenter, worldExtents * 2f);
    }

    void FixedUpdate()
    {


        if (octreeManager == null)
        {
            Debug.LogError($"{name}: OctreeManager is NULL!");
            return;
        }

        // 1) Always refresh your entry in the octree:
        octreeManager.UpdateObject(gameObject);
        lastRegisteredPos = transform.position;

        // 2) Now fetch candidates:
        var candidates = octreeManager.GetCollisionCandidates(gameObject);
        Debug.Log($"[{name}] {candidates?.Count ?? 0} collision candidates.");
        if (candidates == null) return;

        // 3) get candidates
        candidates = octreeManager.GetCollisionCandidates(gameObject);
        if (candidates == null) return;

        // 4) your broad‐phase tests
        var aabbA = ComputeBoundsFromMesh();
        float sphA = aabbA.extents.magnitude;

        foreach (var other in candidates)
        {
            if (other == null || other == gameObject)
            {
                Debug.Log($"{name}: skipped null/self candidate");
                continue;
            }

            if (other.transform.root == transform.root)
            {
                Debug.Log($"{name}: skipped same-root candidate");
                continue;
            }

            var otherScript = other.GetComponent<CollisionDetectionFull>();
            if (otherScript == null)
            {
                Debug.Log($"{name}: skipped non-collisionable object {other.name}");
                continue;
            }

            var aabbB = otherScript.ComputeBoundsFromMesh();
            if (!aabbA.Intersects(aabbB))
            {
                Debug.Log($"{name}: failed AABB test with {other.name}");
                continue;
            }

            float sphB = aabbB.extents.magnitude;
            if ((aabbA.center - aabbB.center).magnitude > (sphA + sphB))
            {
                Debug.Log($"{name}: failed sphere test with {other.name}");
                continue;
            }

            var info = CheckCollision(other);
            if (info.hasCollision)
            {
                Debug.Log($"{name}: calling ApplyResponse to {other.name}");
                ApplyResponse(other, otherScript, info);
            }
            else
            {
                Debug.Log($"{name}: CheckCollision returned false for {other.name}");
            }
        }
        if (octreeManager == null)
        {
            Debug.LogError($"{name}: OctreeManager is NULL!");
            return;
        }
    }
    /// <summary>
    /// Build an AABB from your MassPoints
    /// </summary>
    


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

        // 1) gather verts…
        Vector3[] A = GetWorldVerts(gameObject);
        Vector3[] B = GetWorldVerts(other);
        if (A.Length == 0 || B.Length == 0) return info;

        // 2) GJK
        if (!GJKIntersect(A, B)) return info;

        // 3) EPA:
        Vector3 epaN;
        float epaD;
        if (!ComputeEPA(gameObject, other, out epaN, out epaD) || epaD <= slop)
            return info;   // no real penetration

        info.hasCollision = true;
        info.normal = epaN;
        info.depth = epaD;
        info.point = (transform.position + other.transform.position) * 0.5f;
        return info;



    }




    /// <summary>
    /// Distribute a world‐space impulse to mass points.
    /// weight = clamp(1 - dist/R,0,1)
    /// scaled by invMass.
    /// </summary>


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
        if (mf == null || mf.mesh == null) return new Vector3[0];
        var local = mf.mesh.vertices;  // **deformed** vertices
        var w = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            w[i] = go.transform.TransformPoint(local[i]);
        return w;
    }


    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        var b = ComputeBoundsFromMesh();
        Gizmos.color = boundsColor;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = centerColor;
        Gizmos.DrawWireSphere(b.center, 0.05f);
    }
    /// <summary>
    /// Full response: guaranteed to push bodies apart + bounce + friction.
    /// </summary>
    void ApplyResponse(GameObject other, CollisionDetectionFull otherScript, CollisionInfo info)
    {
        var msA = GetComponent<MassSpringSystem>().GetPoints();
        var msB = other.GetComponent<MassSpringSystem>().GetPoints();
        if (msA == null || msB == null) return;

        // 1) Positional correction applied to mass points (not transform)
        float invMA = msA.Sum(p => p.invMass);
        float invMB = msB.Sum(p => p.invMass);
        float pen = Mathf.Max(info.depth - slop, 0f);
        if (pen > 0f)
        {
            float corr = pen / (invMA + invMB) * correctionFactor;
            Vector3 corrVec = info.normal * corr;
            foreach (var p in msA) p.position += corrVec * p.invMass;
            foreach (var p in msB) p.position -= corrVec * p.invMass;
        }

        // 2) Combine restitution from both materials
        float e = Mathf.Clamp01((restitution + otherScript.restitution) * 0.5f);

        // 3) Relative velocity and bounce impulse
        Vector3 vARel = ComputeCOMVelocity(msA);
        Vector3 vBRel = ComputeCOMVelocity(msB);
        float vn = Vector3.Dot(vARel - vBRel, info.normal);
        float j = -(1 + e) * vn / (invMA + invMB);
        if (j > impulseThreshold)
        {
            Vector3 impulse = info.normal * j;
            DistributeImpulse(msA, info.point, impulse);
            DistributeImpulse(msB, info.point, -impulse);

            // friction (optional)
            Vector3 tangent = (vARel - vBRel) - vn * info.normal;
            if (tangent.sqrMagnitude > 1e-6f)
            {
                tangent.Normalize();
                Vector3 ft = -j * friction * tangent;
                DistributeImpulse(msA, info.point, ft);
                DistributeImpulse(msB, info.point, -ft);
            }
        }

        // 4) Zero out only the COM velocity so the mesh doesn’t “jump”
        Vector3 comVel = ComputeCOMVelocity(msA);
        foreach (var p in msA) p.velocity -= comVel;

        // 5) Play sound
        if (collisionClip != null && audioSrc != null)
            audioSrc.PlayOneShot(collisionClip);
    }
    // Optional helpers you already have:
    Vector3 ComputeCOMVelocity(List<MassPoint> pts)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p.velocity;
        return sum / Mathf.Max(1, pts.Count);
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



    bool ComputeEPA(GameObject goA, GameObject goB, out Vector3 normal, out float depth)
    {
        Bounds aabbA = goA.GetComponent<CollisionDetectionFull>().ComputeBoundsFromMesh();
        Bounds aabbB = goB.GetComponent<CollisionDetectionFull>().ComputeBoundsFromMesh();

        Vector3 overlap = GetOverlapAxis(aabbA, aabbB);
        normal = Vector3.zero;
        depth = 0f;

        // If no overlap on any axis → no collision
        if (overlap.x <= 0 || overlap.y <= 0 || overlap.z <= 0)
            return false;

        // Find the smallest axis of penetration
        if (overlap.x < overlap.y && overlap.x < overlap.z)
        {
            normal = new Vector3(Mathf.Sign(aabbA.center.x - aabbB.center.x), 0f, 0f);
            depth = overlap.x;
        }
        else if (overlap.y < overlap.z)
        {
            normal = new Vector3(0f, Mathf.Sign(aabbA.center.y - aabbB.center.y), 0f);
            depth = overlap.y;
        }
        else
        {
            normal = new Vector3(0f, 0f, Mathf.Sign(aabbA.center.z - aabbB.center.z));
            depth = overlap.z;
        }

        return true;
    }

    Vector3 GetOverlapAxis(Bounds A, Bounds B)
    {
        Vector3 o = Vector3.zero;
        o.x = (A.extents.x + B.extents.x) - Mathf.Abs(A.center.x - B.center.x);
        o.y = (A.extents.y + B.extents.y) - Mathf.Abs(A.center.y - B.center.y);
        o.z = (A.extents.z + B.extents.z) - Mathf.Abs(A.center.z - B.center.z);
        return o;
    }

    void ZeroVelocities(List<MassPoint> pts)
    {
        for (int i = 0; i < pts.Count; i++)
            pts[i].velocity = Vector3.zero;
    }



}
