using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using NefitEasyDotNet.Models;
using NefitEasyDotNet.Models.Internal;

namespace NefitEasyDotNet
{
    public interface INefitEasyClient
    {
        /// <summary>
        /// This event will occur when theres an exception
        /// </summary>
        event EventHandler<Exception> ExceptionEvent;

        /// <summary>
        /// This event will occur on XML write events
        /// </summary>
        event EventHandler<XmlDocument> XmlWriteEvent;

        /// <summary>
        /// This event will occur on XML read events
        /// </summary>
        event EventHandler<XmlDocument> XmlReadEvent;

        /// <summary>
        /// This event will occur on Http get/put events
        /// </summary>
        event EventHandler<string> HttpRequestEvent;

        /// <summary>
        /// This event will occur when the connectionstatus changed.
        /// </summary>
        event EventHandler<NefitConnectionStatus> ConnectionStatusChangedEvent;

        /// <summary>
        /// Returns the current connection status
        /// </summary>
        NefitConnectionStatus ConnectionStatus { get; }

        /// <summary>        
        /// Starts the communication to the Bosch server backend with the credentials provided.    
        /// Subscribe to the <see cref="ConnectionStatusChangedEvent"/> to get notified when the client is successfully connected  
        /// </summary>   
        Task ConnectAsync();
        void Connect();

        /// <summary>        
        /// Disconnects from the Bosch server       
        /// </summary>    
        Task DisconnectAsync();
        void Disconnect();

        /// <summary>
        /// Returns the active user program, can only be 0, 1 or 2
        /// Use this in the <see cref="ProgramAsync"/> command to get the active program
        /// </summary>
        /// <returns>A value between 0 and 2, or <see cref="int.MinValue"/> if the command fails</returns>
        Task<int> GetActiveProgramAsync();
        int GetActiveProgram();

        /// <summary>
        /// Returns the requested user program defined in switch points
        /// </summary>
        /// <param name="programNumber">The program number which to request from the Easy</param>
        /// <returns>An array of ProgramSwitches (converted to the next timestamp of the switch) or null if the command fails</returns>
        Task<IEnumerable<ProgramSwitch>> GetProgramAsync(int programNumber);
        IEnumerable<ProgramSwitch> GetProgram(int programNumber);

        /// <summary>
        /// Returns the user definable switch point names, there are 2 custom names configurable
        /// </summary>
        /// <param name="nameIndex">The index of the name, can only be 0 or 1</param>
        /// <returns>The name of the switchpoint or null if the command fails</returns>
        Task<string> GetSwitchpointNameAsync(int nameIndex);
        string GetSwitchpointName(int nameIndex);

        /// <summary>
        /// Indicates if the fireplace function is currently activated
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> FireplaceFunctionActiveAsync();
        bool? FireplaceFunctionActive();

        /// <summary>
        /// Indicates if the preheating setting is active
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> PreheatingActiveAsync();
        bool? PreheatingActive();

        /// <summary>
        /// Returns the outdoor temperature measured by the Easy or collected over the internet
        /// </summary>
        /// <returns>The outdoor temperature or <see cref="Double.NaN"/> if the command fails</returns>
        Task<double> GetOutdoorTemperatureAsync();
        double GetOutdoorTemperature();

        /// <summary>
        /// Inidicates the Easy service status
        /// </summary>
        /// <returns>Returns a string containing the Easy service status or null if the command fails</returns>
        Task<string> GetEasyServiceStatusAsync();
        string GetEasyServiceStatus();

        /// <summary>
        /// Returns the status of the ignition (presumably if the Central Heating is heating)
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> IgnitionStatusAsync();
        bool? IgnitionStatus();

        /// <summary>
        /// Returns the status of the Central Heating circuit, if a refill is needed
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> RefillNeededStatusAsync();
        bool? RefillNeededStatus();

        /// <summary>
        /// Unknown
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> ClosingValveStatusAsync();
        bool? ClosingValveStatus();

        /// <summary>
        /// Unknown
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> ShortTappingStatusAsync();
        bool? ShortTappingStatus();

        /// <summary>
        /// Indiciates if the Easy detected a leak
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> SystemLeakingStatusAsync();
        bool? SystemLeakingStatus();

