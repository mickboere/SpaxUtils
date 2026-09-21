using TMPro;
using UnityEngine;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Names the player who owns a full-screen menu in split-screen; hidden otherwise.
	/// </summary>
	[RequireComponent(typeof(TMP_Text))]
	public class MenuOwnerLabelUI : MonoBehaviour
	{
		private TMP_Text text;
		private SplitScreenService splitScreenService;
		private PlayerInputWrapper playerInputWrapper;
		private PlayerInputService playerInputService;

		public void InjectDependencies(SplitScreenService splitScreenService, PlayerInputWrapper playerInputWrapper,
			PlayerInputService playerInputService)
		{
			this.splitScreenService = splitScreenService;
			this.playerInputWrapper = playerInputWrapper;
			this.playerInputService = playerInputService;
		}

		protected void Awake()
		{
			text = GetComponent<TMP_Text>();
		}

		protected void OnEnable()
		{
			if (splitScreenService != null)
			{
				splitScreenService.LayoutChangedEvent += Refresh;
			}
			Refresh();
		}

		protected void OnDisable()
		{
			if (splitScreenService != null)
			{
				splitScreenService.LayoutChangedEvent -= Refresh;
			}
		}

		private void Refresh()
		{
			int playerIndex = playerInputService != null ? playerInputService.GetPlayerIndex(playerInputWrapper) : -1;
			bool show = playerIndex >= 0 && splitScreenService != null && splitScreenService.FullscreenOwner == playerIndex;

			text.enabled = show;
			if (show)
			{
				text.text = PlayerAgentService.GetDefaultName(playerIndex);
			}
		}
	}
}
