using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using NUnit.Framework;

namespace StateTransition.UnitTests;

[TestFixture]
public class StateMachineTests
{
	private Mock<StateMachine<AbcState, AbcTrigger, Abc>.ITransitionAction> _triggerActionMock;

	private AbcStateMachine _abcStateMachine;

	[SetUp]
	public void Init()
	{
		_triggerActionMock = new Mock<StateMachine<AbcState, AbcTrigger, Abc>.ITransitionAction>();
		_abcStateMachine = new AbcStateMachine(sm => sm.State);
	}

	[TestCase(AbcState.A, AbcTrigger.SetB, AbcState.B)]
	[TestCase(AbcState.B, AbcTrigger.SetC, AbcState.C)]
	public async Task MovesStateMachineByTrigger(AbcState sourceState, AbcTrigger trigger, AbcState expectedState)
	{
		//Arrange
		var abc = new Abc { State = sourceState };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);
		_abcStateMachine.SetFiniteState(AbcState.C);

		// event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			transition.Trigger.Should().Be(trigger);
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(trigger, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(expectedState);
	}

	[TestCase(AbcState.A, AbcState.B)]
	[TestCase(AbcState.B, AbcState.C)]
	public async Task MovesStateMachineByConcreteState(AbcState sourceState, AbcState concreteState)
	{
		//Arrange
		var abc = new Abc { State = sourceState };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);
		_abcStateMachine.SetFiniteState(AbcState.C);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByStateRequest(concreteState, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(concreteState);
	}

	[Test]
	public async Task MovesStateMachineByAutofireToFiniteState()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo<TransitionOptions>(AbcState.B, AbcTrigger.Auto, customTransitionOptions: options => options.IsAutofire = true);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo<TransitionOptions>(AbcState.C, AbcTrigger.Auto, customTransitionOptions: options => options.IsAutofire = true);

