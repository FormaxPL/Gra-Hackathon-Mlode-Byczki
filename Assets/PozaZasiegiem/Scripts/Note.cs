using UnityEngine;

/// <summary>
/// Notatka – podnoszona ręką i czytana z kartki (HandInteraction).
/// 'paper' = obiekt kartki (z MeshFilter), który jest przenoszony przed oczy.
/// </summary>
public class Note : MonoBehaviour, IInteractable
{
    public string title = "Notatka";
    [TextArea(5, 15)] public string text = "...";
    public string titleEn = "";
    [TextArea(5, 15)] public string textEn = "";

    public string Title => Loc.T(title, titleEn);
    public string Text => Loc.T(text, textEn);
    [Tooltip("Model kartki – jeśli pusty, notatka pokaże się jako zwykła nakładka")]
    public Transform paper;

    public string GetPrompt(PlayerController p) => Loc.T("[E] Podnieś i czytaj: ", "[E] Pick up and read: ") + Title;

    public void Interact(PlayerController p)
    {
        var hand = HandInteraction.Instance;
        if (paper != null && hand != null)
        {
            if (!hand.Busy) hand.ReadNote(this);
            return;
        }
        GameManager.Instance.OpenNote(Title + "\n\n" + Text);
    }
}
