namespace CsvHelperDynamicClassMaps
{
    public static class StringExtensions
    {
        /// <summary>
        /// Compares <paramref name="input" /> against <paramref name="target" />. The comparison is case-sensitive.
        /// </summary>
        /// <param name="input">
        /// The input string.
        /// </param>
        /// <param name="target">
        /// The target string.
        /// </param>
        public static bool IsEqualTo(this string input, string target)
        {
            if (input is null && target is null)
            {
                return true;
            }
            if (input is null || target is null)
            {
                return false;
            }
            if (input.Length != target.Length)
            {
                return false;
            }

            return string.CompareOrdinal(input, target) == 0;
        }
    }
}