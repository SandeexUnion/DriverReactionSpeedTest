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
        Debug.Log($"Объект {other.gameObject.name} вошел в триггерную зону.");
        if (npcCar != null && other.gameObject == npcCar.gameObject)
        {
            timer.StartTimer();
        }
        if (npcMan != null && other.gameObject == npcMan.gameObject || other.gameObject.CompareTag("npcman"))
        {
            Debug.Log("NPC Man entered the trigger zone.");
            timer.StartTimer();
        }
    }
}