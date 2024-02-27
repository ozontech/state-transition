using BenchmarkDotNet.Running;
using StateTransition.Benchmark;

var summary = BenchmarkRunner.Run<OpenClosedStateMachine>();
Console.Write(summary);