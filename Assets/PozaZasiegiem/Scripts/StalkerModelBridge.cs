using UnityEngine;

/// <summary>
/// Łączy AI Stalkera z animacją nowego modelu (entity.fbx + MonsterAnimationDriver):
/// stan AI i prędkość → tryb animacji (chód, trucht, sprint, czajenie przy szafce, wejście do rury, atak).
/// </summary>
public class StalkerModelBridge : MonoBehaviour
{
    public StalkerAI ai;
    public MonsterAnimationDriver driver;

    StalkerAI.State last = (StalkerAI.State)(-1);

    void Awake()
    {
        if (ai == null) ai = GetComponent<StalkerAI>();
        if (driver == null) driver = GetComponentInChildren<MonsterAnimationDriver>(true);
    }

    void Update()
    {
        if (ai == null || driver == null) return;
        var st = ai.CurrentState;
        Vector3 v = ai.Velocity;
        v.y = 0f;
        float spd = v.magnitude;
        driver.SetSpeed(spd);

        if (st == StalkerAI.State.EnterPipe && last != StalkerAI.State.EnterPipe) driver.EnterPipe();
        last = st;

        if (st == StalkerAI.State.Kill) driver.SetMode(MonsterAnimationDriver.MotionMode.Attack);
        else if (st == StalkerAI.State.EnterPipe) { /* poza "wchodzenia" gra sama */ }
        else if (st == StalkerAI.State.InspectHiding && spd < 0.6f) driver.SetMode(MonsterAnimationDriver.MotionMode.LockerWait);
        else if (spd < 0.25f) driver.SetMode(MonsterAnimationDriver.MotionMode.Idle);
        else if (spd < 2.4f) driver.SetMode(MonsterAnimationDriver.MotionMode.Walk);
        else if (spd < 3.9f) driver.SetMode(MonsterAnimationDriver.MotionMode.Jog);
        else driver.SetMode(MonsterAnimationDriver.MotionMode.Sprint);

        driver.SetAlert(ai.CanSeePlayer || ai.IsHuntingPlayer);
    }
}
