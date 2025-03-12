using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.XR;

public class ROSStatusDisplay : MonoBehaviour
{
    public static ROSStatusDisplay Instance { get; private set; }

    public TextMeshProUGUI rosStatusText;
    public TextMeshProUGUI teleopStatusText;

    private bool isROSConnected = false;
    private bool isTeleopOn = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 변경 시 유지
        }
        else
        {
            Destroy(gameObject); // 중복 생성 방지
            return;
        }
    }

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        // ROS 연결 상태 체크
        isROSConnected = ROSConnection.GetOrCreateInstance().HasConnectionThread;
        
        // OVRInput을 사용하여 A 버튼 입력 감지
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            ToggleTeleoperation();
        }

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
