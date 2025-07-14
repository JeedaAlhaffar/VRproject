using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public TMP_Text titleText1;          // "Select Model 1:"
    public TMP_Text titleText2;          // "Select Model 2:"
    public TMP_Dropdown modelDropdown1;  // Dropdown for Model 1
    public TMP_Dropdown modelDropdown2;  // Dropdown for Model 2
    public Button startButton;           // Button to start simulation

    // استخدام المدير الجديد بدلاً من المتغيرات الثابتة
    private SceneDataManager dataManager;

    void Start()
    {
        // الحصول على مرجع للمدير
        dataManager = SceneDataManager.Instance;

        // إعداد النصوص
        titleText1.text = "Select Model 1:";
        titleText2.text = "Select Model 2:";

        // إعداد القوائم المنسدلة
        modelDropdown1.options.Insert(0, new TMP_Dropdown.OptionData("None"));
        modelDropdown2.options.Insert(0, new TMP_Dropdown.OptionData("None"));

        // استرداد القيم المحفوظة سابقاً إن وجدت
        RestorePreviousSelections();

        // إضافة المستمعين
        modelDropdown1.onValueChanged.AddListener(OnDropdown1Changed);
        modelDropdown2.onValueChanged.AddListener(OnDropdown2Changed);
        startButton.onClick.AddListener(OnStartButtonClicked);

        // تحديث حالة زر البدء
        UpdateStartButtonState();
    }

    void RestorePreviousSelections()
    {
        // استرداد الاختيار الأول
        if (!string.IsNullOrEmpty(dataManager.selectedModelName1))
        {
            for (int i = 0; i < modelDropdown1.options.Count; i++)
            {
                if (modelDropdown1.options[i].text == dataManager.selectedModelName1)
                {
                    modelDropdown1.value = i;
                    break;
                }
            }
        }
        else
        {
            modelDropdown1.value = 0;
        }

        // استرداد الاختيار الثاني
        if (!string.IsNullOrEmpty(dataManager.selectedModelName2))
        {
            for (int i = 0; i < modelDropdown2.options.Count; i++)
            {
                if (modelDropdown2.options[i].text == dataManager.selectedModelName2)
                {
                    modelDropdown2.value = i;
                    break;
                }
            }
        }
        else
        {
            modelDropdown2.value = 0;
        }
    }

    void OnDropdown1Changed(int index)
    {
        string choice = modelDropdown1.options[index].text;
        dataManager.selectedModelName1 = (choice == "None") ? null : choice;

        Debug.Log("Selected first model: " + (dataManager.selectedModelName1 ?? "None"));
        UpdateStartButtonState();
    }

    void OnDropdown2Changed(int index)
    {
        string choice = modelDropdown2.options[index].text;
        dataManager.selectedModelName2 = (choice == "None") ? null : choice;

        Debug.Log("Selected second model: " + (dataManager.selectedModelName2 ?? "None"));
        UpdateStartButtonState();
    }

    void UpdateStartButtonState()
    {
        // زر البدء مفعل إذا تم اختيار موديل واحد على الأقل أو تم تحميل موديل خارجي
        startButton.interactable =
            (!string.IsNullOrEmpty(dataManager.selectedModelName1) ||
             !string.IsNullOrEmpty(dataManager.selectedModelName2) ||
             dataManager.hasExternalModel);
    }

    void OnStartButtonClicked()
    {
        // تأكد من تعيين المتغيرات حتى لو لم يغير المستخدم الدروب داون
        if (string.IsNullOrEmpty(dataManager.selectedModelName1))
        {
            string choice = modelDropdown1.options[modelDropdown1.value].text;
            dataManager.selectedModelName1 = (choice == "None") ? null : choice;
        }

        if (string.IsNullOrEmpty(dataManager.selectedModelName2))
        {
            string choice = modelDropdown2.options[modelDropdown2.value].text;
            dataManager.selectedModelName2 = (choice == "None") ? null : choice;
        }

        Debug.Log("Simulation Started!");
        Debug.Log($"Data summary: {dataManager.GetDataSummary()}");

        // الانتقال إلى المشهد الثاني (المحاكاة)
        SceneManager.LoadScene("Simulation");
    }
}