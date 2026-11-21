using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wykrywacz ruchu jak w Alien Isolation – obraz rysowany NA EKRANIE MODELU w lewej ręce.
/// Pokazuje tylko PORUSZAJĄCE SIĘ cele w zasięgu (domyślnie 10 m) – także w rurach nad tobą.
/// Czający się (nieruchomy) potwór jest dla niego niewidoczny. Trzymaj Tab / Q.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class MotionTracker : MonoBehaviour
{
    [Tooltip("Zasięg wykrywacza w metrach")]
    public float range = 10f;
    [Tooltip("Ekran na modelu wykrywacza (Quad z UV 0-1)")]
    public Renderer screenRenderer;
    public int resolution = 128;
    [Tooltip("Najwolniejszy odstęp między pingami (s)")]
    public float pingInterval = 1.2f;
    [Tooltip("Awaryjnie: pokaż obraz radaru także jako 2D na ekranie")]
    public bool overlayFallback = false;

    struct Blip { public Vector2 pos; public float strength; public float dist; }

    PlayerController pc;
    AudioSource src;
    AudioClip beep, idle;
    Texture2D tex;
    Color32[] bg, buf;
    Material screenMat;
    float pingTimer, pingRadius = 2f;
    bool wasActive;
    readonly List<Blip> blips = new List<Blip>();
    readonly List<Vector2> targets = new List<Vector2>();
    readonly List<float> targetDist = new List<float>();

    static readonly string[] Digits =
    {
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
    };

    public bool Active
    {
        get
        {
            if (pc == null || !pc.Inventory.hasTracker || !GameInput.TrackerHeld || GameManager.Blocked || pc.IsHidden) return false;
            var cam = Camcorder.Instance;
            if (cam != null && cam.Raise01 > 0.05f) return false;
            var hand = HandInteraction.Instance;
            return hand == null || !hand.Busy;
        }
    }

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.volume = 0.5f;
        beep = ProceduralAudio.Beep(1400f, 0.07f);
        idle = ProceduralAudio.Beep(700f, 0.05f);

        int r = Mathf.Max(32, resolution);
        resolution = r;
        tex = new Texture2D(r, r, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "TrackerScreen" };
        bg = BuildBackground(r);
        buf = new Color32[r * r];
        System.Array.Copy(bg, buf, bg.Length);
        tex.SetPixels32(buf);
        tex.Apply(false);

        if (screenRenderer != null)
        {
            screenMat = screenRenderer.material;
            if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", tex);
            if (screenMat.HasProperty("_MainTex")) screenMat.SetTexture("_MainTex", tex);
            if (screenMat.HasProperty("_EmissionMap")) screenMat.SetTexture("_EmissionMap", tex);
            if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", Color.white);
            if (screenMat.HasProperty("_Color")) screenMat.SetColor("_Color", Color.white);
            screenMat.EnableKeyword("_EMISSION");
            if (screenMat.HasProperty("_EmissionColor")) screenMat.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.6f));
        }
    }

    void Update()
    {
        if (!Active) { wasActive = false; return; }
        float dt = Time.deltaTime;
        if (!wasActive)
        {
            wasActive = true;
            pingTimer = 0.15f;
            blips.Clear();
        }

        // cele: poruszający się potwór w zasięgu (w poziomie, w układzie gracza)
        targets.Clear();
        targetDist.Clear();
        float nearest = float.MaxValue;
        Quaternion inv = Quaternion.Euler(0f, -pc.Yaw, 0f);
        foreach (var s in StalkerAI.All)
        {
            if (s == null || !s.IsMoving) continue;
            Vector3 rel = inv * (s.transform.position - transform.position);
            rel.y = 0f;
            float d = rel.magnitude;
            if (d > range) continue;
            targets.Add(new Vector2(rel.x, rel.z) / range);
            targetDist.Add(d);
            nearest = Mathf.Min(nearest, d);
        }

        // ping – fala rozchodzi się od środka i "zapala" cele, które mija
        pingTimer -= dt;
        if (pingTimer <= 0f)
        {
            bool any = nearest < range;
            pingTimer = any ? Mathf.Lerp(0.3f, pingInterval, nearest / range) : pingInterval * 1.4f;
            pingRadius = 0f;
            src.pitch = any ? Mathf.Lerp(1.6f, 0.95f, nearest / range) : 1f;
            src.PlayOneShot(any ? beep : idle, any ? 1f : 0.35f);
        }
        float prevR = pingRadius;
        pingRadius += dt * 1.8f;
        for (int i = 0; i < targets.Count; i++)
        {
            float d01 = targets[i].magnitude;
            if (d01 >= prevR && d01 < pingRadius)
                blips.Add(new Blip { pos = targets[i], strength = 1f, dist = targetDist[i] });
        }
        for (int i = blips.Count - 1; i >= 0; i--)
        {
            var b = blips[i];
            b.strength -= dt * 0.7f;
            if (b.strength <= 0f) blips.RemoveAt(i); else blips[i] = b;
        }

        Draw();
    }

    // ===================== RYSOWANIE EKRANU =====================

    void Draw()
    {
        int r = resolution;
        System.Array.Copy(bg, buf, bg.Length);
        float cx = (r - 1) * 0.5f, cy = (r - 1) * 0.5f, R = r * 0.45f;

        if (pingRadius <= 1f)
            Ring(cx, cy, pingRadius * R, new Color32(120, 255, 150, 255), (1f - pingRadius) * 0.8f, 1.6f);

        float nearestBlip = -1f;
        foreach (var b in blips)
        {
            float px = cx + b.pos.x * R, py = cy + b.pos.y * R;
            Disc(px, py, 7f, new Color32(90, 255, 120, 255), b.strength * 0.25f);
            Disc(px, py, 3.6f, new Color32(200, 255, 210, 255), b.strength);
            if (b.strength > 0.25f && (nearestBlip < 0f || b.dist < nearestBlip)) nearestBlip = b.dist;
        }

        // odczyt odległości w rogu
        Color32 txt = new Color32(140, 255, 160, 255);
        int s = Mathf.Max(2, r / 48);
        int x0 = r / 14, y0 = r / 14;
        if (nearestBlip >= 0f)
        {
            int m = Mathf.Clamp(Mathf.RoundToInt(nearestBlip), 0, 99);
            DigitAt(m / 10, x0, y0, s, txt);
            DigitAt(m % 10, x0 + 4 * s, y0, s, txt);
        }
        else
        {
            for (int k = 0; k < 2; k++)
                FillRect(x0 + k * 4 * s, y0 + 2 * s, 3 * s, s, txt, 0.6f);
        }

        tex.SetPixels32(buf);
        tex.Apply(false);
    }

    void Blend(int x, int y, Color32 c, float a)
    {
        if (a <= 0f || x < 0 || y < 0 || x >= resolution || y >= resolution) return;
        a = Mathf.Clamp01(a);
        int i = y * resolution + x;
        Color32 o = buf[i];
        buf[i] = new Color32(
            (byte)(o.r + (c.r - o.r) * a),
            (byte)(o.g + (c.g - o.g) * a),
            (byte)(o.b + (c.b - o.b) * a), 255);
    }

    void Disc(float cx, float cy, float rad, Color32 c, float a)
    {
        int minX = Mathf.FloorToInt(cx - rad), maxX = Mathf.CeilToInt(cx + rad);
        int minY = Mathf.FloorToInt(cy - rad), maxY = Mathf.CeilToInt(cy + rad);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= rad) Blend(x, y, c, a * Mathf.Clamp01(rad - d));
            }
    }

    void Ring(float cx, float cy, float rad, Color32 c, float a, float thickness)
    {
        int steps = Mathf.Max(32, Mathf.CeilToInt(rad * 7f));
        for (int i = 0; i < steps; i++)
        {
            float ang = i * Mathf.PI * 2f / steps;
            for (float t = -thickness * 0.5f; t <= thickness * 0.5f; t += 0.5f)
                Blend(Mathf.RoundToInt(cx + Mathf.Cos(ang) * (rad + t)), Mathf.RoundToInt(cy + Mathf.Sin(ang) * (rad + t)), c, a);
        }
    }

    void FillRect(int x, int y, int w, int h, Color32 c, float a)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++) Blend(xx, yy, c, a);
    }

    void DigitAt(int digit, int x0, int y0, int s, Color32 c)
    {
        string p = Digits[Mathf.Clamp(digit, 0, 9)];
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 3; col++)
                if (p[row * 3 + col] == '1')
                    FillRect(x0 + col * s, y0 + (4 - row) * s, s, s, c, 1f);
    }

    static Color32[] BuildBackground(int r)
    {
        var px = new Color32[r * r];
        float c = (r - 1) * 0.5f, R = r * 0.45f;
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                Color col = new Color(0.01f, 0.05f, 0.02f);
                if (d <= 1.02f)
                {
                    col = Color.Lerp(new Color(0.03f, 0.16f, 0.06f), new Color(0.01f, 0.07f, 0.03f), d);
                    bool ring = Mathf.Abs(d - 1f) < 0.018f || Mathf.Abs(d - 0.667f) < 0.012f || Mathf.Abs(d - 0.333f) < 0.012f;
                    bool cross = (Mathf.Abs(dx) < 0.6f || Mathf.Abs(dy) < 0.6f) && d < 1f;
                    if (ring) col = new Color(0.15f, 0.55f, 0.25f);
                    else if (cross) col = new Color(0.08f, 0.3f, 0.14f);
                    // znacznik "przód" u góry
                    if (Mathf.Abs(dx) < 2.5f && dy > R * 0.9f && dy < R) col = new Color(0.4f, 0.9f, 0.5f);
                }
                if (y % 2 == 0) col *= 0.85f; // linie skanowania
                if (d < 0.05f) col = new Color(0.5f, 1f, 0.6f);
                px[y * r + x] = col;
            }
        return px;
    }

    void OnGUI()
    {
        if (!overlayFallback || !Active || tex == null) return;
        float size = Screen.height * 0.3f;
        GUI.DrawTexture(new Rect((Screen.width - size) / 2f, Screen.height - size - 20f, size, size), tex);
    }
}