		_abcStateMachine.SetFiniteState(AbcState.C);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.Auto, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.C);
	}

	[Test]
	public async Task ExecutesTriggerActionDuringTransitioning()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB, _triggerActionMock.Object);

		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));
		_abcStateMachine.SetFiniteState(AbcState.B);
		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		using var scope = new AssertionScope();
		abc.State.Should().Be(AbcState.B);
		_triggerActionMock.Verify(
			action => action.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()),
			Times.Once);
	}

	[Test]
	public async Task SequentiallyExecutesChainWithTwoMiddlewareHandlersBeforeFiringTheTransitionItself()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.InitDefaultStateTransitionOptions<TransitionOptionsWithMiddlewareLogArgs>();

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC, transitionAction: new TransitionLogger());
		_abcStateMachine.SetFiniteState(AbcState.C);

		// the order of configuring of the middleware and state machine transition matters
		var nextMiddleware = new CustomTransitionMiddlewareHandler();
		_abcStateMachine.UseMiddleware(nextMiddleware);
		var rootMiddleware = new DefaultTransitionMiddlewareHandler();
		_abcStateMachine.UseMiddleware(rootMiddleware);

		var expectedLogOrder = $"{nameof(DefaultTransitionMiddlewareHandler)}{nameof(CustomTransitionMiddlewareHandler)}{nameof(TransitionLogger)}";

		// event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(t =>
		{
			// assert
			if (t.TransitionOptions is TransitionOptionsWithMiddlewareLogArgs options)
			{
				options.TransitionArgs.Log.Should().BeEquivalentTo(expectedLogOrder);
			}
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetC, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.C);
	}

	[TestCase(AbcState.A, AbcState.B, true)]
	[TestCase(AbcState.A, AbcState.A, false)]
	public async Task ExecutesTransitionByTriggerOnlyWhenGuardIsMet(AbcState sourceState, AbcState expectedState,
	                                                                bool isTransitionAvailable)
	{
		//Arrange
		var abc = new Abc { State = sourceState };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB, guardExpression: _ => isTransitionAvailable);
		_abcStateMachine.SetFiniteState(AbcState.B);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(expectedState);
	}

	[TestCase(AbcState.A, AbcState.B, true)]
	[TestCase(AbcState.A, AbcState.A, false)]
	public async Task ExecutesTransitionByStateOnlyWhenGuardIsMet(AbcState sourceState, AbcState expectedState, bool isTransitionAvailable)
	{
		//Arrange
		var abc = new Abc { State = sourceState };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB, guardExpression: _ => isTransitionAvailable);
		_abcStateMachine.SetFiniteState(AbcState.B);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByStateRequest(AbcState.B, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(expectedState);
	}

	[Test]
	public async Task ExecutesTransitionToStateByCorrectTriggerAndWhereGuardIsMet()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB, guardExpression: _ => false)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetC, guardExpression: _ => true);
		_abcStateMachine.SetFiniteState(AbcState.B);

		// event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			transition.Trigger.Should().Be(AbcTrigger.SetC);
		});

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByStateRequest(AbcState.B, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task IgnoresStateTransitioningWhenCurrentStateIsFinite()
	{
		//Arrange
		var abc = new Abc { State = AbcState.C };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetB);

		_abcStateMachine.SetFiniteState(AbcState.C);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetC, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.C);
	}

	[Test]
	public void ThrowsAmbiguousStateConfigurationExceptionWhenCurrentStateIsConfiguredAndSetupAsFinite()
	{
		//Arrange

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);

		//Act
		Action finiteStateAction = () => _abcStateMachine.SetFiniteState(AbcState.B);

		//Assert
		finiteStateAction.Should()
		                 .Throw<StateMachine<AbcState, AbcTrigger, Abc>.AmbiguousStateConfigurationException>()
		                 .WithMessage($"Multiple configurations detected for {AbcState.B} state");
	}

	[Test]
	public async Task ThrowsTransitionExceptionWhenTriggerActionCatchesCommonException()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB,
		                                 transitionAction: _triggerActionMock.Object);
		_abcStateMachine.SetFiniteState(AbcState.B);
		_triggerActionMock
			.Setup(x => x.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()))
			.Throws<Exception>();

		//Act
		Func<Task> fire = () =>
			_abcStateMachine.Fire(
				new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		using var scope = new AssertionScope();
		await fire.Should().ThrowAsync<StateMachine<AbcState, AbcTrigger, Abc>.TransitionException>()
		          .Where(x => x.Transition != null)
		          .WithMessage($"An error occurred while transitioning from {AbcState.A} to {AbcState.B}");
		_triggerActionMock.Verify(
			action => action.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()),
			Times.Once);
	}

	[Test]
	public async Task
		ThrowsAmbiguousTriggerStateResolverExceptionWhenOneMoreTriggerSetupForTheSameDestinationState()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetC);

		//Act
		Func<Task> fire = () =>
			_abcStateMachine.Fire(
				new StateMachine<AbcState, AbcTrigger, Abc>.FireByStateRequest(AbcState.B, abc, CancellationToken.None));

		//Assert
		await fire.Should()
		          .ThrowAsync<StateMachine<AbcState, AbcTrigger, Abc>.AmbiguousTriggerStateResolverException>()
		          .WithMessage($"Multiple trigger state resolvers detected for the transitioning to {AbcState.B} state");
	}

	[Test]
	public async Task ThrowsStateNotConfiguredExceptionWhenNoConfiguredSourceStateForTheTransitioning()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };

		//Act
		Func<Task> fire = () =>
			_abcStateMachine.Fire(
				new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		using var scope = new AssertionScope();
		abc.State.Should().Be(AbcState.A);
		await fire.Should().ThrowAsync<StateMachine<AbcState, AbcTrigger, Abc>.StateNotConfiguredException>()
		          .WithMessage($"The {AbcState.A} state has not been configured or unmarked as finite");
	}

	[Test]
	public async Task
		ThrowsTriggerStateResolverNotFoundExceptionWhenNoTriggerForTheTransitioningToDestinationState()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A);

		//Act
		Func<Task> fire = () =>
			_abcStateMachine.Fire(
				new StateMachine<AbcState, AbcTrigger, Abc>.FireByStateRequest(AbcState.B, abc, CancellationToken.None));

		//Assert
		await fire.Should()
		          .ThrowAsync<StateMachine<AbcState, AbcTrigger, Abc>.TriggerStateResolverNotFoundException>()
		          .WithMessage(
			          $"No suitable trigger state resolver configured for the transitioning to {AbcState.B} state");
	}

	[Test]
	public async Task
		ThrowsTriggerStateResolverNotFoundExceptionWhenNoTriggerForTheTransitioningByConcreteTrigger()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A);

		//Act
		Func<Task> fire = () =>
			_abcStateMachine.Fire(
				new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		await fire.Should()
		          .ThrowAsync<StateMachine<AbcState, AbcTrigger, Abc>.TriggerStateResolverNotFoundException>()
		          .WithMessage(
			          $"No suitable trigger state resolver configured for the transitioning using {AbcTrigger.SetB} trigger");
	}

	[Test]
	public async Task ExecutesDefaultEntryExitActionsAndBeforeAfterTransitioningAccordingToConfiguredOrder()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		const string actionCompletedPostfix = " completed";
		const string firstAction = "AddDefaultEntryAction";
		const string secondAction = "BeforeTransitionTo";
		const string thirdAction = "AddTransitionTo";
		const string fourthAction = "AfterTransitionTo";
		const string fifthAction = "AddDefaultExitAction";

		var expectedLoggedTransitionActions = new List<ActionLogger>
		{
			new(string.Concat(firstAction, actionCompletedPostfix)),
			new(string.Concat(secondAction, actionCompletedPostfix)),
			new(string.Concat(thirdAction, actionCompletedPostfix)),
			new(string.Concat(fourthAction, actionCompletedPostfix)),
			new(string.Concat(fifthAction, actionCompletedPostfix))
		};

		_abcStateMachine.AddDefaultEntryAction(new ActionLogger(firstAction));
		_abcStateMachine.Configure(AbcState.A)
		                .BeforeTransitionTo(AbcState.B, new ActionLogger(secondAction))
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB, transitionAction: new ActionLogger(thirdAction))
		                .AfterTransitionTo(AbcState.B, new ActionLogger(fourthAction));
		_abcStateMachine.AddDefaultExitAction(new ActionLogger(fifthAction));

		// event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			transition.ActionLog.Actions.Should().BeEquivalentTo(expectedLoggedTransitionActions);
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));
	}

	[Test]
	public async Task ThrowsOperationCanceledExceptionAndRollbacksTransition()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };
		var cts = new CancellationTokenSource(500);
		_triggerActionMock
			.Setup(x => x.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()))
			.Callback(() => Thread.Sleep(550));

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB,
		                                 transitionAction: _triggerActionMock.Object);
		_abcStateMachine.SetFiniteState(AbcState.B);

		//Act
		Func<Task> fire = () => _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, cts.Token));

		//Assert
		using var scope = new AssertionScope();
		await fire.Should().ThrowAsync<OperationCanceledException>();
		_triggerActionMock.Verify(
			action => action.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()),
			Times.Once);
		abc.State.Should().Be(AbcState.A);
	}

	[Test]
	public async Task StateMachineAllowsTransitioningByTriggerToTheSameCurrentState()
	{
		//Arrange
		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));

		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB, _triggerActionMock.Object)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);

		_abcStateMachine.SetFiniteState(AbcState.C);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);

		//Act
		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		await _abcStateMachine.Fire(
			new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		_triggerActionMock.Verify(
			action => action.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()),
			Times.Exactly(2));
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task StateMachineSkipsAutofireTransitioningToTheSameCurrentState()
	{
		//Arrange
		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));

		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo<TransitionOptions>(AbcState.B, AbcTrigger.Auto, transitionAction: _triggerActionMock.Object, customTransitionOptions: options => options.IsAutofire = true);

		_abcStateMachine.SetFiniteState(AbcState.C);

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.Auto, abc, CancellationToken.None));

		//Assert
		_triggerActionMock.Verify(
			action => action.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()),
			Times.Exactly(1));
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task AppliesDefaultAndCustomTransitionOptionsToMoveOnDestinationState()
	{
		//Arrange
		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));

		var abc = new Abc { State = AbcState.A };

		_abcStateMachine.InitDefaultStateTransitionOptions<TransitionOptions>(transitionOptions => { transitionOptions.Tracer = new CustomTracer(); });
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo<TransitionOptions>(AbcState.B, AbcTrigger.SetB,
		                                                    customTransitionOptions: transitionOptions => { transitionOptions.IsTransactionalScope = true; },
		                                                    transitionAction: _triggerActionMock.Object);

		_abcStateMachine.SetFiniteState(AbcState.B);

		// event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as TransitionOptions)!;
			transitionOptions.IsTransactionalScope.Should().BeTrue();
			transitionOptions.Tracer.Should().NotBeNull();
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task AppliesOnlyCustomTransitionOptionsToMoveOnDestinationState()
	{
		//Arrange
		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));

		var abc = new Abc { State = AbcState.A };

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo<TransitionOptions>(AbcState.B, AbcTrigger.SetB,
		                                                    customTransitionOptions: transitionOptions => { transitionOptions.IsTransactionalScope = true; },
		                                                    transitionAction: _triggerActionMock.Object);

		_abcStateMachine.SetFiniteState(AbcState.B);
		//event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as TransitionOptions)!;
			transitionOptions.IsTransactionalScope.Should().BeTrue();
			transitionOptions.Tracer.Should().BeNull();
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task AppliesOnlyDefaultTransitionOptionsToMoveOnDestinationState()
	{
		//Arrange
		var abc = new Abc { State = AbcState.A };

		_abcStateMachine.InitDefaultStateTransitionOptions<TransitionOptions>(transitionOptions =>
		{
			transitionOptions.IsTransactionalScope = true;
			transitionOptions.Tracer = new CustomTracer();
		});
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);

		_abcStateMachine.SetFiniteState(AbcState.B);
		//event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as TransitionOptions)!;
			transitionOptions.IsTransactionalScope.Should().BeTrue();
			transitionOptions.Tracer.Should().NotBeNull();
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task DefaultTransitionOptionsWontBeAppliedForCustomTransitionOptions()
	{
		//Arrange
		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));
		var abc = new Abc { State = AbcState.A };

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo<TransitionOptions>(AbcState.B, AbcTrigger.SetB,
		                                                    customTransitionOptions: transitionOptions =>
		                                                    {
			                                                    transitionOptions.IsTransactionalScope = true;
			                                                    transitionOptions.IsAutofire = true;
		                                                    },
		                                                    transitionAction: _triggerActionMock.Object);

		_abcStateMachine.InitDefaultStateTransitionOptions<TransitionOptions>(transitionOptions => { transitionOptions.Tracer = new CustomTracer(); });
		_abcStateMachine.SetFiniteState(AbcState.B);
		//event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as TransitionOptions)!;
			transitionOptions.IsTransactionalScope.Should().BeTrue();
			transitionOptions.Tracer.Should().BeNull();
			transitionOptions.IsAutofire.Should().BeTrue();
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task DefaultTransitionOptionsWillBeAppliedForCustomTransitionOptions()
	{
		//Arrange
		_triggerActionMock.Setup(behaviour =>
			                         behaviour.ExecuteAsync(It.IsAny<StateMachine<AbcState, AbcTrigger, Abc>.Transition>()));
		var abc = new Abc { State = AbcState.A };

		// the order of configuring of the default transition options before custom options is matter
		_abcStateMachine.InitDefaultStateTransitionOptions<TransitionOptions>(transitionOptions =>
		{
			transitionOptions.Tracer = new CustomTracer();
			transitionOptions.IsAutofire = true;
		});

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo<TransitionOptions>(AbcState.B, AbcTrigger.SetB,
		                                                    customTransitionOptions: transitionOptions => { transitionOptions.IsTransactionalScope = true; },
		                                                    transitionAction: _triggerActionMock.Object);

		_abcStateMachine.SetFiniteState(AbcState.B);

		//event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as TransitionOptions)!;
			transitionOptions.IsTransactionalScope.Should().BeTrue();
			transitionOptions.Tracer.Should().NotBeNull();
			transitionOptions.IsAutofire.Should().BeTrue();
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));
		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public void ThrowExceptionWhenDefaultTransitionOptionsTypeIsDiffersFromTheCustomTransitionOptionsType()
	{
		//Arrange
		// the order of configuring of the default transition options before custom options is matter
		_abcStateMachine.InitDefaultStateTransitionOptions<TransitionOptions>(transitionOptions => { transitionOptions.Tracer = new CustomTracer(); });

		//Act
		Action fireTransitionAction = () => _abcStateMachine.Configure(AbcState.A)
		                                                    .AddTransitionTo<CustomTransitionOptions>(AbcState.B, AbcTrigger.SetB,
		                                                                                              customTransitionOptions: transitionOptions => { transitionOptions.IsRetryOnFailed = true; });

		//Assert
		fireTransitionAction.Should()
		                    .Throw<ArgumentException>()
		                    .WithMessage($"Type of the custom transition options ({nameof(CustomTransitionOptions)}) differs from the default ({nameof(TransitionOptions)})");
	}

	[Test]
	public async Task AppliesDefaultTransitionArgsDuringTransitioning()
	{
		//Arrange
		string args = "Order was canceled by client";
		// the order of configuring of the default transition options before custom options is matter
		_abcStateMachine.InitDefaultStateTransitionOptions<CustomTransitionOptionsWithArgs>(transitionOptions =>
		{
			transitionOptions.TransitionArgs = new TransitionArgs
			{
				TransitionReason = args
			};
		});

		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);

		_abcStateMachine.SetFiniteState(AbcState.B);

		//event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as CustomTransitionOptionsWithArgs)!;
			transitionOptions.TransitionArgs.TransitionReason.Should().Be(args);
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public async Task AppliesCustomTransitionArgsDuringTransitioning()
	{
		//Arrange
		string args = "Order was canceled by client";

		var abc = new Abc { State = AbcState.A };
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo<CustomTransitionOptionsWithArgs>(AbcState.B, AbcTrigger.SetB, options => options.TransitionArgs.TransitionReason = args);

		_abcStateMachine.SetFiniteState(AbcState.B);

		//event subscribing to assert
		_abcStateMachine.OnTransitionCompleted(transition =>
		{
			// assert
			var transitionOptions = (transition.TransitionOptions as CustomTransitionOptionsWithArgs)!;
			transitionOptions.TransitionArgs.TransitionReason.Should().Be(args);
		});

		//Act
		await _abcStateMachine.Fire(new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(AbcTrigger.SetB, abc, CancellationToken.None));

		//Assert
		abc.State.Should().Be(AbcState.B);
	}

	[Test]
	public void GetsStateMachineTransitionsGraphBasedOnMermaidNotation()
	{
		//Arrange
		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);
		_abcStateMachine.SetFiniteState(AbcState.C);

		string expectedGraph = @"```mermaid
