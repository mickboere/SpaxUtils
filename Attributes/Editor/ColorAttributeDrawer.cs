using UnityEngine;
using UnityEditor;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(ColorAttribute))]
	public class ColorAttributeDrawer : DecoratorDrawer
	{
		ColorAttribute attr => (ColorAttribute)attribute;
		public override float GetHeight() { return 0; }

		public override void OnGUI(Rect position)
		{
			GUI.color = attr.color;
		}
	}
}