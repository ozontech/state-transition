namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public sealed class AutofireRequest : BaseFireRequest
	{
		public AutofireRequest(TEntity entity, CancellationToken cancellationToken) : base(entity, cancellationToken) { }
	}
}