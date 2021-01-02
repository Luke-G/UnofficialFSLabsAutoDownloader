using System;
using System.Windows;
using FlightSimLabsDownloader.Services;
using FlightSimLabsDownloader.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlightSimLabsDownloader
{
    public partial class App
    {
        private IServiceProvider ServiceProvider { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<DownloadWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DownloadWindow>();
            services.AddSingleton<LicenceManager>();
            services.AddSingleton<DownloaderService>();
        }
    }
}
