using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Binds 8 UI <see cref="Slider"/>s (octad order: N, NE, E, SE, S, SW, W, NW) to a <see cref="Vector8"/>, so an octad
	/// can be dialled at runtime. Read <see cref="Value"/> to poll the sliders; call <see cref="SetValue"/> to seed them.
	/// </summary>
	public class Vector8Sliders : MonoBehaviour
	{
		[Tooltip("Sliders in octad order: N, NE, E, SE, S, SW, W, NW. A missing entry reads as 0.")]
		[SerializeField] private Slider[] sliders = new Slider[8];

		/// <summary>The octad currently dialled into the sliders.</summary>
		public Vector8 Value
		{
			get
			{
				Vector8 v = Vector8.Zero;
				if (sliders != null)
				{
					for (int i = 0; i < 8 && i < sliders.Length; i++)
					{
						if (sliders[i] != null)
						{
							v[i] = sliders[i].value;
						}
					}
				}
				return v;
			}
		}

		/// <summary>Pushes <paramref name="v"/> into the sliders without firing their change callbacks (for seeding).</summary>
		public void SetValue(Vector8 v)
		{
			if (sliders == null)
			{
				return;
			}
			for (int i = 0; i < 8 && i < sliders.Length; i++)
			{
				if (sliders[i] != null)
				{
					sliders[i].SetValueWithoutNotify(v[i]);
				}
			}
		}

#if UNITY_EDITOR
		protected void OnValidate()
		{
			if (sliders == null || sliders.Length != 8)
			{
				System.Array.Resize(ref sliders, 8);
			}
		}
#endif
	}
}
