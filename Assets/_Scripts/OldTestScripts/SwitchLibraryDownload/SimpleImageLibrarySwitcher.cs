using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking; 

public class SimpleLibrarySwitcher : MonoBehaviour
{
    [Header("AR Components")]
    public ARTrackedImageManager trackedImageManager;

    // --- ADD THIS REFERENCE ---
    [Header("Dependencies")]
    [Tooltip("(Optional) Assign the 'Place Once' spawner here to allow it to be reset when switching libraries.")]
    public ObjectSpawner_PlaceOnce placeOnceSpawner; 

    // ... The rest of your variables are the same ...
    [Header("UI")]
    public Button library1Button;
    public Button library2Button;
    [Header("Image URLs")]
    public string imageUrl1;
    public string imageUrl2;
    [Header("Image Settings")]
    public float physicalSize = 0.1f;
    private Texture2D[] library1Images;
    private Texture2D[] library2Images;

    // ... Your Start() and DownloadAndPrepareImages() methods are unchanged ...
    void Start()
    {
        library1Button.interactable = false;
        library2Button.interactable = false;
        library1Button.onClick.AddListener(() => StartCoroutine(SwitchLibrary(library1Images, "Library 1")));
        library2Button.onClick.AddListener(() => StartCoroutine(SwitchLibrary(library2Images, "Library 2")));
        StartCoroutine(DownloadAndPrepareImages());
    }

    IEnumerator DownloadAndPrepareImages()
    {
        // ... This code is correct and does not need to change ...
        Debug.Log("Starting image download process...");
        UnityWebRequest request1 = UnityWebRequestTexture.GetTexture(imageUrl1);
        yield return request1.SendWebRequest();
        if (request1.result == UnityWebRequest.Result.Success)
        {
            Texture2D downloadedTex1 = DownloadHandlerTexture.GetContent(request1);
            downloadedTex1.name = "DownloadedImage1";
            library1Images = new Texture2D[] { downloadedTex1 };
        }
        else { Debug.LogError("Failed to download Image 1: " + request1.error); yield break; }
        UnityWebRequest request2 = UnityWebRequestTexture.GetTexture(imageUrl2);
        yield return request2.SendWebRequest();
        if (request2.result == UnityWebRequest.Result.Success)
        {
            Texture2D downloadedTex2 = DownloadHandlerTexture.GetContent(request2);
            downloadedTex2.name = "DownloadedImage2";
            library2Images = new Texture2D[] { downloadedTex2 };
        }
        else { Debug.LogError("Failed to download Image 2: " + request2.error); yield break; }
        library1Button.interactable = true;
        library2Button.interactable = true;
    }


    IEnumerator SwitchLibrary(Texture2D[] images, string libraryName)
    {
        if (images == null || images.Length == 0)
        {
            Debug.LogError($"Cannot switch to {libraryName}, no images have been loaded for it.");
            yield break;
        }

        // --- ADD THIS LOGIC ---
        // Before we do anything, check if the "Place Once" spawner is active and reset it.
        // This ensures it's ready for the new library we're about to load.
        if (placeOnceSpawner != null && placeOnceSpawner.gameObject.activeInHierarchy && placeOnceSpawner.enabled)
        {
            placeOnceSpawner.ResetForNewScan();
        }
        // --- END OF ADDED LOGIC ---

        Debug.Log($"Switching to {libraryName}");

        // Disable the manager to change the library
        trackedImageManager.enabled = false;

        // ... The rest of the coroutine is the same and correct ...
        var newLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
        if (newLibrary == null) { Debug.LogError("Mutable runtime library not supported."); yield break; }
        foreach (var image in images)
        {
            if (image == null) continue;
            var jobState = newLibrary.ScheduleAddImageWithValidationJob(image, image.name, physicalSize);
            yield return new WaitUntil(() => jobState.jobHandle.IsCompleted);
            if (jobState.status != AddReferenceImageJobStatus.Success)
            {
                Debug.LogError($"Failed to add {image.name}. Status: {jobState.status}");
            }
        }
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