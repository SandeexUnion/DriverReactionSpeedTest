using UnityEngine;

public class TriggerZone : MonoBehaviour
{
    [SerializeField] private SimpleCarController player;
    [SerializeField] private NPCCarController npcCar;
    [SerializeField] private NPCManController npcMan;
    [SerializeField] private NPCManController npcMan2;

    private void Awake()
    {
        
        player = FindObjectOfType<SimpleCarController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (player == null || other.gameObject != player.gameObject) return;

        
        ActivateNpcCar();
        ActivateNpcMan();
    }

    private void ActivateNpcCar()
    {
        if (npcCar != null)
        {
            npcCar.maxMotorTorque = 100;
        }
    }

    private void ActivateNpcMan()
    {
        Debug.Log("Activating NPC Man...");
        if (npcMan != null)
        {
            Debug.Log("Starting NPC Man movement.");
            NPCManController manToActivate = ChooseMan();

            
            manToActivate?.StartMoving(); 
        }
    }

    private NPCManController ChooseMan()
    {
        Debug.Log("Choosing which NPC Man to activate...");
        if (npcMan2 == null) return npcMan;

        
        if (Random.value < 0.5f)
        {
            return npcMan;
        }
        else
        {
            return npcMan2;
        }
    }
}