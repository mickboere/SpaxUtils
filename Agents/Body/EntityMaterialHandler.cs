using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class EntityMaterialHandler : EntityComponentMono
	{
		[SerializeField, ReadOnly] private Color currentColor;
		[SerializeField] private bool hasDefaultColor;
		[SerializeField, Conditional(nameof(hasDefaultColor))] private Color defaultColor;
		
		private EntityAppearanceHandler appearanceHandler;

		public void InjectDependencies(EntityAppearanceHandler appearanceHandler)
		{
			this.appearanceHandler = appearanceHandler;
		}

		protected void Start()
		{
			if (Entity.RuntimeData.TryGetValue(EntityDataIdentifiers.COLOR, out string colString))
			{
				if (ColorUtility.TryParseHtmlString($"#{colString}", out Color color))
				{
					SwitchColor(color);
				}
				else
				{
					SpaxDebug.Error($"Color data was found but could not be parsed:", colString);
				}
			}
			else if (hasDefaultColor)
			{
				SwitchColor(defaultColor);
			}
		}

		private void SwitchColor(Color color)
		{
			currentColor = color;
			foreach (EntityAppearanceHandler.BodyPart bodyPart in appearanceHandler.BodyParts)
			{
				bodyPart.Skin.material.color = color;
			}
		}
	}
}
