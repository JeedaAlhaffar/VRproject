
using UnityEngine;
using System.Collections.Generic;

public class MassSpringPhysics : MonoBehaviour
{
    public void UpdatePhysics(List<MassPoint> points, List<Spring> springs, float springStrength, float damping, float restitution, float shapeMatchingStrength, int constraintIterations, float constraintStiffness, bool useCOMClamping, float rotationalDamping, Vector3 gravity, float interactionForce, bool enableDebugLogs, int debugInterval, int frameCounter)
    {
        float dt = Time.fixedDeltaTime;

        //// Apply gravity
        //foreach (var p in points)
        //    p.velocity += gravity * dt;

        // Apply spring forces
        foreach (var spring in springs)
            spring.Apply(springStrength, dt);

        // Apply damping
        foreach (var p in points)
            p.velocity *= (1f - damping * dt);

        // Shape matching
        if (shapeMatchingStrength > 0f)
            ApplyShapeMatching(points, shapeMatchingStrength, dt);

        // Integration
        foreach (var p in points)
            p.position += p.velocity * dt;

        // Constraint solving
        if (constraintIterations > 0 && constraintStiffness > 0f)
            SolveConstraints(springs, constraintIterations, constraintStiffness);

        // Collision handling
        HandleCollisions(points, restitution);
        // Simple ground collision
        //foreach (var p in points)
        //{
        //    if (p.position.y < 0f)
        //    {
        //        p.position.y = 0f;
        //        if (p.velocity.y < 0f)
        //            p.velocity.y = -p.velocity.y * restitution;
        //    }
        //}
        // COM clamping
        if (useCOMClamping)
            ApplyCOMClamping(points);

        // Rotational damping
        if (rotationalDamping > 0f)
            ApplyRotationalDamping(points, rotationalDamping, dt);

        // Debug output
        if (enableDebugLogs && frameCounter % debugInterval == 0)
            PrintDebugInfo(points, frameCounter);
    }

    public void ApplyImpactForce(List<MassPoint> points, Vector3 contactPoint, Vector3 force)
    {
        foreach (var p in points)
        {
            float dist = Vector3.Distance(p.position, contactPoint);
            float weight = Mathf.Clamp01(1f - dist);
            p.velocity += force * weight;
        }
        //GetComponent<MassSpringSystem>().SaveState(); // Save state after applying collision force
    }

    void ApplyShapeMatching(List<MassPoint> points, float shapeMatchingStrength, float dt)
    {
        Vector3 com = Vector3.zero;
        foreach (var p in points)
            com += p.position;
        com /= points.Count;

        Vector3 restCOM = Vector3.zero;
        foreach (var p in points)
            restCOM += p.restPosition;
        restCOM /= points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 goal = com + (points[i].restPosition - restCOM);
            Vector3 correction = (goal - points[i].position) * shapeMatchingStrength * dt;
            points[i].velocity += correction;
        }
    }

    void SolveConstraints(List<Spring> springs, int constraintIterations, float constraintStiffness)
    {
        for (int iter = 0; iter < constraintIterations; iter++)
        {
            foreach (var s in springs)
            {
                Vector3 delta = s.p2.position - s.p1.position;
                float dist = delta.magnitude;
                if (dist < 0.001f) continue;

                float diff = dist - s.restLength;
                Vector3 correction = (diff / dist) * delta * (0.5f * constraintStiffness);
                s.p1.position += correction;
                s.p2.position -= correction;
            }
        }
    }

    void HandleCollisions(List<MassPoint> points, float restitution)
    {
        foreach (var p in points)
        {
            if (p.position.y < 0f)
            {
                Vector3 pos = p.position;
                pos.y = 0f;
                p.position = pos;

                if (p.velocity.y < 0f)
                {
                    // Apply stronger bounce and preserve some vertical velocity
                    p.velocity.y = -p.velocity.y * restitution * 1.2f; // Slightly boost restitution
                    if (Mathf.Abs(p.velocity.y) < 0.2f) // Increased threshold to allow recovery
                        p.velocity.y = Mathf.Max(p.velocity.y, 0f); // Prevent sticking
                }
            }
        }
    }

    void ApplyCOMClamping(List<MassPoint> points)
    {
        bool anyOnFloor = false;
        foreach (var p in points)
        {
            if (p.position.y <= 0.001f)
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

            // Only clamp if COM velocity is significantly downward
            //if (comVel.y < -0.1f) // Relaxed threshold to allow bounce-back
            //{
            //    foreach (var p in points)
            //    {
            //        if (p.transform.position.y <= 0.001f)
            //            p.velocity.y = Mathf.Max(p.velocity.y, 0f);
            //        else
            //            p.velocity.y *= 0.9f; // Gentle damping for non-ground points
            //    }
            //}
        }
    }

    void ApplyRotationalDamping(List<MassPoint> points, float rotationalDamping, float dt)
    {
        Vector3 comPos = Vector3.zero;
        Vector3 comVel = Vector3.zero;
        foreach (var p in points)
        {
            comPos += p.position;
            comVel += p.velocity;
        }
        comPos /= points.Count;
        comVel /= points.Count;

        Vector3 totalAngularMomentum = Vector3.zero;
        float totalMass = 0f;
        foreach (var p in points)
        {
            Vector3 r = p.position - comPos;
            Vector3 vRel = p.velocity - comVel;
            totalAngularMomentum += Vector3.Cross(r, vRel);
            totalMass += r.sqrMagnitude;
        }

        if (totalMass > 1e-6f)
        {
            Vector3 omega = totalAngularMomentum / totalMass;
            foreach (var p in points)
            {
                Vector3 r = p.position - comPos;
                Vector3 vRot = Vector3.Cross(omega, r);
                p.velocity -= vRot * (rotationalDamping * dt);
            }
        }
    }

    void PrintDebugInfo(List<MassPoint> points, int frameCounter)
    {
        Vector3 comPos = Vector3.zero;
        Vector3 comVel = Vector3.zero;
        foreach (var p in points)
        {
            comPos += p.position;
            comVel += p.velocity;
        }
        comPos /= points.Count;
        comVel /= points.Count;

        float totalEnergy = 0f;
        foreach (var p in points)
            totalEnergy += p.velocity.sqrMagnitude;

        Debug.Log($"[{GetComponent<MassSpringSystem>().material}] Frame {frameCounter}: COM Pos {comPos:F2}, COM Vel {comVel:F2}, Energy {totalEnergy:F2}");
    }
}
