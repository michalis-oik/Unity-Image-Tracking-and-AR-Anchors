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
    [SerializeField] private GameObject downPrefab;
    [SerializeField] private GameObject upPrefab;
    [SerializeField] private GameObject rightPrefab;
    [SerializeField] private GameObject forwardPrefab;
    [SerializeField] private float spawnDistance = 0.5f;

    [Header("UI Elements")]
    [SerializeField] private Button trackButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI distanceText;

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

        if (distanceText != null)
        {
            distanceText.gameObject.SetActive(false);
        }

        UpdateStatus("Press 'Track Image' to begin");
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
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

        bool imageExists = false;
        if (runtimeLibrary.count > 0)
        {
            imageExists = true;
        }

        if (!imageExists && downloadedTexture != null)
        {
            var jobState = runtimeLibrary.ScheduleAddImageWithValidationJob(
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

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (!libraryInitialized) return;

        // Process added and updated images
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            UpdateDistanceText(trackedImage); // Update distance on add
            ProcessTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            UpdateDistanceText(trackedImage); // Update distance on update
            ProcessTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.removed)
        {
            if (distanceText != null && downloadedTexture != null)
            {
                distanceText.gameObject.SetActive(false);
            }
        }
    }

    #endregion
    
    private void UpdateDistanceText(ARTrackedImage trackedImage)
    {
        // Don't update distance if objects are already spawned, or UI isn't set
        if (hasSpawned || distanceText == null || downloadedTexture == null) return;
        
        // Only show distance for the correct image
        if (trackedImage.referenceImage.name != downloadedTexture.name) return;

        if (trackedImage.trackingState == TrackingState.Tracking || trackedImage.trackingState == TrackingState.Limited)
        {
            if (!distanceText.gameObject.activeSelf)
            {
                distanceText.gameObject.SetActive(true);
            }
            // Calculate distance between the main camera and the tracked image
            float distance = Vector3.Distance(Camera.main.transform.position, trackedImage.transform.position);
            distanceText.text = $"Distance: {distance:F2} m";
        }
        else
        {
            // If tracking is lost, hide the text
            distanceText.gameObject.SetActive(false);
        }
    }

    #region ProcessTrackedImage
    private async void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (hasSpawned || downloadedTexture == null ||
            trackedImage.referenceImage.name != downloadedTexture.name)
            return;

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            if (spawnedAnchor != null)
            {
                Destroy(spawnedAnchor.gameObject);
            }

            Pose anchorPose = new Pose(trackedImage.transform.position, trackedImage.transform.rotation);
            Result<ARAnchor> result = await anchorManager.TryAddAnchorAsync(anchorPose);

            if (result.status.IsSuccess())
            {
                spawnedAnchor = result.value;
            }
            else
            {
                spawnedAnchor = null;
            }

            if (spawnedAnchor != null)
            {
                SpawnObjects(spawnedAnchor.transform);
                hasSpawned = true;

                // if (distanceText != null)
                // {
                //     distanceText.gameObject.SetActive(false);
                // }

                UpdateStatus("Objects placed! Press Reset to scan again.");
                if (resetButton != null) resetButton.gameObject.SetActive(true);
                if (trackButton != null) trackButton.interactable = false;
                if (trackedImageManager != null) trackedImageManager.enabled = false;
            }
            else
            {
                UpdateStatus("Failed to create an anchor. Please try again.");
            }
        }
    }
    #endregion

    #region SpawnObjects
    private void SpawnObjects(Transform anchor)
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        Vector3 upPos = anchor.position + anchor.up * spawnDistance;
        Vector3 downPos = anchor.position - anchor.up * spawnDistance;
        Vector3 rightPos = anchor.position + anchor.right * spawnDistance;
        Vector3 forwardPos = anchor.position + anchor.forward * spawnDistance;

        if (upPrefab != null)
        {
            GameObject upObj = Instantiate(upPrefab, downPos, anchor.rotation);
            upObj.transform.SetParent(anchor);
            spawnedObjects.Add(upObj);
        }
        if (downPrefab != null)
        {
            GameObject downObj = Instantiate(downPrefab, upPos, anchor.rotation);
            downObj.transform.SetParent(anchor);
            spawnedObjects.Add(downObj);
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

        if (spawnedAnchor != null)
        {
            Destroy(spawnedAnchor.gameObject);
            spawnedAnchor = null;
        }

        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        hasSpawned = false;
        libraryInitialized = false;

        if (trackedImageManager != null)
        {
            trackedImageManager.enabled = false;
        }

        if (distanceText != null)
        {
            distanceText.gameObject.SetActive(false);
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