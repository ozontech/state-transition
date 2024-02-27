namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	internal class TransitionGuard
	{
		public static readonly TransitionGuard Empty = new();

		private Func<TEntity, bool> Guard { get; } = _ => true;

		internal TransitionGuard(Func<TEntity, bool> guard)
		{
			Guard = guard ?? throw new ArgumentNullException(nameof(guard));
		}

		private TransitionGuard() { }

		public bool GuardIsMet(TEntity entity)
		{
			return Guard(entity);
		}
	}
}