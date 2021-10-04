using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cybercore.Api.WebSocketNotifications;
using Cybercore.Configuration;
using Cybercore.Extensions;
using Cybercore.Messaging;
using Cybercore.Notifications.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using WebSocketManager;
using WebSocketManager.Common;

namespace Cybercore.Api
{
    public class WebSocketNotificationsRelay : WebSocketHandler
    {
        public WebSocketNotificationsRelay(WebSocketConnectionManager webSocketConnectionManager, IComponentContext ctx) :
            base(webSocketConnectionManager, new StringMethodInvocationStrategy())
        {
            messageBus = ctx.Resolve<IMessageBus>();
            clusterConfig = ctx.Resolve<ClusterConfig>();
            pools = clusterConfig.Pools.ToDictionary(x => x.Id, x => x);

            serializer = new JsonSerializer
            {
                ContractResolver = ctx.Resolve<JsonSerializerSettings>().ContractResolver
            };

            Relay<BlockFoundNotification>(WsNotificationType.BlockFound);
            Relay<BlockUnlockedNotification>(WsNotificationType.BlockUnlocked);
            Relay<BlockConfirmationProgressNotification>(WsNotificationType.BlockUnlockProgress);
            Relay<NewChainHeightNotification>(WsNotificationType.NewChainHeight);
            Relay<PaymentNotification>(WsNotificationType.Payment);
            Relay<HashrateNotification>(WsNotificationType.HashrateUpdated);
        }

        private readonly IMessageBus messageBus;
        private readonly ClusterConfig clusterConfig;
        private readonly Dictionary<string, PoolConfig> pools;
        private readonly JsonSerializer serializer;
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public override async Task OnConnected(WebSocket socket)
        {
            WebSocketConnectionManager.AddSocket(socket);

            var greeting = ToJson(WsNotificationType.Greeting, new { Message = "Connected to Cybercore notification relay" });
            await socket.SendAsync(greeting, CancellationToken.None);
        }

        private void Relay<T>(WsNotificationType type)
        {
            messageBus.Listen<T>()
                .Select(x => Observable.FromAsync(() => BroadcastNotification(type, x)))
                .Concat()
                .Subscribe();
        }

        private async Task BroadcastNotification<T>(WsNotificationType type, T notification)
        {
            try
            {
                var json = ToJson(type, notification);

                var msg = new Message
                {
                    MessageType = MessageType.TextRaw,
                    Data = json
                };

                await SendMessageToAllAsync(msg);
            }

            catch(Exception ex)
            {
                logger.Error(ex);
            }
        }

        private string ToJson<T>(WsNotificationType type, T msg)
        {
            var result = JObject.FromObject(msg, serializer);
            result["type"] = type.ToString().ToLower();

            return result.ToString(Formatting.None);
        }
    }
}