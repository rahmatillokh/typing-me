using System.Collections.Generic;

namespace TypingMe.Data
{
    /// <summary>
    /// One generated level. Produced deterministically by <see cref="TypingMe.Gameplay.LevelGenerator"/>,
    /// so the same level number always yields the same run (§5).
    /// </summary>
    public sealed class LevelData
    {
        public LevelData(int levelNumber, IReadOnlyList<string> words, int targetClears, float fallSpeed, float spawnInterval)
        {
            LevelNumber = levelNumber;
            Words = words;
            TargetClears = targetClears;
            FallSpeed = fallSpeed;
            SpawnInterval = spawnInterval;
        }

        public int LevelNumber { get; }

        /// <summary>
        /// Spawn queue. Longer than <see cref="TargetClears"/> — the surplus covers missed words so a
        /// single miss cannot make the level unwinnable. See the note in LevelGenerator.
        /// </summary>
        public IReadOnlyList<string> Words { get; }

        /// <summary>Words the player must clear to complete the level (20 per §3).</summary>
        public int TargetClears { get; }

        /// <summary>Reference pixels per second, before the per-word easing curve.</summary>
        public float FallSpeed { get; }

        /// <summary>Seconds between spawns.</summary>
        public float SpawnInterval { get; }
    }
}
