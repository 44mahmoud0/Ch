using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MahmoudAI.App
{
    public interface IWinUiContext
    {
        DispatcherQueue Dispatcher { get; }
        void SetXamlRoot(XamlRoot xamlRoot);
        XamlRoot GetRequiredXamlRoot();
    }

    public sealed class WinUiContext : IWinUiContext
    {
        private WeakReference<XamlRoot>? _xamlRoot;

        public DispatcherQueue Dispatcher { get; }

        public WinUiContext(DispatcherQueue dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public void SetXamlRoot(XamlRoot xamlRoot)
        {
            _xamlRoot = new WeakReference<XamlRoot>(xamlRoot);
        }

        public XamlRoot GetRequiredXamlRoot()
        {
            if (_xamlRoot is not null && _xamlRoot.TryGetTarget(out var root))
            {
                return root;
            }

            throw new InvalidOperationException("No active WinUI XamlRoot is available for user approval.");
        }
    }
}
