using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Android;

public class LogUtility : MonoBehaviour
{
    private static LogUtility instance;
    public static LogUtility Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("LogUtility");
                instance = go.AddComponent<LogUtility>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
    private Thread logThread;
    private bool isRunning;
    private StreamWriter logWriter;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitLogger();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
#endif
        string sdcardPath = GetExternalStoragePath();
        string filePath = Path.Combine(sdcardPath, "user_log.txt");
        Debug.Log("Log will be saved to: " + filePath);
    }

    string GetExternalStoragePath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    using (var env = new AndroidJavaClass("android.os.Environment"))
    {
        return env.CallStatic<AndroidJavaObject>("getExternalStorageDirectory")
                  .Call<string>("getAbsolutePath");
    }
#else
        return Application.persistentDataPath;
#endif
    }

    private void InitLogger()
    {
        logWriter = new StreamWriter(GetExternalStoragePath(), true);
        isRunning = true;

        Application.logMessageReceived += HandleLog;

        logThread = new Thread(ProcessLogQueue);
        logThread.IsBackground = true;
        logThread.Start();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
        isRunning = false;
        logThread?.Join();

        if (logWriter != null)
        {
            logWriter.Flush();
            logWriter.Close();
            logWriter = null;
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {logString}";
        if (type == LogType.Error || type == LogType.Exception)
        {
            logEntry += $"\n{stackTrace}";  
        }
        logQueue.Enqueue(logEntry);
    }

    private void ProcessLogQueue()
    {
        while (isRunning)
        {
            while (logQueue.TryDequeue(out string logEntry))
            {
                try
                {
                    logWriter.WriteLine(logEntry);
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to write log to file: " + e.Message);
                }
            }
            logWriter.Flush();
            Thread.Sleep(200); // Пауза, чтоб не загружать поток CPU
        }
    }
}
