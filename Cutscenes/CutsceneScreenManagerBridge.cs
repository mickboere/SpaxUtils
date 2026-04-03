using SpaxUtils.UI;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Lives alongside the <see cref="UIScreenManager"/> in the scene.
	/// Listens to <see cref="CutsceneService"/> events and switches the screen context accordingly.
	/// </summary>
	public class CutsceneScreenManagerBridge : MonoBehaviour
	{
		private CutsceneService cutsceneService;
		private UIScreenManager screenManager;

		public void InjectDependencies(CutsceneService cutsceneService, UIScreenManager screenManager)
		{
			this.cutsceneService = cutsceneService;
			this.screenManager = screenManager;
		}

		private void Awake()
		{
			cutsceneService = GlobalDependencyManager.Instance.Get<CutsceneService>();
			cutsceneService.CutsceneStartedEvent += OnCutsceneStarted;
			cutsceneService.CutsceneEndedEvent += OnCutsceneEnded;
		}

		private void OnDestroy()
		{
			if (cutsceneService != null)
			{
				cutsceneService.CutsceneStartedEvent -= OnCutsceneStarted;
				cutsceneService.CutsceneEndedEvent -= OnCutsceneEnded;
			}
		}

		private void OnCutsceneStarted()
		{
			screenManager.SwitchContext(ContextIdentifiers.CUTSCENE);
		}

		private void OnCutsceneEnded()
		{
			screenManager.SwitchContext(string.Empty);
		}
	}
}
