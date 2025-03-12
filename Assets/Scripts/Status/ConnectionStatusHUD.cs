using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectionStatusHUD : MonoBehaviour
{
    public GameObject hudPanel; 
    private bool isActive = false;

    void Update()
    {
        // A button
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) 
        {
            isActive = !isActive;
            hudPanel.SetActive(isActive);
        }
    }
}