using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Score : MonoBehaviour
{
    [SerializeField] private AudioClip audioStar;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private Image _carrotsImage0;
    [SerializeField] private Image _carrotsImage1;
    [SerializeField] private Image _carrotsImage2;
    public static int score = 0;
    public static int carrot = 0;

    private void Start()
    {
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        // Al cargar el nivel 2 el contador mostraba el valor escrito en la escena,
        // no las estrellas acumuladas del nivel anterior.
        if (_scoreText != null) _scoreText.text = score.ToString();
        RefreshCarrots();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isStar = other.gameObject.CompareTag("Star");
        bool isCarrot = other.gameObject.CompareTag("Carrot");
        if (!isStar && !isCarrot) return;

        // Destroy() se aplica al final del frame: se desactiva ya el collider
        // para que el mismo objeto no pueda contarse dos veces.
        other.enabled = false;

        if (isStar)
        {
            score++;
            if (_scoreText != null) _scoreText.text = score.ToString();
        }
        else
        {
            carrot++;
            RefreshCarrots();
        }

        if (isCarrot) Sonido.Reproducir("interfaz/zanahoria");
        else if (_audioSource != null && audioStar != null) _audioSource.PlayOneShot(audioStar, 0.05f);
        else Sonido.Reproducir("interfaz/estrella");
        Destello(other.transform.position);
        Destroy(other.gameObject);
    }

    [Header("Efecto al recoger")]
    [Tooltip("Fotogramas del destello que aparece al recoger una estrella o zanahoria (item-feedback, CC0).")]
    [SerializeField] private Sprite[] destelloFrames;
    [SerializeField] private float destelloFps = 14f;

    private void Destello(Vector3 posicion)
    {
        if (destelloFrames == null || destelloFrames.Length == 0) return;
        GameObject fx = new GameObject("Destello");
        fx.transform.position = posicion;
        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 10;
        StartCoroutine(AnimarDestello(fx, sr));
    }

    private IEnumerator AnimarDestello(GameObject fx, SpriteRenderer sr)
    {
        float espera = 1f / Mathf.Max(1f, destelloFps);
        foreach (Sprite s in destelloFrames)
        {
            if (fx == null) yield break;
            sr.sprite = s;
            yield return new WaitForSeconds(espera);
        }
        if (fx != null) Destroy(fx);
    }

    private void RefreshCarrots()
    {
        SetCarrotAlpha(_carrotsImage0, carrot >= 1);
        SetCarrotAlpha(_carrotsImage1, carrot >= 2);
        SetCarrotAlpha(_carrotsImage2, carrot >= 3);
    }

    private static void SetCarrotAlpha(Image img, bool collected)
    {
        if (img == null || !collected) return;
        Color c = img.color;
        c.a = 1f;
        img.color = c;
    }
}
