using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EntityResourceLibrary), menuName = "ScriptableObjects/" + nameof(EntityResourceLibrary))]
	public class EntityResourceLibrary : ScriptableObject, IService
	{
		[Serializable]
		public class MaterialList
		{
			public Material source;
			public List<Material> replacements;

			public Material Get(string name)
			{
				for (int i = 0; i < replacements.Count; i++)
				{
					if (replacements[i].name == name)
					{
						return replacements[i];
					}
				}
				return null;
			}
		}

		[Serializable]
		public class BodyPartList
		{
			[ConstDropdown(typeof(IBodyLocations))] public string location;
			public List<SkinnedMeshRenderer> replacements;

			public SkinnedMeshRenderer Get(string name)
			{
				for (int i = 0; i < replacements.Count; i++)
				{
					if (replacements[i].sharedMesh.name == name)
					{
						return replacements[i];
					}
				}
				return null;
			}
		}

		public IReadOnlyDictionary<string, MaterialList> Materials
		{
			get
			{
				// Count check as well as null: a read before deserialization completes would otherwise
				// latch an empty dictionary, which is not null and so never rebuilds.
				if (_materials == null || _materials.Count == 0)
				{
					_materials = materials.ToDictionary((m) => m.source.name, (m) => m);
				}
				return _materials;
			}
		}
		private Dictionary<string, MaterialList> _materials;

		public IReadOnlyDictionary<string, BodyPartList> BodyParts
		{
			get
			{
				if (_bodyParts == null || _bodyParts.Count == 0)
				{
					_bodyParts = bodyParts.ToDictionary((m) => m.location, (m) => m);
				}
				return _bodyParts;
			}
		}
		private Dictionary<string, BodyPartList> _bodyParts;

		[SerializeField] private List<MaterialList> materials;
		[SerializeField] private List<BodyPartList> bodyParts;

		/// <summary>Drops the caches so the next read rebuilds them; runs after deserialization.</summary>
		protected void OnEnable()
		{
			_materials = null;
			_bodyParts = null;
		}

#if UNITY_EDITOR
		/// <summary>Rebuilds on inspector edits, which would otherwise not apply until the next domain reload.</summary>
		protected void OnValidate()
		{
			_materials = null;
			_bodyParts = null;
		}
#endif

		/// <summary>
		/// Retrieves an entity material by name.
		/// </summary>
		/// <param name="original">The name of the material you are seeking to replace.</param>
		/// <param name="name">The name of the material you are seeking to replace the original with.</param>
		public Material GetMaterial(string original, string name)
		{
			if (Materials.ContainsKey(original))
			{
				return Materials[original].Get(name);
			}
			return null;
		}

		/// <summary>
		/// <see cref="GetMaterial(string, string)"/>
		/// </summary>
		public bool TryGetMaterial(string original, string name, out Material material)
		{
			material = GetMaterial(original, name);
			return material != null;
		}

		public SkinnedMeshRenderer GetBodyPart(string location, string name)
		{
			if (BodyParts.ContainsKey(location))
			{
				return BodyParts[location].Get(name);
			}
			return null;
		}

		public bool TryGetBodyPart(string location, string name, out SkinnedMeshRenderer bodyPart)
		{
			bodyPart = GetBodyPart(location, name);
			return bodyPart != null;
		}
	}
}
