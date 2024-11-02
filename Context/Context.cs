namespace SpaxUtils
{
	/// <summary>
	/// Standard <see cref="IContext"/> implementation.
	/// </summary>
	public class Context : IContext
	{
		public string ID { get; }
		public bool Mute { get; set; }
		public bool Solo { get; set; }

		public Context(string id)
		{
			ID = id;
		}
	}
}
