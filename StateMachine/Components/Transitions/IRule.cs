namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Rule interface containing a "Valid" bool and a "Validity" float amount.
	/// </summary>
	public interface IRule
	{
		/// <summary>
		/// Does this rule meet the requirements?
		/// </summary>
		bool Valid { get; }

		/// <summary>
		/// How valid is this transition to be used?
		/// Can be useful to determine the correct transition when multiple transitions are valid.
		/// </summary>
		float Validity { get; }
	}
}