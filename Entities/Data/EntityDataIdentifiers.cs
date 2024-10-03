namespace SpaxUtils
{
	/// <summary>
	/// Data identification labels unique to <see cref="IEntity"/>s.
	/// </summary>
	public class EntityDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string ENTITY = "ENTITY/";

		public const string NAME = ENTITY + "Name";
		public const string ALIVE = ENTITY + "Alive";
		public const string AGE = ENTITY + "Age";
		public const string POSITION = ENTITY + "Pos";
		public const string ROTATION = ENTITY + "Rot";
		public const string SCALE = ENTITY + "Scale";
		public const string HEIGHT = ENTITY + "Height";
	}
}
