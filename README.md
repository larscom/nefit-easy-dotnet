# Nefit Easy™ .NET Library
.NET client library for the [Nefit Easy](http://www.nefit.nl/consument/service/easy/easy) smart thermostat.




## PLEASE READ BEFORE USE!
Use this library in moderation: don't flood the backend with new connections made every X seconds. Instead, if you want to poll the backend for data, create a connection once and reuse it for each command. In the end, it's your own responsibility to not get blocked because of excessive (ab)use.

## Disclaimer

The implementation of this library is based on reverse-engineering the communications between the apps and the backend, plus various other bits and pieces of information. It is *not* based on any official information given out by Nefit/Bosch, and therefore there are no guarantees whatsoever regarding the safety of your devices and/or their settings, or the accuracy of the information provided.



## Examples

### Create Client & Connect

Create a client and get the owner info.

```
INefitEasyClient client = NefitEasyClient.Create("serial", "accesskey", "password");
    
await client.ConnectAsync();
    
if (client.ConnectionStatus == NefitConnectionStatus.Connected)
{
    IEnumerable<string> owner = await client.OwnerInfoAsync();
}    
```

#### 

### Subscribe to Connection Events

```
INefitEasyClient client = NefitEasyClient.Create("serial", "accesskey", "password");

client.ConnectionStatusChangedEvent += (sender, status) =>
        {
            switch (status)
             {
                 case NefitConnectionStatus.Connecting:
                     break;
                 case NefitConnectionStatus.Connected:
                     break;
                 case NefitConnectionStatus.Authenticating:
                     break;
                 case NefitConnectionStatus.AuthenticationTest:
                     break;
                 case NefitConnectionStatus.InvalidSerialAccessKey:
                     break;
                 case NefitConnectionStatus.InvalidPassword:
                     break;
                 case NefitConnectionStatus.Disconnecting:
                     break;
                 case NefitConnectionStatus.Disconnected:
                     break;
                }
         };

await client.ConnectAsync();
```

#### 

### UI Status

Get the current UI status (room temperature and more)

```
INefitEasyClient client = NefitEasyClient.Create("serial", "accesskey", "password");
    
await client.ConnectAsync();
    
if (client.ConnectionStatus == NefitConnectionStatus.Connected)
{
     UiStatus status = await client.UiStatusAsync();
     double temperature = status.InHouseTemperature;
}
```

#### 

### Set Room Temperature

Set a room temperature between 5 and 30 degrees Celsius

```
INefitEasyClient client = NefitEasyClient.Create("serial", "accesskey", "password");
    
await client.ConnectAsync();
    
if (client.ConnectionStatus == NefitConnectionStatus.Connected)
{
    bool succeeded = await client.SetTemperatureAsync(24d);
}
```

#### 