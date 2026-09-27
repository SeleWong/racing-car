// ---------------------------------------------------------------------------
// 飞机大战 · 权威服务器入口
//   dotnet run --project Server            (默认 8080 端口)
//   dotnet run --project Server -- --port 9000 --seed 123
// 客户端连接:ws://<本机IP>:8080/  —— 多端同时接入即同局游戏。
// ---------------------------------------------------------------------------
using System;
using System.Threading;

namespace PlaneWar.Server
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            int port = 8080;
            uint seed = (uint)Environment.TickCount;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--port" && i + 1 < args.Length) int.TryParse(args[i + 1], out port);
                if (args[i] == "--seed" && i + 1 < args.Length) uint.TryParse(args[i + 1], out seed);
            }

            string prefix = "http://+:" + port + "/";
            var server = new WsServer(prefix, seed);
            try
            {
                server.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to start listener: " + ex.Message);
                Console.Error.WriteLine("On some platforms binding 'http://+:' may require elevated privileges;");
                Console.Error.WriteLine("try a different port or run with admin rights.");
                return 1;
            }

            Console.WriteLine("==============================================");
            Console.WriteLine("  PlaneWar GameServer (authoritative)");
            Console.WriteLine("  WebSocket endpoint : ws://<this-host>:" + port + "/");
            Console.WriteLine("  Seed               : " + seed);
            Console.WriteLine("  Tick rate          : 60Hz sim / 20Hz snapshot");
            Console.WriteLine("==============================================");
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Shutting down...");
                Environment.Exit(0);
            };
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
    }
}
