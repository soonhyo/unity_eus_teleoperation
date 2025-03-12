using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitTransform : MonoBehaviour
{
    [SerializeField] private GameObject targetObject;
    // private bool isTransformInitialized = false;
    private bool isTrapped;
    // Start is called before the first frame update
    void Start()
    {
        isTrapped = false;

        // if (targetObject != null)
        // {
        //     // 현재 GameObject의 Transform을 targetObject의 Transform과 동일하게 설정
        //     // transform.position = targetObject.transform.position;
        //     // transform.rotation = targetObject.transform.rotation;
        // }
        // else
        // {
        //     Debug.LogWarning("targetObject is not set");
        // }
    }

    // Update is called once per frame
    void Update()
    {
        // if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        // {
        //     UpdateMoverTransform();
        // }

        // if (ROSStatusDisplay.Instance.IsROSConnected() && ROSStatusDisplay.Instance.IsTeleoperationOn() && (!isTransformInitialized))
        // {
        //     UpdateMoverTransform();
        //     isTransformInitialized = true;
        // }

        if (ROSStatusDisplay.Instance.IsROSConnected() && (!this.isTrapped))
        {
            UpdateMoverTransform();
        }
        // if (ROSStatusDisplay.Instance.IsTeleoperationOn())
        // {
        //     //visualize
        // }
            
    }

    void UpdateMoverTransform()
    {
        if (targetObject != null)
        {
            // 현재 GameObject의 Transform을 targetObject의 Transform과 동일하게 설정
            transform.position = targetObject.transform.position;
            transform.rotation = targetObject.transform.rotation;
        }
        else
        {
            Debug.LogWarning("targetObject is not set");
        }
    }

    public void setInTrapped(bool value)
    {
        this.isTrapped = value;
    }
    public bool getInTrapped()
    {
        return this.isTrapped;
    }
}
