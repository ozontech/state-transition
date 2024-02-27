namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public class AmbiguousStateConfigurationException : Exception
	{
		private readonly TState _state;

		internal AmbiguousStateConfigurationException(TState state)
		{
			_state = state;
		}

		public override string Message => $"Multiple configurations detected for {_state} state";
	}
}