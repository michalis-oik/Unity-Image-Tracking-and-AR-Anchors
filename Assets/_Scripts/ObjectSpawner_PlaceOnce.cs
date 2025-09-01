using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

/// <summary>
/// Spawns objects relative to a tracked image ONCE, then disables the
/// image tracking manager to lock the objects in world space.
/// Also destroys previously spawned objects when reset.
/// </summary>
public class ObjectSpawner_PlaceOnce : MonoBehaviour
{
    [Header("Prefabs & Settings")]
    public GameObject upPrefab;
    public GameObject rightPrefab;
    public GameObject forwardPrefab;
    public float spawnDistance = 0.5f;

    [Header("AR System Reference")]
    [SerializeField] ARTrackedImageManager m_TrackedImageManager;

    private bool hasSpawned = false;
    
    // ADDED: A list to keep track of the objects we've created.
    private List<GameObject> m_spawnedObjects = new List<GameObject>();

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
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            if (!hasSpawned)
            {
                SpawnObjectsForImage(trackedImage);
                hasSpawned = true;
                m_TrackedImageManager.enabled = false;
                Debug.Log("[Place Once Mode] Objects spawned. ARTrackedImageManager disabled.");
                return;
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

        Transform t = trackedImage.transform;

        // MODIFIED: Capture the instantiated objects and add them to our list.
        GameObject newUpObject = Instantiate(upPrefab, t.position + (t.up * spawnDistance), t.rotation);
        m_spawnedObjects.Add(newUpObject);

        GameObject newRightObject = Instantiate(rightPrefab, t.position + (t.right * spawnDistance), t.rotation);
        m_spawnedObjects.Add(newRightObject);

        GameObject newFrontObject = Instantiate(forwardPrefab, t.position + (t.forward * spawnDistance), t.rotation);
        m_spawnedObjects.Add(newFrontObject);

        Debug.Log($"[Place Once Mode] Spawned object group for image '{trackedImage.referenceImage.name}'.");
    }

    /// <summary>
    /// Destroys all previously spawned objects and resets the spawner state.
    /// </summary>
    public void ResetForNewScan()
    {
        // MODIFIED: Add logic to destroy existing objects before resetting.
        Debug.Log($"[Place Once Mode] Resetting. Destroying {m_spawnedObjects.Count} old objects.");
        foreach (GameObject obj in m_spawnedObjects)
        {
            Destroy(obj);
        }
        
        // After destroying them, clear the list to remove the dead references.
        m_spawnedObjects.Clear();
        
        // Now reset the state as before.
        hasSpawned = false;
        
        // We still don't re-enable the manager here; the Library Switcher handles that.
        Debug.Log("[Place Once Mode] Spawner has been reset and is ready for a new scan.");
    }
}