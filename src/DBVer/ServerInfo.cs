using System.Text;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace DBVer
{
    class ServerInfo
    {
        private readonly Server server;
        private Scripter scripter;

        public ServerInfo(string serverHost, string userName, string password)
        {
            var connection = new ServerConnection(serverHost, userName, password) { NonPooledConnection = true, AutoDisconnectMode = AutoDisconnectMode.NoAutoDisconnect };
            server = new Server(connection);
            server.ConnectionContext.Connect();
            CreateScripter();
        }

        public Server Server => server;
        public Scripter Scripter => scripter;

        void CreateScripter()
        {
            scripter = new Scripter(server);
            scripter.Options = new ScriptingOptions
                {
                    IncludeHeaders = false,
                    Indexes = true,
                    DriAllKeys = true,
                    NoCollation = false,
                    SchemaQualify = true,
                    SchemaQualifyForeignKeysReferences = true,
                    Permissions = false,
                    Encoding = Encoding.UTF8
                };
        }

        public void Disconnect()
        {
            server.ConnectionContext.Disconnect();
        }
    }
}
