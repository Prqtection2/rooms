namespace Rooms.App.ViewModels;

/// <summary>Marshals an action onto the WPF UI thread (service events fire on background threads).</summary>
internal static class UiThread
{
    public static void Post(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}