        /// <summary>
        /// Indiciates if the thermal desinfect program is enabled
        /// </summary>
        /// <returns>True/false or null if the command fails</returns>
        Task<bool?> ThermalDesinfectEnabledAsync();
        bool? ThermalDesinfectEnabled();

        /// <summary>
        /// Returns the timestamp of the next scheduled thermal desinfect.
        /// </summary>
        /// <returns>The date/time of the next thermal desinfect or a datetime with 0 ticks if the command fails</returns>
        Task<DateTime> GetNextThermalDesinfectAsync();
        DateTime GetNextThermalDesinfect();

        /// <summary>
        /// Returns the current pressure of the Central Heating circuit
        /// </summary>
        /// <returns>The presure of the Central Heating circuit or <see cref="Double.NaN"/> if the command fails</returns>
        Task<double> GetSystemPressureAsync();
        double GetSystemPressure();

        /// <summary>
        /// Returns the current tempreature of the supply temperature of the Central Heating circuit
        /// </summary>
        /// <returns>The presure of the Central Heating circuit or <see cref="Double.NaN"/> if the command fails</returns>
        Task<double> GetCentralHeatingSupplyTemperatureAsync();
        double GetCentralHeatingSupplyTemperature();

        /// <summary>
        /// Returns the current status of the central heating, note; the descriptions are in Dutch
        /// </summary>
        /// <returns>The current status of the central heating or null if the command fails</returns>
        Task<StatusCode> GetStatusCodeAsync();
        StatusCode GetStatusCode();

        /// <summary>
        /// Returns the current switch point (in other words what the Central Heating is currently doing)
        /// </summary>
        /// <returns>The current switch point of the central heating or null if the command fails</returns>
        Task<ProgramSwitch> GetCurrentSwitchPointAsync();
        ProgramSwitch GetCurrentSwitchPoint();

        /// <summary>
        /// Returns the next switch point (in other words what the Central Heating will be doing)
        /// </summary>
        /// <returns>The current switch point of the central heating or null if the command fails</returns>
        Task<ProgramSwitch> GetNextSwitchPointAsync();
        ProgramSwitch GetNextSwitchPoint();

        /// <summary>
        /// Returns the location of the Easy device
        /// </summary>
        /// <returns>Location of the easy device, or null if the command fails</returns>
        Task<Location> GetLocationAsync();
        Location GetLocation();

        /// <summary>
        /// Returns all daily gas usage samples collected by the Easy device
        /// </summary>
        /// <returns>An IEnumerable of gas samples</returns>
        Task<IEnumerable<GasSample>> GetGasUsageAsync();
        IEnumerable<GasSample> GetGasUsage();

        /// <summary>
        /// Returns the overall status presented in the UI
        /// </summary>
        /// <returns>The UI status, or null if the command fails</returns>
        Task<UiStatus> GetUiStatusAsync();
        UiStatus GetUiStatus();

        /// <summary>
        /// Returns the owner information filled in on the Easy app
        /// </summary>
        /// <returns>Returns an IEnumerable of the following items: Name/Phone number/Email address, or null if the command fails</returns>
        Task<IEnumerable<string>> GetOwnerInfoAsync();
        IEnumerable<string> GetOwnerInfo();

        /// <summary>
        /// Returns the installer information filled in on the Easy app
        /// </summary>
        /// <returns>Returns an IEnumerable of the following items: Name/Company/Telephone number/Email address, or null if the command fails</returns>
        Task<IEnumerable<string>> GetInstallerInfoAsync();
        IEnumerable<string> GetInstallerInfo();

        /// <summary>
        /// Returns the serial number of the Nefit Easy thermostat, this is not the serial number you enter for communication
        /// Use <see cref="GetEasyUuidAsync"/> for that
        /// </summary>
        /// <returns>The serial number of the Nefit Easy thermostat, or null if the command fails</returns>
        Task<string> GetEasySerialAsync();
        string GetEasySerial();

        /// <summary>
        /// Returns the firmware version of the Nefit Easy thermostat
        /// </summary>
        /// <returns>Firmware version or null if the command fails</returns>
        Task<string> GetEasyFirmwareAsync();
        string GetEasyFirmware();

