using System;
using UnityEngine;
using UnityEngine.Events;

public class GameClock : MonoBehaviour
{
    public static GameClock Instance
    {
        get;
        private set;
    }

    public float realSecondsPerDay = 120f;
    public int dayStartHour = 6;

    public int CurrentDay { get; private set; } = 1;
    public int CurrentHour { get; private set; } = 6;
    public int CurrentMinute { get; private set; } = 0;

    public float TimeOfDay { get; private set; } = 0f;

    public event Action OnNewDay;
    public UnityEvent OnNewDayUnityEvent;

    private float timer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        TimeOfDay = timer / realSecondsPerDay;

        float totalHours = dayStartHour + TimeOfDay * 24;
        CurrentHour = Mathf.FloorToInt(totalHours) % 24;
        CurrentMinute = Mathf.FloorToInt((totalHours % 1f) * 60f);

        if (timer >= realSecondsPerDay)
        {
            timer -= realSecondsPerDay;
            CurrentDay++;
            OnNewDay?.Invoke();
            OnNewDayUnityEvent?.Invoke();
            Debug.Log("[GameClock] Day " + CurrentDay + " started.");
        }
    }

    public string GetTimeString()
    {
        return CurrentHour.ToString("D2") + ":" + CurrentMinute.ToString("D2");
    }

    public string GetDayString()
    {
        return "Day " + CurrentDay;
    }
}