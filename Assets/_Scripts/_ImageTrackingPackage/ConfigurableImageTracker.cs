using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using TMPro;

public class ConfigurableImageTracker : MonoBehaviour
{
    public enum TrackingMode
    {
        AnchorBased,
        TransformBased
    }
    
    public enum TrackingState
    {
        NotInitialized,
        Downloading,
        ReadyToScan,
        Tracking,
        Limited,
        Lost
    }

    [Header("AR System References")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private PlaneVisualizerController planeVisualizerController;

    [Header("Tracking Configuration")]
    [SerializeField] private TrackingMode trackingMode = TrackingMode.AnchorBased;
    
    [Header("Image Target Setup")]
    [SerializeField] private string imageUrl;
    [SerializeField] private float physicalImageSize = 0.1f;

    [Header("UI Elements")]
    [SerializeField] private Button trackButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI distanceText;

    // State Variables
    private bool isDownloading = false;
    private MutableRuntimeReferenceImageLibrary runtimeLibrary;
    private Texture2D downloadedTexture;
    private bool libraryInitialized = false;
    
    // Tracking Results
    private ARAnchor spawnedAnchor;
    private GameObject imageTrackingRoot;
    private TrackingState currentState = TrackingState.NotInitialized;

    // Events for external components
    [Header("Tracking Events")]
    public UnityEvent OnTrackingInitialized;
    public UnityEvent OnImageDownloaded;
    public UnityEvent OnReadyToScan;
    public UnityEvent<ARAnchor> OnAnchorCreated;
    public UnityEvent<Transform> OnTransformCreated;
    public UnityEvent<TrackingState> OnTrackingStateChanged;
    public UnityEvent OnTrackingLost;
    public UnityEvent OnTrackingRestored;
    public UnityEvent OnTrackingReset;

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
        SetTrackingState(TrackingState.NotInitialized);
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
        SetTrackingState(TrackingState.Downloading);
        if (trackButton != null) trackButton.interactable = false;
        
        UpdateStatus("Downloading image...");
        
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateStatus($"Error: Failed to download image.\n{request.error}");
            if (trackButton != null) trackButton.interactable = true;
            isDownloading = false;
            SetTrackingState(TrackingState.NotInitialized);
            yield break;
        }
        
        downloadedTexture = DownloadHandlerTexture.GetContent(request);
        downloadedTexture.name = "DynamicTarget";
        
        UpdateStatus("Image downloaded. Setting up tracking...");
        OnImageDownloaded?.Invoke();
        
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
        
        if (planeVisualizerController != null)
            planeVisualizerController.ShowPlanes();
            
        SetTrackingState(TrackingState.ReadyToScan);
        OnTrackingInitialized?.Invoke();
        OnReadyToScan?.Invoke();
    }
    #endregion

    #region Image Tracking Event Handler
    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (!libraryInitialized) return;

        // Process added and updated images
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            UpdateDistanceText(trackedImage);
            ProcessTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            UpdateDistanceText(trackedImage);
            ProcessTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.removed)
        {
            if (distanceText != null && downloadedTexture != null)
            {
                distanceText.gameObject.SetActive(false);
            }
            
            // If we lose tracking of our target image
            if (trackedImage.Value.referenceImage.name == downloadedTexture.name)
            {
                SetTrackingState(TrackingState.Lost);
                OnTrackingLost?.Invoke();
            }
        }
    }
    #endregion
    
    private void UpdateDistanceText(ARTrackedImage trackedImage)
    {
        if (distanceText == null || downloadedTexture == null) return;
        
        // Only show distance for the correct image
        if (trackedImage.referenceImage.name != downloadedTexture.name) return;

        if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking || trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Limited)
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
        if (downloadedTexture == null || trackedImage.referenceImage.name != downloadedTexture.name)
            return;

        if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
        {
            SetTrackingState(TrackingState.Tracking);
            
            if (trackingMode == TrackingMode.AnchorBased)
            {
                // Anchor-based tracking
                if (spawnedAnchor != null)
                {
                    Destroy(spawnedAnchor.gameObject);
                }

                Pose anchorPose = new Pose(trackedImage.transform.position, trackedImage.transform.rotation);
                Result<ARAnchor> result = await anchorManager.TryAddAnchorAsync(anchorPose);

                if (result.status.IsSuccess())
                {
                    spawnedAnchor = result.value;
                    OnAnchorCreated?.Invoke(spawnedAnchor);
                    
                    UpdateStatus("Anchor created successfully!");
                    if (resetButton != null) resetButton.gameObject.SetActive(true);
                    if (trackButton != null) trackButton.interactable = false;
                    if (trackedImageManager != null) trackedImageManager.enabled = false;
                }
                else
                {
                    UpdateStatus("Failed to create an anchor. Please try again.");
                }
            }
            else
            {
                // Transform-based tracking
                if (imageTrackingRoot == null)
                {
                    imageTrackingRoot = new GameObject("ImageTrackingRoot");
                    OnTransformCreated?.Invoke(imageTrackingRoot.transform);
                    UpdateStatus("Transform root created successfully!");
                    if (resetButton != null) resetButton.gameObject.SetActive(true);
                    if (trackButton != null) trackButton.interactable = false;
                }
                
                // Update the transform position and rotation
                imageTrackingRoot.transform.SetPositionAndRotation(
                    trackedImage.transform.position, trackedImage.transform.rotation);
                    
                // If we were in limited or lost state before, notify that tracking is restored
                if (currentState == TrackingState.Limited || currentState == TrackingState.Lost)
                {
                    OnTrackingRestored?.Invoke();
                }
            }
            
            if (planeVisualizerController != null)
                planeVisualizerController.HidePlanes();
        }
        else if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Limited)
        {
            SetTrackingState(TrackingState.Limited);
        }
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

        if (imageTrackingRoot != null)
        {
            Destroy(imageTrackingRoot);
            imageTrackingRoot = null;
        }

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

        if (planeVisualizerController != null)
            planeVisualizerController.ShowPlanes();

        OnTrackingReset?.Invoke();
        SetTrackingState(TrackingState.NotInitialized);
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
    
    private void SetTrackingState(TrackingState newState)
    {
        if (currentState != newState)
        {
            TrackingState previousState = currentState;
            currentState = newState;
            OnTrackingStateChanged?.Invoke(newState);
        }
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
    
    // Public properties to access tracking results
    public ARAnchor AnchorResult => spawnedAnchor;
    public Transform TransformResult => imageTrackingRoot != null ? imageTrackingRoot.transform : null;
    public bool IsTracking => (trackingMode == TrackingMode.AnchorBased && spawnedAnchor != null) || 
                             (trackingMode == TrackingMode.TransformBased && imageTrackingRoot != null);
    public TrackingState CurrentTrackingState => currentState;
}