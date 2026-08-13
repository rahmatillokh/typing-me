using System;
using System.Collections;
using TypingMe.Data;
using UnityEngine;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// The level's boss: health, the sigil damage rule, and the attack schedule.
    /// </summary>
    /// <remarks>
    /// Deliberately knows nothing about the play area. It decides *that* an attack happens and of
    /// which kind; <see cref="LevelRunner"/> decides what that does to the spawner, router and
    /// keyboard. Keeps the fight rules testable without a scene.
    /// </remarks>
    public sealed class BossController : MonoBehaviour
    {
        public BossDefinition Definition { get; private set; }
        public float Health { get; private set; }
        public bool IsEnraged { get; private set; }
        public bool IsFighting { get; private set; }

        public bool IsDefeated => Definition != null && Health <= 0f;

        public float MaxHealth => Definition?.MaxHealth ?? 0f;

        public float HealthFraction =>
            Definition == null || Definition.MaxHealth <= 0f ? 0f : Mathf.Clamp01(Health / Definition.MaxHealth);

        /// <summary>(current, max)</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>An attack is incoming — raised <c>telegraph</c> seconds before it lands.</summary>
        public event Action<BossAttack> AttackTelegraphed;

        public event Action<BossAttack> AttackFired;
        public event Action Enraged;
        public event Action Defeated;

        private BossTuningSO _tuning;
        private Coroutine _loop;
        private BossAttack _lastAttack;

        public void Begin(BossDefinition definition, BossTuningSO tuning)
        {
            StopFighting();

            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));

            Health = definition.MaxHealth;
            IsEnraged = false;
            IsFighting = true;

            HealthChanged?.Invoke(Health, definition.MaxHealth);

            // Coroutines don't tick outside play mode; health and damage stay testable in EditMode.
            if (Application.isPlaying) _loop = StartCoroutine(AttackLoop());
        }

        public void StopFighting()
        {
            IsFighting = false;

            if (_loop == null) return;

            StopCoroutine(_loop);
            _loop = null;
        }

        /// <summary>
        /// Applies a cleared word's damage. Longer words hit harder, and a word containing the
        /// boss's sigil hits harder still — that is what makes the sigil worth hunting.
        /// </summary>
        public float TakeDamage(string word, out bool sigilHit)
        {
            sigilHit = false;
            if (Definition == null || IsDefeated || string.IsNullOrEmpty(word)) return 0f;

            float damage = word.Length;

            sigilHit = word.IndexOf(Definition.Sigil) >= 0;
            if (sigilHit) damage *= _tuning.sigilDamageMultiplier;

            Health = Mathf.Max(0f, Health - damage);
            HealthChanged?.Invoke(Health, Definition.MaxHealth);

            // Only the season boss has a second phase.
            if (!IsEnraged && Definition.IsSeasonBoss && HealthFraction <= _tuning.enrageThreshold)
            {
                IsEnraged = true;
                Enraged?.Invoke();
            }

            if (!IsDefeated) return damage;

            StopFighting();
            Defeated?.Invoke();
            return damage;
        }

        private IEnumerator AttackLoop()
        {
            BossRankProfile profile = Definition.Profile;

            // Never open a level with an attack — the player needs a moment to read the board.
            yield return new WaitForSeconds(Mathf.Max(1f, profile.firstAttackDelay));

            while (IsFighting && !IsDefeated)
            {
                BossAttack attack = PickAttack(profile);

                AttackTelegraphed?.Invoke(attack);
                yield return new WaitForSeconds(Mathf.Max(0.2f, profile.telegraph));

                if (!IsFighting || IsDefeated) yield break;

                AttackFired?.Invoke(attack);
                _lastAttack = attack;

                if (profile.doubleAttacks)
                {
                    BossAttack second = PickAttack(profile);
                    if (second != attack) AttackFired?.Invoke(second);
                }

                float interval = profile.attackInterval;
                if (IsEnraged) interval *= _tuning.enrageIntervalScale;

                // The telegraph is part of the cycle, not extra time on top of it.
                yield return new WaitForSeconds(Mathf.Max(1.5f, interval - profile.telegraph));
            }
        }

        /// <summary>Avoids repeating the previous attack when the rank has more than one to choose from.</summary>
        private BossAttack PickAttack(BossRankProfile profile)
        {
            BossAttack[] pool = profile.attacks;
            if (pool == null || pool.Length == 0) return BossAttack.WordBurst;
            if (pool.Length == 1) return pool[0];

            for (int attempt = 0; attempt < 6; attempt++)
            {
                BossAttack candidate = pool[UnityEngine.Random.Range(0, pool.Length)];
                if (candidate != _lastAttack) return candidate;
            }

            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
    }
}
