using UnityEngine;
using SimpleFileBrowser;
using Dummiesman;
using System.IO;

public class ModelLoader : MonoBehaviour
{
    private SceneDataManager dataManager;

    void Start()
    {
        dataManager = SceneDataManager.Instance;
        Debug.Log("🔄 ModelLoader initialized");
    }

    // عند الضغط على زر تحميل الموديل الخارجي
    public void LoadModelFromFile()
    {
        Debug.Log("📂 Opening file browser...");
        FileBrowser.SetFilters(true, new FileBrowser.Filter("3D Models", ".obj"));
        FileBrowser.ShowLoadDialog(OnFileSelected, OnCancel, FileBrowser.PickMode.Files, false, "Select Model", "Load");
    }

    void OnFileSelected(string[] paths)
    {
        if (dataManager == null)
        {
            dataManager = SceneDataManager.Instance;
            Debug.Log("🔄 SceneDataManager instance obtained");
        }

        string path = paths[0];
        string fileName = Path.GetFileNameWithoutExtension(path);
        Debug.Log($"📦 Selected model path: {path}");
        Debug.Log($"📦 File name: {fileName}");

        try
        {
            var loader = new OBJLoader();
            GameObject loadedModel = loader.Load(path);

            if (loadedModel == null)
            {
                Debug.LogError("❌ Failed to import OBJ model.");
                return;
            }

            Debug.Log("✅ OBJ model loaded successfully");

            // إعداد الموديل
            loadedModel.transform.localPosition = Vector3.zero;
            loadedModel.transform.localRotation = Quaternion.identity;
            loadedModel.transform.localScale = Vector3.one ;
            loadedModel.name = fileName;

            // البحث عن أول GameObject يحتوي على MeshFilter في الموديل
            GameObject meshObject = FindMeshFilterInHierarchy(loadedModel);

            if (meshObject != null)
            {
                Debug.Log($"🔍 Found MeshFilter in: {meshObject.name}");

                // إضافة MassSpringSystem مباشرة للـ GameObject الذي يحتوي على MeshFilter
                var spring = meshObject.AddComponent<MassSpringSystem>();

                if (spring != null)
                {
                    spring.material = MassSpringSystem.MaterialType.Rubber;
                    spring.enabled = false; // عطل حتى يتم تفعيله بالمشهد الثاني

                    Debug.Log($"✅ MassSpringSystem added to: {meshObject.name}");

                    // تأكد من أن الـ MassSpringSystem يتم إعداده بشكل صحيح
                    StartCoroutine(InitializeMassSpringDelayed(spring));
                }
            }
            else
            {
                Debug.LogError("❌ No MeshFilter found in the loaded model!");
                return;
            }

            loadedModel.SetActive(false); // إخفاء الموديل حتى يتم نقله للمشهد الثاني

            // تأكد من أن الموديل لن يُحذف عند تغيير المشهد
            DontDestroyOnLoad(loadedModel);

            Debug.Log($"🔧 Model setup complete: {fileName}");

            // تخزين الموديل الخارجي باستخدام المدير
            dataManager.SetExternalModel(loadedModel);

            Debug.Log("✅ Model loaded and MassSpringSystem attached!");
            Debug.Log($"📁 External model stored for next scene: {fileName}");
            Debug.Log($"📊 Current data status: {dataManager.GetDataSummary()}");

            // تحديث حالة زر البدء في UIManager
            var uiManager = FindObjectOfType<UIManager>();
            if (uiManager != null)
            {
                uiManager.SendMessage("UpdateStartButtonState", SendMessageOptions.DontRequireReceiver);
                Debug.Log("🔄 UI updated");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager not found for updating button state");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading model: {e.Message}");
        }
    }

    // دالة للبحث عن أول GameObject يحتوي على MeshFilter
    GameObject FindMeshFilterInHierarchy(GameObject parent)
    {
        // تحقق من الـ parent أولاً
        var meshFilter = parent.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            return parent;
        }

        // البحث في الأطفال
        foreach (Transform child in parent.transform)
        {
            GameObject result = FindMeshFilterInHierarchy(child.gameObject);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    // دالة مساعدة لتأخير تهيئة MassSpringSystem
    System.Collections.IEnumerator InitializeMassSpringDelayed(MassSpringSystem spring)
    {
        yield return new WaitForEndOfFrame();

        try
        {
            // تأكد من أن جميع المكونات جاهزة
            if (spring != null && spring.gameObject != null)
            {
                Debug.Log("🔧 MassSpringSystem initialized properly");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing MassSpringSystem: {e.Message}");
        }
    }

    void OnCancel()
    {
        Debug.Log("❌ User canceled model selection");
    }
}