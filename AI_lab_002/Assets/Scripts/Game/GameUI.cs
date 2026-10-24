using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniChess
{
    /// <summary>
    /// Пользовательский интерфейс: строка статуса, панель информации об ИИ,
    /// кнопки (новая игра, отмена хода, глубина поиска) и экран конца партии.
    /// Создаётся целиком из кода на uGUI.
    /// </summary>
    public sealed class GameUI
    {
        private static readonly Color PanelColor = new Color(0.23f, 0.26f, 0.30f, 0.95f);
        private static readonly Color PanelActive = new Color(0.30f, 0.52f, 0.33f, 0.95f);
        private static readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color DimTextColor = new Color(0.68f, 0.70f, 0.74f);

        public System.Action NewGameWhite;
        public System.Action NewGameBlack;
        public System.Action UndoClicked;
        public System.Action PlayAgain;
        public System.Action<int> DepthSelected;

        private Text statusText;
        private Text infoText;
        private Text overlayTitle;
        private Text overlaySubtitle;
        private GameObject overlayRoot;
        private readonly Dictionary<int, Image> depthButtons = new Dictionary<int, Image>();

        public void Build()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            statusText = CreateText(canvasObject.transform, "Status");
            Place(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 44f));
            statusText.fontSize = 25;
            statusText.color = TextColor;
            statusText.alignment = TextAnchor.MiddleCenter;

            infoText = CreateText(canvasObject.transform, "Info");
            Place(infoText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 104f), new Vector2(760f, 64f));
            infoText.fontSize = 15;
            infoText.color = DimTextColor;
            infoText.alignment = TextAnchor.LowerLeft;

            BuildBottomBar(canvasObject.transform);
            BuildGameOverOverlay(canvasObject.transform);
        }

        public void SetStatus(string text)
        {
            statusText.text = text;
        }

        public void SetInfo(string text)
        {
            infoText.text = text;
        }

        public void SetDepthSelected(int depth)
        {
            foreach (KeyValuePair<int, Image> pair in depthButtons)
                pair.Value.color = pair.Key == depth ? PanelActive : PanelColor;
        }

        public void ShowGameOver(string title, string subtitle)
        {
            overlayTitle.text = title;
            overlaySubtitle.text = subtitle;
            overlayRoot.SetActive(true);
        }

        public void HideGameOver()
        {
            overlayRoot.SetActive(false);
        }

        // ---------------------------------------------------------------- сборка

        private void BuildBottomBar(Transform canvasTransform)
        {
            var bar = new GameObject("BottomBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(canvasTransform, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 10f);
            barRect.sizeDelta = new Vector2(0f, 56f);

            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateButton(bar.transform, "Новая игра: белые", 190f, 44f, 16, OnNewGameWhite);
            CreateButton(bar.transform, "Новая игра: чёрные", 190f, 44f, 16, OnNewGameBlack);
            CreateButton(bar.transform, "Отменить ход", 140f, 44f, 16, OnUndoClicked);

            Text depthLabel = CreateText(bar.transform, "DepthLabel");
            depthLabel.text = "Глубина ИИ:";
            depthLabel.fontSize = 16;
            depthLabel.color = DimTextColor;
            depthLabel.alignment = TextAnchor.MiddleCenter;
            var labelLayout = depthLabel.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = 110f;
            labelLayout.minHeight = 44f;

            foreach (int depth in new[] { 2, 3, 4, 5 })
            {
                int captured = depth;
                Button button = CreateButton(bar.transform, depth.ToString(), 44f, 44f, 18, () => OnDepthSelected(captured));
                depthButtons[depth] = button.GetComponent<Image>();
            }
        }

        private void BuildGameOverOverlay(Transform canvasTransform)
        {
            overlayRoot = new GameObject("GameOverOverlay", typeof(RectTransform), typeof(Image));
            overlayRoot.transform.SetParent(canvasTransform, false);
            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            overlayTitle = CreateText(overlayRoot.transform, "Title");
            Place(overlayTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(800f, 64f));
            overlayTitle.fontSize = 42;
            overlayTitle.color = TextColor;
            overlayTitle.alignment = TextAnchor.MiddleCenter;

            overlaySubtitle = CreateText(overlayRoot.transform, "Subtitle");
            Place(overlaySubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 40f));
            overlaySubtitle.fontSize = 19;
            overlaySubtitle.color = DimTextColor;
            overlaySubtitle.alignment = TextAnchor.MiddleCenter;

            CreateButton(overlayRoot.transform, "Играть снова", 200f, 48f, 18, OnPlayAgain,
                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f));

            overlayRoot.SetActive(false);
        }

        private void OnNewGameWhite() { if (NewGameWhite != null) NewGameWhite(); }
        private void OnNewGameBlack() { if (NewGameBlack != null) NewGameBlack(); }
        private void OnUndoClicked() { if (UndoClicked != null) UndoClicked(); }
        private void OnPlayAgain() { if (PlayAgain != null) PlayAgain(); }
        private void OnDepthSelected(int depth) { if (DepthSelected != null) DepthSelected(depth); }

        // ---------------------------------------------------------------- помощники

        private static Font BuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Text CreateText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = BuiltinFont();
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private Button CreateButton(Transform parent, string label, float width, float height, int fontSize,
                                    System.Action onClick)
        {
            return CreateButton(parent, label, width, height, fontSize, onClick, null, null, null);
        }

        private Button CreateButton(Transform parent, string label, float width, float height, int fontSize,
                                    System.Action onClick, Vector2? anchorMin, Vector2? anchorMax, Vector2? position)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            if (anchorMin.HasValue && anchorMax.HasValue && position.HasValue)
            {
                rect.anchorMin = anchorMin.Value;
                rect.anchorMax = anchorMax.Value;
                rect.anchoredPosition = position.Value;
            }
            rect.sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<Image>();
            image.color = PanelColor;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.32f, 0.36f, 0.41f);
            colors.pressedColor = new Color(0.17f, 0.19f, 0.22f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var layout = go.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.minHeight = height;

            Text text = CreateText(go.transform, "Label");
            Place(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;

            return button;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
