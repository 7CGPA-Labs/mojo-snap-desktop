using System;
using System.Linq;
using Makaretu.Dns;
using Fleck;

namespace EmuFrontend.CoreInterop
{
    public class EcosystemController
    {
        private MulticastService _mdns;
        private ServiceDiscovery _sd;
        private ServiceProfile _profile;
        private WebSocketServer _wsServer;
        private CoreManager _coreManager;

        private readonly object _socketsLock = new object();
        private System.Collections.Generic.List<IWebSocketConnection> _connectedSockets = new System.Collections.Generic.List<IWebSocketConnection>();

        public EcosystemController(CoreManager coreManager, int port = 8080, string serverName = "Mojo Desktop PC")
        {
            _coreManager = coreManager;
            
            _coreManager.OnCoreLoaded += (coreName) =>
            {
                var payload = $"{{\"event\": \"core_loaded\", \"core\": \"{coreName}\"}}";
                lock (_socketsLock)
                {
                    foreach (var socket in _connectedSockets)
                    {
                        socket.Send(payload);
                    }
                }
            };

            // Start WebSocket Server
            _wsServer = new WebSocketServer($"ws://0.0.0.0:{port}");
            _wsServer.SupportedSubProtocols = new[] { "controller" };
            _wsServer.Start(socket =>
            {
                socket.OnOpen = () => 
                {
                    Logger.Info($"Virtual Controller connected: {socket.ConnectionInfo.ClientIpAddress}");
                    lock (_socketsLock) { _connectedSockets.Add(socket); }
                    if (!string.IsNullOrEmpty(_coreManager.CurrentCoreName))
                    {
                        socket.Send($"{{\"event\": \"core_loaded\", \"core\": \"{_coreManager.CurrentCoreName}\"}}");
                    }
                };
                socket.OnClose = () => 
                {
                    Logger.Info($"Virtual Controller disconnected: {socket.ConnectionInfo.ClientIpAddress}");
                    lock (_socketsLock) { _connectedSockets.Remove(socket); }
                };
                socket.OnBinary = (data) =>
                {
                    if (data.Length >= 3)
                    {
                        byte playerIdx = data[0]; // 1 or 2
                        byte actionPhase = data[1]; // 1=DOWN, 2=UP, 3=AXIS
                        byte inputId = data[2];

                        if (inputId == 11) return;

                        if (actionPhase == 1 || actionPhase == 2)
                        {
                            bool isDown = actionPhase == 1;
                            if (playerIdx == 1 && inputId < 16)
                            {
                                _coreManager.VirtualP1Buttons[inputId] = isDown;
                            }
                            else if (playerIdx == 2 && inputId < 16)
                            {
                                _coreManager.VirtualP2Buttons[inputId] = isDown;
                            }
                        }
                        // Note: Axis not fully mapped in CoreManager yet, ignoring for now.
                    }
                };
            });

            // Start mDNS Broadcasting
            _mdns = new MulticastService();
            _sd = new ServiceDiscovery(_mdns);
            
            _profile = new ServiceProfile(serverName, "_retroconsole._tcp", (ushort)port);
            _profile.AddProperty("port", port.ToString());
            _profile.AddProperty("serverName", serverName);
            _profile.AddProperty("hostType", "desktop");
            
            _sd.Advertise(_profile);
            _mdns.Start();
            Logger.Info($"mDNS broadcasting started on port {port} for {serverName}");
        }

        public void Stop()
        {
            _sd?.Unadvertise(_profile);
            _mdns?.Stop();
            _wsServer?.Dispose();
        }
    }
}
