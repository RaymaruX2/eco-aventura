using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChestMove : MonoBehaviour
{
    private Animator anim;
    private float horizontalInput;
    public float speed;
    private float spawnPosX=6;
    [SerializeField] GameObject carrot;
    private AudioSource audioSource;
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();
        InvokeRepeating("SpawnRandomCarrot", 2, 1.5f);
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput != 0)
        {
            transform.Translate(Vector2.right * horizontalInput * Time.deltaTime * speed);
            // El limite se aplica DESPUES de mover (antes se podia salir un frame de la pantalla y temblar en el borde).
            float clampedX = Mathf.Clamp(transform.position.x, -7f, 7f);
            transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
        }
    }
    private void SpawnRandomCarrot()
    {
        Vector2 spawnPos = new Vector2(Random.Range(-spawnPosX, spawnPosX), 6);
        Instantiate(carrot, spawnPos, carrot.transform.rotation);
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        anim.SetBool("ChestEat",true);
        audioSource.Play();
        Destroy(other.gameObject);
        Invoke("ChestClose",0.5f);
    }
    private void ChestClose()
    {
        anim.SetBool("ChestEat",false);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        Destroy(other.gameObject);
    }
}
