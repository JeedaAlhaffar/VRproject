using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MassSpringSystem : MonoBehaviour
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

    void Awake()
    {
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

        ApplyMaterialSettings();
        builder.Initialize(this, points, springs,  originalVertices, originalTriangles);
        builder.BuildMassSpringFull(fillResolution, structuralColor, shearColor, bendingColor, couplingColor, enableShearRuntime, enableBendingRuntime);
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

    void FixedUpdate()
    {
       
        frameCounter++;
        physics.UpdatePhysics(points, springs, springStrength, damping, restitution, shapeMatchingStrength, constraintIterations, constraintStiffness, useCOMClamping, rotationalDamping, gravity, interactionForce, enableDebugLogs, debugInterval, frameCounter);
        interaction.UpdateInteraction(points);
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
                shapeMatchingStrength = 6f;
                enableShearRuntime = true;
                enableBendingRuntime = true;
                constraintIterations = 10;
                constraintStiffness =1f;
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

        if (material != MaterialType.Custom)
        {
            enableShearRuntime = enableShear;
            enableBendingRuntime = enableBending;
        }

        Debug.Log($"Applied {material} settings: SpringStr={springStrength}, Damping={damping}, Restitution={restitution}");
    }
}