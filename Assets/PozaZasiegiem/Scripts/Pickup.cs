using UnityEngine;

public enum PickupType { Part, Tracker, Weapon, Battery }

/// <summary>
/// Przedmiot do podniesienia: część radiostacji, wykrywacz, gaśnica, bateria.
/// Podnoszenie jest animowane przez HandInteraction (ręka sięga, przedmiot ląduje w dłoni).
/// </summary>
public class Pickup : MonoBehaviour, IInteractable
{
    public PickupType type = PickupType.Part;
    public string displayName = "Część";
    public string displayNameEn = "Part";
    public int weaponCharges = 3;
    public float batteryAmount = 40f;
    public float spinSpeed = 60f;

    bool picked;

    void Update()
    {
        if (!picked) transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    public string Name => Loc.T(displayName, displayNameEn);
    public string GetPrompt(PlayerController p) => picked ? null : Loc.T("[E] Podnieś: ", "[E] Pick up: ") + Name;

    public void Interact(PlayerController p)
    {
        if (picked) return;
        var hand = HandInteraction.Instance;
        if (hand != null)
        {
            if (!hand.Busy) hand.PickUp(this);
            return;
        }
        picked = true;
        Apply(p);
        Destroy(gameObject);
    }

    /// <summary>Wyłącza kolizje i poświatę – przedmiot jest już w ręce.</summary>
    public void PrepareForHand()
    {
        picked = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Efekt podniesienia (ekwipunek + komunikat).</summary>
    public void Apply(PlayerController p)
    {
        var inv = p.Inventory;
        var gm = GameManager.Instance;
        string msg = "";
        switch (type)
        {
            case PickupType.Part:
                inv.parts++;
                msg = Loc.T("Część radiostacji: ", "Radio part: ") + $"{Name} ({inv.parts}/{gm.partsRequired})";
                break;
            case PickupType.Tracker:
                inv.hasTracker = true;
                msg = Loc.T("Wykrywacz ruchu! Trzymaj [Tab] lub [Q], żeby go użyć. Zasięg: 10 m.", "Motion tracker! Hold [Tab] or [Q] to use it. Range: 10 m.");
                break;
            case PickupType.Weapon:
                inv.hasWeapon = true;
                inv.weaponCharges += weaponCharges;
                msg = Loc.T("Gaśnica! [LPM] odstrasza istotę. Ładunki: ", "Fire extinguisher! [LMB] scares the creature away. Charges: ") + inv.weaponCharges;
                break;
            case PickupType.Battery:
                if (p.Flashlight != null) p.Flashlight.AddBattery(batteryAmount);
                if (Camcorder.Instance != null) Camcorder.Instance.AddBattery(batteryAmount);
                msg = Loc.T("Baterie", "Batteries") + $" (+{batteryAmount:0}% " + Loc.T("latarka i kamera)", "flashlight and camera)");
                break;
        }
        if (gm != null) gm.ShowMessage(msg, 3.5f);
    }
}
