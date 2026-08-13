using TMPro;
using TypingMe.Data;
using TypingMe.Fx;
using TypingMe.Gameplay;
using TypingMe.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TypingMe.Core;

namespace TypingMe.EditorTools
{
    /// <summary>Builds the Menu and Game scenes from code (see <see cref="ProjectBootstrap"/>).</summary>
    internal static class SceneBuilder
    {
        private const string ScenesDir = "Assets/Scenes";

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        /// <summary>
        /// The boss patrols here, at the top of the play area, and throws words from just below it.
        /// </summary>
        /// <remarks>
        /// Words used to spawn at 430; giving the boss the top band shortened the fall to 536 px from
        /// 680. LevelTuning's speeds were scaled by that same ratio (×0.788) so fall *duration* — the
        /// thing the difficulty curve was tuned against — is unchanged.
        /// </remarks>
        private const float BossPatrolY = 366f;
        private const float SpawnY = 286f;
        private const float BottomLineY = -250f;

        // Level grid: one season at a time, 20 levels as 5x4.
        // 5 * 96 + 4 * 12 = 528 reference px across; four rows are 420 px tall, comfortably inside
        // the shortest canvas (550 px of room at 16:9) without scrolling.
        private const int LevelColumns = 5;
        private const float LevelCellSize = 96f;
        private const float LevelCellSpacing = 12f;
        private const float LevelAreaLeftAnchor = 0.21f;
        private const float LevelAreaRightAnchor = 0.79f;
        private const float LevelAreaTopInset = 340f;
        private const float LevelAreaBottomInset = 130f;
        private const float SeasonTabHeight = 50f;

        public const string SplashVideoPath = "Assets/Video/AysappsSplash.mp4";

        public static void BuildAll(AssetSet assets)
        {
            BuildSplashScene(assets);
            BuildMenuScene(assets);
            BuildGameScene(assets);
        }

        #region Splash scene

        /// <summary>
        /// The launch splash: the Aysapps clip full-screen, then straight into the Menu.
        /// </summary>
        private static void BuildSplashScene(AssetSet assets)
        {
            Scene scene = NewScene();

            Camera camera = CreateCamera(assets.PostProcess);
            camera.backgroundColor = Color.black;

            CreateEventSystem();
            Canvas canvas = CreateCanvas(camera);
            Transform root = canvas.transform;

            Image backdrop = UiFactory.AddImage("Backdrop", root, null, Color.black);
            UiFactory.Stretch((RectTransform)backdrop.transform);

            RawImage surface = UiFactory.AddRawImage("VideoSurface", root, Color.white);
            var surfaceRect = (RectTransform)surface.transform;

            // AspectRatioFitter needs coincident anchors; it drives the size itself.
            surfaceRect.anchorMin = surfaceRect.anchorMax = new Vector2(0.5f, 0.5f);
            surfaceRect.pivot = new Vector2(0.5f, 0.5f);
            surfaceRect.anchoredPosition = Vector2.zero;
            surfaceRect.sizeDelta = Reference;

            var fitter = surface.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            TextMeshProUGUI skipHint = UiFactory.AddText("SkipHint", root, "PRESS ANY KEY TO SKIP",
                assets.Fonts.Ui, 22f, TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)skipHint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(760f, 30f));
            skipHint.alpha = 0f;

            // Last child, so the fade-out covers the video and the hint alike.
            Image fade = UiFactory.AddImage("Fade", root, null, Color.black);
            UiFactory.Stretch((RectTransform)fade.transform);
            CanvasGroup fadeGroup = UiFactory.AddCanvasGroup(fade.gameObject, 0f, false, false);

            var playerGo = new GameObject("SplashPlayer", typeof(VideoPlayer), typeof(AudioSource));

            var videoPlayer = playerGo.GetComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(SplashVideoPath);
            videoPlayer.skipOnDrop = true;

            if (videoPlayer.clip == null)
                Debug.LogWarning($"[Typing Me] Splash video missing at {SplashVideoPath}; " +
                                 "the splash scene will fall straight through to the menu.");

            var videoAudio = playerGo.GetComponent<AudioSource>();
            videoAudio.playOnAwake = false;

            var controller = playerGo.AddComponent<SplashController>();
            using (var wiring = new Wiring(controller))
            {
                wiring.Ref("player", videoPlayer)
                      .Ref("surface", surface)
                      .Ref("videoAudio", videoAudio)
                      .Ref("fade", fadeGroup)
                      .Ref("skipHint", skipHint)
                      .Str("nextScene", "Menu");
            }

            SaveScene(scene, "Splash");
        }

        #endregion

        #region Shared scaffolding

