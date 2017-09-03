using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using agsXMPP;
using NefitEasyDotNet.Models;
using NefitEasyDotNet.Models.Internal;
using Newtonsoft.Json;

namespace NefitEasyDotNet
{
    public sealed class NefitEasyClient : INefitEasyClient
    {
        public event EventHandler<Exception> ExceptionEvent;
        public event EventHandler<XmlDocument> XmlWriteEvent;
        public event EventHandler<XmlDocument> XmlReadEvent;
        public event EventHandler<string> HttpRequestEvent;
        public event EventHandler<NefitConnectionStatus> ConnectionStatusChangedEvent;

        public NefitConnectionStatus ConnectionStatus { get; private set; }

        const string CHost = "wa2-mz36-qrmzh6.bosch.de";
        const string CAccesskeyPrefix = "Ct7ZR03b_";
        const string CRrcContactPrefix = "rrccontact_";
        const string CRrcGatewayPrefix = "rrcgateway_";

        const int CRequestTimeout = 35 * 1000;
        const int CCheckInterval = 100;
        const int CKeepAliveInterval = 30 * 1000;

        XmppClientConnection _client;
        readonly NefitEasyEncryption _encryptionHelper;
        readonly string _accessKey;

        readonly string _serial;
        NefitEasyHttpResponse _lastMessage;
        readonly object _lockObj;
        readonly object _communicationLock;

        NefitEasyClient(string serial, string accesskey, string password)
        {
            ConnectionStatus = NefitConnectionStatus.Disconnected;
            _lockObj = new object();
            _communicationLock = new object();
            _lastMessage = null;
            _serial = serial;
            _accessKey = accesskey;
            _encryptionHelper = new NefitEasyEncryption(accesskey, password);
        }

        void Authenticate()
        {
            if (GetEasyUuid() == _serial)
            {
                UpdateConnectionStatus(NefitConnectionStatus.Connected);
            }
            else
            {
                Disconnect();
            }
        }

        void XmppRead(object sender, string xml)
        {
            XmlDocument doc;

            if (TryParseXml(xml, out doc))
            {
                XmlReadEvent?.Invoke(this, doc);

                if (doc.DocumentElement == null) return;

                if (doc.DocumentElement.Name.Equals(nameof(DocumentElement.Message), StringComparison.InvariantCultureIgnoreCase))
                {
                    var header = new NefitEasyHttpResponse(doc.InnerText);
                    lock (_lockObj)
                    {
                        _lastMessage = header;
                    }
                }
                else if (doc.DocumentElement.Name.Equals(nameof(DocumentElement.Presence), StringComparison.InvariantCultureIgnoreCase))
                {
                    UpdateConnectionStatus(NefitConnectionStatus.AuthenticationTest);
                }
                else if (doc.DocumentElement.Name.Equals(nameof(DocumentElement.Failure), StringComparison.InvariantCultureIgnoreCase)
                    && doc.ChildNodes.Cast<XmlNode>().FirstOrDefault()?.FirstChild.Name == "not-authorized")
                {
                    UpdateConnectionStatus(NefitConnectionStatus.InvalidSerialAccessKey);
                }
            }
            else
            {
                UpdateConnectionStatus(NefitConnectionStatus.Authenticating);
            }
        }

        void XmppWrite(object sender, string xml)
        {
            XmlDocument doc;
            if (!TryParseXml(xml, out doc)) return;
            if (doc.DocumentElement == null) return;

            XmlWriteEvent?.Invoke(this, doc);

            if (doc.DocumentElement.Name.Equals(nameof(DocumentElement.Presence), StringComparison.InvariantCultureIgnoreCase))
            {
                UpdateConnectionStatus(NefitConnectionStatus.AuthenticationTest);
            }
        }

        bool HttpPut(string url, string data)
        {
            lock (_communicationLock)
            {
                try
                {
                    if (_client == null) return false;

                    var request = new NefitEasyHttpRequest(url, _client.MyJID, $"{CRrcGatewayPrefix}{_serial}@{CHost}", _encryptionHelper.Encrypt(data));
                    _client.Send(request.ToString());

                    HttpRequestEvent?.Invoke(this, $"HttpPut: {request}");
                    var timeout = CRequestTimeout;

                    while (timeout > 0)
                    {
                        lock (_lockObj)
                        {
                            if (_lastMessage != null)
                            {
                                var result = _lastMessage.Code == (int)HttpStatusCode.OK || _lastMessage.Code == (int)HttpStatusCode.NoContent;
                                if (result && ConnectionStatus != NefitConnectionStatus.Connected) UpdateConnectionStatus(NefitConnectionStatus.Connected);

                                _lastMessage = null;
                                return result;
                            }
                        }
                        timeout -= CCheckInterval;
                        Thread.Sleep(CCheckInterval);
                    }
                }
                catch (Exception e)
                {
                    ExceptionEvent?.Invoke(this, e);
                }
                return false;
            }
        }

