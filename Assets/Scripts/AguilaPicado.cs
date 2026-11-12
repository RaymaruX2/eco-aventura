using UnityEngine;

// Aguila: ataque EN PICADO desde el aire (el enemigo mas exigente).
//  Patrulla : vuela de lado a lado a su altura, con un ligero balanceo.
//  Aviso    : al ver al jugador por debajo se detiene y aletea rapido un instante (se ve venir).
//  Picado   : se lanza en linea recta hacia donde estaba el jugador.
//  Regreso  : vuelve a su altura de patrulla mas despacio (ventana para contraatacar o pisarla).
// Como la abeja, no usa fisica: se mueve por transform y su collider hace dano al tocarla.
[RequireComponent(typeof(Enemy))]
[DisallowMultipleComponent]
public class AguilaPicado : MonoBehaviour
{
    [Header("Patrulla")]
    [SerializeField] private float rangoPatrulla = 4f;
    [SerializeField] private float velocidadPatrulla = 2f;
    [SerializeField] private float balanceo = 0.25f;

    [Header("Picado")]
    [Tooltip("Ancho de la zona (a cada lado) bajo el aguila en la que detecta al jugador.")]
    [SerializeField] private float anchoDeteccion = 3.5f;
    [SerializeField] private float profundidadDeteccion = 8f;
    [SerializeField] private float tiempoAviso = 0.45f;
    [SerializeField] private float velocidadPicado = 9f;
    [SerializeField] private float velocidadRegreso = 3f;
    [SerializeField] private float enfriamiento = 1.2f;
    [SerializeField] private bool spriteMiraIzquierda = true;

    private enum Estado { Patrulla, Aviso, Picado, Regreso }
    private Estado estado = Estado.Patrulla;

    private Enemy enemy;
    private Animator anim;
    private Collider2D[] propios;
    private Transform jugador;
    private Vector2 casa;
    private Vector2 objetivo;
    private float tiempoEstado;
    private float fase;
    private int dir = -1;
    private float proximoPicado;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        anim = GetComponent<Animator>();
        enemy.AnimacionAtaqueExterna = true;
        propios = GetComponentsInChildren<Collider2D>();
    }

    private void Start()
    {
        casa = transform.position;
        jugador = EnemigoUtil.BuscarJugador();
        fase = Random.Range(0f, 10f);
    }

    private void Update()
    {
        if (enemy.IsDead) return;
        if (enemy.IsStunned) { if (estado == Estado.Picado) Cambiar(Estado.Regreso); return; }

        Vector2 pos = transform.position;
        switch (estado)
        {
            case Estado.Patrulla:
            {
                if (anim != null) anim.speed = 1f;
                fase += Time.deltaTime;
                float x = pos.x + dir * velocidadPatrulla * Time.deltaTime;
                if (Mathf.Abs(x - casa.x) > rangoPatrulla) { dir = -dir; x = pos.x; }
                float y = Mathf.MoveTowards(pos.y, casa.y + Mathf.Sin(fase * 2.5f) * balanceo, 2f * Time.deltaTime);
                Mover(new Vector2(x, y), dir);
                if (Time.time >= proximoPicado && JugadorDebajo(out Vector2 p))
                {
                    objetivo = p;
                    Cambiar(Estado.Aviso);
                    enemy.Sonar("alerta");
                }
                break;
            }
            case Estado.Aviso:
                // Aleteo rapido y un pequeno retroceso hacia arriba: anuncia el picado.
                if (anim != null) anim.speed = 2.5f;
                transform.position = pos + Vector2.up * 0.6f * Time.deltaTime;
                Mirar(Mathf.Sign(objetivo.x - pos.x));
                if (Time.time - tiempoEstado >= tiempoAviso) { Cambiar(Estado.Picado); enemy.Sonar("ataque"); }
                break;

            case Estado.Picado:
            {
                if (anim != null) anim.speed = 1.6f;
                Vector2 d = objetivo - pos;
                Vector2 paso = d.normalized * velocidadPicado * Time.deltaTime;
                bool choca = EnemigoUtil.HayPared(pos, Mathf.Sign(d.x), 0.6f, propios)
                             || EnemigoUtil.HaySuelo(pos, 1.0f, propios) && d.y < 0f;
                if (paso.magnitude >= d.magnitude || choca || Time.time - tiempoEstado > 2f) Cambiar(Estado.Regreso);
                else transform.position = pos + paso;
                break;
            }
            case Estado.Regreso:
            {
                if (anim != null) anim.speed = 1.2f;
                Vector2 meta = new Vector2(Mathf.Clamp(pos.x, casa.x - rangoPatrulla, casa.x + rangoPatrulla), casa.y);
                Vector2 nueva = Vector2.MoveTowards(pos, meta, velocidadRegreso * Time.deltaTime);
                Mover(nueva, Mathf.Sign(meta.x - pos.x));
                if (Vector2.Distance(nueva, meta) < 0.05f) { proximoPicado = Time.time + enfriamiento; Cambiar(Estado.Patrulla); }
                break;
            }
        }
    }

    private bool JugadorDebajo(out Vector2 p)
    {
        p = default;
        if (jugador == null || Death.isDeath) return false;
        p = jugador.position;
        Vector2 d = p - (Vector2)transform.position;
        return d.y < -0.5f && d.y > -profundidadDeteccion && Mathf.Abs(d.x) <= anchoDeteccion;
    }

    private void Cambiar(Estado e) { estado = e; tiempoEstado = Time.time; }

    private void Mover(Vector2 nueva, float direccion)
    {
        transform.position = new Vector3(nueva.x, nueva.y, transform.position.z);
        if (Mathf.Abs(direccion) > 0.01f) Mirar(direccion);
    }

    private void Mirar(float direccion)
    {
        if (Mathf.Abs(direccion) < 0.01f) return;
        bool mirarDerecha = direccion > 0f;
        transform.rotation = Quaternion.Euler(0f, (mirarDerecha == spriteMiraIzquierda) ? 180f : 0f, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? (Vector3)casa : transform.position;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawLine(c + Vector3.left * rangoPatrulla, c + Vector3.right * rangoPatrulla);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireCube(transform.position + Vector3.down * profundidadDeteccion * 0.5f, new Vector3(anchoDeteccion * 2f, profundidadDeteccion, 0.1f));
    }
}
