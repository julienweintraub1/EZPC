using System.Collections.Generic;

namespace EZPC.Models
{
    public class HardwareInfo
    {
        // CPU Info
        public string CpuName { get; set; } = "";
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public string CpuManufacturer { get; set; } = "";

        // GPU Info
        public string GpuName { get; set; } = "";
        public string GpuDriverVersion { get; set; } = "";
        public string GpuDriverDate { get; set; } = "";
        public string GpuManufacturer { get; set; } = "";

        // RAM Info
        public long TotalRamGB { get; set; }

        // Storage Info
        public List<StorageInfo> Drives { get; set; } = new();

        // Scan metadata
        public DateTime ScanDate { get; set; }
    }

    public class StorageInfo
    {
        public string Name { get; set; } = "";           // "Samsung SSD 970 EVO"
        public string MediaType { get; set; } = "";      // "SSD" or "HDD"
        public long CapacityGB { get; set; }
        public long FreeSpaceGB { get; set; }
        public int HealthPercent { get; set; }           // 0-100, -1 if unknown
        public string DriveLetter { get; set; } = "";    // "C:"
    }
}