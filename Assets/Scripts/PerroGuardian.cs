using UnityEngine;

// Perro guardian: vigila su zona y ataca al jugador a distancia corta.
//
//  Vigilancia : recorre su zona caminando (posicion inicial +- rangoGuardia), con pausas en las
//               que se gira a mirar alrededor.
//  Alerta     : si ve al jugador (distancia y altura parecidas) se gira hacia el y corre a por el,
//               sin salir de su territorio (rangoTerritorio) ni caerse por los bordes.
//  Ataque     : con el jugador a su alcance se agacha (se ve venir), se lanza hacia delante y muerde.
//               El mordisco golpea en una zona delante del hocico, sin tener que tocarlo.
//  Aturdido   : tras recibir un zarpazo (Enemy.IsStunned) se detiene un momento.
//
// Animator (Anims/Perro/Perro.controller): parametros speed (float), enemyAttack (bool) y
// enemyDeath (trigger, lo activa Enemy). Reposo/Caminar/Correr dependen de la velocidad real.
// El mismo componente sirve para el perro alfa cambiando sus valores (mas rapido, mas alcance).
// La orientacion se hace girando el objeto para que los colliders tambien giren.
[RequireComponent(typeof(Enemy))]
[DisallowMultipleComponent]
public class PerroGuardian : MonoBehaviour
{
    [Header("Vigilancia")]
    [SerializeField] private float rangoGuardia = 2.5f;
    [SerializeField] private float velocidadPaseo = 1.0f;
    [SerializeField] private Vector2 pausaVigilancia = new Vector2(0.8f, 2.0f);

    [Header("Deteccion")]
    [SerializeField] private float distanciaDeteccion = 6f;
    [SerializeField] private float toleranciaAltura = 1.8f;
    [Tooltip("Distancia maxima que se aleja de su posicion inicial al perseguir.")]
    [SerializeField] private float rangoTerritorio = 4.5f;
    [SerializeField] private float velocidadAlerta = 2.4f;

    [Header("Ataque")]
    [Tooltip("Alcance del mordisco medido desde el centro del perro.")]
    [SerializeField] private float alcanceAtaque = 2.2f;
    [SerializeField] private float altoZonaAtaque = 1.3f;
    [Tooltip("Tiempo (s) que el perro se agacha antes de lanzarse (se ve venir el ataque).")]
    [SerializeField] private float preparacionAtaque = 0.15f;
    [Tooltip("Momento del ataque (s) en que el mordisco hace dano.")]
    [SerializeField] private float momentoMordisco = 0.25f;
    [Tooltip("Duracion total del ataque (igual a la animacion de ataque).")]
    [SerializeField] private float duracionAtaque = 0.5f;
    [SerializeField] private float enfriamientoAtaque = 1.1f;
    [SerializeField] private float distanciaEmbestida = 0.8f;

    [Header("Sprite")]
    [Tooltip("El dibujo original del perro mira a la derecha.")]
    [SerializeField] private bool spriteMiraDerecha = true;

    private enum Estado { Vigilancia, Alerta, Atacando }
    private Estado estado = Estado.Vigilancia;

    private Enemy enemy;
    private Animator anim;
    private SpriteRenderer sr;
    private Transform jugador;
    private Death muerteJugador;
    private Collider2D[] propios;
    private float inicioX;
    private float destinoPaseoX;
    private float esperaHasta;
    private float proximoAtaque;
    private float inicioAtaque;
    private bool mordiscoHecho;
    private int orientacionBase;     // 1 = el sprite (con su flipX) mira a la derecha
    private int mirando = 1;
    private float halfBody = 0.7f;
    private float piesOffset = 0.8f;
    private float velocidadActual;
    private float proximoPaso;

