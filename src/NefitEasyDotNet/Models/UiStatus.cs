using System;
using System.Globalization;
using System.Linq;
using NefitEasyDotNet.Models.Internal;

namespace NefitEasyDotNet.Models
{
    public class UiStatus
    {
        public ClockProgram ClockProgram { get; }
        public InHouseStatus InHouseStatus { get; }
        public double InHouseTemperature { get; }
        public BoilerIndicator BoilerIndicator { get; }
        public ControlMode Control { get; }
        public double TempOverrideDuration { get; }
        public int CurrentProgramSwitch { get; }
        public bool PowerSaveMode { get; }
        public bool FireplaceMode { get; }
        public bool HotWaterAvailable { get; }
        public bool TempOverride { get; }
        public bool HolidayMode { get; }
        public bool BoilerBlock { get; }
        public bool DayAsSunday { get; }
        public bool BoilerLock { get; }
        public bool BoilerMaintenance { get; }
        public double TemperatureSetpoint { get; }
        public double TemperatureOverrideSetpoint { get; }
        public double TemparatureManualSetpoint { get; }
        public bool HedEnabled { get; }
        string HedDeviceName { get; }
        public bool HedDeviceAtHome { get; }
        public UserModes UserMode { get; }
        internal UiStatus(NefitStatus stat)
        {
            UserMode = (UserModes)Enum.Parse(typeof(UserModes), stat.UMD, true);
            ClockProgram = (ClockProgram)Enum.Parse(typeof(ClockProgram), stat.CPM, true);
            InHouseStatus = (InHouseStatus)Enum.Parse(typeof(InHouseStatus), stat.IHS, true);
            Control = (ControlMode)Enum.Parse(typeof(ControlMode), stat.CTR, true);
            BoilerIndicator = EnumHelper.ToArray<BoilerIndicator>().FirstOrDefault(bi => (int)bi == (int)(BoilerIndicatorRef)Enum.Parse(typeof(BoilerIndicatorRef), stat.BAI, true));
            InHouseTemperature = double.Parse(stat.IHT, CultureInfo.InvariantCulture);
            TempOverrideDuration = double.Parse(stat.TOD, CultureInfo.InvariantCulture);
            CurrentProgramSwitch = Convert.ToInt32(stat.CSP);
            PowerSaveMode = NefitEasyUtils.IsOnOrTrue(stat.ESI);
            FireplaceMode = NefitEasyUtils.IsOnOrTrue(stat.FPA);
            TempOverride = NefitEasyUtils.IsOnOrTrue(stat.TOR);
            HolidayMode = NefitEasyUtils.IsOnOrTrue(stat.HMD);
            BoilerBlock = NefitEasyUtils.IsOnOrTrue(stat.BBE);
            DayAsSunday = NefitEasyUtils.IsOnOrTrue(stat.DAS);
            BoilerLock = NefitEasyUtils.IsOnOrTrue(stat.BLE);
            BoilerMaintenance = NefitEasyUtils.IsOnOrTrue(stat.BMR);
            TemperatureSetpoint = double.Parse(stat.TSP, CultureInfo.InvariantCulture);
            TemperatureOverrideSetpoint = double.Parse(stat.TOT, CultureInfo.InvariantCulture);
            TemparatureManualSetpoint = double.Parse(stat.MMT, CultureInfo.InvariantCulture);
            HedEnabled = NefitEasyUtils.IsOnOrTrue(stat.HED_EN);
            HedDeviceAtHome = NefitEasyUtils.IsOnOrTrue(stat.HED_DEV);
            HotWaterAvailable = NefitEasyUtils.IsOnOrTrue(stat.DHW);
            HedDeviceName = stat.HED_DB;
        }
    }
}