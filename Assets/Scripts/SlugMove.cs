using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Babosa: patrulla entre sus waypoints y persigue al jugador cuando lo ve cerca,
// sin salir nunca de su zona de patrulla (no se cae de las plataformas).
public class SlugMove : MonoBehaviour
{
    [SerializeField] private GameObject[] waypoints;
    private int currentWaypointIndex = 0;

    [SerializeField] private float speed = 2.0f;

    [Header("Comportamiento")]
    [Tooltip("Pausa en cada extremo de la patrulla, como si mirase alrededor.")]
    [SerializeField] private float waitAtWaypoint = 0.5f;
    [Tooltip("Distancia horizontal a la que detecta al jugador.")]
    [SerializeField] private float chaseRange = 4f;
    [Tooltip("Diferencia de altura maxima para considerar que el jugador esta en su plataforma.")]
    [SerializeField] private float chaseHeightTolerance = 1.5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;

    private Enemy enemy;
    private Transform player;
    private float waitUntil;
    private float minX, maxX;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        minX = float.MaxValue;
        maxX = float.MinValue;
        if (waypoints != null)
        {
            foreach (GameObject w in waypoints)
            {
                if (w == null) continue;
                minX = Mathf.Min(minX, w.transform.position.x);
                maxX = Mathf.Max(maxX, w.transform.position.x);
            }
        }
        if (minX > maxX) { minX = maxX = transform.position.x; }
    }

    private void Update()
    {
        if (waypoints == null || waypoints.Length == 0 || waypoints[currentWaypointIndex] == null) return;
        if (enemy != null && (enemy.IsDead || enemy.IsStunned)) return;

        float step = speed * Time.deltaTime;
        Vector3 pos = transform.position;

        // --- Persecucion ---
        if (PlayerInSight(pos))
        {
            float targetX = Mathf.Clamp(player.position.x, minX, maxX);
            float newX = Mathf.MoveTowards(pos.x, targetX, step * chaseSpeedMultiplier);
            // Mira siempre al jugador mientras lo persigue, aunque este en el borde de su zona.
            Face(player.position.x - pos.x);
            transform.position = new Vector3(newX, pos.y, pos.z);
            waitUntil = 0f;
            return;
        }

        // --- Patrulla ---
        if (Time.time < waitUntil) return;

        Vector2 target = waypoints[currentWaypointIndex].transform.position;
        if (Vector2.Distance(target, pos) < .1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            waitUntil = Time.time + waitAtWaypoint;
            return;
        }

        Vector2 newPos = Vector2.MoveTowards(pos, target, step);
        Face(newPos.x - pos.x);
        // Se conserva la Z original para no alterar el orden de dibujado.
        transform.position = new Vector3(newPos.x, newPos.y, pos.z);
    }

    private bool PlayerInSight(Vector3 pos)
    {
        if (player == null || Death.isDeath || chaseRange <= 0f) return false;
        Vector3 pp = player.position;
        bool inRange = Mathf.Abs(pp.x - pos.x) <= chaseRange && Mathf.Abs(pp.y - pos.y) <= chaseHeightTolerance;
        bool inZone = pp.x >= minX - chaseRange * 0.5f && pp.x <= maxX + chaseRange * 0.5f;
        return inRange && inZone;
    }

    // El sprite de la babosa mira a la izquierda: rotacion 0 = izquierda, 180 = derecha
    // (igual que el script original, que tambien voltea los colliders).
    private void Face(float dx)
    {
        if (Mathf.Abs(dx) < 0.0001f) return;
        transform.rotation = dx < 0f ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 180, 0);
    }
}
