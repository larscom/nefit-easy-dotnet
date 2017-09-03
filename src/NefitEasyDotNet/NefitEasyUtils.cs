using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NefitEasyDotNet.Models;
using NefitEasyDotNet.Models.Internal;

namespace NefitEasyDotNet
{
    public static class NefitEasyUtils
    {
        public static IEnumerable<ProgramSwitch> ParseProgram(IEnumerable<NefitProgram> progs)
        {
            var programs = new List<ProgramSwitch>();
            programs.AddRange(progs.Where(x => x.active.Equals("on")).Select(prog => new ProgramSwitch(prog)));
            programs.Sort((p1, p2) => p1.Timestamp.CompareTo(p2.Timestamp));
            return programs;
        }

        public static bool IsOnOrTrue(string status) => !string.IsNullOrEmpty(status) && status == "on" || status == "true";

        public static string GetHttpPutDataString(string value) => "{\"value\":'" + value.ToLowerInvariant() + "'}";

        public static string GetHttpPutDataString(double value) => "{\"value\":" + value.ToString(CultureInfo.InvariantCulture) + "}";

        public static DateTime GetNextDate(string day, int time)
        {
            DayOfWeek dwi;
            switch (day)
            {
                case "Mo":
                    dwi = DayOfWeek.Monday;
                    break;
                case "Tu":
                    dwi = DayOfWeek.Tuesday;
                    break;
                case "We":
                    dwi = DayOfWeek.Wednesday;
                    break;
                case "Th":
                    dwi = DayOfWeek.Thursday;
                    break;
                case "Fr":
                    dwi = DayOfWeek.Friday;
                    break;
                case "Sa":
                    dwi = DayOfWeek.Saturday;
                    break;
                default:
                    dwi = DayOfWeek.Sunday;
                    break;
            }
            var now = DateTime.Today;
            while (now.DayOfWeek != dwi)
            {
                now = now.AddDays(1);
            }
            now = now.AddMinutes(time);
            return now;
        }
    }
}