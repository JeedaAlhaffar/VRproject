using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class UnifiedMassSpringSystem : MonoBehaviour
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
    public bool useCOMClamping_Custom = false;
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
    private Mesh mesh;
    private Vector3[] localVerts;
    private int[] tris;
    private Vector3 voxelStep;

    // For debugging and material change detection
    private int frameCounter = 0;
    private MaterialType lastMaterial;
    private bool lastEnableShear;
    private bool lastEnableBending;

    // Store original mesh data
    private Vector3[] originalVertices;
    private int[] originalTriangles;

    void Awake()
    {
        cam = Camera.main;
        mesh = GetComponent<MeshFilter>().mesh;
        localVerts = mesh.vertices;
        tris = mesh.triangles;
        Debug.Log("Vertex count: " + localVerts.Length);


    }
    //void Start()
    //{
    //    cam = Camera.main;
    //    var renderer = GetComponent<MeshRenderer>();
    //    if (renderer) renderer.enabled = false;

    //    // Store original mesh data
    //    Mesh mesh = GetComponent<MeshFilter>().mesh;
    //    originalVertices = mesh.vertices;
    //    originalTriangles = mesh.triangles;

    //    lastMaterial = material;
    //    lastEnableShear = enableShear;
    //    lastEnableBending = enableBending;

    //    ApplyMaterialSettings();
    //    BuildMassSpringFromMesh();
    //}

    void Start()
    {
        lastMaterial = material;
        lastEnableShear = enableShear;
        lastEnableBending = enableBending;
        // disable original visual
        var r = GetComponent<MeshRenderer>();
        if (r) r.enabled = false;

        // apply material
        ApplyMaterialSettings();
        //BuildMassSpringFromMesh();

        // build all mass points + springs
        BuildSurface();
        BuildInterior();
        BuildAllSprings();
        Debug.Log("Vertex count1: " + localVerts.Length);

    }
    void BuildSurface()
    {
        // one mass point per vertex, **localPosition** so they move with mesh
        foreach (var v in localVerts)
        {
            var go = Instantiate(massPointPrefab, Vector3.zero, Quaternion.identity, transform);
            go.transform.localPosition = v;
            points.Add(new MassPoint(go.transform, v));
        }
    }

    void BuildInterior()
    {
        var bounds = mesh.bounds;
        // compute world‐space voxel step
        Vector3 minW = transform.TransformPoint(bounds.min);
        Vector3 maxW = transform.TransformPoint(bounds.max);
        voxelStep = (maxW - minW) / fillResolution;

        // sample interior
        for (int x = 0; x < fillResolution; x++)
            for (int y = 0; y < fillResolution; y++)
                for (int z = 0; z < fillResolution; z++)
                {
                    Vector3 wC = minW + new Vector3(
                        (x + .5f) * voxelStep.x, (y + .5f) * voxelStep.y, (z + .5f) * voxelStep.z
                    );
                    if (!PointInside(wC)) continue;

                    // spawn in **local** coords
                    Vector3 lC = transform.InverseTransformPoint(wC);
                    var go = Instantiate(massPointPrefab, Vector3.zero, Quaternion.identity, transform);
                    go.transform.localPosition = lC;
                    points.Add(new MassPoint(go.transform, lC));
                }
    }
    bool PointInside(Vector3 wP)
    {
        // ray‐cast up
        var r = new Ray(wP, Vector3.up);
        int hits = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 A = transform.TransformPoint(localVerts[tris[i]]);
            Vector3 B = transform.TransformPoint(localVerts[tris[i + 1]]);
            Vector3 C = transform.TransformPoint(localVerts[tris[i + 2]]);
            if (RayTri(r, A, B, C)) hits++;
        }
        return (hits & 1) == 1;
    }

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
    void AddSpring(MassPoint a, MassPoint b, Color col)
    {
        springs.Add(new Spring(a, b, col));
    }

    void BuildAllSprings()
    {
        var n = points.Count;
        // surface–surface: along mesh triangles
        // build index->masspoint for first localVerts.Length points
        var surfMap = new MassPoint[localVerts.Length];
        for (int i = 0; i < localVerts.Length; i++) surfMap[i] = points[i];
        for (int i = 0; i < tris.Length; i += 3)
        {
            AddSpring(surfMap[tris[i + 0]], surfMap[tris[i + 1]], structuralColor);
            AddSpring(surfMap[tris[i + 1]], surfMap[tris[i + 2]], structuralColor);
            AddSpring(surfMap[tris[i + 2]], surfMap[tris[i + 0]], structuralColor);
        }

        // interior–interior: grid connectivity
        int surfaceCount = localVerts.Length;
        int interiorCount = n - surfaceCount;
        // map interior to grid index
        var grid = new Dictionary<Vector3Int, MassPoint>();
        for (int i = surfaceCount; i < n; i++)
        {
            var mp = points[i];
            var idx = new Vector3Int(
                Mathf.FloorToInt(mp.restPosition.x / voxelStep.x),
                Mathf.FloorToInt(mp.restPosition.y / voxelStep.y),
                Mathf.FloorToInt(mp.restPosition.z / voxelStep.z)
            );
            grid[idx] = mp;
        }
        var offs = new[] { Vector3Int.right, Vector3Int.up, new Vector3Int(0, 0, 1) };
        foreach (var kv in grid)
            foreach (var d in offs)
                if (grid.TryGetValue(kv.Key + d, out var nb))
                    AddSpring(kv.Value, nb, shearColor);

        // coupling: each surface to nearest interior
        float maxd = voxelStep.magnitude * 2f, maxd2 = maxd * maxd;
        for (int i = 0; i < surfaceCount; i++)
        {
            var s = points[i];
            MassPoint best = null; float bd = maxd2;
            Vector3 sw = s.transform.position;
            for (int j = surfaceCount; j < n; j++)
            {
                var ip = points[j];
                float d2 = (sw - ip.transform.position).sqrMagnitude;
                if (d2 < bd) { bd = d2; best = ip; }
            }
            if (best != null) AddSpring(s, best, couplingColor);
        }
        // right after your structural springs loop in BuildAllSprings():
        if (enableBendingRuntime)
        {
            var surfAdj = new Dictionary<MassPoint, HashSet<MassPoint>>();
            // build adjacency from structural (yellow) springs
            foreach (var s in springs)
            {
                if (s.color != structuralColor) continue;
                if (!surfAdj.ContainsKey(s.p1)) surfAdj[s.p1] = new HashSet<MassPoint>();
                if (!surfAdj.ContainsKey(s.p2)) surfAdj[s.p2] = new HashSet<MassPoint>();
                surfAdj[s.p1].Add(s.p2);
                surfAdj[s.p2].Add(s.p1);
            }
            // connect two‑hop neighbors
            foreach (var kv in surfAdj)
            {
                var center = kv.Key;
                foreach (var mid in kv.Value)
                    foreach (var end in surfAdj[mid])
                        if (end != center && !surfAdj[center].Contains(end))
                            AddSpring(center, end, bendingColor);
            }
        }

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
            BuildMassSpringFromMesh();
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
        Debug.Log($"💥 ApplyImpactForce called! Point: {contactPoint}, Force: {force}");

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
                AddShearSprings(indexToPoint, originalTriangles, connected);
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
    void AddShearSprings(Dictionary<int, MassPoint> indexToPoint, int[] triangles, HashSet<(MassPoint, MassPoint)> connected)
    {
        // بناء خريطة الحواف
        Dictionary<(int, int), List<int>> edgeToTriangles = new();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int[] tri = { triangles[i], triangles[i + 1], triangles[i + 2] };

            AddEdge(tri[0], tri[1], i);
            AddEdge(tri[1], tri[2], i);
            AddEdge(tri[2], tri[0], i);
        }

        void AddEdge(int a, int b, int triIndex)
        {
            var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
            if (!edgeToTriangles.ContainsKey(edge))
                edgeToTriangles[edge] = new List<int>();
            edgeToTriangles[edge].Add(triIndex);
        }

        // الآن نبحث عن حواف مشتركة بين مثلثين
        foreach (var pair in edgeToTriangles)
        {
            var shared = pair.Value;
            if (shared.Count == 2)
            {
                int t1 = shared[0];
                int t2 = shared[1];

                int[] tri1 = { triangles[t1], triangles[t1 + 1], triangles[t1 + 2] };
                int[] tri2 = { triangles[t2], triangles[t2 + 1], triangles[t2 + 2] };

                HashSet<int> common = new(tri1);
                common.IntersectWith(tri2);

                if (common.Count == 2)
                {
                    int v1 = -1, v2 = -1;
                    foreach (int v in tri1) if (!common.Contains(v)) v1 = v;
                    foreach (int v in tri2) if (!common.Contains(v)) v2 = v;

                    if (v1 != -1 && v2 != -1)
                    {
                        TryAddSpring(indexToPoint[v1], indexToPoint[v2], shearColor, connected);
                    }
                }
            }
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


    // تابع LateUpdate هنا
    void LateUpdate()
    {
        if (points == null || points.Count == 0) return;

        Vector3 center = Vector3.zero;
        foreach (var p in points)
            center += p.transform.position;

        center /= points.Count;
        transform.position = center;
    }

}
