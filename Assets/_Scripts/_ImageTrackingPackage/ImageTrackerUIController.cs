using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;

public class ImageTrackerUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button trackButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private GameObject distancePanel;

    [Header("Tracker Reference")]
    [SerializeField] private ConfigurableImageTracker imageTracker;

    [Header("Plane Visualization")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private bool autoManagePlanes = true;

    void Start()
    {
        if (trackButton != null) trackButton.onClick.AddListener(OnTrackButtonClicked);
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClicked);
            resetButton.gameObject.SetActive(false);
        }

        if (distancePanel != null) distancePanel.SetActive(false);

        if (planeManager == null && autoManagePlanes)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        if (imageTracker != null)
        {
            imageTracker.OnStatusUpdate.AddListener(UpdateStatusText);
            imageTracker.OnDistanceUpdate.AddListener(UpdateDistanceText);
            imageTracker.OnTrackingButtonStateChange.AddListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.AddListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.AddListener(OnTrackingStateChanged);

            // --- MODIFIED: Subscribe to the new unified event ---
            imageTracker.OnImageTracked.AddListener(OnImageTracked);
        }
    }

    private void OnTrackButtonClicked()
    {
        imageTracker.StartImageTracking();
        if (autoManagePlanes && planeManager != null)
        {
            ShowPlanes();
        }
    }

    private void OnResetButtonClicked()
    {
        imageTracker.ResetExperience();
        if (autoManagePlanes && planeManager != null)
        {
            ShowPlanes();
        }
    }

    // --- NEW: Listener for the TrackedImageResultEvent ---
    private void OnImageTracked(TrackedImageResult result)
    {
        // This single function now handles both anchor and transform based results.
        Debug.Log($"UI Controller received tracked image: {result.ImageName}, IsAnchor: {result.IsRootAnchor}");
        
        // Example: Hide planes once any image is successfully tracked.
        if (autoManagePlanes && planeManager != null)
        {
            HidePlanes();
        }
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void UpdateDistanceText(float distance)
    {
        if (distanceText != null && distancePanel != null)
        {
            if (!distancePanel.activeSelf)
            {
                distancePanel.SetActive(true);
            }
            distanceText.text = $"Distance: {distance:F2} m";
        }
    }

    private void SetTrackButtonState(bool interactable)
    {
        if (trackButton != null)
        {
            trackButton.interactable = interactable;
        }
    }

    private void SetResetButtonState(bool visible)
    {
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(visible);
        }
    }

    private void OnTrackingStateChanged(ConfigurableImageTracker.TrackingState state)
    {
        switch (state)
        {
            case ConfigurableImageTracker.TrackingState.Lost:
                if (distancePanel != null)
                {
                    distancePanel.SetActive(false);
                }
                break;
        }
    }

    #region Plane Management
    private void SetAllPlanesActive(bool isVisible)
    {
        if (planeManager == null) return;
        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(isVisible);
        }
    }
    
    public void ShowPlanes()
    {
        SetAllPlanesActive(true);
    }
    
    public void HidePlanes()
    {
        SetAllPlanesActive(false);
    }
    #endregion

    void OnDestroy()
    {
        if (imageTracker != null)
        {
            imageTracker.OnStatusUpdate.RemoveListener(UpdateStatusText);
            imageTracker.OnDistanceUpdate.RemoveListener(UpdateDistanceText);
            imageTracker.OnTrackingButtonStateChange.RemoveListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.RemoveListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.RemoveListener(OnTrackingStateChanged);
            // --- MODIFIED: Unsubscribe from the new event ---
            imageTracker.OnImageTracked.RemoveListener(OnImageTracked);
        }

        if (trackButton != null) trackButton.onClick.RemoveAllListeners();
        if (resetButton != null) resetButton.onClick.RemoveAllListeners();
    }
}