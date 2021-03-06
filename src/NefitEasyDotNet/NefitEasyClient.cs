﻿using System;
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

        public void Connect()
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

        public void Disconnect()
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

        public async Task<int> GetActiveProgramAsync() => await Task.Run(() => GetActiveProgram());

        public int GetActiveProgram() => HttpGet<int>(EndpointPaths.USER_PROGRAM_ACTIVE_PROGRAM_ENDPOINT_PATH);

        public async Task<IEnumerable<ProgramSwitch>> GetProgramAsync(int programNumber) => await Task.Run(() => GetProgram(programNumber));

        public IEnumerable<ProgramSwitch> GetProgram(int programNumber)
        {
            if (programNumber < 0 || programNumber >= 3) return null;

            var program0 = HttpGet<IEnumerable<NefitProgram>>($"{EndpointPaths.USER_PROGRAM_PROGRAM_ENDPOINT_PATH}{programNumber}");

            return !program0.Any() ? null : NefitEasyUtils.ParseProgram(program0);
        }

        public async Task<string> GetSwitchpointNameAsync(int nameIndex) => await Task.Run(() => GetSwitchpointName(nameIndex));

        public string GetSwitchpointName(int nameIndex)
        {
            if (--nameIndex >= 1 && nameIndex <= 2)
                return HttpGet<string>($"{EndpointPaths.USER_PROGRAM_USER_SWITCHPOINT_NAME_ENDPOINT_PATH}{nameIndex}");

            return null;
        }

        public async Task<bool?> FireplaceFunctionActiveAsync() => await Task.Run(() => FireplaceFunctionActive());

        public bool? FireplaceFunctionActive() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.USER_PROGRAM_PREHEATING_ENDPOINT_PATH));

        public async Task<bool?> PreheatingActiveAsync() => await Task.Run(() => PreheatingActive());

        public bool? PreheatingActive() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.USER_PROGRAM_PREHEATING_ENDPOINT_PATH));

        public async Task<double> GetOutdoorTemperatureAsync() => await Task.Run(() => GetOutdoorTemperature());

        public double GetOutdoorTemperature() => HttpGet<double>(EndpointPaths.SENSORS_TEMPERATURES_OUTDOOR_ENDPOINT_PATH);

        public async Task<string> GetEasyServiceStatusAsync() => await Task.Run(() => GetEasyServiceStatus());

        public string GetEasyServiceStatus() => HttpGet<string>(EndpointPaths.REMOTE_SERVICESTATE_ENDPOINT_PATH);

        public async Task<bool?> IgnitionStatusAsync() => await Task.Run(() => IgnitionStatus());

        public bool? IgnitionStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_IGNITION_STATUS_ENDPOINT_PATH));

        public async Task<bool?> RefillNeededStatusAsync() => await Task.Run(() => RefillNeededStatus());

        public bool? RefillNeededStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_REFILL_NEEDED_STATUS_ENDPOINT_PATH));

        public async Task<bool?> ClosingValveStatusAsync() => await Task.Run(() => ClosingValveStatus());

        public bool? ClosingValveStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_CLOSING_VALVE_STATUS_ENDPOINT_PATH));

        public async Task<bool?> ShortTappingStatusAsync() => await Task.Run(() => ShortTappingStatus());

        public bool? ShortTappingStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_SHORT_TAPPING_STATUS_ENDPOINT_PATH));

        public async Task<bool?> SystemLeakingStatusAsync() => await Task.Run(() => SystemLeakingStatus());

        public bool? SystemLeakingStatus() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.PM_SYSTEM_LEAKING_STATUS_ENDPOINT_PATH));

        public async Task<bool?> ThermalDesinfectEnabledAsync() => await Task.Run(() => ThermalDesinfectEnabled());

        public bool? ThermalDesinfectEnabled() => NefitEasyUtils.IsOnOrTrue(HttpGet<string>(EndpointPaths.DHWA_THERMAL_DESINFECT_STATE_ENDPOINT_PATH));

        public async Task<DateTime> GetNextThermalDesinfectAsync() => await Task.Run(() => GetNextThermalDesinfect());

        public DateTime GetNextThermalDesinfect()
        {
            var nextTermalTime = HttpGet<int>(EndpointPaths.DHWA_THERMAL_DESINFECT_TIME_ENDPOINT_PATH);
            var nextTermalDay = HttpGet<string>(EndpointPaths.DHWA_THERMAL_DESINFECT_WEEKDAY_ENDPOINT_PATH);
            return nextTermalDay != null ? NefitEasyUtils.GetNextDate(nextTermalDay, nextTermalTime) : new DateTime();
        }

        public async Task<double> GetSystemPressureAsync() => await Task.Run(() => GetSystemPressure());

        public double GetSystemPressure() => HttpGet<double>(EndpointPaths.APPLIANCE_SYSTEM_PRESSURE_ENDPOINT_PATH);

        public async Task<double> GetCentralHeatingSupplyTemperatureAsync() => await Task.Run(() => GetCentralHeatingSupplyTemperature());

        public double GetCentralHeatingSupplyTemperature() => HttpGet<double>(EndpointPaths.HC1_ACTUAL_SUPPLY_TEMPERATURE_ENDPOINT_PATH);

        public async Task<StatusCode> GetStatusCodeAsync() => await Task.Run(() => GetStatusCode());

        public StatusCode GetStatusCode() => new StatusCode(HttpGet<string>(EndpointPaths.APPLIANCE_DISPLAY_CODE_ENDPOINT_PATH), Convert.ToInt32(HttpGet<string>(EndpointPaths.APPLIANCE_CAUSE_CODE_ENDPOINT_PATH)));

        public async Task<ProgramSwitch> GetCurrentSwitchPointAsync() => await Task.Run(() => GetCurrentSwitchPoint());

        public ProgramSwitch GetCurrentSwitchPoint()
        {
            var sp = HttpGet<IEnumerable<NefitSwitch>>(EndpointPaths.DHWA_CURRENT_SWITCHPOINT_ENDPOINT_PATH);
            return sp.Any() ? new ProgramSwitch(sp.ElementAt(0)) : null;
        }

        public async Task<ProgramSwitch> GetNextSwitchPointAsync() => await Task.Run(() => GetNextSwitchPoint());

        public ProgramSwitch GetNextSwitchPoint()
        {
            var sp = HttpGet<IEnumerable<NefitSwitch>>(EndpointPaths.DHWA_NEXT_SWITCHPOINT_ENDPOINT_PATH);
            return sp.Any() ? new ProgramSwitch(sp.ElementAt(0)) : null;
        }

        public async Task<Location> GetLocationAsync() => await Task.Run(() => GetLocation());

        public Location GetLocation() => new Location(HttpGet<double>(EndpointPaths.LOCATION_LATITUDE_ENDPOINT_PATH), HttpGet<double>(EndpointPaths.LOCATION_LONGITUDE_ENDPOINT_PATH));

        public async Task<IEnumerable<GasSample>> GetGasUsageAsync() => await Task.Run(() => GetGasUsage());

        public IEnumerable<GasSample> GetGasUsage()
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

        public async Task<UiStatus> GetUiStatusAsync() => await Task.Run(() => GetUiStatus());

        public UiStatus GetUiStatus()
        {
            var status = HttpGet<NefitStatus>(EndpointPaths.UISTATUS_ENDPOINT_PATH);
            return status != null ? new UiStatus(status) : default(UiStatus);
        }

        public async Task<IEnumerable<string>> GetOwnerInfoAsync() => await Task.Run(() => GetOwnerInfo());

        public IEnumerable<string> GetOwnerInfo() => HttpGet<string>(EndpointPaths.PERSONAL_DETAILS_ENDPOINT_PATH)?.Split(';');

        public async Task<IEnumerable<string>> GetInstallerInfoAsync() => await Task.Run(() => GetInstallerInfo());

        public IEnumerable<string> GetInstallerInfo() => HttpGet<string>(EndpointPaths.INSTALLER_DETAILS_ENDPOINT_PATH)?.Split(';');

        public async Task<string> GetEasySerialAsync() => await Task.Run(() => GetEasySerial());

        public string GetEasySerial() => HttpGet<string>(EndpointPaths.SERIAL_NUMBER_ENDPOINT_PATH);

        public async Task<string> GetEasyFirmwareAsync() => await Task.Run(() => GetEasyFirmware());

        public string GetEasyFirmware() => HttpGet<string>(EndpointPaths.VERSION_FIRMWARE_ENDPOINT_PATH);

        public async Task<string> GetEasyHardwareAsync() => await Task.Run(() => GetEasyHardware());

        public string GetEasyHardware() => HttpGet<string>(EndpointPaths.VERSION_HARDWARE_ENDPOINT_PATH);

        public async Task<string> GetEasyUuidAsync() => await Task.Run(() => GetEasyUuid());

        public string GetEasyUuid() => HttpGet<string>(EndpointPaths.UUID_ENDPOINT_PATH);

        public async Task<EasyUpdateStrategy> GetEasyUpdateStrategyAsync() => await Task.Run(() => GetEasyUpdateStrategy());

        public EasyUpdateStrategy GetEasyUpdateStrategy() => HttpGet<EasyUpdateStrategy>(EndpointPaths.UPDATE_STRATEGY_ENDPOINT_PATH);

        public async Task<string> GetCentralHeatingSerialAsync() => await Task.Run(() => GetCentralHeatingSerial());

        public string GetCentralHeatingSerial() => HttpGet<string>(EndpointPaths.APPLIANCE_SERIAL_NUMBER_ENDPOINT_PATH);

        public async Task<string> GetCentralHeatingVersionAsync() => await Task.Run(() => GetCentralHeatingVersion());

        public string GetCentralHeatingVersion() => HttpGet<string>(EndpointPaths.APPLIANCE_VERSION_ENDPOINT_PATH);

        public async Task<string> GetCentralHeatingBurnerMakeAsync() => await Task.Run(() => GetCentralHeatingBurnerMake());

        public string GetCentralHeatingBurnerMake() => HttpGet<string>(EndpointPaths.EMS_BRANDBIT_ENDPOINT_PATH);

        public async Task<EasySensitivity> GetEasySensitivityAsync() => await Task.Run(() => GetEasySensitivity());

        public EasySensitivity GetEasySensitivity() => HttpGet<EasySensitivity>(EndpointPaths.PIR_SENSITIVITY_ENDPOINT_PATH);

        public async Task<double> GetEasyTemperatureStepAsync() => await Task.Run(() => GetEasyTemperatureStep());

        public double GetEasyTemperatureStep() => HttpGet<double>(EndpointPaths.TEMPERATURE_STEP_ENDPOINT_PATH);

        public async Task<double> GetEasyTemperatureOffsetAsync() => await Task.Run(() => GetEasyTemperatureOffset());

        public double GetEasyTemperatureOffset() => HttpGet<double>(EndpointPaths.HC1_TEMPERATURE_ADJUSTMENT_ENDPOINT_PATH);

        public async Task<bool> SetHotWaterModeClockProgramAsync(bool onOff) => await Task.Run(() => SetHotWaterModeClockProgram(onOff));

        public bool SetHotWaterModeClockProgram(bool onOff) => HttpPut(EndpointPaths.DHWA_OPERATION_CLOCK_MODE_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(onOff ? nameof(Switch.On) : nameof(Switch.Off)));

        public async Task<bool> SetHotWaterModeManualProgramAsync(bool onOff) => await Task.Run(() => SetHotWaterModeManualProgram(onOff));

        public bool SetHotWaterModeManualProgram(bool onOff) => HttpPut(EndpointPaths.DHWA_OPERATION_MANUAL_MODE_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(onOff ? nameof(Switch.On) : nameof(Switch.Off)));

        public async Task<bool> SetUserModeAsync(UserModes newMode) => await Task.Run(() => SetUserMode(newMode));

        public bool SetUserMode(UserModes newMode) => newMode != UserModes.Unknown && HttpPut(EndpointPaths.HC1_USERMODE_ENDPOINT_PATH, NefitEasyUtils.GetHttpPutDataString(newMode.ToString()));

        public async Task<bool> SetTemperatureAsync(double temperature) => await Task.Run(() => SetTemperature(temperature));

        public bool SetTemperature(double temperature)
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