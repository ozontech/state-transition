namespace StateTransition;

public partial class StateMachine<TState, TTrigger, TEntity>
{
	public class StateTransitionManager
	{
		private readonly State _state;
		private readonly Func<TState, State> _stateLookup;
		private readonly Func<BaseTransitionOptions> _defaultTransitionOptionsLookup;

		internal StateTransitionManager(State state, Func<TState, State> stateLookup, Func<BaseTransitionOptions> defaultTransitionOptionsLookup)
		{
			_state = state;
			_stateLookup = stateLookup;
			_defaultTransitionOptionsLookup = defaultTransitionOptionsLookup;
		}

		/// <summary>
		///     Adds transition for the current state configuration to the destination state with required custom transition
		///     options to control a transitioning process
		///     In addition, it allows to set specified action and guard
		/// </summary>
		/// <param name="destinationState">The state that the trigger will cause a transition to</param>
		/// <param name="trigger">Trigger which fires a transition to the destination state</param>
		/// <param name="customTransitionOptions">Allows to use custom transition options</param>
		/// <param name="transitionAction">Action to execute during transitioning process</param>
		/// <param name="guardExpression">
		///     Transition can be done only if a specified guard is met for the associated trigger.
		///     Happy path is when we found a single trigger resolver and its guard met conditions.
		///     By default, when guard is not defined, then we will allow transition without checking conditions.
		/// </param>
		/// <returns>The state configuration which describes transitioning from this state.</returns>
		public StateTransitionManager AddTransitionTo<TOptions>(
			TState destinationState,
			TTrigger trigger,
			Action<TOptions> customTransitionOptions,
			ITransitionAction transitionAction = default,
			Func<TEntity, bool> guardExpression = default) where TOptions : BaseTransitionOptions
		{
			TOptions options;
			var defaultOptions = _defaultTransitionOptionsLookup();
			if (defaultOptions is not null)
			{
				if (defaultOptions is not TOptions transitionOptions)
				{
					throw new ArgumentException($"Type of the custom transition options ({typeof(TOptions).Name}) differs from the default ({defaultOptions.GetType().Name})");
				}

				options = transitionOptions;
			}
			else
			{
				options = Activator.CreateInstance<TOptions>();
			}

			customTransitionOptions.Invoke(options);
			return MapTransitionToTriggerStateResolver(_state, destinationState, trigger, transitionAction, guardExpression, options);
		}

		/// <summary>
		///     Adds transition for the current state configuration to the destination state.
		///     In addition, it allows to set specified action and guard
		/// </summary>
		/// <param name="destinationState">The state that the trigger will cause a transition to</param>
		/// <param name="trigger">Trigger which fires a transition to the destination state</param>
		/// <param name="transitionAction">Action to execute during transitioning process</param>
		/// <param name="guardExpression">
		///     Transition can be done only if a specified guard is met for the associated trigger.
		///     Happy path is when we found a single trigger resolver and its guard met conditions.
		///     By default, when guard is not defined, then we will allow transition without checking conditions.
		/// </param>
		/// <returns>The state configuration which describes transitioning from this state.</returns>
		public StateTransitionManager AddTransitionTo(
			TState destinationState,
			TTrigger trigger,
			ITransitionAction transitionAction = default,
			Func<TEntity, bool> guardExpression = default)
		{
			return MapTransitionToTriggerStateResolver(_state, destinationState, trigger, transitionAction, guardExpression);
		}

		/// <summary>
		///     Specify an action that will be executed before state transitioning from the source to specified destination state
		/// </summary>
		public StateTransitionManager BeforeTransitionTo(TState destinationState, ITransitionAction action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			_state.BeforeTransitionTo(destinationState, action);
			return this;
		}

		/// <summary>
		///     Specify an action that will be executed after state transitioning from the source to specified destination state
		/// </summary>
		public StateTransitionManager AfterTransitionTo(TState destinationState, ITransitionAction action)
		{
			var state = _stateLookup(destinationState);
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			state.AfterTransitionFrom(_state.Current, action);
			return this;
		}

		private StateTransitionManager MapTransitionToTriggerStateResolver(State state,
		                                                                   TState destinationState,
		                                                                   TTrigger trigger,
		                                                                   ITransitionAction transitionAction = default,
		                                                                   Func<TEntity, bool> guardExpression = default,
		                                                                   BaseTransitionOptions options = default)
		{
			state.MapTo(transitionAction is not null
				            ? new TransitionActionTriggerStateResolver(trigger, destinationState, options ?? _defaultTransitionOptionsLookup(), guardExpression, transitionAction)
				            : new TransitionTriggerStateResolver(trigger, destinationState, options ?? _defaultTransitionOptionsLookup(), guardExpression));
			return this;
		}
	}
}