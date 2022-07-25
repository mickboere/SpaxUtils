namespace SpaxUtils
{
	/// <summary>
	/// Basic implementation of an <see cref="IStatModifier/>.
	/// </summary>
	public class StatModifier : IStatModifier
	{
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public string Stat { get; private set; }

		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public object Identifier { get; private set; }

		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public IModifier<float> Modifier { get; private set; }

		public StatModifier(string stat, object identifier, IModifier<float> modifier)
		{
			Stat = stat;
			Identifier = identifier;
			Modifier = modifier;
		}
	}
}
