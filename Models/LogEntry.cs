namespace MmuIspApi.Models;

// system | selection | distribution | user | admin
public static class LogCategory
{
    public const string System = "system";
    public const string Selection = "selection";
    public const string Distribution = "distribution";
    public const string User = "user";
    public const string Admin = "admin";
}

// info | success | warning | error
public static class LogType
{
    public const string Info = "info";
    public const string Success = "success";
    public const string Warning = "warning";
    public const string Error = "error";
}

public class LogEntry
{
    public string Id { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Detail { get; set; }
    public string? Actor { get; set; }
    public string? Ip { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
