namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	internal abstract class BaseTriggerStateResolver
	{
		protected internal TState Destination { get; }

		protected internal TTrigger Trigger { get; }

		protected internal TransitionGuard Guard { get; }

		protected internal BaseTransitionOptions TransitionOptions { get; }

		protected BaseTriggerStateResolver(TTrigger trigger, TState destination, BaseTransitionOptions transitionOptions, Func<TEntity, bool> guard)
		{
			Trigger = trigger;
			Destination = destination;
			TransitionOptions = transitionOptions ?? BaseTransitionOptions.Default;
			Guard = guard is not null ? new TransitionGuard(guard) : TransitionGuard.Empty;
		}
	}
}