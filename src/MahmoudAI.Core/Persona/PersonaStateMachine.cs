using System;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Persona
{
    public enum PersonaState
    {
        Idle,
        Listening,
        Thinking,
        Speaking,
        Working,
        Warning,
        SafeMode
    }

    public class PersonaStateMachine
    {
        private readonly ILogger<PersonaStateMachine> _logger;
        public PersonaState CurrentState { get; private set; } = PersonaState.Idle;

        public event EventHandler<PersonaState>? StateChanged;

        public PersonaStateMachine(ILogger<PersonaStateMachine> logger)
        {
            _logger = logger;
        }

        public void TransitionTo(PersonaState newState)
        {
            if (CurrentState != newState)
            {
                _logger.LogInformation("Persona state transitioning from {OldState} to {NewState}", CurrentState, newState);
                CurrentState = newState;
                StateChanged?.Invoke(this, CurrentState);
            }
        }
    }
}
