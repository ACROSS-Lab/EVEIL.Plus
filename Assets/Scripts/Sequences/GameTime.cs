using NaughtyAttributes;
using System;
using TMPro;
using UnityEngine;

[Serializable]
public struct GameTime
{
    [Range(1, 12)] public int month;
    [Range(1, 30)] public int day;   
    [Range(0, 23)] public int hour; 

    public static readonly string[] monthKeys = new string[] 
    {
        "month.January", 
        "month.February",
        "month.March",
        "month.April",
        "month.May", 
        "month.June", 
        "month.July", 
        "month.August", 
        "month.September", 
        "month.October", 
        "month.November",
        "month.December"
    };

    public static readonly int[] daysPerMonth = new int[] 
    {
        31, // January (Index 0)
        28, // February (Index 1)
        31, // March (Index 2)
        30, // April (Index 3)
        31, // May (Index 4)
        30, // June (Index 5)
        31, // July (Index 6)
        31, // August (Index 7)
        30, // September (Index 8)
        31, // October (Index 9)
        30, // November (Index 10)
        31  // December (Index 11)
    };

    public float ToTotalHours()
    {
        int totalDaysPassed = 0;
        
        for (int i = 0; i < month - 1; i++)
        {
            totalDaysPassed += daysPerMonth[i];
        }

        totalDaysPassed += day - 1;

        return (totalDaysPassed * 24) + hour;
    }

    public static GameTime FromTotalHours(float totalHours)
    {
        GameTime time = new GameTime();
        int totalHoursInt = Mathf.FloorToInt(totalHours);

        int totalDays = totalHoursInt / 24;
        int remainingHours = totalHoursInt % 24;

        int monthIndex = 0;
        
        while (monthIndex < 11 && totalDays >= daysPerMonth[monthIndex])
        {
            totalDays -= daysPerMonth[monthIndex];
            monthIndex++;
        }

        time.month = monthIndex + 1;
        time.day = totalDays + 1;
        time.hour = remainingHours;

        return time;
    }
}