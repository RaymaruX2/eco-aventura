using UnityEngine;

// Rana: ataque SALTANDO.
// Espera en su sitio y, cuando el jugador entra en su zona, salta en arco hacia el. Entre saltos
// descansa un momento (es la ventana para pisarla o atacarla). Nunca salta hacia un vacio ni
// sale de su territorio. Usa fisica (Rigidbody2D) para que los saltos sean naturales.
//
// Animator (Anims/Enemigos/Rana.controller): grounded (bool), vSpeed (float), enemyDeath (trigger).
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class RanaSaltarina : MonoBehaviour
{
    [SerializeField] private float distanciaDeteccion = 7f;
    [SerializeField] private float toleranciaAltura = 3f;
    [SerializeField] private float rangoTerritorio = 5f;
    [SerializeField] private float fuerzaSalto = 7.5f;
    [SerializeField] private float velocidadHorizontal = 3.2f;
    [Tooltip("Descanso entre saltos (s).")]
    [SerializeField] private Vector2 descanso = new Vector2(1.0f, 1.6f);
    [Tooltip("Sin jugador cerca, da pequenos saltos en su sitio cada cierto tiempo.")]
    [SerializeField] private bool saltitosEnReposo = true;
    [SerializeField] private bool spriteMiraIzquierda = true;

    private Enemy enemy;
    private Animator anim;
    private Rigidbody2D rb;
    private Collider2D[] propios;
    private Transform jugador;
    private float inicioX;
    private float medioAncho, pies;
    private float proximoSalto;
    private bool enSuelo;
    private bool aterrizajePendiente;
    private bool vioJugador;

    private static readonly int GroundedHash = Animator.StringToHash("grounded");
    private static readonly int VSpeedHash = Animator.StringToHash("vSpeed");

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemy.AnimacionAtaqueExterna = true;
        propios = GetComponentsInChildren<Collider2D>();
    }

    private void Start()
    {
        inicioX = transform.position.x;
        EnemigoUtil.MedirCuerpo(transform, propios, out medioAncho, out pies);
        jugador = EnemigoUtil.BuscarJugador();
        rb.freezeRotation = true;
        proximoSalto = Time.time + Random.Range(descanso.x, descanso.y);
    }

    private void Update()
    {
        if (enemy.IsDead) return;
        Vector2 pos = transform.position;
        enSuelo = rb.velocity.y <= 0.05f && EnemigoUtil.HaySuelo(new Vector2(pos.x, pos.y - pies + 0.1f), 0.2f, propios);

        if (anim != null)
        {
            anim.SetBool(GroundedHash, enSuelo);
            anim.SetFloat(VSpeedHash, rb.velocity.y);
        }

        if (enSuelo && aterrizajePendiente) { aterrizajePendiente = false; enemy.Sonar("aterrizaje", 0.8f); }
        if (!enSuelo || enemy.IsStunned) return;
        // En el suelo no resbala.
        rb.velocity = new Vector2(0f, rb.velocity.y);
        if (Time.time < proximoSalto) return;

        float dirSalto = 0f;
        if (jugador != null && !Death.isDeath)
        {
            Vector2 d = jugador.position - transform.position;
            if (Mathf.Abs(d.x) <= distanciaDeteccion && Mathf.Abs(d.y) <= toleranciaAltura)
            {
                dirSalto = Mathf.Sign(d.x);
                Mirar(dirSalto);
                if (!vioJugador) { vioJugador = true; enemy.Sonar("alerta"); }
            }
        }

        if (dirSalto == 0f && !saltitosEnReposo) { proximoSalto = Time.time + descanso.y; return; }

        if (dirSalto == 0f) vioJugador = false;
        float vx = dirSalto * velocidadHorizontal;
        float fuerza = dirSalto == 0f ? fuerzaSalto * 0.45f : fuerzaSalto;
        // No saltar fuera de su territorio ni hacia un vacio.
        if (dirSalto != 0f)
        {
            float aterrizaje = pos.x + vx * TiempoDeVuelo(fuerza);
            bool fuera = Mathf.Abs(aterrizaje - inicioX) > rangoTerritorio;
            bool vacio = !EnemigoUtil.HaySuelo(new Vector2(aterrizaje, pos.y + 1f), 4f, propios);
            if (fuera || vacio) { vx = 0f; fuerza = fuerzaSalto * 0.45f; }
        }

        rb.velocity = new Vector2(vx, fuerza);
        enemy.Sonar("salto", dirSalto == 0f ? 0.5f : 1f);
        aterrizajePendiente = true;
        proximoSalto = Time.time + Random.Range(descanso.x, descanso.y) + TiempoDeVuelo(fuerza);
    }

    private float TiempoDeVuelo(float v0)
    {
        float g = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        return g > 0.01f ? 2f * v0 / g : 1f;
    }

    private void Mirar(float dir)
    {
        bool mirarDerecha = dir > 0f;
        transform.rotation = Quaternion.Euler(0f, (mirarDerecha == spriteMiraIzquierda) ? 180f : 0f, 0f);
    }
}
