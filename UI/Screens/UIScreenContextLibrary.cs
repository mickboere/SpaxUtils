using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.UI
{
	[CreateAssetMenu(fileName = "UIScreenContextLibrary", menuName = "ScriptableObjects/UIScreenContextLibrary")]
	public class UIScreenContextLibrary : ScriptableObject, IService
	{
		public IReadOnlyDictionary<string, UIScreen> Screens
		{
			get
			{
				if (screens == null)
				{
					screens = new Dictionary<string, UIScreen>();
					foreach (UIScreenContextConfig config in configs)
					{
						screens.Add(config.context, config.screen);
					}
				}
				return screens;
			}
		}
		private Dictionary<string, UIScreen> screens;

		[SerializeField] private List<UIScreenContextConfig> configs;
	}
}
