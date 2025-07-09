using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public TMP_Text titleText1;         // "Select First Model:"
    public TMP_Text titleText2;         // "Select Second Model:"
    public TMP_Dropdown modelDropdown1; // أول Dropdown
    public TMP_Dropdown modelDropdown2; // ثاني Dropdown
    public Button startButton;          // زر بدء المحاكاة

    void Start()
    {
        // تهيئة العناوين
        titleText1.text = "Select Model 1:";
        titleText2.text = "Select Model 2:";
   
        // إضافة Listeners للتعامل مع التغييرات
        modelDropdown1.onValueChanged.AddListener(OnDropdown1Changed);
        modelDropdown2.onValueChanged.AddListener(OnDropdown2Changed);

        // إضافة Listener للزر
        startButton.onClick.AddListener(OnStartButtonClicked);
    }

    void OnDropdown1Changed(int index)
    {
        string selectedModel = modelDropdown1.options[index].text;
        Debug.Log("Selected first model: " + selectedModel);
        // هنا تقدر تحمّل الموديل أو تحفظ الاختيار
    }

    void OnDropdown2Changed(int index)
    {
        string selectedModel = modelDropdown2.options[index].text;
        Debug.Log("Selected second model: " + selectedModel);
        // هنا تقدر تحمّل الموديل أو تحفظ الاختيار
    }

    void OnStartButtonClicked()
    {
        Debug.Log("Simulation Started!");
        // هنا تبدأ تشغيل المحاكاة
        SceneManager.LoadScene("Assets/Scenes/Simulation.unity");
    }
}
