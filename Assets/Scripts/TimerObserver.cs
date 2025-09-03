using UnityEngine;

public class SimpleInputTimer : MonoBehaviour
{
    [SerializeField] private SimpleCarController player;
    private float _startTime;
    private bool _isTimerRunning;

    private void Start()
    {
        if (player == null)
            player = FindObjectOfType<SimpleCarController>();
    }

    void Update()
    {
        // Остановка таймера при нажатии тормоза
       if(player.currentBrakeValue>0.1f)
        {
            StopTimer();
        }
    }

    public void StartTimer()
    {
        if (!_isTimerRunning)
        {
            _startTime = Time.time;
            _isTimerRunning = true;
            Debug.Log("Таймер запущен");
        }
    }

    private void StopTimer()
    {
        if (_isTimerRunning)
        {
            float elapsedTime = Time.time - _startTime;
            _isTimerRunning = false;
            Debug.Log($"Таймер остановлен. Прошло времени: {elapsedTime:F2} секунд");
            OnTimerStopped(elapsedTime);
        }
    }

    private void OnTimerStopped(float time)
    {
        // Дополнительные действия...
    }
}