using System;

namespace Corker.UI
{
    public class Program
    {
        // This entry point is used when building for generic net9.0 (e.g. on Linux for CI/verification)
        // because MAUI platforms (iOS, Android, Mac, Windows) have their own entry points in Platforms/
        public static void Main(string[] args)
        {
            Console.WriteLine("Corker UI build verification successful.");
        }
    }
}
