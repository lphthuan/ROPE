using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ROPE.Editor
{
    public static class UnityMcpConnection
    {
        [MenuItem("Tools/ROPE/Connect Unity MCP")]
        public static async void Connect()
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate => candidate.GetName().Name == "MCPForUnity.Editor");
                if (assembly == null)
                {
                    throw new InvalidOperationException("MCP for Unity package has not finished importing.");
                }

                var configType = assembly.GetType("MCPForUnity.Editor.Services.EditorConfigurationCache", true);
                var config = configType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                configType.GetMethod("SetUseHttpTransport").Invoke(config, new object[] { true });
                configType.GetMethod("SetHttpBaseUrl").Invoke(config, new object[] { "http://127.0.0.1:8080" });
                configType.GetMethod("SetHttpTransportScope").Invoke(config, new object[] { "local" });

                var locator = assembly.GetType("MCPForUnity.Editor.Services.MCPServiceLocator", true);
                var manager = locator.GetProperty("TransportManager", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var modeType = assembly.GetType("MCPForUnity.Editor.Services.Transport.TransportMode", true);
                var task = (Task<bool>)manager.GetType().GetMethod("StartAsync")
                    .Invoke(manager, new[] { Enum.Parse(modeType, "Http") });
                bool connected = await task;
                Directory.CreateDirectory("Logs/MCP");
                File.WriteAllText("Logs/MCP/EditorConnection.txt", connected ? "Connected" : "Failed");
                Debug.Log($"ROPE Unity MCP connection: {connected}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
