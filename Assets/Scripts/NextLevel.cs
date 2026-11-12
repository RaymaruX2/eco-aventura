using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevel : MonoBehaviour
{
    [SerializeField] public AudioClip _audioclip;
    [SerializeField] public AudioSource _audiosource;
    [SerializeField] public GameObject ThinkingBallon;
    [SerializeField] public GameObject WinScene;

    private bool loading;

    public void SkipNextLevel()
    {
        Time.timeScale = 1;
        CharacterControl.resume = false;
        if (loading) return;
        loading = true;
        Sonido.Reproducir("interfaz/boton");
        // Desde el menu principal empieza una partida NUEVA: se reinician vidas y contadores.
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            HealthSystem.health = 3; Score.score = 0; Score.carrot = 0; Enemy.killcounter = 0; Death.deathcounter = 0;
        }
        Score.carrot = 0;
        Transicion.CargarEscena(SceneManager.GetActiveScene().buildIndex + 1);
    }

    IEnumerator LoadLevel()
    {
        loading = true;
        // Realtime: funciona aunque el juego este pausado (timeScale = 0).
        yield return new WaitForSecondsRealtime(0.5f);
        Score.carrot = 0;
        Time.timeScale = 1;
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
        SceneManager.LoadScene(next);
    }

    private void OnTriggerEnter2D(Collider2D other) { IntentarSalir(other, true); }
    // Si el jugador entra sin las zanahorias y las consigue... o llega cayendo, se vuelve a comprobar.
    private void OnTriggerStay2D(Collider2D other) { IntentarSalir(other, false); }

    private void IntentarSalir(Collider2D other, bool entrando)
    {
        // Antes cualquier objeto (enemigos, zanahorias...) mostraba el globo de pensamiento.
        if (!other.gameObject.CompareTag("Player") || loading || Death.isDeath) return;
        // Solo cuenta si el jugador esta DE PIE ante la puerta (no cayendo/saltando por encima).
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null && Mathf.Abs(rb.velocity.y) > 0.5f) return;
        Collider2D propio = GetComponent<Collider2D>();
        if (propio != null && Mathf.Abs(other.bounds.center.x - propio.bounds.center.x) > propio.bounds.extents.x) return;

        if (Score.carrot > 2)
        {
            loading = true;
            CharacterControl.moving = false;
            if (rb != null) rb.velocity = Vector2.zero;
            if (_audiosource != null && _audioclip != null) _audiosource.PlayOneShot(_audioclip, 1);
            if (ThinkingBallon != null) ThinkingBallon.SetActive(false);

            int siguiente = SceneManager.GetActiveScene().buildIndex + 1;
            // Ultimo nivel con panel de victoria propio (estadisticas): se muestra antes de los creditos.
            if (WinScene != null) Invoke(nameof(Delay), 1f);
            else { Score.carrot = 0; Transicion.NivelCompletado(siguiente); }
        }
        else if (ThinkingBallon != null && entrando)
        {
            ThinkingBallon.SetActive(true);
            Sonido.Reproducir("interfaz/puerta_cerrada");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player") && ThinkingBallon != null)
            ThinkingBallon.SetActive(false);
    }

    void Delay()
    {
        Sonido.Reproducir("interfaz/nivel_completado");
        if (WinScene != null) WinScene.SetActive(true);
        Time.timeScale = 0;
    }
}
