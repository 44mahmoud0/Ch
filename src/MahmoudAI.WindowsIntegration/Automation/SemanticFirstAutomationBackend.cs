using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Integration;

namespace MahmoudAI.WindowsIntegration.Automation
{
    internal sealed class SemanticFirstAutomationBackend : IWindowsAutomationBackend, IDisposable
    {
        private readonly Uia3AutomationBackend _uia3;
        private readonly IWindowsAutomationBackend _win32;

        public SemanticFirstAutomationBackend(Uia3AutomationBackend uia3, IWindowsAutomationBackend win32)
        {
            _uia3 = uia3 ?? throw new ArgumentNullException(nameof(uia3));
            _win32 = win32 ?? throw new ArgumentNullException(nameof(win32));
        }

        public async Task<AutomationResult> ExecuteAsync(
            AutomationRequest request,
            CancellationToken cancellationToken)
        {
            var semanticResult = await _uia3.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (semanticResult.Succeeded
                || request.Operation is AutomationOperation.SetValue or AutomationOperation.Capture)
            {
                return semanticResult;
            }

            return await _win32.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _uia3.Dispose();
            if (_win32 is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
