using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public interface ITaskExecutor
    {
        Task<TaskExecutionResult> ExecuteAsync(
            string missionId,
            MissionTaskDefinition task,
            CancellationToken missionToken);
    }
}
