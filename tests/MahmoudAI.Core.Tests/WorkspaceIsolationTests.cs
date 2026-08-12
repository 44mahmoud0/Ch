using System;
using System.IO;
using FluentAssertions;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class WorkspaceIsolationTests
    {
        [Fact]
        public void WorkspaceManager_ShouldAllowValidWorkspacePaths()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "MahmoudWorkspaces");
            var manager = new WorkspaceIsolationManager(baseDir, NullLogger<WorkspaceIsolationManager>.Instance);

            string resolved = manager.ValidateAndResolvePath("m_123", "docs/report.md");
            resolved.Should().StartWith(manager.GetMissionWorkspacePath("m_123"));
        }

        [Fact]
        public void WorkspaceManager_ShouldBlockPathTraversal()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "MahmoudWorkspaces");
            var manager = new WorkspaceIsolationManager(baseDir, NullLogger<WorkspaceIsolationManager>.Instance);

            Action act = () => manager.ValidateAndResolvePath("m_123", "../../../etc/passwd");
            act.Should().Throw<UnauthorizedAccessException>();
        }
    }
}
