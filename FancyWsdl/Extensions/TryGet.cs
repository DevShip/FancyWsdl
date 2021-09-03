namespace FancyWsdl.Extensions
{
    public static class TryGetExtensions
    {
        public static bool TryGetNotNullOrEmpty(this string src, out string outStr)
        {
            if (!string.IsNullOrEmpty(src))
            {
                outStr = src;
                return true;
            }
            else
            {
                outStr = null;
                return false;
            }

        }
    }
}
