using System.Text.RegularExpressions;

namespace IsmIoTSettings
{
    /// <summary>
    /// This class provides public Regexes to check input
    /// </summary>
    public static class RegexHelper
    {
        /// <summary>
        /// Matches if all characters are digits, minimum length 1
        /// </summary>
        public static Regex Number = new Regex(@"^\d+$");
        /// <summary>
        /// Matches if all characters are letters, minimum length 1
        /// </summary>
        public static Regex Word = new Regex(@"^[A-Za-z]+$");
        /// <summary>
        /// Matches if only alphanumerical characters, including underscore, are used, minimum length 1
        /// </summary>
        public static Regex Text = new Regex(@"^[A-Za-z0-9_]+$");
        /// <summary>
        /// Matches if it is a valid url to a BLOB in a container "fwupdates". The BLOB must be a tarfile.
        /// e.g. https://ismportalstorage.blob.core.windows.net/fwupdates/v0.0.1-alpha.tar
        /// </summary>
        public static Regex FwBlobUrl = new Regex(@"https:\/\/\w+\.blob\.core\.windows\.net\/fwupdates/.+\.tar");
        /// <summary>
        /// This regex tests a software name for validity.
        /// (.+)- Checks the prefix
        /// (r\d\.\d\.\d) Checks the release number
        /// -(.+) Checks the suffix
        /// </summary>
        public static Regex SoftwareName = new Regex(@"(.+)-(r\d\.\d\.\d)-(.+)");
    }
}