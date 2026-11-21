using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Trailer z edytora: „Poza Zasiegiem → Odtworz trailer”.
/// Z wiersza poleceń (otwiera edytor i od razu puszcza trailer):
///   Unity.exe -projectPath "ścieżka do projektu" -executeMethod TrailerMenu.Play
/// W trakcie Play działa też konsola w grze: klawisz ` i komenda „trailer”.
/// </summary>
public static class TrailerMenu
{
    [MenuItem("Poza Zasiegiem/Odtworz trailer", false, 30)]
    public static void Play()
    {
        if (EditorApplication.isPlaying)
        {
            TrailerDirector.Request();
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // start z menu głównego, jak w normalnej grze; trailer sam wczyta mapę
        var menu = "Assets/PozaZasiegiem/Scenes/" + MapPicker.MenuScene + ".unity";
        var first = System.IO.File.Exists(menu) ? menu : "Assets/PozaZasiegiem/Scenes/" + TrailerDirector.Map + ".unity";
        EditorSceneManager.OpenScene(first);
        SessionState.SetBool(DevConsole.EditorTrailerFlag, true);
        EditorApplication.EnterPlaymode();
    }
}
