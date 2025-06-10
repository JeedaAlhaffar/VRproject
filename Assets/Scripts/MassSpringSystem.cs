using UnityEngine;
using System.Collections.Generic;

public class MassSpringSystem : MonoBehaviour
{
    public GameObject massPointPrefab;
    public Transform sphereCenter;  // Assign this in the Inspector or it will auto-calculate
    public float sphereRadius = 1.5f;
    public int gridSizeX = 4;
    public int gridSizeY = 4;
    public float spacing = 1.0f;
    public float springStrength = 50f;
    public float damping = 2f;

    private List<MassPoint> points = new List<MassPoint>();
    private List<Spring> springs = new List<Spring>();
    private List<MassPoint> spherePoints = new List<MassPoint>();

    void Start()
    {
        int gridSizeZ = gridSizeX;
        float cubeSpacing = spacing;

        if (massPointPrefab == null)
        {
            Debug.LogError("MassPointPrefab not assigned!");
            enabled = false;
            return;
        }

        // Step 1: Create cube particles
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    Vector3 pos = new Vector3(x, y, z) * cubeSpacing + transform.position;
                    GameObject obj = Instantiate(massPointPrefab, pos, Quaternion.identity);
                    MassPoint p = new MassPoint(obj.transform);
                    p.velocity = Vector3.zero;

                    // Fix top corners
                    if (y == gridSizeY - 1 && (x == 0 || x == gridSizeX - 1) && (z == 0 || z == gridSizeZ - 1))
                        p.isFixed = true;

                    points.Add(p);
                }
            }
        }

        // Step 2: Create springs between cube points
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    int index = x + y * gridSizeX + z * gridSizeX * gridSizeY;
                    if (x < gridSizeX - 1)
                        CreateSpring(index, index + 1);
                    if (y < gridSizeY - 1)
                        CreateSpring(index, index + gridSizeX);
                    if (z < gridSizeZ - 1)
                        CreateSpring(index, index + gridSizeX * gridSizeY);
                }
            }
        }

        // Step 3: Create sphere
        CreateSphere(10, 20, sphereRadius);
    }

    void FixedUpdate()
    {
        Vector3 gravity = new Vector3(0, -9.81f, 0);

        foreach (var point in points)
            point.velocity += gravity * Time.fixedDeltaTime;
        foreach (var point in spherePoints)
            point.velocity += Vector3.zero;

        foreach (var spring in springs)
            spring.Apply(springStrength, damping);

        foreach (var p in points)
        {
            p.transform.position += p.velocity * Time.fixedDeltaTime;
            if (!p.isFixed)
                p.velocity *= (1 - damping * Time.fixedDeltaTime);
        }

        foreach (var p in spherePoints)
        {
            p.transform.position += p.velocity * Time.fixedDeltaTime;
            if (!p.isFixed)
                p.velocity *= (1 - damping * Time.fixedDeltaTime);
        }

        HandleCollisionCubeSphere();
    }

    void CreateSpring(int i, int j)
    {
        var p1 = points[i];
        var p2 = points[j];
        springs.Add(new Spring(p1, p2));
    }

    void CreateSpringSphere(int i, int j)
    {
        var p1 = spherePoints[i];
        var p2 = spherePoints[j];
        springs.Add(new Spring(p1, p2));
    }

    void CreateSphere(int numLatitude, int numLongitude, float radius)
    {
        spherePoints.Clear();

        for (int lat = 0; lat <= numLatitude; lat++)
        {
            float theta = Mathf.PI * lat / numLatitude;
            for (int lon = 0; lon <= numLongitude; lon++)
            {
                float phi = 2 * Mathf.PI * lon / numLongitude;

                Vector3 pos = new Vector3(
                    radius * Mathf.Sin(theta) * Mathf.Cos(phi),
                    radius * Mathf.Cos(theta),
                    radius * Mathf.Sin(theta) * Mathf.Sin(phi)
                );

                GameObject obj = Instantiate(massPointPrefab, pos + transform.position + new Vector3(5, 0, 0), Quaternion.identity);
                MassPoint p = new MassPoint(obj.transform);
                p.velocity = Vector3.zero;
                spherePoints.Add(p);
            }
        }

        for (int lat = 0; lat <= numLatitude; lat++)
        {
            for (int lon = 0; lon <= numLongitude; lon++)
            {
                int current = lat * (numLongitude + 1) + lon;

                int lonNeighbor = lat * (numLongitude + 1) + ((lon + 1) % (numLongitude + 1));
                if (lonNeighbor != current)
                    CreateSpringSphere(current, lonNeighbor);

                if (lat < numLatitude)
                {
                    int latNeighbor = (lat + 1) * (numLongitude + 1) + lon;
                    CreateSpringSphere(current, latNeighbor);
                }
            }
        }

        // Set center point if not manually assigned
        if (sphereCenter == null)
        {
            GameObject centerObj = new GameObject("SphereCenter");
            sphereCenter = centerObj.transform;
        }
    }

    void HandleCollisionCubeSphere()
    {
        if (sphereCenter == null)
        {
            UpdateSphereCenter(); // fallback
            if (sphereCenter == null) return;
        }

        Vector3 center = sphereCenter.position;

        foreach (var point in points)
        {
            Vector3 dir = point.transform.position - center;
            float dist = dir.magnitude;
            if (dist < sphereRadius)
            {
                Vector3 normal = dir.normalized;
                point.transform.position = center + normal * sphereRadius;
                point.velocity = Vector3.Reflect(point.velocity, normal) * 0.5f;
            }
        }

        float minDistance = 0.1f;
        foreach (var sp in spherePoints)
        {
            foreach (var cp in points)
            {
                Vector3 delta = sp.transform.position - cp.transform.position;
                float dist = delta.magnitude;

                if (dist < minDistance && dist > 0)
                {
                    Vector3 correction = delta.normalized * (minDistance - dist) * 0.5f;
                    if (!sp.isFixed)
                        sp.transform.position += correction;
                    if (!cp.isFixed)
                        cp.transform.position -= correction;

                    Vector3 normal = delta.normalized;
                    sp.velocity = Vector3.Reflect(sp.velocity, normal) * 0.5f;
                    cp.velocity = Vector3.Reflect(cp.velocity, -normal) * 0.5f;
                }
            }
        }

        UpdateSphereCenter();
    }

    void UpdateSphereCenter()
    {
        if (spherePoints.Count == 0) return;
        Vector3 avg = Vector3.zero;
        foreach (var p in spherePoints)
            avg += p.transform.position;
        sphereCenter.position = avg / spherePoints.Count;
    }

    class MassPoint
    {
        public Transform transform;
        public Vector3 velocity;
        public bool isFixed = false;

        public MassPoint(Transform t) => transform = t;
    }

    class Spring
    {
        MassPoint p1, p2;
        float restLength;

        public Spring(MassPoint a, MassPoint b)
        {
            p1 = a;
            p2 = b;
            restLength = Vector3.Distance(a.transform.position, b.transform.position);
        }

        public void Apply(float k, float damping)
        {
            Vector3 dir = p2.transform.position - p1.transform.position;
            float dist = dir.magnitude;
            Vector3 force = k * (dist - restLength) * dir.normalized;

            if (!p1.isFixed)
            {
                p1.velocity += force * Time.fixedDeltaTime;
                p1.velocity *= (1 - damping * Time.fixedDeltaTime);
            }

            if (!p2.isFixed)
            {
                p2.velocity -= force * Time.fixedDeltaTime;
                p2.velocity *= (1 - damping * Time.fixedDeltaTime);
            }

            Debug.DrawLine(p1.transform.position, p2.transform.position, Color.white);
        }
    }
}
