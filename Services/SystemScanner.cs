using System;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using EZPC.Models;

namespace EZPC.Services
{
    public class SystemScanner
    {
        public async Task<HardwareInfo> ScanSystemAsync()
        {
            var info = new HardwareInfo
            {
                ScanDate = DateTime.Now
            };

            try
            {
                await Task.Run(() => ScanCpu(info));
                await Task.Run(() => ScanGpu(info));
                await Task.Run(() => ScanRam(info));
                await Task.Run(() => ScanWindows(info));
                await Task.Run(() => ScanMotherboardAndBios(info));  // NEW METHOD
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scan error: {ex.Message}");
            }

            return info;
        }

        private void ScanCpu(HardwareInfo info)
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                info.CpuName = obj["Name"]?.ToString()?.Trim();
                info.CpuCores = Convert.ToInt32(obj["NumberOfCores"]);
                info.CpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);

                // NEW - Detect CPU manufacturer
                info.CpuManufacturer = obj["Manufacturer"]?.ToString()?.Trim();

                break;
            }
        }

        private void ScanGpu(HardwareInfo info)
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (name != null && !name.Contains("Microsoft Basic"))
                {
                    info.GpuName = name.Trim();
                    info.GpuDriverVersion = obj["DriverVersion"]?.ToString();
                    info.GpuDriverDate = obj["DriverDate"]?.ToString();

                    // NEW - Detect GPU manufacturer from name
                    var gpuLower = name.ToLower();
                    if (gpuLower.Contains("nvidia") || gpuLower.Contains("geforce") || gpuLower.Contains("gtx") || gpuLower.Contains("rtx"))
                        info.GpuManufacturer = "NVIDIA";
                    else if (gpuLower.Contains("amd") || gpuLower.Contains("radeon"))
                        info.GpuManufacturer = "AMD";
                    else if (gpuLower.Contains("intel") && gpuLower.Contains("arc"))
                        info.GpuManufacturer = "Intel";
                    else
                        info.GpuManufacturer = "Unknown";

                    break;
                }
            }
        }

        private void ScanRam(HardwareInfo info)
        {
            long totalBytes = 0;
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                totalBytes += Convert.ToInt64(obj["Capacity"]);
            }
            info.TotalRamGB = totalBytes / (1024 * 1024 * 1024);
        }

        private void ScanWindows(HardwareInfo info)
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                info.WindowsVersion = obj["Caption"]?.ToString();
                info.WindowsBuild = obj["BuildNumber"]?.ToString();
                break;
            }
        }

        // NEW METHOD - Scans motherboard and BIOS info
        private void ScanMotherboardAndBios(HardwareInfo info)
        {
            // Get Motherboard info
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.MotherboardManufacturer = obj["Manufacturer"]?.ToString()?.Trim();
                    info.MotherboardModel = obj["Product"]?.ToString()?.Trim();
                    break;
                }
            }

            // Get BIOS info
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString()?.Trim();

                    // Parse BIOS date (WMI returns: YYYYMMDDHHMMSS.MMMMMM+UUU)
                    var rawDate = obj["ReleaseDate"]?.ToString();
                    if (!string.IsNullOrEmpty(rawDate) && rawDate.Length >= 8)
                    {
                        // Extract just YYYY-MM-DD
                        info.BiosDate = $"{rawDate.Substring(0, 4)}-{rawDate.Substring(4, 2)}-{rawDate.Substring(6, 2)}";
                    }

                    break;
                }
            }
        }
    }
}