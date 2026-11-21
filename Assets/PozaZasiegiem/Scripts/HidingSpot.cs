using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Szafka / szafka pod blatem / szafa chłodnicza. Pivot obiektu = podłoga w środku kryjówki,
/// oś Z (forward) = kierunek wyjścia.
/// Jeśli Stalker widział, jak się chowasz – wyciągnie cię.
/// Jeśli tylko obwąchuje kryjówkę – wstrzymaj oddech (Spacja), jak w Alien Isolation.
/// </summary>
public class HidingSpot : MonoBehaviour, IInteractable
{
    public static readonly List<HidingSpot> All = new List<HidingSpot>();

    public string displayName = "Schowaj się";
    public string displayNameEn = "Hide";
    public float exitDistance = 1.1f;
    [Tooltip("Wysokość kamery w kryjówce (szafka pod blatem = nisko)")]
    public float cameraHeight = 1.6f;

    public Vector3 InsidePosition => transform.position;
    public Vector3 ExitPosition => transform.position + transform.forward * exitDistance;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public string GetPrompt(PlayerController p) => "[E] " + Loc.T(displayName, displayNameEn);
    public void Interact(PlayerController p) => p.EnterHiding(this);

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, ExitPosition + Vector3.up * 0.1f);
        Gizmos.DrawWireSphere(ExitPosition + Vector3.up * 0.1f, 0.2f);
    }
}
