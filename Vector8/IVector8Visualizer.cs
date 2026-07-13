using UnityEngine;

namespace SpaxUtils
{
	public interface IVector8Visualizer
	{
		public void Visualize(Vector8 vector8, Color[] colors = null);
	}
}
