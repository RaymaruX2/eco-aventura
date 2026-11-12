using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HealthSystem : MonoBehaviour
{
    public static int health = 3;
    [SerializeField] GameObject live3;
    [SerializeField] GameObject live2;
    [SerializeField] GameObject live1;
    [SerializeField] GameObject live0;
    [SerializeField] GameObject pauseScene;

    private int shownHealth = -1;
    private bool gameOverStarted;

    private void Update()
    {
        // Solo se actualiza la UI cuando la vida cambia (antes se hacia cada frame).
        if (health == shownHealth) return;
        shownHealth = health;

        SetActiveSafe(live3, health >= 3);
        SetActiveSafe(live2, health == 2);
        SetActiveSafe(live1, health == 1);
        SetActiveSafe(live0, health <= 0);

        // Antes, con 0 vidas se lanzaban dos corrutinas nuevas en CADA frame.
        if (health <= 0 && !gameOverStarted)
        {
            gameOverStarted = true;
            StartCoroutine(GameOver());
        }
    }

    private IEnumerator GameOver()
    {
        yield return new WaitForSeconds(0.9f);
        if (pauseScene != null) pauseScene.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        Time.timeScale = 0;
    }

    private static void SetActiveSafe(GameObject go, bool value)
    {
        if (go != null && go.activeSelf != value) go.SetActive(value);
    }
}
