using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public TMP_Text titleText1;          // "Select First Model:"
    public TMP_Text titleText2;          // "Select Second Model:"
    public TMP_Dropdown modelDropdown1;  // أول Dropdown
    public TMP_Dropdown modelDropdown2;  // ثاني Dropdown
    public Button startButton;           // زر بدء المحاكاة

    // Static variables to share data with next scene
    public static string selectedModelName1;
    public static string selectedModelName2;

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
        selectedModelName1 = modelDropdown1.options[index].text;
        Debug.Log("Selected first model: " + selectedModelName1);
    }

    void OnDropdown2Changed(int index)
    {
        selectedModelName2 = modelDropdown2.options[index].text;
        Debug.Log("Selected second model: " + selectedModelName2);
    }

    void OnStartButtonClicked()
    {
        // حفظ آخر اختيار إذا ما تغيرت الـ Dropdowns بعد التشغيل
        if (string.IsNullOrEmpty(selectedModelName1))
            selectedModelName1 = modelDropdown1.options[modelDropdown1.value].text;
        if (string.IsNullOrEmpty(selectedModelName2))
            selectedModelName2 = modelDropdown2.options[modelDropdown2.value].text;

        Debug.Log("Simulation Started!");
        Debug.Log($"Models chosen: {selectedModelName1}, {selectedModelName2}");

        // تحميل مشهد السيموليشن (يُفضل كتابة اسم المشهد فقط إذا ضايفه بـ Build Settings)
        SceneManager.LoadScene("Simulation");
    }
}
