using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Attribute for creating a dropdown of CONST values in the editor.
	/// </summary>
	public class ConstDropdownAttribute : PropertyAttribute
	{
		public const char ADRESS_SEPARATOR = '/';
		public const string EMPTY_OPTION = "<NULL>";

		/// <summary>
		/// The max distance to allow a rename.
		/// </summary>
		public const int RENAME_ACCURACY = 1;

		/// <summary>
		/// All of the class types we're allowed to pull const values from.
		/// </summary>
		public readonly Type[] Collections;

		/// <summary>
		/// Will include a single entry named <EMPTY> who'se value equals an "" (empty string)
		/// </summary>
		public readonly bool IncludeEmpty;

		/// <summary>
		/// Will show the entire adress of the const value in the editor like so: Assembly/Namespace/Class/X
		/// </summary>
		public readonly bool ShowAdress;

		/// <summary>
		/// Will store the entire adress of the const value in the variable.
		/// If your constants are actually copying real values, you want this disabled.
		/// </summary>
		public readonly bool StoreAdress;

		/// <summary>
		/// When true: If the current field value does not match one of the available options,
		/// drawer will attempt to find the closest match, if there is no match the first available option will be selected.
		/// </summary>
		public readonly bool ForceOption;

		/// <summary>
		/// The option that will be shown when <see cref="IncludeEmpty"/> is set to TRUE.
		/// </summary>
		public readonly string EmptyOption;

		public readonly string Filter;

		public ConstDropdownAttribute(bool includeEmpty, bool showAdress, bool storeAdress, bool forceOption, string emptyOption, string filter, params Type[] collections)
		{
			Collections = collections;
			IncludeEmpty = includeEmpty;
			ShowAdress = showAdress;
			StoreAdress = storeAdress;
			ForceOption = forceOption;
			EmptyOption = emptyOption;
			Filter = filter;
		}

		public ConstDropdownAttribute(
			Type collection,
			bool includeEmpty = false,
			bool showAdress = false,
			bool storeAdress = false,
			bool forceOption = false,
			string emptyOption = EMPTY_OPTION,
			string filter = null)
		{
			Collections = new Type[] { collection };
			IncludeEmpty = includeEmpty;
			ShowAdress = showAdress;
			StoreAdress = storeAdress;
			ForceOption = forceOption;
			EmptyOption = emptyOption;
			Filter = filter;
		}
	}
}
