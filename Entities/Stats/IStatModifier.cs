namespace SpaxUtils
{
	/// <summary>
	/// Interface for a data container that contains everything needed to modify a stat.
	/// </summary>
	public interface IStatModifier
	{
		/// <summary>
		/// The stat to add the modifier to.
		/// </summary>
		string Stat { get; }

		/// <summary>
		/// The identifier used to store the modifier.
		/// Often times this is the modder - whoever adds the modifier.
		/// </summary>
		object Identifier { get; }

		/// <summary>
		/// The modifier to add to the stat.
		/// </summary>
		IModifier<float> Modifier { get; }
	}
}
