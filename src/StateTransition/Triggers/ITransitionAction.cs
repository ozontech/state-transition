namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public interface ITransitionAction
	{
		public Task ExecuteAsync(Transition transition);
	}

	internal class TransitionAction : ITransitionAction
	{
		private readonly ITransitionAction _action;

		internal TransitionAction(ITransitionAction action)
		{
			_action = action;
		}

		public async Task ExecuteAsync(Transition transition)
		{
			await _action.ExecuteAsync(transition);
			transition.ActionLog.Log(_action);
		}
	}
}