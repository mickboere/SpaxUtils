using UnityEditor;

namespace SpaxUtils
{
	public static class EditorExtensions
	{
		public static SerializedProperty FindNeighbourProperty(this SerializedProperty property, string name)
		{
			SerializedProperty found = property.serializedObject.FindProperty(name);

			if (found == null)
			{
				string path = property.propertyPath.SecondToLast(".") + "." + name;
				found = property.serializedObject.FindProperty(path);

				if (found == null)
				{
					SpaxDebug.Error($"Could not find property.", $"Property name: \"{name}\"\nFrom: \"{property.propertyPath}\", For: \"{path}\"");
				}
			}

			return found;
		}
	}
}
