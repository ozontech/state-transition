namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public class TriggerStateResolverNotFoundException : Exception
	{
		internal TriggerStateResolverNotFoundException(TState destinationState)
		{
			Message = $"No suitable trigger state resolver configured for the transitioning to {destinationState} state";
		}

		internal TriggerStateResolverNotFoundException(TTrigger trigger)
		{
			Message = $"No suitable trigger state resolver configured for the transitioning using {trigger} trigger";
		}

		public override string Message { get; }
	}
}