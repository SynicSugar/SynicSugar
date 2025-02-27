using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SynicSugar.Base;
using ResultE = Epic.OnlineServices.Result;
//We can't call the main-Assembly from own-assemblies.
//So, use such processes through this assembly.
namespace SynicSugar.P2P {
    public sealed class EOSSessionManager : SessionCore {
        public EOSSessionManager() : base (){
            P2PHandle = EOSManager.Instance.GetEOSPlatformInterface().GetP2PInterface();
            // To get Next packet size
            standardPacketSizeOptions = new GetNextReceivedPacketSizeOptions {
                LocalUserId = SynicSugarManger.Instance.LocalUserId.AsEpic,
                RequestedChannel = null
            };

            synicPacketSizeOptions = new GetNextReceivedPacketSizeOptions {
                LocalUserId = SynicSugarManger.Instance.LocalUserId.AsEpic,
                RequestedChannel = 255
            };

            queueOptions = new GetPacketQueueInfoOptions();
        }

        /// <summary>
        /// For catch
        /// </summary>
        internal P2PInterface P2PHandle;
        /// <summary>
        /// For pointer to pass receive packet
        /// </summary>
        public SocketId ReferenceSocketId;

        ulong RequestNotifyId, InterruptedNotify, EstablishedNotify, ClosedNotify;
        // Allocate memory at maximum packet size in advance.
        byte[] buffer = new byte[1170];

        /// <summary>
        /// To get packets
        /// </summary>
        GetNextReceivedPacketSizeOptions standardPacketSizeOptions, synicPacketSizeOptions;
        GetPacketQueueInfoOptions queueOptions;
        ProductUserId productUserId;
        
    #region INetworkCore
        /// <summary>
        /// For ConnectManager. Stop packet receeiveing to buffer. While stopping, packets are dropped.
        /// </summary>
        /// <param name="isForced">If True, stop and clear current packet queue. <br />
        /// If false, process current queue, then stop it.</param>
        /// <param name="token">For this task</param>
        protected override async UniTask<Result> PauseConnections(bool isForced, CancellationToken token){
            if(isForced){
                ResetConnections();
                return Result.Success;
            }
            
            RemoveNotifyAndCloseConnection();
            
            GetPacketQueueInfoOptions options = new GetPacketQueueInfoOptions();
            PacketQueueInfo info = new PacketQueueInfo();
            P2PHandle.GetPacketQueueInfo(ref options, out info);

            while (info.IncomingPacketQueueCurrentPacketCount > 0){
                await UniTask.Yield(PlayerLoopTiming.EarlyUpdate, cancellationToken: token);
                P2PHandle.GetPacketQueueInfo(ref options, out info);
            }

            ((INetworkCore)this).StopPacketReceiver();
            return Result.Success;
        }
        /// <summary>
        /// Prepare to receive in advance. If user sent packets, it can open to get packets for a socket id without this.
        /// </summary>
        protected override Result RestartConnections(){
            return InitiateConnection();
        }
        
        /// <summary>
        /// To get Packetｓ.
        /// Use this from hub. In Unity, we can not call methods in Main-Assembly from SynicSugar.dll.
        /// </summary>
        public override bool GetPacketFromBuffer(ref byte ch, ref UserId id, ref ArraySegment<byte> payload){
            ResultE existPacket = P2PHandle.GetNextReceivedPacketSize(ref standardPacketSizeOptions, out uint nextPacketSizeBytes);
            if(existPacket != ResultE.Success){
                return false;
            }
            if(nextPacketSizeBytes > 1170){
                Logger.LogError("GetPacketFromBuffer", $"Packet size {nextPacketSizeBytes} exceeds maximum expected size of 1170.");
                return false;
            }
            //Set options
            ReceivePacketOptions options = new ReceivePacketOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                MaxDataSizeBytes = nextPacketSizeBytes,
                RequestedChannel = null
            };

