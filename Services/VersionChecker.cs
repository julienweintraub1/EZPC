using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EZPC.Models;

namespace EZPC.Services
{
    public class VersionChecker
    {
        private JsonDocument? _data;

        public VersionChecker()
        {
            try
            {
                var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "manufacturer_urls.json");
                if (File.Exists(jsonPath))
                {
                    _data = JsonDocument.Parse(File.ReadAllText(jsonPath));
                }
            }
            catch { }
        }

        public List<ComponentInfo> GetRecommendations(HardwareInfo hw)
        {
            var list = new List<ComponentInfo>();

            // GPU Section
            if (!string.IsNullOrEmpty(hw.GpuName))
            {
                list.Add(BuildGpuSection(hw));
            }

            // CPU Section  
            if (!string.IsNullOrEmpty(hw.CpuName))
            {
                list.Add(BuildCpuSection(hw));
            }

            // Storage Section
            if (hw.Drives.Count > 0)
            {
                list.Add(BuildStorageSection(hw));
            }

            return list;
        }

        private ComponentInfo BuildGpuSection(HardwareInfo hw)
        {
            var mfgKey = GetGpuMfgKey(hw.GpuManufacturer);
            string? latestVersion = null;
            string? driverUrl = null;
            string? monitoringTool = null;
            string? monitoringUrl = null;

            if (_data != null && mfgKey != null)
            {
                try
                {
                    var gpuData = _data.RootElement.GetProperty("gpu").GetProperty(mfgKey);
                    latestVersion = gpuData.GetProperty("latestDriverVersion").GetString();
                    driverUrl = gpuData.GetProperty("driverUrl").GetString();
                    
                    var tools = gpuData.GetProperty("monitoringTools");
                    monitoringTool = tools.GetProperty("name").GetString();
                    monitoringUrl = tools.GetProperty("url").GetString();
                }
                catch { }
            }

            var driverAge = GetAge(hw.GpuDriverDate);
            var ageText = driverAge.HasValue ? FormatAge(driverAge.Value) : "unknown age";

            string description;
            if (!string.IsNullOrEmpty(latestVersion) && hw.GpuDriverVersion != latestVersion)
            {
                description = $"Current driver: {hw.GpuDriverVersion} ({ageText})\n" +
                              $"Latest available: {latestVersion}\n\n" +
                              $"Update recommended for improved performance and game compatibility.";
            }
            else
            {
                description = $"Current driver: {hw.GpuDriverVersion} ({ageText})\n" +
                              $"Your driver appears to be up to date.";
            }

            if (!string.IsNullOrEmpty(monitoringTool))
            {
                description += $"\n\n📊 Monitoring Tool: {monitoringTool}";
            }

            return new ComponentInfo
            {
                Category = ComponentCategory.GPU,
                Title = "GPU",
                Subtitle = hw.GpuName,
                Description = description,
                ActionText = "Download Driver",
                ActionUrl = driverUrl ?? "https://www.nvidia.com/Download/index.aspx",
                IsActionable = true,
                ExtraInfo = !string.IsNullOrEmpty(monitoringUrl) ? $"Monitoring: {monitoringUrl}" : null
            };
        }

        private ComponentInfo BuildCpuSection(HardwareInfo hw)
        {
            var mfgKey = GetCpuMfgKey(hw.CpuManufacturer);
            string? monitoringTool = null;
            string? monitoringUrl = null;
            string? ocTool = null;
            string? ocUrl = null;

            if (_data != null && mfgKey != null)
            {
                try
                {
                    var cpuData = _data.RootElement.GetProperty("cpu").GetProperty(mfgKey);
                    
                    var tools = cpuData.GetProperty("monitoringTools");
                    monitoringTool = tools.GetProperty("name").GetString();
                    monitoringUrl = tools.GetProperty("url").GetString();

                    var ocTools = cpuData.GetProperty("overclockingTools");
                    ocTool = ocTools.GetProperty("name").GetString();
                    ocUrl = ocTools.GetProperty("url").GetString();
                }
                catch { }
            }

            var description = $"Cores: {hw.CpuCores} | Threads: {hw.CpuThreads}\n\n";
            
            if (!string.IsNullOrEmpty(monitoringTool))
            {
                description += $"📊 Monitoring: {monitoringTool}\n";
            }
            
            if (!string.IsNullOrEmpty(ocTool))
            {
                description += $"⚡ Overclocking: {ocTool}";
            }

            return new ComponentInfo
            {
                Category = ComponentCategory.CPU,
                Title = "CPU",
                Subtitle = hw.CpuName,
                Description = description,
                ActionText = "Get Monitoring Tool",
                ActionUrl = monitoringUrl ?? "https://www.hwinfo.com/download/",
                IsActionable = true,
                ExtraInfo = !string.IsNullOrEmpty(ocUrl) ? $"Overclocking: {ocUrl}" : null
            };
        }

        private ComponentInfo BuildStorageSection(HardwareInfo hw)
        {
            var lines = new List<string>();
            
            foreach (var drive in hw.Drives)
            {
                var usedPercent = drive.CapacityGB > 0 
                    ? (int)((drive.CapacityGB - drive.FreeSpaceGB) * 100 / drive.CapacityGB) 
                    : 0;
                
                var healthStatus = "";
                if (usedPercent > 90)
                {
                    healthStatus = " ⚠️ Low space!";
                }
                
                lines.Add($"💾 {drive.DriveLetter} ({drive.MediaType}) - {drive.FreeSpaceGB} GB free of {drive.CapacityGB} GB{healthStatus}");
                lines.Add($"   {drive.Name}");
                lines.Add("");
            }

            var description = string.Join("\n", lines).TrimEnd();
            
            // Add tip
            description += "\n\n💡 Keep at least 10-15% free space for optimal SSD performance.";

            return new ComponentInfo
            {
                Category = ComponentCategory.Storage,
                Title = "STORAGE",
                Subtitle = $"{hw.Drives.Count} drive(s) detected",
                Description = description,
                ActionText = "Open Disk Cleanup",
                ActionUrl = "cleanmgr",
                IsActionable = true
            };
        }

        private static string? GetGpuMfgKey(string? mfg)
        {
            if (string.IsNullOrEmpty(mfg)) return null;
            var lower = mfg.ToLower();
            if (lower.Contains("nvidia")) return "nvidia";
            if (lower.Contains("amd")) return "amd";
            if (lower.Contains("intel")) return "intel";
            return null;
        }

        private static string? GetCpuMfgKey(string? mfg)
        {
            if (string.IsNullOrEmpty(mfg)) return null;
            var lower = mfg.ToLower();
            if (lower.Contains("intel")) return "intel";
            if (lower.Contains("amd")) return "amd";
            return null;
        }

        private static TimeSpan? GetAge(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return null;
            try
            {
                if (DateTime.TryParse(dateStr, out var date))
                    return DateTime.Now - date;
            }
            catch { }
            return null;
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalDays > 365)
                return $"{(int)(age.TotalDays / 365)} year(s) old";
            if (age.TotalDays > 30)
                return $"{(int)(age.TotalDays / 30)} month(s) old";
            if (age.TotalDays > 1)
                return $"{(int)age.TotalDays} day(s) old";
            return "recent";
        }
    }
}