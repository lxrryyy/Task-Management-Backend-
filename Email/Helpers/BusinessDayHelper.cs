namespace TaskManagement.Helpers
{
    public static class BusinessDayHelper
    {
        private static readonly Dictionary<int, double> StoryPointHours = new()
            {
                { 1,  2  },   // XXS — 1–2 hrs    
                { 2,  4  },   // XS  — 3–4 hrs    
                { 3,  8  },   // S   — 6–8 hrs    
                { 5,  16 },   // M   — 12–16 hrs  
                { 8,  32 },   // L   — 3–4 days   
                { 13, 45 },   // XL  — 1+ week 
                { 21, 90 },   // XXL — 2+ weeks        
            }; // fibonacci sequence meanings

        public static double GetHoursForStoryPoints(int storyPoints)
        {
            return StoryPointHours.TryGetValue(storyPoints, out var hours) ? hours : 0;
        }
        public static DateTime AddBusinessDays(DateTime startDate, int businessDays)
        {
            if (businessDays < 0)
                throw new ArgumentOutOfRangeException(nameof(businessDays),
                    "Business days must be non-negative.");

            DateTime current = SnapToNextBusinessDay(startDate);

            int daysAdded = 0;
            while (daysAdded < businessDays)
            {
                current = current.AddDays(1);
                if (IsBusinessDay(current))
                    daysAdded++;
            }

            return current;
        }

        public static DateTime CalculateDueDateFromStoryPoints(
            DateTime startTime,
            int storyPoints,
            int workdayStartHour = 9,  //9AM Start Odecci Office Hours
            int workdayEndHour = 18)   //6PM End Odecci Office Hours
        {
            double totalHours = StoryPointHours[storyPoints];

            DateTime cursor = SnapToNextBusinessDay(startTime);

            if (cursor.Hour < workdayStartHour)
                cursor = cursor.Date.AddHours(workdayStartHour);

            if (cursor.Hour >= workdayEndHour)
                cursor = AddBusinessDays(cursor.Date, 1).Date.AddHours(workdayStartHour);

            double remainingHours = totalHours;

            while (remainingHours > 0)
            {
                double hoursLeftToday = workdayEndHour - cursor.Hour - (cursor.Minute / 60.0);

                if (remainingHours <= hoursLeftToday)
                {
                    cursor = cursor.AddHours(remainingHours);
                    remainingHours = 0;
                }
                else
                {
                    remainingHours -= hoursLeftToday;
                    cursor = AddBusinessDays(cursor.Date, 1).Date.AddHours(workdayStartHour);
                }
            }

            return RoundUpToHalfDayBoundary(cursor, workdayStartHour, workdayEndHour);
        }

        public static bool IsBusinessDay(DateTime date) =>
            date.DayOfWeek != DayOfWeek.Saturday &&
            date.DayOfWeek != DayOfWeek.Sunday;

        private static DateTime SnapToNextBusinessDay(DateTime date)
        {
            while (!IsBusinessDay(date))
                date = date.AddDays(1);
            return date;
        }

        private static DateTime RoundUpToHalfDayBoundary(
            DateTime dateTime,
            int workdayStartHour,
            int workdayEndHour)
        {
            int midpoint = workdayStartHour + (workdayEndHour - workdayStartHour) / 2;

            if (dateTime.Hour < midpoint)
                return dateTime.Date.AddHours(midpoint);

            if (dateTime.Hour == midpoint && dateTime.Minute > 0)
                return dateTime.Date.AddHours(workdayEndHour);

            if (dateTime.Hour > midpoint && dateTime.Hour < workdayEndHour)
                return dateTime.Date.AddHours(workdayEndHour);

            return dateTime;
        }
    }
}