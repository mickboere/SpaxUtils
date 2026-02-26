namespace SpaxUtils
{
	/// <summary>
	/// Data identification labels unique to <see cref="IEntity"/>s.
	/// </summary>
	public class EntityDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string ENTITY = "ENTITY/";

		public const string ID = ENTITY + "ID";
		public const string NAME = ENTITY + "Name";
		public const string SEED = ENTITY + "Seed";
		public const string ALIVE = ENTITY + "Alive";
		public const string AGE = ENTITY + "Age";
		public const string SCENE = ENTITY + "Scene";
		public const string CYCLE = ENTITY + "Cycle";
		public const string POSITION = ENTITY + "Pos";
		public const string ROTATION = ENTITY + "Rot";
		public const string SCALE = ENTITY + "Scale";
		public const string OFF = ENTITY + "Off";

		/// <summary>
		/// Float entity height in meters.
		/// </summary>
		public const string HEIGHT = ENTITY + "Height";

		/// <summary>
		/// String hex color code #"color".
		/// </summary>
		public const string COLOR = ENTITY + "Color";

		/// <summary>
		/// Float (0-1) that scales the entity's stats.
		/// </summary>
		public const string SCALING = ENTITY + "Scaling";

		/// <summary>
		/// Float (0-1) that scales the entity's behaviour to be more or less difficult.
		/// </summary>
		public const string DIFFICULTY = ENTITY + "Difficulty";

		/// <summary>
		/// Int defining the entity's <see cref="PriorityLevel"/>.
		/// </summary>
		public const string PRIORITY = ENTITY + "Priority";

		/// <summary>
		/// Bool defining whether the entity should run in debug mode.
		/// </summary>
		public const string DEBUG = ENTITY + "Debug";
	}
}
