using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro; // Make sure to add this for TextMeshPro

/// <summary>
/// Downloads a single image at runtime, sets up the AR Tracked Image Manager,
/// spawns a group of prefabs when the image is detected, and then disables tracking.
/// Provides a reset button to clear spawned objects and start scanning again.
/// Displays status updates to the user.
/// </summary>
public class ARImageTracker_RuntimeSpawner : MonoBehaviour
{
    [Header("AR Components")]
    [Tooltip("The AR Tracked Image Manager in your scene.")]
    [SerializeField] private ARTrackedImageManager m_TrackedImageManager;

    [Header("Download & Image Settings")]
    [Tooltip("The public URL of the image you want to track.")]
    public string imageUrl;
    [Tooltip("The physical size of the image in meters.")]
    public float physicalImageSize = 0.1f;

    [Header("Spawning")]
    [Tooltip("The prefab to spawn ABOVE the detected image.")]
    public GameObject upPrefab;
    [Tooltip("The prefab to spawn to the RIGHT of the detected image.")]
    public GameObject rightPrefab;
    [Tooltip("The prefab to spawn in FRONT of the detected image.")]
    public GameObject forwardPrefab;
    [Tooltip("How far from the center of the image to spawn the prefabs.")]
    public float spawnDistance = 0.1f;
    
    [Header("UI")]
    [Tooltip("The button that will reset the experience.")]
    public Button resetButton;
    [Tooltip("A TextMeshPro UI element to display status messages.")]
    public TextMeshProUGUI statusText;

    // State tracking variables
    private bool hasSpawned = false;
    private List<GameObject> m_SpawnedObjects = new List<GameObject>();
    private MutableRuntimeReferenceImageLibrary m_RuntimeLibrary;

    void Start()
    {
        // --- 1. Initial Setup ---
        if (resetButton == null) Debug.LogError("Reset Button has not been assigned!");
        if (m_TrackedImageManager == null) Debug.LogError("AR Tracked Image Manager has not been assigned!");
        
        resetButton.onClick.AddListener(ResetTracking);
        resetButton.interactable = false; 

        StartCoroutine(DownloadAndSetupTracker());
    }

    void OnEnable()
    {
        if (m_TrackedImageManager != null)
        {
            // Note: The event is called trackedImagesChanged in modern ARF versions
            m_TrackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged); // += OnTrackedImagesChanged;
        }
    }

    void OnDisable()
    {
        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged); // -= OnTrackedImagesChanged;
        }
    }

    /// <summary>
    /// A helper function to safely update the status text UI.
    /// </summary>
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log(message); // Also log to console for debugging
    }

    IEnumerator DownloadAndSetupTracker()
    {
        UpdateStatusText("Downloading image...");
        if (string.IsNullOrEmpty(imageUrl))
        {
            UpdateStatusText("Error: Image URL is empty.");
            yield break;
        }

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateStatusText("Error: Failed to download image.\nCheck URL and connection.");
            Debug.LogError("Failed to download image: " + request.error);
            yield break;
        }

        Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);
        downloadedTexture.name = "RuntimeImage";
        UpdateStatusText("Download complete. Creating AR library...");

        m_TrackedImageManager.enabled = false;
        m_RuntimeLibrary = m_TrackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
        if (m_RuntimeLibrary == null)
        {
            UpdateStatusText("Error: Platform does not support runtime image libraries.");
            yield break;
        }

        var jobState = m_RuntimeLibrary.ScheduleAddImageWithValidationJob(downloadedTexture, "RuntimeImage", physicalImageSize);
        yield return new WaitUntil(() => jobState.jobHandle.IsCompleted);

        if (jobState.status != AddReferenceImageJobStatus.Success)
        {
            UpdateStatusText("Error: Failed to create AR library.");
            Debug.LogError($"Failed to add image to library. Status: {jobState.status}");
            yield break;
        }

        m_TrackedImageManager.referenceLibrary = m_RuntimeLibrary;
        m_TrackedImageManager.enabled = true;
        UpdateStatusText("Ready. Scan the image to place objects.");
    }
    
    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (hasSpawned)
        {
            return;
        }

        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            // *** THE BUG FIX IS HERE ***
            // Only spawn if the image is being actively tracked.
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                if (!hasSpawned)
                {
                    SpawnObjectGroup(trackedImage);
                    return; // Exit after spawning
                }
            }
        }

        // You can optionally add logic for the 'updated' event too,
        // in case an image is detected but its state changes to Tracking later.
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                if (!hasSpawned)
                {
                    SpawnObjectGroup(trackedImage);
                    return; // Exit after spawning
                }
            }
        }
    }

    private void SpawnObjectGroup(ARTrackedImage trackedImage)
    {
        if (upPrefab == null || rightPrefab == null || forwardPrefab == null)
        {
            UpdateStatusText("Error: Spawning prefabs are not set.");
            return;
        }

        UpdateStatusText($"Image '{trackedImage.referenceImage.name}' detected!");
        
        Transform t = trackedImage.transform;

        m_SpawnedObjects.Add(Instantiate(upPrefab, t.position + (t.up * spawnDistance), t.rotation));
        m_SpawnedObjects.Add(Instantiate(rightPrefab, t.position + (t.right * spawnDistance), t.rotation));
        m_SpawnedObjects.Add(Instantiate(forwardPrefab, t.position + (t.forward * spawnDistance), t.rotation));
        
        hasSpawned = true;
        m_TrackedImageManager.enabled = false;
        
        UpdateStatusText("Object placed! Press Reset to scan again.");
        resetButton.interactable = true;
    }

    public void ResetTracking()
    {
        UpdateStatusText("Resetting...");

        foreach (GameObject obj in m_SpawnedObjects)
        {
            Destroy(obj);
        }
        m_SpawnedObjects.Clear();

        hasSpawned = false;

        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.enabled = true;
        }
        
        UpdateStatusText("Ready. Scan the image to place objects.");
        resetButton.interactable = false;
    }

    void OnDestroy()
    {
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
        }
    }
}