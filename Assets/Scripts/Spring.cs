using UnityEngine;

public class Spring
{
    public MassPoint p1, p2;
    public float restLength;
    public Color color;
    public MassSpringSystem system;     // <-- reference

    public float minStretch = 0.5f;
    public float maxStretch = 1.5f;
    bool _broken = false;

    public Spring(MassPoint a, MassPoint b, Color c, MassSpringSystem sys)
    {
        p1 = a;
        p2 = b;
        color = c;
        restLength = Vector3.Distance(a.position, b.position);
        system = sys;                  // capture the system
    }

    public bool Broken => _broken;

    public void Apply(float k, float dt)
    {
        if (_broken)
            return;

        Vector3 delta = p2.position - p1.position;
        float dist = delta.magnitude;
        if (dist < 1e-6f)
            return;

        float ratio = dist / restLength;

        // 1) Over‑stretch → break sound
        if (ratio >= maxStretch)
        {
            _broken = true;
            if (system != null && system.clipSpringBreak != null)
            {
                // play at the spring's midpoint
                Vector3 pos = (p1.position + p2.position) * 0.5f;
                AudioSource.PlayClipAtPoint(system.clipSpringBreak, pos);
            }
            return;
        }

        // 2) Over‑compress → slack sound
        if (ratio <= minStretch)
        {
            if (system != null && system.clipSpringSlack != null)
            {
                Vector3 pos = (p1.position + p2.position) * 0.5f;
                AudioSource.PlayClipAtPoint(system.clipSpringSlack, pos);
            }
            return;
        }

        // 3) Normal Hooke’s force
        Vector3 force = k * (dist - restLength) * (delta / dist);
        p1.velocity += force * (dt * p1.invMass);
        p2.velocity -= force * (dt * p2.invMass);
    }
}
