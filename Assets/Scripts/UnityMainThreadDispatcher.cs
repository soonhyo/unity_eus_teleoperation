using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            GameObject obj = new GameObject("UnityMainThreadDispatcher");
            _instance = obj.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }
        return _instance;
    }

    private Queue<System.Action> _executionQueue = new Queue<System.Action>();

    public void Enqueue(System.Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        // 메인 스레드에서 작업을 실행
        while (_executionQueue.Count > 0)
        {
            System.Action action = null;
            lock (_executionQueue)
            {
                action = _executionQueue.Dequeue();
            }
            action?.Invoke();
        }
    }
}
