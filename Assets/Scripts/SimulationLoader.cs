using UnityEngine;

public class SimulationLoader : MonoBehaviour
{
    public GameObject[] availablePrefabs;

    void Start()
    {
        string modelName1 = UIManager.selectedModelName1;
        string modelName2 = UIManager.selectedModelName2;

        Debug.Log("Loading models: " + modelName1 + " and " + modelName2);

        LoadAndActivateMassSpring(modelName1, new Vector3(-5, 5, 0)); // مكان الموديل الأول
        LoadAndActivateMassSpring(modelName2, new Vector3(2, 5, 0));  // مكان الموديل التاني
    }

    void LoadAndActivateMassSpring(string modelName, Vector3 position)
    {
        foreach (var prefab in availablePrefabs)
        {
            if (prefab != null && prefab.name == modelName)
            {
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                Debug.Log("Instantiated prefab: " + modelName);

                // نشغّل الماس سبرينغ
                var massSpring = instance.GetComponent<MassSpringSystem>();
                if (massSpring != null)
                {
                    massSpring.enabled = true; // لو كان معطّل نشغّله
                    Debug.Log("Activated MassSpringSystem on: " + modelName);
                }
                else
                {
                    Debug.LogWarning("Prefab does not have MassSpringSystem: " + modelName);
                }
                return;
            }
        }

        Debug.LogWarning("Could not find prefab with name: " + modelName);
    }
}
