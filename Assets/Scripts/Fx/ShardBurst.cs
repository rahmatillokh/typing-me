using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.Fx
{
    /// <summary>
    /// The particle burst §3 calls for when a word is cleared: neon shards thrown outward that
    /// arc down, spin, fade and shrink.
    /// </summary>
    /// <remarks>
    /// Built from pooled uGUI images rather than a ParticleSystem. The play area is a Screen Space –
    /// Camera canvas, where a ParticleSystem needs its own renderer ordering and does not sort
    /// cleanly against UI; pooled images share the canvas batch and cost nothing per burst after
    /// the pool is warmed.
    /// </remarks>
    public sealed class ShardBurst : MonoBehaviour
    {
        [SerializeField] private int poolSize = 96;
        [SerializeField] private int shardsPerBurst = 16;

        [Header("Motion (reference px)")]
        [SerializeField] private float minSpeed = 190f;
        [SerializeField] private float maxSpeed = 430f;
        [SerializeField] private float gravity = -820f;
        [SerializeField] private float lifetime = 0.55f;
        [SerializeField] private float spin = 520f;

        [Header("Shape")]
        [SerializeField] private float shardLength = 22f;
        [SerializeField] private float shardThickness = 6f;

        private struct Shard
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Spin;
            public float Scale;
            public Color Colour;
            public bool Active;
        }

        private Shard[] _shards;

        private void Awake() => BuildPool();

        private void BuildPool()
        {
            if (_shards != null) return;

            _shards = new Shard[Mathf.Max(1, poolSize)];

            for (int i = 0; i < _shards.Length; i++)
            {
                var go = new GameObject($"Shard{i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(shardLength, shardThickness);

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;

                go.SetActive(false);

                _shards[i].Rect = rect;
                _shards[i].Image = image;
            }
        }

        /// <summary>Fires a burst at <paramref name="origin"/> (anchored position in this rect's space).</summary>
        public void Play(Vector2 origin, Color colour)
        {
            BuildPool();

            int spawned = 0;

            for (int i = 0; i < _shards.Length && spawned < shardsPerBurst; i++)
            {
                if (_shards[i].Active) continue;

                // Bias upward so the burst reads as an outward pop rather than a puddle.
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(minSpeed, maxSpeed);

                _shards[i].Active = true;
                _shards[i].Position = origin + new Vector2(Random.Range(-28f, 28f), Random.Range(-10f, 10f));
                _shards[i].Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Abs(Mathf.Sin(angle)) * speed * 0.9f + 90f);
                _shards[i].Age = 0f;
                _shards[i].Life = lifetime * Random.Range(0.75f, 1.25f);
                _shards[i].Spin = Random.Range(-spin, spin);
                _shards[i].Scale = Random.Range(0.7f, 1.35f);
                _shards[i].Colour = colour;

                _shards[i].Rect.anchoredPosition = _shards[i].Position;
                _shards[i].Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                _shards[i].Rect.localScale = Vector3.one * _shards[i].Scale;
                _shards[i].Image.color = colour;
                _shards[i].Rect.gameObject.SetActive(true);

                spawned++;
            }
        }

        private void Update()
        {
            if (_shards == null) return;

            float delta = Time.deltaTime;

            for (int i = 0; i < _shards.Length; i++)
            {
                if (!_shards[i].Active) continue;

                _shards[i].Age += delta;

                if (_shards[i].Age >= _shards[i].Life)
                {
                    _shards[i].Active = false;
                    _shards[i].Rect.gameObject.SetActive(false);
                    continue;
                }

                float t = _shards[i].Age / _shards[i].Life;

                _shards[i].Velocity += new Vector2(0f, gravity * delta);
                _shards[i].Position += _shards[i].Velocity * delta;

                _shards[i].Rect.anchoredPosition = _shards[i].Position;
                _shards[i].Rect.localRotation *= Quaternion.Euler(0f, 0f, _shards[i].Spin * delta);
                _shards[i].Rect.localScale = Vector3.one * _shards[i].Scale * (1f - t * 0.65f);

                Color colour = _shards[i].Colour;
                colour.a = 1f - t * t; // hold bright, then drop away fast
                _shards[i].Image.color = colour;
            }
        }
    }
}
