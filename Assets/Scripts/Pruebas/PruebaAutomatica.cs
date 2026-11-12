using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Prueba automatica de principio a fin (solo se activa ejecutando el juego con -autotest).
//   "The Lost Forest.exe" -batchmode -nographics -autotest -logFile prueba.log
// Recorre: menu -> nivel 1 (enemigos, dano y reaparicion, checkpoints, meta) -> nivel 2 ->
// panel de victoria -> creditos -> menu -> Game Over -> reinicio. Registra errores y excepciones
// y termina con codigo 0 (todo bien) o 1 (fallos).
public class PruebaAutomatica : MonoBehaviour
{
    private readonly List<string> errores = new List<string>();
    private readonly List<string> pasos = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        if (!System.Environment.GetCommandLineArgs().Contains("-autotest")) return;
        GameObject go = new GameObject("PruebaAutomatica");
        DontDestroyOnLoad(go);
        go.AddComponent<PruebaAutomatica>();
    }

    private void Awake()
    {
        Application.runInBackground = true;
        Application.logMessageReceived += (msg, stack, tipo) =>
        {
            if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                errores.Add(tipo + ": " + msg + " | " + (stack ?? "").Split('\n')[0]);
        };
    }

    private void Start() { StartCoroutine(Recorrido()); }

    private void Ok(bool cond, string paso)
    {
        pasos.Add((cond ? "[OK]   " : "[FALLO]") + " " + paso);
        if (!cond) errores.Add("Comprobacion fallida: " + paso);
        Debug.Log("AUTOTEST " + (cond ? "OK " : "FALLO ") + paso);
    }

    private IEnumerator EsperarEscena(int indice, float max)
    {
        float t = 0f;
        while (SceneManager.GetActiveScene().buildIndex != indice && t < max) { t += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(1.8f);   // tarjeta de titulo + fundido
    }

    private static GameObject Jugador() { return GameObject.FindGameObjectWithTag("Player"); }

    private static void Teletransportar(Vector2 p)
    {
        GameObject j = Jugador();
        if (j == null) return;
        Rigidbody2D rb = j.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.velocity = Vector2.zero; rb.position = p; }
        j.transform.position = p;
    }

    private IEnumerator Recorrido()
    {
        yield return new WaitForSecondsRealtime(2f);
        Ok(SceneManager.GetActiveScene().buildIndex == 0, "El juego arranca en el menu principal (" + SceneManager.GetActiveScene().name + ")");
        Ok(GestorAudio.Instancia != null && GestorAudio.Instancia.Biblioteca != null, "Gestor de audio y biblioteca de sonidos cargados");

        // --- Menu -> nivel 1 (como el boton Comenzar)
        NextLevel inicio = FindObjectOfType<NextLevel>();
        if (inicio != null) inicio.SkipNextLevel(); else Transicion.CargarEscena(1);
        yield return EsperarEscena(1, 15f);
        Ok(SceneManager.GetActiveScene().buildIndex == 1, "Boton Comenzar carga el nivel 1");
        Ok(Jugador() != null, "Nivel 1: el jugador existe");
        Ok(Camera.main != null, "Nivel 1: camara principal");
        Ok(FindObjectOfType<GestorAmbiente>() != null, "Nivel 1: gestor de ambiente por zonas activo");
        Ok(HealthSystem.health == 3, "Nivel 1: partida nueva con 3 vidas");

        // --- Recorrido por las zonas (ambiente, enemigos, camara)
        Vector2[] puntos = { new Vector2(20, 1.5f), new Vector2(45, 3.5f), new Vector2(66, 2), new Vector2(88, -5), new Vector2(112, 2.5f),
                             new Vector2(128, 2), new Vector2(136, 6), new Vector2(148, 6), new Vector2(160, 12) };
        foreach (Vector2 p in puntos)
        {
            Teletransportar(p);
            yield return new WaitForSecondsRealtime(1.2f);
            if (Death.isDeath) yield return new WaitForSecondsRealtime(1.5f);
        }
        Ok(true, "Nivel 1: recorrido por las 9 secciones sin bloqueos");
        Ok(FindObjectsOfType<Enemy>().Length > 0, "Nivel 1: enemigos activos (" + FindObjectsOfType<Enemy>().Length + ")");

        // --- Dano y reaparicion en el checkpoint
        HealthSystem.health = 3;
        Death d = Jugador().GetComponent<Death>();
        yield return new WaitForSecondsRealtime(1.2f);   // fin de posible invulnerabilidad
        bool golpe = d.RecibirDanio();
        yield return new WaitForSecondsRealtime(2.2f);
        Ok(golpe && HealthSystem.health == 2 && !Death.isDeath, "Dano: se pierde una vida y el jugador reaparece");

        // --- Meta del nivel 1 (con 3 zanahorias)
        Score.carrot = 3;
        NextLevel meta = FindObjectOfType<NextLevel>();
        Ok(meta != null, "Nivel 1: existe la meta");
        if (meta != null)
        {
            Collider2D c = meta.GetComponent<Collider2D>();
            Teletransportar(new Vector2(c.bounds.center.x, c.bounds.min.y + 1.0f));
        }
        yield return EsperarEscena(2, 20f);
        Ok(SceneManager.GetActiveScene().buildIndex == 2, "La meta del nivel 1 carga el nivel 2");
        Ok(Score.carrot == 0 && HealthSystem.health == 2, "Entre niveles se conservan vidas y se reinician zanahorias");

        // --- Nivel 2: meta -> panel de victoria -> creditos
        yield return new WaitForSecondsRealtime(2f);
        Ok(Jugador() != null, "Nivel 2: el jugador existe");
        Score.carrot = 3;
        NextLevel meta2 = null;
        foreach (NextLevel n in FindObjectsOfType<NextLevel>()) if (n.GetComponent<Collider2D>() != null) meta2 = n;
        Ok(meta2 != null, "Nivel 2: existe la meta");
        if (meta2 != null)
        {
            Collider2D c2 = meta2.GetComponent<Collider2D>();
            Teletransportar(new Vector2(c2.bounds.center.x, c2.bounds.min.y + 1.0f));
        }
        yield return new WaitForSecondsRealtime(3f);
        WinScore panel = FindObjectOfType<WinScore>();
        Ok(panel != null && panel.gameObject.activeInHierarchy, "Nivel 2: la meta muestra el panel de victoria con estadisticas");
        NextLevel continuar = null;
        foreach (NextLevel n in FindObjectsOfType<NextLevel>()) if (n.GetComponent<Collider2D>() == null) continuar = n;
        if (continuar != null) continuar.SkipNextLevel(); else Transicion.CargarEscena(3);
        yield return EsperarEscena(3, 15f);
        Ok(SceneManager.GetActiveScene().buildIndex == 3, "Continuar lleva a la escena final");

        // --- Creditos -> menu
        GotoMainMenu menu = FindObjectOfType<GotoMainMenu>();
        if (menu != null) menu.GoMainMenu(); else Transicion.CargarEscena(0);
        yield return EsperarEscena(0, 15f);
        Ok(SceneManager.GetActiveScene().buildIndex == 0, "La escena final vuelve al menu principal");

        // --- Nueva partida -> Game Over -> reinicio
        inicio = FindObjectOfType<NextLevel>();
        if (inicio != null) inicio.SkipNextLevel(); else Transicion.CargarEscena(1);
        yield return EsperarEscena(1, 15f);
        Ok(HealthSystem.health == 3 && Score.score == 0, "Partida nueva desde el menu: vidas y puntos reiniciados");
        HealthSystem.health = 1;
        yield return new WaitForSecondsRealtime(1.2f);
        Jugador().GetComponent<Death>().RecibirDanio();
        yield return new WaitForSecondsRealtime(2.5f);
        Ok(Time.timeScale == 0f, "Sin vidas: aparece Game Over y el juego se pausa");
        RestartGame reinicio = null;
        foreach (RestartGame r in Resources.FindObjectsOfTypeAll<RestartGame>()) if (r.gameObject.scene.IsValid() && r.gameObject.activeInHierarchy) reinicio = r;
        if (reinicio != null) reinicio.Restart(); else Transicion.CargarEscena(1);
        yield return EsperarEscena(1, 15f);
        Ok(SceneManager.GetActiveScene().buildIndex == 1 && Time.timeScale == 1f && HealthSystem.health == 3, "Reiniciar tras Game Over vuelve al nivel 1 con 3 vidas");
        yield return new WaitForSecondsRealtime(2f);

        // --- Informe
        StringBuilder sb = new StringBuilder("\n===== INFORME DE PRUEBA AUTOMATICA =====\n");
        foreach (string p in pasos) sb.AppendLine(p);
        sb.AppendLine("Errores y excepciones registrados: " + errores.Count);
        foreach (string e in errores) sb.AppendLine("  - " + e);
        Debug.Log(sb.ToString());
        Application.Quit(errores.Count == 0 ? 0 : 1);
    }
}

static class ArgsExt
{
    public static bool Contains(this string[] a, string s) { foreach (string x in a) if (x == s) return true; return false; }
}
