using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
public class InputManager : MonoBehaviour
{
    [SerializeField] private GameObject rightController;
    [SerializeField] private GameObject leftController;
    private readonly List<string> targetPoses = new List<string>(4);
    private readonly List<string> targetValues = new List<string>(2);
    
    private ROSConnection ros;
    private bool isInitPose = false;
    
    private float rightTriggerValue = 0.0f;
    private float leftTriggerValue = 0.0f;

    // 컨트롤러별 상태 분리
    private bool isPublishingRight = false;
    private bool isPublishingLeft = false;
    private OVRControllerHelper rightHelper;
    private OVRControllerHelper leftHelper;

    private readonly RaycastHit[] raycastHitsRight = new RaycastHit[10];
    private readonly RaycastHit[] raycastHitsLeft = new RaycastHit[10];
    private readonly Dictionary<string, PoseStampedMsg> poseMsgPool = new Dictionary<string, PoseStampedMsg>();
    private readonly Dictionary<string, Float32Msg> floatMsgPool = new Dictionary<string, Float32Msg>();
    private readonly Dictionary<string, PointMsg> pointMsgPool = new Dictionary<string, PointMsg>();
    private readonly Dictionary<string, QuaternionMsg> quaternionMsgPool = new Dictionary<string, QuaternionMsg>();
    private readonly StringMsg initPoseMsg = new StringMsg("init_pose");
    private HeaderMsg headerMsg;

    void Start()
    {
        targetPoses.AddRange(new[] { "right_wrist_target_pose", "left_wrist_target_pose", "head_target_pose", "waist_target_pose" });
        targetValues.AddRange(new[] { "right_hand", "left_hand" });

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
        
        ros.RegisterPublisher<StringMsg>("command_pose");
        headerMsg = new HeaderMsg { frame_id = "base_link" };

        // 컨트롤러별 헬퍼 초기화
        rightHelper = rightController.GetComponent<OVRControllerHelper>();
        leftHelper = leftController.GetComponent<OVRControllerHelper>();
    }

    void Update()
    {
        ControllerAttachedUpdate(rightController, ref isPublishingRight, rightHelper, raycastHitsRight);
        ControllerAttachedUpdate(leftController, ref isPublishingLeft, leftHelper, raycastHitsLeft);
    }

    void ControllerAttachedUpdate(GameObject controller, ref bool isPublishing, OVRControllerHelper controllerHelper, RaycastHit[] raycastHits)
    {
        if (!ROSStatusDisplay.Instance.IsTeleoperationOn())
        {
            ReleaseChildren(controller, ref isPublishing);
            return;
        }

        if (!isInitPose)
        {
            SendInitPose();
            isInitPose = true;
        }

        if (OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            SendInitPose();
        }

        if (controllerHelper == null) return;

        float handTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controllerHelper.m_controller);

        if (handTriggerValue > 0.5f)
        {
            HandleGrab(controller, controllerHelper, raycastHits, ref isPublishing);
        }
        else
        {
            ReleaseChildren(controller, ref isPublishing);
        }

        if (isPublishing)
        {
            PublishChildrenPoses(controller);
        }
    }

    private void HandleGrab(GameObject controller, OVRControllerHelper controllerHelper, RaycastHit[] raycastHits, ref bool isPublishing)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            controller.transform.position, 
            0.05f, 
            controller.transform.forward, 
            raycastHits
        );

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = raycastHits[i].collider.transform;
            string hitTag = hitTransform.tag;
            
            if (targetPoses.Contains(hitTag))
            {
                hitTransform.parent = controller.transform;
                hitTransform.GetComponent<InitTransform>().setInTrapped(true);
                
                if (ROSStatusDisplay.Instance.IsROSConnected() && ROSStatusDisplay.Instance.IsTeleoperationOn())
                {
                    isPublishing = true;
                    UpdateTriggerValues(hitTag, controllerHelper);
                }
                break;
            }
        }
    }

    private void ReleaseChildren(GameObject controller, ref bool isPublishing)
    {
        int childCount = controller.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = controller.transform.GetChild(i);
            if (targetPoses.Contains(child.tag))
            {
                child.GetComponent<InitTransform>().setInTrapped(false);
                child.parent = null;
                isPublishing = false;
            }
        }
    }

    private void UpdateTriggerValues(string tag, OVRControllerHelper controllerHelper)
    {
        if (tag == "right_wrist_target_pose")
        {
            rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controllerHelper.m_controller);
        }
        else if (tag == "left_wrist_target_pose")
        {
            leftTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controllerHelper.m_controller);
        }
    }


    private void PublishChildrenPoses(GameObject controller)
    {
        int childCount = controller.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = controller.transform.GetChild(i);
            string childTag = child.tag;
            if (targetPoses.Contains(childTag))
            {
                PublishTargetPose(child, childTag);
            }
        }
    }

    void PublishTargetPose(Transform targetTransform, string tag)
    {
        // 풀링된 객체 가져오기
        PoseStampedMsg poseMsg = poseMsgPool[tag];
        PointMsg rosPosition = pointMsgPool[tag];
        QuaternionMsg rosRotation = quaternionMsgPool[tag];

        // Vector3<FLU>를 직접 사용하지 않고, 변환된 값을 바로 대입
        var fluPosition = targetTransform.position.To<FLU>(); // Vector3<FLU> 반환
        rosPosition.x = fluPosition.x;
        rosPosition.y = fluPosition.y;
        rosPosition.z = fluPosition.z;

        var fluRotation = targetTransform.rotation.To<FLU>(); // Quaternion<FLU> 반환
        rosRotation.x = fluRotation.x;
        rosRotation.y = fluRotation.y;
        rosRotation.z = fluRotation.z;
        rosRotation.w = fluRotation.w;

        poseMsg.header = headerMsg;
        poseMsg.pose.position = rosPosition;
        poseMsg.pose.orientation = rosRotation;
        
        ros.Publish(tag, poseMsg);

        if (tag == "right_wrist_target_pose" || tag == "left_wrist_target_pose")
        {
            string handTag = tag == "right_wrist_target_pose" ? "right_hand" : "left_hand";
            Float32Msg handMsg = floatMsgPool[handTag];
            handMsg.data = tag == "right_wrist_target_pose" ? rightTriggerValue : leftTriggerValue;
            ros.Publish(handTag, handMsg);
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
        ros.Publish("command_pose", initPoseMsg);
        Debug.Log("Sent command_pose request to ROS.");
    }

// Done
// TODO: fix others pose when controlled
// TODO: fix head and waist mover except joint-wise orientation
// TODO' fix error with teleoperation on off 
// TODO: fix head orientation
// TODO: add hand controller
// TODO: init pose button
// TODO: change gripper open direction

// Now
// TODO' add pointcloud? image

// Next
// TODO' stil slow ik and unsafe - add collision avoidance add elbow ik
// TODO: add movable robot object
// TODO' add speed scailing factor
// TODO' add switch egocentric mode and perspective mode
// TODO' add body tracker
// TODO' add trigger for rosbag
// TODO' add dropdown menu UI for change mode

// pending
// TODO: change hand speed more slowly
}
