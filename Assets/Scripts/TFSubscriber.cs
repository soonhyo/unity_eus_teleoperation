using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;

public class TFSubscriber : MonoBehaviour
{
    public string tfTopic = "/tf";
    [SerializeField] private GameObject m_Robot;

    private ROSConnection ros;
    private Dictionary<string, Transform> jointTransforms = new Dictionary<string, Transform>();
    private Dictionary<string, Vector3> targetPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> targetRotations = new Dictionary<string, Quaternion>();
    private bool isPoseInitialized;

    private int tfCount = 0;
    private float tfTimer = 0;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TFMessageMsg>(tfTopic, UpdateURDFTransforms);

        foreach (Transform child in m_Robot.GetComponentsInChildren<Transform>())
        {
            jointTransforms[child.name] = child;
        }
    }

    void UpdateURDFTransforms(TFMessageMsg tfMessage)
    {
        tfCount++;
        tfTimer += Time.deltaTime;

        if (tfTimer > 1.0)
        {
            Debug.Log("TF HZ: " + tfCount);
            tfCount = 0;
            tfTimer = 0;
        }

        foreach (TransformStampedMsg tf in tfMessage.transforms)
        {
            if (jointTransforms.TryGetValue(tf.child_frame_id, out Transform jointTransform))
            {
                targetPositions[tf.child_frame_id] = tf.transform.translation.From<FLU>();
                targetRotations[tf.child_frame_id] = tf.transform.rotation.From<FLU>();
            }
        }
    }

    void FixedUpdate()
    {
        
        foreach (var joint in jointTransforms)
        {
            if (targetPositions.TryGetValue(joint.Key, out Vector3 targetPos) &&
                targetRotations.TryGetValue(joint.Key, out Quaternion targetRot))
            {
                Rigidbody rb = joint.Value.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.MovePosition(targetPos);
                    rb.MoveRotation(targetRot);
                }
                else
                {
                    joint.Value.localPosition = targetPos;
                    joint.Value.localRotation = targetRot;
                }
            }
        }
        if (!isPoseInitialized){
            isPoseInitialized = true;
        }
    }   

    public bool IsPoseInitialized()
    {
        return isPoseInitialized;
    }
}
