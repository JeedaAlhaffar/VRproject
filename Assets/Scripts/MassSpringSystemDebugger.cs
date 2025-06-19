//jeeda
using UnityEngine;
using System.Collections.Generic;

// Attach this to an empty GameObject, assign a small sphere prefab to massPointPrefab.
// The script will instantiate a grid of mass‐points and "springs" between them.

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MassSpringSystemDebugger : MonoBehaviour
{
    public enum MaterialType { Slimy, Rubber, Solid, Custom }

    [Header("Material Mode (choose one)")]
    public MaterialType material = MaterialType.Rubber;

    [Header("Custom Overrides (only used if MaterialType == Custom)")]
    public float springStrength_Custom = 80f;
    public float damping_Custom = 1.5f;
    public float restitution_Custom = 0.5f;
    public bool enableShear_Custom = true;
    public bool enableBending_Custom = true;
    public int constraintIterations_Custom = 2;
    public float constraintStiffness_Custom = 0.3f;
    public bool useCOMClamping_Custom = false;
    public float rotationalDamping_Custom = 0.1f;

    [Header("Grid & Prefab")]
    public GameObject massPointPrefab;
    public int gridSizeX = 3;
    public int gridSizeY = 3;
    public int gridSizeZ = 3;
    public float spacing = 1.0f;

    [Header("Global Physics")]
    [Tooltip("Gravity applied to each mass point per second.")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Header("Debug")]
    [Tooltip("How many FixedUpdate frames to wait between printing COM/spring diagnostics.")]
    public int debugInterval = 60;

    //–– Internals ––
    private List<MassPoint> points = new List<MassPoint>();
    private List<Spring> springs = new List<Spring>();

    // Dynamically chosen parameters (based on material selection or custom)
    private float springStrength;
    private float damping;
    private float restitution;
    private bool enableShear;
    private bool enableBending;
    private int constraintIterations;
    private float constraintStiffness;
    private bool useCOMClamping;
    private float rotationalDamping;

    // Mouse‐drag
    private MassPoint selectedPoint = null;
    private Camera mainCam;

    // For debugging
    private int frameCounter = 0;

    void Start()
    {
        mainCam = Camera.main;
        ApplyMaterialSettings();
        BuildPoints();
        BuildSprings();
    }

    void ApplyMaterialSettings()
    {
        switch (material)
        {
            case MaterialType.Slimy:
                springStrength = 20f;
                damping = 5f;   // anywhere 4–6
                restitution = 0f;
                enableShear = true;
                enableBending = false;
                constraintIterations = 1;
                constraintStiffness = 0.0f;  // rely on soft dynamics
                useCOMClamping = false;
                rotationalDamping = 0f;
                break;

            case MaterialType.Rubber:
                springStrength = 100f;  // anywhere 80–100
                damping = 2.5f;        // 1–2
                restitution = 0.5f;
                enableShear = true;
                enableBending = false;
                constraintIterations = 0;
                constraintStiffness = 0.0f;
                useCOMClamping = false;
                rotationalDamping = 0.0f;
                break;

            case MaterialType.Solid:
                springStrength = 150f;  // very large to mimic rigidity
                damping = 25f;          // heavy damping so it doesn’t oscillate
                restitution = 0f;       // no bounce
                enableShear = true;
                enableBending = true;
                constraintIterations = 10;
                constraintStiffness = 1.0f;   // fully enforce constraints
                useCOMClamping = true;
                rotationalDamping = 0.2f;
                break;

            case MaterialType.Custom:
                springStrength = springStrength_Custom;
                damping = damping_Custom;
                restitution = restitution_Custom;
                enableShear = enableShear_Custom;
                enableBending = enableBending_Custom;
                constraintIterations = constraintIterations_Custom;
                constraintStiffness = constraintStiffness_Custom;
                useCOMClamping = useCOMClamping_Custom;
                rotationalDamping = rotationalDamping_Custom;
                break;
        }
    }

    void Update()
    {
        // On mouse‐down: pick nearest mass point
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            float minDist = 0.2f;
            selectedPoint = null;

            foreach (var p in points)
            {
                Vector3 toPoint = p.transform.position - ray.origin;
                float proj = Vector3.Dot(toPoint, ray.direction.normalized);
                Vector3 closest = ray.origin + ray.direction.normalized * proj;
                float dist = Vector3.Distance(p.transform.position, closest);

                if (dist < minDist)
                {
                    selectedPoint = p;
                    break;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            selectedPoint = null;
        }
    }

    void BuildPoints()
    {
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    Vector3 pos = transform.TransformPoint(
                        new Vector3(x * spacing, y * spacing + 1f, z * spacing));
                    GameObject go = Instantiate(massPointPrefab, pos, Quaternion.identity, transform);
                    points.Add(new MassPoint(go.transform));
                }
            }
        }
    }

    void BuildSprings()
    {
        // Define colors for each spring type
        Color structuralColor = Color.blue;
        Color shearColor = Color.green;
        Color bendingColor = Color.red;

        // 1) Structural springs (axis‐aligned)
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    int i = GetIndex(x, y, z);

                    if (x < gridSizeX - 1)
                        springs.Add(new Spring(points[i], points[GetIndex(x + 1, y, z)], structuralColor));

                    if (y < gridSizeY - 1)
                        springs.Add(new Spring(points[i], points[GetIndex(x, y + 1, z)], structuralColor));

                    if (z < gridSizeZ - 1)
                        springs.Add(new Spring(points[i], points[GetIndex(x, y, z + 1)], structuralColor));
                }
            }
        }

        // 2) Shear springs (face‐diagonals)
        if (enableShear)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    for (int x = 0; x < gridSizeX; x++)
                    {
                        int i = GetIndex(x, y, z);

                        // XY face
                        if (x < gridSizeX - 1 && y < gridSizeY - 1)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y + 1, z)], shearColor));

                        if (x < gridSizeX - 1 && y > 0)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y - 1, z)], shearColor));

                        // XZ face
                        if (x < gridSizeX - 1 && z < gridSizeZ - 1)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y, z + 1)], shearColor));

                        if (x < gridSizeX - 1 && z > 0)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y, z - 1)], shearColor));

                        // YZ face
                        if (y < gridSizeY - 1 && z < gridSizeZ - 1)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y + 1, z + 1)], shearColor));

                        if (y < gridSizeY - 1 && z > 0)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y + 1, z - 1)], shearColor));
                    }
                }
            }
        }

        // 3) Bending springs (two‐points‐apart)
        if (enableBending)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    for (int x = 0; x < gridSizeX; x++)
                    {
                        int i = GetIndex(x, y, z);

                        if (x < gridSizeX - 2)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 2, y, z)], bendingColor));

                        if (y < gridSizeY - 2)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y + 2, z)], bendingColor));

                        if (z < gridSizeZ - 2)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y, z + 2)], bendingColor));
                    }
                }
            }
        }
    }

    int GetIndex(int x, int y, int z)
    {
        return z * gridSizeY * gridSizeX + y * gridSizeX + x;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        frameCounter++;

        // 1) Gravity on each mass‐point
        foreach (var p in points)
            p.velocity += gravity * dt;

        // 2) Hooke’s‐Law spring forces
        foreach (var s in springs)
            s.Apply(springStrength, dt);

        // 3) Global drag/damping: F_damp = −c * v
        foreach (var p in points)
            p.velocity += -damping * p.velocity * dt;

        // 4) Integrate positions
        foreach (var p in points)
            p.transform.position += p.velocity * dt;

        // 5) Floor collision (y=0), per‐point
        foreach (var p in points)
        {
            if (p.transform.position.y < 0f)
            {
                // pre‐bounce log
                Debug.Log($"[Floor] Pre‐Bounce p.velocity.y = {p.velocity.y:F3}");

                // clamp to floor
                Vector3 pos = p.transform.position;
                pos.y = 0f;
                p.transform.position = pos;

                // bounce if moving downward
                if (p.velocity.y < 0f)
                {
                    p.velocity.y = -p.velocity.y * restitution;

                    // if bounce is very small, clamp to zero
                    if (Mathf.Abs(p.velocity.y) < 0.1f)
                    {
                        p.velocity.y = 0f;
                        Debug.Log($"[Floor] Post‐Bounce p.velocity.y was tiny, clamped to 0");
                    }
                    else
                    {
                        Debug.Log($"[Floor] Post‐Bounce p.velocity.y = {p.velocity.y:F3}");
                    }
                }
            }
        }

        // 6) Optional: Position‐Based Constraint Projection
        if (constraintIterations > 0 && constraintStiffness > 0f)
        {
            for (int iter = 0; iter < constraintIterations; iter++)
            {
                foreach (var s in springs)
                {
                    Vector3 delta = s.p2.transform.position - s.p1.transform.position;
                    float dist = delta.magnitude;
                    if (dist == 0f) continue;
                    float diff = dist - s.restLength;
                    Vector3 correction = (diff / dist) * delta * (0.5f * constraintStiffness);
                    s.p1.transform.position += correction;
                    s.p2.transform.position -= correction;
                }
            }
        }

        // 7) Optional: COM‐Clamping (prevents slow “drift” if bottom is touching floor)
        if (useCOMClamping)
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
                // compute COM velocity
                Vector3 comVel = Vector3.zero;
                foreach (var p in points) comVel += p.velocity;
                comVel /= points.Count;

                if (comVel.y < 0f)
                {
                    foreach (var p in points)
                        p.velocity.y = 0f;
                    Debug.Log($"[COM Clamp] COMVel.y was {comVel.y:F3}, zeroed all p.velocity.y");
                }
            }
        }

        // 8) Optional: Rotational Damping (remove net spin around COM)
        if (rotationalDamping > 0f)
        {
            // a) compute COM position & velocity
            Vector3 comPos = Vector3.zero;
            Vector3 comVel = Vector3.zero;
            foreach (var p in points)
            {
                comPos += p.transform.position;
                comVel += p.velocity;
            }
            comPos /= points.Count;
            comVel /= points.Count;

            // b) compute total angular momentum L = Σ [ r_i × (v_i - comVel ) ]
            Vector3 totalAngularMomentum = Vector3.zero;
            float Iapprox = 0f;
            foreach (var p in points)
            {
                Vector3 r = p.transform.position - comPos;
                Vector3 vRel = p.velocity - comVel;
                totalAngularMomentum += Vector3.Cross(r, vRel);
                Iapprox += r.sqrMagnitude;
            }

            if (Iapprox > 1e-6f)
            {
                Vector3 omega = totalAngularMomentum / Iapprox;
                // c) drive each point’s relative velocity toward pure rotation
                foreach (var p in points)
                {
                    Vector3 r = p.transform.position - comPos;
                    Vector3 vRel = p.velocity - comVel;
                    Vector3 vRot = Vector3.Cross(omega, r);
                    Vector3 deltaV = vRot - vRel;
                    p.velocity += deltaV * (rotationalDamping * dt);
                }
            }
        }

        // 9) Zero out any tiny horizontal drift of the COM (keeps “solid” from sliding)
        {
            Vector3 comVel = Vector3.zero;
            foreach (var p in points) comVel += p.velocity;
            comVel /= points.Count;

            foreach (var p in points)
            {
                p.velocity.x -= comVel.x;
                p.velocity.z -= comVel.z;
            }
        }

        // 10) Mouse‐drag: apply a spring toward the mouse ray for the selected point
        if (selectedPoint != null)
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            float depth = Vector3.Distance(mainCam.transform.position, selectedPoint.transform.position);
            Vector3 targetPos = ray.origin + ray.direction.normalized * depth;
            Vector3 dragForce = (targetPos - selectedPoint.transform.position) * 500f;
            selectedPoint.velocity += dragForce * dt;
        }

        // 11) Periodic debug print (COM, L, I, Ω, AvgSpringStretch)
        if (frameCounter % debugInterval == 0)
        {
            // COM pos & vel
            Vector3 comPos = Vector3.zero;
            Vector3 comVel = Vector3.zero;
            foreach (var p in points)
            {
                comPos += p.transform.position;
                comVel += p.velocity;
            }
            comPos /= points.Count;
            comVel /= points.Count;

            // total angular momentum and Iapprox
            Vector3 totalAngMom = Vector3.zero;
            float Iapprox = 0f;
            foreach (var p in points)
            {
                Vector3 r = p.transform.position - comPos;
                Vector3 vRel = p.velocity - comVel;
                totalAngMom += Vector3.Cross(r, vRel);
                Iapprox += r.sqrMagnitude;
            }
            Vector3 omega = (Iapprox > 1e-6f) ? totalAngMom / Iapprox : Vector3.zero;

            // average spring‐stretch
            float sumStretch = 0f;
            foreach (var s in springs)
            {
                Vector3 d = s.p2.transform.position - s.p1.transform.position;
                float dist = d.magnitude;
                sumStretch += Mathf.Abs(dist - s.restLength);
            }
            float avgStretch = sumStretch / springs.Count;

            Debug.Log($"[Debug] Frame {frameCounter} ⇒ COM Pos {comPos:F3}, COM Vel {comVel:F3}");
            Debug.Log($"[Debug] TotalAngMom {totalAngMom:F3}, Iapprox {Iapprox:F3}, Omega {omega:F3}");
            Debug.Log($"[Debug] AvgSpringStretch {avgStretch:F4}");
        }
    }

    // Draw each spring with its assigned color
    void OnDrawGizmos()
    {
        if (springs == null) return;

        foreach (var s in springs)
        {
            Gizmos.color = s.color;
            Gizmos.DrawLine(s.p1.transform.position, s.p2.transform.position);
        }

        // Highlight the currently selected point in red
        if (selectedPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(selectedPoint.transform.position, 0.1f);
        }
    }

    //–– Helper Classes ––

    private class MassPoint
    {
        public Transform transform;
        public Vector3 velocity = Vector3.zero;

        public MassPoint(Transform t)
        {
            transform = t;
        }
    }

    private class Spring
    {
        public MassPoint p1, p2;
        public float restLength;
        public Color color;      // ← store the color to draw with

        public Spring(MassPoint a, MassPoint b, Color color)
        {
            p1 = a;
            p2 = b;
            this.color = color;
            restLength = Vector3.Distance(a.transform.position, b.transform.position);
        }

        public void Apply(float k, float dt)
        {
            Vector3 dir = p2.transform.position - p1.transform.position;
            float dist = dir.magnitude;
            if (dist == 0f) return;
            Vector3 force = k * (dist - restLength) * (dir / dist);
            p1.velocity += force * dt;
            p2.velocity -= force * dt;
        }
    }
}
////taghreed
//using UnityEngine;
//using System.Collections.Generic;

//public class MassSpringSystemDebugger : MonoBehaviour
//{
//    public Color springColor = Color.yellow;
//    private List<LineRenderer> lines = new List<LineRenderer>();

//    void OnDrawGizmos()
//    {
//        var builder = GetComponent<MassSpringMeshBuilder>();
//        if (builder == null) return;

//        Gizmos.color = springColor;

//        var mesh = GetComponent<MeshFilter>().sharedMesh;
//        if (mesh == null) return;

//        Vector3[] vertices = mesh.vertices;
//        int[] triangles = mesh.triangles;

//        for (int i = 0; i < triangles.Length; i += 3)
//        {
//            Vector3 a = transform.TransformPoint(vertices[triangles[i]]);
//            Vector3 b = transform.TransformPoint(vertices[triangles[i + 1]]);
//            Vector3 c = transform.TransformPoint(vertices[triangles[i + 2]]);

//            Gizmos.DrawLine(a, b);
//            Gizmos.DrawLine(b, c);
//            Gizmos.DrawLine(c, a);
//        }
//    }  
//}
