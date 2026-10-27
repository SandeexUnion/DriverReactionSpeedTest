using UnityEngine;
using UnityEngine.AI;

public class NPCManController : MonoBehaviour
{
    [SerializeField] private GameObject point;
    private NavMeshAgent agent;
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    
    void Update()
    {
        
    }

    public void StartMoving()
    {
        agent.SetDestination(point.transform.position);
    }
}