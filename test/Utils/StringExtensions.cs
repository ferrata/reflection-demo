namespace test.Utils
{
    internal static class StringExtensions
    {
        public static string Wash(this string input)
        {
            return input
                .Trim()
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\t", "    ");
        }
    }
}