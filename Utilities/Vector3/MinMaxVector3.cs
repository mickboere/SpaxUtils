using UnityEngine;

namespace SpaxUtils
{
	[System.Serializable]
	public class MinMaxVector3
	{
		public Vector2 X => minMaxX;
		public float MinX => Mathf.Min(X.x, X.y);
		public float MaxX => Mathf.Max(X.x, X.y);
		public Vector2 Y => minMaxY;
		public float MinY => Mathf.Min(Y.x, Y.y);
		public float MaxY => Mathf.Max(Y.x, Y.y);
		public Vector2 Z => minMaxZ;
		public float MinZ => Mathf.Min(Z.x, Z.y);
		public float MaxZ => Mathf.Max(Z.x, Z.y);

		[SerializeField] private Vector2 minMaxX;
		[SerializeField] private Vector2 minMaxY;
		[SerializeField] private Vector2 minMaxZ;

		public MinMaxVector3(Vector2 x, Vector2 y, Vector2 z)
		{
			minMaxX = x;
			minMaxY = y;
			minMaxZ = z;
		}
	}
}