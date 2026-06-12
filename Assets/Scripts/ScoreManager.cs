using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pediatric points economy. Pickup is a squared-distance check against the
/// bridge's pose event - NO physics triggers - so coin collection keeps working
/// unchanged when poses come from the robot's /odom topic. Points persist in
/// PlayerPrefs and fund the Theme Shop.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Tooltip("Meters within which a coin is collected.")]
    public float pickupRadius = 1.5f;
    [Tooltip("HUD points label (TopBar/Txt_Points).")]
    public TMPro.TextMeshProUGUI pointsLabel;

    [Header("Sound")]
    [Tooltip("Sparkly chime on coin collection (one central source, no per-coin audio).")]
    public AudioClip collectClip;
    [Range(0f, 1f)] public float collectVolume = 0.7f;

    private AudioSource sfx;

    public int CurrentPoints { get; private set; }
    public event Action<int> OnPointsChanged;

    private readonly List<Coin> activeCoins = new List<Coin>(64);

    void Awake()
    {
        Instance = this;
        CurrentPoints = GamePrefs.GetInt(GamePrefs.CurrentPoints);

        sfx = GetComponent<AudioSource>();
        if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;          // 2D UI sound
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        WheelchairStateBridge.OnWheelchairPoseUpdated += HandlePose;
    }

    void OnDisable()
    {
        WheelchairStateBridge.OnWheelchairPoseUpdated -= HandlePose;
    }

    void Start()
    {
        UpdateLabel();
    }

    public void Register(Coin coin)
    {
        if (coin != null && !activeCoins.Contains(coin)) activeCoins.Add(coin);
    }

    private void HandlePose(Vector3 pose)
    {
        float r2 = pickupRadius * pickupRadius;
        for (int i = activeCoins.Count - 1; i >= 0; i--)
        {
            Coin coin = activeCoins[i];
            if (coin == null || !coin.IsActive)
            {
                activeCoins.RemoveAt(i);
                continue;
            }
            if ((coin.Position - pose).sqrMagnitude <= r2)
            {
                coin.Collect();
                activeCoins.RemoveAt(i);
                if (collectClip != null) sfx.PlayOneShot(collectClip, collectVolume);
                AddPoints(1);
            }
        }
    }

    public void AddPoints(int amount)
    {
        CurrentPoints += amount;
        GamePrefs.SetInt(GamePrefs.CurrentPoints, CurrentPoints);
        OnPointsChanged?.Invoke(CurrentPoints);
        UpdateLabel();
    }

    /// <summary>Deducts points if affordable (Theme Shop purchases).</summary>
    public bool TrySpend(int cost)
    {
        if (CurrentPoints < cost) return false;
        CurrentPoints -= cost;
        GamePrefs.SetInt(GamePrefs.CurrentPoints, CurrentPoints);
        OnPointsChanged?.Invoke(CurrentPoints);
        UpdateLabel();
        return true;
    }

    private void UpdateLabel()
    {
        if (pointsLabel != null) pointsLabel.text = "PTS: " + CurrentPoints;
    }
}
