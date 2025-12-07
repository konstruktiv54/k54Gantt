using System.Windows;
using Core.Services;
using Core.Services.UndoRedo;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Services;
using Wpf.ViewModels;
using Wpf.Views;

namespace Wpf;

/// <summary>
/// Точка входа приложения с настройкой DI контейнера.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Провайдер сервисов (DI контейнер).
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Обработчик запуска приложения.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Настраиваем DI контейнер
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();
        
        // Связываем FileService с ResourceService и ProductionCalendarService
        var fileService = Services.GetRequiredService<FileService>();
        fileService.ResourceService = Services.GetRequiredService<ResourceService>();
        fileService.ProductionCalendarService = Services.GetRequiredService<ProductionCalendarService>();

        // Создаём и показываем главное окно
        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// Регистрация сервисов в DI контейнере.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<FileService>();
        services.AddSingleton<ResourceService>();
        services.AddSingleton<ProductionCalendarService>();
        services.AddSingleton<WorkingDaysCalculator>();
        services.AddSingleton<EngagementCalculationService>();
        services.AddSingleton<AutoSaveManager>();
        services.AddSingleton<TaskCopyService>();
        services.AddSingleton<UndoRedoService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainViewModel>();
    }
}
