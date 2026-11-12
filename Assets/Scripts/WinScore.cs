using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WinScore : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _starText;
    [SerializeField] private TextMeshProUGUI _killText;
    [SerializeField] private TextMeshProUGUI _deathText;
    [SerializeField] private TextMeshProUGUI _totalText;
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null) audioSource.Play();
        if (_starText != null) _starText.text = Score.score.ToString("000");
        if (_killText != null) _killText.text = Enemy.killcounter.ToString("000");
        if (_deathText != null) _deathText.text = Death.deathcounter.ToString("000");
        // La puntuacion total nunca es negativa.
        int total = Mathf.Max(0, Score.score * 10 + Enemy.killcounter * 15 - Death.deathcounter * 10);
        if (_totalText != null) _totalText.text = total.ToString();
    }
}
