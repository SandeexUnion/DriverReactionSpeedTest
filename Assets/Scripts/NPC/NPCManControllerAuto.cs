using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NPCManControllerAuto : MonoBehaviour
{
    [SerializeField] private GameObject firstPoint;
    [SerializeField] private GameObject secondPoint;
    private NavMeshAgent agent;
    private GameObject currentDestination; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        currentDestination = firstPoint; 
        StartCoroutine(MoveBetweenPoints()); 
    }

    private IEnumerator MoveBetweenPoints()
    {
        while (true) 
        {
            agent.SetDestination(currentDestination.transform.position); 
            yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance); // Ждем пока он дойдет до currentDestination
            SwitchDestination(); 
        }
    }

    private void SwitchDestination()
    {
        currentDestination = (currentDestination == firstPoint) ? secondPoint : firstPoint; 
    }
}