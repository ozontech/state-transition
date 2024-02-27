namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public class AmbiguousTriggerStateResolverException : Exception
	{
		private readonly TState _destinationState;

		internal AmbiguousTriggerStateResolverException(TState destinationState)
		{
			_destinationState = destinationState;
		}

		public override string Message =>
			$"Multiple trigger state resolvers detected for the transitioning to {_destinationState} state";
	}
}