using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MassSpringMeshBuilder : MonoBehaviour
{
    public GameObject massPointPrefab;
    public float springStrength = 50f;
    public float damping = 2f;
    public float restitution = 0.3f;
    public float interactionForce = 500f;
    public float shapeMatchingStrength = 5f;

    private List<MassPoint> points = new List<MassPoint>();
    private List<Spring> springs = new List<Spring>();
    private Camera cam;
    private MassPoint selected;

    void Start()
    {
        cam = Camera.main;
        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;

        BuildMeshMassPoints();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Apply spring forces
        foreach (var s in springs)
            s.Apply(springStrength, dt);

        // Damping
        foreach (var p in points)
            p.velocity += -damping * p.velocity * dt;

        // Apply shape matching to preserve structure
        ApplyShapeMatching(dt);

        // Update positions
        foreach (var p in points)
        {
            p.transform.position += p.velocity * dt;

            // Ground collision (optional)
            if (p.transform.position.y < 0f)
            {
                Vector3 pos = p.transform.position;
                pos.y = 0f;
                p.transform.position = pos;

                if (p.velocity.y < 0f)
                    p.velocity.y = -p.velocity.y * restitution;
            }
        }

        // Mouse interaction
        if (selected != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float depth = Vector3.Distance(cam.transform.position, selected.transform.position);
            Vector3 target = ray.origin + ray.direction.normalized * depth;
            Vector3 force = (target - selected.transform.position) * interactionForce;
            selected.velocity += force * dt;
        }
    }

    void Update()
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
                    break;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
            selected = null;
    }

    void BuildMeshMassPoints()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Transform parent = this.transform;

        Dictionary<Vector3, MassPoint> pointMap = new Dictionary<Vector3, MassPoint>();
        Dictionary<int, MassPoint> indexToPoint = new Dictionary<int, MassPoint>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = parent.TransformPoint(vertices[i]);
            Vector3 localPos = vertices[i];
            if (!pointMap.ContainsKey(localPos))
            {
                var go = Instantiate(massPointPrefab, worldPos, Quaternion.identity, parent);
                var p = new MassPoint(go.transform, worldPos);
                pointMap[localPos] = p;
            }
            indexToPoint[i] = pointMap[localPos];
        }

        points.AddRange(pointMap.Values);

        HashSet<(MassPoint, MassPoint)> connected = new HashSet<(MassPoint, MassPoint)>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            MassPoint a = indexToPoint[triangles[i]];
            MassPoint b = indexToPoint[triangles[i + 1]];
            MassPoint c = indexToPoint[triangles[i + 2]];

            TryAddSpring(a, b);
            TryAddSpring(b, c);
            TryAddSpring(c, a);
        }

        void TryAddSpring(MassPoint a, MassPoint b)
        {
            var key = (a, b);
            var revKey = (b, a);
            if (connected.Contains(key) || connected.Contains(revKey)) return;
            connected.Add(key);
            springs.Add(new Spring(a, b));
        }
    }

    void ApplyShapeMatching(float dt)
    {
        Vector3 centerOfMass = Vector3.zero;
        foreach (var p in points)
            centerOfMass += p.transform.position;
        centerOfMass /= points.Count;

        Vector3 restCenter = Vector3.zero;
        foreach (var p in points)
            restCenter += p.restPosition;
        restCenter /= points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 goal = centerOfMass + (points[i].restPosition - restCenter);
            Vector3 correction = (goal - points[i].transform.position) * shapeMatchingStrength * dt;
            points[i].velocity += correction;
        }
    }

    public class MassPoint
    {
        public Transform transform;
        public Vector3 velocity = Vector3.zero;
        public Vector3 restPosition;

        public MassPoint(Transform t, Vector3 rest)
        {
            transform = t;
            restPosition = rest;
        }
    }

    public class Spring
    {
        public MassPoint p1, p2;
        float restLength;
   
        public Spring(MassPoint a, MassPoint b )
        {
            p1 = a; p2 = b;
            restLength = Vector3.Distance(a.transform.position, b.transform.position);
        
        }

        public void Apply(float k, float dt)
        {
            Vector3 dir = p2.transform.position - p1.transform.position;
            float dist = dir.magnitude;
            Vector3 f = k * (dist - restLength) * dir.normalized;
            p1.velocity += f * dt;
            p2.velocity -= f * dt;
        }
    }
    public List<Spring> GetSprings()
    {
        return springs;
    }

}