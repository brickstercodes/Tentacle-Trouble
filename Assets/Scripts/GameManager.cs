using UnityEngine;
using System;

namespace Octo
{
    /// <summary>
    /// Central game state: score, stopwatch, best time.
    /// Singleton — lives on any GameObject in the scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField] private int pointsPerCoin = 100;
        [SerializeField] private int totalCoinsInLevel = 6;

        private int score;
        private float elapsed;
        private float bestTime;
        private bool running;
        private bool finished;

        private const string BestTimeKey = "BestTime";

        public int Score => score;
        public float Elapsed => elapsed;
        public float BestTime => bestTime;
        public bool IsRunning => running;
        public bool IsFinished => finished;
        public int TotalCoins => totalCoinsInLevel;
        public int CoinsCollected => score / Mathf.Max(pointsPerCoin, 1);

        public event Action<int> OnScoreChanged;
        public event Action OnLevelComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            bestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
        }

        private void Start()
        {
            StartTimer();
        }

        private void Update()
        {
            if (running && !finished)
                elapsed += Time.deltaTime;
        }

        /// <summary>Start the stopwatch (call when first coin is grabbed or game starts).</summary>
        public void StartTimer()
        {
            if (running) return;
            running = true;
            elapsed = 0f;
        }

        public void AddCoinScore()
        {
            if (!running) StartTimer();

            score += pointsPerCoin;
            OnScoreChanged?.Invoke(score);

            if (CoinsCollected >= totalCoinsInLevel)
                CompletLevel();
        }

        private void CompletLevel()
        {
            finished = true;
            running = false;

            if (bestTime <= 0f || elapsed < bestTime)
            {
                bestTime = elapsed;
                PlayerPrefs.SetFloat(BestTimeKey, bestTime);
                PlayerPrefs.Save();
            }

            OnLevelComplete?.Invoke();
            Debug.Log($"[GameManager] Level complete! Time: {elapsed:F1}s  Best: {bestTime:F1}s");
        }

        public void ResetGame()
        {
            score = 0;
            elapsed = 0f;
            running = false;
            finished = false;
            OnScoreChanged?.Invoke(score);
        }
    }
}
