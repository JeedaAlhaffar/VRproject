using UnityEngine;

public class SceneDataManager : MonoBehaviour
{
    private static SceneDataManager instance;
    public static SceneDataManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SceneDataManager");
                instance = go.AddComponent<SceneDataManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("Model Selection Data")]
    public string selectedModelName1;
    public string selectedModelName2;
    public bool hasExternalModel;
    public GameObject loadedExternalModel;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // دوال لتحديث البيانات
    public void SetSelectedModels(string model1, string model2, bool hasExternal)
    {
        selectedModelName1 = model1;
        selectedModelName2 = model2;
        hasExternalModel = hasExternal;

        Debug.Log($"Data updated: Model1={model1}, Model2={model2}, HasExternal={hasExternal}");
    }

    public void SetExternalModel(GameObject model)
    {
        // إذا كان هناك موديل خارجي سابق، احذفه
        if (loadedExternalModel != null)
        {
            Destroy(loadedExternalModel);
        }

        loadedExternalModel = model;
        hasExternalModel = (model != null);

        Debug.Log($"External model set: {(model != null ? model.name : "None")}");
    }

    // دالة لمسح البيانات عند الحاجة
    public void ClearData()
    {
        selectedModelName1 = null;
        selectedModelName2 = null;
        hasExternalModel = false;

        if (loadedExternalModel != null)
        {
            Destroy(loadedExternalModel);
            loadedExternalModel = null;
        }

        Debug.Log("Scene data cleared");
    }

    // دالة للحصول على ملخص البيانات
    public string GetDataSummary()
    {
        return $"Model1: {selectedModelName1 ?? "None"}, " +
               $"Model2: {selectedModelName2 ?? "None"}, " +
               $"External: {(hasExternalModel ? "Yes" : "No")}";
    }
}