            payload = new ArraySegment<byte>(buffer, 0, (int)nextPacketSizeBytes);
            ResultE result = P2PHandle.ReceivePacket(ref options, ref productUserId, ref ReferenceSocketId, out ch, payload, out uint bytesWritten);
            
            if (result != ResultE.Success){
#if SYNICSUGAR_LOG //This range is for performance since this is called every frame.
                if(result == ResultE.InvalidParameters){
                    Logger.LogError("Get Packets", "input was invalid", (Result)result);
                }
#endif
                return false; //No packet
            }
            id = UserId.GetUserId(productUserId);
        #if SYNICSUGAR_PACKETINFO
            Debug.Log($"ReceivePacket: ch: {ch} from {id.ToMaskedString()} / packet size {bytesWritten} / payload {payload.ToHexString()}");
        #endif
            return true;
        }
        /// <summary>
        /// To get only SynicPacket.
        /// Use this from ConenctHub not to call some methods in Main-Assembly from SynicSugar.dll.
        /// </summary>
        public override bool GetSynicPacketFromBuffer(ref byte ch, ref UserId id, ref ArraySegment<byte> payload){
            ResultE existPacket = P2PHandle.GetNextReceivedPacketSize(ref synicPacketSizeOptions, out uint nextPacketSizeBytes);
            if(existPacket != ResultE.Success){
                return false;
            }
            if(nextPacketSizeBytes > 1170){
                Logger.LogError("GetSynicPacketFromBuffer", $"Packet size {nextPacketSizeBytes} exceeds maximum expected size of 1170.");
                return false;
            }
            //Set options
            ReceivePacketOptions options = new ReceivePacketOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                MaxDataSizeBytes = nextPacketSizeBytes,
                RequestedChannel = 255
            };

            payload = new ArraySegment<byte>(buffer, 0, (int)nextPacketSizeBytes);
            ResultE result = P2PHandle.ReceivePacket(ref options, ref productUserId, ref ReferenceSocketId, out ch, payload, out uint bytesWritten);
            
            if (result != ResultE.Success){
#if SYNICSUGAR_LOG //This range is for performance since this is called every frame.
                if(result == ResultE.InvalidParameters){
                    Logger.LogError("Get Synic Packets", "input was invalid", (Result)result);
                }
#endif
                return false; //No packet
            }
            id = UserId.GetUserId(productUserId);
        #if SYNICSUGAR_PACKETINFO
            Debug.Log($"ReceivePacket(Synic): ch: {ch}  from {id.ToMaskedString()} / packet size {bytesWritten} / payload {payload.ToHexString()}");
        #endif
            return true;
        }
    
    #endregion
        /// <summary>
        /// Clear the packet queues.
        /// Just for PausePacketXXX.
        /// </summary>
        void ClearPacketQueue(){
            ClearPacketQueueOptions options = new ClearPacketQueueOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                SocketId = SocketId
            };

            foreach(var id in p2pInfo.Instance.userIds.RemoteUserIds){
                options.RemoteUserId = id.AsEpic;

                ResultE result = P2PHandle.ClearPacketQueue(ref options);
                
                if (result != ResultE.Success){
                    Logger.LogError("ClearPacketQueue", "can't clear packet queue", (Result)result);
                    return;
                }
            }
            Logger.Log("ClearPacketQueue", "Finish!");
        }
        public override Result GetPacketQueueInfo(out PacketQueueInformation info)
        {
            var result = (Result)P2PHandle.GetPacketQueueInfo(ref queueOptions, out PacketQueueInfo packetInfo);
            if(result != Result.Success){
                Logger.LogError("GetPacketQueueInfo", "Failed to get packet queue info.", result);
                info = new PacketQueueInformation();
            }
            else
            {
                info = new PacketQueueInformation(){
                    IncomingPacketQueueMaxSizeBytes = packetInfo.IncomingPacketQueueMaxSizeBytes,
                    IncomingPacketQueueCurrentSizeBytes = packetInfo.IncomingPacketQueueCurrentSizeBytes,
                    IncomingPacketQueueCurrentPacketCount = packetInfo.IncomingPacketQueueCurrentPacketCount,
                    OutgoingPacketQueueMaxSizeBytes = packetInfo.OutgoingPacketQueueMaxSizeBytes,
                    OutgoingPacketQueueCurrentSizeBytes = packetInfo.OutgoingPacketQueueCurrentSizeBytes,
                    OutgoingPacketQueueCurrentPacketCount = packetInfo.OutgoingPacketQueueCurrentPacketCount
                };
            }
            return result;
        }
