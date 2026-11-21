using UnityEngine;

/// <summary>
/// Latarka z baterią (trzymana w prawej ręce): mocny snop + szerokie światło rozproszone.
/// Włączona latarka = Stalker widzi cię z dalszej odległości i zauważa snop, jeśli na niego poświecisz.
/// Gdy kamera jest podniesiona, klawisz F przełącza noktowizję zamiast latarki.
/// </summary>
[RequireComponent(typeof(Light))]
public class Flashlight : MonoBehaviour
{
    public float battery = 100f;
    public float drainPerSecond = 0.6f;
    public bool startOn = true;
    [Tooltip("Obiekt szkła latarki – świeci, gdy latarka jest włączona")]
    public Renderer lensRenderer;
    [Tooltip("Szerokie, słabsze światło wokół snopu (rozproszenie)")]
    public Light spill;
    [Tooltip("Stożek snopu światła (efekt wolumetryczny) – widoczny, gdy latarka świeci")]
    public Renderer beamRenderer;

    public bool IsOn { get; private set; }
    public float Battery01 => battery / 100f;
    public float Range => spot != null ? spot.range : 0f;
    public float SpotAngle => spot != null ? spot.spotAngle : 0f;

    Light spot;
    float baseIntensity, spillBase, baseRange, spillRange;
    Material beamMat;
    Color beamColor;

    void Awake()
    {
        spot = GetComponent<Light>();
        baseIntensity = spot.intensity;
        baseRange = spot.range;
        if (spill != null) { spillBase = spill.intensity; spillRange = spill.range; }
        if (beamRenderer != null)
        {
            beamMat = beamRenderer.material;
            if (beamMat.HasProperty("_Color")) beamColor = beamMat.GetColor("_Color");
        }
        SetOn(startOn);
    }

    void Update()
    {
        if (!GameManager.Blocked && GameInput.FlashlightDown)
        {
            var pc = PlayerController.Instance;
            var cam = Camcorder.Instance;
            bool camUp = cam != null && cam.IsRaised;
            if (!camUp && (pc == null || !pc.IsHidden)) SetOn(!IsOn);
        }

        if (!IsOn) return;
        battery -= drainPerSecond * Time.deltaTime;
        if (battery <= 0f) { battery = 0f; SetOn(false); return; }

        float k = 1f;
        if (battery < 30f) k *= Mathf.Lerp(0.5f, 1f, battery / 30f);   // słabnie przy końcu baterii
        if (battery < 15f && Random.value < 0.08f) k *= Random.Range(0.1f, 0.6f);
        // awaria prądu: latarka "mało daje" – słaba, krótka i migocze
        float rangeK = 1f;
        var bo = Blackout.Instance;
        if (Blackout.Active && bo != null)
        {
            k *= bo.flashlightPower * (Random.value < 0.05f ? Random.Range(0.3f, 0.8f) : 1f);
            rangeK = bo.flashlightRange;
        }
        spot.range = baseRange * rangeK;
        if (spill != null) spill.range = spillRange * rangeK;
        spot.intensity = baseIntensity * k;
        if (spill != null) spill.intensity = spillBase * k;
        if (beamMat != null && beamMat.HasProperty("_Color"))
        {
            var c = beamColor; c.a *= k; beamMat.SetColor("_Color", c);
        }
        if (beamRenderer != null)
        {
            // snop chowa się przy kamerze w trybie podglądu (żeby nie zasłaniał obrazu)
            var cc = Camcorder.Instance;
            beamRenderer.enabled = VolumetricFx.Enabled && !(cc != null && cc.Viewing);
        }
    }

    public void SetOn(bool on)
    {
        if (on && battery <= 0f) on = false;
        IsOn = on;
        if (spot != null) spot.enabled = on;
        if (spill != null) spill.enabled = on;
        if (lensRenderer != null) lensRenderer.enabled = on;
        if (beamRenderer != null) beamRenderer.enabled = on && VolumetricFx.Enabled;
    }

    public void AddBattery(float amount) => battery = Mathf.Min(100f, battery + amount);
}
