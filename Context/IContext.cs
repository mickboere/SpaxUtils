namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IIdentifiable"/> object that can be either muted or solo'd.
	/// </summary>
	public interface IContext : IIdentifiable
	{
		bool Mute { get; set; }
		bool Solo { get; set; }
	}
}
