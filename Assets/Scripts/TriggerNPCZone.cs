using UnityEngine;

public class TriggerNPCZone : MonoBehaviour
{
    
    [SerializeField] NPCCarController npcCar;
    [SerializeField] SimpleInputTimer timer;

    private void Awake()
    {
        timer = FindAnyObjectByType<SimpleInputTimer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (npcCar != null && other.gameObject == npcCar.gameObject)
        {
            timer.StartTimer();
        }
    }
}
