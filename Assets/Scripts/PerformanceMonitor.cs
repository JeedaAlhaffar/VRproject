using UnityEngine;
using UnityEngine.Profiling;

public class PerformanceMonitor : MonoBehaviour
{
    private float deltaTime = 0.0f;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {

        int w = Screen.width, h = Screen.height;

        GUIStyle style = new GUIStyle();

       // Rect rect = new Rect(10, 10, w, h * 2 / 100);
        Rect rect = new Rect(Screen.width - w/5, 10, 140, 30);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize =16;
        style.normal.textColor = Color.white;

        float fps = 1.0f / deltaTime;
        long totalMemory = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        long reservedMemory = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
        long monoMemory = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
        int activeObjects = FindObjectsOfType<GameObject>().Length;

        string text = $"FPS: {fps:0.} \n" +
                      $"Total Allocated: {totalMemory} MB\n" +
                      $"Reserved Memory: {reservedMemory} MB\n" +
                      $"Mono Heap: {monoMemory} MB\n" +
                      $"Active Objects: {activeObjects}";
   
        GUI.Label(rect, text, style);
    }
}
