using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NefitEasyDotNet
{
    public enum DocumentElement
    {
        Message = 0,
        Presence = 1,
        Failure = 2
    }

    public enum Switch
    {
        On,
        Off
    }

    public enum NefitConnectionStatus
    {
        Disconnected = 0,
        Connecting = 1,
        Authenticating = 2,
        AuthenticationTest = 3,
        InvalidSerialAccessKey = 4,
        InvalidPassword = 5,
        Connected = 6,
        Disconnecting = 7
    }

    public enum EasyUpdateStrategy
    {
        Unknown = 0,
        Automatic = 1
    }

    public enum EasySensitivity
    {
        Unknown = 0,
        Disabled = 1,
        Low = 2,
        High = 3
    }

    public enum OperationModes
    {
        Unknown = 0,
        SelfLearning = 1
    }

    public enum BoilerIndicator
    {
        Unknown = 0,
        Off = 1,
        CentralHeating = 2,
        HotWater = 3
    }

    public enum BoilerIndicatorRef
    {
        Unknown = 0,
        No = 1,
        Ch = 2,
        Hw = 3
    }

    public enum ClockProgram
    {
        Unknown = 0,
        Auto = 1,
        SelfLearning = 2
    }

    public enum ControlMode
    {
        Unknown = 0,
        Room = 1
    }

    public enum InHouseStatus
    {
        Unknown = 0,
        Ok = 1
    }

    public enum ProgramName
    {
        Unknown = 0,
        Sleep = 1,
        Awake = 2,
        LeaveHome = 3,
        Home = 4,
        OtherPeriod1 = 5,
        OtherPeriod2 = 6
    }

    public enum ServiceStatus
    {
        Unknown = 0,
        NoService = 1
    }

    public enum UserModes
    {
        Unknown = 0,
        Manual = 1,
        Clock = 2
    }
}
