using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace StateTransition.Benchmark;

public enum DoorTrigger
{
	Open,
	Close
}

public class Door
{
	public DoorState State { get; set; }

	public bool Locked { get; set; }
}

public enum DoorState
{
	Opened = 1,
	Closed = 2
}

internal class KnockKnockAction : StateMachine<DoorState, DoorTrigger, Door>.ITransitionAction
{
	public Task ExecuteAsync(StateMachine<DoorState, DoorTrigger, Door>.Transition transition)
	{
		Console.WriteLine("Knock-knock! Are you open?");
		return Task.CompletedTask;
	}
}

[SimpleJob(RunStrategy.Throughput, RuntimeMoniker.Net60)]
public class OpenClosedStateMachine : StateMachine<DoorState, DoorTrigger, Door>
{
	private readonly Door _door = new() { State = DoorState.Closed };

	public OpenClosedStateMachine() : base(d => d.State)
	{
		Configure(DoorState.Closed)
			.AddTransitionTo(DoorState.Opened, DoorTrigger.Open, guardExpression: abc => !abc.Locked, transitionAction: new KnockKnockAction());

		SetFiniteState(DoorState.Opened);
	}

	[Benchmark(Baseline = true)]
	public async Task Fire()
	{
		await Fire(new FireByTriggerRequest(DoorTrigger.Open, _door, CancellationToken.None));
	}
}