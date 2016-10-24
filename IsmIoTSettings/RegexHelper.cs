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
        /// Matches if only alphanumerical characters are used, including underscore and hyphen, minimum length 1
        /// </summary>
        public static Regex Text = new Regex(@"^[A-Za-z0-9_-]+$");
    }
}