        bool TryParseXml(string xml, out XmlDocument doc)
        {
            doc = new XmlDocument();
            try
            {
                doc.LoadXml(xml);
                return true;
            }
            catch (Exception)
            {
                //ignored
            }
            return false;
        }

        bool TryParseJsonString<T>(string json, out NefitJson<T> obj)
        {
            obj = null;
            try
            {
                obj = JsonConvert.DeserializeObject<NefitJson<T>>(json);
            }
            catch (Exception)
            {
                //ignored
            }
            return obj != null;
        }

        T HttpGet<T>(string url)
        {
            lock (_communicationLock)
            {
                try
                {
                    if (_client == null) return default(T);

                    var request = new NefitEasyHttpRequest(url, _client.MyJID, $"{CRrcGatewayPrefix}{_serial}@{CHost}");
                    _client.Send(request.ToString());

                    HttpRequestEvent?.Invoke(this, $"HttpGet->request: {request}");
                    var timeout = CRequestTimeout;

                    while (timeout > 0)
                    {
                        lock (_lockObj)
                        {
                            if (_lastMessage != null)
                            {
                                var decrypted = _encryptionHelper.Decrypt(_lastMessage.Payload);
                                HttpRequestEvent?.Invoke(this, $"HttpGet->decrypted: {decrypted}");

                                NefitJson<T> obj;

                                if (TryParseJsonString(decrypted, out obj))
                                {
                                    if (ConnectionStatus != NefitConnectionStatus.Connected) UpdateConnectionStatus(NefitConnectionStatus.Connected);
                                    _lastMessage = null;
                                    return obj.Value;
                                }

                                UpdateConnectionStatus(NefitConnectionStatus.InvalidPassword);
                                timeout = 0;
                            }
                        }
                        timeout -= CCheckInterval;
                        Thread.Sleep(CCheckInterval);
                    }
                }
                catch (Exception e)
                {
                    ExceptionEvent?.Invoke(this, e);
                }
                return default(T);
            }
        }


        /// <summary>
        /// Creates a new instance of the NefitEasyClient
        /// </summary>
        /// <param name="serial"></param>
        /// <param name="accesskey"></param>
        /// <param name="password"></param>
        /// <returns>Returns the NefitEasyClient interface</returns>
        public static INefitEasyClient Create(string serial, string accesskey, string password) => new NefitEasyClient(serial, accesskey, password);

        public async Task ConnectAsync() => await Task.Run(() => Connect());

        void Connect()
        {
            if (_client != null) Disconnect();

            try
            {
                UpdateConnectionStatus(NefitConnectionStatus.Connecting);

                lock (_lockObj)
                {
                    _client = new XmppClientConnection(CHost)
                    {
                        KeepAliveInterval = CKeepAliveInterval,
                        Resource = "",
                        AutoAgents = false,
                        AutoRoster = true,
                        AutoPresence = true
                    };

                    _client.OnReadXml += XmppRead;
                    _client.OnWriteXml += XmppWrite;
                    _client.Open(CRrcContactPrefix + _serial, CAccesskeyPrefix + _accessKey);
                }

                var countDown = CRequestTimeout;
                while ((ConnectionStatus == NefitConnectionStatus.Connecting || ConnectionStatus == NefitConnectionStatus.Authenticating) && countDown >= 0)
                {
                    countDown -= CCheckInterval;
                    Thread.Sleep(CCheckInterval);
                }

                if (ConnectionStatus == NefitConnectionStatus.AuthenticationTest)
                {
                    Authenticate();
                }
                else
                {
                    Disconnect();
                }
            }
            catch (Exception e)
            {
                ExceptionEvent?.Invoke(this, e);
                Disconnect();
            }
        }

        public async Task DisconnectAsync() => await Task.Run(() => Disconnect());

        void Disconnect()
        {
            try
            {
                UpdateConnectionStatus(NefitConnectionStatus.Disconnecting);
                if (_client == null) return;

                lock (_lockObj)
                {
                    _client.OnReadXml -= XmppRead;
                    _client.OnWriteXml -= XmppWrite;
                    _client.Close();
                    _client = null;
                    _lastMessage = null;
                }
            }
            catch (Exception e)
            {
                ExceptionEvent?.Invoke(this, e);
            }
            finally
            {
                UpdateConnectionStatus(NefitConnectionStatus.Disconnected);
            }
        }

