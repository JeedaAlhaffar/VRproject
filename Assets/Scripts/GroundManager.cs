using UnityEngine;

public class GroundManager : MonoBehaviour
{
    public static GroundManager I { get; private set; }

    [Tooltip("Assign your ground plane GameObject here (must have MeshFilter).")]
    public Transform groundPlane;

    [Tooltip("Optional extra sink offset (e.g. if you want a tiny tolerance).")]
    public float sinkOffset = 0f;

    float _planeTopY;

    void Awake()
    {
        if (I != null && I != this)
            Destroy(this);
        else
            I = this;
    }

    void Start()
    {
        if (groundPlane == null)
        {
            Debug.LogError("GroundManager: please assign a groundPlane transform.");
            return;
        }

        // Compute mesh‐bounds in local space:
        var mf = groundPlane.GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("GroundManager: assigned groundPlane has no MeshFilter!");
            return;
        }

        Bounds b = mf.sharedMesh.bounds;
        // The top face in local coordinates is at b.max.y
        Vector3 topLocal = new Vector3(0, b.max.y, 0);

        // Transform to world:
        _planeTopY = groundPlane.TransformPoint(topLocal).y + sinkOffset;
        Debug.Log($"[GroundManager] plane top Y = {_planeTopY:F3}");
    }

    /// <summary>
    /// All soft‐bodies should call this to get the world‑space Y of the ground surface.
    /// </summary>
    public static float PlaneTopY => I != null ? I._planeTopY : 0f;
}
