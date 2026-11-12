using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Death : MonoBehaviour
{
    public GameObject player;
    public Vector2 checkPos;
    private Animator anim;
    private Collider2D _collider2D;
    private Rigidbody2D _rigidbody2D;
    private SpriteRenderer _spriteRenderer;
    public static bool isDeath;
    public static bool isAttack;
    [SerializeField] private AudioClip audiohurt;
    [SerializeField] private AudioSource audiosource;
    public static int deathcounter = 0;

    [Tooltip("Segundos de invulnerabilidad tras reaparecer, para no recibir dos golpes seguidos.")]
    [SerializeField] private float invulnerableTime = 1.0f;
    private float invulnerableUntil;

    private static readonly int DeathHash = Animator.StringToHash("death");
    private static readonly int DeadHash = Animator.StringToHash("dead");

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    private void Start()
    {
        _collider2D = GetComponent<Collider2D>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        // Si no hay checkpoint asignado, se reaparece en la posicion inicial.
        if (checkPos == Vector2.zero) checkPos = transform.position;
    }

    private void Update()
    {
        // Parpadeo durante la invulnerabilidad: el jugador ve que no puede recibir dano.
        if (_spriteRenderer == null) return;
        bool blink = !isDeath && IsInvulnerable && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f;
        Color c = _spriteRenderer.color;
        float a = blink ? 0.35f : 1f;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; _spriteRenderer.color = c; }
    }

    // Contacto con un enemigo o con una zona mortal (pinchos/barreras con tag "Enemy").
    private void OnCollisionEnter2D(Collision2D other)
    {
        TryHitFrom(other.gameObject);
    }

    // Antes solo se comprobaba al INICIAR el contacto: si el jugador seguia pegado al enemigo
    // cuando terminaba la invulnerabilidad, ya no recibia dano.
    private void OnCollisionStay2D(Collision2D other)
    {
        TryHitFrom(other.gameObject);
    }

    private void TryHitFrom(GameObject other)
    {
        if (!other.CompareTag("Enemy")) return;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.IsDead) return;
        RecibirDanio();
    }

    /// <summary>
    /// Quita una vida al jugador y lo devuelve al ultimo checkpoint.
    /// La usan los contactos con enemigos y los ataques a distancia (p. ej. el mordisco del perro).
    /// Devuelve false si el golpe se ignoro (ya muerto o invulnerable).
    /// </summary>
    public bool RecibirDanio()
    {
        if (isDeath || IsInvulnerable || HealthSystem.health <= 0) return false;

        anim.SetBool(DeadHash, true);
        anim.SetTrigger(DeathHash);
        isAttack = true;
        isDeath = true;
        HealthSystem.health = Mathf.Max(0, HealthSystem.health - 1);
        deathcounter += 1;
        _collider2D.enabled = false;
        if (audiosource != null && audiohurt != null) audiosource.PlayOneShot(audiohurt, 0.5f);
        else Sonido.Reproducir("jugador/dano");
        EfectosFX.Sacudir(0.25f, 0.25f);
        EfectosFX.Chispas(transform.position, new Color(1f, 0.4f, 0.35f), 10);
        if (HealthSystem.health <= 0) Sonido.Reproducir("jugador/derrota");
        Invoke(nameof(LoadCheckPoint), 1f);
        return true;
    }

    void LoadCheckPoint()
    {
        // Si el jugador estaba sobre una plataforma movil, se suelta antes de reaparecer.
        if (transform.parent != null) transform.SetParent(null);

        if (_rigidbody2D != null)
        {
            _rigidbody2D.velocity = Vector2.zero;
            _rigidbody2D.position = checkPos;
        }
        gameObject.transform.position = checkPos;
        isDeath = false;
        isAttack = false;
        // Fin de la muerte: el Animator sale de CharacterHurt hacia Idle (o Fall si reaparece en el aire).
        anim.SetBool(DeadHash, false);
        anim.ResetTrigger(DeathHash);
        _collider2D.enabled = true;
        CharacterControl.moving = true;
        invulnerableUntil = Time.time + invulnerableTime;
    }
}
