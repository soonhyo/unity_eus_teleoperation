using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.UI;

public class LidarManager : MonoBehaviour
{
    public LidarDrawer rgbdDrawer;

    public string rgbdTopic;

    private ROSConnection ros;


    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        rgbdDrawer.topic = rgbdTopic;

        OnRGBDTopic(rgbdTopic);
    }

    public void PopulateTopics(Dictionary<string, string> topics)
    {
        List<string> topicList = new List<string>();
        topicList.Add("None");
        foreach (KeyValuePair<string, string> topic in topics)
        {
            if (topic.Value == "sensor_msgs/PointCloud2")
                topicList.Add(topic.Key);
        }
        
        if(topicList.Count == 1)
        {
            Debug.LogWarning("No PointCloud2 topics found!");
            return;
        } else
        {
            Debug.Log("Found " + (topicList.Count - 1) + " PointCloud2 topics: " + string.Join(", ", topicList.GetRange(1, topicList.Count - 1).ToArray()));
        }
    }

    
    public void OnRGBDTopic(string topic)
    {
        rgbdTopic = topic;
        rgbdDrawer._enabled = true;
        rgbdDrawer.OnTopicChange(rgbdTopic);
        PlayerPrefs.SetString("rgbdTopic", rgbdTopic);
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        rgbdDrawer.enabled = false;
    }

    public void RGBD()
    {
        rgbdDrawer.ToggleEnabled();
    }

}
