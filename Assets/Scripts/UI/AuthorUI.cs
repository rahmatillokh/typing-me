using System.Collections.Generic;
using TMPro;
using TypingMe.Audio;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// The Author tab: who made the game, where to find them, and where to support it.
    /// </summary>
    /// <remarks>
    /// Every field comes from <see cref="AuthorInfoSO"/>. Link buttons are built at runtime from
    /// that list, so adding a link is an asset edit rather than a scene change, and a link with no
    /// URL is skipped instead of shipping a button that does nothing.
    /// </remarks>
    public sealed class AuthorUI : MonoBehaviour
    {
        [SerializeField] private AuthorInfoSO info;

        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text roleLabel;
        [SerializeField] private TMP_Text bioLabel;
        [SerializeField] private RectTransform linksRoot;

        [SerializeField] private Button donateButton;
        [SerializeField] private TMP_Text donateLabel;

        [Header("Runtime link buttons")]
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private TMP_FontAsset buttonFont;
        [SerializeField] private float buttonWidth = 300f;
        [SerializeField] private float buttonHeight = 56f;

        private readonly List<GameObject> _linkButtons = new List<GameObject>();

        private void Awake()
        {
            if (donateButton != null) donateButton.onClick.AddListener(OpenDonate);
        }

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += HandleThemeChanged;
            Refresh();
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= HandleThemeChanged;

        private void HandleThemeChanged(ThemeSO theme) => Refresh();

        public void Refresh()
        {
            ThemeSO theme = ThemeManager.ActiveTheme;

            // An empty field is hidden outright rather than filled with a notice. The panel lays its
            // children out vertically, so a hidden one collapses instead of leaving a hole.
            if (nameLabel != null)
            {
                bool filled = info != null && info.HasIdentity;
                nameLabel.gameObject.SetActive(filled);

                if (filled)
                {
                    nameLabel.text = info.displayName.ToUpperInvariant();
                    if (theme != null) nameLabel.color = theme.accentPrimary;
                }
            }

            if (roleLabel != null)
            {
                bool filled = info != null && !string.IsNullOrWhiteSpace(info.role);
                roleLabel.gameObject.SetActive(filled);

                if (filled)
                {
                    roleLabel.text = info.role;
                    if (theme != null) roleLabel.color = theme.accentSecondary;
                }
            }

            if (bioLabel != null)
            {
                bool filled = info != null && info.HasBio;
                bioLabel.gameObject.SetActive(filled);

                if (filled)
                {
                    bioLabel.text = info.bio;
                    if (theme != null) bioLabel.color = theme.textPrimary;
                }
            }

            BuildLinks(theme);

            if (donateButton == null) return;

            bool hasDonate = info != null && info.HasDonate;
            donateButton.gameObject.SetActive(hasDonate);

            if (!hasDonate) return;

            if (donateLabel != null)
            {
                donateLabel.text = string.IsNullOrWhiteSpace(info.donateLabel)
                    ? "SUPPORT THE PROJECT"
                    : info.donateLabel.ToUpperInvariant();

                if (theme != null) donateLabel.color = theme.accentSecondary;
            }

            if (donateButton.targetGraphic is Image frame && theme != null)
                frame.color = new Color(theme.accentSecondary.r, theme.accentSecondary.g,
                    theme.accentSecondary.b, 0.2f);
        }

        private void BuildLinks(ThemeSO theme)
        {
            for (int i = 0; i < _linkButtons.Count; i++)
                if (_linkButtons[i] != null) Destroy(_linkButtons[i]);

            _linkButtons.Clear();

            if (linksRoot == null || info?.links == null) return;

            foreach (AuthorInfoSO.Link link in info.links)
            {
                if (link == null || string.IsNullOrWhiteSpace(link.url)) continue;

                string url = link.url;
                string label = string.IsNullOrWhiteSpace(link.label) ? url : link.label.ToUpperInvariant();

                var go = new GameObject($"Link_{label}", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(linksRoot, false);

                var frame = go.GetComponent<Image>();
                frame.sprite = buttonSprite;
                if (buttonSprite != null) frame.type = Image.Type.Sliced;
                frame.color = theme != null
                    ? new Color(theme.accentPrimary.r, theme.accentPrimary.g, theme.accentPrimary.b, 0.16f)
                    : new Color(1f, 1f, 1f, 0.12f);

                var element = go.GetComponent<LayoutElement>();
                element.preferredWidth = buttonWidth;
                element.preferredHeight = buttonHeight;

                var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);

                var text = textGo.GetComponent<TextMeshProUGUI>();
                if (buttonFont != null) text.font = buttonFont;
                text.text = label;
                text.fontSize = 24f;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.color = theme != null ? theme.accentPrimary : Color.white;

                var textRect = (RectTransform)textGo.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var button = go.GetComponent<Button>();
                button.targetGraphic = frame;
                button.onClick.AddListener(() => Open(url));

                _linkButtons.Add(go);
            }
        }

        private void OpenDonate()
        {
            if (info != null && info.HasDonate) Open(info.donateUrl);
        }

        private static void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            Application.OpenURL(url);
        }
    }
}