        void UpdateConnectionStatus(NefitConnectionStatus status)
        {
            ConnectionStatus = status;
            ConnectionStatusChangedEvent?.Invoke(this, status);

            if (status == NefitConnectionStatus.InvalidPassword || status == NefitConnectionStatus.InvalidSerialAccessKey) Disconnect();
        }

        public async Task<int> ActiveProgramAsync() => await Task.Run(() => GetActiveProgram());

        int GetActiveProgram() => HttpGet<int>(EndpointPaths.USER_PROGRAM_ACTIVE_PROGRAM_ENDPOINT_PATH);

        public async Task<IEnumerable<ProgramSwitch>> ProgramAsync(int programNumber) => await Task.Run(() => GetProgram(programNumber));

        IEnumerable<ProgramSwitch> GetProgram(int programNumber)
        {
            if (programNumber < 0 || programNumber >= 3) return null;

            var program0 = HttpGet<IEnumerable<NefitProgram>>($"{EndpointPaths.USER_PROGRAM_PROGRAM_ENDPOINT_PATH}{programNumber}");

            return !program0.Any() ? null : NefitEasyUtils.ParseProgram(program0);
        }

        public async Task<string> SwitchpointNameAsync(int nameIndex) => await Task.Run(() => GetSwitchpointName(nameIndex));

        string GetSwitchpointName(int nameIndex)
        {
            if (--nameIndex >= 1 && nameIndex <= 2)
                return HttpGet<string>($"{EndpointPaths.USER_PROGRAM_USER_SWITCHPOINT_NAME_ENDPOINT_PATH}{nameIndex}");

            return null;
        }

        public async Task<bool?> FireplaceFunctionActiveAsync() => await Task.Run(() => GetFireplaceFunctionActive());

