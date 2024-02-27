namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	internal class TransitionTriggerStateResolver : BaseTriggerStateResolver
	{
		internal TransitionTriggerStateResolver(TTrigger trigger,
		                                        TState destination,
		                                        BaseTransitionOptions options,
		                                        Func<TEntity, bool> guard) : base(trigger, destination, options, guard) { }
	}
}