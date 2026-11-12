using System.Collections.Generic;
using UnityEngine;

// Gestor de audio persistente (se crea solo al arrancar el juego).
[DefaultExecutionOrder(-200)]
public class GestorAudio : MonoBehaviour
{
    public static GestorAudio Instancia { get; private set; }
    public BibliotecaSonidos Biblioteca { get; private set; }

    [Tooltip("Distancia (unidades) a partir de la cual un sonido situado ya no se oye.")]
    public float distanciaMaxima = 18f;
    [Tooltip("Distancia hasta la que un sonido situado suena a volumen completo.")]
    public float distanciaPlena = 5f;
    public float volumenGeneral = 1f;

    private const int Voces = 16;
    private readonly List<AudioSource> fuentes = new List<AudioSource>();
    private readonly Dictionary<string, float> ultimaVez = new Dictionary<string, float>();
    private int siguiente;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Crear()
    {
        if (Instancia != null) return;
        GameObject go = new GameObject("GestorAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<GestorAudio>();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        Biblioteca = Resources.Load<BibliotecaSonidos>("BibliotecaSonidos");
        for (int i = 0; i < Voces; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;   // 2D: el paneo y la atenuacion se calculan aqui (camara en z=-10)
            fuentes.Add(s);
        }
    }

    public bool Reproducir(string clave, Vector3? posicion, float volumenExtra)
    {
        if (Biblioteca == null || string.IsNullOrEmpty(clave)) return false;
        BibliotecaSonidos.Entrada e = Biblioteca.Buscar(clave);
        if (e == null || e.clips == null || e.clips.Length == 0) return false;

        // Evita que el mismo sonido se repita de forma molesta (p. ej. varios enemigos a la vez).
        if (ultimaVez.TryGetValue(clave, out float t) && Time.unscaledTime - t < e.enfriamiento) return false;

        float vol = e.volumen * volumenExtra * volumenGeneral;
        float pan = 0f;
        if (posicion.HasValue)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 d = (Vector2)(posicion.Value - cam.transform.position);
                float dist = d.magnitude;
                if (dist >= distanciaMaxima) return false;
                vol *= 1f - Mathf.Clamp01((dist - distanciaPlena) / (distanciaMaxima - distanciaPlena));
                pan = Mathf.Clamp(d.x / (cam.orthographicSize * cam.aspect + 2f), -0.8f, 0.8f);
            }
        }
        if (vol <= 0.01f) return false;

        AudioClip clip = e.clips[Random.Range(0, e.clips.Length)];
        if (clip == null) return false;
        AudioSource s = FuenteLibre();
        s.clip = clip;
        s.volume = vol;
        s.panStereo = pan;
        s.pitch = 1f + Random.Range(-e.variacionTono, e.variacionTono);
        s.Play();
        ultimaVez[clave] = Time.unscaledTime;
        return true;
    }

    private AudioSource FuenteLibre()
    {
        for (int i = 0; i < fuentes.Count; i++)
        {
            AudioSource s = fuentes[(siguiente + i) % fuentes.Count];
            if (!s.isPlaying) { siguiente = (siguiente + i + 1) % fuentes.Count; return s; }
        }
        // Todas ocupadas: se reutiliza la mas antigua.
        AudioSource r = fuentes[siguiente];
        siguiente = (siguiente + 1) % fuentes.Count;
        return r;
    }
}
