using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.JskSciurus17Teleop;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
using TMPro;
public class InputManager : MonoBehaviour
{
    [SerializeField] private GameObject rightController;
    [SerializeField] private GameObject leftController;
    [SerializeField] private Transform hmdTransform; // HMD/CenterEyeAnchor reference
    
    private readonly List<string> targetPoses = new List<string> { "right_wrist_target_pose", "left_wrist_target_pose", "head_target_pose", "waist_target_pose" };
    private readonly List<string> targetValues = new List<string> { "right_hand", "left_hand" };
    
    private ROSConnection ros;
    // private bool isInitPose = false;
    private bool isDataRecording = false;
    private bool canStartRecording = true;
    private IEnumerator AllowStartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canStartRecording = true;
    }
    public TMP_InputField bagNameInput;
    private bool gripperToggleMode = false;
    private float gripperToggleValue = 0f;
    private HeaderMsg headerMsg;
    private bool hmdMode = true;
    public GameObject robotRoot;
    public TextMeshPro recordStatusText;
    private float cancelHoldTime = 0f;
    private const float cancelHoldThreshold = 3f;
    private bool isCancelSent = false;
    private bool isRelativePoseControl = false;

    private readonly Dictionary<ControllerType, ControllerState> controllers = new Dictionary<ControllerType, ControllerState>
    {
        { ControllerType.Right, new ControllerState() },
        { ControllerType.Left, new ControllerState() }
    };
    private readonly Dictionary<string, PoseStampedMsg> poseMsgPool = new Dictionary<string, PoseStampedMsg>();
    private readonly Dictionary<string, Float32Msg> floatMsgPool = new Dictionary<string, Float32Msg>();
    private readonly Dictionary<string, PointMsg> pointMsgPool = new Dictionary<string, PointMsg>();
    private readonly Dictionary<string, QuaternionMsg> quaternionMsgPool = new Dictionary<string, QuaternionMsg>();
    private readonly List<string> commandPoseList = new List<string> { "init_pose"};
    private StringMsg commandMsg = new StringMsg{};
    private enum ControllerType { Right, Left }

    public class ControllerState
    {
        public GameObject ControllerObject;
        public OVRControllerHelper Helper;
        public bool IsPosePublishing;
        public bool IsGripperPublishing;
        public float TriggerValue;
        public bool GripperToggleMode;
        public float GripperToggleValue;
        public RaycastHit[] RaycastHits = new RaycastHit[10];
        
        public Vector3 lockControllerWorldPosition;
        public Quaternion lockControllerWorldRotation;
        public Vector3 lockTargetWorldPosition;
        public Quaternion lockTargetWorldRotation;
        public Quaternion lockHmdWorldRotation; // HMD rotation at lock time
        public Quaternion lockRobotWorldRotation; // Robot rotation at lock time
    }

    public void ToggleReletivePoseControl()
    {
        isRelativePoseControl = !isRelativePoseControl;
        
        // Clear all controller states when switching modes
        foreach (var controller in controllers)
        {
            ClearControllerState(controller.Value);
        }
    }

    private void ClearControllerState(ControllerState state)
    {
        state.lockControllerWorldPosition = Vector3.zero;
        state.lockControllerWorldRotation = Quaternion.identity;
        state.lockTargetWorldPosition = Vector3.zero;
        state.lockTargetWorldRotation = Quaternion.identity;
        state.lockHmdWorldRotation = Quaternion.identity;
        state.lockRobotWorldRotation = Quaternion.identity;
    }

    public void ToggleGripperToggleModeLeft()
    {
        var state = controllers[ControllerType.Left];
        state.GripperToggleMode = !state.GripperToggleMode;
    }
    public void ToggleGripperToggleModeRight()
    {
        var state = controllers[ControllerType.Right];
        state.GripperToggleMode = !state.GripperToggleMode;
    }
    public void ToggleHMDMode()
    {
        hmdMode = !hmdMode;
    }
    void Start()
    {
        InitializeControllers();
        InitializeROS();
    }
    void FixedUpdate()
    {
        foreach (var controller in controllers)
        {
            UpdateController(controller.Key);
        }
    }
    private void InitializeControllers()
    {
        controllers[ControllerType.Right].ControllerObject = rightController;
        controllers[ControllerType.Left].ControllerObject = leftController;
        
        foreach (var controller in controllers)
        {
            controller.Value.Helper = controller.Value.ControllerObject.GetComponent<OVRControllerHelper>();
        }
    }
    private void InitializeROS()
    {
        ros = ROSConnection.GetOrCreateInstance();
        
        foreach (var targetPose in targetPoses)
        {
            ros.RegisterPublisher<PoseStampedMsg>(targetPose);
            poseMsgPool[targetPose] = CreatePoseStampedMsg();
            pointMsgPool[targetPose] = new PointMsg();
            quaternionMsgPool[targetPose] = new QuaternionMsg();
        }

        foreach (var targetValue in targetValues)
        {
            ros.RegisterPublisher<Float32Msg>(targetValue);
            floatMsgPool[targetValue] = new Float32Msg();
        }

        ros.RegisterPublisher<StringMsg>("/vr_command");
        headerMsg = new HeaderMsg {frame_id = "base_link"};
                    
        //ros.RegisterRosService<TriggerRequest, TriggerResponse>("start_rosbag");
        // StartRosbagRequest req = new StartRosbagRequest { bag_name = "hair_styling_01" };
        // ros.SendServiceMessage<StartRosbagResponse>("start_rosbag", req, callback);
    
        ros.RegisterRosService<StartRosbagRequest, StartRosbagResponse>("start_rosbag");
        ros.RegisterRosService<TriggerRequest, TriggerResponse>("stop_rosbag");
        ros.RegisterRosService<CancelRosbagRequest, CancelRosbagResponse>("cancel_rosbag");
    }
    private void StartRosbagWithName()
    {
        if (string.IsNullOrEmpty(bagNameInput.text))
        {
            Debug.LogWarning("Bag name is empty!");
            return;
        }

        string inputBagName = bagNameInput.text.Trim();
        StartRosbagRequest req = new StartRosbagRequest { bag_name = inputBagName };
        ros.SendServiceMessage<StartRosbagResponse>("start_rosbag", req, (res) =>
        {
            Debug.Log("StartRosbag Response: " + res.message);
            isDataRecording = res.success;
            recordStatusText.text = isDataRecording ? "Menu\nRecording: ON" : "Menu\nRecording: OFF";
        });
    }
    private void ToggleDataRecord()
    {
        if (isDataRecording)
        {
            TriggerRequest req = new TriggerRequest();
            ros.SendServiceMessage<TriggerResponse>("stop_rosbag", req, (res) =>
            {
                if (res.success)
                {
                    Debug.Log("Stop response: " + res.message);
                    isDataRecording = false;
                    canStartRecording = false;
                    recordStatusText.text = "Menu\nRecording: OFF";
                    StartCoroutine(AllowStartAfterDelay(3f));
                }
                else
                {
                    Debug.LogError("Failed to stop rosbag: " + res.message);
                }
            });
        }
        else
        {
            if (!canStartRecording)
            {
                Debug.LogWarning("Wait for rosbag to stop before starting again.");
                return;
            }

            StartRosbagWithName();
        }
    }


    // private void ToggleDataRecord()
    // {
    //     isDataRecording = !isDataRecording; 
    //     if (isDataRecording)
    //     {
    //         StartRosbag();
    //         Debug.Log("Started to record teleoperation data");  
    //     }
    //     else
    //     {
    //         StopRosbag();
    //         Debug.Log("Stop recording teleoperation data");         
    //     }
    // }

    private void HandleCancelRosbagHold(ControllerType type)
    {
        if (type != ControllerType.Left) return;

        if (OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            cancelHoldTime += Time.deltaTime;

            if (cancelHoldTime >= cancelHoldThreshold && !isCancelSent)
            {
                var cancelReq = new CancelRosbagRequest();
                ros.SendServiceMessage<CancelRosbagResponse>("cancel_rosbag", cancelReq, (res) =>
                {
                    Debug.Log("Cancel response: " + res.message);
                    recordStatusText.text = "Menu\nLast rosbag deleted";
                });

                isCancelSent = true;
            }
        }
        else
        {
            cancelHoldTime = 0f;
            isCancelSent = false;
        }
    }
    private void UpdateController(ControllerType type)
    {
        var state = controllers[type];
        
        if (!ROSStatusDisplay.Instance.IsTeleoperationOn())
        {
            ReleaseChildren(state);
            return;
        }

        if (isRelativePoseControl)
        {
            // if true -> check trigger button -> true -> change gripped to true -> add displacement (derivatives) of controller pose to current controller ball pose
            // change handleGrab compatible with relative pose and absolute pose
            // if false -> ignore this sentence
        }

        // if (!isInitPose)
        // {
        //     SendInitPose();
        //     isInitPose = true;
        // }

        if (type == ControllerType.Right && OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            SendInitPose();
        }
   
        if (type == ControllerType.Left && OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            ToggleDataRecord();
        }
        
        HandleCancelRosbagHold(type);

        if (state.Helper == null) return;

        float handTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, state.Helper.m_controller);

        if (handTriggerValue > 0.5f)
        {
            HandleGrab(type, state);
            if (state.IsPosePublishing) UpdateTriggerValues(type, state);
        }
        else
        {
            ReleaseChildren(state);
        }

        if (state.IsPosePublishing)
        {
            PublishChildrenPoses(state);
        }
    }

    private void HandleGrab(ControllerType type, ControllerState state)
    {
        if (state.IsPosePublishing) return;

        if (isRelativePoseControl)
        {
            // Direct controller-to-ball matching: left controller -> left ball, right controller -> right ball
            string targetTag = type == ControllerType.Right ? "right_wrist_target_pose" : "left_wrist_target_pose";

            GameObject target = GameObject.FindWithTag(targetTag);
            if (target != null)
            {
                target.GetComponent<InitTransform>().setInTrapped(true);

                if (ROSStatusDisplay.Instance.IsROSConnected() && ROSStatusDisplay.Instance.IsTeleoperationOn())
                {
                    state.IsPosePublishing = true;
                }

                // Lock positions when trigger pressed - ball starts from its current position
                state.lockControllerWorldPosition = state.ControllerObject.transform.position;
                state.lockControllerWorldRotation = state.ControllerObject.transform.rotation;
                state.lockTargetWorldPosition = target.transform.position;
                state.lockTargetWorldRotation = target.transform.rotation;
                // Store HMD and Robot orientation at lock time
                if (hmdTransform != null)
                {
                    state.lockHmdWorldRotation = hmdTransform.rotation;
                }
                if (robotRoot != null)
                {
                    state.lockRobotWorldRotation = robotRoot.transform.rotation;
                }
            }
            return;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            state.ControllerObject.transform.position,
            0.001f,
            state.ControllerObject.transform.forward,
            state.RaycastHits
        );

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = state.RaycastHits[i].collider.transform;
            string hitTag = hitTransform.tag;

            if (targetPoses.Contains(hitTag))
            {
                // Absolute mode: preserve control ball position when trapping
                Vector3 currentWorldPos = hitTransform.position;
                Quaternion currentWorldRot = hitTransform.rotation;
                
                hitTransform.parent = state.ControllerObject.transform;
                
                // Restore original world position to prevent jumping
                hitTransform.position = currentWorldPos;
                hitTransform.rotation = currentWorldRot;
                
                
                hitTransform.GetComponent<InitTransform>().setInTrapped(true);

                if (ROSStatusDisplay.Instance.IsROSConnected() && ROSStatusDisplay.Instance.IsTeleoperationOn())
                {
                    state.IsPosePublishing = true;
                }
                break;
            }
        }
    }

    private void ReleaseChildren(ControllerState state)
    {
        if (isRelativePoseControl)
        {
            // Only release if this controller is currently publishing
            if (state.IsPosePublishing)
            {
                string targetTag = "";
                if (state.ControllerObject == rightController)
                    targetTag = "right_wrist_target_pose";
                else if (state.ControllerObject == leftController)
                    targetTag = "left_wrist_target_pose";
                
                if (!string.IsNullOrEmpty(targetTag))
                {
                    GameObject target = GameObject.FindWithTag(targetTag);
                    if (target != null)
                    {
                        target.GetComponent<InitTransform>().setInTrapped(false);
                        state.IsPosePublishing = false;
                        ClearControllerState(state); // Clear state when releasing
                    }
                }
            }
        }
        else
        {
            // Original absolute mode logic
            int childCount = state.ControllerObject.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = state.ControllerObject.transform.GetChild(i);
                if (targetPoses.Contains(child.tag))
                {
                    child.GetComponent<InitTransform>().setInTrapped(false);
                    child.parent = null;
                    state.IsPosePublishing = false;
                    ClearControllerState(state); // Clear state when releasing
                }
            }
        }
    }

    private void UpdateTriggerValues(ControllerType type, ControllerState state)
    {
        if (!state.GripperToggleMode)
        {
            state.TriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, state.Helper.m_controller);
            state.IsGripperPublishing = state.TriggerValue > 0.0f;
        }
        else
        {
            // TODO: fix bug; This part seems to be called repeatedly, making the gripper's motion look unnatural.
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, state.Helper.m_controller))
            {
                state.GripperToggleValue = 1f - state.GripperToggleValue;
                state.TriggerValue = state.GripperToggleValue;
                state.IsGripperPublishing = true;
            }
        }
    }
    private void PublishChildrenPoses(ControllerState state)
    {
        HashSet<string> publishedHandTags = new HashSet<string>();
        
        if (isRelativePoseControl)
        {
            // Publish pose for the specific ball controlled by this controller
            string targetTag = "";
            if (state.ControllerObject == rightController)
                targetTag = "right_wrist_target_pose";
            else if (state.ControllerObject == leftController)
                targetTag = "left_wrist_target_pose";
            
            if (!string.IsNullOrEmpty(targetTag))
            {
                GameObject target = GameObject.FindWithTag(targetTag);
                if (target != null && target.GetComponent<InitTransform>().getInTrapped())
                {
                    PublishTargetPose(target.transform, targetTag, state, publishedHandTags);
                }
            }
        }
        else
        {
            // Original absolute mode logic
            int childCount = state.ControllerObject.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = state.ControllerObject.transform.GetChild(i);
                string childTag = child.tag;
                if (targetPoses.Contains(childTag))
                {
                    PublishTargetPose(child, childTag, state, publishedHandTags);
                }
            }
        }
    }

    private void PublishTargetPose(Transform targetTransform, string tag, ControllerState state, HashSet<string> publishedHandTags)
    {
        PoseStampedMsg poseMsg = poseMsgPool[tag];
        PointMsg rosPosition = pointMsgPool[tag];
        QuaternionMsg rosRotation = quaternionMsgPool[tag];

        Vector3 worldPosition = targetTransform.position;
        Quaternion worldRotation = targetTransform.rotation;
        

        if (isRelativePoseControl)
        {
            // Calculate displacement from lock point
            Vector3 currentControllerWorldPos = state.ControllerObject.transform.position;
            Quaternion currentControllerWorldRot = state.ControllerObject.transform.rotation;
            
            // Calculate raw displacement in world coordinates
            Vector3 rawPositionDelta = currentControllerWorldPos - state.lockControllerWorldPosition;
            
            // Map HMD-relative displacement to robot-root-relative displacement
            Vector3 positionDelta = rawPositionDelta;
            if (hmdTransform != null && robotRoot != null)
            {
                // Get locked HMD directions (when trigger was pressed)
                Vector3 lockHmdForward = state.lockHmdWorldRotation * Vector3.forward;
                Vector3 lockHmdRight = state.lockHmdWorldRotation * Vector3.right;
                Vector3 lockHmdUp = state.lockHmdWorldRotation * Vector3.up;
                
                // Project displacement onto locked HMD axes to get user-relative displacement
                float forwardDisplacement = Vector3.Dot(rawPositionDelta, lockHmdForward);
                float rightDisplacement = Vector3.Dot(rawPositionDelta, lockHmdRight);
                float upDisplacement = Vector3.Dot(rawPositionDelta, lockHmdUp);
                
                // Map to robot root coordinate system using LOCKED robot orientation
                Vector3 lockRobotForward = state.lockRobotWorldRotation * Vector3.forward;
                Vector3 lockRobotRight = state.lockRobotWorldRotation * Vector3.right;
                Vector3 lockRobotUp = state.lockRobotWorldRotation * Vector3.up;
                
                positionDelta = lockRobotForward * forwardDisplacement + 
                               lockRobotRight * rightDisplacement + 
                               lockRobotUp * upDisplacement;
            }
            
            // Calculate rotation displacement
            Quaternion rotationDelta = currentControllerWorldRot * Quaternion.Inverse(state.lockControllerWorldRotation);
            
            // Apply displacement to locked target position and rotation
            worldPosition = state.lockTargetWorldPosition + positionDelta;
            worldRotation = rotationDelta * state.lockTargetWorldRotation;
        }
        // In absolute mode, use targetTransform position/rotation directly
        // The parent-child relationship handles the positioning automatically

        // Convert to robot-relative coordinate frame for ROS
        Vector3 localPosition;
        Quaternion localRotation;
        
        // Both modes need to publish robot-relative coordinates
        localPosition = robotRoot.transform.InverseTransformPoint(worldPosition);
        localRotation = Quaternion.Inverse(robotRoot.transform.rotation) * worldRotation;
        
        var fluPosition = localPosition.To<FLU>();
        rosPosition.x = fluPosition.x;
        rosPosition.y = fluPosition.y;
        rosPosition.z = fluPosition.z;

        var fluRotation = localRotation.To<FLU>();
        rosRotation.x = fluRotation.x;
        rosRotation.y = fluRotation.y;
        rosRotation.z = fluRotation.z;
        rosRotation.w = fluRotation.w;

        // Set appropriate frame_id based on control mode
        HeaderMsg currentHeader = new HeaderMsg();
        if (isRelativePoseControl)
        {
            currentHeader.frame_id = "base_link"; // Robot-relative frame
        }
        else
        {
            currentHeader.frame_id = "map"; // World frame for absolute mode
        }
        
        poseMsg.header = currentHeader;
        poseMsg.pose.position = rosPosition;
        poseMsg.pose.orientation = rosRotation;

        ros.Publish(tag, poseMsg);

        if (state.IsGripperPublishing && (tag == "right_wrist_target_pose" || tag == "left_wrist_target_pose"))
        {
            string handTag = tag == "right_wrist_target_pose" ? "right_hand" : "left_hand";
            if (!publishedHandTags.Contains(handTag))
            {
                Float32Msg handMsg = floatMsgPool[handTag];
                handMsg.data = state.TriggerValue;
                ros.Publish(handTag, handMsg);
                publishedHandTags.Add(handTag);
            }
        }
    }

    private PoseStampedMsg CreatePoseStampedMsg()
    {
        return new PoseStampedMsg
        {
            header = new HeaderMsg(),
            pose = new PoseMsg
            {
                position = new PointMsg(),
                orientation = new QuaternionMsg()
            }
        };
    }

    public void SendInitPose()
    {
        commandMsg.data = "pose/" + commandPoseList[0]; // 0: init_pose
        ros.Publish("/vr_command", commandMsg);
        Debug.Log("Sent init_pose request to ROS.");
    }
}
// Done
// TODO: fix others pose when controlled
// TODO: fix head and waist mover except joint-wise orientation
// TODO' fix error with teleoperation on off 
// TODO: fix head orientation
// TODO: add hand controller
// TODO: init pose button
// TODO: change gripper open direction
// TODO' stil slow ik and unsafe - add collision avoidance add elbow ik -> turn off collision check
// TODO: add movable robot object
// TODO' add dropdown menu UI for change mode

// Now
// TODO' fix bug, the control pose msg is wobbled when grasped the controller ball (important)
// TODO' fix bug, left and right controller's each gripper state is transfered by grasping . fix bug

// Next
// TODO' add speed scailing factor -> pending
// TODO' add body tracker
// TODO: add force react motion ?
// TODO' performance issue, need to switch the state of robot vis model
// TODO: trigger value is not update from real value, so it is weird when left and right controller is changed 

// pending
// TODO: change hand speed more slowly
// TODO' add pointcloud? image -> added but slow?
// TODO' add switch egocentric mode and perspective mode -> need to fix error and ui
// TODO' add trigger for rosbag * Done but need to change the UI more intuitively

