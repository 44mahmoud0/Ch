using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MahmoudAI.Core.Security;

namespace MahmoudAI.App
{
    public sealed class WinUIUserApprovalService : IUserApprovalService
    {
        private readonly IWinUiContext _ui;

        public WinUIUserApprovalService(IWinUiContext ui)
        {
            _ui = ui;
        }

        public async Task<bool> RequestApprovalAsync(CapabilityType capability, string scope, CancellationToken ct)
        {
            if (_ui.Dispatcher.HasThreadAccess)
            {
                return await ShowAsync(capability, scope, ct);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            bool queued = _ui.Dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    bool result = await ShowAsync(capability, scope, ct);
                    tcs.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(ct);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            if (!queued) return false;

            return await tcs.Task.WaitAsync(ct);
        }

        private async Task<bool> ShowAsync(CapabilityType capability, string scope, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var xamlRoot = _ui.GetRequiredXamlRoot();
            var dialog = new ContentDialog
            {
                Title = "Security Permission Request",
                Content = $"Mahmoud AI requests {capability}\nScope: {scope}",
                PrimaryButtonText = "Allow",
                CloseButtonText = "Deny",
                XamlRoot = xamlRoot
            };

            using CancellationTokenRegistration registration = ct.Register(() =>
            {
                _ui.Dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        dialog.Hide();
                    }
                    catch
                    {
                        // Dialog may already be closed
                    }
                });
            });

            ContentDialogResult result = await dialog.ShowAsync();
            ct.ThrowIfCancellationRequested();

            return result == ContentDialogResult.Primary;
        }
    }
}
