using System;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class MCV : SteamCMDAgent
    {
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.MCV",
            author = "Dr.Abc",
            description = "WindowsGSM plugin for supporting Military Conflict: Vietnam Dedicated Server",
            version = "1.0",
            url = "https://github.com/DrAbcOfficial/WindowsGSM.MCV", // Github repository link (Best practice)
            color = "#4b5320" // Color Hex
        };


        // - Standard Constructor and properties
        public MCV(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;


        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true; // MCV requires to login steam account to install the server, so loginAnonymous = false
        public override string AppId => "1136190"; // Game server appId, MCV is 1136190


        // - Game server Fixed variables
        public override string StartPath => "srcds_x64.exe"; // Game server start path, for MCV, it is srcds_x64.exe
        public string FullName = "Military Conflict: Vietnam Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "27015"; // Default port
        public string QueryPort = "27015"; // Default query port
        public string Game  = "vietnam"; // Default game name
        public string Defaultmap = "mcv_camp"; // Default map name
        public string Maxplayers = "16"; // Default maxplayers
        public string Additional = "-nocrashdialog +clientport {{clientport}} +log on"; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() 
        { 
            //Download server.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, Game, "cfg/server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{hostname}}", serverData.ServerName);
                configText = configText.Replace("{{rcon_password}}", serverData.GetRCONPassword());
                File.WriteAllText(configPath, configText);
            }

            //Edit WindowsGSM.cfg
            string configFile = Functions.ServerPath.GetServersConfigs(serverData.ServerID, "WindowsGSM.cfg");
            if (File.Exists(configFile))
            {
                string configText = File.ReadAllText(configFile);
                configText = configText.Replace("{{clientport}}", (int.Parse(serverData.ServerPort) - 10).ToString());
                File.WriteAllText(configFile, configText);
            }
        }


         public async Task<Process> Start()
        {
            string path = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(path))
            {
                Error = $"{StartPath} not found ({path})";
                return null;
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, Game, "cfg/server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"server.cfg not found ({configPath})";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"-console -game {Game}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerIP) ? string.Empty : $" -ip {serverData.ServerIP}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerPort) ? string.Empty : $" -port {serverData.ServerPort}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerMaxPlayer) ? string.Empty : $" -maxplayers {serverData.ServerMaxPlayer}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerGSLT) ? string.Empty : $" +sv_setsteamaccount {serverData.ServerGSLT}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerParam) ? string.Empty : $" {serverData.ServerParam}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerMap) ? string.Empty : $" +map {serverData.ServerMap}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerName) ? string.Empty : $" +hostname {serverData.ServerName}");
            if (serverData.ServerParam.Contains("-game ")) { sb.Replace($" -game {Game}", ""); }
            string param = sb.ToString();

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                        FileName = path,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                        FileName = path,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "quit");
            });
        }
    }
}