using UnityEngine;

// Efectos visuales de particulas en pixel art (cuadrados del mismo tamano que un pixel del gato).
// Un unico gestor persistente con un pool fijo: sin crear ni destruir objetos durante el juego.
//   EfectosFX.Polvo(pos, n)            polvo al saltar / aterrizar / correr
//   EfectosFX.Chispas(pos, color)      chispas al golpear
//   EfectosFX.Estallido(pos, color)    enemigo derrotado
//   EfectosFX.Sacudir(fuerza, tiempo)  temblor de camara
//   EfectosFX.Ambiente(tipo, rect)     particulas ambientales de la zona (1 hojas, 2 luciernagas, 3 viento)
[DefaultExecutionOrder(-150)]
public class EfectosFX : MonoBehaviour
{
    private static EfectosFX inst;
    private const int Max = 260;
    private const float Pixel = 0.1f;

    private struct P
    {
        public bool viva; public Vector2 pos, vel, grav; public float vida, vidaMax, fase, tam;
        public Color color; public int tipo;   // 0 normal, 1 hoja, 2 luciernaga, 3 viento
    }
    private readonly P[] ps = new P[Max];
    private SpriteRenderer[] srs;
    private Sprite pixel;

    // ambiente
    private int tipoAmbiente;
    private float acumulador;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Crear()
    {
        if (inst != null) return;
        GameObject go = new GameObject("EfectosFX");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<EfectosFX>();
    }

