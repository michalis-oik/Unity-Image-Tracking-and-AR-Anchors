using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking;
using UnityEngine.Events;
using System.Collections;

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

    public enum LibrarySource
    {
        DynamicCreation,
        InspectorReference
    }

    [Header("AR System Dependencies")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private ARAnchorManager anchorManager;

    [Header("Tracking Configuration")]
    [SerializeField] private TrackingMode trackingMode = TrackingMode.AnchorBased;
    [SerializeField] private LibrarySource librarySource = LibrarySource.DynamicCreation;
    
    [Header("Image Target Setup")]
    [SerializeField] private string imageUrl;
    [SerializeField] private float physicalImageSize = 0.1f;
    
    [Header("Reference Library (Inspector Reference Mode Only)")]
    [SerializeField] private XRReferenceImageLibrary referenceImageLibrary;

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
    
    [Header("Setup & Initialization Events")]
    public UnityEvent OnTrackingInitialized;
    public UnityEvent OnImageDownloaded;
    public UnityEvent OnReadyToScan;
    
    [Header("Tracking Result Events")]
    public UnityEvent<ARAnchor> OnAnchorCreated;
    public UnityEvent<Transform> OnTransformCreated;
    
    [Header("Tracking State Events")]
    public UnityEvent<TrackingState> OnTrackingStateChanged;
    public UnityEvent OnTrackingLost;
    public UnityEvent OnTrackingRestored;
    public UnityEvent OnTrackingReset;
    
    [Header("UI Update Events")]
    public UnityEvent<string> OnStatusUpdate;
    public UnityEvent<float> OnDistanceUpdate;
    public UnityEvent<bool> OnTrackingButtonStateChange;
    public UnityEvent<bool> OnResetButtonStateChange;

    void Start()
    {
        SetTrackingState(TrackingState.NotInitialized);
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

    #region Public API Methods
    public void StartImageTracking()
    {
        if (isDownloading) return;

        if (librarySource == LibrarySource.DynamicCreation && downloadedTexture == null)
        {
            // First time - need to download
            StartCoroutine(DownloadAndSetupImage());
        }
        else
        {
            // Already downloaded or using inspector reference, just setup tracking
            SetupImageTracking();
        }
    }

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

        OnResetButtonStateChange?.Invoke(false);
        OnTrackingButtonStateChange?.Invoke(true);

        OnTrackingReset?.Invoke();
        SetTrackingState(TrackingState.NotInitialized);
        UpdateStatus("Press 'Track Image' to begin");
    }

    public void SetImageUrl(string url)
    {
        imageUrl = url;
    }

    public void SetPhysicalImageSize(float size)
    {
        physicalImageSize = size;
    }

    public void SetTrackingMode(TrackingMode mode)
    {
        trackingMode = mode;
    }

    public void SetLibrarySource(LibrarySource source)
    {
        librarySource = source;
    }

    public void SetReferenceImageLibrary(XRReferenceImageLibrary library)
    {
        referenceImageLibrary = library;
    }
    #endregion

    #region Image Tracking
    private IEnumerator DownloadAndSetupImage()
    {
        isDownloading = true;
        SetTrackingState(TrackingState.Downloading);
        OnTrackingButtonStateChange?.Invoke(false);
        
        UpdateStatus("Downloading image...");
        
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateStatus($"Error: Failed to download image.\n{request.error}");
            OnTrackingButtonStateChange?.Invoke(true);
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

        if (librarySource == LibrarySource.InspectorReference)
        {
            SetupInspectorReferenceLibrary();
        }
        else
        {
            SetupDynamicLibrary();
        }

        if (!libraryInitialized)
        {
            OnTrackingButtonStateChange?.Invoke(true);
            return;
        }

        trackedImageManager.enabled = true;

        UpdateStatus("Ready! Please scan the image.");
        OnTrackingButtonStateChange?.Invoke(true);
            
        SetTrackingState(TrackingState.ReadyToScan);
        OnTrackingInitialized?.Invoke();
        OnReadyToScan?.Invoke();
    }

    private void SetupInspectorReferenceLibrary()
    {
        if (referenceImageLibrary == null)
        {
            UpdateStatus("Error: No reference image library assigned in Inspector");
            return;
        }

        trackedImageManager.referenceLibrary = referenceImageLibrary;
        libraryInitialized = true;
        UpdateStatus($"Using inspector reference library with {referenceImageLibrary.count} images");
    }

    private void SetupDynamicLibrary()
    {
        if (runtimeLibrary == null)
        {
            runtimeLibrary = trackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
            if (runtimeLibrary == null)
            {
                UpdateStatus("Error: Mutable runtime library not supported.");
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
        libraryInitialized = true;
    }
    #endregion

    #region Image Tracking Event Handler
    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (!libraryInitialized) return;

        // Process added and updated images
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            UpdateDistance(trackedImage);
            ProcessTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            UpdateDistance(trackedImage);
            ProcessTrackedImage(trackedImage);
        }

        foreach (var kvp in eventArgs.removed)
        {
            ARTrackedImage trackedImage = kvp.Value;
            // If we lose tracking of our target image
            if (ShouldProcessImage(trackedImage))
            {
                SetTrackingState(TrackingState.Lost);
                OnTrackingLost?.Invoke();
            }
        }
    }

    private bool ShouldProcessImage(ARTrackedImage trackedImage)
    {
        if (librarySource == LibrarySource.DynamicCreation)
        {
            return downloadedTexture != null && trackedImage.referenceImage.name == downloadedTexture.name;
        }
        else
        {
            // For inspector reference, process all images or implement specific logic
            return true;
        }
    }
    #endregion
    
    private void UpdateDistance(ARTrackedImage trackedImage)
    {
        if (!ShouldProcessImage(trackedImage)) return;

        if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking || 
            trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Limited)
        {
            // Calculate distance between the main camera and the tracked image
            float distance = Vector3.Distance(Camera.main.transform.position, trackedImage.transform.position);
            OnDistanceUpdate?.Invoke(distance);
        }
    }

    #region ProcessTrackedImage
    private async void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (!ShouldProcessImage(trackedImage)) return;

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
                    OnResetButtonStateChange?.Invoke(true);
                    OnTrackingButtonStateChange?.Invoke(false);
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
                    OnResetButtonStateChange?.Invoke(true);
                    OnTrackingButtonStateChange?.Invoke(false);
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
        }
        else if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Limited)
        {
            SetTrackingState(TrackingState.Limited);
        }
    }
    #endregion

    private void UpdateStatus(string message)
    {
        OnStatusUpdate?.Invoke(message);
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
    
    // Public properties to access tracking results
    public ARAnchor AnchorResult => spawnedAnchor;
    public Transform TransformResult => imageTrackingRoot != null ? imageTrackingRoot.transform : null;
    public bool IsTracking => (trackingMode == TrackingMode.AnchorBased && spawnedAnchor != null) || 
                             (trackingMode == TrackingMode.TransformBased && imageTrackingRoot != null);
    public TrackingState CurrentTrackingState => currentState;
    public string CurrentImageUrl => imageUrl;
    public float CurrentImageSize => physicalImageSize;
    public LibrarySource CurrentLibrarySource => librarySource;
    public XRReferenceImageLibrary CurrentReferenceLibrary => referenceImageLibrary;
}