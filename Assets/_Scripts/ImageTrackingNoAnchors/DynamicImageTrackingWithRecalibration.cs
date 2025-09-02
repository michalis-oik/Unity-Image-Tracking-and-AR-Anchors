using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class DynamicImageTrackingPersistent : MonoBehaviour
{
    [Header("AR System References")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private PlaneVisualizerController planeVisualizerController;
    [SerializeField] private ARSession arSession;

    [Header("Image Target Setup")]
    [SerializeField] private string imageUrl;
    [SerializeField] private float physicalImageSize = 0.1f;

    [Header("Spawning Setup")]
    [SerializeField] private GameObject downPrefab;
    [SerializeField] private GameObject upPrefab;
    [SerializeField] private GameObject rightPrefab;
    [SerializeField] private GameObject forwardPrefab;
    [SerializeField] private float spawnDistance = 0.5f;

    [Header("UI Elements")]
    [SerializeField] private Button trackButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;

    // --- State Variables ---
    private bool hasSpawned = false;
    private bool isDownloading = false;
    private GameObject imageTrackingRoot;
    private MutableRuntimeReferenceImageLibrary runtimeLibrary;
    private Texture2D downloadedTexture;
    private bool libraryInitialized = false;

    void Start()
    {
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
            trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
        }
    }

    #region Image Tracking Setup
    public void StartImageTracking()
    {
        if (isDownloading) return;

        if (downloadedTexture == null)
        {
            StartCoroutine(DownloadAndSetupImage());
        }
        else
        {
            SetupImageTracking();
        }
    }

    private IEnumerator DownloadAndSetupImage()
    {
        isDownloading = true;
        if (trackButton != null) trackButton.interactable = false;

        UpdateStatus("Downloading image...");

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

        trackedImageManager.enabled = false;

        runtimeLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
        if (runtimeLibrary == null)
        {
            UpdateStatus("Error: Mutable runtime library not supported.");
            if (trackButton != null) trackButton.interactable = true;
            return;
        }

        if (downloadedTexture != null)
        {
            var jobHandle = runtimeLibrary.ScheduleAddImageWithValidationJob(
                downloadedTexture, downloadedTexture.name, physicalImageSize);
        }

        trackedImageManager.referenceLibrary = runtimeLibrary;
        trackedImageManager.enabled = true;
        libraryInitialized = true;

        UpdateStatus("Ready! Please scan the image.");
        if (trackButton != null) trackButton.interactable = true;

        planeVisualizerController.ShowPlanes();
    }
    #endregion

    #region Image Tracking Event Handler

    private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (!libraryInitialized || downloadedTexture == null) return;

        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            ProcessTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            ProcessTrackedImage(trackedImage);
        }
    }

    private void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage.name != downloadedTexture.name)
            return;

        // When the image is being tracked, update the content's position and rotation.
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            if (!hasSpawned)
            {
                // FIRST TIME: Create a root object and spawn prefabs as its children.
                imageTrackingRoot = new GameObject("ImageTrackingRoot");
                imageTrackingRoot.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
                SpawnObjects(imageTrackingRoot.transform);
                hasSpawned = true;

                // Update UI
                UpdateStatus("Objects placed. Rescan image to recalibrate.");
                if (resetButton != null) resetButton.gameObject.SetActive(true);
                if (trackButton != null) trackButton.interactable = false;
                planeVisualizerController.HidePlanes();
            }
            else
            {
                // RECALIBRATION: The root object already exists, just update its pose.
                if (imageTrackingRoot != null)
                {
                    imageTrackingRoot.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
                    UpdateStatus("Tracking active. Position recalibrated.");
                }
            }
        }
        // =========================================================================
        // CHANGE: We REMOVE the 'else' block that hides the object.
        // By doing nothing when tracking is 'Limited' or 'None', the object
        // will simply remain at its last known tracked position.
        // =========================================================================
        else if (hasSpawned) // Optional: Update status text when tracking is lost
        {
             UpdateStatus("Tracking lost. Rescan image to recalibrate.");
        }
    }
    #endregion

    #region Spawning and Resetting

    private void SpawnObjects(Transform parentTransform)
    {
        // Calculate positions relative to the parent transform
        Vector3 upPos = parentTransform.position + parentTransform.up * spawnDistance;
        Vector3 downPos = parentTransform.position - parentTransform.up * spawnDistance;
        Vector3 rightPos = parentTransform.position + parentTransform.right * spawnDistance;
        Vector3 forwardPos = parentTransform.position + parentTransform.forward * spawnDistance;

        // Instantiate objects and parent them
        if (upPrefab != null) Instantiate(upPrefab, upPos, parentTransform.rotation).transform.SetParent(parentTransform);
        if (downPrefab != null) Instantiate(downPrefab, downPos, parentTransform.rotation).transform.SetParent(parentTransform);
        if (rightPrefab != null) Instantiate(rightPrefab, rightPos, parentTransform.rotation).transform.SetParent(parentTransform);
        if (forwardPrefab != null) Instantiate(forwardPrefab, forwardPos, parentTransform.rotation).transform.SetParent(parentTransform);

        Debug.Log($"Spawned objects relative to {parentTransform.name} at position: {parentTransform.position}");
    }

    public void ResetExperience()
    {
        UpdateStatus("Resetting experience...");

        if (imageTrackingRoot != null)
        {
            Destroy(imageTrackingRoot);
            imageTrackingRoot = null;
        }

        hasSpawned = false;
        libraryInitialized = false;

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

        planeVisualizerController.ShowPlanes();

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
        if (trackButton != null) trackButton.onClick.RemoveAllListeners();
        if (resetButton != null) resetButton.onClick.RemoveAllListeners();
    }
}