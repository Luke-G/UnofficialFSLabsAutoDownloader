namespace FlightSimLabsDownloader.Entities
{
    public class Licence
    {
        public FlightSimLabsProduct Product;
        public string EmailAddress;
        public string SerialKey;
        public string OrderId;
        public string Simulator;

        /// <summary>
        /// Budget way of getting access to a unique licence without messing around with
        /// data structures or optimising the entities. Would strongly recommend refactoring
        /// this for extended use if required.
        /// </summary>
        /// <returns>A unique ID for getting a licence without much querying.</returns>
        public string GetLicenceLocatorToken()
        {
            return SerialKey.Replace("-", "") + Simulator;
        }

        /// <summary>
        /// Rudimentary validation checks to prevent null or obviously invalid values.
        /// This isn't intended to be a fully fledged validation method.
        /// </summary>
        /// <returns>Whether or not the licence details are considered valid.</returns>
        public bool IsValid()
        {
            return EmailAddress?.Length > 3 && SerialKey?.Length > 10 && OrderId?.Length > 10;
        }
    }
}