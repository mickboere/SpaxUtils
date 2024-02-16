using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(ConstDropdownAttribute))]
	public class ConstDropdownDrawer : PropertyDrawer
	{
		private const string EMPTY = "";

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// Retrieve attribute data.
			ConstDropdownAttribute constDropdownAttribute = attribute as ConstDropdownAttribute;

			// Ensure there are collections we can collect const values from.
			if (constDropdownAttribute.Collections == null || constDropdownAttribute.Collections.Length == 0)
			{
				SpaxDebug.Error($"No collections are available for dropdown.", "Dropdown value will default to an empty string.");
				return;
			}

			// Get the dropdown's current value.
			string currentOption = property.stringValue;

			// Get all of the available consts we can find.
			List<string> fullAdressConsts = GetConstValues(constDropdownAttribute.Collections, true, constDropdownAttribute.Filter);
			List<string> noAdressConsts = GetConstValues(constDropdownAttribute.Collections, false, constDropdownAttribute.Filter);
			List<string> storedOptions = constDropdownAttribute.StoreAdress ? fullAdressConsts : noAdressConsts;

			bool includeEmpty = constDropdownAttribute.IncludeEmpty;
			bool error = false;

			// Check if we have any available consts to choose from.
			if (storedOptions.Count == 0)
			{
				// Check if there is an old value, if there is, don't overwrite it.
				if (!string.IsNullOrEmpty(currentOption))
				{
					if (!Application.isPlaying)
					{
						SpaxDebug.Warning("ConstDropdown has an old value but no available collections to pull from.");
					}
				}
				else
				{
					// Current value is either null or empty, lets default it to empty and add the EMPTY option to our dropdown.
					currentOption = EMPTY;
					includeEmpty = true;
					if (!constDropdownAttribute.IncludeEmpty)
					{
						// We defaulted to EMPTY but it's not supposed to be an option.
						error = true;
					}
				}
			}

			// Check if we should add EMPTY to the list of options.
			if (includeEmpty)
			{
				// Since the lists need to show "<EMPTY>" instead of "" as an option, we add the visuals here and
				// replace the later selected option with the actual EMPTY string.
				fullAdressConsts.Insert(0, constDropdownAttribute.EmptyOption);
				noAdressConsts.Insert(0, constDropdownAttribute.EmptyOption);
			}

			// Try and find the correct index.
			int optionIndex = currentOption == EMPTY ? 0 : storedOptions.IndexOf(currentOption);
			if (optionIndex == -1)
			{
				error = true;

				// Option could not be found, reset the option if allowed, else add it as an error.
				optionIndex = 0;
				if (true)//constDropdownAttribute.ForceOption)
				{
					if (!string.IsNullOrEmpty(currentOption))
					{
						// Couldn't find the current value in the list but there seems to be old data.
						int renamedValue = TryFindRenamedValue(currentOption, storedOptions, ConstDropdownAttribute.RENAME_ACCURACY);
						if (renamedValue >= 0)
						{
							// Found a valid rename.
							optionIndex = renamedValue;
							error = false;
						}

						SpaxDebug.Log($"Invalid value; '{currentOption}' selected in dropdown.",
							$"Converted to '{(string.IsNullOrEmpty(storedOptions[optionIndex]) ? constDropdownAttribute.EmptyOption : storedOptions[optionIndex])}'.",
							color: Color.yellow);
					}
				}
				else
				{
					fullAdressConsts.Insert(0, currentOption);
					noAdressConsts.Insert(0, currentOption);
				}
			}

			// Finally draw the dropdown.

			Color previousColor = GUI.color;
			if (error)
			{
				GUI.color = Color.red;
			}

			EditorGUI.BeginProperty(position, label, property);
			string value = storedOptions[EditorGUI.Popup(position, label.text, optionIndex,
				constDropdownAttribute.ShowAdress ? fullAdressConsts.ToArray() : noAdressConsts.ToArray())];
			if (value == constDropdownAttribute.EmptyOption)
			{
				value = EMPTY;
			}
			property.stringValue = value;
			EditorGUI.EndProperty();

			// Show a tooltip with the full stored property value.
			EditorGUI.LabelField(position, new GUIContent("", property.stringValue));

			GUI.color = previousColor;
		}

		private int TryFindRenamedValue(string oldValue, List<string> newValues, int renameAccuracy)
		{
			// First check if capitalization changed.
			for (int i = 0; i < newValues.Count; i++)
			{
				if (oldValue.ToUpper() == newValues[i].ToUpper())
				{
					return i;
				}
			}

			string oldValueSplit = oldValue.Split(ConstDropdownAttribute.ADRESS_SEPARATOR).Last();
			string[] newValuesSplit = newValues.Select((v) => v.Split(ConstDropdownAttribute.ADRESS_SEPARATOR).Last()).ToArray();

			// Secondly check if the filename or adress changed by splitting all the new value paths and only looking at the value part.
			for (int i = 0; i < newValuesSplit.Length; i++)
			{
				if (oldValueSplit == newValuesSplit[i])
				{
					return i;
				}
			}

			// As a last resort, use Distance to determine if the value itself was renamed.
			int lowestDistance = int.MaxValue;
			int lowestDistanceIndex = 0;
			for (int i = 0; i < newValuesSplit.Length; i++)
			{
				int distance = oldValueSplit.Distance(newValuesSplit[i]);
				if (distance < lowestDistance)
				{
					lowestDistance = distance;
					lowestDistanceIndex = i;
				}
			}
			if (lowestDistance <= renameAccuracy)
			{
				return lowestDistanceIndex;
			}

			// Could not find a rename.
			return -1;
		}

		private List<string> GetConstValues(IEnumerable<Type> fromTypes, bool includeAdress, string filter)
		{
			List<string> values = new List<string>();
			foreach (Type type in fromTypes)
			{
				if (type.IsInterface)
				{
					// Collection is an interface, get all its implementations and add their values.
					List<Type> implementations = type.GetAllAssignableTypes((t) => !t.IsInterface);
					values.AddRange(GetConstValues(implementations, includeAdress, filter));
				}
				else
				{
					values.AddRange(type.GetAllPublicConstStrings(includeAdress, ConstDropdownAttribute.ADRESS_SEPARATOR));
				}
			}

			// Filter
			if (!string.IsNullOrWhiteSpace(filter))
			{
				for (int i = 0; i < values.Count; i++)
				{
					if (!values[i].Contains(filter))
					{
						values.RemoveAt(i);
						i--;
					}
				}
			}
			return values;
		}
	}
}