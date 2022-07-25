namespace SpaxUtils
{
	/// <summary>
	/// Interface for behaviours that can be started or stopped.
	/// </summary>
	public interface IBehaviour
	{
		/// <summary>
		/// Returns whether this behaviour is currently running or not.
		/// </summary>
		bool Running { get; }

		/// <summary>
		/// Start this behaviour.
		/// </summary>
		void Start();

		/// <summary>
		/// Stop this behaviour.
		/// </summary>
		void Stop();
	}
}
