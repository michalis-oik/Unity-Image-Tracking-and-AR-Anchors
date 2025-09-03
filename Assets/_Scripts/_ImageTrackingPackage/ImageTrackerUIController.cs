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

        if (planeManager == null && autoManagePlanes)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        if (imageTracker != null)
        {
            imageTracker.OnStatusUpdate.AddListener(UpdateStatusText);
            imageTracker.OnTrackingButtonStateChange.AddListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.AddListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.AddListener(OnTrackingStateChanged);
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
    
    private void OnImageTracked(TrackedImageResult result)
    {
        Debug.Log($"UI Controller received tracked image: {result.ImageName}, IsAnchor: {result.IsRootAnchor}");
        
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
        // UI logic for different states can be added here if needed.
        // For example, changing the color of the status text.
        // The logic for hiding the distance panel on 'Lost' state is no longer needed.
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
            imageTracker.OnTrackingButtonStateChange.RemoveListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.RemoveListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.RemoveListener(OnTrackingStateChanged);
            imageTracker.OnImageTracked.RemoveListener(OnImageTracked);
        }

        if (trackButton != null) trackButton.onClick.RemoveAllListeners();
        if (resetButton != null) resetButton.onClick.RemoveAllListeners();
    }
}