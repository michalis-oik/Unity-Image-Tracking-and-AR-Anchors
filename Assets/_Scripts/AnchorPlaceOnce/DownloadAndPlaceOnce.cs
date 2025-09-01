using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Downloads an image, waits for it to be tracked, and then creates a permanent
/// ARAnchor at its location to spawn objects. 
/// </summary>
public class DownloadAndPlaceOnce : MonoBehaviour
{
    [Header("AR System References")]
    [Tooltip("The AR Tracked Image Manager in your scene.")]
    [SerializeField] private ARTrackedImageManager m_TrackedImageManager;
    [Tooltip("The AR Anchor Manager in your scene. Required for creating stable anchors.")]
    [SerializeField] private ARAnchorManager m_AnchorManager;

    [Header("Image Target Setup")]
    [Tooltip("The public URL of the image to be tracked.")]
    [SerializeField] private string imageUrl;
    [Tooltip("The physical width of the image in meters.")]
    [SerializeField] private float physicalImageSize = 0.1f;

    [Header("Spawning Setup")]
    public GameObject upPrefab;
    public GameObject rightPrefab;
    public GameObject forwardPrefab;
    public float spawnDistance = 0.5f;

    [Header("UI Elements")]
    [Tooltip("Button that triggers the ResetExperience method.")]
    [SerializeField] private Button resetButton;
    [Tooltip("Text field to show the user the current status.")]
    [SerializeField] private TextMeshProUGUI statusText;

    // --- State Variables ---
    private bool m_HasSpawned = false;
    private List<GameObject> m_SpawnedObjects = new List<GameObject>();
    private MutableRuntimeReferenceImageLibrary m_RuntimeLibrary;
    private ARAnchor m_SpawnedAnchor;

    void Start()
    {
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(false);
            resetButton.onClick.AddListener(ResetExperience);
        }

        if (m_TrackedImageManager == null || m_AnchorManager == null)
        {
            UpdateStatus("Error: ARTrackedImageManager or ARAnchorManager not assigned!");
            this.enabled = false;
            return;
        }

        StartCoroutine(SetupImageTracking());
    }

    void OnEnable()
    {
        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged); // += OnTrackablesChanged;
        }
    }

    void OnDisable()
    {
        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged); // -= OnTrackablesChanged;
        }
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            ProcessTrackedImage(trackedImage);
        }
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            ProcessTrackedImage(trackedImage);
        }
    }

    private IEnumerator SetupImageTracking()
    {
        UpdateStatus("Downloading image target...");
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateStatus($"Error: Failed to download image.\n{request.error}");
            yield break;
        }
        Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);
        downloadedTexture.name = "DynamicTarget";
        UpdateStatus("Image downloaded. Preparing AR library...");
        m_TrackedImageManager.enabled = false;
        m_RuntimeLibrary = m_TrackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
        if (m_RuntimeLibrary == null)
        {
            UpdateStatus("Error: Mutable runtime library not supported.");
            yield break;
        }
        var jobState = m_RuntimeLibrary.ScheduleAddImageWithValidationJob(downloadedTexture, downloadedTexture.name, physicalImageSize);
        yield return new WaitUntil(() => jobState.jobHandle.IsCompleted);
        if (jobState.status != AddReferenceImageJobStatus.Success)
        {
            UpdateStatus($"Error: Could not add image to library.\nStatus: {jobState.status}");
            yield break;
        }
        m_TrackedImageManager.referenceLibrary = m_RuntimeLibrary;
        m_TrackedImageManager.enabled = true;
        UpdateStatus("Ready! Please scan the image.");
    }

    private void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (m_HasSpawned)
        {
            return;
        }

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            GameObject anchorGO = new GameObject("Spawn Anchor");
            anchorGO.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);

            m_SpawnedAnchor = anchorGO.AddComponent<ARAnchor>();

            if (m_SpawnedAnchor != null)
            {
                // Spawn objects as children of the new, stable anchor.
                SpawnObjects(m_SpawnedAnchor.transform);
                m_HasSpawned = true;

                // Now that we have a permanent anchor, we can disable image tracking.
                m_TrackedImageManager.enabled = false;

                UpdateStatus("Objects placed! Press Reset to scan again.");
                if (resetButton != null)
                {
                    resetButton.gameObject.SetActive(true);
                }
            }
            else
            {
                UpdateStatus("Failed to create an anchor. Please try again.");
                Destroy(anchorGO); 
            }
        }
    }

    private void SpawnObjects(Transform anchor)
    {
        Vector3 upPos = anchor.position + (anchor.up * spawnDistance);
        Vector3 rightPos = anchor.position + (anchor.right * spawnDistance);
        Vector3 fwdPos = anchor.position + (anchor.forward * spawnDistance);

        GameObject upObj = Instantiate(upPrefab, upPos, anchor.rotation);
        GameObject rightObj = Instantiate(rightPrefab, rightPos, anchor.rotation);
        GameObject fwdObj = Instantiate(forwardPrefab, fwdPos, anchor.rotation);

        upObj.transform.SetParent(anchor);
        rightObj.transform.SetParent(anchor);
        fwdObj.transform.SetParent(anchor);

        m_SpawnedObjects.Add(upObj);
        m_SpawnedObjects.Add(rightObj);
        m_SpawnedObjects.Add(fwdObj);

        Debug.Log("Spawned object group and parented them to a new ARAnchor.");
    }

    public void ResetExperience()
    {
        UpdateStatus("Resetting...");
        if (m_SpawnedAnchor != null)
        {
            Destroy(m_SpawnedAnchor.gameObject);
        }
        m_SpawnedAnchor = null;
        m_SpawnedObjects.Clear();
        m_HasSpawned = false;

        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.enabled = true;
        }
        
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(false);
        }
        UpdateStatus("Ready! Please scan the image.");
    }

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
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetExperience);
        }
    }
}