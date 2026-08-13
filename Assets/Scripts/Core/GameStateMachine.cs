using System;

namespace TypingMe.Core
{
    public enum GameState
    {
        Boot,
        Home,
        Playing,
        Paused,
        GameOver,
        LevelComplete
    }

    /// <summary>Minimal state machine (§9). Transitions are announced, never polled.</summary>
    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Boot;

        /// <summary>(previous, next)</summary>
        public event Action<GameState, GameState> Changed;

        public void Set(GameState next)
        {
            if (next == Current) return;

            GameState previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
        }

        public bool IsAcceptingTyping => Current == GameState.Playing;
    }
}
