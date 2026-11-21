using UnityEngine;

/// <summary>Migająca świetlówka – klimat Backrooms.</summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public Vector2 onTime = new Vector2(0.5f, 4f);
    public Vector2 offTime = new Vector2(0.03f, 0.3f);
    [Tooltip("Stożek snopu światła / świecąca oprawa – gaśnie razem ze światłem")]
    public Renderer shaft;
    public Renderer fixture;

    Light l;
    float baseIntensity, timer;

    void Awake()
    {
        l = GetComponent<Light>();
        baseIntensity = l.intensity;
        timer = Random.Range(onTime.x, onTime.y);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            l.enabled = !l.enabled;
            timer = l.enabled ? Random.Range(onTime.x, onTime.y) : Random.Range(offTime.x, offTime.y);
            if (shaft != null) shaft.enabled = l.enabled && VolumetricFx.Enabled;
            if (fixture != null) fixture.enabled = l.enabled;
        }
        if (l.enabled) l.intensity = baseIntensity * Random.Range(0.85f, 1.05f);
    }
}
