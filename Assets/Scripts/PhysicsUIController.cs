//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;

//public class PhysicsUIController : MonoBehaviour
//{
//    public Rigidbody targetRigidbody;
//    public Collider targetCollider;
//    public PhysicsMaterial physicMaterial;

//    [Header("UI References")]
//    public TMP_InputField massInput;
//    public TMP_InputField densityInput;
//    public TMP_InputField frictionInput;
//    public TMP_InputField bouncinessInput;

//    void Start()
//    {
//        if (targetRigidbody != null)
//            UpdateUI();
//    }

//    public void UpdateUI()
//    {
//        massInput.text = targetRigidbody.mass.ToString("F2");
//        densityInput.text = "N/A"; // تحتاج حساب خاص إذا حابب
//        frictionInput.text = physicMaterial != null ? physicMaterial.dynamicFriction.ToString("F2") : "0";
//        bouncinessInput.text = physicMaterial != null ? physicMaterial.bounciness.ToString("F2") : "0";
//    }

//    public void OnMassChanged(string newVal)
//    {
//        if (float.TryParse(newVal, out float mass) && targetRigidbody != null)
//        {
//            targetRigidbody.mass = Mathf.Max(0.01f, mass);
//        }
//    }

//    public void OnFrictionChanged(string newVal)
//    {
//        if (float.TryParse(newVal, out float friction) && physicMaterial != null)
//        {
//            physicMaterial.dynamicFriction = friction;
//            physicMaterial.staticFriction = friction;
//        }
//    }

//    public void OnBouncinessChanged(string newVal)
//    {
//        if (float.TryParse(newVal, out float bounce) && physicMaterial != null)
//        {
//            physicMaterial.bounciness = bounce;
//        }
//    }

//    // إذا أضفت كثافة كثابت، فيك تحسبها بـ: density = mass / volume (اختياري)
//}
