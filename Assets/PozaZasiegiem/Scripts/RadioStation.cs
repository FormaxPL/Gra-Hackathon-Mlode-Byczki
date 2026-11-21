using UnityEngine;

/// <summary>Cel gry: po zebraniu wszystkich części naprawiasz radiostację i łapiesz ZASIĘG.</summary>
public class RadioStation : MonoBehaviour, IInteractable
{
    public string winTitle = "SYGNAŁ NADANY";
    [TextArea(3, 8)]
    public string winText = "Szum... trzaski... a potem głos:\n\"Tu Agencja ECHO. Odebraliśmy twój sygnał. Nie ruszaj się. Idziemy po ciebie.\"";
    public string winTitleEn = "SIGNAL SENT";
    [TextArea(3, 8)]
    public string winTextEn = "Static... crackling... and then a voice:\n\"This is the ECHO Agency. We received your signal. Don't move. We're coming for you.\"";

    bool used;

    public string GetPrompt(PlayerController p)
    {
        if (used) return Loc.T("Radiostacja – połączono z Agencją ECHO", "Radio – connected to the ECHO Agency");
        int need = GameManager.Instance.partsRequired;
        return p.Inventory.parts >= need
            ? Loc.T("[E] Napraw radiostację i nadaj sygnał", "[E] Repair the radio and send a signal")
            : Loc.T("Radiostacja – brakuje części", "Radio – missing parts") + $" ({p.Inventory.parts}/{need})";
    }

    public void Interact(PlayerController p)
    {
        var gm = GameManager.Instance;
        if (p.Inventory.parts >= gm.partsRequired)
        {
            if (used) return;
            used = true;
            if (StoryDirector.Instance != null) StoryDirector.Instance.OnRadioRepaired(transform.position);
            else gm.Win(Loc.T(winTitle, winTitleEn), Loc.T(winText, winTextEn));
        }
        else gm.ShowMessage(Loc.T("Brak zasięgu. Potrzebujesz więcej części.", "No signal. You need more parts."), 2.5f);
    }
}
