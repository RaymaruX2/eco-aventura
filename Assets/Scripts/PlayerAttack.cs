using System.Collections.Generic;
using UnityEngine;

// Ataque cuerpo a cuerpo del gato (zarpazo).
// Teclas: J, K, X o clic izquierdo.
// - En el suelo reproduce CatAttack y frena al gato mientras golpea.
// - En el aire reproduce CatJumpAttack sin frenar el salto.
// El golpe solo es efectivo durante la "ventana de impacto" (los fotogramas del zarpazo)
// y cada enemigo solo puede recibir un golpe por ataque.
[RequireComponent(typeof(CharacterControl))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Golpe")]
    [SerializeField] private int damage = 1;
    [Tooltip("Duracion total del ataque (igual a la del clip CatAttack: 6 fotogramas x 0.06 s).")]
    [SerializeField] private float attackDuration = 0.36f;
    [Tooltip("Espera minima entre el final de un ataque y el siguiente.")]
    [SerializeField] private float cooldown = 0.1f;
    [Tooltip("Inicio y fin (s) de la ventana en la que el zarpazo hace dano.")]
    [SerializeField] private float hitWindowStart = 0.1f;
    [SerializeField] private float hitWindowEnd = 0.27f;

    [Header("Zona de impacto (relativa al gato, mirando a la derecha)")]
    [SerializeField] private Vector2 hitboxOffset = new Vector2(1.25f, 0.05f);
    [SerializeField] private Vector2 hitboxSize = new Vector2(1.8f, 1.6f);

    [Header("Sensacion")]
    [Tooltip("Fraccion de la velocidad que conserva el gato mientras ataca en el suelo.")]
    [SerializeField] private float groundSpeedFactor = 0.3f;
    [SerializeField] private AudioClip swingClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;

    public bool IsAttacking { get; private set; }
    public float GroundSpeedFactor => groundSpeedFactor;

    private Animator anim;
    private CharacterControl control;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private float attackStartTime = -10f;
    private float nextAttackTime;
    private int facing = 1;
    private bool hitSoundPlayed;
    private readonly HashSet<Enemy> hitThisSwing = new HashSet<Enemy>();

    private static readonly int AttackHash = Animator.StringToHash("attack");

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (IsAttacking)
        {
            UpdateAttack();
            return;
        }

        if (!WantsToAttack() || !CanAttack()) return;
        StartAttack();
    }

    private static bool WantsToAttack()
    {
        return Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.K) ||
               Input.GetKeyDown(KeyCode.X) || Input.GetMouseButtonDown(0);
    }

    private bool CanAttack()
    {
        if (control == null) control = GetComponent<CharacterControl>();
        if (control != null && control.IsWallGrabbing) return false;
        return Time.time >= nextAttackTime &&
               Time.timeScale > 0f &&
               CharacterControl.moving && !CharacterControl.resume &&
               !Death.isDeath && !Climb.isClimbing;
    }

    private void StartAttack()
    {
        IsAttacking = true;
        attackStartTime = Time.time;
        hitThisSwing.Clear();
        hitSoundPlayed = false;
        // La direccion queda fija durante todo el golpe.
        facing = spriteRenderer != null && spriteRenderer.flipX ? -1 : 1;

        if (anim != null) anim.SetTrigger(AttackHash);
        if (audioSource != null && swingClip != null) audioSource.PlayOneShot(swingClip, volume);
        else Sonido.Reproducir("jugador/zarpazo");
    }

    private void UpdateAttack()
    {
        // Morir o empezar a trepar cancela el ataque.
        if (Death.isDeath || Climb.isClimbing)
        {
            Cancel();
            return;
        }

        float t = Time.time - attackStartTime;
        if (t >= hitWindowStart && t <= hitWindowEnd) DoHit();
        if (t >= attackDuration) EndAttack();
    }

    private void DoHit()
    {
        Vector2 center = (Vector2)transform.position + new Vector2(hitboxOffset.x * facing, hitboxOffset.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitboxSize, 0f);
        foreach (Collider2D col in hits)
        {
            if (col == null || col.attachedRigidbody == GetComponent<Rigidbody2D>()) continue;
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead || !hitThisSwing.Add(enemy)) continue;

            enemy.TakeDamage(damage, facing);
            EfectosFX.Chispas(col.bounds.ClosestPoint(transform.position), new Color(1f, 0.9f, 0.5f));
            EfectosFX.Sacudir(0.08f, 0.08f);
            if (!hitSoundPlayed && audioSource != null && hitClip != null)
            {
                audioSource.PlayOneShot(hitClip, volume);
                hitSoundPlayed = true;
            }
        }
    }

    private void EndAttack()
    {
        IsAttacking = false;
        nextAttackTime = Time.time + cooldown;
        if (anim != null) anim.ResetTrigger(AttackHash);
    }

    // Lo llama CharacterControl al saltar: el salto interrumpe el ataque de suelo.
    public void Cancel()
    {
        if (!IsAttacking) return;
        EndAttack();
    }

    public int Facing => facing;

    private void OnDrawGizmosSelected()
    {
        int dir = 1;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (Application.isPlaying) dir = facing;
        else if (sr != null && sr.flipX) dir = -1;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Vector3 center = transform.position + new Vector3(hitboxOffset.x * dir, hitboxOffset.y, 0f);
        Gizmos.DrawWireCube(center, new Vector3(hitboxSize.x, hitboxSize.y, 0.1f));
    }
}
