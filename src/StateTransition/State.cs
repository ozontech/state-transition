namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	internal class State
	{
		internal Dictionary<TTrigger, ICollection<TransitionTriggerStateResolver>> TransitionTriggerStateResolvers { get; } = new();

		private Dictionary<TState, ICollection<TransitionAction>> ActionsBeforeTransitionTo { get; } = new();

		private Dictionary<TState, ICollection<TransitionAction>> ActionsAfterTransitionFrom { get; } = new();

		internal readonly TState Current;
		internal readonly bool IsFinite;

		internal State(TState state, bool isFinite = false)
		{
			Current = state;
			IsFinite = isFinite;
		}

		internal void MapTo(TransitionTriggerStateResolver stateResolver)
		{
			if (!TransitionTriggerStateResolvers.TryGetValue(stateResolver.Trigger, out ICollection<TransitionTriggerStateResolver> allowedTriggerStateResolvers))
			{
				allowedTriggerStateResolvers = new List<TransitionTriggerStateResolver>();
				TransitionTriggerStateResolvers.Add(stateResolver.Trigger, allowedTriggerStateResolvers);
			}

			allowedTriggerStateResolvers.Add(stateResolver);
		}

		internal IEnumerable<TransitionTriggerStateResolver> GetAllAvailableTriggersStateResolvers()
		{
			return TransitionTriggerStateResolvers.Values.SelectMany(x => x);
		}

		internal IEnumerable<TransitionTriggerStateResolver> GetTransitionTriggerStateResolversBy(TTrigger trigger)
		{
			return TransitionTriggerStateResolvers.Where(x => x.Key.Equals(trigger)).SelectMany(x => x.Value);
		}

		internal IEnumerable<TransitionTriggerStateResolver> GetAutoFiredTransitionTriggerStateResolvers()
		{
			return TransitionTriggerStateResolvers.Values
			                                      .SelectMany(x => x)
			                                      .Where(z => !Current.Equals(z.Destination) && z.TransitionOptions.IsAutofire);
		}

		internal void BeforeTransitionTo(TState destinationState, ITransitionAction action)
		{
			if (!ActionsBeforeTransitionTo.TryGetValue(destinationState, out ICollection<TransitionAction> actionsBeforeTransition))
			{
				actionsBeforeTransition = new List<TransitionAction>();
				ActionsBeforeTransitionTo.Add(destinationState, actionsBeforeTransition);
			}

			actionsBeforeTransition.Add(new TransitionAction(action));
		}

		internal void AfterTransitionFrom(TState sourceState, ITransitionAction action)
		{
			if (!ActionsAfterTransitionFrom.TryGetValue(sourceState, out ICollection<TransitionAction> actionsAfterTransition))
			{
				actionsAfterTransition = new List<TransitionAction>();
				ActionsAfterTransitionFrom.Add(sourceState, actionsAfterTransition);
			}

			actionsAfterTransition.Add(new TransitionAction(action));
		}

		internal async Task RunActionsAfter(Transition transition)
		{
			if (!Current.Equals(transition.Source) && !transition.Token.IsCancellationRequested)
			{
				if (ActionsAfterTransitionFrom.TryGetValue(transition.Source, out ICollection<TransitionAction> actionsAfterTransition))
				{
					foreach (var action in actionsAfterTransition)
					{
						await action.ExecuteAsync(transition);
					}
				}
			}
		}

		internal async Task RunActionsBefore(Transition transition)
		{
			if (!Current.Equals(transition.Destination) && !transition.Token.IsCancellationRequested)
			{
				if (ActionsBeforeTransitionTo.TryGetValue(transition.Destination, out ICollection<TransitionAction> actionsBeforeTransition))
				{
					foreach (var action in actionsBeforeTransition)
					{
						await action.ExecuteAsync(transition);
					}
				}
			}
		}
	}
}