    private void Awake()
    {
        Texture2D tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); tex.Apply();
        pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f / Pixel);
        srs = new SpriteRenderer[Max];
        for (int i = 0; i < Max; i++)
        {
            GameObject g = new GameObject("p");
            g.transform.SetParent(transform, false);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = pixel; sr.sortingLayerName = "Player"; sr.sortingOrder = 5; sr.enabled = false;
            srs[i] = sr;
        }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { Limpiar(); tipoAmbiente = 0; };
    }

    private void Limpiar()
    {
        for (int i = 0; i < Max; i++) { ps[i].viva = false; srs[i].enabled = false; }
    }

    private int Libre(bool ambiente)
    {
        // Las particulas ambientales solo usan la mitad del pool: los efectos de juego siempre tienen sitio.
        int desde = ambiente ? Max / 2 : 0, hasta = ambiente ? Max : Max / 2;
        for (int i = desde; i < hasta; i++) if (!ps[i].viva) return i;
        return -1;
    }

    private static void Emitir(Vector2 pos, Vector2 vel, Vector2 grav, float vida, Color c, float tam, int tipo = 0)
    {
        if (inst == null) return;
        int i = inst.Libre(tipo != 0);
        if (i < 0) return;
        inst.ps[i] = new P { viva = true, pos = pos, vel = vel, grav = grav, vida = vida, vidaMax = vida, color = c, tam = tam, tipo = tipo, fase = Random.value * 10f };
        SpriteRenderer sr = inst.srs[i];
        sr.enabled = true; sr.color = c; sr.sortingLayerName = tipo == 0 ? "Player" : "Midground"; sr.sortingOrder = tipo == 0 ? 5 : 6;
        sr.transform.localScale = Vector3.one * tam;
        sr.transform.position = pos;
    }

    // ------------------------------------------------------------------ API
    public static void Polvo(Vector2 pos, int n = 6, float dirX = 0f)
    {
        for (int k = 0; k < n; k++)
        {
            float lado = k % 2 == 0 ? -1f : 1f;
            Vector2 v = new Vector2(lado * Random.Range(0.6f, 1.8f) + dirX * 0.8f, Random.Range(0.2f, 1.1f));
            Color c = Color.Lerp(new Color(0.78f, 0.66f, 0.52f, 0.9f), new Color(0.92f, 0.86f, 0.74f, 0.9f), Random.value);
            Emitir(pos + new Vector2(Random.Range(-0.3f, 0.3f), 0.05f), v, new Vector2(0, -1.2f), Random.Range(0.25f, 0.45f), c, Random.value < 0.3f ? 2f : 1f);
        }
    }

    public static void Chispas(Vector2 pos, Color color, int n = 8)
    {
        for (int k = 0; k < n; k++)
        {
            Vector2 v = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f);
            Emitir(pos, v, new Vector2(0, -6f), Random.Range(0.15f, 0.3f), Color.Lerp(color, Color.white, Random.value * 0.5f), 1f);
        }
    }

    public static void Estallido(Vector2 pos, Color color)
    {
        Chispas(pos, color, 14);
        for (int k = 0; k < 8; k++)
            Emitir(pos + Random.insideUnitCircle * 0.3f, Random.insideUnitCircle * 1.2f + Vector2.up * 0.8f, Vector2.zero,
                   Random.Range(0.35f, 0.6f), new Color(1f, 1f, 1f, 0.8f), 2f);
    }

    public static void Sacudir(float fuerza, float tiempo)
    {
        SmoothCameraFollow.Sacudir(fuerza, tiempo);
    }

    public static void Ambiente(int tipo) { if (inst != null) inst.tipoAmbiente = tipo; }

    // ------------------------------------------------------------------ simulacion
    private void Update()
    {
        float dt = Time.deltaTime;
        EmitirAmbiente(dt);
        for (int i = 0; i < Max; i++)
        {
            if (!ps[i].viva) continue;
            P p = ps[i];
            p.vida -= dt;
            if (p.vida <= 0f) { p.viva = false; srs[i].enabled = false; ps[i] = p; continue; }
            float f = p.vida / p.vidaMax;
            switch (p.tipo)
            {
                case 1: p.vel.x = Mathf.Sin((Time.time + p.fase) * 2.2f) * 0.8f + 0.3f; break;           // hoja que se balancea
                case 2: p.vel = new Vector2(Mathf.Sin((Time.time + p.fase) * 1.3f), Mathf.Cos((Time.time + p.fase) * 1.7f)) * 0.35f; break; // luciernaga
            }
            p.vel += p.grav * dt;
            p.pos += p.vel * dt;
            Color c = p.color;
            if (p.tipo == 2) c.a = p.color.a * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin((Time.time + p.fase) * 3f)));
            c.a *= Mathf.Clamp01(f * 3f) * Mathf.Clamp01((1f - f) * 6f + (p.tipo == 0 ? 1f : 0f));
            srs[i].color = c;
            srs[i].transform.position = p.pos;
            ps[i] = p;
        }
    }

    private void EmitirAmbiente(float dt)
    {
        if (tipoAmbiente == 0) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        float ritmo = tipoAmbiente == 1 ? 2.5f : tipoAmbiente == 2 ? 4f : 6f;   // particulas por segundo
        acumulador += dt * ritmo;
        while (acumulador >= 1f)
        {
            acumulador -= 1f;
            switch (tipoAmbiente)
            {
                case 1: // hojas cayendo desde arriba
                    Emitir(new Vector2(c.x + Random.Range(-w, w), c.y + h + 0.5f), new Vector2(0.3f, -0.7f), Vector2.zero, 9f,
                           Random.value < 0.5f ? new Color(0.45f, 0.72f, 0.28f, 0.9f) : new Color(0.86f, 0.62f, 0.25f, 0.9f), Random.value < 0.4f ? 2f : 1f, 1);
                    break;
                case 2: // luciernagas que flotan
                    Emitir(new Vector2(c.x + Random.Range(-w, w), c.y + Random.Range(-h, h)), Vector2.zero, Vector2.zero, Random.Range(3f, 6f),
                           new Color(1f, 0.92f, 0.45f, 0.95f), 1f, 2);
                    break;
                case 3: // viento: motas de polen arrastradas en horizontal
                    Emitir(new Vector2(c.x - w - 0.5f, c.y + Random.Range(-h, h)), new Vector2(Random.Range(4f, 7f), Random.Range(-0.3f, 0.3f)), Vector2.zero,
                           (2f * w + 1f) / 5f, new Color(1f, 1f, 0.9f, 0.55f), 1f, 3);
                    break;
            }
        }
    }
}
