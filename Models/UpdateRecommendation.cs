namespace EZPC.Models
{
    public enum ComponentCategory
    {
        GPU,
        CPU,
        Storage
    }

    public class ComponentInfo
    {
        public ComponentCategory Category { get; set; }
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Description { get; set; } = "";
        public string ActionText { get; set; } = "";
        public string ActionUrl { get; set; } = "";
        public string? ExtraInfo { get; set; }
        public bool IsActionable { get; set; }
    }
}