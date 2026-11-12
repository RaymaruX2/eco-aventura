using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private Animator anim;
    [SerializeField] private AudioClip audioDeadEnemy;
    [SerializeField] private AudioClip audioAttackEnemy;
    [SerializeField] private AudioSource audiosourceEnemy;
    public static bool isEnemyDeath = false;
    public static int killcounter = 0;
    private bool attack;
    private bool dead;

    [Header("Vida")]
    [Tooltip("Zarpazos necesarios para derrotarlo. Pisarlo lo derrota siempre de un golpe.")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private AudioClip audioHurtEnemy;

    [Header("Reaccion al dano")]
    [Tooltip("Velocidad inicial del retroceso al recibir un zarpazo (0 = no se mueve, p. ej. la planta).")]
    [SerializeField] private float knockbackSpeed = 6f;
    [Tooltip("Tiempo que el enemigo queda aturdido (sin moverse ni perseguir) tras un golpe.")]
    [SerializeField] private float stunTime = 0.4f;
    [SerializeField] private Color hurtColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Ataque por proximidad")]
    [Tooltip("Si es mayor que 0, el enemigo reproduce su animacion de ataque cuando el jugador esta a esta distancia (p. ej. la planta).")]
    [SerializeField] private float attackRange = 0f;

    private int health;
    private float stunnedUntil;
    private Vector2 knockbackVelocity;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Coroutine flashRoutine;
    private Transform player;

    private static readonly int EnemyAttackHash = Animator.StringToHash("enemyAttack");
    private static readonly int EnemyDeathHash = Animator.StringToHash("enemyDeath");

    public bool IsDead => dead;

    // Tipo de criatura para elegir sus sonidos en la BibliotecaSonidos (perro/, rana/, aguila/...).
    public string TipoSonido { get; private set; } = "enemigo";
    private float proximoSonidoAmbiente;

    /// <summary>Reproduce un sonido propio de esta criatura ("alerta", "ataque", "pasos"...).</summary>
    public void Sonar(string accion, float volumen = 1f)
    {
        if (!Sonido.Reproducir(TipoSonido + "/" + accion, transform.position, volumen))
            Sonido.Reproducir("enemigo/" + accion, transform.position, volumen);
    }
    public bool IsStunned => Time.time < stunnedUntil;
    public int Health => health;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        health = Mathf.Max(1, maxHealth);
        if (GetComponent<PerroGuardian>()) TipoSonido = "perro";
        else if (GetComponent<RanaSaltarina>()) TipoSonido = "rana";
        else if (GetComponent<AguilaPicado>()) TipoSonido = "aguila";
        else if (GetComponent<ZarigueyaPatrulla>()) TipoSonido = "zarigueya";
        else if (GetComponent<BeeFly>()) TipoSonido = "abeja";
        else if (GetComponent<SlugMove>()) TipoSonido = "babosa";
        proximoSonidoAmbiente = Time.time + Random.Range(2f, 8f);
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (dead) return;

        // Sonido ambiental propio de cada criatura, de vez en cuando y solo si esta cerca.
        if (Time.time >= proximoSonidoAmbiente)
        {
            proximoSonidoAmbiente = Time.time + Random.Range(4f, 9f);
            Sonido.Reproducir(TipoSonido + "/ambiente", transform.position, 0.8f);
        }

        // Retroceso tras un golpe: se frena de forma progresiva.
        if (knockbackVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += (Vector3)(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, 30f * Time.deltaTime);
        }

        if (anim == null || AnimacionAtaqueExterna) return;

        bool playerNear = attackRange > 0f && player != null && !Death.isDeath && !IsStunned &&
                          Vector2.Distance(player.position, transform.position) <= attackRange;

        if (Death.isAttack && attack)
        {
            anim.SetBool(EnemyAttackHash, true);
        }
        else
        {
            attack = false;
            anim.SetBool(EnemyAttackHash, playerNear);
        }
    }

    // ---------- Dano por zarpazo del jugador ----------
    public void TakeDamage(int amount, int directionSign)
    {
        if (dead) return;

        health -= Mathf.Max(1, amount);
        Flash();
        if (health > 0) Sonar("dano");

        if (health <= 0)
        {
            Die(false);
            return;
        }

        stunnedUntil = Time.time + stunTime;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
        {
            // Enemigos con fisica (rana): el retroceso es un pequeno salto hacia atras.
            if (knockbackSpeed > 0f) body.velocity = new Vector2(Mathf.Sign(directionSign) * knockbackSpeed * 0.6f, 3f);
        }
        else
        {
            knockbackVelocity = new Vector2(Mathf.Sign(directionSign) * knockbackSpeed, 0f);
        }
        if (audiosourceEnemy != null)
        {
            AudioClip clip = audioHurtEnemy != null ? audioHurtEnemy : audioAttackEnemy;
            if (clip != null) audiosourceEnemy.PlayOneShot(clip, 0.5f);
        }
    }

    private void Flash()
    {
        if (spriteRenderer == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Dos parpadeos rapidos: se ve claramente que el golpe ha entrado.
        for (int i = 0; i < 2; i++)
        {
            spriteRenderer.color = hurtColor;
            yield return new WaitForSeconds(0.06f);
            spriteRenderer.color = baseColor;
            yield return new WaitForSeconds(0.05f);
        }
        flashRoutine = null;
    }

    // ---------- Pisoton (trigger superior del enemigo) ----------
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStomp(other);
    }

    // Si el jugador entro en el trigger subiendo y despues cae sobre el enemigo,
    // OnTriggerEnter no se repite: por eso tambien se comprueba en Stay.
    private void OnTriggerStay2D(Collider2D other)
    {
        TryStomp(other);
    }

    private void TryStomp(Collider2D other)
    {
        if (dead || !other.gameObject.CompareTag("Player")) return;
        if (Death.isDeath) return;

        // Solo cuenta como pisoton si el jugador cae sobre el enemigo,
        // no si lo toca de lado o saltando desde abajo.
        Rigidbody2D playerRb = other.attachedRigidbody;
        if (playerRb != null && playerRb.velocity.y > 0.5f) return;

        // Los pies del jugador deben estar por encima de la mitad superior del cuerpo del enemigo.
        // Antes, el trigger del pisoton era mas ancho que el cuerpo (abeja, perro): al tocarlos de
        // LADO el jugador entraba primero en ese trigger y el enemigo moria en vez de hacer dano.
        float playerFeet = other.bounds.min.y;
        if (playerFeet < StompHeight()) return;

        Die(true);
    }

    // Altura minima de los pies del jugador para que un contacto con el trigger superior cuente
    // como pisoton: el punto medio-alto del cuerpo solido del enemigo.
    private float StompHeight()
    {
        float top = float.MinValue, bottom = float.MaxValue;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (col.isTrigger || !col.enabled) continue;
            top = Mathf.Max(top, col.bounds.max.y);
            bottom = Mathf.Min(bottom, col.bounds.min.y);
        }
        if (top == float.MinValue) return transform.position.y;
        return Mathf.Lerp(bottom, top, 0.6f);
    }

    /// <summary>Reproduce el sonido de ataque del enemigo (ladrido del perro, etc.).</summary>
    public void PlayAttackSound()
    {
        if (audiosourceEnemy != null && audioAttackEnemy != null) audiosourceEnemy.PlayOneShot(audioAttackEnemy, 0.5f);
    }

    /// <summary>
    /// Si es true, otro componente (p. ej. PerroGuardian) controla la animacion de ataque
    /// y Enemy deja de modificar el parametro enemyAttack.
    /// </summary>
    public bool AnimacionAtaqueExterna { get; set; }

    private void Die(bool stomped)
    {
        if (dead) return;
        dead = true;
        health = 0;
        knockbackVelocity = Vector2.zero;

        // Sonido de muerte propio de la criatura (si existe) o el asignado en el Inspector.
        if (!Sonido.Reproducir(TipoSonido + "/muerte", transform.position))
        {
            if (audiosourceEnemy != null && audioDeadEnemy != null) audiosourceEnemy.PlayOneShot(audioDeadEnemy, 0.5f);
            else Sonido.Reproducir("enemigo/muerte", transform.position);
        }
        EfectosFX.Estallido(transform.position, new Color(1f, 0.85f, 0.4f));
        if (anim != null)
        {
            anim.SetBool(EnemyAttackHash, false);
            anim.SetTrigger(EnemyDeathHash);
        }
        // Solo el pisoton hace rebotar al jugador.
        if (stomped) isEnemyDeath = true;
        killcounter += 1;

        // Mientras se reproduce la animacion de muerte, el enemigo ya no hace dano
        // ni bloquea al jugador, y deja de moverse.
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        // Se desactiva cualquier comportamiento del enemigo (babosa, abeja, perro, rana, aguila...).
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
            if (mb != this) mb.enabled = false;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        Invoke(nameof(EnemyDestroy), 1f);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (dead) return;
        if (other.gameObject.CompareTag("Player") && audioAttackEnemy != null && audiosourceEnemy != null)
        {
            audiosourceEnemy.PlayOneShot(audioAttackEnemy, 0.5f);
            attack = true;
        }
    }

    void EnemyDestroy()
    {
        Destroy(gameObject);
    }
}
