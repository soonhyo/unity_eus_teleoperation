using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;

public class Sciurus17TFSubscriber : MonoBehaviour
{
    public static Sciurus17TFSubscriber Instance { get; private set; }
    public string tfTopic = "/tf";
    [SerializeField] private GameObject m_Sciurus17;

    private ROSConnection ros;
    private Dictionary<string, Transform> jointTransforms = new Dictionary<string, Transform>();
    private Dictionary<string, Vector3> targetPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> targetRotations = new Dictionary<string, Quaternion>();
    private bool isPoseInitialized;

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

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TFMessageMsg>(tfTopic, UpdateURDFTransforms);

        foreach (Transform child in m_Sciurus17.GetComponentsInChildren<Transform>())
        {
            jointTransforms[child.name] = child;
        }
    }
    void UpdateURDFTransforms(TFMessageMsg tfMessage)
    {
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
