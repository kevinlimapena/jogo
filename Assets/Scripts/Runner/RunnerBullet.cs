using System.Collections.Generic;
using UnityEngine;

public class RunnerBullet : MonoBehaviour
{
    public static readonly List<RunnerBullet> All = new List<RunnerBullet>();
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public float vx;
    public float vz = 80f;
    public float life = 1.4f;
    public float damage = 1f;
    public int pierceLeft;
    public float radius = 0.2f;
    public float explode;
    public float homing;
    public bool canCrit = true;

    readonly HashSet<RunnerObstacle> alreadyHit = new HashSet<RunnerObstacle>();
    [HideInInspector] public MeshRenderer rend;

    // reaproveitamento: em vez de destruir, o tiro volta para a reserva
    static readonly Stack<RunnerBullet> pool = new Stack<RunnerBullet>();

    public static RunnerBullet Rent()
    {
        while (pool.Count > 0)
        {
            var b = pool.Pop();
            if (b != null) return b;
        }
        return null;
    }

    public void ResetShot()
    {
        alreadyHit.Clear();
        canCrit = true;
        explode = 0f;
        homing = 0f;
    }

    public void Release()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        if (pool.Count < 160) pool.Push(this);
        else Destroy(gameObject);
    }
    const float StepLen = 0.5f;

    void Update()
    {
        var g = RunnerGame.I;
        if (g == null || g.state == RunnerState.Menu || g.state == RunnerState.GameOver)
        {
            Release();
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 p = transform.position;

        // teleguiado: curva em direção ao inimigo mais próximo à frente
        if (homing > 0f)
        {
            var target = g.NearestShootable(p, 2f, 60f, null);
            if (target != null)
            {
                Vector3 tp = target.transform.position;
                float dz = Mathf.Max(1f, tp.z - p.z);
                float desiredVx = (tp.x - p.x) / dz * vz;
                vx = Mathf.MoveTowards(vx, desiredVx, homing * 120f * dt);
                float desiredY = Mathf.MoveTowards(p.y, tp.y, homing * 6f * dt);
                p.y = desiredY;
            }
        }

        Vector3 move = new Vector3(vx, 0f, vz) * dt;
        int steps = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / StepLen));
        Vector3 step = move / steps;

        for (int s = 0; s < steps; s++)
        {
            p += step;
            RunnerObstacle hit = null;
            foreach (var o in g.obstacles)
            {
                if (o == null || o.dead || o.type == ObType.Health || o.type == ObType.Ring || o.type == ObType.PlaneBox || o.type == ObType.CarBox || o.type == ObType.ShipBox || o.type == ObType.AngelBox || o.type == ObType.BabelBox || o.type == ObType.JerichoBox || o.type == ObType.GoliathBox || o.type == ObType.Note || o.type == ObType.NoteBad || o.type == ObType.Shockwave || o.type == ObType.SlingStone || o.type == ObType.HouseOpen || o.type == ObType.HouseBlood || o.type == ObType.Cone || o.type == ObType.Boost || o.type == ObType.Platform || o.type == ObType.Coin || alreadyHit.Contains(o)) continue;
                if (o.ContainsPoint(p, radius)) { hit = o; break; }
            }
            if (hit == null) continue;

            if (explode > 0f) g.Blast(p, explode, damage * 0.8f, hit);

            if (hit.type == ObType.Rival)
            {
                g.HitRival(hit, p);
                Release();
                return;
            }

            if (hit.Shootable)
            {
                alreadyHit.Add(hit);
                g.DamageEnemy(hit, damage, canCrit, p);
                g.ChainLightning(hit, damage * 0.5f);
                pierceLeft--;
                if (pierceLeft < 0)
                {
                    Release();
                    return;
                }
            }
            else
            {
                // parede ou barreira: bloqueia o tiro
                g.PlayClank();
                if (hit.stompable || hit.Indestructible) g.ImmuneHint(hit);
                g.Explode(p, new Color(1f, 0.9f, 0.4f), 4);
                Release();
                return;
            }
        }

        transform.position = p;
        if (vx != 0f) transform.rotation = Quaternion.LookRotation(new Vector3(vx, 0f, vz));
        life -= dt;
        if (life <= 0f) Release();
    }
}
