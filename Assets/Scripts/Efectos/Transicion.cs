using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Transiciones entre escenas: fundido a negro, carga asincrona y tarjeta de titulo del nivel.
// Se crea sola al arrancar el juego. Uso:
//   Transicion.CargarEscena(indice);                       // fundido + carga
//   Transicion.NivelCompletado(indiceSiguiente);           // fanfarria + "Nivel completado" + carga
public class Transicion : MonoBehaviour
{
    private static Transicion inst;
    public static bool Ocupada => inst != null && inst.cargando;

    private CanvasGroup grupo;
    private TextMeshProUGUI titulo;
    private TextMeshProUGUI subtitulo;
    private bool cargando;
    private const float Fundido = 0.45f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Crear()
    {
        if (inst != null) return;
        GameObject go = new GameObject("Transicion");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<Transicion>();
    }

    private void Awake()
    {
        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f; grupo.blocksRaycasts = false; grupo.interactable = false;

        Image fondo = Hijo("Fondo").AddComponent<Image>();
        fondo.color = new Color(0.05f, 0.04f, 0.08f, 1f);
        Estirar(fondo.rectTransform);

        titulo = Texto("Titulo", 96, new Vector2(0, 40));
        subtitulo = Texto("Subtitulo", 48, new Vector2(0, -60));
        subtitulo.color = new Color(1f, 0.85f, 0.45f);

        SceneManager.sceneLoaded += AlCargar;
    }

    private GameObject Hijo(string n)
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(transform, false);
        return g;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI Texto(string n, float tam, Vector2 pos)
    {
        TextMeshProUGUI t = Hijo(n).AddComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = tam;
        t.enableWordWrapping = false;
        t.color = Color.white;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(1800, 200);
        r.anchoredPosition = pos;
        return t;
    }

    // ------------------------------------------------------------------ API
    public static void CargarEscena(int indice, string mensaje = null, string sub = null)
    {
        if (inst == null) { Time.timeScale = 1f; SceneManager.LoadScene(indice); return; }
        if (inst.cargando) return;
        inst.StartCoroutine(inst.Cargar(indice, mensaje, sub, 0f));
    }

    public static void NivelCompletado(int indiceSiguiente)
    {
        if (inst == null) { SceneManager.LoadScene(indiceSiguiente); return; }
        if (inst.cargando) return;
        Sonido.Reproducir("interfaz/nivel_completado");
        inst.StartCoroutine(inst.Cargar(indiceSiguiente, "¡Nivel completado!", ResumenPartida(), 1.6f));
    }

    private static string ResumenPartida()
    {
        return "Estrellas: " + Score.score + "   ·   Enemigos: " + Enemy.killcounter;
    }

    // ------------------------------------------------------------------ secuencias
    private IEnumerator Cargar(int indice, string mensaje, string sub, float mostrar)
    {
        cargando = true;
        CharacterControl.moving = false;
        grupo.blocksRaycasts = true;
        titulo.text = mensaje ?? ""; subtitulo.text = sub ?? "";
        if (mostrar > 0f) Sonido.Reproducir("interfaz/transicion");
        yield return Fundir(1f);
        if (mostrar > 0f) yield return new WaitForSecondsRealtime(mostrar);

        Time.timeScale = 1f;
        if (indice < 0 || indice >= SceneManager.sceneCountInBuildSettings) indice = 0;
        AsyncOperation op = SceneManager.LoadSceneAsync(indice);
        while (op != null && !op.isDone) yield return null;
        // El fundido de entrada lo hace AlCargar.
    }

    private void AlCargar(Scene escena, LoadSceneMode modo)
    {
        StopAllCoroutines();
        StartCoroutine(Entrar(escena.name));
    }

    private IEnumerator Entrar(string escena)
    {
        cargando = true;
        BibliotecaSonidos b = GestorAudio.Instancia != null ? GestorAudio.Instancia.Biblioteca : null;
        BibliotecaSonidos.Nivel n = b != null ? b.BuscarNivel(escena) : null;
        if (n != null && !string.IsNullOrEmpty(n.titulo))
        {
            // Tarjeta de titulo del nivel sobre negro y despues se abre la escena.
            string[] partes = n.titulo.Split('|');
            titulo.text = partes[0]; subtitulo.text = partes.Length > 1 ? partes[1] : "";
            grupo.alpha = 1f; grupo.blocksRaycasts = true;
            yield return new WaitForSecondsRealtime(1.1f);
        }
        else if (grupo.alpha < 0.01f)
        {
            cargando = false; yield break;
        }
        titulo.text = n != null ? titulo.text : ""; if (n == null) subtitulo.text = "";
        yield return Fundir(0f);
        titulo.text = ""; subtitulo.text = "";
        grupo.blocksRaycasts = false;
        cargando = false;
    }

    private IEnumerator Fundir(float objetivo)
    {
        float inicio = grupo.alpha, t = 0f;
        while (t < Fundido)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = Mathf.Lerp(inicio, objetivo, t / Fundido);
            yield return null;
        }
        grupo.alpha = objetivo;
    }
}
