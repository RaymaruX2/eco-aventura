using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuitGame : MonoBehaviour
{
    // Antes se lanzaba una corrutina de espera que no hacia nada (Application.Quit se llamaba
    // inmediatamente) y dentro del editor el boton no tenia ningun efecto.
    public void ExitGame()
    {
        Time.timeScale = 1;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