#region Notify(ConnectRquest)
        /// <summary>
        /// Ready to receive packets of users in the same socket.
        /// </summary>
        void AddNotifyPeerConnectionRequest(){
            if (RequestNotifyId == 0){
                AddNotifyPeerConnectionRequestOptions options = new AddNotifyPeerConnectionRequestOptions(){
                    LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                    SocketId = SocketId
                };

                RequestNotifyId = P2PHandle.AddNotifyPeerConnectionRequest(ref options, null, OnIncomingConnectionRequest);
                
                if (RequestNotifyId == 0){
                    Logger.LogError("AddNotifyPeerConnectionRequest", "could not subscribe, bad notification id returned.");
                }
            }
        }
        // Call from SubscribeToConnectionRequest.
        // This function will only be called if the connection has not been accepted yet.
        void OnIncomingConnectionRequest(ref OnIncomingConnectionRequestInfo data){
            if (!(bool)data.SocketId?.SocketName.Equals(ScoketName)){
                Logger.LogError("OnIncomingConnectionRequest", "unknown socket id. This peer should be no lobby member.");
                return;
            }

            AcceptConnectionOptions options = new AcceptConnectionOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                RemoteUserId = data.RemoteUserId,
                SocketId = SocketId
            };

            ResultE result = P2PHandle.AcceptConnection(ref options);

            if (result != ResultE.Success){
                Logger.LogError("OnIncomingConnectionRequest", "error while accepting connection.", (Result)result);
                return;
            }
            Logger.Log("OnIncomingConnectionRequest", $"Accept the connection with {UserId.GetUserId(data.RemoteUserId).ToMaskedString()}");
        }
        void RemoveNotifyPeerConnectionRequest(){
            P2PHandle.RemoveNotifyPeerConnectionRequest(RequestNotifyId);
            RequestNotifyId = 0;
        }
