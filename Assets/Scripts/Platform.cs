using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform : MonoBehaviour
{
    [SerializeField] private GameObject[] waypoints;
    private int currentWaypointIndex = 0;

    [SerializeField] private float speed = 2.0f;

    // Se mueve en FixedUpdate (paso de fisica) para que el jugador que va encima
    // se desplace en sincronia y no tiemble ni resbale.
    private void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0 || waypoints[currentWaypointIndex] == null) return;

        Vector2 target = waypoints[currentWaypointIndex].transform.position;
        if (Vector2.Distance(target, transform.position) < .1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            target = waypoints[currentWaypointIndex].transform.position;
        }

        Vector2 newPos = Vector2.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
    }
}
