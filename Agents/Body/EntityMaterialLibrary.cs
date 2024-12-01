using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(EntityMaterialLibrary), menuName = "ScriptableObjects/" + nameof(EntityMaterialLibrary))]
	public class EntityMaterialLibrary : ScriptableObject
	{
		[Serializable]
		public class MaterialList
		{
			public Material source;
			public List<Material> replacements;
		}

		IReadOnlyDictionary<string, MaterialList> Dictionary
		{
			get
			{
				if (_dictionary == null)
				{
					_dictionary = materials.ToDictionary((m) => m.source.name, (m) => m);
				}
				return _dictionary;
			}
		}
		private Dictionary<string, MaterialList> _dictionary;

		[SerializeField] private List<MaterialList> materials;
	}
}
