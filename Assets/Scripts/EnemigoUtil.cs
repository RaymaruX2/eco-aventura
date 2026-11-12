using UnityEngine;

// Utilidades compartidas por los comportamientos de enemigos terrestres.
public static class EnemigoUtil
{
    private static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    // Un collider cuenta como terreno si es solido y no pertenece al jugador, a enemigos ni al propio objeto.
    public static bool EsTerreno(Collider2D c, Collider2D[] propios)
    {
        if (c == null || c.isTrigger || c.CompareTag("Player") || c.CompareTag("Enemy")) return false;
        if (propios != null) foreach (Collider2D p in propios) if (p == c) return false;
        return true;
    }

    public static bool HaySuelo(Vector2 origen, float distancia, Collider2D[] propios)
    {
        int n = Physics2D.RaycastNonAlloc(origen, Vector2.down, hits, distancia);
        for (int i = 0; i < n; i++) if (EsTerreno(hits[i].collider, propios)) return true;
        return false;
    }

    public static bool HayPared(Vector2 origen, float dir, float distancia, Collider2D[] propios)
    {
        int n = Physics2D.RaycastNonAlloc(origen, Vector2.right * dir, hits, distancia);
        for (int i = 0; i < n; i++) if (EsTerreno(hits[i].collider, propios)) return true;
        return false;
    }

    // Medio ancho del cuerpo y distancia del pivote a los pies (colliders solidos activos).
    public static void MedirCuerpo(Transform t, Collider2D[] propios, out float medioAncho, out float pies)
    {
        medioAncho = 0.5f; pies = 0.5f;
        bool init = false; Bounds b = default;
        foreach (Collider2D c in propios)
        {
            if (c.isTrigger || !c.enabled) continue;
            if (!init) { b = c.bounds; init = true; } else b.Encapsulate(c.bounds);
        }
        if (!init) return;
        medioAncho = Mathf.Max(0.3f, b.extents.x);
        pies = t.position.y - b.min.y;
    }

    public static Transform BuscarJugador()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }
}
