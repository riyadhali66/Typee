namespace Typee.Core.Abstractions;

/// <summary>Tracks whether the Windows session is locked (WTS_SESSION_LOCK).</summary>
public interface ISessionLockState
{
    bool IsLocked { get; }

    void SetLocked(bool locked);
}
