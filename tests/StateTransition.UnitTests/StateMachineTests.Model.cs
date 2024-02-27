using System.Linq.Expressions;

namespace StateTransition.UnitTests;

public class Abc
{
	public AbcState State { get; init; }
}

public enum AbcState
{
	A,
	B,
	C
}

public enum AbcTrigger
{
	Auto,
	SetB,
	SetC
}

internal class AbcStateMachine : StateMachine<AbcState, AbcTrigger, Abc>
{
	public AbcStateMachine(Expression<Func<Abc, AbcState>> entityStateSelector) : base(entityStateSelector) { }
}

internal class DefaultTransitionMiddlewareHandler : TransitionMiddlewareHandler
{
	public override Task Handle<TRequest>(TRequest request, BaseTransitionOptions transitionOptions)
	{
		if (transitionOptions is TransitionOptionsWithMiddlewareLogArgs options)
		{
			options.TransitionArgs.Log += nameof(DefaultTransitionMiddlewareHandler);
		}
		return base.Handle(request, transitionOptions);
	}
}

internal class CustomTransitionMiddlewareHandler : TransitionMiddlewareHandler
{
	public override Task Handle<TRequest>(TRequest request, BaseTransitionOptions transitionOptions)
	{
		if (transitionOptions is TransitionOptionsWithMiddlewareLogArgs options)
		{
			options.TransitionArgs.Log += nameof(CustomTransitionMiddlewareHandler);
		}
		return base.Handle(request, transitionOptions);
	}
}

internal class TransitionOptions : BaseTransitionOptions
{
	public override bool IsAutofire { get; set; }

	public bool IsTransactionalScope { get; set; }

	public CustomTracer Tracer { get; set; }
}

internal class CustomTransitionOptions : BaseTransitionOptions
{
	public override bool IsAutofire { get; set; }

	public bool IsRetryOnFailed { get; set; }
}

internal class CustomTransitionOptionsWithArgs : BaseTransitionOptions<TransitionArgs>
{
	public override bool IsAutofire { get; set; }
}

public class TransitionArgs
{
	public string TransitionReason { get; set; }
}

public class TransitionHandlersLog
{
	public string Log { get; set; }
}

internal class TransitionOptionsWithMiddlewareLogArgs : BaseTransitionOptions<TransitionHandlersLog>
{
	public override bool IsAutofire { get; set; }
}

public class CustomTracer { }

internal class ActionLogger : StateMachine<AbcState, AbcTrigger, Abc>.ITransitionAction
{
	public ActionLogger(string executionLogName)
	{
		ExecutionLog = executionLogName;
	}

	public string ExecutionLog { get; private set; }

	public async Task ExecuteAsync(StateMachine<AbcState, AbcTrigger, Abc>.Transition transition)
	{
		await Task.Run(() => ExecutionLog += " completed");
	}
}

internal class TransitionLogger : StateMachine<AbcState, AbcTrigger, Abc>.ITransitionAction
{
	public Task ExecuteAsync(StateMachine<AbcState, AbcTrigger, Abc>.Transition transition)
	{
		if (transition.TransitionOptions is TransitionOptionsWithMiddlewareLogArgs options)
		{
			options.TransitionArgs.Log += nameof(TransitionLogger);
		}
		return Task.CompletedTask;
	}
}

public class EntityStateTransitionTriggerRequest
{
	public Abc Entity { get; } = new() { State = AbcState.A };

	public AbcState ExpectedState { get; }

	public StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest TriggerRequest { get; }

	public EntityStateTransitionTriggerRequest(AbcState expectedState, AbcTrigger triggerRequest)
	{
		ExpectedState = expectedState;
		TriggerRequest = new StateMachine<AbcState, AbcTrigger, Abc>.FireByTriggerRequest(triggerRequest, Entity, CancellationToken.None);
	}
}