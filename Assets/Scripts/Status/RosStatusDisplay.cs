using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.XR;

public class ROSStatusDisplay : MonoBehaviour
{
    public static ROSStatusDisplay Instance { get; private set; }

    public TextMeshProUGUI rosStatusText;
    public TextMeshProUGUI teleopStatusText;
    public GameObject menu;

    private bool isROSConnected = false;
    private bool isTeleopOn = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(CheckROSConnection), 0f, 1f);
        UpdateUI();
    }

    private void Update()
    {
        // isROSConnected = !ROSConnection.GetOrCreateInstance().HasConnectionError;
        // Debug.Log($"isROSConnected:{isROSConnected}");
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            ToggleTeleoperation();
        }

        // UpdateUI();
    }

    private void CheckROSConnection()
    {
        isROSConnected = !ROSConnection.GetOrCreateInstance().HasConnectionError;
        UpdateUI();
    }

    private void UpdateUI()
    {
        rosStatusText.text = isROSConnected ? "ROS Connection : ON" : "ROS Connection : OFF";
        rosStatusText.color = isROSConnected ? Color.green : Color.red;

        teleopStatusText.text = isTeleopOn ? "Teleoperation: ON" : "Teleoperation: OFF";
        teleopStatusText.color = isTeleopOn ? Color.cyan : Color.gray;
    }

    public void SetTeleoperationStatus(bool status)
    {
        isTeleopOn = status;
        UpdateUI();
    }

    public void ToggleTeleoperation()
    {
        isTeleopOn = !isTeleopOn;

        MeshRenderer renderer = menu.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material[] materials = renderer.materials;
            Color targetColor = isTeleopOn ? Color.blue : Color.red;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].color = targetColor;
            }

            renderer.materials = materials;
        }
        else
        {
            Debug.LogError("no mesh renderer in menu");
        }

        UpdateUI();
    }

    public bool IsROSConnected()
    {
        return isROSConnected;
    }

    public bool IsTeleoperationOn()
    {
        return isTeleopOn;
    }
}
