using System;

namespace EzCraftModManager.Models;

public class DownloadProgress
{
    public string FileName { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100 : 0;
    public string Status { get; set; } = string.Empty;
    public DownloadState State { get; set; } = DownloadState.Pending;
    public string? ErrorMessage { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public double DownloadSpeed { get; set; } // bytes per second

    public string FormattedProgress => $"{FormatBytes(BytesReceived)} / {FormatBytes(TotalBytes)}";
    public string FormattedSpeed => $"{FormatBytes((long)DownloadSpeed)}/s";

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

public enum DownloadState
{
    Pending,
    Downloading,
    Completed,
    Failed,
    Cancelled
}

public class InstallProgress
{
    public string CurrentStep { get; set; } = string.Empty;
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public double OverallProgress => TotalSteps > 0 ? (double)CurrentStepIndex / TotalSteps * 100 : 0;
    public string DetailMessage { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
}
