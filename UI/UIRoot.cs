using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaxUtils
{
	public class UIRoot : MonoBehaviour, IDependency
	{
		[field: SerializeField] public Canvas Canvas { get; private set; }
		[field: SerializeField] public EventSystem EventSystem { get; private set; }
		[field: SerializeField] public Camera Camera { get; private set; }
	}
}
