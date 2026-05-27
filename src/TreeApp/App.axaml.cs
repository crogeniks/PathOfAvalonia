using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var sc = new ServiceCollection();

        sc.AddSingleton<GameRegistry>();
        sc.AddSingleton<IUserSettingsService, UserSettingsService>();
        sc.AddSingleton<IGameAssetService, GameAssetService>();
        sc.AddSingleton<ITreeAssetService, TreeAssetService>();
        sc.AddSingleton<ISpritesAssetService, SpritesAssetService>();
        sc.AddSingleton<IImportService, ImportService>();
        sc.AddSingleton<IPobBackendLocator, PobBackendLocator>();
        sc.AddSingleton<IProcessRunner, ProcessRunner>();
        sc.AddSingleton<IPobCalculationService, PobCalculationService>();

        // Singletons resolved from asset services at first request.
        sc.AddSingleton(sp => sp.GetRequiredService<ITreeAssetService>().Load("3.28.0"));
        sc.AddSingleton(sp => sp.GetRequiredService<ISpritesAssetService>().Load("3.28.0"));

        // PassiveSpec constructor-injects TreeModel.
        sc.AddSingleton<PassiveSpec>();

        sc.AddSingleton<EquipmentViewModel>();
        sc.AddSingleton<ShellViewModel>();
        sc.AddSingleton<MainWindow>();

        Services = sc.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
