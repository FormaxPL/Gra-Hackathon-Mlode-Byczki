using UnityEngine;

/// <summary>
/// Dźwięki generowane kodem, żeby gra miała audio od pierwszej minuty.
/// Później podmieńcie je na prawdziwe pliki z Freesound (pola AudioClip w komponentach).
/// </summary>
public static class ProceduralAudio
{
    const int SR = 44100;

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Zapętla bez trzasku: koniec płynnie przechodzi w początek.</summary>
    static float[] LoopFade(float[] d, float fadeSeconds)
    {
        int fade = Mathf.Clamp((int)(SR * fadeSeconds), 1, d.Length / 3);
        int m = d.Length - fade;
        var o = new float[m];
        for (int i = 0; i < m; i++) o[i] = d[i];
        for (int i = 0; i < fade; i++)
        {
            float a = (float)i / fade;
            o[i] = d[i] * a + d[m + i] * (1f - a);
        }
        return o;
    }

    static float Clip(float x) => Mathf.Clamp(x, -1f, 1f);
    static float Soft(float x) => x / (1f + Mathf.Abs(x));

    public static AudioClip Beep(float freq, float duration)
    {
        int n = (int)(SR * duration);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float env = Mathf.Min(1f, i / 300f) * Mathf.Min(1f, (n - i) / 600f);
            d[i] = Mathf.Sin(2f * Mathf.PI * freq * i / SR) * 0.35f * env;
        }
        return Make("Beep", d);
    }

    /// <summary>Głuche uderzenie – kroki.</summary>
    public static AudioClip Thump(float duration, float freq, int seed)
    {
        var r = new System.Random(seed);
        int n = (int)(SR * duration);
        var d = new float[n];
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Exp(-t * 18f) * Mathf.Min(1f, i / 40f);
            float f = freq * (1f + 1.5f * Mathf.Exp(-t * 40f));
            phase += 2f * Mathf.PI * f / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.08f;
            d[i] = (Mathf.Sin(phase) * 0.8f + lp * 0.6f) * env;
        }
        return Make("Thump", d);
    }

    /// <summary>Szum (np. gaśnica). brightness 0..1 = jak "jasny" szum.</summary>
    public static AudioClip Noise(float duration, float brightness, string name)
    {
        var r = new System.Random(1234);
        int n = (int)(SR * duration);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Min(1f, t / 0.02f) * Mathf.Min(1f, (duration - t) / 0.3f);
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * Mathf.Clamp01(brightness);
            d[i] = lp * 0.6f * env;
        }
        return Make(name, d);
    }

    /// <summary>Brzęczenie świetlówek – zapętlone tło.</summary>
    public static AudioClip Hum(float duration)
    {
        var r = new System.Random(77);
        int n = (int)(SR * duration);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = 0.5f * Mathf.Sin(2f * Mathf.PI * 120f * t)
                    + 0.25f * Mathf.Sin(2f * Mathf.PI * 240f * t)
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * 360f * t)
                    + 0.05f * Mathf.Sin(2f * Mathf.PI * 1200f * t);
            s += ((float)r.NextDouble() * 2f - 1f) * 0.03f;
            d[i] = s * 0.4f;
        }
        return Make("Hum", d);
    }

    /// <summary>Wrzask potwora przy rozpoczęciu pościgu.</summary>
    public static AudioClip Screech(float duration)
    {
        var r = new System.Random(5);
        int n = (int)(SR * duration);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Min(1f, t * 20f) * Mathf.Exp(-t * 2.2f);
            float f = 900f + 350f * Mathf.Sin(2f * Mathf.PI * 9f * t) - 300f * t;
            phase += 2f * Mathf.PI * f / SR;
            float s = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2.01f) + 0.3f * Mathf.Sin(phase * 0.5f);
            s += ((float)r.NextDouble() * 2f - 1f) * 0.35f;
            d[i] = Clip(s * 0.3f * env);
        }
        return Make("Screech", d);
    }

    /// <summary>Metaliczny stuk – potwór w rurach, włazy.</summary>
    public static AudioClip Clank(int seed, float duration)
    {
        var r = new System.Random(seed);
        int n = (int)(SR * duration);
        var d = new float[n];
        float baseF = 140f + (float)r.NextDouble() * 180f;
        float[] ratios = { 1f, 2.76f, 5.40f, 8.93f, 13.3f };
        float[] decays = { 6f, 9f, 15f, 22f, 32f };
        float[] amps = { 0.5f, 0.35f, 0.25f, 0.18f, 0.1f };
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = 0f;
            for (int k = 0; k < ratios.Length; k++)
                s += Mathf.Sin(2f * Mathf.PI * baseF * ratios[k] * t) * amps[k] * Mathf.Exp(-t * decays[k]);
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.3f;
            s += lp * Mathf.Exp(-t * 60f) * 0.8f;
            float t2 = t - 0.11f;
            if (t2 > 0f) s += Mathf.Sin(2f * Mathf.PI * baseF * 1.3f * t2) * 0.25f * Mathf.Exp(-t2 * 14f);
            d[i] = Clip(s * 0.5f);
        }
        return Make("Clank", d);
    }

    /// <summary>Bicie serca (lub-dub).</summary>
    public static AudioClip Heartbeat()
    {
        int n = (int)(SR * 0.45f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Clip(Beat(t, 0f, 52f, 0.9f) + Beat(t, 0.22f, 44f, 0.6f));
        }
        return Make("Heartbeat", d);
    }

    static float Beat(float t, float start, float freq, float amp)
    {
        float x = t - start;
        if (x < 0f) return 0f;
        float env = Mathf.Min(1f, x / 0.008f) * Mathf.Exp(-x * 22f);
        return Mathf.Sin(2f * Mathf.PI * freq * x * (1f + 0.6f * Mathf.Exp(-x * 30f))) * amp * env;
    }

    /// <summary>Niski, chrapliwy pomruk potwora.</summary>
    public static AudioClip Growl(float duration)
    {
        var r = new System.Random(99);
        int n = (int)(SR * duration);
        var d = new float[n];
        float lp = 0f, lp2 = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Min(1f, t / 0.15f) * Mathf.Min(1f, (duration - t) / 0.4f);
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.05f;
            lp2 += (lp - lp2) * 0.2f;
            float rasp = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (23f + 6f * Mathf.Sin(t * 3f)) * t);
            phase += 2f * Mathf.PI * (70f + 12f * Mathf.Sin(t * 2.1f)) / SR;
            float tone = Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f);
            d[i] = Clip((lp2 * 3.5f * rasp + tone * 0.18f * rasp) * env * 0.6f);
        }
        return Make("Growl", d);
    }

    /// <summary>Ciężki, mokry oddech – pętla odtwarzana z potwora, gdy jest blisko.</summary>
    public static AudioClip Breathing(float duration)
    {
        var r = new System.Random(31);
        int n = (int)(SR * duration);
        var d = new float[n];
        float lpA = 0f, lpB = 0f, lpC = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float u = t / duration; // 0..1
            float noise = (float)r.NextDouble() * 2f - 1f;
            lpA += (noise - lpA) * 0.25f;
            lpB += (lpA - lpB) * 0.08f;
            lpC += (lpB - lpC) * 0.04f;
            float band = lpA - lpB;          // syk wdechu
            float low = lpB - lpC;           // chrapliwy wydech
            float inhale = u < 0.4f ? Mathf.Sin(u / 0.4f * Mathf.PI) : 0f;
            float exhale = u > 0.47f && u < 0.93f ? Mathf.Sin((u - 0.47f) / 0.46f * Mathf.PI) : 0f;
            float rasp = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 31f * t);
            d[i] = Clip(band * inhale * 1.6f + low * exhale * 3.2f * rasp);
        }
        return Make("Breathing", d);
    }

    /// <summary>Seria mlaskających kliknięć – potwór "szuka".</summary>
    public static AudioClip Clicks(int seed)
    {
        var r = new System.Random(seed);
        int n = (int)(SR * 1.3f);
        var d = new float[n];
        int count = 4 + r.Next(4);
        for (int c = 0; c < count; c++)
        {
            int start = (int)(SR * (0.05f + (float)r.NextDouble() * 1.1f));
            float f = 2200f + (float)r.NextDouble() * 1800f;
            for (int i = 0; i < SR / 25 && start + i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 160f);
                float burst = i < 90 ? ((float)r.NextDouble() * 2f - 1f) : 0f;
                d[start + i] += (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + burst * 0.5f) * env;
            }
        }
        for (int i = 0; i < n; i++) d[i] = Clip(d[i] * 0.7f);
        return Make("Clicks", d);
    }

    /// <summary>Syk.</summary>
    public static AudioClip Hiss(float duration)
    {
        var r = new System.Random(44);
        int n = (int)(SR * duration);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.15f;
            float hp = noise - lp;
            float env = Mathf.Min(1f, t / 0.08f) * Mathf.Exp(-t * 1.6f);
            d[i] = Clip(hp * env * 0.55f * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 13f * t)));
        }
        return Make("Hiss", d);
    }

    /// <summary>Ciche mruczenie czającego się potwora (pętla) – słychać je spod włazu.</summary>
    public static AudioClip LurkPurr(float duration)
    {
        var r = new System.Random(808);
        int n = (int)(SR * (duration + 0.5f));
        var d = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.03f;
            lp2 += (lp - lp2) * 0.1f;
            float am = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 22f * t);
            float swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / duration);
            float s = lp2 * 6f * am * swell + Mathf.Sin(2f * Mathf.PI * 44f * t) * 0.08f * am;
            if (r.NextDouble() < 0.00012) // pojedyncze mokre mlaśnięcie
                for (int k = 0; k < 300 && i + k < n; k++) d[i + k] += Mathf.Sin(k * 0.35f) * Mathf.Exp(-k / 60f) * 0.3f;
            d[i] += s;
        }
        for (int i = 0; i < n; i++) d[i] = Clip(d[i] * 0.7f);
        return Make("LurkPurr", LoopFade(d, 0.5f));
    }

    /// <summary>Kapnięcie śliny.</summary>
    public static AudioClip Drip()
    {
        int n = (int)(SR * 0.25f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = 500f + 900f * Mathf.Exp(-t * 45f);
            phase += 2f * Mathf.PI * f / SR;
            d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 28f) * 0.45f;
        }
        return Make("Drip", d);
    }

    /// <summary>Dysonansowy akord grozy (gdy potwór cię zauważy).</summary>
    public static AudioClip Stinger()
    {
        var r = new System.Random(66);
        float dur = 2.4f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float[] f = { 110f, 116.5f, 164.8f, 174.6f, 220f, 233.1f, 1760f, 1864f };
        float[] a = { 0.22f, 0.2f, 0.16f, 0.15f, 0.12f, 0.12f, 0.05f, 0.05f };
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Min(1f, t / 0.015f) * Mathf.Exp(-t * 1.3f);
            float s = 0f;
            for (int k = 0; k < f.Length; k++)
            {
                float trem = k >= 6 ? (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 11f * t)) : 1f;
                float ph = 2f * Mathf.PI * f[k] * t;
                s += (Mathf.Sin(ph) + 0.5f * Mathf.Sin(ph * 2f) + 0.3f * Mathf.Sin(ph * 3f)) * a[k] * trem;
            }
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.1f;
            s += lp * Mathf.Exp(-t * 4f) * 0.8f;
            d[i] = Soft(s * env * 1.3f) * 0.85f;
        }
        return Make("Stinger", d);
    }

    /// <summary>Głośne uderzenie jumpscare'a.</summary>
    public static AudioClip JumpscareHit()
    {
        var r = new System.Random(13);
        float dur = 1.8f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float boomPh = 0f, scrPh = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            boomPh += 2f * Mathf.PI * (30f + 45f * Mathf.Exp(-t * 6f)) / SR;
            float boom = Mathf.Sin(boomPh) * Mathf.Exp(-t * 2.5f);
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.35f;
            float crash = lp * Mathf.Exp(-t * 3f);
            float f = 1300f + 500f * Mathf.Sin(2f * Mathf.PI * 13f * t) - 400f * t;
            scrPh += 2f * Mathf.PI * f / SR;
            float scr = (Mathf.Sin(scrPh) + 0.6f * Mathf.Sin(scrPh * 1.98f)) * Mathf.Min(1f, t * 30f) * Mathf.Exp(-t * 1.6f);
            d[i] = Soft((boom * 1.4f + crash * 1.1f + scr * 0.7f) * 1.8f) * 0.95f;
        }
        return Make("JumpscareHit", d);
    }

    /// <summary>Niski dron – stałe, niepokojące tło (pętla).</summary>
    public static AudioClip Drone(float duration)
    {
        var r = new System.Random(5150);
        int n = (int)(SR * (duration + 1f));
        var d = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.3f
                    + Mathf.Sin(2f * Mathf.PI * 55.375f * t) * 0.25f
                    + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.12f
                    + Mathf.Sin(2f * Mathf.PI * 110.125f * t) * 0.08f;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.02f;
            lp2 += (lp - lp2) * 0.05f;
            float wind = lp2 * 5f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t));
            d[i] = Clip((s + wind) * 0.55f);
        }
        return Make("Drone", LoopFade(d, 1f));
    }

    /// <summary>Warstwa napięcia (wysokie, drżące dysonanse + pulsowanie) – głośniejsza, gdy potwór blisko.</summary>
    public static AudioClip TensionLayer(float duration)
    {
        int n = (int)(SR * (duration + 1f));
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float trem = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            float hi = (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 932.375f * t) * 0.9f
                      + Mathf.Sin(2f * Mathf.PI * 1244.5f * t) * 0.4f) * 0.12f * trem;
            float pulsePhase = (t * 1.5f) % 1f;
            float pulse = Mathf.Sin(2f * Mathf.PI * 41f * t) * Mathf.Exp(-pulsePhase * 9f) * 0.5f;
            d[i] = Clip(hi + pulse);
        }
        return Make("Tension", LoopFade(d, 1f));
    }

    /// <summary>Szelest plastikowych pasków w drzwiach chłodni.</summary>
    public static AudioClip PlasticRustle()
    {
        var r = new System.Random(272);
        int n = (int)(SR * 0.5f);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.4f;
            float crackle = r.NextDouble() < 0.02 ? 1f : 0.25f;
            float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 5f);
            d[i] = Clip((noise - lp) * crackle * env * 0.8f);
        }
        return Make("PlasticRustle", d);
    }

    /// <summary>Szelest kartki papieru.</summary>
    public static AudioClip PaperRustle()
    {
        var r = new System.Random(919);
        int n = (int)(SR * 0.45f);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.55f;
            float crinkle = r.NextDouble() < 0.05 ? 1f : 0.15f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
            d[i] = Clip((noise - lp * 0.5f) * crinkle * env * 0.5f);
        }
        return Make("PaperRustle", d);
    }

    /// <summary>Chwycenie przedmiotu (stuknięcie + szmer).</summary>
    public static AudioClip Grab()
    {
        var r = new System.Random(3);
        int n = (int)(SR * 0.22f);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.2f;
            float knock = Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-t * 60f);
            d[i] = Clip(knock * 0.5f + lp * Mathf.Exp(-t * 14f) * 0.6f);
        }
        return Make("Grab", d);
    }

    /// <summary>Pisk włączania noktowizji.</summary>
    public static AudioClip Whine()
    {
        int n = (int)(SR * 0.5f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            phase += 2f * Mathf.PI * (2000f + 6000f * (t / 0.5f)) / SR;
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Min(1f, (0.5f - t) / 0.1f);
            d[i] = Mathf.Sin(phase) * env * 0.12f;
        }
        return Make("Whine", d);
    }

    /// <summary>Przełączenie noktowizji: mechaniczny klik + elektroniczny pisk (w górę przy włączeniu, w dół przy wyłączeniu).</summary>
    public static AudioClip NightVisionSwitch(bool on)
    {
        var r = new System.Random(on ? 61 : 62);
        float dur = 0.7f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = 0f;
            // klik-klak przełącznika
            for (int c = 0; c < 2; c++)
            {
                float x = t - c * 0.045f;
                if (x >= 0f && x < 0.02f)
                    s += (((float)r.NextDouble() * 2f - 1f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3200f * x) * 0.5f) * Mathf.Exp(-x * 300f);
            }
            // pisk ładowania przetwornicy
            float w = Mathf.Clamp01((t - 0.06f) / (dur - 0.06f));
            float f = on ? Mathf.Lerp(1500f, 7500f, w * w) : Mathf.Lerp(6500f, 900f, w);
            phase += 2f * Mathf.PI * f / SR;
            float env = t < 0.06f ? 0f : Mathf.Min(1f, (t - 0.06f) / 0.04f) * Mathf.Min(1f, (dur - t) / 0.15f);
            s += Mathf.Sin(phase) * 0.16f * env;
            // cichy szum elektroniki
            s += ((float)r.NextDouble() * 2f - 1f) * 0.03f * env;
            d[i] = Clip(s);
        }
        return Make("NightVisionSwitch", d);
    }

    /// <summary>Zadyszka gracza (pętla) – szybki, krótki oddech.</summary>
    public static AudioClip Panting()
    {
        var r = new System.Random(140);
        float dur = 1.9f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float lpA = 0f, lpB = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float u = t / dur;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lpA += (noise - lpA) * 0.35f;
            lpB += (lpA - lpB) * 0.12f;
            float band = lpA - lpB;
            float inhale = u < 0.38f ? Mathf.Sin(u / 0.38f * Mathf.PI) : 0f;
            float exhale = u > 0.45f && u < 0.95f ? Mathf.Sin((u - 0.45f) / 0.5f * Mathf.PI) : 0f;
            float voice = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.05f * exhale;
            d[i] = Clip(band * (inhale * 1.1f + exhale * 1.6f) + voice);
        }
        return Make("Panting", d);
    }

    /// <summary>Krok na różnych podłogach: 0 = płytki/beton, 1 = wykładzina, 2 = blacha.</summary>
    public static AudioClip Footstep(int surface, int seed)
    {
        var r = new System.Random(seed);
        float dur = surface == 2 ? 0.3f : 0.18f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            float s;
            if (surface == 1)
            {
                lp += (noise - lp) * 0.04f;                       // miękko, stłumione
                s = lp * 2.2f * Mathf.Exp(-t * 22f) * Mathf.Min(1f, t / 0.01f);
            }
            else if (surface == 2)
            {
                lp += (noise - lp) * 0.3f;                        // metaliczne brzęknięcie
                phase += 2f * Mathf.PI * 520f / SR;
                s = (lp * 0.6f * Mathf.Exp(-t * 40f) + (Mathf.Sin(phase) + 0.6f * Mathf.Sin(phase * 2.7f)) * 0.25f * Mathf.Exp(-t * 14f));
            }
            else
            {
                lp += (noise - lp) * 0.22f;                       // twarde klapnięcie
                phase += 2f * Mathf.PI * (140f * (1f + Mathf.Exp(-t * 50f))) / SR;
                s = (lp * 0.7f + Mathf.Sin(phase) * 0.5f) * Mathf.Exp(-t * 30f);
            }
            d[i] = Clip(s);
        }
        return Make("Footstep", d);
    }

    /// <summary>Świst wahadłowych drzwi.</summary>
    public static AudioClip Swish()
    {
        var r = new System.Random(77);
        int n = (int)(SR * 0.4f);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.08f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI);
            float creak = Mathf.Sin(2f * Mathf.PI * (260f + 40f * Mathf.Sin(t * 30f)) * t) * 0.15f * (t < 0.15f ? 1f : 0f);
            d[i] = Clip(lp * env * 1.6f + creak);
        }
        return Make("Swish", d);
    }

    // ===================== v5: fabuła, awaria prądu, pościg =====================

    /// <summary>Wyłączenie prądu: opadający buczek transformatora + głuche "klak".</summary>
    public static AudioClip PowerDown()
    {
        var r = new System.Random(201);
        float dur = 2.4f; int n = (int)(SR * dur); var d = new float[n];
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = Mathf.Lerp(120f, 18f, Mathf.Pow(t / dur, 0.6f));
            ph += 2f * Mathf.PI * f / SR;
            float env = Mathf.Clamp01(1f - t / dur);
            float hum = (Mathf.Sin(ph) + 0.4f * Mathf.Sin(ph * 3f)) * 0.35f * env;
            float nz = (float)r.NextDouble() * 2f - 1f; lp += (nz - lp) * 0.2f;
            float clunk = t < 0.12f ? (lp * 0.9f + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.8f) * Mathf.Exp(-t * 30f) : 0f;
            d[i] = Clip(hum + clunk);
        }
        return Make("PowerDown", d);
    }

    /// <summary>Powrót prądu: trzaski przekaźników i narastający buczek.</summary>
    public static AudioClip PowerUp()
    {
        var r = new System.Random(202);
        float dur = 1.8f; int n = (int)(SR * dur); var d = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = Mathf.Lerp(30f, 100f, Mathf.Clamp01(t / 1.2f));
            ph += 2f * Mathf.PI * f / SR;
            float hum = Mathf.Sin(ph) * 0.25f * Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((dur - t) / 0.4f);
            float clicks = 0f;
            for (int c = 0; c < 4; c++)
            {
                float x = t - c * 0.17f;
                if (x >= 0f && x < 0.015f) clicks += ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-x * 400f);
            }
            d[i] = Clip(hum + clicks * 0.8f);
        }
        return Make("PowerUp", d);
    }

    /// <summary>Syrena alarmowa (pętla) – dwa tony.</summary>
    public static AudioClip Siren()
    {
        float dur = 2f; int n = (int)(SR * dur); var d = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = 600f + 300f * (0.5f - 0.5f * Mathf.Cos(t / dur * Mathf.PI * 2f));
            ph += 2f * Mathf.PI * f / SR;
            d[i] = Clip(Soft((Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2f)) * 1.6f) * 0.35f);
        }
        return Make("Siren", d);
    }

    /// <summary>Szum radiowy z "głosem" (pasmo mowy modulowane jak sylaby).</summary>
    // formanty samogłosek (F1, F2) – a, e, i, o, u
    static readonly Vector2[] Vowels = { new Vector2(800, 1200), new Vector2(500, 1800), new Vector2(300, 2300), new Vector2(500, 900), new Vector2(330, 800) };

    /// <summary>
    /// „Mowa” postaci bez aktora głosowego: mamrotanie z sylab (samogłoski z formantami), jak w grach indie.
    /// Liczba sylab wynika z długości tekstu, pauzy z interpunkcji, wysokość z <paramref name="basePitch"/>
    /// (Hz, ok. 110–130 męski, 200–230 żeński). Ten sam tekst brzmi zawsze tak samo. Pusty tekst = null.
    /// </summary>
    public static AudioClip Mumble(string text, float basePitch, float maxDuration)
    {
        if (string.IsNullOrEmpty(text)) return null;
        uint h = 2166136261; // FNV-1a – stabilny między uruchomieniami
        foreach (char ch in text) { h ^= ch; h *= 16777619; }
        var r = new System.Random((int)(h & 0x7fffffff));

        var data = new System.Collections.Generic.List<float>();
        int maxN = (int)(SR * maxDuration);
        int letters = 0;
        for (int ci = 0; ci < text.Length && data.Count < maxN; ci++)
        {
            char ch = text[ci];
            if (char.IsLetterOrDigit(ch)) { letters++; if (letters < 3) continue; letters = 0; }
            else if (ch == '.' || ch == ',' || ch == '?' || ch == '!' || ch == '…')
            {
                int pause = (int)(SR * (ch == ',' ? 0.09f : 0.16f));
                for (int i = 0; i < pause && data.Count < maxN; i++) data.Add(0f);
                continue;
            }
            else continue;

            // jedna sylaba: krótki dźwięczny „bla” z losową samogłoską i lekkim ślizgiem wysokości
            float len = 0.08f + (float)r.NextDouble() * 0.06f;
            float f0 = basePitch * (0.88f + (float)r.NextDouble() * 0.26f);
            float glide = ((float)r.NextDouble() - 0.5f) * 0.2f;
            var v = Vowels[r.Next(Vowels.Length)];
            int n = (int)(SR * len);
            // wzmocnienia harmonicznych (formanty) raz na sylabę – nie na każdą próbkę
            int harm = Mathf.Max(1, (int)(3200f / f0));
            var gain = new float[harm + 1];
            for (int k = 1; k <= harm; k++)
            {
                float hf = k * f0;
                gain[k] = 0.25f / k + Mathf.Exp(-Mathf.Pow((hf - v.x) / 160f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((hf - v.y) / 260f, 2f));
            }
            float ph = 0f;
            for (int i = 0; i < n && data.Count < maxN; i++)
            {
                float t = (float)i / n;
                float f = f0 * (1f + glide * t);
                ph += 2f * Mathf.PI * f / SR;
                float s = 0f;
                for (int k = 1; k <= harm; k++) s += Mathf.Sin(ph * k) * gain[k];
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.15f)); // miękki atak i wybrzmienie
                data.Add(s * env);
            }
            int gap = (int)(SR * (0.02f + (float)r.NextDouble() * 0.04f));
            for (int i = 0; i < gap && data.Count < maxN; i++) data.Add(0f);
        }
        if (data.Count == 0) return null;

        float peak = 0f;
        foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        var arr = data.ToArray();
        float norm = peak > 0f ? 0.6f / peak : 1f;
        for (int i = 0; i < arr.Length; i++) arr[i] *= norm;
        return Make("Mumble", arr);
    }

    public static AudioClip RadioVoice(float duration, int seed)
    {
        var r = new System.Random(seed);
        int n = (int)(SR * duration); var d = new float[n];
        float a = 0f, b = 0f, ph = 0f;
        float syl = 0f, sylRate = 5.5f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float nz = (float)r.NextDouble() * 2f - 1f;
            a += (nz - a) * 0.45f; b += (a - b) * 0.08f;
            syl += sylRate / SR;
            float env = Mathf.Pow(Mathf.Abs(Mathf.Sin(syl * Mathf.PI)), 0.6f) * (Mathf.PerlinNoise(t * 1.7f, seed) > 0.32f ? 1f : 0.1f);
            float f0 = 140f + 40f * Mathf.PerlinNoise(t * 2f, 3f);
            ph += 2f * Mathf.PI * f0 / SR;
            float voice = (Soft(Mathf.Sin(ph) * 4f) * 0.5f + (a - b) * 0.8f) * env;
            float hiss = nz * 0.08f;
            float crackle = r.NextDouble() < 0.0006 ? ((float)r.NextDouble() * 2f - 1f) * 0.8f : 0f;
            d[i] = Clip(voice * 0.45f + hiss + crackle);
        }
        return Make("RadioVoice", d);
    }

    /// <summary>Pisk w uszach po upadku.</summary>
    public static AudioClip Tinnitus(float duration)
    {
        int n = (int)(SR * duration); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((duration - t) / (duration * 0.7f));
            d[i] = Mathf.Sin(2f * Mathf.PI * 4200f * t) * 0.12f * env;
        }
        return Make("Tinnitus", d);
    }

    /// <summary>Pękająca podłoga: trzaski + łamanie desek + głuchy łomot.</summary>
    public static AudioClip FloorBreak()
    {
        var r = new System.Random(203);
        float dur = 1.6f; int n = (int)(SR * dur); var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float nz = (float)r.NextDouble() * 2f - 1f; lp += (nz - lp) * 0.3f;
            float cracks = (r.NextDouble() < 0.004 * (1f - t / dur) ? nz : 0f) * 1.2f;
            float rumble = lp * Mathf.Exp(-t * 2.5f) * 0.8f;
            float boom = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 4f) * 0.9f;
            d[i] = Clip(cracks + rumble + boom);
        }
        return Make("FloorBreak", d);
    }

    /// <summary>Skrzypienie (np. podłogi, metalu).</summary>
    public static AudioClip Creak(int seed)
    {
        var r = new System.Random(seed);
        float dur = 1.1f; int n = (int)(SR * dur); var d = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = 180f + 120f * Mathf.Sin(t * 3.1f) + 30f * Mathf.PerlinNoise(t * 20f, seed);
            ph += 2f * Mathf.PI * f / SR;
            float env = Mathf.Sin(t / dur * Mathf.PI);
            float grit = ((float)r.NextDouble() * 2f - 1f) * 0.25f;
            d[i] = Clip((Soft(Mathf.Sin(ph) * 3f) * (0.7f + grit)) * 0.35f * env);
        }
        return Make("Creak", d);
    }

    /// <summary>Zgrzyt podnoszonej bramy.</summary>
    public static AudioClip GateGrind(float duration)
    {
        var r = new System.Random(204);
        int n = (int)(SR * duration); var d = new float[n];
        float lp = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float nz = (float)r.NextDouble() * 2f - 1f; lp += (nz - lp) * 0.15f;
            ph += 2f * Mathf.PI * (90f + 15f * Mathf.Sin(t * 7f)) / SR;
            float env = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((duration - t) / 0.3f);
            float rattle = Mathf.Sin(t * 2f * Mathf.PI * 11f) > 0.6f ? nz * 0.3f : 0f;
            d[i] = Clip((lp * 0.6f + Soft(Mathf.Sin(ph) * 3f) * 0.3f + rattle) * env);
        }
        return Make("GateGrind", d);
    }

    /// <summary>Buczenie portalu (pętla) – niski chór + migotanie.</summary>
    public static AudioClip PortalHum()
    {
        float dur = 4f; int n = (int)(SR * dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.2f
                    + Mathf.Sin(2f * Mathf.PI * 220f * t + Mathf.Sin(t * 3f) * 2f) * 0.08f;
            s *= 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f * 0.5f);
            d[i] = Clip(s);
        }
        return Make("PortalHum", LoopFade(d, 0.5f));
    }

    /// <summary>Świst przejścia przez portal / szybkiego ruchu.</summary>
    public static AudioClip Whoosh(float duration)
    {
        var r = new System.Random(205);
        int n = (int)(SR * duration); var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float u = t / duration;
            float nz = (float)r.NextDouble() * 2f - 1f;
            float k = Mathf.Lerp(0.02f, 0.5f, Mathf.Sin(u * Mathf.PI));
            lp += (nz - lp) * k;
            d[i] = Clip(lp * Mathf.Sin(u * Mathf.PI) * 0.9f);
        }
        return Make("Whoosh", d);
    }

    // =====================================================================
    //  STRASZNY AMBIENT (AmbientScape) i TRAILER
    // =====================================================================

    /// <summary>
    /// Straszne tło całej gry (pętla): głęboki dron, który powoli „dudni”, dysonansowa plama
    /// (sekunda mała + tryton), powiew powietrza w kanałach i co jakiś czas daleki pomruk budynku.
    /// </summary>
    public static AudioClip CreepyAmbience(float duration)
    {
        var r = new System.Random(6660);
        int n = (int)(SR * (duration + 2f));
        var d = new float[n];
        float lp = 0f, lp2 = 0f, rum = 0f, rum2 = 0f, whinePh = 0f, tritPh = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            // dwa prawie równe tony -> dudnienie 0.6 Hz
            float sub = Mathf.Sin(2f * Mathf.PI * 36.7f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 37.3f * t) * 0.26f;
            float sw1 = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 7.3f);
            float sw2 = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 11.1f + 1.3f);
            float sw3 = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 5.7f + 2.1f);
            tritPh += 2f * Mathf.PI * (207.65f + 1.2f * Mathf.Sin(2f * Mathf.PI * 0.21f * t)) / SR;
            float pad = (Mathf.Sin(2f * Mathf.PI * 146.83f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 293.66f * t)) * 0.05f * sw1
                      + (Mathf.Sin(2f * Mathf.PI * 155.56f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 311.13f * t)) * 0.045f * sw2
                      + Mathf.Sin(tritPh) * 0.035f * sw3;
            // cichy, szklany pisk, który powoli „pływa”
            whinePh += 2f * Mathf.PI * (1318.5f + 6f * Mathf.Sin(2f * Mathf.PI * 0.07f * t)) / SR;
            float whine = Mathf.Sin(whinePh) * 0.012f * sw1 * sw2;
            float nz = (float)r.NextDouble() * 2f - 1f;
            lp += (nz - lp) * 0.03f;
            lp2 += (lp - lp2) * 0.03f;
            float air = lp2 * 6f * (0.35f + 0.65f * Mathf.PerlinNoise(t * 0.15f, 3.3f));
            rum += (nz - rum) * 0.002f;
            rum2 += (rum - rum2) * 0.004f;
            float swell = Mathf.PerlinNoise(t * 0.09f, 7.7f);
            float rumble = rum2 * 40f * swell * swell;
            d[i] = Soft((sub + pad + whine + air + rumble) * 1.4f) * 0.7f;
        }
        return Make("CreepyAmbience", LoopFade(d, 2f));
    }

    /// <summary>Kilka głuchych stuknięć gdzieś w rurach (z ciszą na końcu – na pogłos).</summary>
    public static AudioClip DistantKnocks(int seed)
    {
        var r = new System.Random(seed);
        int count = 3 + r.Next(3);
        var starts = new float[count];
        float at = 0.05f;
        for (int k = 0; k < count; k++) { starts[k] = at; at += 0.16f + (float)r.NextDouble() * 0.32f; }
        float dur = at + 1.3f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float s = 0f;
            for (int k = 0; k < count; k++)
            {
                float u = t - starts[k];
                if (u < 0f || u > 0.35f) continue;
                float amp = k == count - 1 ? 1f : 0.75f + 0.25f * Mathf.Sin(k * 1.7f);
                s += Mathf.Sin(2f * Mathf.PI * (85f + 20f * Mathf.Exp(-u * 30f)) * u) * Mathf.Exp(-u * 16f) * amp;
                s += ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-u * 90f) * 0.5f * amp;
            }
            lp += (s - lp) * 0.25f;
            d[i] = Clip(Soft(lp * 2.2f) * 0.8f);
        }
        return Make("DistantKnocks", d);
    }

    /// <summary>Jęk metalu – rura pracuje pod ciężarem czegoś, co po niej pełznie.</summary>
    public static AudioClip MetalGroan(int seed)
    {
        var r = new System.Random(seed);
        float dur = 2.4f + (float)r.NextDouble() * 1.2f;
        int n = (int)(SR * (dur + 1.2f));
        var d = new float[n];
        float ph = 0f, lp = 0f, baseF = 48f + (float)r.NextDouble() * 30f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            if (t > dur) { d[i] = 0f; continue; }
            float u = t / dur;
            float f = baseF * (1f + 0.35f * Mathf.Sin(u * Mathf.PI * 1.3f)) + 12f * Mathf.PerlinNoise(t * 6f, seed * 0.01f);
            ph += 2f * Mathf.PI * f / SR;
            float env = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.5f);
            float grit = 0.75f + 0.5f * (Mathf.PerlinNoise(t * 40f, 2.2f) - 0.5f);
            float tone = Soft(Mathf.Sin(ph) * 4f) * grit + Mathf.Sin(ph * 3.01f) * 0.25f + Mathf.Sin(ph * 5.03f) * 0.12f;
            float nz = (float)r.NextDouble() * 2f - 1f;
            lp += (nz - lp) * 0.08f;
            d[i] = Clip((tone * 0.45f + lp * 0.35f) * env);
        }
        return Make("MetalGroan", d);
    }

    /// <summary>Niewyraźny szept – jakby ktoś mówił tuż za ścianą.</summary>
    public static AudioClip Whisper(int seed, float duration)
    {
        var r = new System.Random(seed);
        int n = (int)(SR * (duration + 1f));
        var d = new float[n];
        float lpA = 0f, lpB = 0f, syllEnd = 0f, syllStart = 0f, gapEnd = 0f;
        bool hiss = false;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            if (t > duration) { d[i] = 0f; continue; }
            if (t >= gapEnd && t >= syllEnd)
            {
                syllStart = t;
                syllEnd = t + 0.08f + (float)r.NextDouble() * 0.16f;
                gapEnd = syllEnd + ((float)r.NextDouble() < 0.25f ? 0.15f + (float)r.NextDouble() * 0.25f : 0.02f);
                hiss = r.NextDouble() < 0.3;
            }
            float nz = (float)r.NextDouble() * 2f - 1f;
            lpA += (nz - lpA) * 0.45f;
            lpB += (nz - lpB) * 0.06f;
            float band = lpA - lpB;          // „głos” (pasmo środkowe)
            float sib = nz - lpA;            // syczące „s”
            float syl = t < syllEnd ? Mathf.Sin(Mathf.Clamp01((t - syllStart) / (syllEnd - syllStart)) * Mathf.PI) : 0f;
            float formant = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (3f + 2f * Mathf.PerlinNoise(t * 2f, seed)) * t);
            float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((duration - t) / 0.4f);
            d[i] = Clip((band * formant * 1.6f + (hiss ? sib * 0.6f : sib * 0.1f)) * syl * env * 0.9f);
        }
        return Make("Whisper", d);
    }

    /// <summary>Uderzenie pod tytuł gry w trailerze: niski, przesterowany „braam” z długim ogonem.</summary>
    public static AudioClip TitleBoom()
    {
        var r = new System.Random(777);
        float dur = 4.5f;
        int n = (int)(SR * dur);
        var d = new float[n];
        float ph = 0f, subPh = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            ph += 2f * Mathf.PI * 36.7f / SR;
            float saw = 0f;
            for (int k = 1; k <= 7; k++) saw += Mathf.Sin(ph * k * (1f + 0.002f * k)) / k;
            subPh += 2f * Mathf.PI * (36.7f + 30f * Mathf.Exp(-t * 5f)) / SR;
            float sub = Mathf.Sin(subPh);
            float nz = (float)r.NextDouble() * 2f - 1f;
            lp += (nz - lp) * 0.2f;
            float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 0.85f);
            float hit = lp * Mathf.Exp(-t * 6f) * 1.4f;
            d[i] = Soft((saw * 0.8f + sub * 1.1f) * env * 2.2f + hit) * 0.95f;
        }
        return Make("TitleBoom", d);
    }
}