        private static Scene NewScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static Camera CreateCamera(VolumeProfile profile)
        {
            // AudioListener is explicit on purpose: the Editor's "Create > Camera" adds one for you,
            // but `new GameObject(typeof(Camera))` does not. Without it every AudioSource still
            // reports isPlaying == true and nothing is ever audible.
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x05, 0x07, 0x0C, 0xFF);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var volumeGo = new GameObject("Global Volume", typeof(Volume));
            var volume = volumeGo.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            return camera;
        }

        /// <summary>
        /// Screen Space - Camera (not Overlay) so the neon UI and falling words are affected by
        /// the Bloom in the post-processing stack (§8).
        /// </summary>
        private static Canvas CreateCanvas(Camera camera)
        {
            var go = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void CreateEventSystem()
        {
            // InputSystemUIInputModule, not StandaloneInputModule: the project is set to the new
            // input backend only, where the legacy module never receives events.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void AddBackdrop(Transform parent)
        {
            RawImage background = UiFactory.AddRawImage("Background", parent, Color.white);
            UiFactory.Stretch((RectTransform)background.transform);
            background.gameObject.AddComponent<GradientBackground>();

            RawImage grid = UiFactory.AddRawImage("GridOverlay", parent, Color.white);
            UiFactory.Stretch((RectTransform)grid.transform);

            var scrolling = grid.gameObject.AddComponent<ScrollingTexture>();
            using (var wiring = new Wiring(scrolling))
            {
                wiring.Enum("pattern", (int)GeneratedPattern.Grid)
                      .Vector2("tiling", new Vector2(16f, 9f))
                      .Vector2("scrollSpeed", new Vector2(0.006f, -0.014f))
                      .Int("cellPixels", 48)
                      .Int("lineThickness", 1);
            }

            UiFactory.Themed(grid, ThemeRole.Grid);

            // The season's weather — petals, embers, leaves or snow — drifts between the backdrop
            // and everything interactive.
            RectTransform ambient = UiFactory.AddRect("SeasonalAmbient", parent);
            UiFactory.Stretch(ambient);

            var fx = ambient.gameObject.AddComponent<SeasonalAmbientFx>();
            using (var wiring = new Wiring(fx))
            {
                wiring.Ref("softSprite", UiFactory.SoftSprite)
                      .Ref("circleSprite", UiFactory.CircleSprite)
                      .Int("count", 26);
            }
        }

        private static void AddScanlines(Transform parent)
        {
            RawImage scanlines = UiFactory.AddRawImage("Scanlines", parent, Color.white);
            UiFactory.Stretch((RectTransform)scanlines.transform);

            var scrolling = scanlines.gameObject.AddComponent<ScrollingTexture>();
            using (var wiring = new Wiring(scrolling))
            {
                wiring.Enum("pattern", (int)GeneratedPattern.Scanlines)
                      .Vector2("tiling", new Vector2(1f, 270f))
                      .Vector2("scrollSpeed", new Vector2(0f, -0.015f))
                      .Int("cellPixels", 4)
                      .Int("lineThickness", 1);
            }

            UiFactory.Themed(scanlines, ThemeRole.Grid, 0.5f);
        }

        private static Slider AddSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform root = UiFactory.AddRect(name, parent);
            UiFactory.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), anchoredPosition, size);

            var slider = root.gameObject.AddComponent<Slider>();
            float inset = size.y * 0.34f;

            Image track = UiFactory.AddImage("Background", root, UiFactory.RoundedSprite,
                new Color(1f, 1f, 1f, 0.14f), raycastTarget: true);
            UiFactory.Stretch((RectTransform)track.transform, 0f, 0f, inset, inset);

            RectTransform fillArea = UiFactory.AddRect("Fill Area", root);
            UiFactory.Stretch(fillArea, 0f, 0f, inset, inset);

            Image fill = UiFactory.AddImage("Fill", fillArea, UiFactory.RoundedSprite, Color.cyan);
            UiFactory.Stretch((RectTransform)fill.transform);
            UiFactory.Themed(fill, ThemeRole.AccentPrimary);

            RectTransform handleArea = UiFactory.AddRect("Handle Slide Area", root);
            UiFactory.Stretch(handleArea, size.y * 0.5f, size.y * 0.5f);

            Image handle = UiFactory.AddImage("Handle", handleArea, UiFactory.CircleSprite, Color.white,
                raycastTarget: true);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(size.y, size.y);
            UiFactory.Themed(handle, ThemeRole.AccentSecondary);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;

            return slider;
        }

        /// <summary>Gives a layout-group child a fixed height, and optionally a fixed width.</summary>
        private static void SizeInColumn(GameObject target, float height, float width = 0f)
        {
            var element = target.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            if (width > 0f) element.preferredWidth = width;
        }

        /// <summary>Fixes a button's size inside a layout group.</summary>
        private static void SizeButton(Button button, float width, float height)
        {
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        private static void SaveScene(Scene scene, string sceneName)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{sceneName}.unity");
        }

        #endregion

        #region Menu scene

        private static void BuildMenuScene(AssetSet assets)
        {
            Scene scene = NewScene();

            Camera camera = CreateCamera(assets.PostProcess);
            CreateEventSystem();
            Canvas canvas = CreateCanvas(camera);
            Transform root = canvas.transform;

            AddBackdrop(root);

            FontSet fonts = assets.Fonts;

            TextMeshProUGUI title = UiFactory.AddText("Title", root, "TYPING ME", fonts.Display, 82f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(1200f, 100f));
            UiFactory.Themed(title, ThemeRole.AccentPrimary);

            TextMeshProUGUI tagline = UiFactory.AddText("Tagline", root, "TYPE FAST. STAY OFFLINE.", fonts.Ui, 28f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)tagline.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -152f), new Vector2(1200f, 40f));
            UiFactory.Themed(tagline, ThemeRole.TextMuted);

            GameObject homePanel = BuildHomePanel(root, assets, out HomeUI homeUi);
            GameObject settingsPanel = BuildSettingsPanel(root, assets);
            GameObject authorPanel = BuildAuthorPanel(root, assets);

            BuildBottomNav(root, fonts, homePanel, settingsPanel, authorPanel);
            AddScanlines(root);

            SaveScene(scene, "Menu");
        }

        private static GameObject BuildHomePanel(Transform parent, AssetSet assets, out HomeUI homeUi)
        {
            FontSet fonts = assets.Fonts;

            RectTransform panel = UiFactory.AddRect("HomePanel", parent);
            UiFactory.Stretch(panel);

            Button continueButton = UiFactory.AddButton("ContinueButton", panel, "CONTINUE", fonts.Display, 36f,
                new Color(1f, 1f, 1f, 0.10f), Color.white, out TextMeshProUGUI continueLabel,
                out Image continueFrame);
            UiFactory.Anchor((RectTransform)continueButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -198f), new Vector2(620f, 88f));
            UiFactory.Themed(continueFrame, ThemeRole.AccentPrimary, 0.16f);
            UiFactory.Themed(continueLabel, ThemeRole.AccentPrimary);

            TextMeshProUGUI bestScore = UiFactory.AddText("BestScore", panel, "BEST  0", fonts.Ui, 28f,
                TextAlignmentOptions.Center, Color.white);
            // Sits in the gap between the Continue button and the season tabs; keep all three in
            // step if any of them moves.
            UiFactory.Anchor((RectTransform)bestScore.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-220f, -296f), new Vector2(400f, 34f));
            UiFactory.Themed(bestScore, ThemeRole.TextMuted);

            TextMeshProUGUI seasonBadge = UiFactory.AddText("SeasonBadge", panel, "SPRING SEASON", fonts.Ui, 28f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)seasonBadge.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(220f, -296f), new Vector2(400f, 34f));

            LevelSelectUI levelSelect = BuildLevelGrid(panel, assets);

            homeUi = panel.gameObject.AddComponent<HomeUI>();
            using (var wiring = new Wiring(homeUi))
            {
                wiring.Ref("continueButton", continueButton)
                      .Ref("continueLabel", continueLabel)
                      .Ref("bestScoreLabel", bestScore)
                      .Ref("seasonLabel", seasonBadge)
                      .Ref("levelSelect", levelSelect);
            }

            return panel.gameObject;
        }

        private static LevelSelectUI BuildLevelGrid(Transform parent, AssetSet assets)
        {
            // Proportional anchors, not fixed insets. CanvasScaler's match-0.5 mode shrinks the
            // reference width below 1920 on anything taller than 16:9 (a 16:10 window gives ~1827),
            // so a grid sized against a fixed 1920 gets its outer columns clipped.
            RectTransform area = UiFactory.AddRect("LevelArea", parent);
            area.anchorMin = new Vector2(LevelAreaLeftAnchor, 0f);
            area.anchorMax = new Vector2(LevelAreaRightAnchor, 1f);
            area.offsetMin = new Vector2(0f, LevelAreaBottomInset);
            area.offsetMax = new Vector2(0f, -LevelAreaTopInset);

            // Season tabs across the top of the level area.
            RectTransform tabRow = UiFactory.AddRect("SeasonTabs", area);
            tabRow.anchorMin = new Vector2(0f, 1f);
            tabRow.anchorMax = new Vector2(1f, 1f);
            tabRow.pivot = new Vector2(0.5f, 1f);
            tabRow.offsetMin = new Vector2(0f, -SeasonTabHeight);
            tabRow.offsetMax = new Vector2(0f, 0f);

            var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f;
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;

            var tabButtons = new Button[SeasonCatalog.SeasonCount];
            var tabLabels = new TextMeshProUGUI[SeasonCatalog.SeasonCount];
            var tabFrames = new Image[SeasonCatalog.SeasonCount];
            var tabLocks = new GameObject[SeasonCatalog.SeasonCount];

            for (int i = 0; i < SeasonCatalog.SeasonCount; i++)
            {
                var season = (Season)i;

                tabButtons[i] = UiFactory.AddButton($"Tab{season}", tabRow, SeasonCatalog.DisplayName(season),
                    assets.Fonts.Ui, 24f, new Color(1f, 1f, 1f, 0.08f), Color.white,
                    out tabLabels[i], out tabFrames[i]);

                var element = tabButtons[i].gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 150f;
                element.preferredHeight = 44f;

                Image lockPlate = UiFactory.AddImage("LockedBadge", tabButtons[i].transform,
                    UiFactory.RoundedSprite, new Color(0f, 0f, 0f, 0.6f));
                UiFactory.Anchor((RectTransform)lockPlate.transform, new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(14f, 14f));
                tabLocks[i] = lockPlate.gameObject;
            }

            RectTransform scrollRoot = UiFactory.AddRect("LevelScroll", area);
            scrollRoot.anchorMin = Vector2.zero;
            scrollRoot.anchorMax = Vector2.one;
            scrollRoot.offsetMin = new Vector2(0f, 0f);
            scrollRoot.offsetMax = new Vector2(0f, -(SeasonTabHeight + 10f));

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.scrollSensitivity = 32f;

            RectTransform viewport = UiFactory.AddRect("Viewport", scrollRoot);
            UiFactory.Stretch(viewport);
            // RectMask2D clips without needing a mask graphic.
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UiFactory.AddRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(LevelCellSize, LevelCellSize);
            grid.spacing = new Vector2(LevelCellSpacing, LevelCellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = LevelColumns;
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            // On the area, not the scroll view: it owns the season tabs as well as the grid.
            var levelSelect = area.gameObject.AddComponent<LevelSelectUI>();
            using (var wiring = new Wiring(levelSelect))
            {
                wiring.Ref("grid", content)
                      .Ref("buttonPrefab", assets.Prefabs.LevelButton.GetComponent<LevelButton>())
                      .Elements("seasonTabs", SeasonCatalog.SeasonCount, (element, index) =>
                      {
                          element.FindPropertyRelative("button").objectReferenceValue = tabButtons[index];
                          element.FindPropertyRelative("label").objectReferenceValue = tabLabels[index];
                          element.FindPropertyRelative("frame").objectReferenceValue = tabFrames[index];
                          element.FindPropertyRelative("lockedBadge").objectReferenceValue = tabLocks[index];
                      });
            }

            return levelSelect;
        }

        private static GameObject BuildSettingsPanel(Transform parent, AssetSet assets)
        {
            FontSet fonts = assets.Fonts;

            RectTransform panel = UiFactory.AddRect("SettingsPanel", parent);
            UiFactory.Stretch(panel);

            TextMeshProUGUI header = UiFactory.AddText("Header", panel, "SETTINGS", fonts.Display, 46f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)header.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -212f), new Vector2(900f, 60f));
            UiFactory.Themed(header, ThemeRole.AccentPrimary);

            // No theme picker — the palette is the season's, and only beating the season changes it.
            // A single line says so, so the absence reads as a rule rather than a missing feature.
            TextMeshProUGUI themeNote = UiFactory.AddText("ThemeNote", panel,
                "THE LOOK FOLLOWS THE SEASON — BEAT A SEASON TO CHANGE IT", fonts.Ui, 24f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)themeNote.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -292f), new Vector2(1100f, 36f));
            UiFactory.Themed(themeNote, ThemeRole.TextMuted);

            Slider musicSlider = AddSlider("MusicSlider", panel, new Vector2(140f, -400f), new Vector2(560f, 36f));
            TextMeshProUGUI musicLabel = UiFactory.AddText("MusicLabel", panel, "MUSIC", fonts.Ui, 28f,
                TextAlignmentOptions.Right, Color.white);
            UiFactory.Anchor((RectTransform)musicLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-280f, -400f), new Vector2(240f, 36f));
            UiFactory.Themed(musicLabel, ThemeRole.TextPrimary);

            Slider sfxSlider = AddSlider("SfxSlider", panel, new Vector2(140f, -466f), new Vector2(560f, 36f));
            TextMeshProUGUI sfxLabel = UiFactory.AddText("SfxLabel", panel, "SFX", fonts.Ui, 28f,
                TextAlignmentOptions.Right, Color.white);
            UiFactory.Anchor((RectTransform)sfxLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-280f, -466f), new Vector2(240f, 36f));
            UiFactory.Themed(sfxLabel, ThemeRole.TextPrimary);

            Button resetButton = UiFactory.AddButton("ResetButton", panel, "RESET PROGRESS", fonts.Ui, 28f,
                new Color(1f, 1f, 1f, 0.10f), Color.white, out TextMeshProUGUI resetLabel, out Image resetFrame);
            UiFactory.Anchor((RectTransform)resetButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -580f), new Vector2(420f, 72f));
            UiFactory.Themed(resetFrame, ThemeRole.AccentAlert, 0.18f);
            UiFactory.Themed(resetLabel, ThemeRole.AccentAlert);

            TextMeshProUGUI version = UiFactory.AddText("Version", panel, "v1.0", fonts.Ui, 22f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)version.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -664f), new Vector2(400f, 30f));
            UiFactory.Themed(version, ThemeRole.TextMuted);

            var settingsUi = panel.gameObject.AddComponent<SettingsUI>();
            using (var wiring = new Wiring(settingsUi))
            {
                wiring.Ref("musicSlider", musicSlider)
                      .Ref("sfxSlider", sfxSlider)
                      .Ref("resetButton", resetButton)
                      .Ref("resetLabel", resetLabel)
                      .Ref("versionLabel", version);
            }

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        /// <summary>
        /// The Author tab. Content lives in AuthorInfo.asset and ships blank — see
        /// <see cref="TypingMe.UI.AuthorUI"/>.
        /// </summary>
        private static GameObject BuildAuthorPanel(Transform parent, AssetSet assets)
        {
            FontSet fonts = assets.Fonts;

            RectTransform panel = UiFactory.AddRect("AuthorPanel", parent);
            UiFactory.Stretch(panel);

            TextMeshProUGUI header = UiFactory.AddText("Header", panel, "AUTHOR", fonts.Display, 46f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)header.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 212f * -1f), new Vector2(900f, 60f));
            UiFactory.Themed(header, ThemeRole.AccentPrimary);

            // Vertical layout, not fixed anchors: any field the author leaves blank is hidden at
            // runtime, and a layout group closes the gap instead of leaving a hole in the page.
            RectTransform content = UiFactory.AddRect("Content", panel);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, -282f);
            content.sizeDelta = new Vector2(1000f, 420f);

            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 18f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            TextMeshProUGUI nameLabel = UiFactory.AddText("Name", content, "AUTHOR", fonts.Display, 40f,
                TextAlignmentOptions.Center, Color.white);
            SizeInColumn(nameLabel.gameObject, 52f);

            TextMeshProUGUI roleLabel = UiFactory.AddText("Role", content, "", fonts.Ui, 26f,
                TextAlignmentOptions.Center, Color.white);
            SizeInColumn(roleLabel.gameObject, 34f);

            TextMeshProUGUI bioLabel = UiFactory.AddText("Bio", content, "", fonts.Ui, 24f,
                TextAlignmentOptions.Top, Color.white);
            bioLabel.textWrappingMode = TextWrappingModes.Normal;

            // No fixed height: TMP reports its own preferred height, so the bio grows with its text.
            var bioElement = bioLabel.gameObject.AddComponent<LayoutElement>();
            bioElement.preferredWidth = 900f;

            RectTransform links = UiFactory.AddRect("Links", content);
            SizeInColumn(links.gameObject, 70f);

            var linkLayout = links.gameObject.AddComponent<HorizontalLayoutGroup>();
            linkLayout.spacing = 16f;
            linkLayout.childAlignment = TextAnchor.MiddleCenter;
            linkLayout.childControlWidth = true;
            linkLayout.childControlHeight = true;
            linkLayout.childForceExpandWidth = false;
            linkLayout.childForceExpandHeight = false;

            Button donate = UiFactory.AddButton("DonateButton", content, "SUPPORT THE PROJECT", fonts.Display,
                30f, new Color(1f, 1f, 1f, 0.12f), Color.white,
                out TextMeshProUGUI donateLabel, out Image donateFrame);
            SizeInColumn(donate.gameObject, 78f, 520f);

            var authorUi = panel.gameObject.AddComponent<AuthorUI>();

            // Re-loaded here rather than taken from the AssetSet. Opening a scene unloads assets
            // nothing is holding, and this one is referenced by nothing until this very panel — so
            // the instance captured before the scenes were built has already been collected.
            // Everything else in the set survives only because the prefabs reference it.
            var info = AssetDatabase.LoadAssetAtPath<AuthorInfoSO>(ProjectBootstrap.AuthorInfoAssetPath);
            if (info == null)
                Debug.LogError($"[Typing Me] AuthorInfo missing at {ProjectBootstrap.AuthorInfoAssetPath}.");

            using (var wiring = new Wiring(authorUi))
            {
                wiring.Ref("info", info)
                      .Ref("nameLabel", nameLabel)
                      .Ref("roleLabel", roleLabel)
                      .Ref("bioLabel", bioLabel)
                      .Ref("linksRoot", links)
                      .Ref("donateButton", donate)
                      .Ref("donateLabel", donateLabel)
                      .Ref("buttonSprite", UiFactory.RoundedSprite)
                      .Ref("buttonFont", fonts.Ui);
            }

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        private static void BuildBottomNav(Transform parent, FontSet fonts, GameObject homePanel,
            GameObject settingsPanel, GameObject authorPanel)
        {
            Image bar = UiFactory.AddImage("BottomNav", parent, UiFactory.RoundedSprite,
                new Color(0f, 0f, 0f, 0.55f), raycastTarget: true);
            UiFactory.StretchHorizontal((RectTransform)bar.transform, 108f, 0f, fromTop: false);

            var ids = new[] { "home", "settings", "author" };
            var captions = new[] { "HOME", "SETTINGS", "AUTHOR" };
            var panels = new[] { homePanel, settingsPanel, authorPanel };

            var buttons = new Button[ids.Length];
            var labels = new TextMeshProUGUI[ids.Length];

            // Three tabs across the bar, evenly spaced about the centre.
            const float tabWidth = 300f;
            const float tabSpacing = 320f;
            float start = -tabSpacing * (ids.Length - 1) * 0.5f;

            for (int i = 0; i < ids.Length; i++)
            {
                buttons[i] = UiFactory.AddButton($"{captions[i]}Tab", bar.transform, captions[i], fonts.Ui, 30f,
                    new Color(1f, 1f, 1f, 0f), Color.white, out labels[i], out Image _);
                UiFactory.Anchor((RectTransform)buttons[i].transform, new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(start + i * tabSpacing, 0f),
                    new Vector2(tabWidth, 84f));
            }

            var nav = bar.gameObject.AddComponent<BottomNav>();
            using (var wiring = new Wiring(nav))
            {
                wiring.Str("defaultTabId", "home")
                      .Elements("tabs", ids.Length, (element, index) =>
                      {
                          element.FindPropertyRelative("id").stringValue = ids[index];
                          element.FindPropertyRelative("button").objectReferenceValue = buttons[index];
                          element.FindPropertyRelative("panel").objectReferenceValue = panels[index];
                          element.FindPropertyRelative("label").objectReferenceValue = labels[index];
                          element.FindPropertyRelative("icon").objectReferenceValue = null;
                      });
            }
        }

        #endregion

        #region Game scene

        private static void BuildGameScene(AssetSet assets)
        {
            Scene scene = NewScene();

            Camera camera = CreateCamera(assets.PostProcess);
            CreateEventSystem();
            Canvas canvas = CreateCanvas(camera);
            Transform root = canvas.transform;
            FontSet fonts = assets.Fonts;

            AddBackdrop(root);

            // Everything that should shake on a miss (§8) lives under here; the backdrop does not.
            RectTransform shakeRoot = UiFactory.AddRect("ShakeRoot", root);
            UiFactory.Stretch(shakeRoot);

            RectTransform wordLayer = UiFactory.AddRect("WordLayer", shakeRoot);
            UiFactory.Stretch(wordLayer);

            // Above the words so shards are never hidden behind a later spawn.
            RectTransform burstLayer = UiFactory.AddRect("BurstLayer", shakeRoot);
            UiFactory.Stretch(burstLayer);
            var clearBurst = burstLayer.gameObject.AddComponent<ShardBurst>();

            // In front of the words: the boss is a character now, not a backdrop, and its telegraph
            // has to stay readable over anything it has just thrown.
            BossUI bossUI = BuildBossUI(shakeRoot, assets, BossPatrolY, 90f);

            Image bottomLine = UiFactory.AddImage("BottomLine", shakeRoot, null, Color.cyan);
            UiFactory.Anchor((RectTransform)bottomLine.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, BottomLineY), new Vector2(1760f, 2f));
            UiFactory.Themed(bottomLine, ThemeRole.AccentPrimary, 0.35f);

            HudUI hud = BuildHud(shakeRoot, assets, out Button backButton, out Button pauseButton);
            KeyboardVisualUI keyboard = BuildKeyboardBar(shakeRoot, assets);

            Image missFlash = UiFactory.AddImage("MissFlash", root, null, Color.clear);
            UiFactory.Stretch((RectTransform)missFlash.transform);

            AddScanlines(root);

            // Below the end panels in sibling order: the interstitial plays first, then the ordinary
            // level-complete panel fades in over it.
            RankUpUI rankUp = BuildRankUpPanel(root, assets);

            // The campaign finale replaces the level-complete panel entirely on the last level, so
            // it sits above the end panels — nothing is meant to draw over it.
            FinaleUI finale = BuildFinalePanel(root, assets);

            GameEndPanel levelCompletePanel = BuildEndPanel(root, fonts, "LevelCompletePanel", "LEVEL COMPLETE");
            GameEndPanel gameOverPanel = BuildEndPanel(root, fonts, "GameOverPanel", "SYSTEM FAILURE");
            GameEndPanel pausePanel = BuildEndPanel(root, fonts, "PausePanel", "PAUSED");
            finale.transform.SetAsLastSibling();

            var systems = new GameObject("GameSystems",
                typeof(WordSpawner), typeof(InputRouter), typeof(MistakeTracker),
                typeof(BossController), typeof(LevelRunner));

            var spawner = systems.GetComponent<WordSpawner>();
            using (var wiring = new Wiring(spawner))
            {
                wiring.Ref("wordLayer", wordLayer)
                      .Ref("wordPrefab", assets.Prefabs.WordItem.GetComponent<WordController>())
                      .Float("spawnY", SpawnY)
                      .Float("bottomY", BottomLineY)
                      .Float("sidePadding", 120f)
                      .Int("lanes", 5);
            }

            var router = systems.GetComponent<InputRouter>();
            using (var wiring = new Wiring(router))
            {
                wiring.Ref("spawner", spawner);
            }

            var mistakes = systems.GetComponent<MistakeTracker>();
            using (var wiring = new Wiring(mistakes))
            {
                wiring.Int("maxMisses", assets.Tuning.maxMisses);
            }

            var boss = systems.GetComponent<BossController>();

            var runner = systems.GetComponent<LevelRunner>();
            using (var wiring = new Wiring(runner))
            {
                wiring.Ref("spawner", spawner)
                      .Ref("router", router)
                      .Ref("mistakes", mistakes)
                      .Ref("boss", boss)
                      .Ref("keyboard", keyboard)
                      .Ref("hud", hud)
                      .Ref("bossUI", bossUI)
                      .Ref("rankUp", rankUp)
                      .Ref("finale", finale)
                      .Ref("gameOverPanel", gameOverPanel)
                      .Ref("levelCompletePanel", levelCompletePanel)
                      .Ref("pausePanel", pausePanel)
                      .Ref("shakeRoot", shakeRoot)
                      .Ref("missFlash", missFlash)
                      .Ref("clearBurst", clearBurst);
            }

            // Wired here rather than in LevelRunner.Awake so the button keeps working if the
            // hierarchy is rearranged by hand later.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(backButton.onClick, runner.QuitToHome);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(pauseButton.onClick, runner.TogglePause);

            SaveScene(scene, "Game");
        }

        /// <summary>
        /// The boss layer. The authored creature sprites and fonts are wired here — the body itself
        /// is assembled by <see cref="BossUI"/> at runtime, because which creature appears isn't
        /// known until a level starts.
        /// </summary>
        private static BossUI BuildBossUI(Transform parent, AssetSet assets, float patrolY, float margin)
        {
            RectTransform layer = UiFactory.AddRect("BossLayer", parent);
            UiFactory.Stretch(layer);

            var bossUI = layer.gameObject.AddComponent<BossUI>();
            using (var wiring = new Wiring(bossUI))
            {
                wiring.RefArray("rankSprites", assets.BossSprites)
                      .Ref("keySprite", UiFactory.RoundedSprite)
                      .Ref("glowSprite", UiFactory.SoftSprite)
                      .Ref("circleSprite", UiFactory.CircleSprite)
                      .Ref("sigilFont", assets.Fonts.Display)
                      .Ref("labelFont", assets.Fonts.Ui)
                      .Float("patrolCentreY", patrolY)
                      .Float("edgeMargin", margin);
            }

            return bossUI;
        }

        private static HudUI BuildHud(Transform parent, AssetSet assets, out Button backButton,
            out Button pauseButton)
        {
            FontSet fonts = assets.Fonts;

            RectTransform hudRoot = UiFactory.AddRect("HUD", parent);
            UiFactory.StretchHorizontal(hudRoot, 130f, 0f, fromTop: true);

            backButton = UiFactory.AddButton("BackButton", hudRoot, "HOME", fonts.Ui, 24f,
                new Color(1f, 1f, 1f, 0.10f), Color.white, out TextMeshProUGUI backLabel, out Image backFrame);
            UiFactory.Anchor((RectTransform)backButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -28f), new Vector2(130f, 56f));
            UiFactory.Themed(backFrame, ThemeRole.TextMuted, 0.25f);
            UiFactory.Themed(backLabel, ThemeRole.TextMuted);

            pauseButton = UiFactory.AddButton("PauseButton", hudRoot, "PAUSE", fonts.Ui, 24f,
                new Color(1f, 1f, 1f, 0.10f), Color.white, out TextMeshProUGUI pauseLabel, out Image pauseFrame);
            UiFactory.Anchor((RectTransform)pauseButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(180f, -28f), new Vector2(130f, 56f));
            UiFactory.Themed(pauseFrame, ThemeRole.TextMuted, 0.25f);
            UiFactory.Themed(pauseLabel, ThemeRole.TextMuted);

            // Boss rank badge, sitting between the buttons and the season label.
            Image rankFrame = UiFactory.AddImage("RankFrame", hudRoot, UiFactory.RoundedSprite,
                new Color(1f, 1f, 1f, 0.2f));
            UiFactory.Anchor((RectTransform)rankFrame.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(324f, -28f), new Vector2(56f, 56f));

            TextMeshProUGUI rankLabel = UiFactory.AddText("RankLabel", rankFrame.transform, "E", fonts.Display,
                30f, TextAlignmentOptions.Center, Color.white);
            UiFactory.Stretch((RectTransform)rankLabel.transform);

            TextMeshProUGUI levelLabel = UiFactory.AddText("LevelLabel", hudRoot, "SPRING · 01", fonts.Display,
                32f, TextAlignmentOptions.Left, Color.white);
            UiFactory.Anchor((RectTransform)levelLabel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(392f, -28f), new Vector2(340f, 56f));
            UiFactory.Themed(levelLabel, ThemeRole.AccentPrimary);

            // Centre bar is the boss's health — it is the level's win condition.
            TextMeshProUGUI healthLabel = UiFactory.AddText("HealthLabel", hudRoot, "HP 0 / 0", fonts.Ui, 32f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)healthLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(400f, 46f));
            UiFactory.Themed(healthLabel, ThemeRole.TextPrimary);

            Image healthTrack = UiFactory.AddImage("HealthTrack", hudRoot, UiFactory.RoundedSprite,
                new Color(1f, 1f, 1f, 0.12f));
            UiFactory.Anchor((RectTransform)healthTrack.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(560f, 12f));

            Image healthFill = UiFactory.AddImage("HealthFill", healthTrack.transform,
                UiFactory.RoundedSprite, Color.cyan);
            UiFactory.Stretch((RectTransform)healthFill.transform);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFill.fillAmount = 1f;
            UiFactory.Themed(healthFill, ThemeRole.AccentAlert);

            RectTransform pips = UiFactory.AddRect("MissPips", hudRoot);
            UiFactory.Anchor(pips, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -28f), new Vector2(180f, 40f));

            var pipLayout = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            pipLayout.spacing = 10f;
            pipLayout.childAlignment = TextAnchor.MiddleRight;
            pipLayout.childControlWidth = false;
            pipLayout.childControlHeight = false;
            pipLayout.childForceExpandWidth = false;
            pipLayout.childForceExpandHeight = false;

            TextMeshProUGUI scoreLabel = UiFactory.AddText("ScoreLabel", hudRoot, "0", fonts.Ui, 32f,
                TextAlignmentOptions.Right, Color.white);
            UiFactory.Anchor((RectTransform)scoreLabel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -76f), new Vector2(320f, 44f));
            UiFactory.Themed(scoreLabel, ThemeRole.AccentSecondary);

            var hud = hudRoot.gameObject.AddComponent<HudUI>();
            using (var wiring = new Wiring(hud))
            {
                wiring.Ref("levelLabel", levelLabel)
                      .Ref("rankLabel", rankLabel)
                      .Ref("rankFrame", rankFrame)
                      .Ref("healthLabel", healthLabel)
                      .Ref("scoreLabel", scoreLabel)
                      .Ref("healthFill", healthFill)
                      .Ref("missPipContainer", pips)
                      .Ref("missPipPrefab", assets.Prefabs.MissPip.GetComponent<Image>());
            }

            return hud;
        }

        private static KeyboardVisualUI BuildKeyboardBar(Transform parent, AssetSet assets)
        {
            Image panel = UiFactory.AddImage("KeyboardBar", parent, UiFactory.RoundedSprite,
                new Color(0f, 0f, 0f, 0.45f));
            UiFactory.StretchHorizontal((RectTransform)panel.transform, 268f, 16f, fromTop: false, sideMargin: 400f);

            RectTransform rows = UiFactory.AddRect("Rows", panel.transform);
            UiFactory.Stretch(rows, 20f, 20f, 16f, 16f);

            var keyboard = panel.gameObject.AddComponent<KeyboardVisualUI>();
            using (var wiring = new Wiring(keyboard))
            {
                wiring.Ref("keyPrefab", assets.Prefabs.KeyboardKey.GetComponent<KeyboardKeyView>())
                      .Ref("rowsParent", rows)
                      .Float("keySize", 74f)
                      .Float("keySpacing", 9f)
                      .Float("rowSpacing", 9f)
                      .FloatArray("rowIndents", 0f, 30f, 76f);
            }

            return keyboard;
        }

        /// <summary>
        /// The rank-clear interstitial: title, motivational line, and a live preview of the next
        /// rank's boss — a second <see cref="BossUI"/> confined to a narrow strip mid-screen.
        /// </summary>
        private static RankUpUI BuildRankUpPanel(Transform parent, AssetSet assets)
        {
            FontSet fonts = assets.Fonts;

            RectTransform root = UiFactory.AddRect("RankUpPanel", parent);
            UiFactory.Stretch(root);

            CanvasGroup group = UiFactory.AddCanvasGroup(root.gameObject, 0f, false, false);

            Image dim = UiFactory.AddImage("Dim", root, null, new Color(0f, 0f, 0f, 0.82f), raycastTarget: true);
            UiFactory.Stretch((RectTransform)dim.transform);

            TextMeshProUGUI title = UiFactory.AddText("Title", root, "RANK D CLEARED", fonts.Display, 72f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 300f), new Vector2(1400f, 92f));

            TextMeshProUGUI motivation = UiFactory.AddText("Motivation", root, "", fonts.Ui, 30f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)motivation.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 218f), new Vector2(1400f, 44f));

            // The teaser strip. The preview BossUI patrols the width of this rect, so it stays a
            // caged exhibit rather than wandering the whole screen.
            RectTransform preview = UiFactory.AddRect("BossPreview", root);
            UiFactory.Anchor(preview, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(760f, 360f));

            BossUI previewBoss = BuildBossUI(preview, assets, 0f, 60f);

            TextMeshProUGUI next = UiFactory.AddText("NextLabel", root, "", fonts.Ui, 30f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)next.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -268f), new Vector2(1400f, 44f));

            Button continueButton = UiFactory.AddButton("ContinueButton", root, "CONTINUE", fonts.Ui, 28f,
                new Color(1f, 1f, 1f, 0.10f), Color.white, out TextMeshProUGUI continueLabel,
                out Image continueFrame);
            UiFactory.Anchor((RectTransform)continueButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f), new Vector2(420f, 70f));
            UiFactory.Themed(continueFrame, ThemeRole.TextMuted, 0.2f);

            // Last child: the season-handover flash must wash over everything at once.
            Image flash = UiFactory.AddImage("Flash", root, null, Color.clear);
            UiFactory.Stretch((RectTransform)flash.transform);

            var rankUp = root.gameObject.AddComponent<RankUpUI>();
            using (var wiring = new Wiring(rankUp))
            {
                wiring.Ref("group", group)
                      .Ref("dim", dim)
                      .Ref("flash", flash)
                      .Ref("titleLabel", title)
                      .Ref("motivationLabel", motivation)
                      .Ref("nextLabel", next)
                      .Ref("previewRoot", preview)
                      .Ref("previewBoss", previewBoss)
                      .Ref("continueButton", continueButton)
                      .Ref("continueLabel", continueLabel);
            }

            root.gameObject.SetActive(false);
            return rankUp;
        }

        /// <summary>
        /// The campaign finale: the hero coronation, the word-by-word tribute, the five defeated
        /// creatures taking a bow, and the new-journey / home choice.
        /// </summary>
        private static FinaleUI BuildFinalePanel(Transform parent, AssetSet assets)
        {
            FontSet fonts = assets.Fonts;

            RectTransform root = UiFactory.AddRect("FinalePanel", parent);
            UiFactory.Stretch(root);

            CanvasGroup group = UiFactory.AddCanvasGroup(root.gameObject, 0f, false, false);

            Image dim = UiFactory.AddImage("Dim", root, null, new Color(0f, 0f, 0f, 0.9f), raycastTarget: true);
            UiFactory.Stretch((RectTransform)dim.transform);

            TextMeshProUGUI title = UiFactory.AddText("Title", root, "ALL SEASONS CONQUERED", fonts.Display,
                76f, TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 330f), new Vector2(1500f, 96f));

            TextMeshProUGUI hero = UiFactory.AddText("HeroLine", root, "YOU ARE THE HERO", fonts.Display,
                52f, TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)hero.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 236f), new Vector2(1400f, 66f));

            TextMeshProUGUI tribute = UiFactory.AddText("Tribute", root, "", fonts.Ui, 30f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)tribute.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 118f), new Vector2(1240f, 130f));
            tribute.textWrappingMode = TextWrappingModes.Normal;

            TextMeshProUGUI noLevels = UiFactory.AddText("NoLevelsLine", root, "", fonts.Ui, 26f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)noLevels.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 18f), new Vector2(1240f, 36f));

            RectTransform trophies = UiFactory.AddRect("Trophies", root);
            UiFactory.Anchor(trophies, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -110f), new Vector2(700f, 100f));

            TextMeshProUGUI score = UiFactory.AddText("ScoreLine", root, "", fonts.Ui, 26f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)score.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -210f), new Vector2(900f, 34f));

            Button newJourney = UiFactory.AddButton("NewJourneyButton", root, "START A NEW JOURNEY",
                fonts.Display, 32f, new Color(1f, 1f, 1f, 0.14f), Color.white,
                out TextMeshProUGUI newJourneyLabel, out Image newJourneyFrame);
            UiFactory.Anchor((RectTransform)newJourney.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 186f), new Vector2(560f, 84f));
            UiFactory.Themed(newJourneyFrame, ThemeRole.AccentPrimary, 0.2f);
            UiFactory.Themed(newJourneyLabel, ThemeRole.AccentPrimary);

            Button home = UiFactory.AddButton("HomeButton", root, "HOME", fonts.Ui, 28f,
                new Color(1f, 1f, 1f, 0.08f), Color.white, out TextMeshProUGUI homeLabel, out Image homeFrame);
            UiFactory.Anchor((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 100f), new Vector2(560f, 70f));
            UiFactory.Themed(homeFrame, ThemeRole.TextMuted, 0.2f);
            UiFactory.Themed(homeLabel, ThemeRole.TextMuted);

            // Last child: the coronation flash washes over everything at once.
            Image flash = UiFactory.AddImage("Flash", root, null, Color.clear);
            UiFactory.Stretch((RectTransform)flash.transform);

            var finale = root.gameObject.AddComponent<FinaleUI>();
            using (var wiring = new Wiring(finale))
            {
                wiring.Ref("group", group)
                      .Ref("dim", dim)
                      .Ref("flash", flash)
                      .Ref("titleLabel", title)
                      .Ref("heroLabel", hero)
                      .Ref("tributeLabel", tribute)
                      .Ref("noLevelsLabel", noLevels)
                      .Ref("scoreLabel", score)
                      .Ref("trophyRoot", trophies)
                      .Ref("newJourneyButton", newJourney)
                      .Ref("newJourneyLabel", newJourneyLabel)
                      .Ref("homeButton", home)
                      .RefArray("rankSprites", assets.BossSprites);
            }

            root.gameObject.SetActive(false);
            return finale;
        }

        private static GameEndPanel BuildEndPanel(Transform parent, FontSet fonts, string name, string defaultTitle)
        {
            RectTransform root = UiFactory.AddRect(name, parent);
            UiFactory.Stretch(root);

            CanvasGroup group = UiFactory.AddCanvasGroup(root.gameObject, 0f, false, false);

            Image dim = UiFactory.AddImage("Dim", root, null, new Color(0f, 0f, 0f, 0.78f), raycastTarget: true);
            UiFactory.Stretch((RectTransform)dim.transform);

            Image card = UiFactory.AddImage("Card", root, UiFactory.RoundedSprite, new Color(0.05f, 0.06f, 0.1f, 0.96f));
            UiFactory.Anchor((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 540f));

            TextMeshProUGUI title = UiFactory.AddText("Title", card.transform, defaultTitle, fonts.Display, 54f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(760f, 70f));
            UiFactory.Themed(title, ThemeRole.AccentPrimary);

            TextMeshProUGUI subtitle = UiFactory.AddText("Subtitle", card.transform, "", fonts.Ui, 30f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)subtitle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(760f, 46f));
            UiFactory.Themed(subtitle, ThemeRole.TextMuted);

            // Buttons live in a layout group so a panel with two of them closes the gap instead of
            // leaving a hole where the third would be.
            RectTransform buttons = UiFactory.AddRect("Buttons", card.transform);
            UiFactory.Anchor(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(560f, 296f));

            var buttonLayout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 12f;
            buttonLayout.childAlignment = TextAnchor.LowerCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            Button primary = UiFactory.AddButton("PrimaryButton", buttons, "NEXT", fonts.Display, 34f,
                new Color(1f, 1f, 1f, 0.14f), Color.white, out TextMeshProUGUI primaryLabel, out Image primaryFrame);
            SizeButton(primary, 520f, 88f);
            UiFactory.Themed(primaryFrame, ThemeRole.AccentPrimary, 0.2f);
            UiFactory.Themed(primaryLabel, ThemeRole.AccentPrimary);

            Button secondary = UiFactory.AddButton("SecondaryButton", buttons, "RETRY", fonts.Ui, 28f,
                new Color(1f, 1f, 1f, 0.08f), Color.white, out TextMeshProUGUI secondaryLabel,
                out Image secondaryFrame);
            SizeButton(secondary, 520f, 72f);
            UiFactory.Themed(secondaryFrame, ThemeRole.TextMuted, 0.2f);
            UiFactory.Themed(secondaryLabel, ThemeRole.TextMuted);

            Button tertiary = UiFactory.AddButton("TertiaryButton", buttons, "HOME", fonts.Ui, 28f,
                new Color(1f, 1f, 1f, 0.08f), Color.white, out TextMeshProUGUI tertiaryLabel,
                out Image tertiaryFrame);
            SizeButton(tertiary, 520f, 72f);
            UiFactory.Themed(tertiaryFrame, ThemeRole.TextMuted, 0.2f);
            UiFactory.Themed(tertiaryLabel, ThemeRole.TextMuted);

            var panel = root.gameObject.AddComponent<GameEndPanel>();
            using (var wiring = new Wiring(panel))
            {
                wiring.Ref("group", group)
                      .Ref("titleLabel", title)
                      .Ref("subtitleLabel", subtitle)
                      .Ref("primaryButton", primary)
                      .Ref("primaryLabel", primaryLabel)
                      .Ref("secondaryButton", secondary)
                      .Ref("secondaryLabel", secondaryLabel)
                      .Ref("tertiaryButton", tertiary)
                      .Ref("tertiaryLabel", tertiaryLabel)
                      .Ref("card", (RectTransform)card.transform);
            }

            root.gameObject.SetActive(false);
            return panel;
        }

        #endregion
    }
}
