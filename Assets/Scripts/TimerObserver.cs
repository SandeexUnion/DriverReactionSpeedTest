using UnityEngine;

public class SimpleInputTimer : MonoBehaviour
{
    [SerializeField] private WheelController player;
    private float _startTime;
    private bool _isTimerRunning;

    private void Start()
    {
        if (player == null)
            player = FindObjectOfType<WheelController>();
    }

    void Update()
    {
        // Остановка таймера при нажатии тормоза (pedalInput < 0)
        if (_isTimerRunning && player.pedalInput < -0.1f) // -0.1f для избежания ложных срабатываний
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