namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public sealed class Transition
	{
		internal Transition(TEntity entity,
		                    TState source,
		                    TState destination,
		                    TTrigger trigger,
		                    BaseTransitionOptions transitionOptions,
		                    CancellationToken token)
		{
			Entity = entity;
			Source = source;
			Destination = destination;
			Trigger = trigger;
			TransitionOptions = transitionOptions;
			Token = token;
		}

		public TEntity Entity { get; }

		public TState Source { get; }

		public TState Destination { get; }

		public TTrigger Trigger { get; }

		public BaseTransitionOptions TransitionOptions { get; }

		public CancellationToken Token { get; }

		public readonly TransitionActionLog ActionLog = new();
	}

	public sealed class TransitionActionLog
	{
		public IList<ITransitionAction> Actions { get; } = new List<ITransitionAction>();

		internal void Log(ITransitionAction action)
		{
			Actions.Add(action);
		}
	}

	internal sealed class TransitionCompletedEvent
	{
		private event Action<Transition> TransitionCompleted;

		internal void Invoke(Transition transition)
		{
			TransitionCompleted?.Invoke(transition);
		}

		internal void Subscribe(IEnumerable<Action<Transition>> actions)
		{
			foreach (var action in actions)
			{
				TransitionCompleted += action;
			}
		}

		internal void Unsubscribe(IEnumerable<Action<Transition>> actions)
		{
			foreach (var action in actions)
			{
				TransitionCompleted -= action;
			}
		}
	}
}