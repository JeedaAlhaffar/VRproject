using UnityEngine;

public class AABBCollisionTester : MonoBehaviour
{
    public GameObject otherObject;
    public Vector3 boxSizeA = Vector3.one;
    public Vector3 boxSizeB = Vector3.one;

void Update()
    {
        if (otherObject == null) return;

        Vector3 centerA = transform.position;
        Vector3 centerB = otherObject.transform.position;

        Vector3 sizeA = boxSizeA;
        Vector3 sizeB = boxSizeB;

        if (GetComponent<Renderer>() != null)
            sizeA = GetComponent<Renderer>().bounds.size;

        if (otherObject.GetComponent<Renderer>() != null)
            sizeB = otherObject.GetComponent<Renderer>().bounds.size;

        if (AABBIntersect(centerA, sizeA, centerB, sizeB))
        {
            Debug.Log($"{gameObject.name} colided with {otherObject.name} ( coluion detecte with AABB )");
        }
    }

    bool AABBIntersect(Vector3 centerA, Vector3 sizeA, Vector3 centerB, Vector3 sizeB)
    {
        Vector3 halfA = sizeA / 2f;
        Vector3 halfB = sizeB / 2f;

        return Mathf.Abs(centerA.x - centerB.x) <= (halfA.x + halfB.x) &&
               Mathf.Abs(centerA.y - centerB.y) <= (halfA.y + halfB.y) &&
               Mathf.Abs(centerA.z - centerB.z) <= (halfA.z + halfB.z);
    }
}