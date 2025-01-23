using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(40)] // Set materials before EntityAppearanceHandler requests them.
	public class EntityMaterialHandler : EntityComponentMono
	{
		[SerializeField, ReadOnly] private Color currentColor;
		[SerializeField] private bool hasDefaultColor;
		[SerializeField, Conditional(nameof(hasDefaultColor))] private Color defaultColor;

		private EntityAppearanceHandler appearanceHandler;
		private EntityResourceLibrary materialLibrary;

		public void InjectDependencies(EntityAppearanceHandler appearanceHandler, EntityResourceLibrary materialLibrary)
		{
			this.appearanceHandler = appearanceHandler;
			this.materialLibrary = materialLibrary;
		}

		protected void Start()
		{
			Load();
		}

		private void Load()
		{
			// Load materials.
			foreach (EntityAppearanceHandler.BodyPart bodyPart in appearanceHandler.BodyParts)
			{
				string matId = bodyPart.Skin.material.name;
				matId = matId.Substring(0, matId.Length - 11); // Remove " (Instanced)" from the end.
				if (Entity.RuntimeData.TryGetValue(matId, out int matIndex) &&
					materialLibrary.Materials.ContainsKey(matId) &&
					materialLibrary.Materials[matId].replacements.Count > matIndex)
				{
					bodyPart.Skin.material = materialLibrary.Materials[matId].replacements[matIndex];
				}
			}

			// Load color.
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
