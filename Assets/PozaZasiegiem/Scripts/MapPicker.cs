/// <summary>
/// Gotowe mapy (zbudowane generatorem, każda z wypalonym światłem) i scena menu.
/// „Rozpocznij grę” w menu losuje jedną z map – przy każdym uruchomieniu inny układ pomieszczeń.
/// </summary>
public static class MapPicker
{
    public const string MenuScene = "MainMenu";
    public static readonly string[] Maps = { "TestLevel_A", "TestLevel_B", "TestLevel_C" };

    public static string Pick(System.Random r) => Maps[r.Next(Maps.Length)];

    public static bool IsMenuScene(string sceneName) => sceneName == MenuScene;
}
