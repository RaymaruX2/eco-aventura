using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resume : MonoBehaviour
{
    [SerializeField] public GameObject resumeScreen;

    public void ResumeGame()
    {
        StartCoroutine(ResumeDelay());
        Sonido.Reproducir("interfaz/pausa_salir");
        resumeScreen.SetActive(false);
        CharacterControl.resume = false;
        Time.timeScale = 1;
    }
    IEnumerator ResumeDelay()
    {
        yield return new WaitForSeconds(0.5f);
    }
}
