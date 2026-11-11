using System;

namespace Anathema.Net
{
    // Decides whether this process is a dedicated server. Any of these turns it on:
    //   - launching the game with -server (optionally -port 7777), e.g. Anathema.exe -batchmode -nographics -server
    //   - a Dedicated Server build (Unity defines UNITY_SERVER)
    //   - the ANATHEMA_SERVER environment variable (set by the editor's "Play as Dedicated Server" menu item)
    public static class ServerLaunch
    {
        public const string EnvironmentFlag = "ANATHEMA_SERVER";

        public static bool Requested(out ushort port)
        {
            port = NetGame.DefaultPort;
            bool server = Environment.GetEnvironmentVariable(EnvironmentFlag) == "1";
#if UNITY_SERVER
            server = true;
#endif
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-server", StringComparison.OrdinalIgnoreCase)) server = true;
                else if (string.Equals(args[i], "-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                         && ushort.TryParse(args[i + 1], out ushort parsed) && parsed > 0)
                    port = parsed;
            }
            return server;
        }
    }
}
