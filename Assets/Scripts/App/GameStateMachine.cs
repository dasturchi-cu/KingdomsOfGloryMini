using System;

namespace KoG.MiniMvp.App
{
    /// <summary>
    /// Centralized state machine for coordinating game states and avoiding conflicts.
    /// </summary>
    public sealed class GameStateMachine
    {
        public event Action<GameState, GameState> StateChanged;

        public GameState CurrentState { get; private set; } = GameState.Loading;

        public bool CanTransitionTo(GameState newState) => CanTransition(CurrentState, newState);

        public void TransitionTo(GameState newState)
        {
            if (CurrentState == newState) return;

            if (!CanTransition(CurrentState, newState))
            {
                UnityEngine.Debug.LogWarning($"[GameStateMachine] Invalid transition from {CurrentState} to {newState}");
                return;
            }

            var previousState = CurrentState;
            CurrentState = newState;

            StateChanged?.Invoke(previousState, newState);
        }

        public static bool CanTransition(GameState from, GameState to)
        {
            // Loading can only transition to Idle
            if (from == GameState.Loading)
            {
                return to == GameState.Idle;
            }

            // If Busy (network operation in progress), we must wait until it returns to another state (usually Idle or Selecting)
            if (from == GameState.Busy)
            {
                return to == GameState.Idle || to == GameState.Selecting || to == GameState.Raiding;
            }

            // During Raiding, building operations are disallowed
            if (from == GameState.Raiding)
            {
                return to == GameState.Idle || to == GameState.Busy;
            }

            // Normal state transitions are generally open but we lock it down to prevent invalid overlaps
            switch (to)
            {
                case GameState.Loading:
                    return false; // Can't go back to Loading under normal circumstances
                
                case GameState.Relocating:
                    // Can only relocate from Selecting or Idle
                    return from == GameState.Idle || from == GameState.Selecting;

                case GameState.Building:
                    // Can only build from Idle or Selecting
                    return from == GameState.Idle || from == GameState.Selecting;

                case GameState.Raiding:
                    // Can raid from Idle or Selecting (not while building/relocating)
                    return from == GameState.Idle || from == GameState.Selecting;
            }

            return true;
        }
    }
}
