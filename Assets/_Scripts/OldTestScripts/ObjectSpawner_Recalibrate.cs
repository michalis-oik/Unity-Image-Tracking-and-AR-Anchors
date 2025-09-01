using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

/// <summary>
/// Spawns objects relative to a tracked image and continuously RECALIBRATES
/// their position and rotation whenever the image is visible.
/// </summary>
public class ObjectSpawner_Recalibrate : MonoBehaviour
{
    [Header("Prefabs & Settings")]
    public GameObject upPrefab;
    public GameObject rightPrefab;
    public GameObject forwardPrefab;
    public float spawnDistance = 0.5f;

    [Header("AR System Reference")]
    [SerializeField] ARTrackedImageManager m_TrackedImageManager;

    private class SpawnedObjectGroup
    {
        public GameObject upObject;
        public GameObject rightObject;
        public GameObject frontObject;
    }

    private readonly Dictionary<string, SpawnedObjectGroup> m_SpawnedObjects = new Dictionary<string, SpawnedObjectGroup>();

    void Awake()
    {
        if (m_TrackedImageManager == null)
        {
            Debug.LogError("ARTrackedImageManager has not been assigned in the Inspector!");
        }
    }

    void OnEnable()
    {
        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }
    }

    void OnDisable()
    {
        if (m_TrackedImageManager != null)
        {
            m_TrackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // Handle newly detected images
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            string imageName = trackedImage.referenceImage.name;
            if (!m_SpawnedObjects.ContainsKey(imageName))
            {
                SpawnObjectsForImage(trackedImage);
            }
        }

        // Handle re-detected images (Recalibrate positions)
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            string imageName = trackedImage.referenceImage.name;
            if (m_SpawnedObjects.TryGetValue(imageName, out SpawnedObjectGroup spawnedGroup))
            {
                if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
                {
                    spawnedGroup.upObject.transform.position = trackedImage.transform.position + (trackedImage.transform.up * spawnDistance);
                    spawnedGroup.upObject.transform.rotation = trackedImage.transform.rotation;
                    spawnedGroup.upObject.SetActive(true);

                    spawnedGroup.rightObject.transform.position = trackedImage.transform.position + (trackedImage.transform.right * spawnDistance);
                    spawnedGroup.rightObject.transform.rotation = trackedImage.transform.rotation;
                    spawnedGroup.rightObject.SetActive(true);

                    spawnedGroup.frontObject.transform.position = trackedImage.transform.position + (trackedImage.transform.forward * spawnDistance);
                    spawnedGroup.frontObject.transform.rotation = trackedImage.transform.rotation;
                    spawnedGroup.frontObject.SetActive(true);
                }
            }
        }
    }

    private void SpawnObjectsForImage(ARTrackedImage trackedImage)
    {
        if (upPrefab == null || rightPrefab == null || forwardPrefab == null)
        {
            Debug.LogError("One or more prefabs are not set. Cannot spawn objects.");
            return;
        }

        string imageName = trackedImage.referenceImage.name;
        Transform t = trackedImage.transform;

        GameObject newUpObject = Instantiate(upPrefab, t.position + (t.up * spawnDistance), t.rotation);
        GameObject newRightObject = Instantiate(rightPrefab, t.position + (t.right * spawnDistance), t.rotation);
        GameObject newFrontObject = Instantiate(forwardPrefab, t.position + (t.forward * spawnDistance), t.rotation);

        SpawnedObjectGroup newGroup = new SpawnedObjectGroup
        {
            upObject = newUpObject,
            rightObject = newRightObject,
            frontObject = newFrontObject
        };

        m_SpawnedObjects.Add(imageName, newGroup);
        Debug.Log($"[Recalibrate Mode] Spawned object group for image '{imageName}'.");
    }
}