using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="Vector8ConfigurationAssetBase"/> implementation providing a simple dropdown for the desired injector key.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(Vector8ConfigurationAsset), menuName = "ScriptableObjects/" + nameof(Vector8ConfigurationAsset))]
	public class Vector8ConfigurationAsset : Vector8ConfigurationAssetBase
	{
		protected override string Key => key;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string key;
	}
}
