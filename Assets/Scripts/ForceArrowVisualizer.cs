using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;

public class ForceArrowVisualizer : MonoBehaviour
{
    private ROSConnection ros; // ROS connection instance
    public string topic; // ROS topic name (set in Inspector)
    public GameObject arrowInstance; // Arrow object instance (set in Inspector)
    public float scaleFactor = 1.0f; // Scaling factor for force magnitude
    public GameObject target; // Target GameObject to follow (set in Inspector)

    void Start()
    {
        // Initialize ROS connection
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<WrenchStampedMsg>(topic, ForceCallback);

        // Check if arrowInstance is set
        if (arrowInstance == null)
        {
            Debug.LogError("Arrow Instance is not set in the Inspector");
        }

        // Warn if target GameObject is not set
        if (target == null)
        {
            Debug.LogWarning("Target GameObject is not set in the Inspector. Arrow will not follow any object.");
        }
    }

    private void ForceCallback(WrenchStampedMsg msg)
    {
        if (arrowInstance == null || target == null) return;

        // Extract force vector from WrenchStamped message in FLU (ROS) convention
        Vector3 localForce = new Vector3(
            -(float)msg.wrench.force.y, // ROS Left (Y) -> Unity Right (X), inverted
            (float)msg.wrench.force.z,  // ROS Up (Z) -> Unity Up (Y)
            (float)msg.wrench.force.x   // ROS Forward (X) -> Unity Forward (Z)
        );

        // Convert the local force from target's local coordinate system to world coordinate system
        Vector3 worldForce = target.transform.TransformDirection(localForce);

        float magnitude = worldForce.magnitude;

        // Set arrow rotation based on world force direction, avoid errors when magnitude is near zero
        // if (magnitude > 0.001f)
        // {
        //     arrowInstance.transform.rotation = Quaternion.LookRotation(localForce);
        // }
        // arrowInstance.transform.rotation = Quaternion.LookRotation(worldForce);
        arrowInstance.transform.up = worldForce.normalized; // Align Y-axis with force direction

        // Set arrow length based on force magnitude
        Vector3 localScale = arrowInstance.transform.localScale;
        localScale.y = magnitude * scaleFactor;
        arrowInstance.transform.localScale = localScale;
        Debug.Log($"localscale: {localScale}");
        Debug.Log($"look ratation: {worldForce}");

        // Adjust position to offset from target, assuming arrowInstance's local Y is the length direction
        // Vector3 direction = worldForce.normalized;
        // float halfLength = (magnitude * scaleFactor) / 2f; // Half of the scaled length
        Vector3 offset = new Vector3(0.0f, 0.1f, 0.0f);
        arrowInstance.transform.position = target.transform.position + offset;
    }
}