        bool? GetFireplaceFunctionActive() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.USER_PROGRAM_PREHEATING_ENDPOINT_PATH));

        public async Task<bool?> PreheatingActiveAsync() => await Task.Run(() => GetPreheatingActive());

        bool? GetPreheatingActive() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.USER_PROGRAM_PREHEATING_ENDPOINT_PATH));

        public async Task<double> OutdoorTemperatureAsync() => await Task.Run(() => GetOutdoorTemperature());

        double GetOutdoorTemperature() => HttpGet<double>(EndpointPaths.SENSORS_TEMPERATURES_OUTDOOR_ENDPOINT_PATH);

        public async Task<string> EasyServiceStatusAsync() => await Task.Run(() => GetEasyServiceStatus());

        string GetEasyServiceStatus() => HttpGet<string>(EndpointPaths.REMOTE_SERVICESTATE_ENDPOINT_PATH);

        public async Task<bool?> IgnitionStatusAsync() => await Task.Run(() => GetIgnitionStatus());

        bool? GetIgnitionStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_IGNITION_STATUS_ENDPOINT_PATH));

        public async Task<bool?> RefillNeededStatusAsync() => await Task.Run(() => GetRefillNeededStatus());

        bool? GetRefillNeededStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_REFILL_NEEDED_STATUS_ENDPOINT_PATH));

        public async Task<bool?> ClosingValveStatusAsync() => await Task.Run(() => GetClosingValveStatus());

        bool? GetClosingValveStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_CLOSING_VALVE_STATUS_ENDPOINT_PATH));

        public async Task<bool?> ShortTappingStatusAsync() => await Task.Run(() => GetShortTappingStatus());

        bool? GetShortTappingStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_SHORT_TAPPING_STATUS_ENDPOINT_PATH));

        public async Task<bool?> SystemLeakingStatusAsync() => await Task.Run(() => GetSystemLeakingStatus());

        bool? GetSystemLeakingStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_SYSTEM_LEAKING_STATUS_ENDPOINT_PATH));

        public async Task<bool?> ThermalDesinfectEnabledAsync() => await Task.Run(() => GetThermalDesinfectEnabled());

        bool? GetThermalDesinfectEnabled() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.DHWA_THERMAL_DESINFECT_STATE_ENDPOINT_PATH));

        public async Task<DateTime> NextThermalDesinfectAsync() => await Task.Run(() => GetNextThermalDesinfect());

        DateTime GetNextThermalDesinfect()
        {
            var nextTermalTime = HttpGet<int>(EndpointPaths.DHWA_THERMAL_DESINFECT_TIME_ENDPOINT_PATH);
            var nextTermalDay = HttpGet<string>(EndpointPaths.DHWA_THERMAL_DESINFECT_WEEKDAY_ENDPOINT_PATH);
            return nextTermalDay != null ? NefitEasyUtils.GetNextDate(nextTermalDay, nextTermalTime) : new DateTime();
        }

        public async Task<double> SystemPressureAsync() => await Task.Run(() => GetSystemPressure());

        double GetSystemPressure() => HttpGet<double>(EndpointPaths.APPLIANCE_SYSTEM_PRESSURE_ENDPOINT_PATH);

        public async Task<double> CentralHeatingSupplyTemperatureAsync() => await Task.Run(() => GetCentralHeatingSupplyTemperature());

        double GetCentralHeatingSupplyTemperature() => HttpGet<double>(EndpointPaths.HC1_ACTUAL_SUPPLY_TEMPERATURE_ENDPOINT_PATH);

        public async Task<StatusCode> StatusCodeAsync() => await Task.Run(() => GetStatusCode());

        StatusCode GetStatusCode() => new StatusCode(HttpGet<string>(EndpointPaths.APPLIANCE_DISPLAY_CODE_ENDPOINT_PATH), Convert.ToInt32(HttpGet<string>(EndpointPaths.APPLIANCE_CAUSE_CODE_ENDPOINT_PATH)));

        public async Task<ProgramSwitch> CurrentSwitchPointAsync() => await Task.Run(() => GetCurrentSwitchPoint());

        ProgramSwitch GetCurrentSwitchPoint()
        {
            var sp = HttpGet<IEnumerable<NefitSwitch>>(EndpointPaths.DHWA_CURRENT_SWITCHPOINT_ENDPOINT_PATH);
            return sp.Any() ? new ProgramSwitch(sp.ElementAt(0)) : null;
        }

        public async Task<ProgramSwitch> NextSwitchPointAsync() => await Task.Run(() => GetNextSwitchPoint());

        ProgramSwitch GetNextSwitchPoint()
        {
            var sp = HttpGet<IEnumerable<NefitSwitch>>(EndpointPaths.DHWA_NEXT_SWITCHPOINT_ENDPOINT_PATH);
            return sp.Any() ? new ProgramSwitch(sp.ElementAt(0)) : null;
        }

        public async Task<Location> LocationAsync() => await Task.Run(() => GetLocation());

        Location GetLocation() => new Location(HttpGet<double>(EndpointPaths.LOCATION_LATITUDE_ENDPOINT_PATH), HttpGet<double>(EndpointPaths.LOCATION_LONGITUDE_ENDPOINT_PATH));

        public async Task<IEnumerable<GasSample>> GasUsageAsync() => await Task.Run(() => GetGasUsage());

        IEnumerable<GasSample> GetGasUsage()
        {
            var hasValidSamples = true;
            var currentPage = 1;

            var gasSamples = new List<GasSample>();

            while (hasValidSamples)
            {
                var samples = HttpGet<IEnumerable<NefitGasSample>>($"{EndpointPaths.RECORDINGS_GAS_USAGE_ENDPOINT_PATH}?page={currentPage}");
                if (samples.Any())
                {
                    foreach (var sample in samples)
                    {
                        if (sample.d != "255-256-65535")
                        {
                            gasSamples.Add(new GasSample(Convert.ToDateTime(sample.d), sample.hw / 10.0, sample.ch / 10.0, sample.T / 10.0));
                        }
                        else
                        {
                            hasValidSamples = false;
                            break;
                        }
                    }
                    currentPage++;
                }
                else
                {
                    hasValidSamples = false;
                }
            }

            return gasSamples;
        }

        public async Task<UiStatus> UiStatusAsync() => await Task.Run(() => GetUiStatus());

        UiStatus GetUiStatus()
        {
            var status = HttpGet<NefitStatus>(EndpointPaths.UISTATUS_ENDPOINT_PATH);
            return status != null ? new UiStatus(status) : default(UiStatus);
        }

        public async Task<IEnumerable<string>> OwnerInfoAsync() => await Task.Run(() => GetOwnerInfo());

        IEnumerable<string> GetOwnerInfo() => HttpGet<string>(EndpointPaths.PERSONAL_DETAILS_ENDPOINT_PATH)?.Split(';');

        public async Task<IEnumerable<string>> InstallerInfoAsync() => await Task.Run(() => GetInstallerInfo());

        IEnumerable<string> GetInstallerInfo() => HttpGet<string>(EndpointPaths.INSTALLER_DETAILS_ENDPOINT_PATH)?.Split(';');

        public async Task<string> EasySerialAsync() => await Task.Run(() => GetEasySerial());

        string GetEasySerial() => HttpGet<string>(EndpointPaths.SERIAL_NUMBER_ENDPOINT_PATH);

        public async Task<string> EasyFirmwareAsync() => await Task.Run(() => GetEasyFirmware());

        string GetEasyFirmware() => HttpGet<string>(EndpointPaths.VERSION_FIRMWARE_ENDPOINT_PATH);

        public async Task<string> EasyHardwareAsync() => await Task.Run(() => GetEasyHardware());

        string GetEasyHardware() => HttpGet<string>(EndpointPaths.VERSION_HARDWARE_ENDPOINT_PATH);

        public async Task<string> EasyUuidAsync() => await Task.Run(() => GetEasyUuid());

        string GetEasyUuid() => HttpGet<string>(EndpointPaths.UUID_ENDPOINT_PATH);

        public async Task<EasyUpdateStrategy> EasyUpdateStrategyAsync() => await Task.Run(() => GetEasyUpdateStrategy());

        EasyUpdateStrategy GetEasyUpdateStrategy() => HttpGet<EasyUpdateStrategy>(EndpointPaths.UPDATE_STRATEGY_ENDPOINT_PATH);

        public async Task<string> CentralHeatingSerialAsync() => await Task.Run(() => GetCentralHeatingSerial());

        string GetCentralHeatingSerial() => HttpGet<string>(EndpointPaths.APPLIANCE_SERIAL_NUMBER_ENDPOINT_PATH);

        public async Task<string> CentralHeatingVersionAsync() => await Task.Run(() => GetCentralHeatingVersion());

        string GetCentralHeatingVersion() => HttpGet<string>(EndpointPaths.APPLIANCE_VERSION_ENDPOINT_PATH);

        public async Task<string> CentralHeatingBurnerMakeAsync() => await Task.Run(() => GetCentralHeatingBurnerMake());

        string GetCentralHeatingBurnerMake() => HttpGet<string>(EndpointPaths.EMS_BRANDBIT_ENDPOINT_PATH);

        public async Task<EasySensitivity> EasySensitivityAsync() => await Task.Run(() => GetEasySensitivity());

        EasySensitivity GetEasySensitivity() => HttpGet<EasySensitivity>(EndpointPaths.PIR_SENSITIVITY_ENDPOINT_PATH);

        public async Task<double> EasyTemperatureStepAsync() => await Task.Run(() => GetEasyTemperatureStep());

        double GetEasyTemperatureStep() => HttpGet<double>(EndpointPaths.TEMPERATURE_STEP_ENDPOINT_PATH);

        public async Task<double> EasyTemperatureOffsetAsync() => await Task.Run(() => GetEasyTemperatureOffset());

        double GetEasyTemperatureOffset() => HttpGet<double>(EndpointPaths.HC1_TEMPERATURE_ADJUSTMENT_ENDPOINT_PATH);

        public async Task<bool> SetHotWaterModeClockProgramAsync(bool onOff) => await Task.Run(() => SetHotWaterModeClockProgram(onOff));

        bool SetHotWaterModeClockProgram(bool onOff) => HttpPut(EndpointPaths.DHWA_OPERATION_CLOCK_MODE_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(onOff ? nameof(Switch.On) : nameof(Switch.Off)));

        public async Task<bool> SetHotWaterModeManualProgramAsync(bool onOff) => await Task.Run(() => SetHotWaterModeManualProgram(onOff));

        bool SetHotWaterModeManualProgram(bool onOff) => HttpPut(EndpointPaths.DHWA_OPERATION_MANUAL_MODE_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(onOff ? nameof(Switch.On) : nameof(Switch.Off)));

        public async Task<bool> SetUserModeAsync(UserModes newMode) => await Task.Run(() => SetUserMode(newMode));

        bool SetUserMode(UserModes newMode) => newMode != UserModes.Unknown && HttpPut(EndpointPaths.HC1_USERMODE_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(newMode.ToString()));

        public async Task<bool> SetTemperatureAsync(double temperature) => await Task.Run(() => SetTemperature(temperature));

        bool SetTemperature(double temperature)
        {
            if (!(temperature >= 5) || !(temperature <= 30)) return false;

            var result = HttpPut(EndpointPaths.HC1_TEMPERATURE_ROOM_MANUAL_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(temperature));
            if (result)
            {
                result = HttpPut(EndpointPaths.HC1_MANUAL_TEMPERATURE_OVERRIDE_STATUS_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(nameof(Switch.On)));
            }
            if (result)
            {
                result = HttpPut(EndpointPaths.HC1_MANUAL_TEMPERATURE_OVERRIDE, NefitEasyUtils.GetHttpPutDataString(temperature));
            }
            return result;
        }
    }
}