using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ColorAttribute : PropertyAttribute
	{
		public float r;
		public float g;
		public float b;
		public float a;

		public ColorAttribute()
		{
			r = g = b = a = 1f;
		}

		public ColorAttribute(float r, float g, float b, float a)
		{
			this.r = r;
			this.g = g;
			this.b = b;
			this.a = a;
		}

		public Color color { get { return new Color(r, g, b, a); } }
	}
}