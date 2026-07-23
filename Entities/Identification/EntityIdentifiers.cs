namespace SpaxUtils
{
	public class EntityIdentifiers : IIdentificationIdentifiers
	{
		public const string PLAYER = "PLAYER";

		private const string CAIRNS = "CAIRNS/";
		public const string CAIRN_SOLEMN = CAIRNS + "NEUTRAL"; // Value kept for existing prefab linkage; align to "SOLEMN" in-editor during the visuals pass.
	}
}
