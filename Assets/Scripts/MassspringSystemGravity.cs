using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MassSpringSystemGravity : MonoBehaviour
{
    [Header("Prefabs & Grid")]
    public GameObject massPointPrefab;
    public int gridSizeX = 3;
    public int gridSizeY = 3;
    public int gridSizeZ = 3;
    public float spacing = 1.0f;

    [Header("Physic Parameters")]
    public float springStrength = 50f;       // k
    public float damping = 2f;               // c in F_damp = -c * v
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    [Range(0f, 1f)]
    public float restitution = 0.3f;         // floor bounce factor

    [Header("Optional Springs")]
    public bool enableShear = true;
    public bool enableBending = false;

    private List<MassPoint> points = new List<MassPoint>();
    private List<Spring> springs = new List<Spring>();

    private MassPoint selectedPoint = null;
    private Camera mainCam;

    void OnDrawGizmos()
    {
        if (springs == null) return;
        Gizmos.color = Color.yellow;
        foreach (var s in springs)
            Gizmos.DrawLine(s.p1.transform.position, s.p2.transform.position);

        if (selectedPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(selectedPoint.transform.position, 0.1f);
        }
    }

    void Start()
    {
        mainCam = Camera.main;
        BuildPoints();
        BuildSprings();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            float minDist = 0.2f;
            selectedPoint = null;

            foreach (var p in points)
            {
                Vector3 toPoint = p.transform.position - ray.origin;
                float projection = Vector3.Dot(toPoint, ray.direction.normalized);
                Vector3 closestPoint = ray.origin + ray.direction.normalized * projection;
                float dist = Vector3.Distance(p.transform.position, closestPoint);

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
            for (int y = 0; y < gridSizeY; y++)
                for (int x = 0; x < gridSizeX; x++)
                {
                    Vector3 pos = transform.TransformPoint(new Vector3(x * spacing, y * spacing + 1f, z * spacing));
                    var go = Instantiate(massPointPrefab, pos, Quaternion.identity, transform);
                    points.Add(new MassPoint(go.transform));
                }
    }

    void BuildSprings()
    {
        for (int z = 0; z < gridSizeZ; z++)
            for (int y = 0; y < gridSizeY; y++)
                for (int x = 0; x < gridSizeX; x++)
                {
                    int i = GetIndex(x, y, z);
                    if (x < gridSizeX - 1) springs.Add(new Spring(points[i], points[GetIndex(x + 1, y, z)]));
                    if (y < gridSizeY - 1) springs.Add(new Spring(points[i], points[GetIndex(x, y + 1, z)]));
                    if (z < gridSizeZ - 1) springs.Add(new Spring(points[i], points[GetIndex(x, y, z + 1)]));
                }

        if (enableShear)
        {
            for (int z = 0; z < gridSizeZ; z++)
                for (int y = 0; y < gridSizeY; y++)
                    for (int x = 0; x < gridSizeX; x++)
                    {
                        int i = GetIndex(x, y, z);
                        if (x < gridSizeX - 1 && y < gridSizeY - 1)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y + 1, z)]));
                        if (x < gridSizeX - 1 && y > 0)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y - 1, z)]));
                        if (x < gridSizeX - 1 && z < gridSizeZ - 1)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y, z + 1)]));
                        if (x < gridSizeX - 1 && z > 0)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 1, y, z - 1)]));
                        if (y < gridSizeY - 1 && z < gridSizeZ - 1)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y + 1, z + 1)]));
                        if (y < gridSizeY - 1 && z > 0)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y + 1, z - 1)]));
                    }
        }

        if (enableBending)
        {
            for (int z = 0; z < gridSizeZ; z++)
                for (int y = 0; y < gridSizeY; y++)
                    for (int x = 0; x < gridSizeX; x++)
                    {
                        int i = GetIndex(x, y, z);
                        if (x < gridSizeX - 2)
                            springs.Add(new Spring(points[i], points[GetIndex(x + 2, y, z)]));
                        if (y < gridSizeY - 2)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y + 2, z)]));
                        if (z < gridSizeZ - 2)
                            springs.Add(new Spring(points[i], points[GetIndex(x, y, z + 2)]));
                    }
        }
    }

    int GetIndex(int x, int y, int z)
        => z * gridSizeY * gridSizeX + y * gridSizeX + x;

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        foreach (var p in points)
            p.velocity += gravity * dt;

        foreach (var s in springs)
            s.Apply(springStrength, dt);

        foreach (var p in points)
            p.velocity += -damping * p.velocity * dt;

        foreach (var p in points)
        {
            p.transform.position += p.velocity * dt;

            if (p.transform.position.y < 0f)
            {
                Vector3 pos = p.transform.position;
                pos.y = 0f;
                p.transform.position = pos;

                if (p.velocity.y < 0f)
                    p.velocity.y = -p.velocity.y * restitution;
            }
        }

        // Mouse dragging
        if (selectedPoint != null)
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            float depth = Vector3.Distance(mainCam.transform.position, selectedPoint.transform.position);
            Vector3 targetPos = ray.origin + ray.direction.normalized * depth;
            Vector3 force = (targetPos - selectedPoint.transform.position) * 100f;
            selectedPoint.velocity += force * dt;
        }
    }

    class MassPoint
    {
        public Transform transform;
        public Vector3 velocity = Vector3.zero;
        public MassPoint(Transform t) => transform = t;
    }

    class Spring
    {
        public MassPoint p1, p2;
        float restLength;

        public Spring(MassPoint a, MassPoint b)
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
            p2.velocity += -f * dt;
        }
    }
}
