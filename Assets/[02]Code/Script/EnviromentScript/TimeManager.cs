using System;
using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    // A single named point in the day cycle: which of the 4 skybox textures is showing and
    // what color the sun/fog should be at that hour. Replaces the old fixed 4-gradient setup so
    // we can have more than 4 named periods (matching a reference table with separate blue
    // hour/sunrise/morning/midday/afternoon/sunset/dusk entries) while still only needing the 4
    // skybox textures that exist - several periods reuse the same skybox and only the light/fog
    // color gets finer resolution between them.
    [Serializable]
    public class TimePeriod
    {
        public string periodName;
        [Range(0, 23)] public int startHour;
        public Texture2D skybox;
        public Color color = Color.white;
    }

    [Header("Skybox Texture")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [Header("Light")]
    [SerializeField] private Light globalLight;

    [Header("Time Periods")]
    [Tooltip("Sorted ascending by Start Hour. The active period for any given hour is the last one whose Start Hour is <= that hour (wrapping past midnight to the last entry).")]
    [SerializeField] private TimePeriod[] timePeriods;

    [Header("Time")]
    [SerializeField] private float timeScale = 1f;
    [SerializeField] private float speedChange = 3f;

    private int minutes;
    public int Minutes { get { return minutes; } set { minutes = value; OnMinutesChange(value); } }

    private int hours;
    public int Hours { get { return hours; } set { hours = value; OnHoursChange(value); } }
    private int days;
    public int Days { get { return days; } set { days = value; } }

    private float tempSecond;
    private Texture2D currentSkybox;
    private Color currentColor = Color.white;

    private void Start()
    {
        StartDay();
    }

    // Initializes every time-of-day-driven environment element (sun rotation, skybox, light
    // color, fog color) to match whatever hours/minutes the scene starts at - not just the
    // sun's rotation. Without this, starting the scene at e.g. hour 14 would still show
    // whatever skybox/light color was left in the Inspector, since OnHoursChange only reacts
    // to the exact hour a transition begins, not to an arbitrary starting hour.
    private void StartDay()
    {
        UpdateSunRotation(hours, minutes);
        SetEnvironmentForHour(hours);
    }

    // Snaps the skybox and light/fog color straight to the steady-state look for whatever
    // period `hour` falls in (no lerp - this is for instant initialization, not a live
    // transition; OnHoursChange still handles the animated transitions between periods
    // during play).
    private void SetEnvironmentForHour(int hour)
    {
        TimePeriod period = GetPeriodForHour(hour);
        if (period == null) return;

        RenderSettings.skybox.SetTexture("_Texture1", period.skybox);
        RenderSettings.skybox.SetTexture("_Texture2", period.skybox);
        RenderSettings.skybox.SetFloat("_Blend", 0f);

        globalLight.color = period.color;
        RenderSettings.fogColor = period.color;

        currentSkybox = period.skybox;
        currentColor = period.color;
    }

    // timePeriods must be sorted ascending by startHour. The active period is the last one
    // whose startHour is <= hour; if hour is earlier than every period's startHour (e.g. 2 AM
    // with the first period starting at 5), it belongs to the last period, since that's the one
    // still running from before midnight.
    private TimePeriod GetPeriodForHour(int hour)
    {
        if (timePeriods == null || timePeriods.Length == 0) return null;

        TimePeriod result = timePeriods[timePeriods.Length - 1];
        foreach (TimePeriod period in timePeriods)
        {
            if (period.startHour <= hour)
            {
                result = period;
            }
            else
            {
                break;
            }
        }
        return result;
    }

    private void UpdateSunRotation(int currentHours, int currentMinutes)
    {
        // dayFraction 0 -> X=-90 (nadir, sun at the bottom, i.e. hours=0/minutes=0 = midnight).
        // dayFraction 0.25 -> X=0 (horizon/sunrise). dayFraction 0.5 -> X=90 (zenith/noon).
        // dayFraction 0.75 -> X=180 (horizon/sunset).
        float dayFraction = (currentHours * 60 + currentMinutes) / 1440f;
        globalLight.transform.localRotation = Quaternion.Euler(new Vector3((dayFraction * 360f) - 90f, 170, 0));

        // Intensity follows how high the sun actually is, read straight off the rotation we
        // just set instead of a separate formula: forward points straight down at noon
        // (-forward.y = 1), horizontal at the horizon (0), and straight up from below the
        // world at midnight (-forward.y negative, clamped to 0). This is exactly sin(elevation)
        // for this rotation setup - the standard real-world approximation for how much direct
        // sunlight a surface receives - so night reaches a true 0 and noon a true 1.
        globalLight.intensity = Mathf.Clamp01(-globalLight.transform.forward.y);
    }

    private void Update(){
        tempSecond += Time.deltaTime * timeScale;
        if(tempSecond > 1){
            Minutes++;
            tempSecond = 0;
        }
    }

    private void OnMinutesChange(int value)
    {
        UpdateSunRotation(hours, value);
        if (value >= 60){
            Hours++;
            minutes = 0;
        }
        if(Hours >= 24){
            Days++;
            hours = 0;
        }
    }

    private void OnHoursChange(int value) {
        TimePeriod period = null;
        foreach (TimePeriod candidate in timePeriods)
        {
            if (candidate.startHour == value)
            {
                period = candidate;
                break;
            }
        }
        if (period == null) return;

        StartCoroutine(LerpSkyBox(currentSkybox, period.skybox, speedChange));
        StartCoroutine(LerpColor(currentColor, period.color, speedChange));
        currentSkybox = period.skybox;
        currentColor = period.color;
    }

    private IEnumerator LerpSkyBox(Texture2D a, Texture2D b, float time) {
        RenderSettings.skybox.SetTexture("_Texture1", a);
        RenderSettings.skybox.SetTexture("_Texture2", b);
        RenderSettings.skybox.SetFloat("_Blend", 0);
        for (float t = 0; t < time; t += Time.deltaTime) {
            RenderSettings.skybox.SetFloat("_Blend", Mathf.Lerp(0, 1, t / time));
            yield return null;
        }
        RenderSettings.skybox.SetTexture("_Texture1", b);
    }

    private IEnumerator LerpColor(Color from, Color to, float time) {
        for (float t = 0; t < time; t += Time.deltaTime) {
            Color c = Color.Lerp(from, to, t / time);
            globalLight.color = c;
            RenderSettings.fogColor = c;
            yield return null;
        }
        globalLight.color = to;
        RenderSettings.fogColor = to;
    }
}
