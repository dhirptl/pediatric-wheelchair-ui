using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The telemetry the Smart Guide backend cares about, written to JSON so the real
/// LIDAR/robot backend can consume (or replace) it later. Each follow session
/// records a session id, which target the child chose, how long the follow lasted,
/// and the distance to that target sampled over time.
///
/// IMPORTANT - the distance log is *throttled*, not per-frame. At 60-120 fps a
/// 2-minute follow would otherwise dump 7k-14k samples, bloating the file and
/// wasting CPU on the Jetson. SmartGuideController updates its on-screen label every
/// frame but only calls Sample() about once a second (logInterval).
/// </summary>
public class FollowAssistLog
{
    // Matches the research schema in the Smart Guide TDD §4.
    [Serializable]
    public class Session
    {
        public string sessionID;            // timestamp id for this follow
        public string selectedTarget;       // Caregiver / WallCorridor / Doorway
        public float durationSeconds;       // total time spent following
        public bool completed;              // true = reached target, false = cancelled
        public List<float> distanceSamples = new List<float>(); // meters, ~1 Hz
    }

    [Serializable]
    private class LogFile
    {
        public List<Session> sessions = new List<Session>();
    }

    private const string FileName = "follow_assist_log.json";

    private Session current;
    private float startTime;

    public bool IsActive => current != null;

    /// <summary>The path the JSON log is written to (under the app's persistent data dir).</summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Begin recording a new follow session for the chosen target.</summary>
    public void BeginFollow(SmartGuideTarget target)
    {
        if (target == null) return;
        current = new Session
        {
            sessionID = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
            selectedTarget = target.type.ToString(),
            completed = false,
        };
        startTime = Time.time;
    }

    /// <summary>
    /// Record one distance reading. Caller is responsible for throttling - this is
    /// expected to be invoked about once a second, not every frame.
    /// </summary>
    public void Sample(float distance)
    {
        if (current == null) return;
        current.distanceSamples.Add(distance);
    }

    /// <summary>End the current session and flush it to the JSON log on disk.</summary>
    public void EndFollow(bool completed)
    {
        if (current == null) return;
        current.durationSeconds = Time.time - startTime;
        current.completed = completed;
        Persist(current);
        current = null;
    }

    private static void Persist(Session session)
    {
        try
        {
            LogFile file = Load();
            file.sessions.Add(session);
            File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
            GamePrefs.SetString(GamePrefs.FollowAssistLog, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FollowAssistLog] Could not write log: " + e.Message);
        }
    }

    private static LogFile Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                if (!string.IsNullOrEmpty(json))
                    return JsonUtility.FromJson<LogFile>(json) ?? new LogFile();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FollowAssistLog] Could not read existing log: " + e.Message);
        }
        return new LogFile();
    }
}
