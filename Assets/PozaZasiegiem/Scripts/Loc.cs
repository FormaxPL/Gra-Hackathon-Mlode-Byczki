/// <summary>
/// Prosta lokalizacja: Loc.T("tekst po polsku", "English text").
/// Język wybiera się w Ustawieniach (zapisywany w PlayerPrefs).
/// Żeby dodać kolejny język – dodaj wartość Lang i trzeci parametr w T().
/// </summary>
public static class Loc
{
    public const int PL = 0, EN = 1;
    public static int Lang = PL;
    public static readonly string[] Names = { "Polski", "English" };

    public static string T(string pl, string en) => Lang == EN && !string.IsNullOrEmpty(en) ? en : pl;
}
