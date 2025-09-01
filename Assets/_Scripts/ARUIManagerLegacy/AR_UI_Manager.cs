using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

public class AR_UI_Manager : MonoBehaviour
{
    [SerializeField] private Button trackImage1Button;
    [SerializeField] private Button trackImage2Button;
    [SerializeField] private RuntimeReferenceImageLibrary trackImageLibrary;


    void Start()
    {
        trackImage1Button.onClick.AddListener(() =>
        {
            Debug.Log("Clicked Button 1");
        });
        trackImage2Button.onClick.AddListener(() =>
        {
            Debug.Log("Clicked Button 2");
        });
    }

}
