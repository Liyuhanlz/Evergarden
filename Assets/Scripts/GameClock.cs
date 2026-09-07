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

    // Freezes day progression (and, since DayNightCycle reads TimeOfDay each
    // frame, the sun/skybox along with it) without touching Time.timeScale --
    // menus that need to keep animating (e.g. the tool rack's scroll) while
    // open can't use timeScale = 0, since that also zeroes Time.deltaTime.
    private bool isPaused;
    public bool IsPaused
    {
        get => isPaused;
        set
        {
            if (isPaused == value) return;
            isPaused = value;
            OnPauseChanged?.Invoke(isPaused);
        }
    }

    public event Action OnNewDay;
    public UnityEvent OnNewDayUnityEvent;

    // Fires whenever IsPaused actually changes -- anything that needs to
    // freeze while a menu is open (animals, etc.) without polling every
    // frame can subscribe instead.
    public event Action<bool> OnPauseChanged;

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
        if (IsPaused) return;

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
        if (0 <= CurrentHour && CurrentHour < 12)
        {
            return CurrentHour.ToString("D2") + ":" + CurrentMinute.ToString("D2") + "AM";
        }
        else
        {
            return CurrentHour.ToString("D2") + ":" + CurrentMinute.ToString("D2") + "PM";
        }

        /*return CurrentHour.ToString("D2") + ":" + CurrentMinute.ToString("D2");*/
    }

    public string GetDayString()
    {
        return "Day " + CurrentDay;
    }
}