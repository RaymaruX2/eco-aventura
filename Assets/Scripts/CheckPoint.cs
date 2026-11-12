using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    public Death deathWall;

    private void Awake()
    {
        if (deathWall == null) deathWall = GetComponent<Death>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (deathWall == null || !other.gameObject.CompareTag("CheckPoint")) return;
        // Mientras el jugador cae tras un golpe no debe guardar un checkpoint nuevo.
        if (Death.isDeath) return;
        Vector2 nuevo = other.transform.position;
        if ((nuevo - deathWall.checkPos).sqrMagnitude > 0.01f)
        {
            // Checkpoint nuevo: aviso sonoro y visual.
            Sonido.Reproducir("interfaz/checkpoint");
            EfectosFX.Chispas(nuevo + Vector2.up * 0.5f, new Color(0.5f, 1f, 0.6f), 12);
        }
        deathWall.checkPos = nuevo;
    }
}
