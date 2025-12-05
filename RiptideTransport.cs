using Riptide;
using Riptide.Utils;
using System;
using System.Net;
using UnityEngine;

namespace Mirror {
    public class RiptideTransport : Transport {
        public override bool Available() {
            return true;
        }

        public override void ClientConnect(string address) {
            if (address == "localhost")
                address = $"127.0.0.1:{Port}";
            Client.Connect(address);
        }

        public override bool ClientConnected() {
            return Client.IsConnected;
        }

        public override void ClientDisconnect() {
            Client.Disconnect();
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId = 0) {
            var message = Message.Create(ToRiptideChannel(channelId), ClientToServerId.ClientMessage);
            message.AddBytes(segment.Array).AddInt(segment.Offset).AddInt(segment.Count);
            Client.Send(message);
        }

        public override int GetMaxPacketSize(int channelId = 0) {
            return Message.MaxSize;
        }

        public override bool ServerActive() {
            return Server.IsRunning;
        }

        public override void ServerDisconnect(int connectionId) {
            Server.DisconnectClient((ushort)connectionId);
        }

        ///
        /// Riptide's default Client class does not come with client addresses, feel free to cast the client class by inheriting this function!
        ///
        public override string ServerGetClientAddress(int connectionId) {
            return "";
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = 0) {
            var message = Message.Create(ToRiptideChannel(channelId), ServerToClientId.ServerMessage);
            message.AddBytes(segment.Array).AddInt(segment.Offset).AddInt(segment.Count);
            Server.Send(message, (ushort)connectionId);
        }
        public static int FromRiptideChannel(MessageSendMode channel) =>
            channel == MessageSendMode.Reliable ? Channels.Reliable : Channels.Unreliable;
        public static MessageSendMode ToRiptideChannel(int channel) =>
            channel == Channels.Reliable ? MessageSendMode.Reliable : MessageSendMode.Unreliable;

        public override void ServerStart() {
            Server.Start(Port, (ushort)NetworkServer.maxConnections);
        }

        public override void ServerStop() {
            Server.Stop();
        }

        public override Uri ServerUri() {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = SCHEME;
            builder.Host = Dns.GetHostName();
            builder.Port = Port;
            return builder.Uri;
        }

        public override void Shutdown() {
            ClientDisconnect();
            ServerStop();
        }

        public override void OnApplicationQuit() {
            base.OnApplicationQuit();
            Server = null;
            Client = null;
        }

        public Server Server {
            get; set;
        }
        public Client Client {
            get; set;
        }
        public static RiptideTransport singleton {
            get; private set;
        }
        [Header("Riptide Settings")]
        public ushort Port = 7777;
        public int MaxMessagePayloadSize = ushort.MaxValue;
        public bool UseRiptideLogger = true;
        public const string SCHEME = "MIRROR";
        protected virtual void Awake() {
            ///
            /// MaxPayloadSize is set to a high value cuz the default value is too low to send mirror's arrays
            ///
            Message.MaxPayloadSize = MaxMessagePayloadSize;
            if (UseRiptideLogger)
                RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogError, Debug.LogError, false);
            singleton = this;
            ///
            /// Subscrbing to events and making instances for the Server and Client riptide class
            ///
            Server = new Server(SCHEME + "SERVER");
            ///
            /// Remember that Riptide does not store address by default!
            ///
            Server.ClientConnected += (x, y) => OnServerConnectedWithAddress.Invoke(y.Client.Id, "");
            Server.ClientDisconnected += (x, y) => OnServerDisconnected.Invoke(y.Client.Id);
            Client = new Client(SCHEME + "CLIENT");
            Client.Connected += (_, _) => OnClientConnected.Invoke();
            Client.Disconnected += (_, _) => OnClientDisconnected.Invoke();
        }

        ///
        /// Using Riptide's MessageHandler to welp, handle incoming messages from client and server
        ///
        [MessageHandler((ushort)ClientToServerId.ClientMessage)]
        protected static void HandleMirrorMessageFromClient(ushort fromClientId, Message message) {
            var segment = new ArraySegment<byte>(message.GetBytes(), message.GetInt(), message.GetInt());
            singleton.OnServerDataReceived?.Invoke(fromClientId, segment, FromRiptideChannel(message.SendMode));
        }
        [MessageHandler((ushort)ServerToClientId.ServerMessage)]
        protected static void HandleMirrorMessageFromServer(Message message) {
            var segment = new ArraySegment<byte>(message.GetBytes(), message.GetInt(), message.GetInt());
            singleton.OnClientDataReceived?.Invoke(segment, FromRiptideChannel(message.SendMode));
        }

        ///
        /// Keep Riptide's peers running!
        ///
       public void Update() {
            if (!enabled) return;

            Server.Update();
            Client.Update();
        }
        
        public enum ServerToClientId : ushort {
            ServerMessage = 1,
        }
        public enum ClientToServerId : ushort {
            ClientMessage = 1,
        }
    }
   
}
