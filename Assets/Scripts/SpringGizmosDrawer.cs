using UnityEngine;

[ExecuteAlways]
public class SpringGizmosDrawer : MonoBehaviour
{
    void OnDrawGizmos()
    {
        var builder = GetComponent<MassSpringMeshBuilder>();
        if (builder == null) return;

        var springs = builder.GetSprings();
        if (springs == null) return;

        foreach (var s in springs)
        {
            if (s == null || s.p1 == null || s.p2 == null) continue;

            Gizmos.color = Color.yellow; // ? ?????? ????? ?????? ??? ??? ????
            Vector3 a = s.p1.transform.position;
            Vector3 b = s.p2.transform.position;
            Gizmos.DrawLine(a, b);
        }
    }
}
