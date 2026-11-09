namespace Typee.Core.Typing;

using Typee.Core.Abstractions;

public sealed class SessionLockState : ISessionLockState
{
    private int _locked;

    public bool IsLocked => Volatile.Read(ref _locked) != 0;

    public void SetLocked(bool locked) =>
        Volatile.Write(ref _locked, locked ? 1 : 0);
}
