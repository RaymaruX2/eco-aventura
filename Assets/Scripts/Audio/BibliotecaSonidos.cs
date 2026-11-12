using System.Collections.Generic;
using UnityEngine;

// Biblioteca central de sonidos (Assets/Resources/BibliotecaSonidos.asset).
// Cada entrada asocia una clave ("jugador/salto", "perro/alerta"...) con uno o varios clips
// (se elige uno al azar para evitar repeticiones), su volumen, la variacion de tono y el tiempo
// minimo entre reproducciones. Tambien define las zonas ambientales de cada nivel.
[CreateAssetMenu(menuName = "Juego/Biblioteca de sonidos")]
public class BibliotecaSonidos : ScriptableObject
{
    [System.Serializable]
    public class Entrada
    {
        public string clave;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volumen = 0.6f;
        [Range(0f, 0.5f)] public float variacionTono = 0.06f;
        [Tooltip("Tiempo minimo (s) entre dos reproducciones de esta clave.")]
        public float enfriamiento = 0.05f;
    }

    [System.Serializable]
    public class Zona
    {
        public string nombre;
        [Tooltip("Rectangulo de la zona en coordenadas del mundo (x, y, ancho, alto).")]
        public Rect area;
        [Tooltip("Prioridad: si se solapan zonas gana la mayor.")]
        public int prioridad;
        public AudioClip ambiente;
        [Range(0f, 1f)] public float volumenAmbiente = 0.35f;
        [Tooltip("Musica propia de la zona. Vacio = la musica original de la escena.")]
        public AudioClip musica;
        [Range(0f, 1f)] public float volumenMusica = 1f;
        [Tooltip("Tinte de pantalla de la zona (alfa = intensidad).")]
        public Color tinte = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Particulas ambientales: 0 ninguna, 1 hojas, 2 luciernagas, 3 viento/polen.")]
        public int particulas;
    }

    [System.Serializable]
    public class Nivel
    {
        public string escena;
        public string titulo;
        public List<Zona> zonas = new List<Zona>();
        [Tooltip("Carteles de ayuda: posicion en el mundo y texto.")]
        public List<Cartel> carteles = new List<Cartel>();
    }

    [System.Serializable]
    public class Cartel
    {
        public Vector2 posicion;
        [TextArea] public string texto;
    }

    public List<Entrada> entradas = new List<Entrada>();
    public List<Nivel> niveles = new List<Nivel>();

    private Dictionary<string, Entrada> indice;

    public Entrada Buscar(string clave)
    {
        if (indice == null)
        {
            indice = new Dictionary<string, Entrada>();
            foreach (Entrada e in entradas)
                if (e != null && !string.IsNullOrEmpty(e.clave)) indice[e.clave] = e;
        }
        indice.TryGetValue(clave, out Entrada r);
        return r;
    }

    public Nivel BuscarNivel(string escena)
    {
        foreach (Nivel n in niveles) if (n.escena == escena) return n;
        return null;
    }
}
