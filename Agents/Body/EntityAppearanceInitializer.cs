using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[DefaultExecutionOrder(40)] // Set meshes and materials before EntityAppearanceHandler requests them.
	public class EntityAppearanceInitializer : EntityComponentMono
	{
		[SerializeField, ReadOnly] private Color currentColor;
		[SerializeField] private bool hasDefaultColor;
		[SerializeField, Conditional(nameof(hasDefaultColor))] private Color defaultColor;

		private EntityAppearanceHandler appearanceHandler;
		private EntityResourceLibrary entityResourceLibrary;
		private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

		public void InjectDependencies(EntityAppearanceHandler appearanceHandler, EntityResourceLibrary entityResourceLibrary)
		{
			this.appearanceHandler = appearanceHandler;
			this.entityResourceLibrary = entityResourceLibrary;
		}

		protected void Start()
		{
			Load();
		}

		private void Load()
		{
			foreach (EntityAppearanceHandler.BodyPart bodyPart in appearanceHandler.BodyParts)
			{
				LoadMesh(bodyPart);
				LoadMaterial(bodyPart);
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

		private void LoadMesh(EntityAppearanceHandler.BodyPart bodyPart)
		{
			if (Entity.RuntimeData.TryGetValue(bodyPart.Location, out string name) && // Seek mesh replacement by name.
				entityResourceLibrary.TryGetBodyPart(bodyPart.Location, name, out SkinnedMeshRenderer replacement))
			{
				// Replace mesh.
				SkinnedMeshRenderer instance = Instantiate(replacement, Entity.Transform);
				bodyPart.Override(instance);
			}
		}

		private void LoadMaterial(EntityAppearanceHandler.BodyPart bodyPart)
		{
			string matId = bodyPart.Skin.material.name;
			matId = matId.Substring(0, matId.Length - 11); // Removes " (Instanced)" from the end.

			if (materialCache.ContainsKey(matId)) // Already cached the desired material.
			{
				bodyPart.Skin.material = materialCache[matId];
			}
			else if (Entity.RuntimeData.TryGetValue(matId, out int matIndex) && // Seek material by index.
				entityResourceLibrary.Materials.ContainsKey(matId) &&
				entityResourceLibrary.Materials[matId].replacements.Count > matIndex)
			{
				materialCache[matId] = entityResourceLibrary.Materials[matId].replacements[matIndex];
				bodyPart.Skin.material = materialCache[matId];
			}
			else if (Entity.RuntimeData.TryGetValue(matId, out string matName) && // Seek material by name.
				entityResourceLibrary.TryGetMaterial(matId, matName, out Material material))
			{
				materialCache[matId] = material;
				bodyPart.Skin.material = material;
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
