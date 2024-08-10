using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can provide an agent with relation data.
	/// </summary>
	public interface IRelationData
	{
		IReadOnlyDictionary<string, float> GetRelations();
	}
}
