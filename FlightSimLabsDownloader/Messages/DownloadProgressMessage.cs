using FlightSimLabsDownloader.Entities;

namespace FlightSimLabsDownloader.Messages
{
    public class DownloadProgressMessage : Message
    {
        public Licence Licence { get; set; }
    }
}