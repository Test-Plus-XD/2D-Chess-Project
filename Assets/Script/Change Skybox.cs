using UnityEngine;

public class ChangeSkybox : MonoBehaviour
{
    public Material newSkybox;

    void Start()
    {
        RenderSettings.skybox = newSkybox; // Change global skybox
        Camera.main.clearFlags = CameraClearFlags.Skybox; // Ensure camera uses skybox
    }
}