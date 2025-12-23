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
        private JsonDocument _manufacturerData;

        public VersionChecker()
        {
            LoadManufacturerData();
        }

        private void LoadManufacturerData()
        {
            try
            {
                var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "manufacturer_urls.json");
                var jsonContent = File.ReadAllText(jsonPath);
                _manufacturerData = JsonDocument.Parse(jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading manufacturer data: {ex.Message}");
            }
        }

        public List<UpdateRecommendation> CheckForUpdates(HardwareInfo hardware)
        {
            var recommendations = new List<UpdateRecommendation>();

            // GPU Driver
            if (!string.IsNullOrEmpty(hardware.GpuName))
            {
                var gpuRec = CheckGpuDriver(hardware);
                if (gpuRec != null) recommendations.Add(gpuRec);
            }

            // GPU Monitoring/Overclocking Tools - NEW
            if (!string.IsNullOrEmpty(hardware.GpuManufacturer))
            {
                var gpuToolRec = RecommendGpuTools(hardware);
                if (gpuToolRec != null) recommendations.Add(gpuToolRec);
            }

            // CPU Monitoring/Overclocking Tools - NEW
            if (!string.IsNullOrEmpty(hardware.CpuManufacturer))
            {
                var cpuToolRec = RecommendCpuTools(hardware);
                if (cpuToolRec != null) recommendations.Add(cpuToolRec);
            }

            // BIOS Update Info - NEW
            if (!string.IsNullOrEmpty(hardware.MotherboardManufacturer))
            {
                var biosRec = RecommendBiosUpdate(hardware);
                if (biosRec != null) recommendations.Add(biosRec);
            }

            // Windows Updates
            recommendations.Add(CheckWindowsUpdates(hardware));

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }

        private UpdateRecommendation CheckGpuDriver(HardwareInfo hardware)
        {
            string manufacturer = DetectGpuManufacturer(hardware.GpuName);

            if (manufacturer == null || _manufacturerData == null)
                return null;

            try
            {
                var gpuData = _manufacturerData.RootElement
                    .GetProperty("gpu")
                    .GetProperty(manufacturer);

                var latestVersion = gpuData.GetProperty("latestDriverVersion").GetString();
                var driverUrl = gpuData.GetProperty("driverUrl").GetString();
                var instructions = gpuData.GetProperty("instructions")
                    .EnumerateArray()
                    .Select(x => x.GetString())
                    .ToList();

                var priority = CompareVersions(hardware.GpuDriverVersion, latestVersion);

                return new UpdateRecommendation
                {
                    Component = $"{manufacturer.ToUpper()} Graphics Driver",
                    CurrentVersion = hardware.GpuDriverVersion ?? "Unknown",
                    LatestVersion = latestVersion,
                    Priority = priority,
                    Description = priority == UpdatePriority.UpToDate
                        ? "Your GPU driver is up to date!"
                        : $"A newer {manufacturer.ToUpper()} driver is available. Update for better performance and bug fixes.",
                    UpdateUrl = driverUrl,
                    Instructions = string.Join("\n", instructions)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking GPU driver: {ex.Message}");
                return null;
            }
        }

        // NEW METHOD - Recommend GPU monitoring/overclocking tools
        private UpdateRecommendation RecommendGpuTools(HardwareInfo hardware)
        {
            string manufacturer = DetectGpuManufacturer(hardware.GpuName);

            if (manufacturer == null || _manufacturerData == null)
                return null;

            try
            {
                var toolData = _manufacturerData.RootElement
                    .GetProperty("gpu")
                    .GetProperty(manufacturer)
                    .GetProperty("monitoringTools");

                var toolName = toolData.GetProperty("name").GetString();
                var toolUrl = toolData.GetProperty("url").GetString();
                var description = toolData.GetProperty("description").GetString();
                var features = toolData.GetProperty("features")
                    .EnumerateArray()
                    .Select(x => $"• {x.GetString()}")
                    .ToList();

                return new UpdateRecommendation
                {
                    Component = "GPU Monitoring & Overclocking",
                    CurrentVersion = "Recommendation",
                    LatestVersion = toolName,
                    Priority = UpdatePriority.Low,
                    Description = description,
                    UpdateUrl = toolUrl,
                    Instructions = $"Features:\n{string.Join("\n", features)}\n\nClick 'Open Tool Page' to download and install."
                };
            }
            catch
            {
                return null;
            }
        }

        // NEW METHOD - Recommend CPU monitoring/overclocking tools
        private UpdateRecommendation RecommendCpuTools(HardwareInfo hardware)
        {
            string manufacturer = DetectCpuManufacturer(hardware.CpuManufacturer);

            if (manufacturer == null || _manufacturerData == null)
                return null;

            try
            {
                var toolData = _manufacturerData.RootElement
                    .GetProperty("cpu")
                    .GetProperty(manufacturer)
                    .GetProperty("monitoringTools");

                var toolName = toolData.GetProperty("name").GetString();
                var toolUrl = toolData.GetProperty("url").GetString();
                var description = toolData.GetProperty("description").GetString();
                var features = toolData.GetProperty("features")
                    .EnumerateArray()
                    .Select(x => $"• {x.GetString()}")
                    .ToList();

                var instructions = $"Recommended: {toolName}\n\n{description}\n\nFeatures:\n{string.Join("\n", features)}";

                // Also add overclocking tool info if available
                try
                {
                    var ocToolData = _manufacturerData.RootElement
                        .GetProperty("cpu")
                        .GetProperty(manufacturer)
                        .GetProperty("overclockingTools");

                    var ocName = ocToolData.GetProperty("name").GetString();
                    var ocUrl = ocToolData.GetProperty("url").GetString();
                    var ocDesc = ocToolData.GetProperty("description").GetString();
                    var warnings = ocToolData.GetProperty("warnings")
                        .EnumerateArray()
                        .Select(x => $"⚠️ {x.GetString()}")
                        .ToList();

                    instructions += $"\n\n─────────────────\nOverclocking Tool: {ocName}\n{ocDesc}\n\nURL: {ocUrl}\n\n{string.Join("\n", warnings)}";
                }
                catch { }

                return new UpdateRecommendation
                {
                    Component = "CPU Monitoring & Temperature",
                    CurrentVersion = hardware.CpuName,
                    LatestVersion = toolName,
                    Priority = UpdatePriority.Low,
                    Description = $"Monitor your CPU temperature and performance with recommended tools.",
                    UpdateUrl = toolUrl,
                    Instructions = instructions
                };
            }
            catch
            {
                return null;
            }
        }

        // NEW METHOD - Recommend BIOS update
        private UpdateRecommendation RecommendBiosUpdate(HardwareInfo hardware)
        {
            if (_manufacturerData == null)
                return null;

            try
            {
                var mfg = hardware.MotherboardManufacturer.ToLower();
                string mfgKey = "default";

                if (mfg.Contains("asus")) mfgKey = "asus";
                else if (mfg.Contains("msi")) mfgKey = "msi";
                else if (mfg.Contains("gigabyte")) mfgKey = "gigabyte";
                else if (mfg.Contains("asrock")) mfgKey = "asrock";

                var biosData = _manufacturerData.RootElement
                    .GetProperty("motherboard")
                    .GetProperty(mfgKey);

                var biosUrl = biosData.GetProperty("biosUrl").GetString();
                var instructions = biosData.GetProperty("instructions")
                    .EnumerateArray()
                    .Select(x => x.GetString())
                    .ToList();

                var description = $"Motherboard: {hardware.MotherboardManufacturer} {hardware.MotherboardModel}\n" +
                                $"Current BIOS: {hardware.BiosVersion} ({hardware.BiosDate})\n\n" +
                                $"Check if a newer BIOS version is available. Only update if you're experiencing issues or need new features.";

                return new UpdateRecommendation
                {
                    Component = "BIOS/UEFI Update",
                    CurrentVersion = $"{hardware.BiosVersion} ({hardware.BiosDate})",
                    LatestVersion = "Check manufacturer website",
                    Priority = UpdatePriority.Low,
                    Description = description,
                    UpdateUrl = biosUrl,
                    Instructions = string.Join("\n", instructions)
                };
            }
            catch
            {
                return null;
            }
        }

        private UpdateRecommendation CheckWindowsUpdates(HardwareInfo hardware)
        {
            if (_manufacturerData == null)
                return null;

            try
            {
                var windowsData = _manufacturerData.RootElement.GetProperty("windows");
                var updateUrl = windowsData.GetProperty("updateUrl").GetString();
                var instructions = windowsData.GetProperty("instructions")
                    .EnumerateArray()
                    .Select(x => x.GetString())
                    .ToList();

                return new UpdateRecommendation
                {
                    Component = "Windows Updates",
                    CurrentVersion = $"{hardware.WindowsVersion} (Build {hardware.WindowsBuild})",
                    LatestVersion = "Check for latest",
                    Priority = UpdatePriority.High,
                    Description = "Regularly check for Windows updates to ensure security and stability.",
                    UpdateUrl = updateUrl,
                    Instructions = string.Join("\n", instructions)
                };
            }
            catch
            {
                return null;
            }
        }

        private string DetectGpuManufacturer(string gpuName)
        {
            if (string.IsNullOrEmpty(gpuName))
                return null;

            gpuName = gpuName.ToLower();

            if (gpuName.Contains("nvidia") || gpuName.Contains("geforce") || gpuName.Contains("gtx") || gpuName.Contains("rtx"))
                return "nvidia";

            if (gpuName.Contains("amd") || gpuName.Contains("radeon"))
                return "amd";

            if (gpuName.Contains("intel") && gpuName.Contains("arc"))
                return "intel";

            return null;
        }

        // NEW METHOD - Detect CPU manufacturer
        private string DetectCpuManufacturer(string cpuMfg)
        {
            if (string.IsNullOrEmpty(cpuMfg))
                return null;

            cpuMfg = cpuMfg.ToLower();

            if (cpuMfg.Contains("intel"))
                return "intel";

            if (cpuMfg.Contains("amd"))
                return "amd";

            return null;
        }

        private UpdatePriority CompareVersions(string current, string latest)
        {
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(latest))
                return UpdatePriority.Medium;

            var currentParts = current.Split('.').Select(p =>
            {
                int.TryParse(new string(p.Where(char.IsDigit).ToArray()), out int num);
                return num;
            }).ToArray();

            var latestParts = latest.Split('.').Select(p =>
            {
                int.TryParse(new string(p.Where(char.IsDigit).ToArray()), out int num);
                return num;
            }).ToArray();

            if (currentParts.Length > 0 && latestParts.Length > 0)
            {
                if (currentParts[0] < latestParts[0])
                    return UpdatePriority.High;

                if (currentParts[0] == latestParts[0])
                {
                    if (currentParts.Length > 1 && latestParts.Length > 1)
                    {
                        if (currentParts[1] < latestParts[1])
                            return UpdatePriority.Medium;
                    }
                }
            }

            return UpdatePriority.UpToDate;
        }
    }
}