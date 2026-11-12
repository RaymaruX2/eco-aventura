using UnityEngine;

// Zarigueya: patrulla RAPIDA de borde a borde.
// Recorre su plataforma corriendo y se da la vuelta al llegar a un borde o a una pared.
// No persigue: es un obstaculo movil que obliga a medir el salto (pasar por encima o pisarla).
// Si ve al jugador delante, acelera un poco (embiste) durante un instante.
[RequireComponent(typeof(Enemy))]
[DisallowMultipleComponent]
public class ZarigueyaPatrulla : MonoBehaviour
{
    [SerializeField] private float velocidad = 3.2f;
    [Tooltip("Multiplicador de velocidad cuando ve al jugador delante.")]
    [SerializeField] private float aceleracionAlVer = 1.4f;
    [SerializeField] private float distanciaVision = 5f;
    [Tooltip("Pausa al darse la vuelta (s).")]
    [SerializeField] private float pausaGiro = 0.25f;
    [Tooltip("El sprite original mira a la izquierda.")]
    [SerializeField] private bool spriteMiraIzquierda = true;
    [SerializeField] private bool empiezaHaciaLaDerecha = false;

    private Enemy enemy;
    private Animator anim;
    private Collider2D[] propios;
    private Transform jugador;
    private float medioAncho, pies;
    private int dir;
    private float pausaHasta;
    private bool viendo;
    private float proximoPaso;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        anim = GetComponent<Animator>();
        enemy.AnimacionAtaqueExterna = true;
        propios = GetComponentsInChildren<Collider2D>();
    }

    private void Start()
    {
        EnemigoUtil.MedirCuerpo(transform, propios, out medioAncho, out pies);
        jugador = EnemigoUtil.BuscarJugador();
        Girar(empiezaHaciaLaDerecha ? 1 : -1);
    }

    private void Update()
    {
        if (enemy.IsDead) return;
        bool quieto = enemy.IsStunned || Time.time < pausaHasta;
        if (anim != null) anim.speed = quieto ? 0.4f : 1f;
        if (quieto) return;

        float v = velocidad;
        if (jugador != null && !Death.isDeath)
        {
            Vector2 d = jugador.position - transform.position;
            bool ve = Mathf.Sign(d.x) == dir && Mathf.Abs(d.x) < distanciaVision && Mathf.Abs(d.y) < 1.5f;
            if (ve) { v *= aceleracionAlVer; if (!viendo) enemy.Sonar("alerta"); }
            viendo = ve;
        }

        Vector2 pos = transform.position;
        Vector2 pie = new Vector2(pos.x + dir * (medioAncho + 0.1f), pos.y - pies + 0.3f);
        Vector2 cuerpo = new Vector2(pos.x, pos.y - pies + 0.4f);
        if (!EnemigoUtil.HaySuelo(pie, 0.7f, propios) || EnemigoUtil.HayPared(cuerpo, dir, medioAncho + 0.1f, propios))
        {
            Girar(-dir);
            pausaHasta = Time.time + pausaGiro;
            return;
        }
        transform.position += new Vector3(dir * v * Time.deltaTime, 0f, 0f);
        if (Time.time >= proximoPaso) { enemy.Sonar("pasos", 0.5f); proximoPaso = Time.time + 0.16f; }
    }

    private void Girar(int nuevaDir)
    {
        dir = nuevaDir;
        bool mirarDerecha = dir > 0;
        // Rotacion en Y: los colliders giran con el sprite.
        transform.rotation = Quaternion.Euler(0f, (mirarDerecha == spriteMiraIzquierda) ? 180f : 0f, 0f);
    }
}