        /// <summary>
        /// Returns the hardware revision of the Nefit Easy thermostat
        /// </summary>
        /// <returns>Hardware revision or null if the command fails</returns>
        Task<string> GetEasyHardwareAsync();
        string GetEasyHardware();

        /// <summary>
        /// Returns the UUID of the Easy thermostat, this is the number you enter in as serial when connecting
        /// </summary>
        /// <returns>The UUID of the Nefit Easy thermostat, or null if the command fails</returns>
        Task<string> GetEasyUuidAsync();
        string GetEasyUuid();

        /// <summary>       
        /// Returns the way the Easy is updated
        /// </summary>       
        /// <returns>The way the Easy is updated, or null if the command fails</returns>
        Task<EasyUpdateStrategy> GetEasyUpdateStrategyAsync();
        EasyUpdateStrategy GetEasyUpdateStrategy();

        /// <summary>
        /// Returns the serial of the central heating appliance
        /// </summary>
        /// <returns>The serial of the central heating appliance, or null if the command fails</returns>
        Task<string> GetCentralHeatingSerialAsync();
        string GetCentralHeatingSerial();

        /// <summary>
        /// Returns the version of the central heating appliance
        /// </summary>
        /// <returns>The version of the central heating appliance, or null if the command fails</returns>
        Task<string> GetCentralHeatingVersionAsync();
        string GetCentralHeatingVersion();

        /// <summary>
        /// Returns the make of the burner in the central heating appliance 
        /// </summary>
        /// <returns>The make of the burner in the central heating appliance, or null if the command fails</returns>
        Task<string> GetCentralHeatingBurnerMakeAsync();
        string GetCentralHeatingBurnerMake();

        /// <summary>
        /// Returns the proximity setting of the Nefit Easy
        /// </summary>
        /// <returns>The proximity setting of the Easy or <see cref="EasySensitivity.Unknown"/> if the command fails</returns>
        Task<EasySensitivity> GetEasySensitivityAsync();
        EasySensitivity GetEasySensitivity();

        /// <summary>
        /// Returns the temperature step setting when changing setpoints 
        /// </summary>
        /// <returns>The temperature step setting for setpoints or <see cref="Double.NaN"/> if the command fails</returns>
        Task<double> GetEasyTemperatureStepAsync();
        double GetEasyTemperatureStep();

        /// <summary>
        /// Returns the room temperature offset setting used by the Nefit Easy 
        /// </summary>
        /// <returns>The room temperature offset setting or <see cref="double.NaN"/> if the command fails</returns>
        Task<double> GetEasyTemperatureOffsetAsync();
        double GetEasyTemperatureOffset();

        /// <summary>
        /// Changes the hot water mode to on or off when the Easy is in clock program mode
        /// </summary>
        /// <param name="onOff">True if the hotwater needs to be turned on, false if off</param>
        /// <returns>True if the command succeeds, false if it fails</returns>
        Task<bool> SetHotWaterModeClockProgramAsync(bool onOff);
        bool SetHotWaterModeClockProgram(bool onOff);

        /// <summary>
        /// Changes the hot water mode to on or off when the Easy is in manual program mode
        /// </summary>
        /// <param name="onOff">True if the hotwater needs to be turned on, false if off</param>
        /// <returns>True if the command succeeds, false if it fails</returns>
        Task<bool> SetHotWaterModeManualProgramAsync(bool onOff);
        bool SetHotWaterModeManualProgram(bool onOff);

        /// <summary>
        /// Switches the Easy between manual and program mode
        /// </summary>
        /// <param name="newMode">Use <see cref="UserModes.Manual"/> to switch to manual mode, use <see cref="UserModes.Clock"/> to switch to program mode, <see cref="UserModes.Unknown"/> is not supported</param>
        /// <returns>True if the command succeeds, false if it fails</returns>
        Task<bool> SetUserModeAsync(UserModes newMode);
        bool SetUserMode(UserModes newMode);

        /// <summary>
        /// Changes the setpoint temperature
        /// </summary>
        /// <param name="temperature">The new temperature (in degrees celcius). The new setpoint must be between 5 and 30 degrees celcius</param>
        /// <returns>True if the command succeeds, false if it fails or if the setpoint is not between 5 and 30 degrees celcius</returns>
        Task<bool> SetTemperatureAsync(double temperature);
        bool SetTemperature(double temperature);
    }
}
