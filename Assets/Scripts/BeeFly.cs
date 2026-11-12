using UnityEngine;

// Abeja: vuela patrullando a izquierda y derecha alrededor de su posicion inicial con un
// suave balanceo vertical. Si el jugador se acerca, lo persigue; si se aleja demasiado de
// su zona, vuelve a ella. Tras recibir un golpe queda aturdida un instante (Enemy.IsStunned).
[DisallowMultipleComponent]
public class BeeFly : MonoBehaviour
{
    [Header("Patrulla")]
    [SerializeField] private float patrolDistance = 2f;
    [Tooltip("Segundos que tarda en ir de un extremo al otro de la patrulla.")]
    [SerializeField] private float patrolHalfCycle = 2.2f;

    [Header("Balanceo")]
    [SerializeField] private float bobAmplitude = 0.2f;
    [SerializeField] private float bobFrequency = 2.5f;

    [Header("Persecucion")]
    [SerializeField] private float detectRange = 4.5f;
    [SerializeField] private float chaseSpeed = 2.2f;
    [Tooltip("Distancia maxima a su posicion inicial; mas alla deja de perseguir y vuelve.")]
    [SerializeField] private float leashRange = 6f;
    [SerializeField] private float returnSpeed = 1.8f;
    [Tooltip("Cuanto puede bajar respecto a su altura inicial (evita que se meta en el suelo).")]
    [SerializeField] private float maxDrop = 1.2f;

    [Header("Sprite")]
    [Tooltip("Marcar si el sprite original mira a la izquierda.")]
    [SerializeField] private bool spriteFacesLeft = true;

    private enum State { Patrol, Chase, Return }
    private State state = State.Patrol;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Vector2 home;
    private Vector2 basePos;       // posicion sin el balanceo
    private Vector2 lastBob;
    private float patrolTime;
    private float bobPhase;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        home = transform.position;
        basePos = home;
        // Fase aleatoria para que varias abejas no se muevan sincronizadas.
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        patrolTime = Random.Range(0f, patrolHalfCycle * 2f);
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (enemy != null && enemy.IsDead) return;

        // Se recalcula la base a partir de la posicion real: asi el retroceso de un golpe
        // (aplicado por Enemy) se respeta en vez de deshacerse.
        basePos = (Vector2)transform.position - lastBob;

        Vector2 bob = new Vector2(0f, Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude);

        if (enemy != null && enemy.IsStunned)
        {
            ApplyPosition(basePos, bob);
            return;
        }

        UpdateState();

        Vector2 previous = basePos;
        switch (state)
        {
            case State.Patrol:
            {
                patrolTime += Time.deltaTime;
                // Coseno: frena suavemente en los extremos como un insecto que cambia de rumbo.
                float x = home.x + Mathf.Sin(patrolTime * Mathf.PI / patrolHalfCycle) * patrolDistance;
                basePos = Vector2.MoveTowards(basePos, new Vector2(x, home.y), returnSpeed * 2f * Time.deltaTime);
                break;
            }
            case State.Chase:
            {
                Vector2 target = (Vector2)player.position + new Vector2(0f, 0.3f);
                target.y = Mathf.Max(target.y, home.y - maxDrop);
                basePos = Vector2.MoveTowards(basePos, target, chaseSpeed * Time.deltaTime);
                break;
            }
            case State.Return:
            {
                basePos = Vector2.MoveTowards(basePos, home, returnSpeed * Time.deltaTime);
                if (Vector2.Distance(basePos, home) < 0.05f)
                {
                    state = State.Patrol;
                    patrolTime = 0f;
                }
                break;
            }
        }

        float dx = basePos.x - previous.x;
        if (state == State.Chase && player != null) dx = player.position.x - basePos.x;
        Face(dx);
        ApplyPosition(basePos, bob);
    }

    private void UpdateState()
    {
        bool playerValid = player != null && !Death.isDeath;
        float distToPlayer = playerValid ? Vector2.Distance(player.position, basePos) : float.MaxValue;
        float playerFromHome = playerValid ? Vector2.Distance(player.position, home) : float.MaxValue;

        if (playerValid && distToPlayer <= detectRange && playerFromHome <= leashRange)
            state = State.Chase;
        else if (state == State.Chase)
            state = State.Return;
    }

    private void ApplyPosition(Vector2 basePosition, Vector2 bob)
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(basePosition.x + bob.x, basePosition.y + bob.y, p.z);
        lastBob = bob;
    }

    private void Face(float dx)
    {
        if (spriteRenderer == null || Mathf.Abs(dx) < 0.001f) return;
        bool movingRight = dx > 0f;
        spriteRenderer.flipX = spriteFacesLeft ? movingRight : !movingRight;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? (Vector3)home : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
        Gizmos.DrawLine(c + Vector3.left * patrolDistance, c + Vector3.right * patrolDistance);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawWireSphere(c, detectRange);
    }
}
