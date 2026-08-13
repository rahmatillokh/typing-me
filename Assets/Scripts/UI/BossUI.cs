using TMPro;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// The boss as a physical presence: a creature that patrols the top of the play area and hurls
    /// the words down at the player.
    /// </summary>
    /// <remarks>
    /// Every rank is a different authored pixel-art creature (Assets/Art/Bosses): D the Slime,
    /// C the Thorn, B the Witch, A the Golem, S the Dragon. The art carries the silhouette; this
    /// component gives each its own size, patrol character and idle motion — the slime squashes,
    /// the thorn wobble-spins, the witch sways as she floats, the golem breathes slow and heavy,
    /// the dragon beats like a wing. A sigil badge hangs under the creature, and the body is the
    /// launch point: <see cref="TypingMe.Gameplay.WordSpawner"/> throws words from wherever it is,
    /// which is what makes its patrol matter rather than being decoration.
    /// </remarks>
    public sealed class BossUI : MonoBehaviour
    {
        [Tooltip("One creature per rank, indexed by (int)BossRank: D, C, B, A, S.")]
        [SerializeField] private Sprite[] rankSprites = new Sprite[5];

        [SerializeField] private Sprite keySprite;
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private TMP_FontAsset sigilFont;
        [SerializeField] private TMP_FontAsset labelFont;

        [Tooltip("Vertical centre of the boss's patrol, in play-area reference px.")]
        [SerializeField] private float patrolCentreY = 366f;

        [Tooltip("Kept clear of the play-area edges so the body never clips out of frame.")]
        [SerializeField] private float edgeMargin = 90f;

        /// <summary>How a rank's creature carries itself. The sprites are square, so size is one number.</summary>
        private readonly struct Gait
        {
            public readonly float Size;
            public readonly float PatrolSpeed;
            public readonly float BobAmplitude;
            public readonly float BobFrequency;

            public Gait(float size, float patrolSpeed, float bobAmplitude, float bobFrequency)
            {
                Size = size;
                PatrolSpeed = patrolSpeed;
                BobAmplitude = bobAmplitude;
                BobFrequency = bobFrequency;
            }
        }

        private static Gait GaitFor(BossRank rank) => rank switch
        {
            //                          size  patrol bob  bobHz
            BossRank.D => new Gait(115f, 85f, 4f, 1.0f), // Slime: slow crawl, squashes instead of bobbing.
            BossRank.C => new Gait(130f, 120f, 8f, 1.3f), // Thorn: quicker, rolling.
            BossRank.B => new Gait(150f, 155f, 13f, 0.9f), // Witch: floaty swoops.
            BossRank.A => new Gait(170f, 130f, 3f, 0.7f), // Golem: heavy, barely leaves the ground.
            BossRank.S => new Gait(205f, 235f, 7f, 1.1f), // Dragon: sweeps the full width fastest.
            _ => new Gait(115f, 85f, 4f, 1.0f)
        };

        private RectTransform _self;
        private RectTransform _body;
        private Image _underglow;
        private Image _creature;
        private RectTransform _creatureRect;
        private Image _badgeRing;
        private Image _badgeFace;
        private TMP_Text _sigil;

        private RectTransform _telegraph;
        private CanvasGroup _telegraphGroup;
        private Image _telegraphFrame;
        private TMP_Text _telegraphLabel;

        private BossDefinition _definition;
        private Gait _gait;
        private Color _tint;

        private bool _active;
        private bool _enraged;
        private float _phase;
        private float _idleTime;
        private float _halfRange;
        private float _hitBoost;
        private float _telegraphRemaining;
        private float _telegraphTotal;

        /// <summary>Where words are thrown from.</summary>
        public RectTransform BodyRect => _body;

        private void Awake()
        {
            _self = (RectTransform)transform;
            BuildSkeleton();
        }

        #region Construction

        private void BuildSkeleton()
        {
            if (_body != null) return;

            // Bind can arrive before Awake when the boss is used as a preview inside another panel.
            if (_self == null) _self = (RectTransform)transform;

            _body = NewRect("BossBody", _self);

            _underglow = NewImage("Underglow", _body, glowSprite);
            _creature = NewImage("Creature", _body, null);
            _creature.type = Image.Type.Simple;
            _creatureRect = (RectTransform)_creature.transform;

            _badgeRing = NewImage("SigilRing", _body, circleSprite);
            _badgeFace = NewImage("SigilFace", _badgeRing.transform, circleSprite);
            Stretch((RectTransform)_badgeFace.transform, 4f);

            var sigilGo = new GameObject("Sigil", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            sigilGo.transform.SetParent(_badgeRing.transform, false);
            _sigil = sigilGo.GetComponent<TextMeshProUGUI>();
            if (sigilFont != null) _sigil.font = sigilFont;
            _sigil.alignment = TextAlignmentOptions.Center;
            _sigil.raycastTarget = false;
            Stretch((RectTransform)_sigil.transform);

            BuildTelegraph();
        }

        private void BuildTelegraph()
        {
            _telegraph = NewRect("Telegraph", _body);
            _telegraph.anchorMin = _telegraph.anchorMax = new Vector2(0.5f, 0f);
            _telegraph.pivot = new Vector2(0.5f, 1f);
            _telegraph.sizeDelta = new Vector2(300f, 40f);

            _telegraphGroup = _telegraph.gameObject.AddComponent<CanvasGroup>();
            _telegraphGroup.alpha = 0f;
            _telegraphGroup.blocksRaycasts = false;
            _telegraphGroup.interactable = false;

            _telegraphFrame = NewImage("Frame", _telegraph, keySprite);
            Stretch((RectTransform)_telegraphFrame.transform);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(_telegraph, false);
            _telegraphLabel = labelGo.GetComponent<TextMeshProUGUI>();
            if (labelFont != null) _telegraphLabel.font = labelFont;
            _telegraphLabel.alignment = TextAlignmentOptions.Center;
            _telegraphLabel.fontSize = 22f;
            _telegraphLabel.raycastTarget = false;
            Stretch((RectTransform)_telegraphLabel.transform);

            _telegraph.gameObject.SetActive(false);
        }

        public void Bind(BossDefinition definition)
        {
            BuildSkeleton();

            _definition = definition;
            if (definition == null)
            {
                _active = false;
                return;
            }

            _gait = GaitFor(definition.Rank);
            _tint = HudUI.RankColour(definition.Rank);
            _enraged = false;

            float size = _gait.Size;
            _body.sizeDelta = new Vector2(size, size);
            _body.localScale = Vector3.one;

            _creature.sprite = SpriteFor(definition.Rank);
            _creature.color = Color.white;
            _creature.enabled = _creature.sprite != null;
            _creatureRect.sizeDelta = new Vector2(size, size);
            _creatureRect.anchoredPosition = Vector2.zero;
            _creatureRect.localRotation = Quaternion.identity;
            _creatureRect.localScale = Vector3.one;

            _underglow.color = new Color(_tint.r, _tint.g, _tint.b, 0.45f);
            var glowRect = (RectTransform)_underglow.transform;
            glowRect.sizeDelta = new Vector2(size * 1.05f, 24f);
            glowRect.anchoredPosition = new Vector2(0f, -size * 0.5f - 2f);

            // The sigil hangs off the creature's lower edge like a name-tag, so it reads on any body.
            float badge = Mathf.Clamp(size * 0.28f, 44f, 58f);
            _badgeRing.color = _tint;
            var badgeRect = (RectTransform)_badgeRing.transform;
            badgeRect.sizeDelta = new Vector2(badge, badge);
            badgeRect.anchoredPosition = new Vector2(0f, -size * 0.44f);
            _badgeFace.color = new Color(0.03f, 0.04f, 0.07f, 0.96f);

            _sigil.text = char.ToUpperInvariant(definition.Sigil).ToString();
            _sigil.fontSize = badge * 0.62f;
            _sigil.color = _tint;

            _telegraph.anchoredPosition = new Vector2(0f, -14f);
            _telegraph.SetAsLastSibling();
            HideTelegraph();

            _halfRange = Mathf.Max(40f, _self.rect.width * 0.5f - size * 0.5f - edgeMargin);
            _phase = 0f;
            _idleTime = 0f;
            _hitBoost = 0f;
            _active = true;

            Reposition();
            ApplyIdle();
        }

        private Sprite SpriteFor(BossRank rank)
        {
            int index = (int)rank;
            if (rankSprites != null && index >= 0 && index < rankSprites.Length && rankSprites[index] != null)
                return rankSprites[index];

            Debug.LogWarning($"[BossUI] No sprite assigned for rank {rank}.");
            return null;
        }

        #endregion

        #region Movement

        private void Update()
        {
            if (!_active || _body == null) return;

            // Sine patrol: eases naturally at the turns instead of snapping direction.
            float period = Mathf.Max(0.5f, 2f * Mathf.PI * _halfRange / Mathf.Max(1f, _gait.PatrolSpeed));
            _phase += Time.deltaTime / period;

            // Enrage quickens the creature itself, not just the attack timer.
            _idleTime += Time.deltaTime * (_enraged ? 1.6f : 1f);

            Reposition();
            ApplyIdle();

            float scale = 1f + _hitBoost;
            _body.localScale = Vector3.one * scale;
            _hitBoost = Mathf.MoveTowards(_hitBoost, 0f, Time.deltaTime * 1.1f);

            if (_telegraphRemaining <= 0f) return;

            _telegraphRemaining -= Time.deltaTime;

            if (_telegraphRemaining <= 0f)
            {
                HideTelegraph();
                return;
            }

            // Blink faster as it closes, so urgency reads without a countdown.
            float progress = 1f - _telegraphRemaining / Mathf.Max(0.01f, _telegraphTotal);
            float rate = Mathf.Lerp(6f, 18f, progress);
            _telegraphGroup.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_telegraphRemaining * rate));
        }

        /// <summary>Each creature's idle, applied to the sprite alone so the badge stays legible.</summary>
        private void ApplyIdle()
        {
            if (_creatureRect == null || _definition == null) return;

            float t = _idleTime;

            switch (_definition.Rank)
            {
                case BossRank.D: // Slime: classic squash and stretch.
                    float squash = Mathf.Sin(t * 3.2f) * 0.06f;
                    _creatureRect.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                    break;

                case BossRank.C: // Thorn: wobble-spin, like a blade never quite at rest.
                    _creatureRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.4f) * 10f);
                    _creatureRect.localScale = Vector3.one * (1f + Mathf.Sin(t * 4.8f) * 0.02f);
                    break;

                case BossRank.B: // Witch: sways as she floats — the big bob comes from the gait.
                    _creatureRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.6f) * 5f);
                    break;

                case BossRank.A: // Golem: slow, heavy breathing.
                    _creatureRect.localScale = Vector3.one * (1f + Mathf.Sin(t * 1.4f) * 0.02f);
                    break;

                default: // Dragon: a wingbeat pulse with a slow menacing roll.
                    _creatureRect.localScale = Vector3.one * (1f + Mathf.Sin(t * 2.0f) * 0.045f);
                    _creatureRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.0f) * 3f);
                    break;
            }
        }

        private void Reposition()
        {
            float x = Mathf.Sin(_phase * Mathf.PI * 2f) * _halfRange;
            float bob = Mathf.Sin(_idleTime * _gait.BobFrequency * Mathf.PI * 2f) * _gait.BobAmplitude;

            _body.anchoredPosition = new Vector2(x, patrolCentreY + bob);
        }

        #endregion

        #region Reactions

        public void ShowTelegraph(BossAttack attack, float seconds)
        {
            if (_telegraph == null) return;

            Color alert = ThemeManager.ActiveTheme != null ? ThemeManager.ActiveTheme.accentAlert : Color.red;

            _telegraphLabel.text = DisplayName(attack);
            _telegraphLabel.color = alert;
            _telegraphFrame.color = new Color(alert.r, alert.g, alert.b, 0.22f);

            _telegraphTotal = Mathf.Max(0.2f, seconds);
            _telegraphRemaining = _telegraphTotal;

            _telegraph.gameObject.SetActive(true);
        }

        public void HideTelegraph()
        {
            _telegraphRemaining = 0f;

            if (_telegraph == null) return;

            _telegraphGroup.alpha = 0f;
            _telegraph.gameObject.SetActive(false);
        }

        /// <summary>Kick on the body when the boss is hit; bigger when the sigil bonus lands.</summary>
        public void PulseDamage(bool sigilHit) => _hitBoost = Mathf.Max(_hitBoost, sigilHit ? 0.17f : 0.07f);

        /// <summary>Recoil as the boss throws — sells the attack as coming from it.</summary>
        public void PlayAttackRecoil() => _hitBoost = Mathf.Max(_hitBoost, 0.22f);

        public void PlayEnrage()
        {
            if (_definition == null) return;

            Color alert = ThemeManager.ActiveTheme != null ? ThemeManager.ActiveTheme.accentAlert : Color.red;

            // The same creature, flushed red and moving quicker.
            _enraged = true;
            _creature.color = Color.Lerp(Color.white, alert, 0.4f);
            _badgeRing.color = alert;
            _sigil.color = alert;
            _underglow.color = new Color(alert.r, alert.g, alert.b, 0.55f);
            _hitBoost = 0.35f;
        }

        public void PlayDefeat()
        {
            HideTelegraph();
            _active = false;
            _hitBoost = 0.4f;

            if (_creature != null) _creature.color = new Color(1f, 1f, 1f, 0.25f);
            if (_badgeRing != null) _badgeRing.color = new Color(_tint.r, _tint.g, _tint.b, 0.2f);
            if (_badgeFace != null) _badgeFace.color = new Color(0.03f, 0.04f, 0.07f, 0.3f);
            if (_sigil != null) _sigil.color = new Color(_tint.r, _tint.g, _tint.b, 0.3f);
            if (_underglow != null) _underglow.color = new Color(_tint.r, _tint.g, _tint.b, 0.12f);
        }

        public static string DisplayName(BossAttack attack) => attack switch
        {
            BossAttack.WordBurst => "WORD BURST",
            BossAttack.SpeedSurge => "SPEED SURGE",
            BossAttack.WordVeil => "WORD VEIL",
            _ => "ATTACK"
        };

        /// <summary>The creature's callsign, used by the rank-up teaser.</summary>
        public static string ArchetypeName(BossRank rank) => rank switch
        {
            BossRank.D => "THE SLIME",
            BossRank.C => "THE THORN",
            BossRank.B => "THE WITCH",
            BossRank.A => "THE GOLEM",
            BossRank.S => "THE DRAGON",
            _ => "THE SLIME"
        };

        #endregion

        #region Builders

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            if (sprite != null) image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            return image;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        #endregion
    }
}
