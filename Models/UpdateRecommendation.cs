namespace EZPC.Models
{
    public enum UpdatePriority
    {
        Critical,   // Security issues or major bugs
        High,       // Performance improvements
        Medium,     // General updates
        Low,        // Optional improvements
        UpToDate    // No update needed
    }

    public class UpdateRecommendation
    {
        public string Component { get; set; }           // "GPU Driver", "Windows", etc.
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public UpdatePriority Priority { get; set; }
        public string Description { get; set; }
        public string UpdateUrl { get; set; }           // Link to manufacturer
        public string Instructions { get; set; }        // Step-by-step guide
    }
}