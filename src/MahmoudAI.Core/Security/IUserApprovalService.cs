using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Security
{
    public interface IUserApprovalService
    {
        Task<bool> RequestApprovalAsync(CapabilityType capability, string scope, CancellationToken ct);
    }
}
