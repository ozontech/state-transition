namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public abstract class BaseFireRequest
	{
		public TEntity Entity { get; }

		public CancellationToken CancellationToken { get; }

		protected BaseFireRequest(TEntity entity, CancellationToken cancellationToken)
		{
			Entity = entity;
			CancellationToken = cancellationToken;
		}
	}
}