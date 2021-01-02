using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Channels;
using System.Threading.Tasks;
using FlightSimLabsDownloader.Entities;
using FlightSimLabsDownloader.Messages;
using HtmlAgilityPack;

namespace FlightSimLabsDownloader.Services
{
    public class DownloaderService
    {
        private readonly Channel<Message> downloadChannel;
        private readonly HttpClient httpClient = new HttpClient();
        private const string FlightSimLabsDownloadEndpoint = "https://redownload.flightsimlabs.com/";

        public DownloaderService()
        {
            downloadChannel = Channel.CreateUnbounded<Message>();
        }

        /// <summary>
        /// Returns the channel reader for the downloads channel.
        /// </summary>
        /// <returns>Downloads channel reader.</returns>
        public ChannelReader<Message> GetChannelReader()
        {
            return downloadChannel.Reader;
        }

        /// <summary>
        /// Start the automation flow and begin downloading the product for a given licence.
        /// </summary>
        /// <param name="licence">The licence to download a product for.</param>
        /// <returns>Headers for the file being downloaded.</returns>
        public async Task<HttpContentHeaders> DownloadProductAsync(Licence licence)
        {
            var web = new HtmlWeb();
            HtmlDocument orderLookupDoc = web.Load(FlightSimLabsDownloadEndpoint);

            string orderLookupViewstate = orderLookupDoc.DocumentNode
                .SelectSingleNode("//input[@id='__VIEWSTATE']")
                .GetAttributeValue("value", string.Empty);

            var requestParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("txtOrderId", licence.OrderId),
                new KeyValuePair<string, string>("txtSerial", licence.SerialKey),
                new KeyValuePair<string, string>("txtEmail", licence.EmailAddress),
                new KeyValuePair<string, string>("__EVENTTARGET", "FindOrder"),
                new KeyValuePair<string, string>("__VIEWSTATE", orderLookupViewstate)
            };

            HttpResponseMessage orderLookupResponse = await httpClient.PostAsync(FlightSimLabsDownloadEndpoint,
                new FormUrlEncodedContent(requestParams));

            string orderLookupResponseString = await orderLookupResponse.Content.ReadAsStringAsync();

            if (!orderLookupResponseString.Contains("Order lookup succeeded"))
            {
                Console.WriteLine("Failed to get a successful response.");
            }

            var downloadDoc = new HtmlDocument();
            downloadDoc.LoadHtml(orderLookupResponseString);

            string downloadViewstate = downloadDoc.DocumentNode
                .SelectSingleNode("//input[@id='__VIEWSTATE']")
                .GetAttributeValue("value", string.Empty);

            requestParams.Remove(requestParams.Find(q => q.Key == "__VIEWSTATE"));
            requestParams.Add(new KeyValuePair<string, string>("__VIEWSTATE", downloadViewstate));

            requestParams.Remove(requestParams.Find(q => q.Key == "__EVENTTARGET"));
            requestParams.Add(new KeyValuePair<string, string>("__EVENTTARGET", "btnDownload"));

            // Start the download.
            var downloadRequest = new HttpRequestMessage
            {
                Content = new FormUrlEncodedContent(requestParams),
                RequestUri = new Uri(FlightSimLabsDownloadEndpoint),
                Method = HttpMethod.Post
            };

            HttpResponseMessage downloadResponse = await httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead);

            string downloadsFolder = GetUserDownloadsFolder();
            string fileName = Path.Combine(downloadsFolder, downloadResponse.Content.Headers.ContentDisposition.FileName);

            Task<Stream> readAsStreamTask = downloadResponse.Content.ReadAsStreamAsync();

            _ = Task.Run(() => DownloadFile(readAsStreamTask, fileName, downloadResponse.Content.Headers, licence));

            return downloadResponse.Content.Headers;
        }

        // This won't always work, but it's good enough for the majority of users.
        public static string GetUserDownloadsFolder()
        {
            return Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE")!, "Downloads");
        }

        private async Task DownloadFile(Task<Stream> readAsStreamTask, string fileName, HttpContentHeaders fileHeaders, Licence licence)
        {
            try
            {
                var totalBytesRead = 0L;
                var readCount = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                await using Stream contentStream = await readAsStreamTask, fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                downloadChannel.Writer.TryWrite(new DownloadProgressMessage
                {
                    Action = MessageAction.DOWNLOAD_HEADERS,
                    Content =  $"Downloading: {fileHeaders.ContentDisposition.FileName}. Size: {FormatBytes(fileHeaders.ContentLength)}",
                    Licence = licence
                });

                do
                {
                    int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        TriggerProgressChanged(fileHeaders.ContentLength, totalBytesRead, licence);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 100 == 0)
                        TriggerProgressChanged(fileHeaders.ContentLength, totalBytesRead, licence);
                } while (isMoreToRead);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead, Licence licence)
        {
            if (!totalDownloadSize.HasValue)
                return;

            downloadChannel.Writer.TryWrite(new DownloadProgressMessage
            {
                Action = MessageAction.DOWNLOAD_PROGRESS,
                Content = Math.Round((double) totalBytesRead / totalDownloadSize.Value * 100, 2),
                Licence = licence
            });
        }

        private static string FormatBytes(long? bytesRaw)
        {
            if (bytesRaw == null)
                return "0B";

            long bytes = bytesRaw.GetValueOrDefault();

            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };

            int i;
            double dblSByte = bytes;

            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
                dblSByte = bytes / 1024.0;

            return $"{dblSByte:0.##} {Suffix[i]}";
        }
    }
}