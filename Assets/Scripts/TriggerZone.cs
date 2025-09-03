using UnityEngine;
using System;
public class TriggerZone : MonoBehaviour
{
    [SerializeField] SimpleCarController player;
    [SerializeField] Transform point;
    [SerializeField] NPCCarController npcCar;

    private void Awake()
    {
        player = FindAnyObjectByType<SimpleCarController>();
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (player != null && point != null && npcCar != null && other.gameObject == player.gameObject)
        {
            npcCar.waypoints.Add(point);
            npcCar.waypoints[0] = point;
        }
    }
}
