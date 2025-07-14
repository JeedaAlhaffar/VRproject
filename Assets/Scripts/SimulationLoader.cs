using UnityEngine;

public class SimulationLoader : MonoBehaviour
{
    public GameObject[] availablePrefabs;
    private SceneDataManager dataManager;

    void Start()
    {
        dataManager = SceneDataManager.Instance;

        int modelCount = 0;

        // موديل 1
        if (!string.IsNullOrEmpty(dataManager.selectedModelName1))
        {
            bool loaded1 = LoadAndActivateMassSpring(dataManager.selectedModelName1, new Vector3(-5, 5, 0));
            if (loaded1) modelCount++;
        }

        // موديل 2
        if (!string.IsNullOrEmpty(dataManager.selectedModelName2))
        {
            bool loaded2 = LoadAndActivateMassSpring(dataManager.selectedModelName2, new Vector3(2, 5, 0));
            if (loaded2) modelCount++;
        }

        // موديل خارجي
        if (dataManager.loadedExternalModel != null)
        {
            Vector3 externalPos;
            if (modelCount == 0)
                externalPos = new Vector3(0, 5, 0);
            else if (modelCount == 1)
                externalPos = new Vector3(0, 5, -5);
            else
                externalPos = new Vector3(0, 5, -6);

            // إنشاء نسخة من الموديل الخارجي
            GameObject externalInstance = Instantiate(dataManager.loadedExternalModel, externalPos, Quaternion.identity);

            // تفعيل الموديل وجميع أطفاله
            //SetActiveRecursively(externalInstance, true);
            externalInstance.SetActive(true);
            Debug.Log($"🎯 External model positioned at: {externalPos}");

            // البحث عن MassSpringSystem في الموديل أو أطفاله
            var spring = FindMassSpringInHierarchy(externalInstance);
            if (spring != null)
            {
                // تأكد من أن الـ GameObject الذي يحتوي على MassSpringSystem يحتوي أيضاً على MeshFilter
                var meshFilter = spring.gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    spring.enabled = true;
                    Debug.Log($"✅ External model MassSpring activated on: {spring.gameObject.name}");
                    Debug.Log($"✅ MeshFilter confirmed on: {spring.gameObject.name}");
            EnsureCollisionDetection(externalInstance);
                }
                else
                {
                    Debug.LogWarning($"⚠️ MassSpringSystem found but no MeshFilter on: {spring.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ External model has no MassSpring component in hierarchy!");
            }

            // التأكد من أن جميع MeshRenderers مفعلة
            EnableAllMeshRenderers(externalInstance);

            modelCount++;
        }

        if (modelCount == 0)
        {
            Debug.LogWarning("لم يتم اختيار أي موديل للتحميل.");
        }

        Debug.Log($"Total models loaded: {modelCount}");
        Debug.Log($"Data summary: {dataManager.GetDataSummary()}");
    }

    // دالة للبحث عن MassSpringSystem في التسلسل الهرمي
    MassSpringSystem FindMassSpringInHierarchy(GameObject parent)
    {
        // تحقق من الـ parent أولاً
        var spring = parent.GetComponent<MassSpringSystem>();
        if (spring != null)
        {
            return spring;
        }

        // البحث في الأطفال
        foreach (Transform child in parent.transform)
        {
            spring = FindMassSpringInHierarchy(child.gameObject);
            if (spring != null)
            {
                return spring;
            }
        }

        return null;
    }

    // دالة لتفعيل جميع MeshRenderers
    void EnableAllMeshRenderers(GameObject parent)
    {
        var renderer = parent.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            Debug.Log($"✅ Enabled MeshRenderer on: {parent.name}");
        }

        foreach (Transform child in parent.transform)
        {
            EnableAllMeshRenderers(child.gameObject);
        }
    }

    // دالة لتفعيل GameObject وجميع أطفاله
    void SetActiveRecursively(GameObject obj, bool active)
    {
        obj.SetActive(active);

        foreach (Transform child in obj.transform)
        {
            SetActiveRecursively(child.gameObject, active);
        }
    }

    // يرجع true إذا تم تحميل الموديل بنجاح، false إذا لم يوجد
    bool LoadAndActivateMassSpring(string modelName, Vector3 position)
    {
        foreach (var prefab in availablePrefabs)
        {
            if (prefab != null && prefab.name == modelName)
            {
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                instance.SetActive(true);
                Debug.Log("Instantiated prefab: " + modelName);

                var massSpring = instance.GetComponent<MassSpringSystem>();
                if (massSpring != null)
                {
                    massSpring.enabled = true;
                    Debug.Log("Activated MassSpringSystem on: " + modelName);
                }
                else
                {
                    Debug.LogWarning("Prefab does not have MassSpringSystem: " + modelName);
                }

                return true;
            }
        }

        Debug.LogWarning("Could not find prefab with name: " + modelName);
        return false;
    }
    void EnsureCollisionDetection(GameObject model)
    {
        var meshObject = FindMeshFilterInHierarchy(model);
        if (meshObject != null)
        {
            var collision = meshObject.GetComponent<CollisionDetectionFull>();
            if (collision == null)
            {
                collision = meshObject.AddComponent<CollisionDetectionFull>();
                Debug.Log($"🛠️ CollisionDetectionFull added to: {meshObject.name}");
            }
            collision.enabled = true;
        }
        else
        {
            Debug.LogWarning("⚠️ No MeshFilter found in model to attach CollisionDetectionFull.");
        }
    }

    // البحث عن أول MeshFilter في الموديل أو أطفاله
    GameObject FindMeshFilterInHierarchy(GameObject parent)
    {
        if (parent.GetComponent<MeshFilter>() != null)
            return parent;

        foreach (Transform child in parent.transform)
        {
            GameObject found = FindMeshFilterInHierarchy(child.gameObject);
            if (found != null)
                return found;
        }

        return null;
    }

}