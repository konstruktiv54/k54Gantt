using System.Windows;
using Core.Services;
using Microsoft.Extensions.DependencyInjection;
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
        // Core Services (Singleton - один экземпляр на всё приложение)
        services.AddSingleton<FileService>();
        services.AddSingleton<ResourceService>();
        
        // EngagementCalculationService — зависит от ResourceService,
        // ProjectManager устанавливается позже через свойство
        services.AddSingleton<EngagementCalculationService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
    }
}
