using System.Linq.Expressions;
using System.Text;

namespace StateTransition;

public abstract partial class StateMachine<TState, TTrigger, TEntity>
	where TEntity : class
{
	private readonly Func<TEntity, TState> _entityStateSelector;
	private readonly IDictionary<TState, State> _configuredStates = new Dictionary<TState, State>();
	private readonly Action<TEntity, TState> _updateEntityState;
	private readonly AsyncLocal<TransitionTriggerStateResolver> _transitionTriggerStateResolver = new();
	private readonly AsyncLocal<State> _currentState = new();
	private readonly AsyncLocal<TransitionCompletedEvent> _transitionCompletedEvent = new();

	private ICollection<TransitionAction> DefaultExitActions { get; } = new List<TransitionAction>();

	private ICollection<TransitionAction> DefaultEntryActions { get; } = new List<TransitionAction>();

	private ICollection<Action<Transition>> OnTransitionCompletedEventActions { get; } = new List<Action<Transition>>();

	private TransitionMiddlewareHandler TransitionMiddlewareHandlerPipeline { get; set; }

	private BaseTransitionOptions _defaultTransitionOptions;

	protected StateMachine(Expression<Func<TEntity, TState>> entityStateSelector)
	{
		_updateEntityState = AssignStateToEntityAction(entityStateSelector);
		_entityStateSelector = entityStateSelector.Compile();
	}

	/// <summary>
	///     Configures the source state
	/// </summary>
	/// <param name="state">The state to configure.</param>
	/// <returns>
	///     The instance of the state transition manager which provides functionality to manage allowed transitions from the
	///     configured state
	/// </returns>
	public StateTransitionManager Configure(TState state)
	{
		return new(GetOrConfigure(state), GetOrConfigure, () => _defaultTransitionOptions);
	}

	/// <summary>
	///     Setup the specific finite state of machine
	/// </summary>
	/// <param name="states">Set of the finite states</param>
	public void SetFiniteState(params TState[] states)
	{
		foreach (var s in states)
		{
			if (!TryGetStateInstance(s, out State stateInstance))
			{
				stateInstance = new State(s, true);
				_configuredStates.Add(s, stateInstance);
			}
			else
			{
				throw new AmbiguousStateConfigurationException(stateInstance.Current);
			}
		}
	}

	/// <summary>
	///     Adds default exit action which will be executed after state transitioning.
	///     Important: All default exit actions will be run after all transition actions
	/// </summary>
	/// <param name="defaultExitAction">Default exit action. </param>
	public void AddDefaultExitAction(ITransitionAction defaultExitAction)
	{
		DefaultExitActions.Add(new TransitionAction(defaultExitAction));
	}

	/// <summary>
	///     Adds default entry action will be executed before state transitioning
	/// </summary>
	/// <param name="defaultEntryAction">Default entry action. </param>
	public void AddDefaultEntryAction(ITransitionAction defaultEntryAction)
	{
		DefaultEntryActions.Add(new TransitionAction(defaultEntryAction));
	}

	/// <summary>
	///     Fires a transition from the current to the specified state using either the specified destination state
	///     or the specified trigger or by defining an appropriate trigger resolver in case if the next state can be
	///     auto-fired.
	/// </summary>
	/// <param name="request">Request for state transitioning</param>
	public async Task Fire(BaseFireRequest request)
	{
		if (!TryGetRequiredTriggerStateResolverFor(request, out var transitionTriggerStateResolver))
		{
			return;
		}

		_transitionTriggerStateResolver.Value = transitionTriggerStateResolver;

		if (TransitionMiddlewareHandlerPipeline is null)
		{
			await FireTransition(request);
		}
		else
		{
			await TransitionMiddlewareHandlerPipeline.Handle(request, _transitionTriggerStateResolver.Value!.TransitionOptions);
		}

		if (_transitionTriggerStateResolver.Value!.Destination.Equals(_entityStateSelector(request.Entity)))
		{
			await Fire(request is not AutofireRequest
				           ? new AutofireRequest(request.Entity, request.CancellationToken)
				           : request);
		}
	}

	/// <summary>
	///     Adds middleware to the transitioning pipeline (based on Stack linked list). Order of the added middlewares is
	///     important
	/// </summary>
	/// <param name="nextMiddlewareHandler">Middleware handler</param>
	public void UseMiddleware<T>(T nextMiddlewareHandler) where T : TransitionMiddlewareHandler
	{
		if (TransitionMiddlewareHandlerPipeline is null)
		{
			TransitionMiddlewareHandlerPipeline = nextMiddlewareHandler;
			var runMiddlewareHandler = new RunMiddlewareHandler(FireTransition);
			TransitionMiddlewareHandlerPipeline.SetNext(runMiddlewareHandler);
		}
		else
		{
			var head = TransitionMiddlewareHandlerPipeline;
			TransitionMiddlewareHandlerPipeline = nextMiddlewareHandler;
			TransitionMiddlewareHandlerPipeline.SetNext(head);
		}
	}

	/// <summary>
	///     Initiates default transition options.
	///     Configuring of the default transition options should be before the configuring custom transition options for all
	///     states via <see cref="Configure" />
	/// </summary>
	/// <param name="useDefaultTransitionOptionsAction">Delegate handler for setting up transition options</param>
	/// <typeparam name="TOptions">Transition options</typeparam>
	public void InitDefaultStateTransitionOptions<TOptions>(Action<TOptions> useDefaultTransitionOptionsAction = default) where TOptions : BaseTransitionOptions
	{
		var options = Activator.CreateInstance<TOptions>();
		useDefaultTransitionOptionsAction?.Invoke(options);

		_defaultTransitionOptions = options;
	}

	/// <summary>
	///     Adds custom action handler to the common queue for its invoking when transition has been completed
	/// </summary>
	/// <param name="action"></param>
	public void OnTransitionCompleted(Action<Transition> action)
	{
		OnTransitionCompletedEventActions.Add(action);
	}

	/// <summary>
	///     Gets graph with all state transitions
	/// </summary>
	/// <returns>string representation of the graph based on Mermaid notation</returns>
	public string GetTransitionsGraph()
	{
		var graph = new StringBuilder();
		graph.AppendLine("```mermaid\ngraph TD;");
		foreach (var configuredState in _configuredStates)
		{
			var transitions = configuredState.Value.TransitionTriggerStateResolvers.Values.SelectMany(x => x, (_, resolver) => resolver.Destination);
			foreach (var transition in transitions)
			{
				graph.AppendLine($"{configuredState.Key}-->{transition};");
			}
		}

		graph.AppendLine("```");
		return graph.ToString();
	}

	private bool TryGetRequiredTriggerStateResolverFor(BaseFireRequest request, out TransitionTriggerStateResolver transitionTriggerStateResolver)
	{
		transitionTriggerStateResolver = null;

		if (!TryGetStateInstance(_entityStateSelector(request.Entity), out State currentState))
		{
			throw new StateNotConfiguredException(_entityStateSelector(request.Entity));
		}

		if (currentState.IsFinite)
		{
			return false;
		}

		_currentState.Value = currentState;

		bool canFire = request switch
		{
			FireByTriggerRequest triggerRequest => CanFire(triggerRequest, out transitionTriggerStateResolver),
			FireByStateRequest stateRequest => CanFire(stateRequest, out transitionTriggerStateResolver),
			AutofireRequest autofireRequest => CanAutofireTransitionToNextState(autofireRequest, out transitionTriggerStateResolver),
			_ => throw new InvalidOperationException(
				     $"The required trigger state resolver for {request.GetType()} type is not found")
		};

		if (!canFire || transitionTriggerStateResolver == null)
		{
			return false;
		}

		if (!TryGetStateInstance(transitionTriggerStateResolver.Destination, out State _))
		{
			throw new StateNotConfiguredException(transitionTriggerStateResolver.Destination);
		}

		return true;
	}

	private bool CanFire(FireByStateRequest stateRequest, out TransitionTriggerStateResolver transitionTrigger)
	{
		transitionTrigger = null;
		var configuredTriggerStateResolvers = _currentState.Value!.GetAllAvailableTriggersStateResolvers().ToList();
		if (!configuredTriggerStateResolvers.Any())
		{
			throw new TriggerStateResolverNotFoundException(stateRequest.State);
		}

		if (!TryFindSuitableTransitionTriggerStateResolver(configuredTriggerStateResolvers,
		                                                   TransitionTriggerStateResolverPredicate(stateRequest),
		                                                   out TransitionTriggerStateResolver triggerStateResolver))
		{
			return false;
		}

		transitionTrigger = triggerStateResolver;
		return transitionTrigger != null;
	}

	private bool CanFire(FireByTriggerRequest triggerRequest, out TransitionTriggerStateResolver transitionTrigger)
	{
		transitionTrigger = null;
		var configuredTriggerStateResolvers = _currentState.Value!.GetTransitionTriggerStateResolversBy(triggerRequest.Trigger).ToList();
		if (!configuredTriggerStateResolvers.Any())
		{
			throw new TriggerStateResolverNotFoundException(triggerRequest.Trigger);
		}

		if (!TryFindSuitableTransitionTriggerStateResolver(configuredTriggerStateResolvers,
		                                                   TransitionTriggerStateResolverPredicate(triggerRequest.Entity),
		                                                   out TransitionTriggerStateResolver triggerStateResolver))
		{
			return false;
		}

		transitionTrigger = triggerStateResolver;
		return transitionTrigger != null;
	}

	private async Task FireTransition(BaseFireRequest fireRequest)
	{
		var cts = CancellationTokenSource.CreateLinkedTokenSource(fireRequest.CancellationToken);
		_transitionCompletedEvent.Value = new();
		_transitionCompletedEvent.Value.Subscribe(OnTransitionCompletedEventActions);

		var transition = new Transition(fireRequest.Entity,
		                                _currentState.Value!.Current,
		                                _transitionTriggerStateResolver.Value!.Destination,
		                                _transitionTriggerStateResolver.Value.Trigger,
		                                _transitionTriggerStateResolver.Value.TransitionOptions,
		                                cts.Token);

		await RunDefaultEntryActions(transition);

		await _currentState.Value.RunActionsBefore(transition);

		if (_transitionTriggerStateResolver.Value is TransitionActionTriggerStateResolver
		    transitionActionTriggerStateResolver)
		{
			transition.Token.ThrowIfCancellationRequested();
			await transitionActionTriggerStateResolver.Execute(transition);
		}

		transition.Token.ThrowIfCancellationRequested();

		_updateEntityState(transition.Entity, transition.Destination);
		_currentState.Value = _configuredStates[transition.Destination];

		await _configuredStates[transition.Destination].RunActionsAfter(transition);

		await RunDefaultExitActions(transition);

		_transitionCompletedEvent.Value?.Invoke(transition);

		_transitionCompletedEvent.Value?.Unsubscribe(OnTransitionCompletedEventActions);
	}

	private bool CanAutofireTransitionToNextState(AutofireRequest request,
	                                              out TransitionTriggerStateResolver transitionTriggerStateResolver)
	{
		transitionTriggerStateResolver = null;

		var autoFiredTransitionTriggerStateResolvers = _currentState.Value!.GetAutoFiredTransitionTriggerStateResolvers().ToList();
		if (!autoFiredTransitionTriggerStateResolvers.Any())
		{
			return false;
		}

		if (!TryFindSuitableTransitionTriggerStateResolver(autoFiredTransitionTriggerStateResolvers,
		                                                   TransitionTriggerStateResolverPredicate(request.Entity),
		                                                   out TransitionTriggerStateResolver triggerStateResolver))
		{
			return false;
		}

		transitionTriggerStateResolver = triggerStateResolver;
		return transitionTriggerStateResolver != null;
	}

	private bool TryFindSuitableTransitionTriggerStateResolver(
		IEnumerable<TransitionTriggerStateResolver> possibleTriggerStateResolvers,
		Func<TransitionTriggerStateResolver, bool> triggerStateResolverFunc,
		out TransitionTriggerStateResolver triggerStateResolver)
	{
		var suitableTriggerStateResolvers = possibleTriggerStateResolvers.Where(triggerStateResolverFunc).ToList();
		if (suitableTriggerStateResolvers.Count > 1)
		{
			throw new AmbiguousTriggerStateResolverException(suitableTriggerStateResolvers.First().Destination);
		}

		triggerStateResolver = suitableTriggerStateResolvers.SingleOrDefault();
		return triggerStateResolver != null;
	}

	private Func<TransitionTriggerStateResolver, bool> TransitionTriggerStateResolverPredicate(TEntity entity)
	{
		return triggerStateResolver => GuardIsMet(triggerStateResolver, entity);
	}

	private Func<TransitionTriggerStateResolver, bool> TransitionTriggerStateResolverPredicate(FireByStateRequest stateRequest)
	{
		return t => stateRequest.State.Equals(t.Destination) && GuardIsMet(t, stateRequest.Entity);
	}

	private bool GuardIsMet(BaseTriggerStateResolver triggerStateResolver, TEntity entity)
	{
		return triggerStateResolver.Guard.GuardIsMet(entity);
	}

	private async Task RunDefaultEntryActions(Transition transition)
	{
		foreach (var action in DefaultEntryActions)
		{
			await action.ExecuteAsync(transition);
		}
	}

	private async Task RunDefaultExitActions(Transition transition)
	{
		foreach (var action in DefaultExitActions)
		{
			await action.ExecuteAsync(transition);
		}
	}

	private State GetOrConfigure(TState sourceState)
	{
		if (!TryGetStateInstance(sourceState, out State stateInstance))
		{
			stateInstance = new State(sourceState);
			_configuredStates.Add(sourceState, stateInstance);
		}

		return stateInstance;
	}

	private bool TryGetStateInstance(TState state, out State stateInstance)
	{
		return _configuredStates.TryGetValue(state, out stateInstance);
	}

	private static Action<TEntity, TState> AssignStateToEntityAction(
		Expression<Func<TEntity, TState>> propertyAccessor)
	{
		var stateProperty = ((MemberExpression)propertyAccessor.Body).Member;
		var entityParam = Expression.Parameter(typeof(TEntity));
		var stateParam = Expression.Parameter(typeof(TState));
		var assignExpression = Expression.Lambda<Action<TEntity, TState>>(
			Expression.Assign(Expression.Property(entityParam, stateProperty.Name), stateParam),
			entityParam, stateParam);

		return assignExpression.Compile();
	}
}