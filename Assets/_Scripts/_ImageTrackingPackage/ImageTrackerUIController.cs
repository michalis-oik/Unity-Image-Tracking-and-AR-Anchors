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
        // Setup button listeners
        if (trackButton != null)
        {
            trackButton.onClick.AddListener(OnTrackButtonClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClicked);
            resetButton.gameObject.SetActive(false);
        }

        if (distanceText != null && distancePanel != null)
        {
            distancePanel.SetActive(false);
        }

        // Try to find plane manager if not assigned
        if (planeManager == null && autoManagePlanes)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        // Subscribe to tracker events
        if (imageTracker != null)
        {
            imageTracker.OnStatusUpdate.AddListener(UpdateStatusText);
            imageTracker.OnDistanceUpdate.AddListener(UpdateDistanceText);
            imageTracker.OnTrackingButtonStateChange.AddListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.AddListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.AddListener(OnTrackingStateChanged);
            imageTracker.OnAnchorCreated.AddListener(OnAnchorCreated);
            imageTracker.OnTransformCreated.AddListener(OnTransformCreated);
            imageTracker.OnTrackingLost.AddListener(OnTrackingLost);
            imageTracker.OnTrackingRestored.AddListener(OnTrackingRestored);
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

    private void OnAnchorCreated(ARAnchor anchor)
    {
        if (autoManagePlanes && planeManager != null)
        {
            HidePlanes();
        }
    }

    private void OnTransformCreated(Transform transform)
    {
        if (autoManagePlanes && planeManager != null)
        {
            HidePlanes();
        }
    }

    private void OnTrackingLost()
    {
        // Optional: Show planes when tracking is lost
        // if (autoManagePlanes && planeManager != null)
        // {
        //     ShowPlanes();
        // }
    }

    private void OnTrackingRestored()
    {
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
        // Handle UI changes based on tracking state
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

    /// <summary>
    /// Sets the visibility of all currently tracked planes.
    /// </summary>
    /// <param name="isVisible">True to show planes, false to hide them.</param>
    private void SetAllPlanesActive(bool isVisible)
    {
        if (planeManager == null)
        {
            Debug.LogWarning("ARPlaneManager not found!", this);
            return;
        }

        // Iterate through all tracked planes and set their game object active/inactive.
        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(isVisible);
        }
    }

    // Public function to show the planes.
    public void ShowPlanes()
    {
        if (planeManager != null)
        {
            SetAllPlanesActive(true);
            Debug.Log("AR Planes are now VISIBLE.");
        }
    }

    // Public function to hide the planes.
    public void HidePlanes()
    {
        if (planeManager != null)
        {
            SetAllPlanesActive(false);
            Debug.Log("AR Planes are now HIDDEN.");
        }
    }

    void OnDestroy()
    {
        // Clean up event listeners
        if (imageTracker != null)
        {
            imageTracker.OnStatusUpdate.RemoveListener(UpdateStatusText);
            imageTracker.OnDistanceUpdate.RemoveListener(UpdateDistanceText);
            imageTracker.OnTrackingButtonStateChange.RemoveListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.RemoveListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.RemoveListener(OnTrackingStateChanged);
            imageTracker.OnAnchorCreated.RemoveListener(OnAnchorCreated);
            imageTracker.OnTransformCreated.RemoveListener(OnTransformCreated);
            imageTracker.OnTrackingLost.RemoveListener(OnTrackingLost);
            imageTracker.OnTrackingRestored.RemoveListener(OnTrackingRestored);
        }

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