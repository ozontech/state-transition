namespace StateTransition.AbcSample;

public enum AbcState
{
	A,
	B,
	C
}

public enum AbcTrigger
{
	SetB,
	SetC,
	Auto
}

public class Abc
{
	public AbcState State { get; set; }
}

public class AbcStateMachine : StateMachine<AbcState, AbcTrigger, Abc>
{
	public AbcStateMachine() : base(abc => abc.State)
	{
		Configure(AbcState.A)
			.AddTransitionTo(AbcState.B, AbcTrigger.SetB);

		Configure(AbcState.B)
			.AddTransitionTo<BaseTransitionOptions>(AbcState.C, AbcTrigger.Auto, options => options.IsAutofire = true);

		SetFiniteState(AbcState.C);
	}

	public async Task Fire(Abc abc, AbcTrigger trigger)
	{
		await Fire(new FireByTriggerRequest(trigger, abc, CancellationToken.None));
	}
}