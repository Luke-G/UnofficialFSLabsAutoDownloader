using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FlightSimLabsDownloader.Entities;
using FlightSimLabsDownloader.Messages;
using FlightSimLabsDownloader.Services;

namespace FlightSimLabsDownloader.Views
{
    public partial class DownloadWindow
    {
        private readonly LicenceManager licenceManager;
        private readonly DownloaderService downloader;

        public DownloadWindow(LicenceManager licenceManager, DownloaderService downloaderService)
        {
            InitializeComponent();

            var cancellationToken = new CancellationToken();

            this.licenceManager = licenceManager;
            downloader = downloaderService;

            Task.Factory.StartNew(() => StartListeningToDownloadChannel(downloader.GetChannelReader(), cancellationToken));

            DisplayInstalledProducts();
        }

        private void DisplayInstalledProducts()
        {
            foreach (Licence licence in licenceManager.GetLicences())
            {
                selectedDownloads.Items.Add(new ListBoxItem
                {
                    Name = $"{licence.GetLicenceLocatorToken()}",
                    Content = $"{licence.Product} - {licence.Simulator}"
                });
            }
        }

        /// <summary>
        /// Starts downloading the currently selected products and configures the UI elements.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartDownloads(object sender, RoutedEventArgs e)
        {
            var marginSetting = new Thickness(0, 0, 0, 10);

            IEnumerable<Licence> licencesForProductsToDownload = licenceManager
                .GetLicences()
                .Where(q => (selectedDownloads.SelectedItems)
                    .Cast<ListBoxItem>()
                    .Select(x => x.Name)
                    .Contains(q.GetLicenceLocatorToken()))
                .ToList();

            if (!licencesForProductsToDownload.Any())
                return;

            startDownloadButton.IsEnabled = false;
            selectedDownloads.IsEnabled = false;

            foreach (Licence licence in licencesForProductsToDownload)
            {
                var grid = new Grid
                {
                    Name = $"{licence.GetLicenceLocatorToken()}grid",
                    Width = 550,
                    Height = 60
                };

                grid.Children.Add(new ProgressBar
                {
                    Name = $"{licence.GetLicenceLocatorToken()}progressBar",
                    Value = 0,
                    Height = 60,
                    Width = 550,
                    Margin = marginSetting
                });

                grid.Children.Add(new TextBlock
                {
                    Name = $"{licence.GetLicenceLocatorToken()}progressBarTopText",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = $"Starting download for {licence.Product} - {licence.Simulator}...",
                    Height = 15,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 30)
                });

                grid.Children.Add(new TextBlock
                {
                    Name = $"{licence.GetLicenceLocatorToken()}progressBarBottomText",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "0%",
                    FontSize = 12,
                    Height = 15
                });

                downloadsPanel.Children.Add(grid);
            }

            // Fire off the tasks.
            _ = licencesForProductsToDownload.Select(productLicence => downloader.DownloadProductAsync(productLicence)).ToArray();
        }

        /// <summary>
        /// Start the channel listener to watch for download events.
        /// </summary>
        /// <param name="channelReader">Reader for the download channel to process messages on.</param>
        /// <param name="cancellationToken">Cancellation token to stop listening to the channel.</param>
        /// <returns>Task.</returns>
        private async Task StartListeningToDownloadChannel(ChannelReader<Message> channelReader, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Message message = await channelReader.ReadAsync(cancellationToken);

                var downloadProgressMessage = message as DownloadProgressMessage;

                if (downloadProgressMessage == null)
                    continue;

                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Grid downloadGrid = downloadsPanel.Children.Cast<Grid>()
                            .First(q => q.Name.Contains(downloadProgressMessage!.Licence.GetLicenceLocatorToken()));

                        IEnumerable<UIElement> gridChildren = downloadGrid.Children.Cast<UIElement>().ToList();

                        if (downloadProgressMessage.Action == MessageAction.DOWNLOAD_HEADERS)
                        {
                            UIElement progressBarTopText = gridChildren
                                .Where(q => q is TextBlock)
                                .First(q => (q as TextBlock)!.Name.Contains("progressBarTopText"));
                            (progressBarTopText as TextBlock)!.Text = message.Content;
                        }
                        else if (downloadProgressMessage.Action == MessageAction.DOWNLOAD_PROGRESS)
                        {
                            UIElement progressBarBottomText = gridChildren
                                .Where(q => q is TextBlock)
                                .First(q => (q as TextBlock)!.Name.Contains("progressBarBottomText"));
                            (progressBarBottomText as TextBlock)!.Text = $"{message.Content}%";

                            UIElement progressBar = gridChildren.First(q => q is ProgressBar);
                            (progressBar as ProgressBar)!.Value = message.Content;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });
            }
        }

        private void OpenDownloadsFolder(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", DownloaderService.GetUserDownloadsFolder());
        }

        private void OpenAboutPage(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.Show();
        }
    }
}