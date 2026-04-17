namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Rule interface containing a "Valid" bool and a "Validity" float amount.
	/// </summary>
	public interface IRule : IStateListener
	{
		/// <summary>
		/// Does this rule meet the requirements?
		/// </summary>
		bool Valid { get; }

		/// <summary>
		/// How valid exactly is this transition? (range depends on context)
		/// Used to determine the correct transition when multiple transitions are valid.
		/// </summary>
		float Validity { get; }

		/// <summary>
		/// Whether this rule needs external callback (true) or if this rule is part of a component node that already receives callbacks (false).
		/// </summary>
		bool IsPureRule { get; }
	}
}
