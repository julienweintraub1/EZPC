using System;
using System.Collections.Generic;
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
                await Task.Run(() => ScanStorage(info));
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
                info.CpuName = obj["Name"]?.ToString()?.Trim() ?? "";
                info.CpuCores = Convert.ToInt32(obj["NumberOfCores"]);
                info.CpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                info.CpuManufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
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
                    
                    var rawDriverVersion = obj["DriverVersion"]?.ToString();
                    
                    var gpuLower = name.ToLower();
                    if (gpuLower.Contains("nvidia") || gpuLower.Contains("geforce") || gpuLower.Contains("gtx") || gpuLower.Contains("rtx"))
                    {
                        info.GpuManufacturer = "NVIDIA";
                        info.GpuDriverVersion = ConvertNvidiaDriverVersion(rawDriverVersion);
                    }
                    else if (gpuLower.Contains("amd") || gpuLower.Contains("radeon"))
                    {
                        info.GpuManufacturer = "AMD";
                        info.GpuDriverVersion = rawDriverVersion ?? "";
                    }
                    else if (gpuLower.Contains("intel"))
                    {
                        info.GpuManufacturer = "Intel";
                        info.GpuDriverVersion = rawDriverVersion ?? "";
                    }
                    else
                    {
                        info.GpuManufacturer = "Unknown";
                        info.GpuDriverVersion = rawDriverVersion ?? "";
                    }

                    var rawDate = obj["DriverDate"]?.ToString();
                    if (!string.IsNullOrEmpty(rawDate) && rawDate.Length >= 8)
                    {
                        info.GpuDriverDate = $"{rawDate.Substring(0, 4)}-{rawDate.Substring(4, 2)}-{rawDate.Substring(6, 2)}";
                    }

                    break;
                }
            }
        }

        private static string ConvertNvidiaDriverVersion(string? wmiVersion)
        {
            if (string.IsNullOrEmpty(wmiVersion))
                return "Unknown";

            try
            {
                var parts = wmiVersion.Split('.');
                if (parts.Length >= 4)
                {
                    var combined = parts[2] + parts[3];
                    
                    if (combined.Length >= 5)
                    {
                        var last5 = combined.Substring(combined.Length - 5);
                        return $"{last5.Substring(0, 3)}.{last5.Substring(3, 2)}";
                    }
                    else if (combined.Length == 4)
                    {
                        return $"{combined.Substring(0, 2)}.{combined.Substring(2, 2)}";
                    }
                }
            }
            catch { }

            return wmiVersion;
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

        private void ScanStorage(HardwareInfo info)
        {
            info.Drives = new List<StorageInfo>();

            // Get physical disk info first
            var diskModels = new Dictionary<int, (string Model, string MediaType)>();
            
            try
            {
                using var physicalDiskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject disk in physicalDiskSearcher.Get())
                {
                    var index = Convert.ToInt32(disk["Index"]);
                    var model = disk["Model"]?.ToString() ?? "Unknown Drive";
                    var mediaType = disk["MediaType"]?.ToString() ?? "";
                    
                    // Detect SSD vs HDD
                    string type = "HDD";
                    var modelLower = model.ToLower();
                    if (modelLower.Contains("ssd") || modelLower.Contains("nvme") || 
                        modelLower.Contains("solid") || mediaType.ToLower().Contains("solid"))
                    {
                        type = "SSD";
                    }
                    
                    diskModels[index] = (model, type);
                }
            }
            catch { }

            // Get logical disk info (C:, D:, etc.)
            try
            {
                using var logicalDiskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
                foreach (ManagementObject disk in logicalDiskSearcher.Get())
                {
                    var driveLetter = disk["DeviceID"]?.ToString() ?? "";
                    var freeSpace = Convert.ToInt64(disk["FreeSpace"] ?? 0);
                    var totalSize = Convert.ToInt64(disk["Size"] ?? 0);

                    // Try to get the physical disk model for this logical disk
                    string model = driveLetter;
                    string mediaType = "Unknown";
                    
                    try
                    {
                        // Query partition to disk mapping
                        using var partitionSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                        foreach (ManagementObject partition in partitionSearcher.Get())
                        {
                            using var diskSearcher = new ManagementObjectSearcher(
                                $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                            foreach (ManagementObject physDisk in diskSearcher.Get())
                            {
                                model = physDisk["Model"]?.ToString() ?? driveLetter;
                                var mt = physDisk["MediaType"]?.ToString() ?? "";
                                var modelLower = model.ToLower();
                                mediaType = (modelLower.Contains("ssd") || modelLower.Contains("nvme") || 
                                            modelLower.Contains("solid") || mt.ToLower().Contains("solid")) 
                                            ? "SSD" : "HDD";
                                break;
                            }
                            break;
                        }
                    }
                    catch { }

                    var storageInfo = new StorageInfo
                    {
                        Name = model,
                        DriveLetter = driveLetter,
                        MediaType = mediaType,
                        CapacityGB = totalSize / (1024 * 1024 * 1024),
                        FreeSpaceGB = freeSpace / (1024 * 1024 * 1024),
                        HealthPercent = -1 // Would need SMART data for real health
                    };

                    info.Drives.Add(storageInfo);
                }
            }
            catch { }
        }
    }
}