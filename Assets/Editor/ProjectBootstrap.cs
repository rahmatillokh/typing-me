using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using TypingMe.Audio;
using TypingMe.Core;
using TypingMe.Data;
using TypingMe.Fx;
using TypingMe.Gameplay;
using TypingMe.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TypingMe.EditorTools
{
    internal sealed class FontSet
    {
        /// <summary>Orbitron — headers (§8).</summary>
        public TMP_FontAsset Display;

        /// <summary>Rajdhani — HUD and UI (§8).</summary>
        public TMP_FontAsset Ui;

        /// <summary>Share Tech Mono — falling words (§8).</summary>
        public TMP_FontAsset Mono;
    }

    internal sealed class PrefabSet
    {
        public GameObject WordItem;
        public GameObject KeyboardKey;
        public GameObject LevelButton;
        public GameObject MissPip;
        public GameObject PersistentServices;
    }

    internal sealed class AssetSet
    {
        public WordBankSO WordBank;
        public LevelTuningSO Tuning;
        public BossTuningSO BossTuning;
        public AuthorInfoSO AuthorInfo;
        public ThemeSO[] Themes;

        /// <summary>Authored boss art, indexed by <see cref="BossRank"/>: D, C, B, A, S.</summary>
        public Sprite[] BossSprites;

        public FontSet Fonts;
        public PrefabSet Prefabs;
        public UnityEngine.Rendering.VolumeProfile PostProcess;
    }

    /// <summary>
    /// Generates every asset, prefab and scene the project needs, from code.
    /// </summary>
    /// <remarks>
    /// Scenes and prefabs are built programmatically rather than hand-authored so the whole project can
    /// be regenerated deterministically and reviewed as source. Re-running is safe: existing assets are
    /// overwritten in place.
    /// Run from the menu, or headless via
    /// <c>-executeMethod TypingMe.EditorTools.ProjectBootstrap.RunAll</c>.
    /// </remarks>
    public static class ProjectBootstrap
    {
        private const string DataDir = "Assets/Data";
        private const string ThemesDir = "Assets/Data/Themes";
        private const string PrefabsDir = "Assets/Prefabs";
        private const string ResourcesDir = "Assets/Resources";
        private const string ScenesDir = "Assets/Scenes";
        private const string TmpFontsDir = "Assets/Fonts/TMP";
        private const string SettingsDir = "Assets/Settings";

        /// <summary>
        /// Authored pixel-art boss sprites. Unlike everything else the bootstrap touches, these are
        /// NOT generated — they are hand-made art; the bootstrap only pins their import settings.
        /// </summary>
        private const string BossArtDir = "Assets/Art/Bosses";

        /// <summary>
        /// The app icon, composed from the dragon sprite by docs/tools/make_icon.py. The bootstrap
        /// assigns it as the player's default icon so both the .app and the .exe carry it.
        /// </summary>
        private const string AppIconPath = "Assets/Art/Icon/AppIcon.png";

        private const string WordListPath = "Assets/Data/WordLists/google-10000-english-usa-no-swears.txt";
        private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        /// <summary>
        /// Tuning assets kept as-is by the last run.
        /// </summary>
        /// <remarks>
        /// Balance assets are deliberately create-only so a playtest tweak in the inspector survives
        /// a rebuild. The cost is that they silently stop matching the field initialisers in code —
        /// which has already caused one stale-cap bug — so the run reports what it kept, and
        /// <see cref="ResetTuningAssets"/> forces them back to the code defaults.
        /// </remarks>
        private static readonly List<string> KeptTuningAssets = new List<string>();

        [MenuItem("Typing Me/Reset Tuning Assets to Code Defaults", false, 2)]
        public static void ResetTuningAssets()
        {
            foreach (string path in new[] { DataDir + "/LevelTuning.asset", DataDir + "/BossTuning.asset" })
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
                    AssetDatabase.DeleteAsset(path);

            AssetDatabase.SaveAssets();
            Debug.Log("[Typing Me] Tuning assets deleted; rebuilding them from code defaults.");

            RunAll();
        }

        [MenuItem("Typing Me/Rebuild Project Assets", false, 0)]
        public static void RunAll()
        {
            KeptTuningAssets.Clear();

            try
            {
                EditorUtility.DisplayProgressBar("Typing Me", "Creating folders…", 0f);
                EnsureFolders();

                EditorUtility.DisplayProgressBar("Typing Me", "Generating TMP font assets…", 0.1f);
                FontSet fonts = CreateFonts();

                EditorUtility.DisplayProgressBar("Typing Me", "Creating data assets…", 0.3f);
                var assets = new AssetSet
                {
                    Fonts = fonts,
                    Themes = CreateThemes(),
                    BossSprites = LoadBossSprites(),
                    WordBank = CreateWordBank(),
                    Tuning = CreateTuning(),
                    BossTuning = CreateBossTuning(),
                    AuthorInfo = CreateAuthorInfo(),
                    PostProcess = CreatePostProcessProfile()
                };

                // Write the data assets out before anything references them. Deliberately no
                // Refresh() here: a reimport destroys the instances just loaded and every reference
                // taken afterwards serialises as null.
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("Typing Me", "Building prefabs…", 0.5f);
                assets.Prefabs = CreatePrefabs(assets);

                EditorUtility.DisplayProgressBar("Typing Me", "Building scenes…", 0.75f);
                SceneBuilder.BuildAll(assets);

                EditorUtility.DisplayProgressBar("Typing Me", "Finalising…", 0.95f);
                ConfigureBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (KeptTuningAssets.Count == 0)
                {
                    Debug.Log("[Typing Me] Project assets rebuilt.");
                }
                else
                {
                    Debug.Log($"[Typing Me] Project assets rebuilt. Kept existing tuning: " +
                              $"{string.Join(", ", KeptTuningAssets)} — these do NOT track the code " +
                              "defaults; use Typing Me/Reset Tuning Assets to Code Defaults if they drifted.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region Folders

        private static void EnsureFolders()
        {
            foreach (string path in new[]
                     {
                         DataDir, ThemesDir, PrefabsDir, ResourcesDir, ScenesDir,
                         "Assets/Fonts", TmpFontsDir, SettingsDir,
                         "Assets/Art", BossArtDir, "Assets/Art/Icon"
                     })
            {
                EnsureFolder(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        #endregion

        #region Fonts

        private static FontSet CreateFonts()
        {
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);

            var set = new FontSet
            {
                Display = CreateFontAsset("Assets/Fonts/Orbitron-Variable.ttf", $"{TmpFontsDir}/Orbitron SDF.asset"),
                Ui = CreateFontAsset("Assets/Fonts/Rajdhani-SemiBold.ttf", $"{TmpFontsDir}/Rajdhani SDF.asset"),
                Mono = CreateFontAsset("Assets/Fonts/ShareTechMono-Regular.ttf", $"{TmpFontsDir}/ShareTechMono SDF.asset")
            };

            // Never leave a null font: TMP text with no font renders nothing at all.
            set.Display ??= fallback;
            set.Ui ??= fallback;
            set.Mono ??= fallback;

            if (set.Display == null)
                Debug.LogError("[Typing Me] No usable TMP font asset — is TMP Essential Resources imported?");

            return set;
        }

        private static TMP_FontAsset CreateFontAsset(string ttfPath, string assetPath, int samplingSize = 90)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (source == null)
            {
                Debug.LogWarning($"[Typing Me] Font missing at {ttfPath}; falling back to the TMP default.");
                return null;
            }

            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                    source, samplingSize, 9, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

                if (asset == null)
                {
                    Debug.LogWarning($"[Typing Me] TMP could not build a font asset from {ttfPath}.");
                    return null;
                }

                asset.name = Path.GetFileNameWithoutExtension(assetPath);
                AssetDatabase.CreateAsset(asset, assetPath);

                // The atlas texture and material are created in memory; without adopting them as
                // sub-assets they'd be dropped on the next domain reload.
                if (asset.atlasTextures is { Length: > 0 } && asset.atlasTextures[0] != null)
                {
                    asset.atlasTextures[0].name = $"{asset.name} Atlas";
                    AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                }

                if (asset.material != null)
                {
                    asset.material.name = $"{asset.name} Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return asset;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Typing Me] Font asset generation failed for {ttfPath}: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Data assets

        /// <summary>
        /// One palette per season, indexed by <see cref="Season"/>. Each is a hard tonal turn from
        /// the one before it, so crossing a season boundary visibly restyles the whole game.
        /// </summary>
        private static ThemeSO[] CreateThemes()
        {
            return new[]
            {
                // Spring — new growth: neon leaf-green over a mossy near-black, blossom pink accents.
                CreateTheme("spring", "Spring",
                    background: new Color32(0x07, 0x14, 0x0D, 0xFF),
                    backgroundLow: new Color32(0x02, 0x0A, 0x07, 0xFF),
                    primary: new Color32(0x3B, 0xFF, 0x9E, 0xFF),
                    secondary: new Color32(0xFF, 0x6E, 0xC7, 0xFF)),

                // Summer — high sun: amber gold over a heat-baked brown-black, pool-water cyan accents.
                CreateTheme("summer", "Summer",
                    background: new Color32(0x17, 0x10, 0x06, 0xFF),
                    backgroundLow: new Color32(0x0B, 0x06, 0x02, 0xFF),
                    primary: new Color32(0xFF, 0xB6, 0x27, 0xFF),
                    secondary: new Color32(0x00, 0xD9, 0xFF, 0xFF)),

                // Autumn — ember dusk: burnt orange over an ashen black, harvest gold accents.
                CreateTheme("autumn", "Autumn",
                    background: new Color32(0x15, 0x0A, 0x05, 0xFF),
                    backgroundLow: new Color32(0x0A, 0x04, 0x02, 0xFF),
                    primary: new Color32(0xFF, 0x7B, 0x39, 0xFF),
                    secondary: new Color32(0xFF, 0xD2, 0x3F, 0xFF)),

                // Winter — deep frost: ice cyan over a midnight blue-black, aurora violet accents.
                CreateTheme("winter", "Winter",
                    background: new Color32(0x06, 0x0B, 0x16, 0xFF),
                    backgroundLow: new Color32(0x02, 0x04, 0x0B, 0xFF),
                    primary: new Color32(0x6F, 0xD8, 0xFF, 0xFF),
                    secondary: new Color32(0xB7, 0x9C, 0xFF, 0xFF))
            };
        }

        private static ThemeSO CreateTheme(string id, string displayName, Color background, Color backgroundLow,
            Color primary, Color secondary)
        {
            string path = $"{ThemesDir}/Theme_{id}.asset";

            var theme = AssetDatabase.LoadAssetAtPath<ThemeSO>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeSO>();
                AssetDatabase.CreateAsset(theme, path);
            }

            theme.themeId = id;
            theme.displayName = displayName;
            theme.backgroundTop = background;
            theme.backgroundBottom = backgroundLow;
            theme.accentPrimary = primary;
            theme.accentSecondary = secondary;
            theme.accentAlert = new Color32(0xFF, 0x38, 0x60, 0xFF);
            theme.keyboardGlow = primary;
            theme.grid = new Color(primary.r, primary.g, primary.b, 0.10f);
            theme.textPrimary = new Color32(0xE8, 0xFA, 0xFF, 0xFF);
            theme.textMuted = new Color32(0x7A, 0x8C, 0xA6, 0xFF);
            theme.panel = new Color(background.r, background.g, background.b, 0.88f);

            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>
        /// Loads the authored boss sprites, one per rank, pinning their import settings first so a
        /// fresh checkout imports them identically: pixel art wants point filtering, no mipmaps and
        /// no compression, none of which are Unity's defaults.
        /// </summary>
        private static Sprite[] LoadBossSprites()
        {
            // Index order must match the BossRank enum: D, C, B, A, S.
            var files = new[]
            {
                "D-shilliq.png", "C-tikan.png", "B-jodugar.png", "A-golem.png", "S-ajdar.png"
            };

            var sprites = new Sprite[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                string path = $"{BossArtDir}/{files[i]}";
                ConfigurePixelArtImporter(path);

                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[i] == null)
                    Debug.LogError($"[Typing Me] Boss sprite missing at {path} — the boss for rank " +
                                   $"{(BossRank)i} will not render. This art is authored, not generated.");
            }

            return sprites;
        }

        private static void ConfigurePixelArtImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = importer.textureType != TextureImporterType.Sprite ||
                         importer.spriteImportMode != SpriteImportMode.Single ||
                         importer.filterMode != FilterMode.Point ||
                         importer.textureCompression != TextureImporterCompression.Uncompressed ||
                         importer.mipmapEnabled || !importer.alphaIsTransparency;

            if (!dirty) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        private static WordBankSO CreateWordBank()
        {
            const string path = DataDir + "/WordBank.asset";

            var bank = AssetDatabase.LoadAssetAtPath<WordBankSO>(path);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<WordBankSO>();
                AssetDatabase.CreateAsset(bank, path);
            }

            var list = AssetDatabase.LoadAssetAtPath<TextAsset>(WordListPath);
            if (list == null)
                Debug.LogWarning($"[Typing Me] Word list not found at {WordListPath}; the bank will use its fallback words.");

            using (var wiring = new Wiring(bank))
            {
                wiring.Ref("sourceList", list)
                      .Enum("rankingMode", (int)WordRankingMode.FrequencyRank);
            }

            EditorUtility.SetDirty(bank);
            return bank;
        }

        private static LevelTuningSO CreateTuning()
        {
            const string path = DataDir + "/LevelTuning.asset";

            var tuning = AssetDatabase.LoadAssetAtPath<LevelTuningSO>(path);
            if (tuning != null)
            {
                KeptTuningAssets.Add(path);
                return tuning;
            }

            // Field initialisers already hold the §5 starting values.
            tuning = ScriptableObject.CreateInstance<LevelTuningSO>();
            AssetDatabase.CreateAsset(tuning, path);
            EditorUtility.SetDirty(tuning);

            return tuning;
        }

        private static BossTuningSO CreateBossTuning()
        {
            const string path = DataDir + "/BossTuning.asset";

            var tuning = AssetDatabase.LoadAssetAtPath<BossTuningSO>(path);
            if (tuning != null)
            {
                KeptTuningAssets.Add(path);
                return tuning;
            }

            // Field initialisers hold the per-rank profiles (D→A, plus S for the season boss).
            tuning = ScriptableObject.CreateInstance<BossTuningSO>();
            AssetDatabase.CreateAsset(tuning, path);
            EditorUtility.SetDirty(tuning);

            return tuning;
        }

        /// <summary>
        /// Creates the Author asset empty, and never overwrites it. Its contents are facts about a
        /// real person, so they are filled in by hand — the UI shows an explicit "not filled in"
        /// notice until then rather than inventing anything.
        /// </summary>
        /// <summary>
        /// Where the Author asset lives. Public so the scene builder can re-load it at the moment it
        /// is needed: opening a new scene unloads assets that nothing references yet, and this one
        /// has no consumer until the Menu scene's Author panel is built.
        /// </summary>
        internal const string AuthorInfoAssetPath = DataDir + "/AuthorInfo.asset";

        private static AuthorInfoSO CreateAuthorInfo()
        {
            const string path = AuthorInfoAssetPath;

            var info = AssetDatabase.LoadAssetAtPath<AuthorInfoSO>(path);
            if (info != null) return info;

            info = ScriptableObject.CreateInstance<AuthorInfoSO>();
            AssetDatabase.CreateAsset(info, path);
            EditorUtility.SetDirty(info);

            Debug.Log($"[Typing Me] Created {path} — fill in name, bio, links and donate URL; " +
                      "the Author tab shows placeholders until you do.");

            return info;
        }

        private static UnityEngine.Rendering.VolumeProfile CreatePostProcessProfile()
        {
            const string path = SettingsDir + "/PostProcessProfile.asset";

            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // Bloom carries the neon glow — the single most important effect for the look (§8).
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(0.75f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(Color.white);
            Adopt(bloom, profile);

            var aberration = profile.Add<UnityEngine.Rendering.Universal.ChromaticAberration>(true);
            aberration.intensity.Override(0.12f);
            Adopt(aberration, profile);

            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            Adopt(vignette, profile);

            var grade = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            grade.postExposure.Override(0.15f);
            grade.saturation.Override(12f);
            grade.contrast.Override(10f);
            Adopt(grade, profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            return profile;
        }

        private static void Adopt(UnityEngine.Rendering.VolumeComponent component,
            UnityEngine.Rendering.VolumeProfile profile)
        {
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        #endregion

        #region Prefabs

        private static PrefabSet CreatePrefabs(AssetSet assets)
        {
            return new PrefabSet
            {
                WordItem = BuildWordItem(assets.Fonts),
                KeyboardKey = BuildKeyboardKey(assets.Fonts),
                LevelButton = BuildLevelButton(assets.Fonts),
                MissPip = BuildMissPip(),
                PersistentServices = BuildPersistentServices(assets)
            };
        }

        private static GameObject SavePrefab(GameObject instance, string name, string directory = PrefabsDir)
        {
            string path = $"{directory}/{name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return saved;
        }

        private static GameObject BuildWordItem(FontSet fonts)
        {
            var root = new GameObject("WordItem",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI),
                typeof(CanvasGroup), typeof(WordController));

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(520f, 76f);

            var label = root.GetComponent<TextMeshProUGUI>();
            if (fonts.Mono != null) label.font = fonts.Mono;
            label.fontSize = 54f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.color = Color.white;

            using (var wiring = new Wiring(root.GetComponent<WordController>()))
            {
                wiring.Ref("label", label)
                      .Ref("group", root.GetComponent<CanvasGroup>());
            }

            return SavePrefab(root, "WordItem");
        }

        private static GameObject BuildKeyboardKey(FontSet fonts)
        {
            var root = new GameObject("KeyboardKey",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(LayoutElement), typeof(KeyboardKeyView));

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(74f, 74f);

            var background = root.GetComponent<Image>();
            background.sprite = UiFactory.RoundedSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, 0.07f);
            background.raycastTarget = false;

            var layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 74f;
            layout.preferredHeight = 74f;

            Image glow = UiFactory.AddImage("Glow", root.transform, UiFactory.SoftSprite, Color.clear);
            UiFactory.Stretch((RectTransform)glow.transform, -6f, -6f, -6f, -6f);
            glow.enabled = false;

            TextMeshProUGUI label = UiFactory.AddText("Label", root.transform, "A", fonts.Ui, 30f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Stretch((RectTransform)label.transform);

            using (var wiring = new Wiring(root.GetComponent<KeyboardKeyView>()))
            {
                wiring.Ref("background", background)
                      .Ref("glow", glow)
                      .Ref("label", label);
            }

            return SavePrefab(root, "KeyboardKey");
        }

        private static GameObject BuildLevelButton(FontSet fonts)
        {
            var root = new GameObject("LevelButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(LevelButton));

            var rect = (RectTransform)root.transform;

            // GridLayoutGroup drives the real size; this just keeps the prefab sane in isolation.
            rect.sizeDelta = new Vector2(96f, 96f);

            var frame = root.GetComponent<Image>();
            frame.sprite = UiFactory.RoundedSprite;
            frame.type = Image.Type.Sliced;
            frame.color = new Color32(0x00, 0xFF, 0xF2, 0x40);
            frame.raycastTarget = true;

            var button = root.GetComponent<Button>();
            button.targetGraphic = frame;

            TextMeshProUGUI number = UiFactory.AddText("Number", root.transform, "01", fonts.Display, 30f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)number.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 9f), new Vector2(92f, 40f));

            // Boss rank for the level, so the grid reads as a difficulty map (§ seasons).
            TextMeshProUGUI rank = UiFactory.AddText("Rank", root.transform, "E", fonts.Ui, 19f,
                TextAlignmentOptions.Center, Color.white);
            UiFactory.Anchor((RectTransform)rank.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 11f), new Vector2(56f, 22f));

            // Placeholder lock plate — swap for real art in the M5 visual pass.
            Image locked = UiFactory.AddImage("LockedBadge", root.transform, UiFactory.RoundedSprite,
                new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Anchor((RectTransform)locked.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 14f), new Vector2(46f, 10f));

            using (var wiring = new Wiring(root.GetComponent<LevelButton>()))
            {
                wiring.Ref("button", button)
                      .Ref("numberLabel", number)
                      .Ref("rankLabel", rank)
                      .Ref("frame", frame)
                      .Ref("lockedBadge", locked.gameObject);
            }

            return SavePrefab(root, "LevelButton");
        }

        private static GameObject BuildMissPip()
        {
            var root = new GameObject("MissPip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(22f, 22f);

            var image = root.GetComponent<Image>();
            image.sprite = UiFactory.CircleSprite;
            image.color = Color.grey;
            image.raycastTarget = false;

            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 22f;
            layout.preferredHeight = 22f;

            return SavePrefab(root, "MissPip");
        }

        private static GameObject BuildPersistentServices(AssetSet assets)
        {
            var root = new GameObject("PersistentServices",
                typeof(GameManager), typeof(ThemeManager), typeof(AudioManager));

            var music = new GameObject("MusicSource", typeof(AudioSource));
            music.transform.SetParent(root.transform, false);
            AudioSource musicSource = music.GetComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            var sfx = new GameObject("SfxSource", typeof(AudioSource));
            sfx.transform.SetParent(root.transform, false);
            AudioSource sfxSource = sfx.GetComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            using (var wiring = new Wiring(root.GetComponent<GameManager>()))
            {
                wiring.Ref("wordBank", assets.WordBank)
                      .Ref("tuning", assets.Tuning)
                      .Ref("bossTuning", assets.BossTuning);
            }

            using (var wiring = new Wiring(root.GetComponent<ThemeManager>()))
            {
                wiring.RefArray("seasonThemes", assets.Themes);
            }

            using (var wiring = new Wiring(root.GetComponent<AudioManager>()))
            {
                wiring.Ref("musicSource", musicSource)
                      .Ref("sfxSource", sfxSource);
            }

            // Lives in Resources so AppBootstrap can load it before the first scene (§9).
            return SavePrefab(root, "PersistentServices", ResourcesDir);
        }

        #endregion

        private static void ConfigureBuildSettings()
        {
            // Splash first: it is the launch scene and hands off to Menu.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{ScenesDir}/Splash.unity", true),
                new EditorBuildSettingsScene($"{ScenesDir}/Menu.unity", true),
                new EditorBuildSettingsScene($"{ScenesDir}/Game.unity", true)
            };

            PlayerSettings.companyName = string.IsNullOrWhiteSpace(PlayerSettings.companyName)
                ? "Typing Me"
                : PlayerSettings.companyName;

            PlayerSettings.productName = "Typing Me";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = false;

            ConfigureAppIcon();
        }

        /// <summary>
        /// Assigns the default player icon, which Unity scales into the macOS .icns and the
        /// Windows .exe resources at build time.
        /// </summary>
        private static void ConfigureAppIcon()
        {
            ConfigureIconImporter(AppIconPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[Typing Me] App icon missing at {AppIconPath} — builds will use " +
                                 "Unity's default icon. Regenerate it with docs/tools/make_icon.py.");
                return;
            }

            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,
                new[] { icon }, IconKind.Any);
        }

        /// <summary>
        /// The icon is a smooth composite, not pixel art: bilinear filtering and no compression,
        /// so Unity's downscaled icon sizes stay clean.
        /// </summary>
        private static void ConfigureIconImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = importer.textureType != TextureImporterType.Default ||
                         importer.textureCompression != TextureImporterCompression.Uncompressed ||
                         importer.mipmapEnabled || !importer.alphaIsTransparency;

            if (!dirty) return;

            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
