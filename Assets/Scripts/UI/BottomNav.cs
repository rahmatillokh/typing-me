using System;
using System.Collections.Generic;
using TMPro;
using TypingMe.Audio;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// Bottom navigation (§7). Two tabs today, but tabs are data — adding a third is a list entry,
    /// not a code change.
    /// </summary>
    public sealed class BottomNav : MonoBehaviour
    {
        [Serializable]
        public sealed class Tab
        {
            public string id;
            public Button button;
            public GameObject panel;
            public TMP_Text label;
            public Image icon;
        }

        [SerializeField] private List<Tab> tabs = new List<Tab>();
        [SerializeField] private string defaultTabId = "home";

        public event Action<string> TabChanged;

        public string ActiveTabId { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab?.button == null) continue;

                string id = tab.id;
                tab.button.onClick.AddListener(() => Select(id));
            }
        }

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += HandleThemeChanged;
            Select(string.IsNullOrEmpty(ActiveTabId) ? defaultTabId : ActiveTabId);
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= HandleThemeChanged;

        private void HandleThemeChanged(ThemeSO theme) => Repaint();

        public void Select(string tabId)
        {
            ActiveTabId = tabId;

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab == null) continue;

                bool active = tab.id == tabId;
                if (tab.panel != null) tab.panel.SetActive(active);
            }

            Repaint();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            TabChanged?.Invoke(tabId);
        }

        private void Repaint()
        {
            ThemeSO theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab == null) continue;

                bool active = tab.id == ActiveTabId;
                Color colour = active ? theme.accentPrimary : theme.textMuted;

                if (tab.label != null) tab.label.color = colour;
                if (tab.icon != null) tab.icon.color = colour;
            }
        }
    }
}
