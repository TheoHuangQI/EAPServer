using EAPServer;
using Microsoft.Extensions.Configuration;

try
{
    IConfiguration config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", true, true)
        .Build();
    StartUp.InitLoad(config);

}
catch (Exception e)
{
    Console.WriteLine($"程序错误:{e.StackTrace}");
}
