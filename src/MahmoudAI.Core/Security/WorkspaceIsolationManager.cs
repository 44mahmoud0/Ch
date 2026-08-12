using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Security
{
    public class WorkspaceIsolationManager
    {
        private readonly string _baseDirectory;
        private readonly ILogger<WorkspaceIsolationManager> _logger;

        public WorkspaceIsolationManager(string baseDirectory, ILogger<WorkspaceIsolationManager> logger)
        {
            _baseDirectory = Path.GetFullPath(baseDirectory);
            _logger = logger;
            Directory.CreateDirectory(_baseDirectory);
        }

        public string GetMissionWorkspacePath(string missionId)
        {
            string path = Path.GetFullPath(Path.Combine(_baseDirectory, "missions", missionId, "workspace"));
            Directory.CreateDirectory(path);
            return path;
        }

        public string ValidateAndResolvePath(string missionId, string relativeOrAbsolutePath)
        {
            string workspacePath = GetMissionWorkspacePath(missionId);
            string fullPath;

            if (Path.IsPathRooted(relativeOrAbsolutePath))
            {
                fullPath = Path.GetFullPath(relativeOrAbsolutePath);
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativeOrAbsolutePath));
            }

            if (!fullPath.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt detected and blocked: {Path}", relativeOrAbsolutePath);
                throw new UnauthorizedAccessException($"Access denied: Path '{relativeOrAbsolutePath}' escapes mission workspace boundaries.");
            }

            return fullPath;
        }
    }
}