graph TD;
A-->B;
B-->C;
```
";

		//Act
		var graph = _abcStateMachine.GetTransitionsGraph();

		//Assert
		graph.Should().BeEquivalentTo(expectedGraph);
	}

	[Test]
	public async Task RunsSeriesOfParallelStateTransitionsAndGuaranteesMultiThreadingSafety()
	{
		//Arrange
		var requestsSet = GenerateTransitionTriggerRequestsSet();

		_abcStateMachine.Configure(AbcState.A)
		                .AddTransitionTo(AbcState.B, AbcTrigger.SetB)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);
		_abcStateMachine.Configure(AbcState.B)
		                .AddTransitionTo(AbcState.C, AbcTrigger.SetC);
		_abcStateMachine.SetFiniteState(AbcState.C);

		var tasks = new List<Task>();

		foreach (EntityStateTransitionTriggerRequest request in requestsSet)
		{
			tasks.Add(Task.Run(async () =>
			{
				await _abcStateMachine.Fire(request.TriggerRequest);
				//Assert
				request.Entity.State.Should().Be(request.ExpectedState);
			}));
		}

		//Act
		await Task.WhenAll(tasks);

		List<EntityStateTransitionTriggerRequest> GenerateTransitionTriggerRequestsSet()
		{
			return new List<EntityStateTransitionTriggerRequest>
			{
				new(AbcState.B, AbcTrigger.SetB),
				new(AbcState.C, AbcTrigger.SetC),
				new(AbcState.B, AbcTrigger.SetB),
				new(AbcState.C, AbcTrigger.SetC),
				new(AbcState.B, AbcTrigger.SetB),
				new(AbcState.C, AbcTrigger.SetC),
				new(AbcState.B, AbcTrigger.SetB),
				new(AbcState.C, AbcTrigger.SetC),
				new(AbcState.B, AbcTrigger.SetB),
				new(AbcState.C, AbcTrigger.SetC)
			};
		}
	}
}