    private static readonly int EnemyAttackHash = Animator.StringToHash("enemyAttack");
    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    private readonly Collider2D[] overlap = new Collider2D[8];

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        enemy.AnimacionAtaqueExterna = true;
        propios = GetComponentsInChildren<Collider2D>();
    }

    private void Start()
    {
        inicioX = transform.position.x;
        destinoPaseoX = inicioX;
        esperaHasta = Time.time + Random.Range(pausaVigilancia.x, pausaVigilancia.y);

        bool flip = sr != null && sr.flipX;
        orientacionBase = (spriteMiraDerecha ? 1 : -1) * (flip ? -1 : 1);
        mirando = orientacionBase;

        // Medidas del cuerpo (para comprobar suelo y paredes delante)
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool init = false;
        foreach (Collider2D c in propios)
        {
            if (c.isTrigger || !c.enabled) continue;
            if (!init) { b = c.bounds; init = true; } else b.Encapsulate(c.bounds);
        }
        if (init)
        {
            halfBody = Mathf.Max(0.4f, Mathf.Min(b.extents.x, 1.0f));
            piesOffset = transform.position.y - b.min.y;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { jugador = p.transform; muerteJugador = p.GetComponent<Death>(); }
    }

    private void OnDisable()
    {
        if (anim != null) { anim.SetBool(EnemyAttackHash, false); anim.SetFloat(SpeedHash, 0f); }
    }

    private void Update()
    {
        if (enemy.IsDead) return;
        velocidadActual = 0f;
        Pensar();
        // La animacion (reposo / caminar / correr) sigue a la velocidad real del perro.
        anim.SetFloat(SpeedHash, velocidadActual);
    }

    private void Pensar()
    {
        if (enemy.IsStunned)
        {
            if (estado == Estado.Atacando) TerminarAtaque();
            return;
        }

        bool veJugador = VeAlJugador(out float dx);

        switch (estado)
        {
            case Estado.Atacando:
                ActualizarAtaque();
                return;

            case Estado.Alerta:
                if (!veJugador) { estado = Estado.Vigilancia; destinoPaseoX = transform.position.x; return; }
                Orientar(Mathf.Sign(dx));
                if (Mathf.Abs(dx) <= alcanceAtaque && Time.time >= proximoAtaque) { EmpezarAtaque(); return; }
                // Corre hasta quedar a distancia de mordisco, sin salir de su territorio.
                if (Mathf.Abs(dx) > alcanceAtaque * 0.8f) Mover(Mathf.Sign(dx), velocidadAlerta, true);
                return;

            default:
                if (veJugador) { estado = Estado.Alerta; enemy.Sonar("alerta"); return; }
                Vigilar();
                return;
        }
    }

    private void Vigilar()
    {
        if (Time.time < esperaHasta) return;

        float dx = destinoPaseoX - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f)
        {
            // Llega a un punto: pausa mirando alrededor y elige el siguiente.
            esperaHasta = Time.time + Random.Range(pausaVigilancia.x, pausaVigilancia.y);
            destinoPaseoX = inicioX + Random.Range(-rangoGuardia, rangoGuardia);
            if (Random.value < 0.5f) Orientar(-mirando);
            return;
        }
        Orientar(Mathf.Sign(dx));
        if (!Mover(Mathf.Sign(dx), velocidadPaseo, false))
        {
            // Borde o pared: se da la vuelta.
            destinoPaseoX = inicioX - Mathf.Sign(dx) * rangoGuardia * 0.5f;
            esperaHasta = Time.time + pausaVigilancia.x;
        }
    }

    private bool VeAlJugador(out float dx)
    {
        dx = 0f;
        if (jugador == null || Death.isDeath) return false;
        Vector2 d = jugador.position - transform.position;
        dx = d.x;
        if (Mathf.Abs(d.x) > distanciaDeteccion || Mathf.Abs(d.y) > toleranciaAltura) return false;
        // Solo defiende su territorio (con un margen de alcance).
        return Mathf.Abs(jugador.position.x - inicioX) <= rangoTerritorio + alcanceAtaque;
    }

    // Mueve al perro en horizontal si hay suelo delante y no hay pared. Devuelve false si no puede.
    private bool Mover(float dir, float velocidad, bool respetarTerritorio)
    {
        float nuevaX = transform.position.x + dir * velocidad * Time.deltaTime;
        if (Mathf.Abs(nuevaX - inicioX) > (respetarTerritorio ? rangoTerritorio : rangoGuardia + 0.01f)) return false;
        if (!HaySueloDelante(dir) || HayParedDelante(dir)) return false;
        velocidadActual = velocidad;
        if (velocidad > 1.8f && Time.time >= proximoPaso) { enemy.Sonar("pasos", 0.6f); proximoPaso = Time.time + 0.2f; }
        transform.position = new Vector3(nuevaX, transform.position.y, transform.position.z);
        return true;
    }

    private bool HaySueloDelante(float dir)
    {
        Vector2 origen = new Vector2(transform.position.x + dir * (halfBody + 0.15f), transform.position.y - piesOffset + 0.25f);
        int n = Physics2D.RaycastNonAlloc(origen, Vector2.down, hits, 0.6f);
        for (int i = 0; i < n; i++) if (EsSolido(hits[i].collider)) return true;
        return false;
    }

    private bool HayParedDelante(float dir)
    {
        Vector2 origen = new Vector2(transform.position.x, transform.position.y - piesOffset + 0.5f);
        int n = Physics2D.RaycastNonAlloc(origen, Vector2.right * dir, hits, halfBody + 0.15f);
        for (int i = 0; i < n; i++) if (EsSolido(hits[i].collider)) return true;
        return false;
    }

    private bool EsSolido(Collider2D c)
    {
        if (c == null || c.isTrigger || c.CompareTag("Player") || c.CompareTag("Enemy")) return false;
        foreach (Collider2D p in propios) if (p == c) return false;
        return true;
    }

    private void Orientar(float dir)
    {
        int d = dir >= 0f ? 1 : -1;
        if (d == mirando) return;
        mirando = d;
        transform.rotation = Quaternion.Euler(0f, d == orientacionBase ? 0f : 180f, 0f);
    }

    // ---------------------------------------------------------------- ataque
    private void EmpezarAtaque()
    {
        estado = Estado.Atacando;
        inicioAtaque = Time.time;
        mordiscoHecho = false;
        anim.SetBool(EnemyAttackHash, true);
        enemy.PlayAttackSound();
    }

    private void ActualizarAtaque()
    {
        float t = Time.time - inicioAtaque;
        float finEmbestida = momentoMordisco + 0.1f;

        // Primero se agacha (preparacion) y despues se lanza hacia delante durante el mordisco.
        if (t >= preparacionAtaque && t < finEmbestida && HaySueloDelante(mirando) && !HayParedDelante(mirando)
            && Mathf.Abs(transform.position.x - inicioX) < rangoTerritorio)
        {
            float v = distanciaEmbestida / Mathf.Max(0.01f, finEmbestida - preparacionAtaque);
            transform.position += new Vector3(mirando * v * Time.deltaTime, 0f, 0f);
        }

        if (!mordiscoHecho && t >= momentoMordisco)
        {
            mordiscoHecho = true;
            Morder();
        }

        if (t >= duracionAtaque) TerminarAtaque();
    }

    private void Morder()
    {
        if (muerteJugador == null) return;
        Vector2 centro = (Vector2)transform.position + new Vector2(mirando * alcanceAtaque * 0.5f, 0f);
        int n = Physics2D.OverlapBoxNonAlloc(centro, new Vector2(alcanceAtaque, altoZonaAtaque), 0f, overlap);
        for (int i = 0; i < n; i++)
        {
            if (overlap[i] != null && overlap[i].CompareTag("Player"))
            {
                muerteJugador.RecibirDanio();
                return;
            }
        }
    }

    private void TerminarAtaque()
    {
        anim.SetBool(EnemyAttackHash, false);
        proximoAtaque = Time.time + enfriamientoAtaque;
        estado = Estado.Alerta;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? new Vector3(inicioX, transform.position.y, 0f) : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawLine(c + Vector3.left * rangoGuardia, c + Vector3.right * rangoGuardia);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
        int dir = Application.isPlaying ? mirando : 1;
        Gizmos.DrawWireCube(transform.position + new Vector3(dir * alcanceAtaque * 0.5f, 0f, 0f), new Vector3(alcanceAtaque, altoZonaAtaque, 0.1f));
    }
}
