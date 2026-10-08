using System.Collections.Generic;
using UnityEngine;

/// Pedacinhos que voam nas explosões, e efeitos rápidos (raios, ondas).
public class RunnerDebris : MonoBehaviour
{
    public Vector3 velocity;
    public float life = 0.8f;
    public bool gravity = true;
    public bool spin = true;
    public bool grow;          // cresce em vez de encolher (ondas de explosão)
    public bool countsAsSphere;

    /// Quantos pedacinhos / ondas existem agora (para não poluir a tela).
    public static int Live, LiveSpheres;
    bool sphereCounted;
    bool begun;
    [HideInInspector] public bool pooled;          // pedacinho de explosão reaproveitável
    [HideInInspector] public MeshRenderer rend;

    static readonly Stack<RunnerDebris> pool = new Stack<RunnerDebris>();

    public static RunnerDebris Rent()
    {
        while (pool.Count > 0)
        {
            var d = pool.Pop();
            if (d != null) return d;
        }
        return null;
    }

    void OnEnable() { Live++; }
    void OnDisable() { Live--; }
    void OnDestroy()
    {
        if (sphereCounted) LiveSpheres--;
    }

    /// Recomeça a animação (usado ao reaproveitar um pedacinho).
    public void Begin()
    {
        begun = true;
        maxLife = life;
        startScale = transform.localScale;
        spinVel = spin ? Random.insideUnitSphere * 720f : Vector3.zero;
    }

    void Finish()
    {
        if (pooled)
        {
            gameObject.SetActive(false);
            if (pool.Count < 200) { pool.Push(this); return; }
        }
        Destroy(gameObject);
    }

    float maxLife;
    Vector3 startScale;
    Vector3 spinVel;

    void Start()
    {
        if (countsAsSphere) { sphereCounted = true; LiveSpheres++; }
        if (!begun) Begin();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (gravity) velocity.y -= 20f * dt;
        transform.position += velocity * dt;
        if (spin) transform.Rotate(spinVel * dt);
        life -= dt;
        float k = Mathf.Clamp01(life / maxLife);
        transform.localScale = grow ? startScale * (1.2f - k) : startScale * k;
        if (life <= 0f) Finish();
    }
}
