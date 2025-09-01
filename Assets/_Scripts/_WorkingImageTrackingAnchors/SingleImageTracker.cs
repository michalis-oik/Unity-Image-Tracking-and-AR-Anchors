using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class SingleImageTracker : MonoBehaviour
{
    [Header("AR System References")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private PlaneVisualizerController planeVisualizerController;
    [SerializeField] private ARSession arSession;

    [Header("Image Target Setup")]
    [SerializeField] private string imageUrl;
    [SerializeField] private float physicalImageSize = 0.1f;

    [Header("Spawning Setup")]
    [SerializeField] private GameObject upPrefab;
    [SerializeField] private GameObject rightPrefab;
    [SerializeField] private GameObject forwardPrefab;
    [SerializeField] private float spawnDistance = 0.5f;

    [Header("UI Elements")]
    [SerializeField] private Button trackButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;

    // State Variables
    private bool hasSpawned = false;
    private bool isDownloading = false;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private MutableRuntimeReferenceImageLibrary runtimeLibrary;
    private ARAnchor spawnedAnchor;
    private Texture2D downloadedTexture;
    private bool libraryInitialized = false;

    void Start()
    {
        // Setup UI
        if (trackButton != null)
        {
            trackButton.onClick.AddListener(StartImageTracking);
        }

        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(false);
            resetButton.onClick.AddListener(ResetExperience);
        }

        UpdateStatus("Press 'Track Image' to begin");
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged); // += OnTrackablesChanged;
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged); // -= OnTrackablesChanged;
        }
    }

    #region Image Tracking
    public void StartImageTracking()
    {
        if (isDownloading) return;

        if (downloadedTexture == null)
        {
            // First time - need to download
            StartCoroutine(DownloadAndSetupImage());
        }
        else
        {
            // Already downloaded, just setup tracking
            SetupImageTracking();
        }
    }

    private IEnumerator DownloadAndSetupImage()
    {
        isDownloading = true;
        if (trackButton != null) trackButton.interactable = false;
        
        UpdateStatus("Downloading image...");
        
        // Download the image
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateStatus($"Error: Failed to download image.\n{request.error}");
            if (trackButton != null) trackButton.interactable = true;
            isDownloading = false;
            yield break;
        }
        
        downloadedTexture = DownloadHandlerTexture.GetContent(request);
        downloadedTexture.name = "DynamicTarget";
        
        UpdateStatus("Image downloaded. Setting up tracking...");
        
        // Setup the image tracking
        SetupImageTracking();
        isDownloading = false;
    }

    private void SetupImageTracking()
    {
        if (trackedImageManager == null)
        {
            UpdateStatus("Error: No AR Tracked Image Manager assigned");
            return;
        }

        // Ensure the tracked image manager is disabled while we modify the library
        trackedImageManager.enabled = false;

        // Create a new runtime library if needed
        if (runtimeLibrary == null)
        {
            runtimeLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
            if (runtimeLibrary == null)
            {
                UpdateStatus("Error: Mutable runtime library not supported.");
                if (trackButton != null) trackButton.interactable = true;
                return;
            }
        }

        // Check if the image is already in the library
        bool imageExists = false;
        if (runtimeLibrary.count > 0)
        {
            // For simplicity, we'll assume the first image is our target
            imageExists = true;
        }

        if (!imageExists && downloadedTexture != null)
        {
            // Add the image to the library
            var jobState = runtimeLibrary.ScheduleAddImageWithValidationJob(
                downloadedTexture, downloadedTexture.name, physicalImageSize);

            // Wait for the job to complete
            // Note: In a real implementation, you should properly handle this async operation
        }

        // Set the reference library
        trackedImageManager.referenceLibrary = runtimeLibrary;

        // Enable the manager
        trackedImageManager.enabled = true;
        libraryInitialized = true;

        UpdateStatus("Ready! Please scan the image.");
        if (trackButton != null) trackButton.interactable = true;
        
        planeVisualizerController.ShowPlanes();
    }
    #endregion

    #region Image Tracking Event Handler

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (!libraryInitialized) return;

        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            ProcessTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            ProcessTrackedImage(trackedImage);
        }
    }

    #endregion

    #region ProcessTrackedImage
    private async void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (hasSpawned || downloadedTexture == null ||
            trackedImage.referenceImage.name != downloadedTexture.name)
            return;

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // Create a new anchor at the image's position
            // if (spawnedAnchor != null)
            // {
            //     Destroy(spawnedAnchor.gameObject);
            //     spawnedAnchor = null;
            // }

            // // Create a new GameObject for the anchor at the tracked image's position
            // GameObject anchorObject = new GameObject("ImageAnchor");

            // // Set the position and rotation to match the tracked image
            // anchorObject.transform.position = trackedImage.transform.position;
            // anchorObject.transform.rotation = trackedImage.transform.rotation;

            // spawnedAnchor = anchorObject.AddComponent<ARAnchor>();

            if (spawnedAnchor != null)
            {
                Destroy(spawnedAnchor.gameObject);
            }

            // Define the pose from the tracked image
            Pose anchorPose = new Pose(trackedImage.transform.position, trackedImage.transform.rotation);

            // Ask the manager to create the anchor for us. It handles creating the GameObject.
            Result<ARAnchor> result = await anchorManager.TryAddAnchorAsync(anchorPose);

            // The method returns a Result object. Check its status first.
            if (result.status.IsSuccess())
            {
                // If successful, get the anchor from the result's 'value' property
                spawnedAnchor = result.value;
            }
            else
            {
                // If it failed, set the anchor to null
                spawnedAnchor = null;
            }

            if (spawnedAnchor != null)
            {
                // Spawn objects relative to the anchor
                SpawnObjects(spawnedAnchor.transform);
                hasSpawned = true;

                UpdateStatus("Objects placed! Press Reset to scan again.");
                if (resetButton != null) resetButton.gameObject.SetActive(true);
                if (trackButton != null) trackButton.interactable = false;
                if (trackedImageManager != null) trackedImageManager.enabled = false; // close the tracking manager to stop scanning
            }
            else
            {
                UpdateStatus("Failed to create an anchor. Please try again.");
                //Destroy(anchorObject);
            }
        }
    }
    #endregion

    #region SpawnObjects
    private void SpawnObjects(Transform anchor)
    {
        // Clear any previously spawned objects
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        // Calculate positions relative to the anchor
        // Use local space offsets instead of world space to ensure proper placement
        Vector3 upPos = anchor.position + anchor.up * spawnDistance;
        Vector3 rightPos = anchor.position + anchor.right * spawnDistance;
        Vector3 forwardPos = anchor.position + anchor.forward * spawnDistance;

        // Instantiate objects
        if (upPrefab != null)
        {
            GameObject upObj = Instantiate(upPrefab, upPos, anchor.rotation);
            upObj.transform.SetParent(anchor);
            spawnedObjects.Add(upObj);
        }

        if (rightPrefab != null)
        {
            GameObject rightObj = Instantiate(rightPrefab, rightPos, anchor.rotation);
            rightObj.transform.SetParent(anchor);
            spawnedObjects.Add(rightObj);
        }

        if (forwardPrefab != null)
        {
            GameObject forwardObj = Instantiate(forwardPrefab, forwardPos, anchor.rotation);
            forwardObj.transform.SetParent(anchor);
            spawnedObjects.Add(forwardObj);
        }

        Debug.Log($"Spawned objects at position: {anchor.position}");

        planeVisualizerController.HidePlanes();
    }
    #endregion

    #region Reset
    public void ResetExperience()
    {
        UpdateStatus("Resetting experience...");

        // Destroy the anchor and all spawned objects
        if (spawnedAnchor != null)
        {
            Destroy(spawnedAnchor.gameObject);
            spawnedAnchor = null;
        }

        // Clear the list of spawned objects
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        hasSpawned = false;
        libraryInitialized = false;

        // Disable the tracked image manager
        if (trackedImageManager != null)
        {
            trackedImageManager.enabled = false;
        }

        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(false);
        }

        if (trackButton != null)
        {
            trackButton.interactable = true;
        }

        UpdateStatus("Press 'Track Image' to begin");
    }
    #endregion

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[Status]: {message}");
    }

    void OnDestroy()
    {
        if (trackButton != null)
        {
            trackButton.onClick.RemoveAllListeners();
        }
        
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
        }
    }
}