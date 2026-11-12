using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ambiente por zonas de cada nivel (configurado en BibliotecaSonidos > niveles).
// Al cambiar de zona hace fundidos suaves de: sonido ambiental, musica, tinte/iluminacion de
// pantalla (con vineta) y particulas ambientales. Tambien coloca los carteles de ayuda.
// Se crea automaticamente en cada escena que tenga configuracion.
public class GestorAmbiente : MonoBehaviour
{
    private BibliotecaSonidos.Nivel nivel;
    private Transform jugador;
    private BibliotecaSonidos.Zona zonaActual;
    private AudioSource ambA, ambB, musZona;
    private readonly List<AudioSource> musicaEscena = new List<AudioSource>();
    private readonly Dictionary<AudioSource, float> volOriginal = new Dictionary<AudioSource, float>();
    private Image tinte, vineta;
    private float objAmb, objMus, objEscena = 1f;
    private Color objTinte;
    private const float VelFundido = 0.7f;   // unidades de volumen por segundo

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        AlCargar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AlCargar(Scene s, LoadSceneMode m)
    {
        if (GestorAudio.Instancia == null || GestorAudio.Instancia.Biblioteca == null) return;
        BibliotecaSonidos.Nivel n = GestorAudio.Instancia.Biblioteca.BuscarNivel(s.name);
        if (n == null || FindObjectOfType<GestorAmbiente>() != null) return;
        GameObject go = new GameObject("GestorAmbiente");
        SceneManager.MoveGameObjectToScene(go, s);
        go.AddComponent<GestorAmbiente>().nivel = n;
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) jugador = p.transform;

        // Musica original de la escena (fuentes en bucle y largas): se conserva y se atenua por zonas.
        foreach (AudioSource a in FindObjectsOfType<AudioSource>())
        {
            if (a.loop && a.clip != null && a.clip.length > 20f && a.playOnAwake)
            {
                musicaEscena.Add(a); volOriginal[a] = a.volume;
            }
        }
        ambA = Fuente(); ambB = Fuente(); musZona = Fuente();
        CrearTinte();
        CrearCarteles();
        NubesEnMovimiento();
    }

    private AudioSource Fuente()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.loop = true; s.playOnAwake = false; s.volume = 0f; s.spatialBlend = 0f;
        return s;
    }

    private void CrearTinte()
    {
        GameObject c = new GameObject("TinteAmbiente", typeof(RectTransform));
        c.transform.SetParent(transform, false);
        Canvas cv = c.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = -10;   // por debajo del HUD
        tinte = Capa(c.transform, "Tinte", null);
        vineta = Capa(c.transform, "Vineta", CrearVineta());
        tinte.color = vineta.color = new Color(0, 0, 0, 0);
    }

    private static Image Capa(Transform padre, string n, Sprite s)
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(padre, false);
        Image i = g.AddComponent<Image>(); i.sprite = s; i.raycastTarget = false;
        RectTransform r = i.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return i;
    }

    private static Sprite CrearVineta()
    {
        const int N = 64;
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d))));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    private void CrearCarteles()
    {
        foreach (BibliotecaSonidos.Cartel c in nivel.carteles)
        {
            GameObject g = new GameObject("Cartel");
            g.transform.SetParent(transform, false);
            g.transform.position = new Vector3(c.posicion.x, c.posicion.y, 0f);
            TextMeshPro t = g.AddComponent<TextMeshPro>();
            t.text = c.texto;
            t.fontSize = 3.2f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(1f, 0.97f, 0.88f);
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(40, 24, 30, 255);
            t.rectTransform.sizeDelta = new Vector2(9f, 3f);
            t.sortingLayerID = SortingLayer.NameToID("Foreground");
            t.sortingOrder = 20;
        }
    }

    private static void NubesEnMovimiento()
    {
        // La capa de nubes avanza sola muy despacio: el cielo tiene vida aunque el jugador este quieto.
        foreach (FondoMovimiento f in FindObjectsOfType<FondoMovimiento>())
        {
            SpriteRenderer sr = f.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null && sr.sprite.name.ToLower().Contains("cloud")) f.desplazamientoAutomatico = 0.006f;
        }
    }

    private BibliotecaSonidos.Zona ZonaEn(Vector2 p)
    {
        BibliotecaSonidos.Zona mejor = null;
        foreach (BibliotecaSonidos.Zona z in nivel.zonas)
            if (z.area.Contains(p) && (mejor == null || z.prioridad > mejor.prioridad)) mejor = z;
        return mejor;
    }

    private void Update()
    {
        if (jugador == null) return;
        BibliotecaSonidos.Zona z = ZonaEn(jugador.position);
        if (z != zonaActual) CambiarZona(z);

        float dt = Time.unscaledDeltaTime * VelFundido;
        // ambiente: ambA es la fuente activa, ambB se apaga
        ambA.volume = Mathf.MoveTowards(ambA.volume, objAmb, dt);
        ambB.volume = Mathf.MoveTowards(ambB.volume, 0f, dt);
        if (ambB.volume <= 0f && ambB.isPlaying) ambB.Stop();
        musZona.volume = Mathf.MoveTowards(musZona.volume, objMus, dt);
        if (musZona.volume <= 0f && musZona.isPlaying && objMus <= 0f) musZona.Stop();
        foreach (AudioSource a in musicaEscena)
            if (a != null) a.volume = Mathf.MoveTowards(a.volume, volOriginal[a] * objEscena, dt * volOriginal[a] + 0.0001f);
        tinte.color = Color.Lerp(tinte.color, objTinte, Time.unscaledDeltaTime * 1.5f);
        Color v = vineta.color; v.a = Mathf.Lerp(v.a, objTinte.a * 1.6f, Time.unscaledDeltaTime * 1.5f); vineta.color = new Color(0.02f, 0.01f, 0.04f, v.a);
    }

    private void CambiarZona(BibliotecaSonidos.Zona z)
    {
        zonaActual = z;
        // ambiente
        AudioClip nuevo = z != null ? z.ambiente : null;
        if (ambA.clip != nuevo)
        {
            AudioSource tmp = ambA; ambA = ambB; ambB = tmp;   // la activa pasa a apagarse
            ambA.clip = nuevo; ambA.volume = 0f;
            if (nuevo != null) ambA.Play();
        }
        objAmb = z != null && z.ambiente != null ? z.volumenAmbiente : 0f;
        // musica: propia de la zona o la original de la escena
        if (z != null && z.musica != null)
        {
            if (musZona.clip != z.musica) { musZona.clip = z.musica; musZona.Play(); }
            objMus = z.volumenMusica * 0.5f; objEscena = 0f;
        }
        else
        {
            objMus = 0f; objEscena = z != null ? z.volumenMusica : 1f;
        }
        objTinte = z != null ? z.tinte : new Color(0, 0, 0, 0);
        EfectosFX.Ambiente(z != null ? z.particulas : 0);
    }

    private void OnDestroy() { EfectosFX.Ambiente(0); }
}