#endregion
#region Connect
    /// <summary>
    /// Prep for p2p connections.
    /// Call from the library after the MatchMake is established.
    /// </summary>
    //* Maybe: Some processes in InitConnectConfig need time to complete and the Member list will be created after that end. Therefore, we will add Notify first to spent time.
    protected override Result InitiateConnection(bool checkInitConnect = false){
        AddNotifyPeerConnectionRequest();
        AcceptAllConenctions();

        if(checkInitConnect || p2pConfig.Instance.UseDisconnectedEarlyNotify){
            UpdatePacketOptions();
            AddNotifyPeerConnectionEstablished();
        }
        if(p2pConfig.Instance.UseDisconnectedEarlyNotify){
            AddNotifyPeerConnectionInterrupted();
        }else{
            AddNotifyPeerConnectionClosed();
        }
        return Result.Success;
    }
    /// <summary>
    /// This UserId may have changed from the pre-UserId, so update object's value before connection.
    /// </summary>
    void UpdatePacketOptions(){
        standardPacketSizeOptions.LocalUserId = SynicSugarManger.Instance.LocalUserId.AsEpic;
        synicPacketSizeOptions.LocalUserId = SynicSugarManger.Instance.LocalUserId.AsEpic;
    }
    
    //Reason: This order(Receiver, Connection, Que) is that if the RPC includes Rpc to reply, the connections are automatically re-started.
    /// <summary>
    /// Stop packet reciver, clse connections, then clear PacketQueue(incoming and outgoing).　<br />
    /// Errors here are not fatal; even if a failure occurs, the entire cleanup process will proceed to ensure all resources are released.
    /// </summary>
    protected internal override Result ResetConnections(){
        Result receiverResult = ((INetworkCore)this).StopPacketReceiver();
        if(receiverResult != Result.Success){
            Logger.LogError("ResetConnections", "Failed to stop the packet receiver.", receiverResult);
        }
        CancelRTTToken();
        Result connectionResult = RemoveNotifyAndCloseConnection();
        if(connectionResult != Result.Success){
            Logger.LogError("ResetConnections", "Failed to close the connection.", connectionResult);
        }
        ClearPacketQueue();
        return receiverResult == Result.Success ? connectionResult : receiverResult;
    }
    /// <summary>
    /// For the end of matchmaking. <br />
    /// Immediate packet reception permission in advance
    /// </summary>
    void AcceptAllConenctions(){
        ResultE result = ResultE.Success;
        ProductUserId localUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic;
        foreach(var id in p2pInfo.Instance.userIds.RemoteUserIds){
            AcceptConnectionOptions options = new AcceptConnectionOptions(){
                LocalUserId = localUserId,
                RemoteUserId = id.AsEpic,
                SocketId = SocketId
            };
            
            result = P2PHandle.AcceptConnection(ref options);

            if (result != ResultE.Success){
                Logger.LogError("AcceptAllConenctions", $"error while accepting connection for {id.ToMaskedString()}.", (Result)result);
                break;
            }
            Logger.Log("AcceptAllConenctions", $"Accept the connection with {id.ToMaskedString()}");
        }
    }
