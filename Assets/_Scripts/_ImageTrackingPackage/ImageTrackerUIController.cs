using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        // Subscribe to tracker events
        if (imageTracker != null)
        {
            imageTracker.OnStatusUpdate.AddListener(UpdateStatusText);
            imageTracker.OnDistanceUpdate.AddListener(UpdateDistanceText);
            imageTracker.OnTrackingButtonStateChange.AddListener(SetTrackButtonState);
            imageTracker.OnResetButtonStateChange.AddListener(SetResetButtonState);
            imageTracker.OnTrackingStateChanged.AddListener(OnTrackingStateChanged);
        }
    }

    private void OnTrackButtonClicked()
    {
        imageTracker.StartImageTracking();
    }

    private void OnResetButtonClicked()
    {
        imageTracker.ResetExperience();
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