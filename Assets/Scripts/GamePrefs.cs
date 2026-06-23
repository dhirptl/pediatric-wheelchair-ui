using UnityEngine;

/// <summary>
/// Centralized PlayerPrefs keys and helpers so persistence keys aren't
/// string-littered across scripts. JSON helpers use JsonUtility for the
/// structured payloads (room calibration, owned themes).
/// </summary>
public static class GamePrefs
{
    public const string Destination = "Destination";
    public const string GameMode = "GameMode";
    public const string CurrentPoints = "CurrentPoints";
    public const string RoomCalibration = "RoomCalibration";
    public const string OwnedThemes = "OwnedThemes";
    public const string EquippedTheme = "EquippedTheme";
    public const string FollowAssistLog = "FollowAssistLog";
    public const string ViewMode = "ViewMode";

    public static int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);

    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

    public static string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(key, defaultValue);

    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }

    public static void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

    public static T GetJson<T>(string key) where T : class
    {
        string json = PlayerPrefs.GetString(key, "");
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json);
    }

    public static void SetJson<T>(string key, T value) where T : class
    {
        SetString(key, JsonUtility.ToJson(value));
    }
}
