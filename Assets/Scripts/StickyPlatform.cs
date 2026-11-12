using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StickyPlatform : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player") && !Death.isDeath)
        {
            other.gameObject.transform.SetParent(transform, true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Solo se suelta si sigue siendo hijo de esta plataforma
        // (evita desemparentar al jugador de otra plataforma en la que ya esta).
        if (other.gameObject.CompareTag("Player") && other.transform.parent == transform)
        {
            other.gameObject.transform.SetParent(null, true);
        }
    }
}
