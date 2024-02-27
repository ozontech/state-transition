namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public sealed class FireByTriggerRequest : BaseFireRequest
	{
		public TTrigger Trigger { get; }

		public FireByTriggerRequest(TTrigger trigger, TEntity entity, CancellationToken cancellationToken) : base(entity, cancellationToken)
		{
			Trigger = trigger;
		}
	}
}