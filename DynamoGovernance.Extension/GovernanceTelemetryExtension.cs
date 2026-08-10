using Dynamo.Extensions;

namespace DynamoGovernance.Extension
{
    public class GovernanceTelemetryExtension : IExtension
    {
        public string UniqueId => "F2BA577E-4C5C-4A37-8BFC-2A5C11FAC698";

        public string Name => "Dynamo Governance Telemetry";

        public void Dispose()
        {
        }

        public void Ready(ReadyParams sp)
        {
            WriteTestMessage("Ready");
        }

        public void Shutdown()
        {
            WriteTestMessage("Shutdown");
        }

        public void Startup(StartupParams sp)
        {
            WriteTestMessage("Startup");
        }
        private static void WriteTestMessage(string lifecycleEvent)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "DynamoGovernance",
                "Logs");

            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "extension-test.log");

            File.AppendAllText(
                path,
                $"{DateTimeOffset.UtcNow:O} | {lifecycleEvent}{Environment.NewLine}");
        }
    }
}
