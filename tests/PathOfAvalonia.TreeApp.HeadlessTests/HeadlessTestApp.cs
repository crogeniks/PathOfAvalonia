using Avalonia;
using Avalonia.Headless;
using PathOfAvalonia.TreeApp;

[assembly: AvaloniaTestApplication(typeof(PathOfAvalonia.TreeApp.HeadlessTests.HeadlessTestApp))]

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
        })
        .WithInterFont();
}
