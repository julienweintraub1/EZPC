namespace EZPC.Models
{
    public class HardwareInfo
    {
        // CPU Info
        public string CpuName { get; set; }
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public string CpuManufacturer { get; set; }  // NEW - Added for Intel/AMD detection

        // GPU Info
        public string GpuName { get; set; }
        public string GpuDriverVersion { get; set; }
        public string GpuDriverDate { get; set; }
        public string GpuManufacturer { get; set; }  // NEW - Added for NVIDIA/AMD/Intel detection

        // RAM Info
        public long TotalRamGB { get; set; }

        // OS Info
        public string WindowsVersion { get; set; }
        public string WindowsBuild { get; set; }

        // Motherboard & BIOS Info - ALL NEW
        public string MotherboardManufacturer { get; set; }
        public string MotherboardModel { get; set; }
        public string BiosVersion { get; set; }
        public string BiosDate { get; set; }

        // Scan metadata
        public DateTime ScanDate { get; set; }
    }
}