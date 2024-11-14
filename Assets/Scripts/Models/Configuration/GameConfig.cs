using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "DoggoNogo/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Trial Settings")]
    [SerializeField] private int defaultTrialCount = 60;
    [SerializeField] private int minTrialsPerLevel = 10;
    [SerializeField] private Vector2 isiRange = new(1f, 4f);

    [Header("Reaction Time Settings")]
    [SerializeField] private float minReactionTime = 0.15f;
    [SerializeField] private float maxReactionTime = 0.6f;
    [SerializeField] private float initialMedianRT = 0.375f; // (maxRT + minRT) / 2

    [Header("Scoring Settings")]
    [SerializeField] private int minScore = 100;
    [SerializeField] private int maxScore = 200;
    [SerializeField] private int penaltyScore = -100;

    // Public accessors
    public int DefaultTrialCount => defaultTrialCount;
    public int MinTrialsPerLevel => minTrialsPerLevel;
    public Vector2 ISIRange => isiRange;
    public float MinReactionTime => minReactionTime;
    public float MaxReactionTime => maxReactionTime;
    public float InitialMedianRT => initialMedianRT;
    public int MinScore => minScore;
    public int MaxScore => maxScore;
    public int PenaltyScore => penaltyScore;
}

/*
To create and use this in Unity:
1. Right-click in Project window
2. Select Create > DoggoNogo > GameConfig
3. Name it something like "DefaultGameConfig"
4. Modify values in Inspector
5. Drag into your GameController component reference
*/