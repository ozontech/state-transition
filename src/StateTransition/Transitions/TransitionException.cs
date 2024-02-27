namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public sealed class TransitionException : Exception
	{
		public Transition Transition { get; }

		internal TransitionException(Transition transition, Exception innerException)
			: base($"An error occurred while transitioning from {transition.Source} to {transition.Destination}", innerException)
		{
			Transition = transition;
		}
	}
}