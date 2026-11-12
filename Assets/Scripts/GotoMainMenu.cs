using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GotoMainMenu : MonoBehaviour
{
    private bool loading;

    public void GoMainMenu()
    {
        if (loading) return;
        StartCoroutine(LoadLevel());
    }

    IEnumerator LoadLevel()
    {
        loading = true;
        // Antes se usaba WaitForSeconds: desde el menu de pausa o Game Over (timeScale = 0)
        // la espera nunca terminaba y el boton no hacia nada.
        Sonido.Reproducir("interfaz/boton");
        yield return new WaitForSecondsRealtime(0.3f);
        Score.carrot = 0;
        HealthSystem.health = 3;
        Score.score = 0;
        Enemy.killcounter = 0;
        Death.deathcounter = 0;
        CharacterControl.resume = false;
        Time.timeScale = 1;
        Transicion.CargarEscena(0);
    }
}
