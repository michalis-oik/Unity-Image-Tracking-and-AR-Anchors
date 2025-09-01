using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class PlaneVisualizerController : MonoBehaviour
{
    [Tooltip("The AR Plane Manager that will be controlled.")]
    public ARPlaneManager planeManager;

    void Awake()
    {
        if (planeManager == null)
        {
            planeManager = FindFirstObjectByType <ARPlaneManager>();
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
            Debug.LogError("ARPlaneManager not found!", this);
            return;
        }

        // Iterate through all tracked planes and set their game object active/inactive.
        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(isVisible);
        }
    }

    // Public function to be called by a UI Button to show the planes.
    public void ShowPlanes()
    {
        SetAllPlanesActive(true);
        Debug.Log("AR Planes are now VISIBLE.");
    }

    // Public function to be called by a UI Button to hide the planes.
    public void HidePlanes()
    {
        SetAllPlanesActive(false);
        Debug.Log("AR Planes are now HIDDEN.");
    }
}