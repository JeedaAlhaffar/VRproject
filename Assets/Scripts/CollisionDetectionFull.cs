using UnityEngine;
using System.Collections.Generic;

public class CollisionDetectionFull : MonoBehaviour
{
    public Vector3 boxSizeA = Vector3.one;
    [Range(0f, 1f)] public float restitution = 0.3f;

    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        GameObject[] all = GameObject.FindObjectsOfType<GameObject>();
        Vector3 velocityA = (transform.position - lastPosition) / Time.deltaTime;

        foreach (GameObject other in all)
        {
            if (other == gameObject || other.transform.root == transform.root) continue;
            if (!other.TryGetComponent<MeshFilter>(out var mfOther) || mfOther.sharedMesh == null) continue;
            if (!other.TryGetComponent<Renderer>(out _)) continue;

            Vector3 centerA = transform.position;
            Vector3 centerB = other.transform.position;
            Vector3 sizeA = GetComponent<Renderer>() ? GetComponent<Renderer>().bounds.size : boxSizeA;
            Vector3 sizeB = other.GetComponent<Renderer>() ? other.GetComponent<Renderer>().bounds.size : Vector3.one;

            DrawAABB(centerA, sizeA, Color.yellow);
            DrawAABB(centerB, sizeB, Color.cyan);

            if (!AABBIntersect(centerA, sizeA, centerB, sizeB)) continue;
            Debug.Log($"✅ AABB detected between {name} and {other.name}");

            float radiusA = sizeA.magnitude * 0.5f;
            float radiusB = sizeB.magnitude * 0.5f;
            if (!BoundingSphereIntersect(centerA, radiusA, centerB, radiusB)) continue;
            Debug.Log($"✅ Bounding Sphere detected between {name} and {other.name}");

            OBB obbA = new OBB(transform);
            OBB obbB = new OBB(other.transform);
            if (!SATCollision.CheckOBBIntersection(obbA, obbB)) continue;
            Debug.Log($"🟥 OBB SAT Collision detected between {name} and {other.name}");

            Vector3[] vertsA = GetWorldVerts(gameObject);
            Vector3[] vertsB = GetWorldVerts(other);

            Vector3 velocityB = Vector3.zero;
            if (other.TryGetComponent<CollisionDetectionFull>(out var otherCol))
                velocityB = otherCol.GetCurrentVelocity();

            if (vertsA.Length > 0 && vertsB.Length > 0)
            {
                bool hitGJK = GJKIntersect(vertsA, vertsB);
                if (hitGJK)
                {
                    float force = EstimateImpactForce(velocityA, velocityB);
                    Debug.Log($"💥 GJK Collision Detected between {name} and {other.name}! Estimated Force: {force:F2} N");
                }
                else
                {
                    bool hitEBM = EBMCollision(transform, other.transform);
                    if (hitEBM)
                    {
                        float force = EstimateImpactForce(velocityA, velocityB);
                        Debug.Log($"🔥 EBM Collision Detected between {name} and {other.name}! Estimated Force: {force:F2} N");
                    }
                    else
                    {
                        bool hitXPPD = XPPDCollision(vertsA, vertsB);
                        if (hitXPPD)
                        {
                            float force = EstimateImpactForce(velocityA, velocityB);
                            Debug.Log($"🟢 XPPD Collision Detected between {name} and {other.name}! Estimated Force: {force:F2} N");
                        }
                        else
                        {
                            Debug.Log($"❌ No collision confirmed between {name} and {other.name}");
                        }
                    }
                }
            }
        }

