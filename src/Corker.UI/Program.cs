#if !IOS && !ANDROID && !MACCATALYST && !WINDOWS
using System;

namespace Corker.UI;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("This is a build-only target for Linux verification.");
    }
}
#endif
