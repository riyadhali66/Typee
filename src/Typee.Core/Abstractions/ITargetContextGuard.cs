using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ITargetContextGuard
{
    TargetSkipReason? GetSkipReason();
}
