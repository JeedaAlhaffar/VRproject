using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MassSpringSystem : MonoBehaviour
{
    public enum MaterialType { Slimy, Rubber, Solid, Custom,Sand }

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
    [Header("Mass Settings")]
    [Tooltip("Total mass of the entire object in kilograms.")]
    public float totalMass = 1f;
    [Header("Compression Settings")]
    [Range(0f, 1f)]
    [Tooltip("How much height is preserved when resting on ground: 0=flat, 1=no settle.")]
    public float compressionFactor = 1f;
    [Header("Ground Settings (optional)")]
    public Transform groundPlane; // assign your Plane's transform in the Inspector
    [Header("Behavior")]
    [Tooltip("If true, this soft‐body will remain fixed and not fall under gravity.")]
    public bool isStaticBody = false;
    [Header("Spring Break Settings")]
    [Tooltip("Under this fraction of rest‐length the spring is slack (no force).")]
    [Range(0f, 1f)] public float minStretchFactor = 0.5f;

    [Tooltip("Above this fraction of rest‐length the spring breaks.")]
    [Range(1f, 3f)] public float maxStretchFactor = 1.5f;
    [Header("Environmental Forces")]
    [Tooltip("Constant wind acceleration (m/s²) applied each FixedUpdate.")]
    public Vector3 wind = new Vector3(0f, 0f, 0f);
    [Header("Spring Audio (one‑shot)")]
    public AudioClip clipSpringBreak;    // assign Chain Break(M4A_128K).m4a
    public AudioClip clipSpringSlack;    // assign Chains Being Broken 6(M4A_128K).m4a


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

    private List<MassPoint> points = new();
    private List<Spring> springs = new();
    private MassSpringBuilder builder;
    private MassSpringPhysics physics;
    private MassSpringInteraction interaction;
    public int[] surfacePointIndices;
    private float originalHeight;
    float floorY;      // world‑space Y of the mesh bottom
    float ceilingY;    // world‑space Y of the mesh top



    void Awake()
    {
        if (massPointPrefab == null)
        {
            massPointPrefab = Resources.Load<GameObject>("Prefabs/MassPoint");
            if (massPointPrefab == null)
            {
                Debug.LogError("❌ Could not find prefab at Resources/Prefabs/MassPoint. Make sure it exists!");
            }
        }
        builder = gameObject.AddComponent<MassSpringBuilder>();
        physics = gameObject.AddComponent<MassSpringPhysics>();
        interaction = gameObject.AddComponent<MassSpringInteraction>();
    }

    void Start()
    {

        var renderer = GetComponent<MeshRenderer>();
        if (renderer) renderer.enabled = false;

        // Store original mesh data
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        originalTriangles = mesh.triangles;

        lastMaterial = material; 
        lastEnableShear = enableShear;
        lastEnableBending = enableBending;
        Bounds b = mesh.bounds;
        Vector3 bottomLocal = new Vector3(0f, b.min.y, 0f);
        Vector3 topLocal = new Vector3(0f, b.max.y, 0f);

        floorY = transform.TransformPoint(bottomLocal).y;
        ceilingY = transform.TransformPoint(topLocal).y;
        // In MassSpringSystem.Start(), when assigning floorY for a static body:

        if (groundPlane != null)
        {
            // assume the plane also has a MeshFilter
            var mf = groundPlane.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // plane's local mesh bottom (min.y)
                float bottomLocalY = mf.sharedMesh.bounds.min.y;
                // convert to world Y
                floorY = groundPlane.TransformPoint(new Vector3(0, bottomLocalY, 0)).y;
            }
            else
            {
                floorY = groundPlane.position.y;
            }
        }

        Debug.Log($"[MassSpringSystem] floorY = {floorY}");

        ApplyMaterialSettings();
        builder.Initialize(this, points, springs,  originalVertices, originalTriangles);
        builder.BuildMassSpringFull(fillResolution, structuralColor, shearColor, bendingColor, couplingColor, enableShearRuntime, enableBendingRuntime);
        // ───────────────────────────────────────────────
        // NEW: Distribute totalMass evenly to each MassPoint
        if (points.Count > 0)
        {
            float perMass = totalMass / points.Count;
            foreach (var p in points)
            {
                p.mass = perMass;
                p.invMass = (perMass > 0f) ? 1f / perMass : 0f;
            }
        }
        else
        {
            Debug.LogWarning("MassSpringSystem: no MassPoints were generated.");
        }


    }
    public float GetCeilingY()
    {
        return ceilingY;
    }


    /// <summary>
    /// How tall is the object above the floor at rest.
    /// </summary>
    public float GetOriginalHeight()
    {
        return ceilingY - floorY;
    }

    /// <summary>
    /// The world‑space Y of the mesh bottom at rest.
    /// </summary>
    public float GetFloorY()
    {
        return floorY;
    }

    void Update()
    {
        if (material != lastMaterial || enableShear != lastEnableShear || enableBending != lastEnableBending)
        {
            Debug.Log($"Material changed from {lastMaterial} to {material}. Rebuilding system...");

            lastMaterial = material;
            lastEnableShear = enableShear;
            lastEnableBending = enableBending;

            builder.DestroySystem();
            ApplyMaterialSettings();
            builder.BuildMassSpringFull(fillResolution, structuralColor, shearColor, bendingColor, couplingColor, enableShearRuntime, enableBendingRuntime);
        }

        interaction.HandleMouseInput(points);
    }
    ///// <summary>
    ///// Called by physics to know the rest‐height above y=0.
    ///// </summary>
    //public float GetOriginalHeight()
    //{
    //    return originalHeight;
    //}
    void FixedUpdate()
    {
        // 1) early‐out static bodies
        if (isStaticBody)
            return;

        // 2) advance all the points & springs
        physics.UpdatePhysics(
            points, springs,
            springStrength, damping, restitution,
            shapeMatchingStrength, constraintIterations,
            constraintStiffness, useCOMClamping, rotationalDamping,
            gravity, interactionForce,
            enableDebugLogs, debugInterval,
            ++frameCounter);

        // 3) remove any springs that have Broken == true
        springs.RemoveAll(s => s.Broken);

        // 4) update any “picking / dragging” gizmos, etc.
        interaction.UpdateInteraction(points);

        // 5) synchronize your mesh vertices from the new point positions
        var mf = GetComponent<MeshFilter>();
        var mesh = mf.mesh;
        var verts = mesh.vertices; // (local-space)

        if (verts.Length == surfacePointIndices.Length)
        {
            for (int i = 0; i < surfacePointIndices.Length; i++)
            {
                int pi = surfacePointIndices[i];
                verts[i] = transform.InverseTransformPoint(points[pi].position);
            }
            mesh.vertices = verts;
            mesh.RecalculateBounds();
        }
        else
        {
            Debug.LogWarning("Vertex array length mismatch. Skipping mesh update.");
        }
        foreach (var pt in points)
        {
            // only update those that actually have a visual assigned
            if (pt.visual != null)
                pt.visual.position = pt.position;
        }
    }

    void OnDrawGizmos()
    {
        if (interaction != null  && springs != null)
            interaction.DrawGizmos(points,springs);
    }

    void OnDestroy()
    {
        builder.DestroySystem();
    }
    public List<MassPoint> GetPoints()
    {
        return points;
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
                compressionFactor = 1f;  // only 10% height preserved
                minStretchFactor = 0.1f;  // almost never slack
                maxStretchFactor = 2.7f;
                wind = new Vector3(-0.01f, 0, 0);   // gentle breeze


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
                useCOMClamping = true ;
                rotationalDamping = 0.1f;
                compressionFactor = 1f;  // 70% height preserved
                minStretchFactor = 0.2f;  // almost never slack
                maxStretchFactor = 90.0f; // stretches more before breaking
                wind = new Vector3(-0.01f, 0, 0);   // gentle breeze

                break;

            case MaterialType.Solid:
                springStrength = 200f;
                damping = 25f;
                restitution = 0.2f;
                shapeMatchingStrength = 6f;
                enableShearRuntime = true;
                enableBendingRuntime = true;
                constraintIterations = 10;
                constraintStiffness =1f;
                useCOMClamping = true;
                rotationalDamping = 0.3f;
                compressionFactor = 1f;    // 100% height preserved
                minStretchFactor = 0.3f;  // almost never slack
                maxStretchFactor = 2.0f;  // very stiff—break only at extreme stretch
                wind = new Vector3(-0.8f, 0, 0);   // gentle breeze

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
            case MaterialType.Sand:
                springStrength = 2f; // extremely weak springs
                damping = 10f;       // high damping to prevent bouncing
                restitution = 0.0f;  // no bounciness
                shapeMatchingStrength = 0f;
                enableShearRuntime = false;
                enableBendingRuntime = false;
                constraintIterations = 1;
                constraintStiffness = 0.05f; // almost free movement
                useCOMClamping = false;
                rotationalDamping = 0f;
                break;

        }

        if (material != MaterialType.Custom)
        {
            enableShearRuntime = enableShear;
            enableBendingRuntime = enableBending;
        }

        Debug.Log($"Applied {material} settings: SpringStr={springStrength}, Damping={damping}, Restitution={restitution}");
    }
    public float GetMass() => totalMass;
    public int GetDensity()
    {
        return fillResolution;
    }
    //public float GetFriction() => friction;

    public void SetMass(float value) => totalMass = Mathf.Max(0.01f, value);
    public void SetDensity(float value)
    {
        fillResolution = (int)Mathf.Clamp(value, 4, 32);
        RebuildSystem();
    }

    //public void SetFriction(float value) => friction = Mathf.Clamp01(value);

    // إعادة بناء النظام عند تغيّر القيم
    void RebuildSystem()
    {
        if (builder == null || originalVertices == null || originalTriangles == null) return;

        builder.DestroySystem();
        ApplyMaterialSettings();
        builder.Initialize(this, points, springs, originalVertices, originalTriangles);
        builder.BuildMassSpringFull(fillResolution, structuralColor, shearColor, bendingColor, couplingColor, enableShearRuntime, enableBendingRuntime);
    }

}