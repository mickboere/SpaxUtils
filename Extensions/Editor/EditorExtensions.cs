using UnityEditor;

namespace SpaxUtils
{
	public static class EditorExtensions
	{
		public static SerializedProperty FindNeighbourProperty(this SerializedProperty property, string name, bool log = true)
		{
			SerializedProperty found = property.serializedObject.FindProperty(name);

			if (found == null)
			{
				string path = property.propertyPath.GetDirectoryPath('.') + "." + name;
				found = property.serializedObject.FindProperty(path);

				if (log && found == null)
				{
					SpaxDebug.Error($"Could not find property.", $"Property name: \"{name}\"\nFrom: \"{property.propertyPath}\", For: \"{path}\"");
				}
			}

			return found;
		}

		public static SerializedProperty FindProperty(this SerializedProperty property, string name)
		{
			SerializedProperty found = property.serializedObject.FindProperty(name);

			if (found == null)
			{
				found = property.FindNeighbourProperty(name, false);

				if (found == null)
				{
					// Child
					string path = property.propertyPath + "." + name;
					found = property.serializedObject.FindProperty(path);
				}

				if (found == null)
				{
					SpaxDebug.Error($"Could not find property.", $"Property name: \"{name}\"\nFrom: \"{property.propertyPath}\"");
				}
			}

			return found;
		}
	}
}
