using UnityEngine;

/// Drone que orbita o jogador e atira sozinho no inimigo mais próximo.
public class RunnerDrone : MonoBehaviour
{
    public int index;
    float cd;
    float angle;

    void Start()
    {
        cd = Random.Range(0.2f, 0.8f);
    }

    void Update()
    {
        var g = RunnerGame.I;
        if (g == null || g.player == null) return;
        float dt = Time.deltaTime;
        int count = Mathf.Max(1, g.stats.drones);

        angle += dt * 2.2f;
        float a = angle + index * Mathf.PI * 2f / count;
        var pp = g.player.transform.position;
        transform.position = pp + new Vector3(Mathf.Cos(a) * 1.3f, 1.3f + Mathf.Sin(a * 2f) * 0.15f, Mathf.Sin(a) * 0.5f - 0.2f);
        transform.Rotate(0f, 300f * dt, 0f);

        if (g.state != RunnerState.Playing) return;

        cd -= dt;
        if (cd > 0f) return;

        var target = g.NearestShootable(transform.position, 4f, 45f, null);   // alcance reduzido
        if (target == null) { cd = 0.15f; return; }

        cd = 0.9f / Mathf.Sqrt(g.stats.fireRateMul);
        Vector3 tp = target.transform.position;
        Vector3 p = transform.position;
        p.y = Mathf.Min(p.y, tp.y + 0.3f);
        float rel = 75f;
        float vz = g.speed + rel;
        float time = Mathf.Max(0.05f, (tp.z - p.z) / vz);
        float vx = (tp.x - p.x) / time;
        var b = g.FireBullet(p, vx, vz, 0.7f * g.stats.damageMul, 0.8f, 0.65f, 0, 0f, 1.5f, new Color(0.4f, 1f, 0.6f));
        b.canCrit = true;
    }
}
