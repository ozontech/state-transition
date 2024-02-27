namespace StateTransition;

public abstract class TransitionMiddlewareHandler
{
	internal TransitionMiddlewareHandler Next { get; private set; }

	public virtual async Task Handle<TRequest>(TRequest request, BaseTransitionOptions transitionOptions)
	{
		if (Next is not null)
		{
			await Next.Handle(request, transitionOptions);
		}
	}

	internal void SetNext(TransitionMiddlewareHandler next)
	{
		Next = next;
	}
}

public partial class StateMachine<TState, TTrigger, TEntity>
{
	internal class RunMiddlewareHandler : TransitionMiddlewareHandler
	{
		private readonly Func<BaseFireRequest, Task> _fire;

		internal RunMiddlewareHandler(Func<BaseFireRequest, Task> fire)
		{
			_fire = fire;
		}

		public override Task Handle<TRequest>(TRequest request, BaseTransitionOptions transitionOptions)
		{
			return _fire(request as BaseFireRequest);
		}
	}
}