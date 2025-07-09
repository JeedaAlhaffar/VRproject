using UnityEngine;

public class Spring
{
    public MassPoint p1, p2;
    public float restLength;
    public Color color;

    public Spring(MassPoint a, MassPoint b, Color c)
    {
        p1 = a;
        p2 = b;
        restLength = Vector3.Distance(a.position, b.position);
        color = c;
    }

    public void Apply(float k, float dt)
    {
        Vector3 dir = p2.position - p1.position;
        float dist = dir.magnitude;
        if (dist < 0.001f) return;

        Vector3 force = k * (dist - restLength) * dir.normalized;
        p1.velocity += force * dt;
        p2.velocity -= force * dt;
    }
}