using UnityEngine;

/// <summary>
/// "Broń" – gaśnica. Nie zabija, tylko odstrasza Stalkera (jak miotacz ognia w Alien Isolation):
/// trafiony ucieka do najbliższej wentylacji i przez chwilę daje spokój.
/// Robi hałas, więc nie używaj jej bez potrzeby.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class Extinguisher : MonoBehaviour
{
    public float range = 7f;
    public float coneAngle = 35f;
    public float noiseRadius = 10f;

    PlayerController pc;
    FirstPersonArms arms;
    AudioSource src;
    AudioClip hiss;

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        arms = GetComponentInChildren<FirstPersonArms>(true);
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        hiss = ProceduralAudio.Noise(0.9f, 0.35f, "Hiss");
    }

    void Update()
    {
        if (GameManager.Blocked || pc.IsHidden) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        var inv = pc.Inventory;
        if (!inv.hasWeapon || !GameInput.FireDown) return;

        var gm = GameManager.Instance;
        if (inv.weaponCharges <= 0) { gm.ShowMessage(Loc.T("Gaśnica jest pusta.", "The extinguisher is empty."), 1.5f); return; }

        inv.weaponCharges--;
        src.PlayOneShot(hiss, 0.8f);
        if (arms != null) arms.PlaySpray();
        gm.Flash(new Color(1f, 1f, 1f, 0.35f), 0.5f);
        NoiseSystem.Emit(transform.position, noiseRadius);

        Vector3 eye = pc.EyePosition;
        Vector3 fwd = pc.cameraPivot.forward;
        foreach (var s in StalkerAI.All)
        {
            Vector3 target = s.transform.position + Vector3.up * 1.4f;
            Vector3 to = target - eye;
            if (to.magnitude > range || Vector3.Angle(fwd, to) > coneAngle) continue;
            if (Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore)) continue; // ściana
            s.Scare();
        }
    }
}
