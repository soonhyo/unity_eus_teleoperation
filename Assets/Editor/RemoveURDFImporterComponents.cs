using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR

using Unity.Robotics.UrdfImporter;
using Unity.Robotics.UrdfImporter.Control;

public class RemoveURDFImporterComponents : MonoBehaviour
{
    [MenuItem("GameObject/Remove URDF-Importer Components", false, 0)]

    static void Execute(MenuCommand command)
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            RemoveChildComponents(obj.transform);
        }
    }

    private static void RemoveChildComponents(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            RemoveComponents(child);
            RemoveChildComponents(child);
        }
        RemoveComponents(t);
    }

    private static void RemoveComponents(Transform t)
    {
        // RemoveComponent<>(t);
        RemoveComponent<UrdfRobot>(t);
        RemoveComponent<Controller>(t);
        RemoveComponent<UrdfPlugins>(t);
        RemoveComponent<UrdfPlugin>(t);
        RemoveComponent<UrdfLink>(t);
        RemoveComponent<UrdfVisuals>(t);
        RemoveComponent<UrdfVisual>(t);
        RemoveComponent<UrdfCollisions>(t);
        RemoveComponent<UrdfCollision>(t);
        RemoveComponent<UrdfLink>(t);
        RemoveComponent<UrdfInertial>(t);
        RemoveComponent<UrdfJointFixed>(t);
        RemoveComponent<UrdfJointRevolute>(t);
        RemoveComponent<UrdfJointPrismatic>(t);
        RemoveComponent<UrdfJointContinuous>(t);

        // Must be last...
        RemoveComponent<ArticulationBody>(t);
    }

    private static void RemoveComponent<T>(Transform t) where T : Object
    {
        var s = t.GetComponent<T>();
        while (s != null)
        {
            DestroyImmediate(s);
            s = t.GetComponent<T>();
        }
    }
}

#endif