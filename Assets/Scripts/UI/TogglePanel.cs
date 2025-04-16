using UnityEngine;

public class ClosePanel : MonoBehaviour
{
    public GameObject panel;

    void Update()
    {
        // check for Left controller's Two button(X button)
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
        {
            TogglePanel();
        }
    }

    private void TogglePanel()
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf); 
        }
    }

    public void OnCloseButtonClicked()
    {
        if (panel != null)
        {
            panel.SetActive(false); // UI button for close panel
        }
    }
}