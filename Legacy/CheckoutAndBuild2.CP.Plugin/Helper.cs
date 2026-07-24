using System.Text.RegularExpressions;

namespace CheckoutAndBuild2.CP.Plugin
{
    public class Helper
    {
        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$";
        }

    }
}