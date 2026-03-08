using UnityEngine;
using UnityEngine.UI;

namespace Octo.UI
{
    /// <summary>
    /// In-game HUD: stopwatch, best time, score, completion banner.
    /// Entirely code-driven — no prefab or TMP font asset needed.
    /// Attach to any GameObject (e.g. GameManager).
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Style")]
        [SerializeField] private int fontSize = 32;

        private Text timerText;
        private Text bestText;
        private Text scoreText;
        private Text bannerText;
        private Outline scoreOutline;

        private float scoreFlashTimer;

        private readonly Color textColor = Color.white;
        private readonly Color bestTimeColor = new Color(1f, 0.84f, 0f);
        private readonly Color scoreFlashColor = new Color(1f, 0.95f, 0.4f);

        private void Start()
        {
            BuildCanvas();

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnScoreChanged += OnScoreChanged;
                gm.OnLevelComplete += OnLevelComplete;
            }
            else
            {
                Debug.LogWarning("[GameHUD] GameManager.Instance is null — add GameManager to the scene.");
            }
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnScoreChanged -= OnScoreChanged;
                gm.OnLevelComplete -= OnLevelComplete;
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            float t = gm.Elapsed;
            int mins = (int)(t / 60f);
            float secs = t % 60f;
            timerText.text = $"{mins:00}:{secs:00.00}";

            if (gm.BestTime > 0f)
            {
                float bt = gm.BestTime;
                int bm = (int)(bt / 60f);
                float bs = bt % 60f;
                bestText.text = $"BEST  {bm:00}:{bs:00.00}";
            }
            else
            {
                bestText.text = "BEST  --:--.--";
            }

            if (scoreFlashTimer > 0f)
            {
                scoreFlashTimer -= Time.deltaTime;
                scoreText.color = Color.Lerp(textColor, scoreFlashColor, scoreFlashTimer / 0.4f);
            }
        }

        private void OnScoreChanged(int newScore)
        {
            scoreText.text = $"SCORE  {newScore}";
            scoreFlashTimer = 0.4f;
            scoreText.color = scoreFlashColor;
        }

        private void OnLevelComplete()
        {
            var gm = GameManager.Instance;
            float t = gm.Elapsed;
            int mins = (int)(t / 60f);
            float secs = t % 60f;
            bool isNewBest = Mathf.Approximately(gm.BestTime, t);
            bannerText.text = isNewBest
                ? $"ALL COINS!  {mins:00}:{secs:00.00}  NEW BEST!"
                : $"ALL COINS!  {mins:00}:{secs:00.00}";
            bannerText.gameObject.SetActive(true);
        }
//  fix
        private void BuildCanvas()
        {
            var canvasGo = new GameObject("GameHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            timerText = MakeLabel(canvasGo.transform, "Timer",
                TextAnchor.UpperLeft, new Vector2(30, -20), fontSize + 8, textColor);
            timerText.text = "00:00.00";

            bestText = MakeLabel(canvasGo.transform, "BestTime",
                TextAnchor.UpperLeft, new Vector2(30, -70), fontSize - 4, bestTimeColor);
            bestText.text = "BEST  --:--.--";

            scoreText = MakeLabel(canvasGo.transform, "Score",
                TextAnchor.UpperRight, new Vector2(-30, -20), fontSize + 4, textColor);
            scoreText.text = "SCORE  0";

            bannerText = MakeLabel(canvasGo.transform, "Banner",
                TextAnchor.UpperCenter, new Vector2(0, -200), fontSize + 16, bestTimeColor);
            bannerText.text = "";
            bannerText.gameObject.SetActive(false);

            Debug.Log("[GameHUD] Canvas built successfully.");
        }

        private Text MakeLabel(Transform parent, string name,
            TextAnchor anchor, Vector2 offset, int size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            // Try built-in Arial first, fall back to OS font
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            if (text.font == null)
                Debug.LogError($"[GameHUD] No font found for label '{name}'!");
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(2, -2);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(3, -3);

            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(500, 60);

            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    text.alignment = TextAnchor.UpperLeft;
                    break;
                case TextAnchor.UpperRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1);
                    text.alignment = TextAnchor.UpperRight;
                    break;
                case TextAnchor.UpperCenter:
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.sizeDelta = new Vector2(800, 80);
                    text.alignment = TextAnchor.UpperCenter;
                    break;
            }

            rect.anchoredPosition = offset;
            return text;
        }
    }
}