        lastPosition = transform.position;
    }

    public Vector3 GetCurrentVelocity()
    {
        return (transform.position - lastPosition) / Time.deltaTime;
    }

    float EstimateImpactForce(Vector3 velA, Vector3 velB)
    {
        Vector3 relVel = velA - velB;
        float relSpeed = relVel.magnitude;
        float massEstimate = 1f;
        return 0.5f * massEstimate * relSpeed * relSpeed;
    }

    Vector3[] GetWorldVerts(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return new Vector3[0];
        Vector3[] local = mf.sharedMesh.vertices;
        Vector3[] world = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = go.transform.TransformPoint(local[i]);
        return world;
    }

    void DrawAABB(Vector3 c, Vector3 s, Color col)
    {
        Vector3 h = s * 0.5f;
        Vector3 min = c - h, max = c + h;
        Debug.DrawLine(min, new Vector3(max.x, min.y, min.z), col);
        Debug.DrawLine(min, new Vector3(min.x, max.y, min.z), col);
        Debug.DrawLine(min, new Vector3(min.x, min.y, max.z), col);
        Debug.DrawLine(max, new Vector3(min.x, max.y, max.z), col);
        Debug.DrawLine(max, new Vector3(max.x, min.y, max.z), col);
        Debug.DrawLine(max, new Vector3(max.x, max.y, min.z), col);
    }

    bool AABBIntersect(Vector3 aC, Vector3 aS, Vector3 bC, Vector3 bS)
    {
        Vector3 hA = aS * 0.5f;
        Vector3 hB = bS * 0.5f;
        return Mathf.Abs(aC.x - bC.x) <= hA.x + hB.x &&
               Mathf.Abs(aC.y - bC.y) <= hA.y + hB.y &&
               Mathf.Abs(aC.z - bC.z) <= hA.z + hB.z;
    }

    bool BoundingSphereIntersect(Vector3 aC, float rA, Vector3 bC, float rB)
    {
        float d2 = (aC - bC).sqrMagnitude;
        float R = rA + rB;
        return d2 <= R * R;
    }


    ///////
    bool GJKIntersect(Vector3[] A, Vector3[] B)
    {
        List<Vector3> simplex = new List<Vector3>();
        Vector3 dir = B[0] - A[0];
        simplex.Add(Support(A, B, dir));
        dir = -simplex[0];
        for (int i = 0; i < 50; i++)
        {
            Vector3 p = Support(A, B, dir);
            if (Vector3.Dot(p, dir) <= 0f) return false;
            simplex.Add(p);
            if (HandleSimplex(ref simplex, ref dir)) return true;
        }
        return false;
    }

    Vector3 Support(Vector3[] A, Vector3[] B, Vector3 d) => Farthest(A, d) - Farthest(B, -d);

    Vector3 Farthest(Vector3[] pts, Vector3 d)
    {
        float max = float.MinValue;
        Vector3 best = Vector3.zero;
        foreach (Vector3 p in pts)
        {
            float dot = Vector3.Dot(p, d);
            if (dot > max)
            {
                max = dot;
                best = p;
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

    bool EBMCollision(Transform A, Transform B)
    {
        MeshFilter mfA = A.GetComponent<MeshFilter>();
        MeshFilter mfB = B.GetComponent<MeshFilter>();
        if (mfA == null || mfB == null) return false;

        Vector3[] vA = mfA.sharedMesh.vertices;
        int[] tA = mfA.sharedMesh.triangles;
        Vector3[] vB = mfB.sharedMesh.vertices;
        int[] tB = mfB.sharedMesh.triangles;

        for (int i = 0; i < tA.Length; i += 3)
        {
            Vector3 a0 = A.TransformPoint(vA[tA[i]]);
            Vector3 a1 = A.TransformPoint(vA[tA[i + 1]]);
            Vector3 a2 = A.TransformPoint(vA[tA[i + 2]]);

            for (int j = 0; j < tB.Length; j += 3)
            {
                Vector3 b0 = B.TransformPoint(vB[tB[j]]);
                Vector3 b1 = B.TransformPoint(vB[tB[j + 1]]);
                Vector3 b2 = B.TransformPoint(vB[tB[j + 2]]);

                if (TriTriIntersect(a0, a1, a2, b0, b1, b2))
                    return true;
            }
        }
        return false;
    }

    bool TriTriIntersect(Vector3 V0, Vector3 V1, Vector3 V2,
                         Vector3 U0, Vector3 U1, Vector3 U2)
    {
        Vector3 N1 = Vector3.Cross(V1 - V0, V2 - V0);
        float d1 = -Vector3.Dot(N1, V0);
        float du0 = Vector3.Dot(N1, U0) + d1;
        float du1 = Vector3.Dot(N1, U1) + d1;
        float du2 = Vector3.Dot(N1, U2) + d1;
        if ((du0 > 0f && du1 > 0f && du2 > 0f) || (du0 < 0f && du1 < 0f && du2 < 0f)) return false;

        Vector3 N2 = Vector3.Cross(U1 - U0, U2 - U0);
        float d2 = -Vector3.Dot(N2, U0);
        float dv0 = Vector3.Dot(N2, V0) + d2;
        float dv1 = Vector3.Dot(N2, V1) + d2;
        float dv2 = Vector3.Dot(N2, V2) + d2;
        if ((dv0 > 0f && dv1 > 0f && dv2 > 0f) || (dv0 < 0f && dv1 < 0f && dv2 < 0f)) return false;

        Vector3 D = Vector3.Cross(N1, N2);
        float ax = Mathf.Abs(D.x), ay = Mathf.Abs(D.y), az = Mathf.Abs(D.z);
        int idx = (ax > ay) ? ((ax > az) ? 0 : 2) : ((ay > az) ? 1 : 2);

        float[] IA = Project(V0, V1, V2, D, idx);
        float[] IB = Project(U0, U1, U2, D, idx);
        if (IA[1] < IB[0] || IB[1] < IA[0]) return false;

        return true;
    }

    float[] Project(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 d, int i)
    {
        float s0 = Vector3.Dot(p0, d);
        float s1 = Vector3.Dot(p1, d);
        float s2 = Vector3.Dot(p2, d);
        float min = Mathf.Min(s0, s1, s2);
        float max = Mathf.Max(s0, s1, s2);
        return new float[] { min, max };
    }
    bool XPPDCollision(Vector3[] A, Vector3[] B)
    {
        // مثال مبدئي لتقاطع بالإسقاط الشعاعي Axis-Aligned
        for (int axis = 0; axis < 3; axis++)
        {
            float minA = float.MaxValue, maxA = float.MinValue;
            float minB = float.MaxValue, maxB = float.MinValue;

            foreach (var p in A)
            {
                float val = axis == 0 ? p.x : (axis == 1 ? p.y : p.z);
                minA = Mathf.Min(minA, val);
                maxA = Mathf.Max(maxA, val);
            }

            foreach (var p in B)
            {
                float val = axis == 0 ? p.x : (axis == 1 ? p.y : p.z);
                minB = Mathf.Min(minB, val);
                maxB = Mathf.Max(maxB, val);
            }

            if (maxA < minB || maxB < minA)
                return false;
        }
        return true;
    }
    public class OBB
    {
        public Vector3 Center;
        public Vector3[] Axes;
        public Vector3 HalfSizes;

        public OBB(Transform t)
        {
            Center = t.position;
            Axes = new Vector3[3];
            Axes[0] = t.right.normalized;
            Axes[1] = t.up.normalized;
            Axes[2] = t.forward.normalized;
            HalfSizes = Vector3.Scale(t.localScale, new Vector3(0.5f, 0.5f, 0.5f));
        }
    }

    public static class SATCollision
    {
        public static bool CheckOBBIntersection(OBB a, OBB b)
        {
            const float EPS = 1e-6f;
            Vector3 t = b.Center - a.Center;
            float[,] R = new float[3, 3];
            float[,] AbsR = new float[3, 3];

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    R[i, j] = Vector3.Dot(a.Axes[i], b.Axes[j]);
                    AbsR[i, j] = Mathf.Abs(R[i, j]) + EPS;
                }

            Vector3 T = new Vector3(
                Vector3.Dot(t, a.Axes[0]),
                Vector3.Dot(t, a.Axes[1]),
                Vector3.Dot(t, a.Axes[2])
            );

            for (int i = 0; i < 3; i++)
            {
                float ra = a.HalfSizes[i];
                float rb = b.HalfSizes.x * AbsR[i, 0] + b.HalfSizes.y * AbsR[i, 1] + b.HalfSizes.z * AbsR[i, 2];
                if (Mathf.Abs(T[i]) > ra + rb) return false;
            }

            for (int i = 0; i < 3; i++)
            {
                float ra = a.HalfSizes.x * AbsR[0, i] + a.HalfSizes.y * AbsR[1, i] + a.HalfSizes.z * AbsR[2, i];
                float rb = b.HalfSizes[i];
                if (Mathf.Abs(Vector3.Dot(t, b.Axes[i])) > ra + rb) return false;
            }

            return true;
        }
    }
}
