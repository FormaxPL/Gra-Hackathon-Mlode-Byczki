using UnityEngine;

/// <summary>
/// Fizyczne podnoszenie rzeczy ręką:
///  - przedmioty: lewa ręka sięga, przedmiot leci do dłoni, części radiostacji są chwilę oglądane, potem chowane,
///  - notatki: kartka jest podnoszona przed oczy i czytana z kartki (tekst nanoszony na papier),
///    [E] / LPM / Esc odkłada ją z powrotem na miejsce.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class HandInteraction : MonoBehaviour
{
    public static HandInteraction Instance { get; private set; }

    [Header("Pozycje (lokalnie względem kamery)")]
    public Vector3 holdPos = new Vector3(0.03f, -0.1f, 0.42f);
    public Vector3 holdEuler = new Vector3(-20f, 25f, 0f);
    public Vector3 stowPos = new Vector3(-0.25f, -0.75f, 0.25f);
    public Vector3 readPos = new Vector3(0f, -0.01f, 0.34f);
    public Vector3 readEuler = new Vector3(-80f, 0f, 0f);

    enum S { Idle, Reach, Inspect, Stow, ReadIn, Reading, ReadOut }

    public bool Busy => state != S.Idle;
    public bool IsReading => state == S.ReadIn || state == S.Reading || state == S.ReadOut;

    S state;
    float t;
    PlayerController pc;
    FirstPersonArms arms;
    Transform cam;
    Camera viewCam;
    Transform item;
    Vector3 fromPos, outPos;
    Quaternion fromRot, outRot;
    Pickup pickup;
    Note note;
    Transform origParent;
    Vector3 origLocalPos, origLocalScale;
    Quaternion origLocalRot;
    AudioSource src;
    AudioClip paperClip, grabClip;
    GUIStyle titleStyle, textStyle, hintStyle;

    void Awake()
    {
        Instance = this;
        pc = GetComponent<PlayerController>();
        arms = GetComponentInChildren<FirstPersonArms>(true);
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        paperClip = ProceduralAudio.PaperRustle();
        grabClip = ProceduralAudio.Grab();
    }

    void Start()
    {
        cam = pc.cameraTransform;
        if (cam != null) viewCam = cam.GetComponent<Camera>();
    }

    static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    // ===================== API =====================

    public void PickUp(Pickup p)
    {
        if (Busy || p == null || cam == null) return;
        pickup = p;
        p.PrepareForHand();
        item = p.transform;
        item.SetParent(cam, true);
        fromPos = item.localPosition;
        fromRot = item.localRotation;
        state = S.Reach;
        t = 0f;
        src.PlayOneShot(grabClip, 0.6f);
    }

    public void ReadNote(Note n)
    {
        if (Busy || n == null || n.paper == null || cam == null) return;
        note = n;
        item = n.paper;
        origParent = item.parent;
        origLocalPos = item.localPosition;
        origLocalRot = item.localRotation;
        origLocalScale = item.localScale;
        item.SetParent(cam, true);
        fromPos = item.localPosition;
        fromRot = item.localRotation;
        state = S.ReadIn;
        t = 0f;
        src.PlayOneShot(paperClip, 0.8f);
    }

    // ===================== ANIMACJA =====================

    void Update()
    {
        if (state == S.Idle) return;
        if (item == null) { Finish(); return; }
        var gm = GameManager.Instance;
        if (gm != null && (gm.IsDying || gm.IsOver)) { AbortInstant(); return; }
        t += Time.deltaTime;
        Quaternion hold = Quaternion.Euler(holdEuler);

        switch (state)
        {
            case S.Reach:
            {
                float k = Smooth(t / 0.35f);
                item.localPosition = Vector3.Lerp(fromPos, holdPos, k);
                item.localRotation = Quaternion.Slerp(fromRot, hold, k);
                HandUnder(item.localPosition);
                if (t >= 0.35f) Next(pickup != null && pickup.type == PickupType.Part ? S.Inspect : S.Stow);
                break;
            }
            case S.Inspect:
            {
                item.localPosition = holdPos + new Vector3(0f, Mathf.Sin(t * 3f) * 0.005f, 0f);
                item.localRotation = hold * Quaternion.Euler(0f, t * 110f, 0f);
                HandUnder(item.localPosition);
                if (t >= 1.1f) { fromRot = item.localRotation; Next(S.Stow); }
                break;
            }
            case S.Stow:
            {
                float k = Smooth(t / 0.3f);
                item.localPosition = Vector3.Lerp(holdPos, stowPos, k);
                HandUnder(item.localPosition);
                if (t >= 0.3f)
                {
                    var p = pickup;
                    pickup = null;
                    item = null;
                    Finish();
                    if (p != null)
                    {
                        p.Apply(pc);
                        Destroy(p.gameObject);
                    }
                }
                break;
            }
            case S.ReadIn:
            {
                float k = Smooth(t / 0.45f);
                item.localPosition = Vector3.Lerp(fromPos, readPos, k);
                item.localRotation = Quaternion.Slerp(fromRot, Quaternion.Euler(readEuler), k);
                HoldPaper();
                if (arms != null) arms.rightLower = k;
                if (t >= 0.45f) Next(S.Reading);
                break;
            }
            case S.Reading:
            {
                item.localPosition = readPos + new Vector3(Mathf.Sin(Time.time * 0.9f) * 0.002f, Mathf.Sin(Time.time * 1.3f) * 0.003f, 0f);
                item.localRotation = Quaternion.Euler(readEuler);
                HoldPaper();
                if (t > 0.25f && (GameInput.InteractDown || GameInput.FireDown || GameInput.EscapeDown))
                {
                    fromPos = item.localPosition;
                    fromRot = item.localRotation;
                    outPos = cam.InverseTransformPoint(origParent != null ? origParent.TransformPoint(origLocalPos) : item.position);
                    Quaternion worldRot = origParent != null ? origParent.rotation * origLocalRot : item.rotation;
                    outRot = Quaternion.Inverse(cam.rotation) * worldRot;
                    src.PlayOneShot(paperClip, 0.6f);
                    Next(S.ReadOut);
                }
                break;
            }
            case S.ReadOut:
            {
                float k = Smooth(t / 0.35f);
                item.localPosition = Vector3.Lerp(fromPos, outPos, k);
                item.localRotation = Quaternion.Slerp(fromRot, outRot, k);
                if (arms != null) arms.rightLower = 1f - k;
                if (k < 0.6f) HoldPaper();
                else if (arms != null) arms.ClearLeftOverride();
                if (t >= 0.35f)
                {
                    item.SetParent(origParent, false);
                    item.localPosition = origLocalPos;
                    item.localRotation = origLocalRot;
                    item.localScale = origLocalScale;
                    note = null;
                    item = null;
                    Finish();
                }
                break;
            }
        }
    }

    /// <summary>Natychmiast kończy animację (np. gdy potwór zaatakował w trakcie czytania).</summary>
    void AbortInstant()
    {
        if (note != null && item != null)
        {
            item.SetParent(origParent, false);
            item.localPosition = origLocalPos;
            item.localRotation = origLocalRot;
            item.localScale = origLocalScale;
        }
        else if (pickup != null)
        {
            pickup.Apply(pc);
            Destroy(pickup.gameObject);
        }
        note = null;
        pickup = null;
        item = null;
        Finish();
    }

    void Next(S s)
    {
        state = s;
        t = 0f;
    }

    void Finish()
    {
        state = S.Idle;
        t = 0f;
        if (arms != null)
        {
            arms.ClearLeftOverride();
            arms.rightLower = 0f;
        }
    }

    void HandUnder(Vector3 itemLocal)
    {
        if (arms != null) arms.SetLeftOverride(itemLocal + new Vector3(-0.04f, -0.07f, -0.06f), Quaternion.Euler(-10f, 15f, 0f));
    }

    void HoldPaper()
    {
        if (arms != null) arms.SetLeftOverride(item.localPosition + new Vector3(-0.13f, -0.15f, -0.02f), Quaternion.Euler(-30f, 25f, 0f));
    }

    // ===================== TEKST NA KARTCE =====================

    void OnGUI()
    {
        if (state != S.Reading || note == null || item == null || viewCam == null) return;

        var mf = item.GetComponent<MeshFilter>();
        Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.25f, 0.01f, 0.35f));
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 local = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.max.y, (i & 2) == 0 ? b.min.z : b.max.z);
            Vector3 sp = viewCam.WorldToScreenPoint(item.TransformPoint(local));
            if (sp.z <= 0f) return;
            float gx = sp.x, gy = Screen.height - sp.y;
            minX = Mathf.Min(minX, gx); maxX = Mathf.Max(maxX, gx);
            minY = Mathf.Min(minY, gy); maxY = Mathf.Max(maxY, gy);
        }
        var r = new Rect(minX, minY, maxX - minX, maxY - minY);
        float pad = r.width * 0.08f;
        var inner = new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f);

        int fs = Mathf.Max(10, Mathf.RoundToInt(r.height / 34f));
        if (textStyle == null || textStyle.fontSize != fs)
        {
            textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = fs, fontStyle = FontStyle.Italic };
            textStyle.normal.textColor = new Color(0.12f, 0.09f, 0.07f);
            titleStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(fs * 1.25f), fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(12, Mathf.RoundToInt(fs * 0.95f)) };
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        GUI.Label(new Rect(inner.x, inner.y, inner.width, fs * 2f), note.Title, titleStyle);
        GUI.Label(new Rect(inner.x, inner.y + fs * 2.2f, inner.width, inner.height - fs * 2.2f), note.Text, textStyle);
        GUI.Label(new Rect(0f, Mathf.Min(Screen.height - fs * 2.5f, r.yMax + fs * 0.6f), Screen.width, fs * 2f), Loc.T("[E] odłóż notatkę", "[E] put the note down"), hintStyle);
    }
}
