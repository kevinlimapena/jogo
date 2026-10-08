using UnityEngine;

/// Pedacinhos que voam nas explosões, e efeitos rápidos (raios, ondas).
public class RunnerDebris : MonoBehaviour
{
    public Vector3 velocity;
    public float life = 0.8f;
    public bool gravity = true;
    public bool spin = true;
    public bool grow;          // cresce em vez de encolher (ondas de explosão)

    float maxLife;
    Vector3 startScale;
    Vector3 spinVel;

    void Start()
    {
        maxLife = life;
        startScale = transform.localScale;
        spinVel = spin ? Random.insideUnitSphere * 720f : Vector3.zero;
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
        if (life <= 0f) Destroy(gameObject);
    }
}
