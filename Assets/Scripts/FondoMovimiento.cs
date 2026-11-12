using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Capa de fondo con parallax basado en la CAMARA.
//
// Antes: las capas eran hijas del jugador (subian y bajaban con cada salto), la textura se
// desplazaba segun la velocidad del jugador (si una plataforma movil lo llevaba, el fondo no
// se movia aunque la camara si) y tenian escala no uniforme (pixel art estirado en horizontal).
//
// Ahora:
//  - Cada capa acompana a la camara y su textura se desplaza en proporcion al movimiento real
//    de la camara. "velocidadMovimiento.x" se conserva como profundidad relativa de la capa
//    (0.3 arboles, 0.2 montanas, 0.1 nubes) y "intensidad" la suaviza globalmente.
//      factor 0 = capa infinitamente lejana (quieta respecto a la pantalla)
//      factor 1 = capa que se mueve igual que el escenario
//  - Escala uniforme (proporciones originales del dibujo) y la textura se repite las veces
//    necesarias para cubrir cualquier ancho de pantalla, sin mostrar nunca los bordes.
//  - El ligero desplazamiento vertical se limita al margen real de la imagen.
// Se ejecuta despues de la camara para que no haya un fotograma de retraso (temblor).
[DefaultExecutionOrder(100)]
public class FondoMovimiento : MonoBehaviour
{
    [SerializeField] private Vector2 velocidadMovimiento;
    [Tooltip("Multiplicador global del parallax. 1 = valores originales, menor = mas sutil.")]
    [SerializeField] private float intensidad = 0.8f;
    [Tooltip("Cuanto se desplaza la capa en vertical respecto al mundo (muy poco).")]
    [SerializeField] private float factorVertical = 0.08f;
    [Tooltip("Usa escala uniforme (sin estirar el dibujo) y repite la textura para cubrir la pantalla.")]
    [SerializeField] private bool corregirProporcion = true;
    [Tooltip("Desplazamiento automatico de la textura (UV por segundo), p. ej. nubes que avanzan solas.")]
    public float desplazamientoAutomatico = 0f;

    private SpriteRenderer sr;
    private Material material;
    private Transform cam;
    private Camera camara;
    private float offsetY;
    private float z;
    private Vector3 camaraInicial;
    private Vector2 offsetTexturaInicial;
    private float anchoRepeticion = 1f;   // ancho en el mundo de una repeticion de la imagen
    private float altoCapa = 1f;
    private float ultimoAspecto = -1f;
    private float escalaUniforme = 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        material = sr.material;
        camara = Camera.main;
        if (camara != null)
        {
            // Se mide en Awake, antes de que la camara se separe del jugador o se ajuste a los
            // limites del nivel: asi se conserva el encuadre vertical disenado en la escena.
            offsetY = transform.position.y - camara.transform.position.y;
        }
        z = transform.position.z;
        escalaUniforme = Mathf.Abs(transform.lossyScale.y);
    }

    private void Start()
    {
        if (camara == null) { enabled = false; return; }
        cam = camara.transform;

        // Se separa del jugador: a partir de ahora la capa solo depende de la camara.
        transform.SetParent(null, true);

        camaraInicial = cam.position;
        if (material != null) offsetTexturaInicial = material.mainTextureOffset;
        AjustarTamano();
    }

    private void AjustarTamano()
    {
        ultimoAspecto = camara.aspect;
        float anchoSprite = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
        float altoSprite = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;

        // Cobertura vertical: con un encuadre mas amplio la imagen debe seguir cubriendo toda la
        // altura de la vista (mas un margen) para que nunca se vea su borde.
        float mitadNecesaria = camara.orthographicSize + 1f + Mathf.Abs(offsetY);
        if (altoSprite * escalaUniforme * 0.5f < mitadNecesaria)
            escalaUniforme = mitadNecesaria / (altoSprite * 0.5f);

        if (corregirProporcion)
        {
            float anchoVista = camara.orthographicSize * 2f * camara.aspect;
            anchoRepeticion = anchoSprite * escalaUniforme;
            int repeticiones = Mathf.Max(1, Mathf.CeilToInt((anchoVista + 2f) / anchoRepeticion));
            transform.localScale = new Vector3(escalaUniforme * repeticiones, escalaUniforme, 1f);
            if (material != null) material.mainTextureScale = new Vector2(repeticiones, 1f);
        }
        else
        {
            anchoRepeticion = anchoSprite * Mathf.Abs(transform.lossyScale.x);
        }
        anchoRepeticion = Mathf.Max(0.01f, anchoRepeticion);
        altoCapa = altoSprite * Mathf.Abs(transform.lossyScale.y);
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        if (!Mathf.Approximately(camara.aspect, ultimoAspecto)) AjustarTamano();

        Vector3 delta = cam.position - camaraInicial;
        float factor = Mathf.Clamp01(velocidadMovimiento.x * intensidad);

        // Horizontal: la imagen se repite (wrap Repeat), asi que el scroll es infinito.
        if (material != null)
            material.mainTextureOffset = offsetTexturaInicial + new Vector2(delta.x * factor / anchoRepeticion + Time.time * desplazamientoAutomatico, 0f);

        // Vertical: ligero desplazamiento opuesto al de la camara, limitado al margen real
        // de la imagen para que nunca aparezca su borde superior o inferior.
        float medioVista = camara.orthographicSize;
        float margenInferior = altoCapa * 0.5f - offsetY - medioVista - 0.2f;
        float margenSuperior = altoCapa * 0.5f + offsetY - medioVista - 0.2f;
        float dy = -delta.y * factorVertical * (0.5f + factor);
        dy = Mathf.Clamp(dy, -Mathf.Max(0f, margenSuperior), Mathf.Max(0f, margenInferior));

        transform.position = new Vector3(cam.position.x, cam.position.y + offsetY + dy, z);
    }

    private void OnDestroy()
    {
        // .material crea una copia en tiempo de ejecucion: se libera para no acumular memoria.
        if (material != null) Destroy(material);
    }
}
