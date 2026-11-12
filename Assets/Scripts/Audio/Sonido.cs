using System.Collections.Generic;
using UnityEngine;

// Punto de entrada para reproducir efectos desde cualquier script:
//   Sonido.Reproducir("jugador/salto");                 // sonido "de interfaz" (sin posicion)
//   Sonido.Reproducir("perro/alerta", transform.position); // sonido situado en el mundo
// Los sonidos situados se panean a izquierda/derecha y se atenuan con la distancia a la camara.
public static class Sonido
{
    public static bool Reproducir(string clave, Vector3? posicion = null, float volumen = 1f)
    {
        return GestorAudio.Instancia != null && GestorAudio.Instancia.Reproducir(clave, posicion, volumen);
    }

    public static bool Existe(string clave)
    {
        return GestorAudio.Instancia != null && GestorAudio.Instancia.Biblioteca != null &&
               GestorAudio.Instancia.Biblioteca.Buscar(clave) != null;
    }
}
