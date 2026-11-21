using UnityEngine;

/// <summary>
/// Notatka – podnoszona ręką i czytana z kartki (HandInteraction).
/// 'paper' = obiekt kartki (z MeshFilter), który jest przenoszony przed oczy.
/// </summary>
public class Note : MonoBehaviour, IInteractable
{
    [Tooltip("Klucz w NoteTexts – treść pobierana stamtąd (puste = pola poniżej / dopasowanie po starym tytule)")]
    public string id = "";
    public string title = "Notatka";
    [TextArea(5, 15)] public string text = "...";
    public string titleEn = "";
    [TextArea(5, 15)] public string textEn = "";

    public string Title => Loc.T(title, titleEn);
    public string Text => Loc.T(text, textEn);
    [Tooltip("Model kartki – jeśli pusty, notatka pokaże się jako zwykła nakładka")]
    public Transform paper;

    void Awake()
    {
        // aktualna treść z NoteTexts – działa też w scenach zbudowanych starszym generatorem
        if (!NoteTexts.TryGet(id, title, out var e)) return;
        title = e.title; text = e.text; titleEn = e.titleEn; textEn = e.textEn;
    }

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
