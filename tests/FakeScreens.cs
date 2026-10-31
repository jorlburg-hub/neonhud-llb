using UnityEngine;

public enum ScreenType
{
    NONE = 0, MENU = 2, MENU_MAIN = 3, MENU_OPTIONS = 8, PLAYERS = 15, GAME_INTRO = 19, GAME_HUD = 20, GAME_RESULTS = 21, GAME_PAUSE = 22,
    DIALOG = 26, DIALOG_CONFIRMATION = 27, OPTIONS = 31
}

namespace LLScreen
{
    public abstract class ScreenBase : MonoBehaviour { public ScreenType screenType; }
    public class ScreenAny : ScreenBase { }

    public class UIScreen : MonoBehaviour
    {
        public static ScreenBase[] currentScreens = new ScreenBase[4];

        public static void Show(int layer, ScreenType type)
        {
            var g = new GameObject("screen" + layer);
            var s = g.AddComponent<ScreenAny>();
            s.screenType = type;
            currentScreens[layer] = s;
        }
        public static void Clear(int layer) { currentScreens[layer] = null; }
        public static void ClearAll() { for (int i = 0; i < currentScreens.Length; i++) currentScreens[i] = null; }
    }
}
