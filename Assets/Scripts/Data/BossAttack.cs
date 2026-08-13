namespace TypingMe.Data
{
    /// <summary>What a boss does when its attack timer fires.</summary>
    /// <remarks>
    /// Stored as an int inside <c>BossTuning.asset</c>, so changing the order or numbering here
    /// requires regenerating that asset — see Typing Me/Reset Tuning Assets to Code Defaults.
    /// </remarks>
    public enum BossAttack
    {
        /// <summary>Drops several extra words at once, flooding the play area.</summary>
        WordBurst = 0,

        /// <summary>Everything on screen falls faster for a few seconds.</summary>
        SpeedSurge = 1,

        /// <summary>Obscures words behind glitch blocks so they can't be read.</summary>
        WordVeil = 2
    }
}
