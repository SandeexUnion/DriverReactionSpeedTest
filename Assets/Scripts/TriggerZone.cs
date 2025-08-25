using UnityEngine;
using System;
public class TriggerZone : MonoBehaviour
{
    [SerializeField] SimpleCarController player;
    [SerializeField] Transform point;
    [SerializeField] NPCCarController npcCar;
    [SerializeField] SimpleInputTimer timer;

    private void Awake()
    {
        player = FindAnyObjectByType<SimpleCarController>();
        timer = FindAnyObjectByType<SimpleInputTimer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (player != null && point != null && npcCar != null && other.gameObject == player.gameObject)
        {
            npcCar.waypoints.Add(point);
            npcCar.waypoints[0] = point;
            timer.StartTimer();
        }
    }
}
