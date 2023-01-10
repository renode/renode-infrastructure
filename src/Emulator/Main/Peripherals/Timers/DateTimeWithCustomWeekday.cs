
//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class DateTimeWithCustomWeekday
    {
        public static DateTimeWithCustomWeekday FromDateTime(DateTime dt)
        {
            return new DateTimeWithCustomWeekday
            {
                currentTime = dt
            };
        }

        public void AddSeconds(int s)
        {
            currentTime = currentTime.AddSeconds(s);
        }

        public int Second
        {
            get => currentTime.Second;
            set
            {
                if(value < 0  || value > 59)
                {
                    throw new ArgumentException($"Seconds value out of range: {value}");
                }

                currentTime = new DateTime(Year, Month, Day, Hour, Minute, value);
            }
        }

        public int Minute
        {
            get => currentTime.Minute;
            set
            {
                if(value < 0  || value > 59)
                {
                    throw new ArgumentException($"Minutes value out of range: {value}");
                }

                currentTime = new DateTime(Year, Month, Day, Hour, value, Second);
            }
        }

        public int Hour
        {
            get => currentTime.Hour;
            set
            {
                if(value < 0  || value > 23)
                {
                    throw new ArgumentException($"Hours value out of range: {value}");
                }

                currentTime = new DateTime(Year, Month, Day, value, Minute, Second);
            }
        }

        public int Day
        {
            get => currentTime.Day;
            set
            {
                if(value < 1  || value > 31)
                {
                    throw new ArgumentException($"Day value out of range: {value}");
                }

                currentTime = new DateTime(Year, Month, value, Hour, Minute, Second);
                UpdateWeekdayOffset();
            }
        }

        public int Month
        {
            get => currentTime.Month;
            set
            {
                if(value < 1  || value > 12)
                {
                    throw new ArgumentException($"Month value out of range: {value}");
                }

                currentTime = new DateTime(Year, value, Day, Hour, Minute, Second);
                UpdateWeekdayOffset();
            }
        }

        public int Year
        {
            get => currentTime.Year;
            set
            {
                currentTime = new DateTime(value, Month, Day, Hour, Minute, Second);
                UpdateWeekdayOffset();
            }
        }

        public DayOfWeek Weekday
        {
            get
            {
                return currentTime.DayOfWeek - weekdayOffset;
            }
            set
            {
                currentWeekday = value;
                UpdateWeekdayOffset();
            }
        }

        private void UpdateWeekdayOffset()
        {
            weekdayOffset = currentTime.DayOfWeek - currentWeekday;
        }

        private int weekdayOffset;
        private DayOfWeek currentWeekday;
        private DateTime currentTime;
    }
}
