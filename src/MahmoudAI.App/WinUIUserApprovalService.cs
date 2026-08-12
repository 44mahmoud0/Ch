using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MahmoudAI.Core.Security;

namespace MahmoudAI.App
{
    public class WinUIUserApprovalService : IUserApprovalService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Func<XamlRoot?> _xamlRootProvider;

        public WinUIUserApprovalService(DispatcherQueue dispatcherQueue, Func<XamlRoot?> xamlRootProvider)
        {
            _dispatcherQueue = dispatcherQueue;
            _xamlRootProvider = xamlRootProvider;
        }

        public async Task<bool> RequestApprovalAsync(CapabilityType capability, string scope, CancellationToken ct)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                return await ShowPermissionDialogAsync(capability, scope, ct);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            bool queued = _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    bool result = await ShowPermissionDialogAsync(capability, scope, ct);
                    tcs.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(ct);
                }
                catch (Exception)
                {
                    tcs.TrySetResult(false);
                }
            });

            if (!queued) return false;

            return await tcs.Task.WaitAsync(ct);
        }

        private async Task<bool> ShowPermissionDialogAsync(CapabilityType capability, string scope, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var xamlRoot = _xamlRootProvider();
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
                _dispatcherQueue.TryEnqueue(() =>
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
