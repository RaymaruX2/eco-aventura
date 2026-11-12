using UnityEngine;
using UnityEngine.Tilemaps;

// Seguimiento de camara para el jugador.
// - Separa la Main Camera del Player al empezar (conservando el encuadre de la escena).
// - Suavizado independiente en X e Y, con ligera anticipacion en la direccion de avance.
// - Zona muerta vertical: los saltos normales no mueven la camara; si cae o sube de verdad, la sigue.
// - En caidas largas acelera el seguimiento vertical para que el jugador nunca salga de la pantalla.
// - Limites automaticos: calcula el rectangulo del nivel a partir de los Tilemaps con tag "Ground"
//   y nunca muestra zonas vacias fuera del mapa.
[DisallowMultipleComponent]
public class SmoothCameraFollow : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [Tooltip("Tiempo de suavizado horizontal (s). Mas bajo = camara mas pegada.")]
    [SerializeField] private float smoothTimeX = 0.12f;
    [Tooltip("Tiempo de suavizado vertical (s).")]
    [SerializeField] private float smoothTimeY = 0.22f;
    [Tooltip("Distancia que la camara se adelanta hacia donde se mueve el jugador.")]
    [SerializeField] private float lookAhead = 0.8f;
    [SerializeField] private float lookAheadSmooth = 0.4f;
    [Tooltip("Si el jugador se aleja mas de esta distancia (p. ej. al reaparecer), la camara salta directamente.")]
    [SerializeField] private float snapDistance = 8f;

    [Header("Vertical")]
    [Tooltip("Margen vertical (unidades) dentro del cual la camara no sigue al jugador.")]
    [SerializeField] private float verticalDeadZone = 0.9f;
    [Tooltip("Suavizado vertical cuando el jugador cae rapido o se acerca al borde de la pantalla.")]
    [SerializeField] private float fastSmoothTimeY = 0.07f;
    [Tooltip("Al caer, la camara baja un poco la mirada para mostrar donde va a aterrizar el jugador.")]
    [SerializeField] private float fallLookDown = 1.8f;
    [Tooltip("Velocidad de caida a partir de la cual la camara mira hacia abajo.")]
    [SerializeField] private float fallLookDownSpeed = 5f;

    [Header("Limites del nivel")]
    [SerializeField] private bool useLevelBounds = true;
    [Tooltip("Margen extra (unidades) que se deja ver alrededor de los tiles del nivel.")]
    [SerializeField] private float boundsPadding = 0f;
    [Tooltip("Cielo extra (unidades) permitido por encima del tile mas alto del nivel.")]
    [SerializeField] private float extraTop = 2f;

    private Vector3 offset;
    private Vector2 velocity;
    private float currentLookAhead;
    private float lookAheadVelocity;
    private float targetY;
    private float currentLookDown;
    private float lookDownVelocity;
    private Rigidbody2D body;
    private Vector3 basePos;

    // Temblor de camara (golpes, dano). Se suma a la posicion base sin afectar al suavizado.
    private static SmoothCameraFollow activa;
    private float temblorHasta, temblorDuracion, temblorFuerza;
    public static void Sacudir(float fuerza, float duracion)
    {
        if (activa == null) return;
        if (Time.unscaledTime + duracion > activa.temblorHasta || fuerza > activa.temblorFuerza)
        {
            activa.temblorFuerza = fuerza; activa.temblorDuracion = Mathf.Max(0.01f, duracion);
            activa.temblorHasta = Time.unscaledTime + duracion;
        }
    }
    private Vector3 Temblor()
    {
        float r = temblorHasta - Time.unscaledTime;
        if (r <= 0f) return Vector3.zero;
        float k = temblorFuerza * (r / temblorDuracion);
        // Desplazamiento redondeado a pixeles para no emborronar el pixel art.
        Vector2 o = Random.insideUnitCircle * k;
        return new Vector3(Mathf.Round(o.x * 10f) / 10f, Mathf.Round(o.y * 10f) / 10f, 0f);
    }
    private void OnEnable() { activa = this; }
    private void OnDisable() { if (activa == this) activa = null; }
    private bool hasBounds;
    private Rect levelRect;

    private void Start()
    {
        if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null)
        {
            enabled = false;
            return;
        }

        body = GetComponent<Rigidbody2D>();
        Transform camT = targetCamera.transform;
        offset = camT.position - transform.position;

        // Se separa del jugador conservando su posicion actual.
        if (camT.IsChildOf(transform)) camT.SetParent(null, true);

        if (useLevelBounds) hasBounds = ComputeLevelBounds(out levelRect);
        targetY = transform.position.y + offset.y;

        // Encuadre inicial ya dentro de los limites (sin "viaje" de la camara al empezar).
        Vector3 start = ClampToBounds(new Vector3(transform.position.x + offset.x, targetY, camT.position.z));
        camT.position = start;
        basePos = start;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;
        Transform camT = targetCamera.transform;

        float vx = body != null ? body.velocity.x : 0f;
        float vy = body != null ? body.velocity.y : 0f;
        float desiredLook = Mathf.Abs(vx) > 0.5f ? Mathf.Sign(vx) * lookAhead : currentLookAhead;
        currentLookAhead = Mathf.SmoothDamp(currentLookAhead, desiredLook, ref lookAheadVelocity, lookAheadSmooth);

        // --- Zona muerta vertical ---
        float playerY = transform.position.y + offset.y;
        if (playerY > targetY + verticalDeadZone) targetY = playerY - verticalDeadZone;
        else if (playerY < targetY - verticalDeadZone) targetY = playerY + verticalDeadZone;

        // Mirada hacia abajo en caidas (suavizada para que no sea brusca).
        float desiredLookDown = vy < -fallLookDownSpeed ? -fallLookDown : 0f;
        currentLookDown = Mathf.SmoothDamp(currentLookDown, desiredLookDown, ref lookDownVelocity, 0.3f);

        Vector3 target = new Vector3(transform.position.x + offset.x + currentLookAhead, targetY + currentLookDown, camT.position.z);
        target = ClampToBounds(target);
        Vector3 pos = basePos;

        if (Vector2.Distance(pos, target) > snapDistance)
        {
            velocity = Vector2.zero;
            targetY = playerY;
            basePos = ClampToBounds(new Vector3(transform.position.x + offset.x, playerY, pos.z));
            camT.position = basePos + Temblor();
            return;
        }

        // Caida rapida o jugador cerca del borde inferior/superior -> seguimiento vertical rapido.
        float halfH = targetCamera.orthographicSize;
        bool nearEdge = Mathf.Abs(transform.position.y - pos.y) > halfH * 0.55f;
        float ySmooth = (vy < -6f || nearEdge) ? fastSmoothTimeY : smoothTimeY;

        float x = Mathf.SmoothDamp(pos.x, target.x, ref velocity.x, smoothTimeX);
        float y = Mathf.SmoothDamp(pos.y, target.y, ref velocity.y, ySmooth);
        basePos = ClampToBounds(new Vector3(x, y, pos.z));
        camT.position = basePos + Temblor();
    }

    private Vector3 ClampToBounds(Vector3 p)
    {
        if (!hasBounds) return p;
        float halfH = targetCamera.orthographicSize;
        float halfW = halfH * targetCamera.aspect;

        float minX = levelRect.xMin + halfW, maxX = levelRect.xMax - halfW;
        float minY = levelRect.yMin + halfH, maxY = levelRect.yMax - halfH;
        p.x = minX > maxX ? levelRect.center.x : Mathf.Clamp(p.x, minX, maxX);
        p.y = minY > maxY ? levelRect.center.y : Mathf.Clamp(p.y, minY, maxY);
        return p;
    }

    // Rectangulo del nivel: union de los Tilemaps con tag "Ground" (el suelo jugable).
    private bool ComputeLevelBounds(out Rect rect)
    {
        rect = default;
        bool found = false;
        Bounds total = default;
        foreach (Tilemap tm in FindObjectsOfType<Tilemap>())
        {
            if (!tm.CompareTag("Ground")) continue;
            tm.CompressBounds();
            if (tm.cellBounds.size.x <= 0 || tm.cellBounds.size.y <= 0) continue;
            Bounds lb = tm.localBounds;
            Vector3 a = tm.transform.TransformPoint(lb.min);
            Vector3 b = tm.transform.TransformPoint(lb.max);
            Bounds wb = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 1f));
            if (!found) { total = wb; found = true; }
            else total.Encapsulate(wb);
        }
        if (!found) return false;
        rect = Rect.MinMaxRect(total.min.x - boundsPadding, total.min.y - boundsPadding,
                               total.max.x + boundsPadding, total.max.y + boundsPadding + extraTop);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hasBounds) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(levelRect.center, levelRect.size);
    }
}
