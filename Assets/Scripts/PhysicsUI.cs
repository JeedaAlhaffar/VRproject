using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PhysicsUI : MonoBehaviour
{
    public TMP_Dropdown materialDropdown;

    public Slider weightSlider;
    public TMP_Text weightValueText;

    public Slider densitySlider;
    public TMP_Text densityValueText;

    public Slider frictionSlider;
    public TMP_Text frictionValueText;

    public Slider gravitySlider;
    public TMP_Text gravityValueText;

    private MassSpringSystem targetSystem;

    void Start()
    {
        // إعداد قائمة المواد في الدروبداون
        materialDropdown.ClearOptions();
        materialDropdown.AddOptions(new System.Collections.Generic.List<string> { "Solid", "Rubber", "Slimy" });

        // ربط أحداث السلايدرز والدروب داون
        materialDropdown.onValueChanged.AddListener(OnMaterialChanged);
        weightSlider.onValueChanged.AddListener(OnWeightChanged);
        densitySlider.onValueChanged.AddListener(OnDensityChanged);
        frictionSlider.onValueChanged.AddListener(OnFrictionChanged);
        gravitySlider.onValueChanged.AddListener(OnGravityChanged);
    }

    public void SetTarget(MassSpringSystem system)
    {
        targetSystem = system;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (targetSystem == null) return;

        materialDropdown.value = (int)targetSystem.material;

        weightSlider.value = targetSystem.GetMass();
        weightValueText.text = targetSystem.GetMass().ToString("F2");

        densitySlider.value = targetSystem.GetDensity();
        densityValueText.text = targetSystem.GetDensity().ToString("F2");

        frictionSlider.value = targetSystem.GetFriction();
        frictionValueText.text = targetSystem.GetFriction().ToString("F2");

        gravitySlider.value = targetSystem.gravity.y;
        gravityValueText.text = targetSystem.gravity.y.ToString("F2");
    }

   public void OnMaterialChanged(int index)
    {
        if (targetSystem == null) return;
        targetSystem.material = (MassSpringSystem.MaterialType)index;
        Debug.Log("Material changed to: " + targetSystem.material);
        UpdateUI();
    }

   public void OnWeightChanged(float val)
    {
        if (targetSystem == null) return;
        targetSystem.SetMass(val);
        weightValueText.text = val.ToString("F2");
        Debug.Log("Weight changed to: " + val); 
        
    }

  public  void OnDensityChanged(float val)
    {
        if (targetSystem == null) return;
        targetSystem.SetDensity(val);
        densityValueText.text = val.ToString("F2");
        Debug.Log("Density changed to: " + val);
    }

   public void OnFrictionChanged(float val)
    {
        if (targetSystem == null) return;
        targetSystem.SetFriction(val);
        frictionValueText.text = val.ToString("F2");
        Debug.Log("Friction changed to: " + val); 
    }

  public void OnGravityChanged(float val)
    {
        if (targetSystem == null) return;
        Vector3 g = targetSystem.gravity;
        g.y = val;
        targetSystem.gravity = g;
        gravityValueText.text = val.ToString("F2");
        Debug.Log("Gravity Y changed to: " + val); 
    }
}
