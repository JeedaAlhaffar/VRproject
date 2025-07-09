using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class FilledMassSpringSystem : MonoBehaviour
{
    public enum MaterialType { Slimy, Rubber, Solid, Custom }

    [Header("Material Settings")]
    public MaterialType material = MaterialType.Rubber;

    [Header("Custom Advanced Settings (only used if MaterialType == Custom)")]
    public float springStrength_Custom = 50f;
    public float damping_Custom = 2f;
    public float restitution_Custom = 0.3f;
    public float shapeMatchingStrength_Custom = 5f;
    public bool enableShear_Custom = true;
    public bool enableBending_Custom = false;
    public int constraintIterations_Custom = 2;
    public float constraintStiffness_Custom = 0.3f;
    public bool useCOMClamping_Custom = true;
    public float rotationalDamping_Custom = 0.1f;

    [Header("Spring Toggles")]
    public bool enableShear = true;
    public bool enableBending = false;

    [Header("Global Physics")]
    [Tooltip("Gravity applied to each mass point per second.")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Header("Interaction Settings")]
    public float interactionForce = 500f;

    [Header("Spring Colors")]
    public Color structuralColor = Color.yellow;
    public Color shearColor = Color.green;
    public Color bendingColor = Color.red;
    public Color couplingColor = Color.blue;


    [Header("Debug")]
    [Tooltip("How many FixedUpdate frames to wait between printing COM/spring diagnostics.")]
    public int debugInterval = 60;
    public bool enableDebugLogs = false;

    [Header("References")]
    public GameObject massPointPrefab;
    [Header("Fill Settings")]
    [Range(4, 32)] public int fillResolution = 8;

    private List<MassPoint> points = new();
    private List<Spring> springs = new();
    private Camera cam;
    private MassPoint selected;

    // Runtime parameters
    private float springStrength;
    private float damping;
    private float restitution;
    private float shapeMatchingStrength;
    private bool enableShearRuntime;
    private bool enableBendingRuntime;
    private int constraintIterations;
    private float constraintStiffness;
    private bool useCOMClamping;
    private float rotationalDamping;

    // For debugging and material change detection
    private int frameCounter = 0;
    private MaterialType lastMaterial;
    private bool lastEnableShear;
    private bool lastEnableBending;

    // Store original mesh data
    private Vector3[] originalVertices;
    private int[] originalTriangles;

    void Start()
    {
        cam = Camera.main;
        var renderer = GetComponent<MeshRenderer>();
        if (renderer) renderer.enabled = false;

        // Store original mesh data
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        originalTriangles = mesh.triangles;

        lastMaterial = material;
        lastEnableShear = enableShear;
        lastEnableBending = enableBending;

        ApplyMaterialSettings();
        //BuildMassSpringFromMesh();
        BuildMassSpringFull();

    }
    void BuildMassSpringFull()
    {
        // 1) grab mesh data
        var mf = GetComponent<MeshFilter>();
        var mesh = mf.mesh;
        var verts = mesh.vertices;
        var tris = mesh.triangles;

        // clear any previous
        points.Clear();
        springs.Clear();

        // 2) weld exterior vertices
        var weld = new Dictionary<Vector3, MassPoint>();
        var idx2pt = new MassPoint[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            var local = verts[i];
            if (!weld.TryGetValue(local, out var mp))
            {
                var world = transform.TransformPoint(local);
                var go = Instantiate(massPointPrefab, world, Quaternion.identity, transform);
                mp = new MassPoint(go.transform, local);
                weld[local] = mp;
                points.Add(mp);
            }
            idx2pt[i] = mp;
        }

        // 3) fill interior
        var bounds = mesh.bounds;
        Vector3 minW = transform.TransformPoint(bounds.min);
        Vector3 maxW = transform.TransformPoint(bounds.max);
        Vector3 step = (maxW - minW) / fillResolution;

        for (int x = 0; x < fillResolution; x++)
            for (int y = 0; y < fillResolution; y++)
                for (int z = 0; z < fillResolution; z++)
                {
                    Vector3 wP = minW + new Vector3((x + .5f) * step.x,
                                                    (y + .5f) * step.y,
                                                    (z + .5f) * step.z);
                    if (!PointInside(wP, verts, tris)) continue;

                    Vector3 local = transform.InverseTransformPoint(wP);
                    if (!weld.TryGetValue(local, out var ip))
                    {
                        var go = Instantiate(massPointPrefab, wP, Quaternion.identity, transform);
                        var rend = go.GetComponent<MeshRenderer>();
                        if (rend != null) rend.enabled = false;
                        go.transform.localPosition = local;
                        ip = new MassPoint(go.transform, local);
                        weld[local] = ip;
                        points.Add(ip);
                    }
                }

        // 4) build _all_ springs
        var connected = new HashSet<(MassPoint, MassPoint)>();

        //  4a) structural (mesh edges)
        for (int i = 0; i < tris.Length; i += 3)
        {
            TryAddSpring(idx2pt[tris[i + 0]], idx2pt[tris[i + 1]], structuralColor, connected);
            TryAddSpring(idx2pt[tris[i + 1]], idx2pt[tris[i + 2]], structuralColor, connected);
            TryAddSpring(idx2pt[tris[i + 2]], idx2pt[tris[i + 0]], structuralColor, connected);
        }

        //  4b) shear (face diagonals)
        if (enableShearRuntime)
            AddShearSprings(idx2pt, tris, connected);

        //  4c) bending (two‑hop neighbors)
        if (enableBendingRuntime)
            CreateBendingSprings(connected);

        //  4d) interior ↔ interior (grid connectivity)
        var grid = new Dictionary<Vector3Int, MassPoint>();
        foreach (var mp in points)
        {
            // identify interior & exterior by mapping restPosition→voxel index
            var idx = new Vector3Int(
                Mathf.FloorToInt(mp.restPosition.x / step.x),
                Mathf.FloorToInt(mp.restPosition.y / step.y),
                Mathf.FloorToInt(mp.restPosition.z / step.z)
            );
            grid[idx] = mp;
        }
        var offs = new[] { Vector3Int.right, Vector3Int.up, new Vector3Int(0, 0, 1) };
        foreach (var kv in grid)
            foreach (var d in offs)
                if (grid.TryGetValue(kv.Key + d, out var nb))
                    TryAddSpring(kv.Value, nb, shearColor, connected);

        if (material == MaterialType.Solid)
        {
            // Build a list of just the interior MassPoints:
            // (your grid dictionary only contains interior ones anyway)
            var interior = new List<MassPoint>(grid.Values);

            // Choose how many extra neighbors you want, and a cutoff radius
            int k = 12;                           // connect to 12 nearest neighbors
            float cutoff = step.magnitude * 1.5f;       // only if they’re within ~1.5 voxels

            // For each interior point, find its k nearest interior neighbors
            for (int i = 0; i < interior.Count; i++)
            {
                var a = interior[i];
                // Temporary list of (distance², MassPoint)
                var dists = new List<(float d2, MassPoint mp)>(interior.Count - 1);
                for (int j = 0; j < interior.Count; j++)
                {
                    if (i == j) continue;
                    var b = interior[j];
                    float d2 = (a.restPosition - b.restPosition).sqrMagnitude;
                    if (d2 <= cutoff * cutoff)
                        dists.Add((d2, b));
                }
                // sort by distance² ascending
                dists.Sort((x, y) => x.d2.CompareTo(y.d2));
                // take up to k nearest
                int take = Mathf.Min(k, dists.Count);
                for (int n = 0; n < take; n++)
                {
                    TryAddSpring(a, dists[n].mp, structuralColor, connected);
                }
            }
        }


        //  4e) coupling: each surface point to its nearest interior neighbor
        float maxd2 = (step.magnitude * 2f);
        maxd2 *= maxd2;
        foreach (var surf in weld.Values)
        {
            Vector3 sw = transform.TransformPoint(surf.restPosition);
            MassPoint best = null; float bd = maxd2;
            foreach (var ip in points)
            {
                Vector3 iw = transform.TransformPoint(ip.restPosition);
                float d2 = (sw - iw).sqrMagnitude;
                if (d2 > 0f && d2 < bd)
                {
                    bd = d2;
                    best = ip;
                }
            }
            if (best != null)
                TryAddSpring(surf, best, couplingColor, connected);
        }
    }
    bool PointInside(Vector3 wP, Vector3[] verts, int[] tris)
    {
        var ray = new Ray(wP, Vector3.up);
        int hits = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 A = transform.TransformPoint(verts[tris[i]]);
            Vector3 B = transform.TransformPoint(verts[tris[i + 1]]);
            Vector3 C = transform.TransformPoint(verts[tris[i + 2]]);
            if (RayTri(ray, A, B, C)) hits++;
        }
        return (hits & 1) == 1;
    }

    // classic Möller–Trumbore
    static bool RayTri(Ray ray, Vector3 a, Vector3 b, Vector3 c)
    {
        const float EPS = 1e-6f;
        var e1 = b - a; var e2 = c - a;
        var P = Vector3.Cross(ray.direction, e2);
        var det = Vector3.Dot(e1, P);
        if (Mathf.Abs(det) < EPS) return false;
        var inv = 1f / det;
        var T = ray.origin - a;
        var u = Vector3.Dot(T, P) * inv;
        if (u < 0 || u > 1) return false;
        var Q = Vector3.Cross(T, e1);
        var v = Vector3.Dot(ray.direction, Q) * inv;
        if (v < 0 || u + v > 1) return false;
        var t = Vector3.Dot(e2, Q) * inv;
        return t > EPS;
    }


    void Update()
    {
        // Check if material settings have changed
        if (material != lastMaterial || enableShear != lastEnableShear || enableBending != lastEnableBending)
        {
            Debug.Log($"Material changed from {lastMaterial} to {material}. Rebuilding system...");

            lastMaterial = material;
            lastEnableShear = enableShear;
            lastEnableBending = enableBending;

            // Destroy existing system
            DestroySystem();

            // Rebuild with new settings
            ApplyMaterialSettings();
            //BuildMassSpringFromMesh();
            BuildMassSpringFull();

        }

        HandleMouseInput();
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float minDist = 0.2f;
            selected = null;

            foreach (var p in points)
            {
                Vector3 toPoint = p.transform.position - ray.origin;
                float projection = Vector3.Dot(toPoint, ray.direction.normalized);
                Vector3 closest = ray.origin + ray.direction.normalized * projection;
                float dist = Vector3.Distance(p.transform.position, closest);

                if (dist < minDist)
                {
                    selected = p;
                    minDist = dist;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
            selected = null;
    }

    void DestroySystem()
    {
        // Destroy all mass point GameObjects
        foreach (var point in points)
        {
            if (point.transform != null)
                DestroyImmediate(point.transform.gameObject);
        }

        points.Clear();
        springs.Clear();
        selected = null;
    }

    void ApplyMaterialSettings()
    {
        switch (material)
        {
            case MaterialType.Slimy:
                springStrength = 20f;
                damping = 5f;
                restitution = 0.1f;
                shapeMatchingStrength = 0;
                enableShearRuntime = true;
                enableBendingRuntime = false;
                constraintIterations = 1;
                constraintStiffness = 0.1f;
                useCOMClamping = true;
                rotationalDamping = 0.05f;
                break;

            case MaterialType.Rubber:
                springStrength = 100f;
                damping = 2.5f;
                restitution = 0.6f;
                shapeMatchingStrength = 3f;
                enableShearRuntime = true;
                enableBendingRuntime = false;
                constraintIterations = 2;
                constraintStiffness = 0.5f;
                useCOMClamping = true;
                rotationalDamping = 0.1f;
                break;

            case MaterialType.Solid:
                springStrength = 200f;
                damping = 25f;
                restitution = 0.2f;
                shapeMatchingStrength = 10f;
                enableShearRuntime = true;
                enableBendingRuntime = true;
                constraintIterations = 5;
                constraintStiffness = 0.8f;
                useCOMClamping = true;
                rotationalDamping = 0.3f;
                break;

            case MaterialType.Custom:
                springStrength = springStrength_Custom;
                damping = damping_Custom;
                restitution = restitution_Custom;
                shapeMatchingStrength = shapeMatchingStrength_Custom;
                enableShearRuntime = enableShear_Custom;
                enableBendingRuntime = enableBending_Custom;
                constraintIterations = constraintIterations_Custom;
                constraintStiffness = constraintStiffness_Custom;
                useCOMClamping = useCOMClamping_Custom;
                rotationalDamping = rotationalDamping_Custom;
                break;
        }

        // Override with inspector toggles for non-custom materials
        if (material != MaterialType.Custom)
        {
            enableShearRuntime = enableShear;
            enableBendingRuntime = enableBending;
        }

        Debug.Log($"Applied {material} settings: SpringStr={springStrength}, Damping={damping}, Restitution={restitution}");
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        frameCounter++;

        // 1) Apply gravity
        foreach (var p in points)
            p.velocity += gravity * dt;

        // 2) Apply spring forces
        foreach (var spring in springs)
            spring.Apply(springStrength, dt);

        // 3) Apply damping
        foreach (var p in points)
            p.velocity *= (1f - damping * dt);

        // 4) Shape matching
        if (shapeMatchingStrength > 0f)
            ApplyShapeMatching(dt);

        // 5) Integration
        foreach (var p in points)
            p.transform.position += p.velocity * dt;

        // 6) Constraint solving
        if (constraintIterations > 0 && constraintStiffness > 0f)
        {
            SolveConstraints();
        }

        // 7) Collision handling
        HandleCollisions();

        // 8) Optional: COM clamping
        if (useCOMClamping)
            ApplyCOMClamping();

        // 9) Optional: Rotational damping
        if (rotationalDamping > 0f)
            ApplyRotationalDamping(dt);

        // 10) Mouse interaction
        HandleMouseDrag(dt);

        // 11) Debug output
        if (enableDebugLogs && frameCounter % debugInterval == 0)
            PrintDebugInfo();
    }

    void SolveConstraints()
    {
        for (int iter = 0; iter < constraintIterations; iter++)
        {
            foreach (var s in springs)
            {
                Vector3 delta = s.p2.transform.position - s.p1.transform.position;
                float dist = delta.magnitude;
                if (dist < 0.001f) continue;

                float diff = dist - s.restLength;
                Vector3 correction = (diff / dist) * delta * (0.5f * constraintStiffness);

                s.p1.transform.position += correction;
                s.p2.transform.position -= correction;
            }
        }
    }

    void HandleCollisions()
    {
        foreach (var p in points)
        {
            if (p.transform.position.y < 0f)
            {
                Vector3 pos = p.transform.position;
                pos.y = 0f;
                p.transform.position = pos;

                if (p.velocity.y < 0f)
                {
                    p.velocity.y = -p.velocity.y * restitution;

                    if (Mathf.Abs(p.velocity.y) < 0.1f)
                        p.velocity.y = 0f;
                }
            }
        }
    }

    void ApplyCOMClamping()
    {
        bool anyOnFloor = false;
        foreach (var p in points)
        {
            if (p.transform.position.y <= 0.001f)
            {
                anyOnFloor = true;
                break;
            }
        }

        if (anyOnFloor)
        {
            Vector3 comVel = Vector3.zero;
            foreach (var p in points)
                comVel += p.velocity;
            comVel /= points.Count;

            if (comVel.y < 0f)
            {
                foreach (var p in points)
                    p.velocity.y = Mathf.Max(0f, p.velocity.y);
            }
        }
    }
    public void ApplyImpactForce(Vector3 contactPoint, Vector3 force)
    {
        foreach (var p in points)
        {
            float dist = Vector3.Distance(p.transform.position, contactPoint);
            float weight = Mathf.Clamp01(1f - dist);
            p.velocity += force * weight;
        }
    }
    void ApplyRotationalDamping(float dt)
    {
        Vector3 comPos = Vector3.zero;
        Vector3 comVel = Vector3.zero;

        foreach (var p in points)
        {
            comPos += p.transform.position;
            comVel += p.velocity;
        }
        comPos /= points.Count;
        comVel /= points.Count;

        Vector3 totalAngularMomentum = Vector3.zero;
        float totalMass = 0f;

        foreach (var p in points)
        {
            Vector3 r = p.transform.position - comPos;
            Vector3 vRel = p.velocity - comVel;
            totalAngularMomentum += Vector3.Cross(r, vRel);
            totalMass += r.sqrMagnitude;
        }

        if (totalMass > 1e-6f)
        {
            Vector3 omega = totalAngularMomentum / totalMass;

            foreach (var p in points)
            {
                Vector3 r = p.transform.position - comPos;
                Vector3 vRot = Vector3.Cross(omega, r);
                p.velocity -= vRot * (rotationalDamping * dt);
            }
        }
    }

    void HandleMouseDrag(float dt)
    {
        if (selected != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float depth = Vector3.Distance(cam.transform.position, selected.transform.position);
            Vector3 target = ray.origin + ray.direction.normalized * depth;
            Vector3 force = (target - selected.transform.position) * interactionForce;
            selected.velocity += force * dt;
        }
    }

    void PrintDebugInfo()
    {
        Vector3 comPos = Vector3.zero;
        Vector3 comVel = Vector3.zero;

        foreach (var p in points)
        {
            comPos += p.transform.position;
            comVel += p.velocity;
        }
        comPos /= points.Count;
        comVel /= points.Count;

        float totalEnergy = 0f;
        foreach (var p in points)
            totalEnergy += p.velocity.sqrMagnitude;

        Debug.Log($"[{material}] Frame {frameCounter}: COM Pos {comPos:F2}, COM Vel {comVel:F2}, Energy {totalEnergy:F2}");
    }

    void BuildMassSpringFromMesh()
    {
        Transform parent = transform;
        Dictionary<Vector3, MassPoint> pointMap = new();
        Dictionary<int, MassPoint> indexToPoint = new();

        // Create mass points
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 worldPos = parent.TransformPoint(originalVertices[i]);
            Vector3 local = originalVertices[i];

            if (!pointMap.ContainsKey(local))
            {
                var go = Instantiate(massPointPrefab, worldPos, Quaternion.identity, parent);
                var p = new MassPoint(go.transform, worldPos);
                pointMap[local] = p;
                points.Add(p);
            }
            indexToPoint[i] = pointMap[local];
        }

        // Create springs
        HashSet<(MassPoint, MassPoint)> connected = new();

        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            MassPoint a = indexToPoint[originalTriangles[i]];
            MassPoint b = indexToPoint[originalTriangles[i + 1]];
            MassPoint c = indexToPoint[originalTriangles[i + 2]];

            // Structural springs (triangle edges)
            TryAddSpring(a, b, structuralColor, connected);
            TryAddSpring(b, c, structuralColor, connected);
            TryAddSpring(c, a, structuralColor, connected);

            // Shear springs (face diagonals)
            if (enableShearRuntime)
            {
                // AddShearSprings(indexToPoint, originalTriangles, connected);
            }
        }

        // Bending springs (connect vertices that are two edges apart)
        if (enableBendingRuntime)
        {
            CreateBendingSprings(connected);
        }

        Debug.Log($"Created {points.Count} points and {springs.Count} springs for {material} material");
    }

    void TryAddSpring(MassPoint a, MassPoint b, Color color, HashSet<(MassPoint, MassPoint)> connected)
    {
        var key = (a, b);
        var rev = (b, a);

        if (connected.Contains(key) || connected.Contains(rev))
            return;

        connected.Add(key);
        springs.Add(new Spring(a, b, color));
    }

    void CreateBendingSprings(HashSet<(MassPoint, MassPoint)> connected)
    {
        // Create bending springs by connecting points that are two edges apart
        var adjacencyMap = new Dictionary<MassPoint, List<MassPoint>>();

        // Build adjacency map
        foreach (var spring in springs)
        {
            if (!adjacencyMap.ContainsKey(spring.p1))
                adjacencyMap[spring.p1] = new List<MassPoint>();
            if (!adjacencyMap.ContainsKey(spring.p2))
                adjacencyMap[spring.p2] = new List<MassPoint>();

            adjacencyMap[spring.p1].Add(spring.p2);
            adjacencyMap[spring.p2].Add(spring.p1);
        }

        // Create bending springs
        foreach (var kvp in adjacencyMap)
        {
            var center = kvp.Key;
            var neighbors = kvp.Value;

            for (int i = 0; i < neighbors.Count; i++)
            {
                for (int j = i + 1; j < neighbors.Count; j++)
                {
                    TryAddSpring(neighbors[i], neighbors[j], bendingColor, connected);
                }
            }
        }
    }
    void AddShearSprings(MassPoint[] indexToPoint, int[] triangles, HashSet<(MassPoint, MassPoint)> connected)
    {
        // build edge→triangle map
        var edgeToTris = new Dictionary<(int, int), List<int>>();
        void AddEdge(int x, int y, int t)
        {
            var e = x < y ? (x, y) : (y, x);
            if (!edgeToTris.TryGetValue(e, out var lst)) { lst = new List<int>(); edgeToTris[e] = lst; }
            lst.Add(t);
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            AddEdge(triangles[i + 0], triangles[i + 1], i);
            AddEdge(triangles[i + 1], triangles[i + 2], i);
            AddEdge(triangles[i + 2], triangles[i + 0], i);
        }

        foreach (var kv in edgeToTris)
        {
            var shared = kv.Value;
            if (shared.Count != 2) continue;

            int t1 = shared[0], t2 = shared[1];
            var tri1 = new[] { triangles[t1 + 0], triangles[t1 + 1], triangles[t1 + 2] };
            var tri2 = new[] { triangles[t2 + 0], triangles[t2 + 1], triangles[t2 + 2] };

            var common = new HashSet<int>(tri1);
            common.IntersectWith(tri2);
            if (common.Count != 2) continue;

            int v1 = -1, v2 = -1;
            foreach (var v in tri1) if (!common.Contains(v)) v1 = v;
            foreach (var v in tri2) if (!common.Contains(v)) v2 = v;

            if (v1 >= 0 && v2 >= 0)
                TryAddSpring(indexToPoint[v1], indexToPoint[v2], shearColor, connected);
        }
    }


    void ApplyShapeMatching(float dt)
    {
        Vector3 com = Vector3.zero;
        foreach (var p in points)
            com += p.transform.position;
        com /= points.Count;

        Vector3 restCOM = Vector3.zero;
        foreach (var p in points)
            restCOM += p.restPosition;
        restCOM /= points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 goal = com + (points[i].restPosition - restCOM);
            Vector3 correction = (goal - points[i].transform.position) * shapeMatchingStrength * dt;
            points[i].velocity += correction;
        }
    }

    void OnDrawGizmos()
    {
        if (springs == null) return;

        foreach (var spring in springs)
        {
            Gizmos.color = spring.color;
            Gizmos.DrawLine(spring.p1.transform.position, spring.p2.transform.position);
        }

        if (selected != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(selected.transform.position, 0.1f);
        }
    }

    void OnDestroy()
    {
        DestroySystem();
    }

    public class MassPoint
    {
        public Transform transform;
        public Vector3 velocity;
        public Vector3 restPosition;

        public MassPoint(Transform t, Vector3 rest)
        {
            transform = t;
            restPosition = rest;
            velocity = Vector3.zero;
        }
    }

    public class Spring
    {
        public MassPoint p1, p2;
        public float restLength;
        public Color color;

        public Spring(MassPoint a, MassPoint b, Color c)
        {
            p1 = a;
            p2 = b;
            restLength = Vector3.Distance(a.transform.position, b.transform.position);
            color = c;
        }

        public void Apply(float k, float dt)
        {
            Vector3 dir = p2.transform.position - p1.transform.position;
            float dist = dir.magnitude;
            if (dist < 0.001f) return;

            Vector3 force = k * (dist - restLength) * dir.normalized;
            p1.velocity += force * dt;
            p2.velocity -= force * dt;
        }
    }
}