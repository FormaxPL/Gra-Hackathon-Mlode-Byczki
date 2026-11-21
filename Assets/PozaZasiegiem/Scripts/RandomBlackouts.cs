using UnityEngine;

/// <summary>
/// Losowe awarie prądu w czasie eksploracji (oprócz fabularnej po naprawie radia):
/// co 3–6 minut światła gasną na 20–45 s, bez podpowiedzi na ekranie.
/// Nie w przerywnikach, pościgu, kryjówce, przy czytaniu notatki ani gdy trwa inna awaria.
/// </summary>
[RequireComponent(typeof(Blackout))]
public class RandomBlackouts : MonoBehaviour
{
    const float RetryDelay = 10f;

    readonly System.Random rng = new System.Random();
    float timer;

    public static float NextInterval(System.Random r) => 180f + (float)r.NextDouble() * 180f;
    public static float NextDuration(System.Random r) => 20f + (float)r.NextDouble() * 25f;

    public static bool CanStart(bool explorePhase, bool cutscene, bool blackoutActive, bool inputBlocked, bool hiding) =>
        explorePhase && !cutscene && !blackoutActive && !inputBlocked && !hiding;

    void Start() => timer = NextInterval(rng);

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        var sd = StoryDirector.Instance;
        var gm = GameManager.Instance;
        var pc = PlayerController.Instance;
        bool explore = sd != null && sd.CurrentPhase == StoryDirector.Phase.Explore;
        bool ok = CanStart(explore, PlayerController.CutsceneLock, Blackout.Active,
                           gm != null && gm.InputBlocked, pc != null && pc.IsHidden);
        if (!ok) { timer = RetryDelay; return; }

        Blackout.Instance.Begin(NextDuration(rng), false);
        timer = NextInterval(rng);
    }
}
