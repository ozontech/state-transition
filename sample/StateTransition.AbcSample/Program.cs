using StateTransition.AbcSample;

var abcStateMachine = new AbcStateMachine();
var abc = new Abc { State = AbcState.A };
await abcStateMachine.Fire(abc, AbcTrigger.SetB);

Console.WriteLine(abc.State);