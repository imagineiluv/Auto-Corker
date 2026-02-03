#if !MACCATALYST && !WINDOWS && !ANDROID && !IOS
using System;

namespace Corker.UI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("This is a dummy entry point for build verification on Linux.");
        }
    }
}
#endif
