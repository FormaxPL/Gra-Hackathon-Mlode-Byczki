/// <summary>Wszystko, z czym gracz może wejść w interakcję klawiszem E.</summary>
public interface IInteractable
{
    string GetPrompt(PlayerController player);
    void Interact(PlayerController player);
}
