namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Rule interface containing a "Valid" bool and a "Validity" float amount.
	/// </summary>
	public interface IRule : IStateComponent
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
	}
}
