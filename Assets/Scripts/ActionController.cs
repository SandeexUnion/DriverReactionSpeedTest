using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ActionController : MonoBehaviour
{
    [Header("Fog Settings")]
    [SerializeField] private float fogDensityStep = 0.01f;
    [SerializeField] private float minFogDensity = 0f;
    [SerializeField] private float maxFogDensity = 0.5f;

    
    void Start()
    {
        
        if (!RenderSettings.fog)
        {
            RenderSettings.fog = true;
        }
    }

    
    void Update()
    {

    }

    public void ChangeSceneByName(string sceneName = "MainMenu")
    {
        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            Debug.Log($"Changing to scene: {sceneName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load scene {sceneName}: {e.Message}");
        }
    }

    void ChangeFogDensity(float changeAmount)
    {
        float newDensity = RenderSettings.fogDensity + changeAmount;
        newDensity = Mathf.Clamp(newDensity, minFogDensity, maxFogDensity);

        RenderSettings.fogDensity = newDensity;
        Debug.Log($"Fog density changed to: {newDensity:F4}");
    }

    public void OnDecreaseFog(InputValue inputValue)
    {
        if (inputValue.isPressed)
        {
            ChangeFogDensity(-fogDensityStep);
        }
    }

    public void OnIncreaseFog(InputValue inputValue)
    {
        if (inputValue.isPressed)
        {
            ChangeFogDensity(fogDensityStep);
        }
    }

    public void OnMonitor(InputValue inputValue)
    {
        Debug.Log("Дисплеев подключено: " + Display.displays.Length);
        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
        }
    }

    public void OnEscape(InputValue inputValue)
    {
        if (inputValue.isPressed)
        {
            ChangeSceneByName("MainMenu");
        }
    }

    public void OnRestart(InputValue inputValue)
    {
        if (inputValue.isPressed)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}