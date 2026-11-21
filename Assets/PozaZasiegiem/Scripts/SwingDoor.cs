using UnityEngine;

/// <summary>
/// Wahadłowe drzwi kuchenne (dwa skrzydła). Otwierają się w stronę ruchu przechodzącego
/// i bujają się, zanim się zamkną. Lokalnie: X = szerokość, Z = kierunek przejścia, pivot = podłoga.
/// </summary>
public class SwingDoor : MonoBehaviour
{
    public Transform leftHinge, rightHinge;
    public float halfWidth = 1.1f;
    public float openAngle = 85f;
    public float stiffness = 9f;
    public float damping = 2.2f;

    float angL, velL, angR, velR;
    int openSide;
    AudioSource src;
    AudioClip swish;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 16f;
        swish = ProceduralAudio.Swish();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        int side = 0;
        var pc = PlayerController.Instance;
        if (pc != null && !pc.IsHidden) side = Check(pc.transform.position, pc.Velocity);
        if (side == 0)
            foreach (var s in StalkerAI.All)
            {
                if (s == null || s.InPipes) continue;
                side = Check(s.transform.position, s.Velocity);
                if (side != 0) break;
            }

        if (side != 0)
        {
            if (openSide == 0)
            {
                openSide = side;
                src.pitch = Random.Range(0.9f, 1.1f);
                src.PlayOneShot(swish, 0.7f);
            }
            angL = Mathf.MoveTowards(angL, -openSide * openAngle, dt * 420f);
            angR = Mathf.MoveTowards(angR, openSide * openAngle, dt * 420f);
            velL = velR = 0f;
        }
        else
        {
            openSide = 0;
            velL += (-stiffness * angL - damping * velL) * dt;
            angL += velL * dt;
            velR += (-stiffness * angR - damping * velR) * dt;
            angR += velR * dt;
        }
        if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0f, angL, 0f);
        if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0f, angR, 0f);
    }

    /// <summary>0 = nikogo w drzwiach; +1/-1 = w którą stronę otworzyć.</summary>
    int Check(Vector3 worldPos, Vector3 worldVel)
    {
        Vector3 lp = transform.InverseTransformPoint(worldPos);
        if (Mathf.Abs(lp.x) > halfWidth || Mathf.Abs(lp.z) > 0.9f) return 0;
        if (openSide != 0) return openSide;
        Vector3 lv = transform.InverseTransformDirection(worldVel);
        if (Mathf.Abs(lv.z) > 0.1f) return lv.z > 0f ? 1 : -1;
        return lp.z < 0f ? 1 : -1;
    }
}
