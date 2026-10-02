using System.Reflection;
using System.Runtime.InteropServices;
using HelloDotnet;

// Версия читается из атрибута сборки, который задаётся в .csproj (<Version>)
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    .Split('+')[0] ?? "unknown";

Console.WriteLine($"hello-dotnet version {version}");
Console.WriteLine("Hello from C# in GitHub Actions! 🚀📦");
Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
Console.WriteLine($"Arch: {RuntimeInformation.OSArchitecture}");
Console.WriteLine(Greeting.Greet("GitHub"));
Console.WriteLine($"Sum 1..10 = {Greeting.SumRange(1, 10)}");

if (args.Length > 0)
{
    Console.WriteLine("Аргументы:");
    for (int i = 0; i < args.Length; i++)
    {
        Console.WriteLine($"  {i + 1}: {args[i]}");
    }
}

return 0;
