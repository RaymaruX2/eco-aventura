using UnityEngine;
using UnityEngine.SceneManagement;

// Salvaguarda para los CompositeCollider2D del suelo.
// El suelo de los niveles usa TilemapCollider2D + CompositeCollider2D (une todos los tiles en
// una sola forma continua, sin juntas internas en las que el jugador tropiece). La geometria se
// genera automaticamente; esto solo garantiza que, si al cargar una escena un Composite se
// quedara sin geometria, se regenere. No requiere anadir nada a las escenas.
public static class CompositeColliderSafety
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Check();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Check();
    }

    private static void Check()
    {
        foreach (CompositeCollider2D composite in Object.FindObjectsOfType<CompositeCollider2D>())
        {
            if (composite.pathCount == 0 || composite.shapeCount == 0)
                composite.GenerateGeometry();
        }
    }
}
