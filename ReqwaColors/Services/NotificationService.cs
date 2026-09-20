namespace ReqwaColors.Services;

/// <summary>Severity of an in-app notification.</summary>
public enum NotificationKind
{
    Success,
    Error,
    Info
}

/// <summary>
/// Raises transient in-app notifications ("Changes applied").
/// The shell window subscribes and renders them as a small toast;
/// no modal Windows message boxes are used.
/// </summary>
public sealed class NotificationService
{
    /// <summary>Raised on the UI thread context of the caller.</summary>
    public event Action<NotificationKind, string>? Notified;

    public void Success(string message) => Notified?.Invoke(NotificationKind.Success, message);
    public void Error(string message) => Notified?.Invoke(NotificationKind.Error, message);
    public void Info(string message) => Notified?.Invoke(NotificationKind.Info, message);
}