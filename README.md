# Calisto service
[![Build and test](https://github.com/Odery/Calisto-WindowsSerivce/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Odery/Calisto-WindowsSerivce/actions/workflows/dotnet-desktop.yml)
[![License](https://img.shields.io/github/license/Odery/Calisto-WindowsSerivce.svg)](https://github.com/Odery/Calisto-WindowsSerivce/blob/master/LICENSE)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.6%20or%20later-brightgreen)](https://dotnet.microsoft.com/download/dotnet-framework)
[![log4net](https://img.shields.io/badge/log4net-2.0.8%20or%20later-brightgreen)](https://www.nuget.org/packages/log4net/)
[![System.Net.Http](https://img.shields.io/badge/System.Net.Http-4.3.4%20or%20later-brightgreen)](https://www.nuget.org/packages/System.Net.Http/)

Calisto is a Windows service that makes periodic HTTP requests to an API and takes actions based on the API's response. The service is implemented in C# using the .NET Framework and runs on a Windows host.

The service is designed to run continuously in the background, making HTTP requests to a specified API at a configurable interval. The API's response determines the action taken by the service. Possible actions include shutting down the host, returning system status data as a JSON object, or taking no action.

The service also includes a cache for system status data to improve response times. In addition, the service uses log4net to log events and errors for debugging and troubleshooting purposes.

## Prerequisites

Before using the service, ensure that the following software is installed on the host:

-   .NET Framework 4.6 or later
-   log4net 2.0.8 or later
-   System.Net.Http 4.3.4 or later

## Configuration

The service can be configured using the following app settings in the `app.config` file:

-   `PrimaryApiUrl`: The URL of the primary API to which the service makes HTTP requests. This is the primary source of instructions for the service.
-   `SecondaryApiUrl`: The URL of the secondary API to which the service makes HTTP requests if the primary API is not available. This is used as a fallback in case the primary API is down or not responding.
-   `TimerInterval`: The interval at which the service makes HTTP requests to the API, in milliseconds. This value determines how frequently the service checks the API for new instructions.
- `MaxRetries`: The number of tries to connect to an endpoint

## Deployment

To deploy the service, follow these steps:

1.  Build the solution in Visual Studio. This will create an executable file for the service.
2.  Open a command prompt with administrator privileges.
3.  Navigate to the directory where the service executable is located.
4.  Run the following command to install the service: 
```powershell 
sc create calisto binPath= "path\to\calisto.exe"
``` 
This command installs the service and specifies the path to the executable file.
6.  Run the following command to start the service: 
```powershell 
sc start calisto 
``` 
This command starts the service and makes it available for use.

## Usage

To use the service, send HTTP GET requests to the primary or secondary API with the appropriate URL. The service will take actions based on the API's response, as described below:

-   If the API's response is `"shutdown"`, the service will shut down the host.
-   If the API's response is `"data"`, the service will return system status data as a JSON object. The system status data includes information about the host's CPU, memory, and storage usage.
-   If the API's response is anything else, the service will take no action.

## Caching

The service uses a cache to store system status data, which is returned in response to `"data"` requests. This allows the service to respond faster to subsequent `"data"` requests without having to retrieve the data from the host each time. The cache is updated every time the service makes a request to the API, so the data remains current.

## Logging

The service uses log4net to log events and errors for debugging and troubleshooting purposes. The log files are stored in the service's installation directory and can be accessed for more detailed information about the service's operations.

## Troubleshooting
If the service is not functioning as expected, there are a few steps you can take to troubleshoot the issue:

1.  Check the event log for error messages. The event log may contain information about issues that occurred while the service was running.
2.  Consult the log files. The service uses log4net to log detailed information about its operations, including errors and exceptions. The log files can be accessed from the service's installation directory.
3.  Check the configuration settings. Make sure that the `app.config` and `var.config` file is properly configured and that the correct URLs for the primary and secondary APIs are specified.
4.  Check the API. Make sure that the API is functioning properly and that it is responding to requests as expected.
5.  Restart the service. If all else fails, try stopping and starting the service to see if that resolves the issue.
## Additional Information

For more information about the service's code and implementation, refer to the source code comments.
