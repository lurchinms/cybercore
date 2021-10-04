using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cybercore.Blockchain;
using Cybercore.Configuration;
using Cybercore.Contracts;
using Cybercore.Extensions;
using Cybercore.Messaging;
using Cybercore.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NLog;
using ProtoBuf;
using ZeroMQ;

namespace Cybercore.Mining
{
    public class ShareRelay : IHostedService
    {
        public ShareRelay(
            ClusterConfig clusterConfig,
            IMessageBus messageBus)
        {
            Contract.RequiresNonNull(messageBus, nameof(messageBus));

            this.clusterConfig = clusterConfig;
            this.messageBus = messageBus;
        }

        private readonly IMessageBus messageBus;
        private readonly ClusterConfig clusterConfig;
        private readonly BlockingCollection<Share> queue = new();
        private IDisposable queueSub;
        private readonly int QueueSizeWarningThreshold = 1024;
        private bool hasWarnedAboutBacklogSize;
        private ZSocket pubSocket;

        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [Flags]
        public enum WireFormat
        {
            Json = 1,
            ProtocolBuffers = 2
        }

        public const int WireFormatMask = 0xF;

        private void InitializeQueue()
        {
            queueSub = queue.GetConsumingEnumerable()
                .ToObservable(TaskPoolScheduler.Default)
                .Do(_ => CheckQueueBacklog())
                .Subscribe(share =>
                {
                    share.Source = clusterConfig.ClusterName;
                    share.BlockRewardDouble = (double) share.BlockReward;

                    try
                    {
                        const int flags = (int) WireFormat.ProtocolBuffers;

                        using(var msg = new ZMessage())
                        {
                            msg.Add(new ZFrame(share.PoolId));
                            msg.Add(new ZFrame(flags));

                            using(var stream = new MemoryStream())
                            {
                                Serializer.Serialize(stream, share);
                                msg.Add(new ZFrame(stream.ToArray()));
                            }

                            pubSocket.SendMessage(msg);
                        }
                    }

                    catch(Exception ex)
                    {
                        logger.Error(ex);
                    }
                });
        }

        private void CheckQueueBacklog()
        {
            if(queue.Count > QueueSizeWarningThreshold)
            {
                if(!hasWarnedAboutBacklogSize)
                {
                    logger.Warn(() => $"Share relay queue backlog has crossed {QueueSizeWarningThreshold}");
                    hasWarnedAboutBacklogSize = true;
                }
            }

            else if(hasWarnedAboutBacklogSize && queue.Count <= QueueSizeWarningThreshold / 2)
            {
                hasWarnedAboutBacklogSize = false;
            }
        }

        public Task StartAsync(CancellationToken ct)
        {
            messageBus.Listen<StratumShare>().Subscribe(x => queue.Add(x.Share, ct));

            pubSocket = new ZSocket(ZSocketType.PUB);

            if(!clusterConfig.ShareRelay.Connect)
            {
                pubSocket.SetupCurveTlsServer(clusterConfig.ShareRelay.SharedEncryptionKey, logger);

                pubSocket.Bind(clusterConfig.ShareRelay.PublishUrl);

                if(pubSocket.CurveServer)
                    logger.Info(() => $"Bound to {clusterConfig.ShareRelay.PublishUrl} using Curve public-key {pubSocket.CurvePublicKey.ToHexString()}");
                else
                    logger.Info(() => $"Bound to {clusterConfig.ShareRelay.PublishUrl}");
            }

            else
            {
                if(!string.IsNullOrEmpty(clusterConfig.ShareRelay.SharedEncryptionKey?.Trim()))
                    logger.ThrowLogPoolStartupException("ZeroMQ Curve is not supported in ShareRelay Connect-Mode");

                pubSocket.Connect(clusterConfig.ShareRelay.PublishUrl);

                logger.Info(() => $"Connected to {clusterConfig.ShareRelay.PublishUrl}");
            }

            InitializeQueue();

            logger.Info(() => "Online");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            pubSocket.Dispose();

            queueSub?.Dispose();
            queueSub = null;

            return Task.CompletedTask;
        }
    }
}