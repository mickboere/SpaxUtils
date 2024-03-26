namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Base <see cref="IRule"/> node implementation.
	/// </summary>
	[NodeTint("#AB6F22"), NodeWidth(130)]
	public abstract class RuleNodeBase : StateMachineNodeBase, IRule
	{
		public abstract bool Valid { get; }
		public virtual float Validity => 1f;
	}
}