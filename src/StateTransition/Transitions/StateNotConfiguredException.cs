namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public class StateNotConfiguredException : Exception
	{
		private readonly TState _state;

		internal StateNotConfiguredException(TState state)
		{
			_state = state;
		}

		public override string Message => $"The {_state} state has not been configured or unmarked as finite";
	}
}