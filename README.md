# IoT-Edge-Sas-Token-Helper
Generates SAS Tokens from within a running IoT Edge module by querying the Workload API from IoT Edge

## Description
IoT Edge 1.2 brings a (currently in preview) [MQTT broker](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-publish-subscribe?view=iotedge-2020-11), that can be used by local modules or external applications. A SAS token will be needed for authentication.

If you want to use the broker from within an IoT Edge module, you can generate the SAS token by calling the workload API. This project does exactly this. This project uses the [SecurityDaemonClient](https://github.com/Azure/event-grid-iot-edge/tree/master/SecurityDaemonClient) class from the event-grid-iot-edge SDK as reference.

## Usage
Building the project you will get a nuget package, that can be added to your project. A sample csproj file could look like this:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <RestoreAdditionalProjectSources>
      https://api.nuget.org/v3/index.json;
      $(MSBuildThisFileDirectory)/References/
    </RestoreAdditionalProjectSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Azure.IoT.Edge.SasTokenHelper" Version="*"/>
  </ItemGroup>
</Project>
```
Please copy the generated nuget file [Azure.IoT.Edge.SasTokenHelper.1.0.4.nupkg](Azure.IoT.Edge.SasTokenHelper.1.0.4.nupkg) to a sufolder *References*.

Within your code, you can then use the package like this:

```c#
using IoTEdgeSasTokenHelper;
...

var securityDaemonClient = new SecurityDaemonClient();
// use Gateway Hostname, as the deviceId might not be the actual hostname
string hostname = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME");
string clientId = $"{securityDaemonClient.DeviceId}/{securityDaemonClient.ModuleId}";
string username = $"{securityDaemonClient.IotHubHostName}/{securityDaemonClient.DeviceId}/{securityDaemonClient.ModuleId}/?api-version=2018-06-30";
//call the workload api to get the TOKEN 
string password = await securityDaemonClient.GetModuleToken(3600);

```