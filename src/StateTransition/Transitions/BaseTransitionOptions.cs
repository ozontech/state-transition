namespace StateTransition;

public class BaseTransitionOptions<TArgs> : BaseTransitionOptions where TArgs : new()
{
	public TArgs TransitionArgs { get; set; } = new();
}

public class BaseTransitionOptions
{
	public virtual bool IsAutofire { get; set; }

	internal static readonly BaseTransitionOptions Default = new();
}