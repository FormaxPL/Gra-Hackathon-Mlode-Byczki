using System.Collections;
using UnityEngine;

public enum MenuStalkerAct { PeekCorner, VentCrawl, StandFar }

/// <summary>
/// Potwór w menu głównym (korytarz Backrooms). Co 12–25 s losuje jedno zachowanie, nigdy to samo dwa razy z rzędu:
/// wychyla się zza rogu i patrzy w kamerę, wypełza z wentylacji i przechodzi korytarzem,
/// albo stoi nieruchomo w głębi i znika, gdy mrugną świetlówki. Kamera lekko dryfuje.
/// Punkty ścieżek ustawia generator menu (MenuSceneBuilder).
/// </summary>
public class MenuStalkerDirector : MonoBehaviour
{
    public Transform stalker;
    public MonsterAnimationDriver driver;
    public Camera cam;
    public Transform cornerHidden, cornerPeek, ventInside, ventMouth, ventFloor, walkEnd, farSpot;
    public Light[] lamps;

    readonly System.Random rng = new System.Random();
    Quaternion camBase;

    public static MenuStalkerAct NextAct(MenuStalkerAct last, System.Random r)
    {
        int n = System.Enum.GetValues(typeof(MenuStalkerAct)).Length;
        int pick = r.Next(n - 1);            // jedno z pozostałych
        if (pick >= (int)last) pick++;
        return (MenuStalkerAct)pick;
    }

    public static float NextDelay(System.Random r) => 12f + (float)r.NextDouble() * 13f;

    IEnumerator Start()
    {
        if (cam != null) camBase = cam.transform.localRotation;
        Hide();
        var last = (MenuStalkerAct)rng.Next(3);
        yield return new WaitForSeconds(3f); // pierwsze pojawienie się szybciej
        while (true)
        {
            last = NextAct(last, rng);
            yield return Play(last);
            Hide();
            yield return new WaitForSeconds(NextDelay(rng));
        }
    }

    /// <summary>Odegraj zachowanie od razu (podgląd / testy).</summary>
    public IEnumerator Play(MenuStalkerAct act)
    {
        switch (act)
        {
            case MenuStalkerAct.PeekCorner: yield return PeekCorner(); break;
            case MenuStalkerAct.VentCrawl: yield return VentCrawl(); break;
            default: yield return StandFar(); break;
        }
    }

    void Update()
    {
        if (cam == null) return;
        float t = Time.time;
        cam.transform.localRotation = camBase * Quaternion.Euler(Mathf.Sin(t * 0.21f) * 1.2f, Mathf.Sin(t * 0.13f) * 1.6f, Mathf.Sin(t * 0.17f) * 0.4f);
    }

    void Hide() { if (stalker != null) stalker.gameObject.SetActive(false); }

    void Place(Transform at, bool faceCamera)
    {
        stalker.gameObject.SetActive(true);
        stalker.position = at.position;
        if (faceCamera && cam != null)
        {
            var d = cam.transform.position - stalker.position; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) stalker.rotation = Quaternion.LookRotation(d);
        }
        else stalker.rotation = at.rotation;
    }

    void Mode(MonsterAnimationDriver.MotionMode m, float speed)
    {
        if (driver == null) return;
        driver.SetMode(m);
        driver.SetSpeed(speed);
    }

    IEnumerator MoveTo(Vector3 target, float speed, bool turn)
    {
        while ((stalker.position - target).sqrMagnitude > 0.0004f)
        {
            var d = target - stalker.position;
            if (turn) { var flat = new Vector3(d.x, 0f, d.z); if (flat.sqrMagnitude > 1e-4f) stalker.rotation = Quaternion.Slerp(stalker.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 6f); }
            stalker.position = Vector3.MoveTowards(stalker.position, target, speed * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator PeekCorner()
    {
        Place(cornerHidden, true);
        Mode(MonsterAnimationDriver.MotionMode.LockerWait, 0f);
        yield return MoveTo(cornerPeek.position, 0.35f, false);   // powoli wychyla się zza rogu
        yield return new WaitForSeconds(3f + (float)rng.NextDouble() * 2f);
        yield return MoveTo(cornerHidden.position, 0.8f, false);  // i chowa się szybciej
    }

    IEnumerator VentCrawl()
    {
        Place(ventInside, false);
        Mode(MonsterAnimationDriver.MotionMode.PipeEnter, 0f);
        yield return MoveTo(ventMouth.position, 0.45f, false);    // wypełza z kratki
        Mode(MonsterAnimationDriver.MotionMode.Idle, 0f);
        yield return MoveTo(ventFloor.position, 2.5f, false);     // zeskakuje na podłogę
        yield return new WaitForSeconds(0.8f);
        Mode(MonsterAnimationDriver.MotionMode.Walk, 1.3f);
        yield return MoveTo(walkEnd.position, 1.3f, true);        // i odchodzi korytarzem za róg
    }

    IEnumerator StandFar()
    {
        Place(farSpot, true);
        Mode(MonsterAnimationDriver.MotionMode.Idle, 0f);
        yield return new WaitForSeconds(4f + (float)rng.NextDouble() * 3f);
        // mrugnięcie świetlówek – w ciemności znika
        for (int i = 0; i < 3; i++)
        {
            SetLamps(false);
            yield return new WaitForSeconds(0.08f + (float)rng.NextDouble() * 0.1f);
            if (i == 1) Hide();
            SetLamps(true);
            yield return new WaitForSeconds(0.1f + (float)rng.NextDouble() * 0.15f);
        }
    }

    void SetLamps(bool on)
    {
        if (lamps == null) return;
        foreach (var l in lamps) if (l != null) l.enabled = on;
    }
}
