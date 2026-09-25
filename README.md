[![NuGet](https://img.shields.io/nuget/v/StateTransition)](https://www.nuget.org/packages/StateTransition/)

![](logo.png)

# Overview

The `StateTransition` is a simple, but very handy .NET library for configuring of the state machine.

The API of the `StateTransition` provides complete set of features for flexible and quick configuring the state machine by state or trigger with a number of different useful options.
There is everything you need and no frills.
> `less code, more opportunities`

## Disclaimer
⚠ Although we use it in production, it still isn't v1.0.0. Please, test your configured state machines carefully on dev/stage environments before deploying to the prod.

## Contributing
The `StateTransition` is an open-source project and we will thank you for your contributions to our project. Read the rules [here](./CONTRIBUTING.md).

## Motivation
When you need to orchestrate some business process, then ensuring reliable and flexible orchestration of the process will often depend on the possibilities of the state machine engine.

We are defined 4 important aspects for `StateTransition`'s engine, that we'd like to have for best matching and cover our requirements:
- `loosely coupled & entity-oriented`
- `flexibility`
- `reliability`
- `simplicity`

Well, before developing own implementation we made a short research of the couple similar popular libs for configuring of state machines: [stateless](https://github.com/dotnet-state-machine/stateless)
and [automatonymous](http://masstransit-project.com/Automatonymous/). But these solutions didn’t match all above our requirements.

## Main features

So let's look at the implemented features based on our requirements.
- `loosely coupled & entity-oriented`
  - The configuration of the `StateTransition` engine is built around the working with `TEntity` object type. Under the hood of engine the `low coupling` characteristic and this is confirmed at least by the fact that the `TEntity` type knows nothing about the state-machine itself and that it controls its state. Because of this, the implementation of entity state changes is flexible and adaptive, and you only need to specify the name of the property that will be responsible for the `TEntity` state. So it allows the `StateTransition` to have full control over the process of changing the state of `TEntity`. Moreover, the `StateTransition`'s engine is absolutely state-less.
  - This is an unique and distinctive difference from many other state-machine solutions.
- `flexibility`
  - A flexible way to work with options to control the transition. Out of the box, 1 basic `IsAutofire` option will already be available, which tells the `StateTransition`'s engine that you can automatically fire a transition to the next state without any trigger.
    - The `StateTransition`'s engine provides the ability to set your custom options under each transition.
      - For example, in our services we use the custom `UseTransactionScope` option to specify a `compensable` transaction that will roll back changes that were previously applied before an error occurred in the system.
      - You can override the `IsAutofire` option value if needed for your custom option.
  - It is possible to 
    - pass additional custom arguments available as part of the transition options
    - fire the state transitioning by using `TState` or `TTrigger`
    - to configure custom `entry/exit` actions that are executed before and after a **specific** transition
    - to configure of the default `entry/exit` action that are always executed before and after **any** transition
    - loop the transition to the same `TState` state by using specific `TTrigger`
    - define transition guards to check the condition on a particular transition from the current to the final state
    - build a transitions state graph using Mermaid notation based on a configured state machine
- `reliability`
  - Out of the box, own native `middleware` functionality is available to provide `reliable` control. Let's look at the most basic and useful examples of how to use this functionality by our opinion:
      - ensuring data integrity and consistency to avoid distributed transactions
      - tracing and logging during the transition
      - handling of exceptions that occurred during the transition, etc.
- `simplicity`
  - Easily and handy customizable configuration and to be sure it is enough to get acquainted with [Getting started](#getting-started) section
  
## Performance
```config
BenchmarkDotNet v0.13.12, macOS Sonoma 14.2.1 (23C71) [Darwin 23.2.0]
Apple M1, 1 CPU, 8 logical and 8 physical cores
.NET SDK 7.0.305
[Host]     : .NET 6.0.3 (6.0.322.12309), Arm64 RyuJIT AdvSIMD
Job-VWVFSC : .NET 6.0.3 (6.0.322.12309), Arm64 RyuJIT AdvSIMD

Runtime=.NET 6.0  RunStrategy=Throughput

// * Legends *
Mean   : Arithmetic mean of all measurements
Error  : Half of 99.9% confidence interval
StdDev : Standard deviation of all measurements
Ratio  : Mean of the ratio distribution ([Current]/[Baseline])
1 ns   : 1 Nanosecond (0.000000001 sec)
```

Transitioning by trigger to the specific state with custom transition action and boolean guard can achieve the following benchmark:

| Method | Mean     | Error    | StdDev   | Ratio |
|------- |---------:|---------:|---------:|------:|
| Fire   | 30.24 ns | 0.187 ns | 0.146 ns |  1.00 |

It is worth taking into account that during these `30+ ns` we also had time to set a new value for the `State` property of the related `TEntity` object type.
You can explore our project for benchmarks and run it to verify these calculations! [Here it is](benchmark/StateTransition.Benchmark/OpenClosedStateMachine.cs)!

## Getting Started

To start configure your state machine, all you need to do is declare a new class that will be inherited from the base class
generic class `StateMachine<TState,TTrigger,TEntity>`.

The base class `StateMachine<TState,TTrigger,TEntity>` has 3 types of parameters where

- `TState` allows to describe your type with possible states that can be used to add transitions.
  - Initialization of the state machine involves passing a property with `TState` type responsible for the state of the entity object, which will be updated after the transition to a new status.
- `TTrigger` allows to describe the necessary triggers that will be used to call the transition on the of the state machine.
- `TEntity` allows you to specify the type of entity for which the state machine itself will be configured, with all necessary transitions from one state to another.

Now let's look how to easily start configure your state machine using the below sample.

## Sample with simple state machine

To feel the power of the state-transition's engine let's build a more realistic example — an **e-commerce order lifecycle** (created → paid → fulfilled → shipped → delivered, with cancellations and returns). It exercises almost every `StateTransition` feature at once:

- transition **guards** (`Func<TEntity, bool>`),
- transition **actions** (`ITransitionAction`) and per-transition **entry/exit** actions (`BeforeTransitionTo` / `AfterTransitionTo`),
- default **entry/exit** actions executed around **any** transition (audit trail),
- **custom transition options** — a subclass of `BaseTransitionOptions<TArgs>` with its own flags and arguments,
- a **middleware** pipeline (tracing, transaction scope, retry),
- **autofire** — automatic transition without an external trigger,
- firing both **by trigger** and **by state**,
- **`OnTransitionCompleted`** and **`GetTransitionsGraph()`** (Mermaid).

### Domain

```csharp
public enum OrderState
{
    Placed,             // created
    PaymentProcessing,  // payment in progress
    Paid,               // paid
    InFulfillment,      // confirmed, on the warehouse
    ReadyToShip,        // ready for shipment
    Shipped,            // shipped
    Delivered,          // delivered (finite)
    Cancelled,          // cancelled (finite)
    Returned,           // return requested
    Refunded            // money returned (finite)
}

public enum OrderTrigger
{
    StartPayment,
    PaymentSucceeded,
    RetryPayment,
    Confirm,
    AssignWarehouse,
    Ship,
    Deliver,
    Cancel,
    RequestReturn,
    Refund
}

public class Order
{
    public OrderState State { get; set; }
    public long Id { get; init; }
    public decimal Total { get; init; }
    public bool PaymentCaptured { get; set; }
    public bool WarehouseAssigned { get; set; }
}
```

### Custom transition options with arguments

```csharp
public class OrderTransitionOptions : BaseTransitionOptions<OrderTransitionArgs>
{
    public override bool IsAutofire { get; set; }
    public bool UseTransactionScope { get; set; }
    public bool IsRetryableOnFailure { get; set; }
}

public class OrderTransitionArgs
{
    public string Reason { get; set; }
    public long OperatorId { get; set; }
}
```

The generic base class `BaseTransitionOptions<TArgs>` passes custom arguments (`Reason`, `OperatorId`) along with the transition.

### Transition actions

```csharp
public class CapturePaymentAction : StateMachine<OrderState, OrderTrigger, Order>.ITransitionAction
{
    public async Task ExecuteAsync(StateMachine<OrderState, OrderTrigger, Order>.Transition t)
    {
        // real gateway call; Entity / Destination / TransitionOptions are available via t
        t.Entity.PaymentCaptured = true;
        await Task.CompletedTask;
    }
}

public class RefundPaymentAction : StateMachine<OrderState, OrderTrigger, Order>.ITransitionAction { /* ... */ }
public class ReserveStockAction   : StateMachine<OrderState, OrderTrigger, Order>.ITransitionAction { /* ... */ }
public class ReleaseStockAction   : StateMachine<OrderState, OrderTrigger, Order>.ITransitionAction { /* ... */ }
public class AssignWarehouseAction : StateMachine<OrderState, OrderTrigger, Order>.ITransitionAction
{
    public Task ExecuteAsync(StateMachine<OrderState, OrderTrigger, Order>.Transition t)
    {
        t.Entity.WarehouseAssigned = true;
        return Task.CompletedTask;
    }
}
public class AuditTrailAction     : StateMachine<OrderState, OrderTrigger, Order>.ITransitionAction { /* ... */ }
```

### Middleware

```csharp
public class TracingMiddleware : TransitionMiddlewareHandler
{
    public override async Task Handle<TRequest>(TRequest req, BaseTransitionOptions options)
    {
        // open a span / Jaeger, propagate trace_id from request.Entity
        await base.Handle(req, options);
    }
}

public class TransactionScopeMiddleware : TransitionMiddlewareHandler
{
    public override async Task Handle<TRequest>(TRequest req, BaseTransitionOptions options)
    {
        if (options is OrderTransitionOptions { UseTransactionScope: true })
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            await base.Handle(req, options);
            scope.Complete();
        }
        else
        {
            await base.Handle(req, options);
        }
    }
}

public class RetryOnFailureMiddleware : TransitionMiddlewareHandler
{
    public override async Task Handle<TRequest>(TRequest req, BaseTransitionOptions options)
    {
        const int attempts = 3;
        for (var i = 0; i < attempts; i++)
        {
            try { await base.Handle(req, options); return; }
            catch (Exception) when (i < attempts - 1 && options is OrderTransitionOptions { IsRetryableOnFailure: true })
            {
                await Task.Delay(100 * (i + 1));
            }
        }

        await base.Handle(req, options);
    }
}
```

> **Note.** The order of the middleware matters and is inverted: `UseMiddleware` puts each new handler at the head of the stack, so **the last registered middleware runs first**. In the example below `TransactionScopeMiddleware` is registered last so that it wraps the whole pipeline.

### The state machine itself

```csharp
public class OrderStateMachine : StateMachine<OrderState, OrderTrigger, Order>
{
    public OrderStateMachine() : base(order => order.State)
    {
        // default entry/exit — executed BEFORE/AFTER any transition (audit)
        AddDefaultExitAction(new AuditTrailAction());

        // middleware: order matters — the last one added runs first
        UseMiddleware(new TracingMiddleware());
        UseMiddleware(new RetryOnFailureMiddleware());
        UseMiddleware(new TransactionScopeMiddleware());

        // transition completion -> e.g. log or emit a domain event to external subscribers
        OnTransitionCompleted(t => Console.WriteLine($"Order {t.Entity.Id}: {t.Source} -> {t.Destination}"));

        // NOTE: each AddTransitionTo<OrderTransitionOptions> below gets its own fresh options
        // instance, so per-transition flags (IsAutofire, transaction, retry) don't leak to
        // other transitions. Do NOT use InitDefaultStateTransitionOptions here — it would share
        // ONE options object across all transitions and IsAutofire would become global.

        // ---- payment ----
        Configure(OrderState.Placed)
            .AddTransitionTo<OrderTransitionOptions>(OrderState.PaymentProcessing, OrderTrigger.StartPayment, o => { })
            .AddTransitionTo<OrderTransitionOptions>(OrderState.Cancelled, OrderTrigger.Cancel, o => { });

        // transition to the same state — the "loop" feature (retry cycle)
        Configure(OrderState.PaymentProcessing)
            .AddTransitionTo<OrderTransitionOptions>(OrderState.PaymentProcessing, OrderTrigger.RetryPayment, o => { })
            .AddTransitionTo<OrderTransitionOptions>(OrderState.Cancelled, OrderTrigger.Cancel, o => { });

        Configure(OrderState.PaymentProcessing)
            .AddTransitionTo<OrderTransitionOptions>(
                OrderState.Paid, OrderTrigger.PaymentSucceeded,
                o => { o.IsRetryableOnFailure = true; o.UseTransactionScope = true; },
                transitionAction: new CapturePaymentAction());

        // ---- fulfillment ----
        // before entering InFulfillment we reserve the stock (entry-to a specific destination)
        Configure(OrderState.Paid)
            .AddTransitionTo<OrderTransitionOptions>(
                OrderState.InFulfillment, OrderTrigger.Confirm,
                o => { },
                transitionAction: new ReserveStockAction())
            .AddTransitionTo<OrderTransitionOptions>(
                OrderState.Cancelled, OrderTrigger.Cancel,
                o => { },
                guardExpression: order => !order.WarehouseAssigned)
            .BeforeTransitionTo(OrderState.InFulfillment, new ReserveStockAction());

        // warehouse assignment happens AUTOMATICALLY (autofire) — no external trigger
        Configure(OrderState.InFulfillment)
            .AddTransitionTo<OrderTransitionOptions>(
                OrderState.ReadyToShip, OrderTrigger.AssignWarehouse,
                o => o.IsAutofire = true,
                transitionAction: new AssignWarehouseAction());

        // after reaching Shipped from ReadyToShip we release the stock
        Configure(OrderState.ReadyToShip)
            .AddTransitionTo<OrderTransitionOptions>(OrderState.Shipped, OrderTrigger.Ship, o => { })
            .AddTransitionTo<OrderTransitionOptions>(OrderState.Cancelled, OrderTrigger.Cancel, o => { })
            .AfterTransitionTo(OrderState.Shipped, new ReleaseStockAction());

        // ---- delivery and return ----
        Configure(OrderState.Shipped)
            .AddTransitionTo<OrderTransitionOptions>(OrderState.Delivered, OrderTrigger.Deliver, o => { })
            .AddTransitionTo<OrderTransitionOptions>(OrderState.Returned, OrderTrigger.RequestReturn, o => { });

        Configure(OrderState.Returned)
            .AddTransitionTo<OrderTransitionOptions>(
                OrderState.Refunded, OrderTrigger.Refund,
                o => o.UseTransactionScope = true,
                transitionAction: new RefundPaymentAction());

        // terminal states — no transitions out of them
        SetFiniteState(OrderState.Delivered, OrderState.Cancelled, OrderState.Refunded);
    }
}
```

### Usage

The example demonstrates firing **by trigger**, firing **by state**, and the **autofire** cascade:

> `FireByTriggerRequest` and `FireByStateRequest` are nested types of `StateMachine<...>`, so in code outside the machine class you must address them through the derived type: `OrderStateMachine.FireByTriggerRequest`.

```csharp
var order = new Order { Id = 42, Total = 1024.50m, State = OrderState.Placed };
var machine = new OrderStateMachine();

await machine.Fire(new OrderStateMachine.FireByTriggerRequest(OrderTrigger.StartPayment, order, CancellationToken.None));
await machine.Fire(new OrderStateMachine.FireByTriggerRequest(OrderTrigger.PaymentSucceeded, order, CancellationToken.None));
await machine.Fire(new OrderStateMachine.FireByTriggerRequest(OrderTrigger.Confirm, order, CancellationToken.None));

// autofire: after Confirm the order automatically moved to ReadyToShip
Console.WriteLine(order.State); // ReadyToShip

// alternatively — jump straight to the target state by TState:
await machine.Fire(new OrderStateMachine.FireByStateRequest(OrderState.Shipped, order, CancellationToken.None));

// transition graph in Mermaid notation for documentation purposes
Console.WriteLine(machine.GetTransitionsGraph());
```

A few things worth knowing before you go:

- **`SetFiniteState` cannot be combined with `Configure` for the same state** (otherwise `AmbiguousStateConfigurationException` is thrown) — terminal states have no outgoing transitions.
- **Guards do not throw** when the condition isn't met — the transition simply doesn't fire. This differs from a missing trigger, which throws `TriggerStateResolverNotFoundException`.
- **Autofire cascades**: a single external call (e.g. `Confirm`) can pull a whole chain of automatic transitions behind it.
- **`InitDefaultStateTransitionOptions<TOptions>()` shares one options instance across all transitions.** If you need per-transition flags (an `IsAutofire` on only one transition, or a transaction scope on only one), give each transition its own options instance via `AddTransitionTo<TOptions>(..., o => ...)` and **do not** initialize a shared default — otherwise the last configured flags leak to every transition, which breaks the autofire filter.

## API

The API of the `StateTransition` has the followed structure:

![api-structure](api-structure.png)

- [StateMachine](./src/StateTransition/StateMachine.cs) class is responsible for full control over the state machine itself and directly for state transition.
- [StateTransitionManager](./src/StateTransition/StateTransitionManager.cs) class is responsible for managing transitions for an each configured state.

More details about exposed API methods in these classes you can get from their xml-comments in code.
