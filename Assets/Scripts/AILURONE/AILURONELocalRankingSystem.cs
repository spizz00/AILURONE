using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AILURONE.Ranking
{
    public static class AILURONELocalRanking
    {
        private const string FileName = "ailurone_local_ranking.json";
        private const int MaximumEntries = 100;

        [Serializable]
        public sealed class Entry
        {
            public string username;
            public int score;
        }

        [Serializable]
        private sealed class RankingData
        {
            public string currentUsername = string.Empty;
            public List<Entry> entries = new List<Entry>();
        }

        private static RankingData _data;

        public static string CurrentUsername
        {
            get
            {
                EnsureLoaded();
                return _data.currentUsername;
            }
        }

        public static string SanitizeUsername(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] source = value.Trim().ToCharArray();
            char[] result = new char[Mathf.Min(16, source.Length)];
            int length = 0;
            for (int index = 0; index < source.Length && length < result.Length; index++)
            {
                char character = source[index];
                if (char.IsLetterOrDigit(character) ||
                    character == '_' || character == '-')
                {
                    result[length++] = character;
                }
            }
            return new string(result, 0, length);
        }

        public static void SetCurrentUsername(string username)
        {
            EnsureLoaded();
            string sanitized = SanitizeUsername(username);
            if (string.IsNullOrEmpty(sanitized))
            {
                throw new ArgumentException("Username is required.");
            }
            _data.currentUsername = sanitized;
            Save();
        }

        public static int SubmitScore(int score)
        {
            EnsureLoaded();
            string username = SanitizeUsername(_data.currentUsername);
            if (string.IsNullOrEmpty(username))
            {
                username = "OPERATOR";
                _data.currentUsername = username;
            }

            Entry match = null;
            for (int index = 0; index < _data.entries.Count; index++)
            {
                if (string.Equals(
                    _data.entries[index].username,
                    username,
                    StringComparison.OrdinalIgnoreCase))
                {
                    match = _data.entries[index];
                    break;
                }
            }

            if (match == null)
            {
                match = new Entry { username = username, score = score };
                _data.entries.Add(match);
            }
            else
            {
                match.username = username;
                match.score = Mathf.Max(match.score, score);
            }

            SortEntries(_data.entries);
            if (_data.entries.Count > MaximumEntries)
            {
                _data.entries.RemoveRange(
                    MaximumEntries,
                    _data.entries.Count - MaximumEntries);
            }
            Save();
            return GetRank(username);
        }

        public static List<Entry> GetTopEntries(int count)
        {
            EnsureLoaded();
            SortEntries(_data.entries);
            int length = Mathf.Min(Mathf.Max(0, count), _data.entries.Count);
            List<Entry> copy = new List<Entry>(length);
            for (int index = 0; index < length; index++)
            {
                Entry entry = _data.entries[index];
                copy.Add(new Entry
                {
                    username = entry.username,
                    score = entry.score
                });
            }
            return copy;
        }

        public static int GetRank(string username)
        {
            EnsureLoaded();
            SortEntries(_data.entries);
            for (int index = 0; index < _data.entries.Count; index++)
            {
                if (string.Equals(
                    _data.entries[index].username,
                    username,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1;
                }
            }
            return 0;
        }

        private static void EnsureLoaded()
        {
            if (_data != null)
            {
                return;
            }

            string path = Path.Combine(Application.persistentDataPath, FileName);
            try
            {
                _data = File.Exists(path)
                    ? JsonUtility.FromJson<RankingData>(File.ReadAllText(path))
                    : new RankingData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Local Ranking] Invalid save was ignored: " +
                    exception.Message);
                _data = new RankingData();
            }

            if (_data == null)
            {
                _data = new RankingData();
            }
            if (_data.entries == null)
            {
                _data.entries = new List<Entry>();
            }
        }

        private static void Save()
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(_data, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Local Ranking] Save failed: " + exception.Message);
            }
        }

        private static void SortEntries(List<Entry> entries)
        {
            entries.Sort((left, right) =>
            {
                int scoreOrder = right.score.CompareTo(left.score);
                return scoreOrder != 0
                    ? scoreOrder
                    : string.Compare(
                        left.username,
                        right.username,
                        StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    public sealed class AILURONEUsernamePrompt : MonoBehaviour
    {
        private TMP_InputField _input;
        private TMP_Text _status;
        private Action _accepted;

        public static void Show(Action accepted)
        {
            AILURONEUsernamePrompt existing =
                FindAnyObjectByType<AILURONEUsernamePrompt>();
            if (existing != null)
            {
                existing._accepted = accepted;
                existing._input.Select();
                return;
            }

            GameObject root = new GameObject(
                "AILURONE_UsernamePrompt",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(AILURONEUsernamePrompt));
            AILURONEUsernamePrompt prompt =
                root.GetComponent<AILURONEUsernamePrompt>();
            prompt._accepted = accepted;
        }

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            BuildInterface();
        }

        private void Start()
        {
            _input.text = AILURONELocalRanking.CurrentUsername;
            _input.Select();
            _input.ActivateInputField();
        }

        private void BuildInterface()
        {
            TMP_FontAsset font = RankingUI.FindPreferredFont();
            Image dimmer = RankingUI.CreateImage(
                transform,
                "Dimmer",
                new Color(0.01f, 0.015f, 0.025f, 0.90f));
            RankingUI.Stretch(dimmer.rectTransform);

            Image panel = RankingUI.CreateImage(
                dimmer.transform,
                "IdentityPanel",
                new Color(0.035f, 0.05f, 0.065f, 0.98f));
            RankingUI.SetRect(
                panel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(720f, 330f),
                Vector2.zero);

            Image accent = RankingUI.CreateImage(
                panel.transform,
                "Accent",
                new Color(0.12f, 0.88f, 1f, 1f));
            RankingUI.SetRect(
                accent.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(7f, 330f),
                new Vector2(3.5f, 0f));

            RankingUI.CreateText(
                panel.transform,
                "Title",
                "IDENTIFY OPERATOR",
                font,
                42f,
                TextAlignmentOptions.Left,
                Color.white,
                new Vector2(0.5f, 1f),
                new Vector2(620f, 70f),
                new Vector2(0f, -55f));

            RankingUI.CreateText(
                panel.transform,
                "Instruction",
                "ENTER A LOCAL USERNAME  /  1-16 CHARACTERS",
                font,
                20f,
                TextAlignmentOptions.Left,
                new Color(0.55f, 0.72f, 0.78f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(620f, 42f),
                new Vector2(0f, -112f));

            Image inputBackground = RankingUI.CreateImage(
                panel.transform,
                "Input",
                new Color(0.01f, 0.015f, 0.025f, 1f));
            inputBackground.raycastTarget = true;
            RankingUI.SetRect(
                inputBackground.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(620f, 70f),
                new Vector2(0f, 2f));

            GameObject inputObject = inputBackground.gameObject;
            _input = inputObject.AddComponent<TMP_InputField>();
            _input.characterLimit = 16;
            _input.lineType = TMP_InputField.LineType.SingleLine;

            TMP_Text inputText = RankingUI.CreateText(
                inputBackground.transform,
                "Text",
                string.Empty,
                font,
                31f,
                TextAlignmentOptions.Left,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(570f, 58f),
                Vector2.zero);
            TMP_Text placeholder = RankingUI.CreateText(
                inputBackground.transform,
                "Placeholder",
                "USERNAME",
                font,
                31f,
                TextAlignmentOptions.Left,
                new Color(1f, 1f, 1f, 0.25f),
                new Vector2(0.5f, 0.5f),
                new Vector2(570f, 58f),
                Vector2.zero);
            _input.textComponent = inputText;
            _input.placeholder = placeholder;
            _input.onSubmit.AddListener(_ => Accept());

            _status = RankingUI.CreateText(
                panel.transform,
                "Status",
                string.Empty,
                font,
                17f,
                TextAlignmentOptions.Left,
                new Color(1f, 0.32f, 0.24f, 1f),
                new Vector2(0.5f, 0f),
                new Vector2(400f, 42f),
                new Vector2(-110f, 51f));

            Button confirm = RankingUI.CreateButton(
                panel.transform,
                "Confirm",
                "CONFIRM",
                font,
                new Vector2(190f, 54f),
                new Vector2(215f, -112f));
            confirm.onClick.AddListener(Accept);
        }

        private void Accept()
        {
            string username = AILURONELocalRanking.SanitizeUsername(_input.text);
            if (string.IsNullOrEmpty(username))
            {
                _status.text = "USERNAME REQUIRED";
                _input.Select();
                return;
            }

            AILURONELocalRanking.SetCurrentUsername(username);
            Action callback = _accepted;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }
    }

    public enum LevelCompleteChoice
    {
        None,
        MainMenu,
        Restart,
        Quit
    }

    public sealed class AILURONELevelCompleteRankingScreen : MonoBehaviour
    {
        public LevelCompleteChoice Choice { get; private set; }

        private int _score;
        private float _elapsedTime;
        private int _rank;
        private readonly List<ButtonChoiceBinding> _buttonBindings =
            new List<ButtonChoiceBinding>();

        private sealed class ButtonChoiceBinding
        {
            public Button button;
            public LevelCompleteChoice choice;
        }

        public static AILURONELevelCompleteRankingScreen Show(
            int score,
            float elapsedTime)
        {
            GameObject root = new GameObject(
                "AILURONE_LevelCompleteRanking",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(AILURONELevelCompleteRankingScreen));
            AILURONELevelCompleteRankingScreen screen =
                root.GetComponent<AILURONELevelCompleteRankingScreen>();
            screen._score = score;
            screen._elapsedTime = elapsedTime;
            screen._rank = AILURONELocalRanking.SubmitScore(score);
            screen.BuildInterface();
            return screen;
        }

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;
            EnsureRankingEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LateUpdate()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Choice != LevelCompleteChoice.None ||
                Mouse.current == null ||
                !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            for (int index = 0; index < _buttonBindings.Count; index++)
            {
                ButtonChoiceBinding binding = _buttonBindings[index];
                if (binding.button != null && binding.button.interactable &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        binding.button.transform as RectTransform,
                        pointer,
                        null))
                {
                    Choice = binding.choice;
                    break;
                }
            }
        }

        private void BuildInterface()
        {
            TMP_FontAsset font = RankingUI.FindPreferredFont();
            Image background = RankingUI.CreateImage(
                transform,
                "Background",
                new Color(0.006f, 0.009f, 0.014f, 0.985f));
            RankingUI.Stretch(background.rectTransform);

            Image frame = RankingUI.CreateImage(
                background.transform,
                "ResultFrame",
                new Color(0.025f, 0.037f, 0.050f, 0.985f));
            RankingUI.SetRect(
                frame.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(1720f, 900f),
                Vector2.zero);

            Image topAccent = RankingUI.CreateImage(
                frame.transform,
                "TopAccent",
                new Color(0.12f, 0.88f, 1f, 0.95f));
            RankingUI.SetRect(
                topAccent.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(1720f, 5f),
                new Vector2(0f, -2.5f));

            Image divider = RankingUI.CreateImage(
                frame.transform,
                "ColumnDivider",
                new Color(0.12f, 0.88f, 1f, 0.72f));
            RankingUI.SetRect(
                divider.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(3f, 800f),
                new Vector2(225f, 0f));

            RankingUI.CreateText(
                frame.transform,
                "CompleteTitle",
                "LEVEL  //  COMPLETE",
                font,
                62f,
                TextAlignmentOptions.Left,
                Color.white,
                new Vector2(0.5f, 1f),
                new Vector2(970f, 88f),
                new Vector2(-325f, -64f));

            Image stageStrip = RankingUI.CreateImage(
                frame.transform,
                "StageStrip",
                new Color(0.065f, 0.090f, 0.112f, 1f));
            RankingUI.SetRect(
                stageStrip.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(970f, 52f),
                new Vector2(-325f, -142f));

            RankingUI.CreateText(
                stageStrip.transform,
                "Text",
                "01  //  FINAL ZONE        CONVERGENCE APERTURE",
                font,
                22f,
                TextAlignmentOptions.Left,
                new Color(0.16f, 0.92f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(920f, 48f),
                Vector2.zero);

            string timeText = TimeSpan.FromSeconds(_elapsedTime)
                .ToString(@"mm\:ss\.ff");
            Image timeBlock = RankingUI.CreateImage(
                frame.transform,
                "TimeBlock",
                new Color(0.040f, 0.057f, 0.072f, 0.96f));
            RankingUI.SetRect(
                timeBlock.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(970f, 118f),
                new Vector2(-325f, -235f));
            RankingUI.CreateText(
                timeBlock.transform,
                "TimeLabel",
                "COMPLETION TIME",
                font,
                20f,
                TextAlignmentOptions.Left,
                new Color(0.55f, 0.68f, 0.73f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, 100f),
                new Vector2(-270f, 0f));
            RankingUI.CreateText(
                timeBlock.transform,
                "TimeValue",
                timeText,
                font,
                58f,
                TextAlignmentOptions.Right,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(500f, 100f),
                new Vector2(205f, 0f));

            Image scoreBlock = RankingUI.CreateImage(
                frame.transform,
                "ScoreBlock",
                new Color(0.050f, 0.070f, 0.087f, 0.98f));
            RankingUI.SetRect(
                scoreBlock.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(600f, 230f),
                new Vector2(-510f, -10f));
            Image scoreAccent = RankingUI.CreateImage(
                scoreBlock.transform,
                "Accent",
                new Color(0.12f, 0.88f, 1f, 0.95f));
            RankingUI.SetRect(
                scoreAccent.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(7f, 230f),
                new Vector2(3.5f, 0f));

            RankingUI.CreateText(
                scoreBlock.transform,
                "ScoreLabel",
                "FINAL SCORE",
                font,
                21f,
                TextAlignmentOptions.Left,
                new Color(0.58f, 0.72f, 0.78f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(530f, 40f),
                new Vector2(10f, 74f));
            RankingUI.CreateText(
                scoreBlock.transform,
                "Score",
                _score.ToString("N0"),
                font,
                80f,
                TextAlignmentOptions.Left,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(530f, 100f),
                new Vector2(10f, 10f));
            RankingUI.CreateText(
                scoreBlock.transform,
                "ScoreFooter",
                "LOCAL RECORD SUBMITTED",
                font,
                17f,
                TextAlignmentOptions.Left,
                new Color(0.12f, 0.88f, 1f, 0.82f),
                new Vector2(0.5f, 0.5f),
                new Vector2(530f, 35f),
                new Vector2(10f, -78f));

            Image placementBlock = RankingUI.CreateImage(
                frame.transform,
                "PlacementBlock",
                new Color(0.040f, 0.057f, 0.072f, 0.98f));
            RankingUI.SetRect(
                placementBlock.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(340f, 230f),
                new Vector2(-25f, -10f));
            RankingUI.CreateText(
                placementBlock.transform,
                "PlacementLabel",
                "LOCAL PLACEMENT",
                font,
                18f,
                TextAlignmentOptions.Center,
                new Color(0.58f, 0.72f, 0.78f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(310f, 36f),
                new Vector2(0f, 75f));

            RankingUI.CreateText(
                placementBlock.transform,
                "PlayerRank",
                "#" + _rank.ToString("0000"),
                font,
                55f,
                TextAlignmentOptions.Center,
                new Color(0.16f, 0.92f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(310f, 70f),
                new Vector2(0f, 12f));
            RankingUI.CreateText(
                placementBlock.transform,
                "PlayerName",
                AILURONELocalRanking.CurrentUsername.ToUpperInvariant(),
                font,
                21f,
                TextAlignmentOptions.Center,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(310f, 42f),
                new Vector2(0f, -65f));

            BuildLeaderboard(frame.transform, font);
            BuildButtons(frame.transform, font);
        }

        private void BuildLeaderboard(Transform parent, TMP_FontAsset font)
        {
            RankingUI.CreateText(
                parent,
                "RankingTitle",
                "LOCAL  //  RANKING",
                font,
                30f,
                TextAlignmentOptions.Left,
                Color.white,
                new Vector2(0.5f, 1f),
                new Vector2(570f, 48f),
                new Vector2(535f, -62f));
            RankingUI.CreateText(
                parent,
                "RankingColumns",
                "RANK                 OPERATOR                         SCORE",
                font,
                14f,
                TextAlignmentOptions.Left,
                new Color(0.48f, 0.63f, 0.69f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(570f, 30f),
                new Vector2(535f, -105f));

            List<AILURONELocalRanking.Entry> entries =
                AILURONELocalRanking.GetTopEntries(10);
            for (int index = 0; index < 10; index++)
            {
                bool available = index < entries.Count;
                AILURONELocalRanking.Entry entry = available
                    ? entries[index]
                    : null;
                bool current = available && string.Equals(
                    entry.username,
                    AILURONELocalRanking.CurrentUsername,
                    StringComparison.OrdinalIgnoreCase);
                Color rowColor = current
                    ? new Color(0.08f, 0.42f, 0.50f, 0.95f)
                    : new Color(0.07f, 0.09f, 0.12f, 0.92f);
                Image row = RankingUI.CreateImage(
                    parent,
                    "Rank_" + (index + 1).ToString("00"),
                    rowColor);
                RankingUI.SetRect(
                    row.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(590f, 54f),
                    new Vector2(535f, -151f - index * 59f));

                Color textColor = available
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.2f);
                RankingUI.CreateText(
                    row.transform,
                    "Rank",
                    "#" + (index + 1).ToString("0000"),
                    font,
                    18f,
                    TextAlignmentOptions.MidlineLeft,
                    textColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(115f, 48f),
                    new Vector2(-220f, 0f));
                RankingUI.CreateText(
                    row.transform,
                    "Operator",
                    available ? entry.username : "---",
                    font,
                    19f,
                    TextAlignmentOptions.MidlineLeft,
                    textColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(290f, 48f),
                    new Vector2(-15f, 0f));
                RankingUI.CreateText(
                    row.transform,
                    "Score",
                    available ? entry.score.ToString("N0") : "---",
                    font,
                    19f,
                    TextAlignmentOptions.MidlineRight,
                    current
                        ? new Color(0.18f, 0.95f, 1f, 1f)
                        : textColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(135f, 48f),
                    new Vector2(210f, 0f));
            }
        }

        private void BuildButtons(Transform parent, TMP_FontAsset font)
        {
            Button mainMenu = RankingUI.CreateButton(
                parent,
                "MainMenu",
                "MAIN MENU",
                font,
                new Vector2(280f, 66f),
                new Vector2(-655f, -380f));
            RegisterButton(mainMenu, LevelCompleteChoice.MainMenu);

            Button restart = RankingUI.CreateButton(
                parent,
                "Restart",
                "RESTART",
                font,
                new Vector2(280f, 66f),
                new Vector2(-350f, -380f));
            RegisterButton(restart, LevelCompleteChoice.Restart);

            Button quit = RankingUI.CreateButton(
                parent,
                "Quit",
                "QUIT",
                font,
                new Vector2(280f, 66f),
                new Vector2(-45f, -380f));
            RegisterButton(quit, LevelCompleteChoice.Quit);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(mainMenu.gameObject);
            }
        }

        private void RegisterButton(
            Button button,
            LevelCompleteChoice choice)
        {
            button.onClick.AddListener(() => Choice = choice);
            _buttonBindings.Add(new ButtonChoiceBinding
            {
                button = button,
                choice = choice
            });
        }

        private static void EnsureRankingEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventObject = new GameObject(
                    "RankingEventSystem_Runtime",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            eventSystem.gameObject.SetActive(true);
            eventSystem.enabled = true;
            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                StandaloneInputModule legacy =
                    eventSystem.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    legacy.enabled = false;
                }
                inputModule = eventSystem.gameObject.AddComponent<
                    InputSystemUIInputModule>();
            }
            inputModule.enabled = true;
        }
    }

    internal static class RankingUI
    {
        public static TMP_FontAsset FindPreferredFont()
        {
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include);
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_FontAsset font = texts[index].font;
                if (font != null && font.name.IndexOf(
                    "SpaceGrotesk",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return font;
                }
            }
            return TMP_Settings.defaultFontAsset;
        }

        public static Image CreateImage(
            Transform parent,
            string name,
            Color color)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            float size,
            TextAlignmentOptions alignment,
            Color color,
            Vector2 anchor,
            Vector2 dimensions,
            Vector2 position)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchor, dimensions, position);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 dimensions,
            Vector2 position)
        {
            Image image = CreateImage(
                parent,
                name,
                new Color(0.10f, 0.14f, 0.18f, 0.96f));
            SetRect(
                image.rectTransform,
                new Vector2(0.5f, 0.5f),
                dimensions,
                position);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.12f, 0.82f, 0.95f, 1f);
            colors.pressedColor = new Color(0.08f, 0.55f, 0.66f, 1f);
            button.colors = colors;
            CreateText(
                image.transform,
                "Label",
                label,
                font,
                24f,
                TextAlignmentOptions.Center,
                Color.white,
                new Vector2(0.5f, 0.5f),
                dimensions,
                Vector2.zero);
            return button;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 dimensions,
            Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
        }
    }
}
