using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class GameStateIdentifiers : IStateIdentifiers
	{
		public const string LOADING = "Loading";

		public const string MAIN_MENU = "MainMenu";
		public const string CHARACTER_CREATION = "CharacterCreation";
		public const string LOBBY = "Lobby";
		public const string GAME = "Game";
	}
}
