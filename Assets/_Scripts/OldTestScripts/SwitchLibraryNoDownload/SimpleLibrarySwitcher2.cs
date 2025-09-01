using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SimpleLibrarySwitcher2 : MonoBehaviour
{
    public ARTrackedImageManager trackedImageManager;
    public Button library1Button;
    public Button library2Button;
    public Texture2D[] library1Images;
    public Texture2D[] library2Images;
    public float physicalSize = 0.1f;
    
    void Start()
    {
        library1Button.onClick.AddListener(() => StartCoroutine(SwitchLibrary(library1Images, "Library 1")));
        library2Button.onClick.AddListener(() => StartCoroutine(SwitchLibrary(library2Images, "Library 2")));
    }
    
    IEnumerator SwitchLibrary(Texture2D[] images, string libraryName)
    {
        Debug.Log($"Switching to {libraryName}");
        
        // Disable the manager
        trackedImageManager.enabled = false;
        
        // Create a new mutable library
        var newLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
        
        if (newLibrary == null)
        {
            Debug.LogError("Mutable runtime library not supported");
            yield break;
        }
        
        // Add all images to the new library
        foreach (var image in images)
        {
            if (image == null) continue;
            
            // Use the extension method
            var jobState = newLibrary.ScheduleAddImageWithValidationJob(
                image,
                image.name,
                physicalSize
            );
            
            // Wait for the job to complete
            yield return new WaitUntil(() => jobState.jobHandle.IsCompleted);
            
            jobState.jobHandle.Complete();
            
            Debug.Log($"Added: {image.name}");
        }
        
        // Switch to the new library
        trackedImageManager.referenceLibrary = newLibrary;
        trackedImageManager.enabled = true;
        
        Debug.Log($"Switched to {libraryName} successfully");
    }
    
    void OnDestroy()
    {
        library1Button.onClick.RemoveAllListeners();
        library2Button.onClick.RemoveAllListeners();
    }
}