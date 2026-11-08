namespace Typee.Core.Models;

public enum TargetSkipReason
{
    NoForegroundWindow,
    BlocklistedProcess,
    ElevatedTarget,
    PasswordField,
    FullScreenPause
}
