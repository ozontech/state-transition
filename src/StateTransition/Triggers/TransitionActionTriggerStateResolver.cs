namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	internal sealed class TransitionActionTriggerStateResolver : TransitionTriggerStateResolver
	{
		private TransitionAction TransitionAction { get; }

		internal TransitionActionTriggerStateResolver(TTrigger trigger,
		                                              TState destination,
		                                              BaseTransitionOptions options,
		                                              Func<TEntity, bool> transitionGuard,
		                                              ITransitionAction transitionAction) : base(trigger, destination, options, transitionGuard)
		{
			TransitionAction = new TransitionAction(transitionAction ?? throw new ArgumentNullException(nameof(transitionAction)));
		}

		internal async Task Execute(Transition transition)
		{
			try
			{
				await TransitionAction.ExecuteAsync(transition);
			}
			catch (Exception ex)
			{
				throw new TransitionException(transition, ex);
			}
		}
	}
}