#endregion
#region Early Disconnected Notify
    void AddNotifyPeerConnectionInterrupted(){
        if (InterruptedNotify == 0){
            AddNotifyPeerConnectionInterruptedOptions options = new AddNotifyPeerConnectionInterruptedOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                SocketId = SocketId
            };

            InterruptedNotify = P2PHandle.AddNotifyPeerConnectionInterrupted(ref options, null, OnPeerConnectionInterruptedCallback);
            
            if (InterruptedNotify == 0){
                Logger.LogError("Connection Request", "could not subscribe, bad notification id returned.");
            }
        }
    }
    // Call from SubscribeToConnectionRequest.
    void OnPeerConnectionInterruptedCallback(ref OnPeerConnectionInterruptedInfo data){
        if (!(bool)data.SocketId?.SocketName.Equals(ScoketName)){
            Logger.LogError("InterruptedCallback", "unknown socket id. This peer should be no lobby member.");
            return;
        }
        //Users with young index send Heartbeat.
        if(p2pInfo.Instance.GetUserIndex(p2pInfo.Instance.LocalUserId) <= 2){
            p2pInfo.Instance.RefreshPing(UserId.GetUserId(data.RemoteUserId)).Forget();
            HeartBeatToLobby(p2pInfo.Instance.GetUserIndex(UserId.GetUserId(data.RemoteUserId)));
        }

        p2pInfo.Instance.ConnectionNotifier.EarlyDisconnected(UserId.GetUserId(data.RemoteUserId), Reason.Interrupted);
        Logger.Log("PeerConnectionInterrupted", $"Connection lost now. UserId: {UserId.GetUserId(data.RemoteUserId).ToMaskedString()} / Reason: {Reason.Interrupted}");
    }
    void RemoveNotifyPeerConnectionInterrupted(){
        P2PHandle.RemoveNotifyPeerConnectionInterrupted(InterruptedNotify);
        InterruptedNotify = 0;
    }
    void AddNotifyPeerConnectionEstablished(){
        if (EstablishedNotify == 0){
            AddNotifyPeerConnectionEstablishedOptions options = new AddNotifyPeerConnectionEstablishedOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                SocketId = SocketId
            };

            EstablishedNotify = P2PHandle.AddNotifyPeerConnectionEstablished(ref options, null, OnPeerConnectionEstablishedCallback);
            
            if (EstablishedNotify == 0){
                Logger.LogError("AddNotifyPeerConnectionEstablished", "could not subscribe, bad notification id returned.");
            }
        }
    }
    // Call from SubscribeToConnectionRequest.
    void OnPeerConnectionEstablishedCallback(ref OnPeerConnectionEstablishedInfo data){
        if (!(bool)data.SocketId?.SocketName.Equals(ScoketName)){
            Logger.LogError("OnPeerConnectionEstablishedCallback", "unknown socket id. This peer should be no lobby member.");
            return;
        }
        if(data.ConnectionType == ConnectionEstablishedType.Reconnection){
            p2pInfo.Instance.ConnectionNotifier.Restored(UserId.GetUserId(data.RemoteUserId));
            Logger.Log("OnPeerConnectionEstablishedCallback", "Connection is restored.");
            return;
        }
        
        if(data.ConnectionType == ConnectionEstablishedType.NewConnection &&
            p2pInfo.Instance.userIds.RemoteUserIds.Contains(UserId.GetUserId(data.RemoteUserId))){
            p2pInfo.Instance.ConnectionNotifier.OnEstablished(UserId.GetUserId(data.RemoteUserId));
            return;
        }
    }
    internal void RemoveNotifyPeerConnectionnEstablished(){
        P2PHandle.RemoveNotifyPeerConnectionEstablished(EstablishedNotify);
        EstablishedNotify = 0;
    }
    void AddNotifyPeerConnectionClosed(){
        if (ClosedNotify == 0){
            AddNotifyPeerConnectionClosedOptions options = new AddNotifyPeerConnectionClosedOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                SocketId = SocketId
            };

            ClosedNotify = P2PHandle.AddNotifyPeerConnectionClosed(ref options, null, OnPeerConnectionClosedCallback);
            
            if (ClosedNotify == 0){
                Logger.LogError("AddNotifyPeerConnectionClosed", "could not subscribe, bad notification id returned.");
            }
        }
    }
    // Call from SubscribeToConnectionRequest.
    void OnPeerConnectionClosedCallback(ref OnRemoteConnectionClosedInfo data){
        if (!(bool)data.SocketId?.SocketName.Equals(ScoketName)){
            Logger.LogError("OnPeerConnectionClosedCallback", "unknown socket id. This peer should be no lobby member.");
            return;
        }
        if(data.Reason is not ConnectionClosedReason.ClosedByLocalUser or ConnectionClosedReason.ClosedByPeer){
            //Users with young index send Heartbeat.
            if(p2pInfo.Instance.GetUserIndex(p2pInfo.Instance.LocalUserId) <= 2){
                //+100 is second's symbol.
                int disconnectedUserIndex = 100 + p2pInfo.Instance.GetUserIndex(UserId.GetUserId(data.RemoteUserId));
                HeartBeatToLobby(disconnectedUserIndex);
            }
            Logger.Log("OnPeerConnectionClosedCallback", $"Connection lost now. UserId {UserId.GetUserId(data.RemoteUserId).ToMaskedString()} / Reason {data.Reason}");
        }
    }
    internal void RemoveNotifyPeerConnectionnClosed(){
        P2PHandle.RemoveNotifyPeerConnectionClosed(ClosedNotify);
        ClosedNotify = 0;
    }
#endregion
#region Disconnect
        protected override Result CloseConnection (){
            RemoveNotifyPeerConnectionRequest();
            if(p2pConfig.Instance.UseDisconnectedEarlyNotify){
                RemoveNotifyPeerConnectionInterrupted();
                RemoveNotifyPeerConnectionnEstablished();
            }else{
                RemoveNotifyPeerConnectionnClosed();
            }

            var closeOptions = new CloseConnectionsOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                SocketId = SocketId
            };
            Result result = (Result)P2PHandle.CloseConnections(ref closeOptions);
            if(result != Result.Success){
                Logger.LogError("CloseConnections", "Failed to close connection.", result);
            }
            return result;
        }
#endregion
    }
}