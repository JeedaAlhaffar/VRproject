using UnityEngine;
using System.Collections.Generic;

public class MassSpringInteraction : MonoBehaviour
{
    private Camera cam;
    private MassPoint selected;
    private float interactionForce;

    void Awake()
    {
        cam = Camera.main;
    }

    public void HandleMouseInput(List<MassPoint> points)
    {
        interactionForce = GetComponent<MassSpringSystem>().interactionForce;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float minDist = 0.2f; // Increase this if selection is hard
            selected = null;

            foreach (var p in points)
            {
                Vector3 toPoint = p.position - ray.origin;
                float proj = Vector3.Dot(toPoint, ray.direction);
                Vector3 closest = ray.origin + ray.direction * proj;
                float dist = Vector3.Distance(p.position, closest);

                if (dist < minDist)
                {
                    selected = p;
                    minDist = dist;
                }
            }

            //if (selected != null)
            //    Debug.Log("Selected point at: " + selected.position);
        }

        if (Input.GetMouseButtonUp(0))
        {
            selected = null;
        }
    }

    public void UpdateInteraction(List<MassPoint> points)
    {
        if (selected != null)
        {
            float dt = Time.fixedDeltaTime;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            Plane dragPlane = new Plane(-cam.transform.forward, selected.position);
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 target = ray.GetPoint(enter);
                Vector3 force = (target - selected.position) * interactionForce;
                selected.velocity += force * dt;
            }
        }
    }

    public void DrawGizmos(List<MassPoint> points, List<Spring> springs)
    {
        if (springs != null)
        {
            foreach (var spring in springs)
            {
                Gizmos.color = spring.color;
                Gizmos.DrawLine(spring.p1.position, spring.p2.position);
            }
        }

        if (points != null)
        {
            foreach (var p in points)
            {
                Gizmos.color = (p == selected) ? Color.red : Color.gray;
                Gizmos.DrawSphere(p.position, 0.03f);
            }
        }
    }
}
