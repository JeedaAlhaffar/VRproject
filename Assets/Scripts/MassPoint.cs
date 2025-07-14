using UnityEngine;

public class MassPoint
{
    public Vector3 position;      // World position
    public Vector3 restPosition;  // Local rest position
    public Vector3 velocity;
    // NEW:
    public float mass = 1f;
    public float invMass = 1f;
    public Transform visual = null;

    public MassPoint(Vector3 worldPos, Vector3 localRestPos)
    {
        position = worldPos;
        restPosition = localRestPos;
        velocity = Vector3.zero;
    }
}
