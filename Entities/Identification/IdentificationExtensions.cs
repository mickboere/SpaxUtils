namespace SpaxUtils
{
	public static class IdentificationExtensions
	{
		/// <summary>
		/// Full tag containing ID, Name and Labels.
		/// </summary>
		public static string TagFull(this IIdentification id) => $"{id.ID}|{id.Name}|({string.Join(", ", id.Labels)})";

		/// <summary>
		/// Tag containing Name and Labels.
		/// </summary>
		public static string Tag(this IIdentification id) => $"{id.Name}|({string.Join(", ", id.Labels)})";

		/// <summary>
		/// Tag containing only Labels.
		/// </summary>
		public static string TagLabels(this IIdentification id) => $"({string.Join(", ", id.Labels)})";
	}
}
