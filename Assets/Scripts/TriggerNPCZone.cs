using UnityEngine;

public class TriggerNPCZone : MonoBehaviour
{
    
    [SerializeField] NPCCarController npcCar;
    [SerializeField] SimpleInputTimer timer;
    [SerializeField] NPCManController npcMan;

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
        if (npcMan != null && other.gameObject == npcMan.gameObject)
        {
            timer.StartTimer();
        }
    }
}