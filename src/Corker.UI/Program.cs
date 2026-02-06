#if !WINDOWS && !MACCATALYST && !IOS && !ANDROID
using System;

namespace Corker.UI;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Corker.UI for Linux/Generic requires a platform-specific host or GTK support.");
    }
}
#endif
