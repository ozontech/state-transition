namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public sealed class FireByStateRequest : BaseFireRequest
	{
		public TState State { get; }

		public FireByStateRequest(TState state, TEntity entity, CancellationToken cancellationToken) : base(entity, cancellationToken)
		{
			State = state;
		}
	}
}