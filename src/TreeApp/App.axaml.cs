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
        sc.AddSingleton<IUserPathService, UserPathService>();
        sc.AddSingleton<IBuildPlannerPathService, BuildPlannerPathService>();
        sc.AddSingleton<ITextFileSaveService, TextFileSaveService>();
        sc.AddSingleton<ITextFileOpenService, TextFileOpenService>();
        sc.AddSingleton<IBuildNamePrefixPromptService, BuildNamePrefixPromptService>();
        sc.AddSingleton<IGameAssetLayout, Poe1GameAssetLayout>();
        sc.AddSingleton<IGameAssetLayout, Poe2GameAssetLayout>();
        sc.AddSingleton<IGameAssetLayoutRegistry, GameAssetLayoutRegistry>();
        sc.AddSingleton<IGameAssetService, GameAssetService>();
        sc.AddSingleton<IBuildPlannerExportService, BuildPlannerExportService>();
        sc.AddSingleton<IBuildPlannerImportService, BuildPlannerImportService>();
        sc.AddSingleton<IStorageProviderAccessor, StorageProviderAccessor>();

        sc.AddTransient<IGameWorkspaceFactory, GameWorkspaceFactory>();
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
