using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour
{
    [SerializeField] private GameObject carrot;
    [SerializeField] private AudioClip audioChest;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animator anim;
    private bool isOpen = false;

    private void Awake()
    {
        // Referencias de respaldo si no se asignaron en el Inspector.
        if (anim == null) anim = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isOpen || !other.gameObject.CompareTag("Player")) return;
        // Un jugador muerto (cayendo tras un golpe) no abre cofres.
        if (Death.isDeath) return;

        isOpen = true;
        if (audioSource != null && audioChest != null) audioSource.PlayOneShot(audioChest, 0.5f);
        if (anim != null) anim.SetBool("open", true);
        if (carrot != null) carrot.SetActive(true);
    }
}
