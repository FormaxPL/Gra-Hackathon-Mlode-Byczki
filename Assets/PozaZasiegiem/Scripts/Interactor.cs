using UnityEngine;

/// <summary>Promień z kamery – pokazuje podpowiedź "[E] ..." i wywołuje Interact.</summary>
[RequireComponent(typeof(PlayerController))]
public class Interactor : MonoBehaviour
{
    public float range = 2.6f;
    PlayerController pc;

    void Awake() => pc = GetComponent<PlayerController>();

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var hand = HandInteraction.Instance;
        if (gm.InputBlocked || pc.IsHidden || (hand != null && hand.Busy)) { gm.SetPrompt(null); return; }

        IInteractable target = null;
        Transform cam = pc.cameraPivot;
        if (Physics.SphereCast(cam.position, 0.12f, cam.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Collide))
            target = hit.collider.GetComponentInParent<IInteractable>();

        string prompt = target != null ? target.GetPrompt(pc) : null;
        gm.SetPrompt(prompt);
        if (target != null && prompt != null && GameInput.InteractDown) target.Interact(pc);
    }
}
