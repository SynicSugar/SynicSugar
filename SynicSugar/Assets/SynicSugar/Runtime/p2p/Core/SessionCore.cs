using Epic.OnlineServices.P2P;
using UnityEngine;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SynicSugar.P2P;
using SynicSugar.MatchMake;
//We can't call the main-Assembly from own-assemblies.
//So, use such processes through this script.
namespace SynicSugar.Base {
    public abstract class SessionCore : INetworkCore {
        protected SessionCore(){
            Logger.Log("SessionCore", $"SessionCore is initialized");
            IsConnected = false;
            GeneratePacketReceiver();
        }
        internal void Dispose(){
            Logger.Log("SessionCore", $"SessionCore is disposed");
            Destroy(receiverObject);
        }
        GameObject receiverObject;
        protected string _socketName;
        internal string ScoketName { 
            get { return _socketName; }
            set {
                _socketName = value;
                SocketId = new SocketId(){ SocketName = _socketName };
        }}
        /// <summary>
        /// For actual connection
        /// </summary>
        /// <value></value>
        public SocketId SocketId { get; protected set; }

        /// <summary>
        /// For internal task process.
        /// </summary>
        internal protected CancellationTokenSource rttTokenSource;

        //Packet Receiver
        internal protected ReceiverType validReceiverType { get; protected set; }
        
        PacketReceiver FixedUpdateReceiver, UpdateReceiver, LateUpdateReceiver, SynicReceiver;
        /// <summary>
        /// Is the connection currently active?　<br />
        /// Can change this flag own, but basically Base class manages the flags.　
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Is this local user in Matchmaking or Session?
        /// </summary>
        /// <returns></returns>
        protected bool IsInNetworkProcess(){
           return SynicSugarManger.Instance.State.IsInSession || SynicSugarManger.Instance.State.IsMatchmaking;
        }
        /// <summary>
        /// Is this local user in SetupProcess（Matchmaking and not looking for oponents） or Session?
        /// </summary>
        /// <returns></returns>
        protected bool IsInSetupProcessOrSession(){
           return SynicSugarManger.Instance.State.IsInSession || (SynicSugarManger.Instance.State.IsMatchmaking && !MatchMakeManager.Instance.isLooking);
        }
        
        /// <summary>
        /// Generate packet receiver object and add each receiver script.
        /// </summary>
        void GeneratePacketReceiver(){
            if(receiverObject != null){
                Logger.LogError("GeneratePacketReceiver", "This manager has generated receivers already.");
                return;
            }
            receiverObject = new GameObject("SynicSugarReceiver");
            receiverObject.AddComponent<PacketReceiverOnFixedUpdate>();
            receiverObject.AddComponent<PacketReceiverOnUpdate>();
            receiverObject.AddComponent<PacketReceiverOnLateUpdate>();
            receiverObject.AddComponent<PacketReceiverForSynic>();

            FixedUpdateReceiver = receiverObject.GetComponent<PacketReceiverOnFixedUpdate>();
            UpdateReceiver = receiverObject.GetComponent<PacketReceiverOnUpdate>();
            LateUpdateReceiver = receiverObject.GetComponent<PacketReceiverOnLateUpdate>();
            SynicReceiver = receiverObject.GetComponent<PacketReceiverForSynic>();

            FixedUpdateReceiver.SetGetPacket(this);
            UpdateReceiver.SetGetPacket(this);
            LateUpdateReceiver.SetGetPacket(this);
            SynicReceiver.SetGetPacket(this);

            UnityEngine.Object.DontDestroyOnLoad(receiverObject);

            validReceiverType = ReceiverType.None;
        }
        protected void Destroy(GameObject gameObject) {
            UnityEngine.Object.Destroy(gameObject);
        }
    #region INetworkCore
        /// <summary>
        /// For ConnectManager. Stop packet receeiveing to buffer. While stopping, packets are dropped.
        /// </summary>
        /// <param name="isForced">If True, stop and clear current packet queue. <br />
        /// If false, process current queue, then stop it.</param>
        /// <param name="token">For this task</param>
        async UniTask<Result> INetworkCore.PauseConnections(bool isForced, CancellationToken token){
            if(!IsConnected || p2pInfo.Instance.SessionType != SessionType.OnlineSession){
                Logger.LogWarning("PauseConnections", !IsConnected ?  "The connection is already paused or inactive. No action taken." : "Cannot pause because the user is not in an online session.");
                return Result.InvalidAPICall;
            }
            //To stop sending process before result.
            IsConnected = false;
            CancelRTTToken();
            Result result = await PauseConnections(isForced, token);

            //reflect actual result
            IsConnected = result != Result.Success;
            if(result != Result.Success){
                rttTokenSource = new CancellationTokenSource();
            }
            return result;
        }
        protected abstract UniTask<Result> PauseConnections(bool isForced, CancellationToken token);

        /// <summary>
        /// Prepare to receive in advance. If user sent packets, it can open to get packets for a socket id without this.
        /// </summary>
        Result INetworkCore.RestartConnections(){
            if(IsConnected || p2pInfo.Instance.SessionType != SessionType.OnlineSession){
                Logger.LogWarning("RestartConnections", IsConnected ?  "The connection is already active. No action taken." : "Cannot restart because the user is not in an online session.");
                return Result.InvalidAPICall;
            }
            Result result = InitiateConnection();

            if(result == Result.Success){
                IsConnected = true;
                rttTokenSource = new CancellationTokenSource();
            }
            return result;
        }

        /// <summary>
        /// Use this from hub not to call some methods in Main-Assembly from SynicSugar.dll.<br />
        /// Stop connections, exit current lobby.<br />
        /// The Last user closes lobby.
        /// </summary>
        /// <param name="destroyManager">If true, destroy NetworkManager after cancel matchmake.</param>
        /// <param name="cleanupMemberCountChanged">Need to call MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(id, false) after exit lobby?</param>
        /// <param name="token">token for this task</param>
        async UniTask<Result> INetworkCore.ExitSession(bool destroyManager, bool cleanupMemberCountChanged , CancellationToken token){
            if(!SynicSugarManger.Instance.State.IsInSession || p2pInfo.Instance.SessionType != SessionType.OnlineSession){
                // For the user who kicked from the lobby.
                if(p2pInfo.Instance.SessionType == SessionType.InvalidSession){
                    CleanupOnLobbyClosure(destroyManager, cleanupMemberCountChanged);
                    return Result.Success;
                }
                Logger.LogWarning("ExitSession", !SynicSugarManger.Instance.State.IsInSession ? "This local user is NOT in Online Session. No action taken." : "Cannot exit because the user is not in an online session.");
                return Result.InvalidAPICall;
            }

            //Stop connection
            bool tmpState = IsConnected;
            IsConnected = false;
            ResetConnections();
            //Then, destroy Lobby
            Result result;
            //The last user
            if (p2pInfo.Instance.IsHost() && p2pInfo.Instance.CurrentConnectedUserIds.Count == 1){
                result = await MatchMakeManager.Instance.CloseCurrentLobby(cleanupMemberCountChanged, token);
            }else{
                result = await MatchMakeManager.Instance.ExitCurrentLobby(cleanupMemberCountChanged, token);
            }
            
            if(result != Result.Success){
                if(tmpState){
                    IsConnected = true;
                    rttTokenSource = new CancellationTokenSource();
                }
                return result;
            }

            SynicSugarManger.Instance.State.IsInSession = false;
            
            if(destroyManager){
                Destroy(MatchMakeManager.Instance.gameObject);
            }else{
                p2pInfo.Instance.Reset();
            }

            return result;
        }

        /// <summary>
        /// Use this from hub not to call some methods in Main-Assembly from SynicSugar.dll.<br />
        /// Stop connections, exit current lobby.<br />
        /// Host closes lobby. Guest leaves lobby. <br />
        /// If host call this after the lobby has other users, Guests in this lobby are kicked out from the lobby.
        /// </summary>
        /// <param name="destroyManager">If true, destroy NetworkManager after cancel matchmake.</param>
        /// <param name="cleanupMemberCountChanged">Need to call MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(id, false) after exit lobby?</param>
        /// <param name="token">token for this task</param>
        async UniTask<Result> INetworkCore.CloseSession(bool destroyManager, bool cleanupMemberCountChanged, CancellationToken token){
            if(!SynicSugarManger.Instance.State.IsInSession || p2pInfo.Instance.SessionType != SessionType.OnlineSession){
                // For the user who kicked from the lobby.
                if(p2pInfo.Instance.SessionType == SessionType.InvalidSession){
                    CleanupOnLobbyClosure(destroyManager, cleanupMemberCountChanged);
                    return Result.Success;
                }
                Logger.LogWarning("CloseSession", !SynicSugarManger.Instance.State.IsInSession ? "This local user is NOT in Session. No action taken." : "Cannot close because the user is not in an online session.");
                return Result.InvalidAPICall;
            }

            //Stop connection
            bool tmpState = IsConnected;
            IsConnected = false;
            ResetConnections();
            //Then, destroy Lobby
            Result result;
            if(p2pInfo.Instance.IsHost()){
                result = await MatchMakeManager.Instance.CloseCurrentLobby(cleanupMemberCountChanged, token);
            }else{
                result = await MatchMakeManager.Instance.ExitCurrentLobby(cleanupMemberCountChanged, token);
            }
            if(result != Result.Success){
                if(tmpState){
                    IsConnected = true;
                    rttTokenSource = new CancellationTokenSource();
                }
                return result;
            }

            SynicSugarManger.Instance.State.IsInSession = false;

            if(destroyManager){
                Destroy(MatchMakeManager.Instance.gameObject);
            }else{
                p2pInfo.Instance.Reset();
            }
            
            return result;
        }
        /// <summary>
        /// Cleans up session state for a user who has been removed from the lobby due to lobby closure or disconnection.
        /// </summary>
        /// <param name="destroyManager"></param>
        void CleanupOnLobbyClosure(bool destroyManager, bool cleanupMemberCountChanged){
            MatchMakeManager.Instance.ResetStateOnLobbyClosure(cleanupMemberCountChanged);

            IsConnected = false;
            
            if(destroyManager){
                Destroy(MatchMakeManager.Instance.gameObject);
            }else{
                p2pInfo.Instance.Reset();
            }
            Logger.Log("CleanupOnLobbyClosure", "Clean up the session state.");
        }

        /// <summary>
        /// Just for solo mode like as tutorial. <br />
        /// Destory Lobby Instance. We can use just Destory(MatchMakeManager.Instance)　and delete LobbyID method without calling this.<br />
        /// </summary>
        /// <param name="destroyManager">If true, destroy NetworkManager after cancel matchmake.</param>
        /// <param name="cleanupMemberCountChanged">Need to call MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(id, false) after exit lobby?</param>
        /// <param name="token">token for this task</param>
        /// <returns>Always return true. the LastResultCode becomes Success after return true.</returns>
        async UniTask<Result> INetworkCore.DestoryOfflineLobby(bool destroyManager, bool cleanupMemberCountChanged, CancellationToken token){
            if(!SynicSugarManger.Instance.State.IsInSession || p2pInfo.Instance.SessionType != SessionType.OfflineSession){
                Logger.LogWarning("DestoryOfflineLobby", !SynicSugarManger.Instance.State.IsInSession ? "This local user is NOT in Session. No action taken." : "This local user doesn't have an offline lobby");
                return Result.InvalidAPICall;
            }
            CancelRTTToken();

            await MatchMakeManager.Instance.DestoryOfflineLobby(cleanupMemberCountChanged, token);
            
            SynicSugarManger.Instance.State.IsInSession = false;
            if(destroyManager){
                Destroy(MatchMakeManager.Instance.gameObject);
            }else{
                p2pInfo.Instance.Reset();
            }

            return Result.Success;
        }

        /// <summary>
        /// Start standart packet receiver on each timing. Only one can be enabled, including Synic.<br />
        /// Use this from hub not to call some methods in Main-Assembly from SynicSugar.dll. 
        /// </summary>
        Result INetworkCore.StartPacketReceiver(IPacketConvert hubInstance, PacketReceiveTiming timing, uint maxBatchSize){
            if(!SynicSugarManger.Instance.State.IsInSession){
                Logger.LogWarning("StartPacketReceiver", "This local user is NOT in Session. This function can only be called in a session.");
                return Result.InvalidAPICall;
            }
            if(validReceiverType != ReceiverType.None){
                ((INetworkCore)this).StopPacketReceiver();
            }
            if(p2pConfig.Instance.AutoRefreshPing){
                if(rttTokenSource == null || !rttTokenSource.Token.CanBeCanceled){
                    CancelRTTToken();
                    rttTokenSource = new CancellationTokenSource();
                }
                AutoRefreshPings(rttTokenSource.Token).Forget();
            }
            
            switch(timing){
                case PacketReceiveTiming.FixedUpdate:
                    FixedUpdateReceiver.StartPacketReceiver(hubInstance, maxBatchSize);
                    validReceiverType = ReceiverType.FixedUpdate;
                break;
                case PacketReceiveTiming.Update:
                    UpdateReceiver.StartPacketReceiver(hubInstance, maxBatchSize);
                    validReceiverType = ReceiverType.Update;
                break;
                case PacketReceiveTiming.LateUpdate:
                    LateUpdateReceiver.StartPacketReceiver(hubInstance, maxBatchSize);
                    validReceiverType = ReceiverType.LateUpdate;
                break;
            }

            return Result.Success;
        }

        /// <summary>
        /// Start Synic packet receiver on each timing. Only one can be enabled, including Standard receiver.<br />
        /// Use this from hub not to call some methods in Main-Assembly from SynicSugar.dll. <br />
        /// </summary>
        Result INetworkCore.StartSynicReceiver(IPacketConvert hubInstance, uint maxBatchSize){
            if(!SynicSugarManger.Instance.State.IsInSession){
                Logger.LogWarning("StopPacketReceiver", "This local user is NOT in Session. This function can only be called in a session.");
                return Result.InvalidAPICall;
            }
            if(validReceiverType != ReceiverType.None){
                ((INetworkCore)this).StopPacketReceiver();
            }

            SynicReceiver.StartPacketReceiver(hubInstance, maxBatchSize);
            validReceiverType = ReceiverType.Synic;

            return Result.Success;
        }

        Result INetworkCore.StopPacketReceiver(){
            if(!SynicSugarManger.Instance.State.IsInSession){
                Logger.LogWarning("StopPacketReceiver", "This local user is NOT in Session. This function can only be called in a session.");
                return Result.InvalidAPICall;
            }
            if(validReceiverType is ReceiverType.None){
                Logger.LogWarning("StopPacketReceiver", "PacketReciver is not working now. No action taken.");
                return Result.InvalidAPICall;
            }
            
            switch(validReceiverType){
                case ReceiverType.FixedUpdate:
                    FixedUpdateReceiver.StopPacketReceiver();
                break;
                case ReceiverType.Update:
                    UpdateReceiver.StopPacketReceiver();
                break;
                case ReceiverType.LateUpdate:
                    LateUpdateReceiver.StopPacketReceiver();
                break;
                case ReceiverType.Synic:
                    SynicReceiver.StopPacketReceiver();
                break;
            }
     
            validReceiverType = ReceiverType.None;
            return Result.Success;
        }
    #endregion

    #region For Receiver
        /// <summary>
        /// To get Packets.
        /// Use this from hub not to call some methods in Main-Assembly from SynicSugar.dll.
        /// </summary>
        public abstract bool GetPacketFromBuffer(ref byte ch, ref UserId id, ref ArraySegment<byte> payload);
        /// <summary>
        /// To get only SynicPacket.
        /// Use this from ConenctHub not to call some methods in Main-Assembly from SynicSugar.dll.
        /// </summary>
        public abstract bool GetSynicPacketFromBuffer(ref byte ch, ref UserId id, ref ArraySegment<byte> payload);
    #endregion
#region Connect
        /// <summary>
        /// Prep for p2p connections.<br />
        /// Connect or Request a connection with all other peers. <br />
        /// </summary>
        //* Maybe: Some processes in InitConnectConfig need time to complete and the Member list will be created after that end. Therefore, we will add Notify first to spent time.
        public Result OpenConnection(){
            if(!IsInSetupProcessOrSession()){
                Logger.LogWarning("OpenConnection", "This local user is NOT in SetupProcess or Session. No action taken.");
                return Result.InvalidAPICall;
            }
            Result result = InitiateConnection();

            if (result == Result.Success){
                rttTokenSource = new CancellationTokenSource();
                IsConnected = true;
            }
            return result;
        }
        protected abstract Result InitiateConnection();

        /// <summary>
        /// Connect or Request a connection with other peer.<br />
        /// </summary>
        /// <param name="targetId">Reconnected user's id</param>
        /// <returns></returns>
        public abstract Result AcceptConnection(UserId targetId);
#endregion
#region Disconnect
        /// <summary>
        /// Handle termination of notification at end of session, p2p disconnection, etc.
        /// This is the entry point for initializing the IsConnected flag. All initialization and disconnection 
        /// operations should be routed through this method rather than calling CloseConnection directly.
        /// This method is invoked through ResetConnections when a connection termination occurs.
        /// </summary>
        protected Result RemoveNotifyAndCloseConnection (){
            Result result = CloseConnection();

            if(result == Result.Success){
                IsConnected = false;
            }
            return result;
        }
        /// <summary>
        /// Close all p2p conenction
        /// </summary>
        /// <returns></returns>
        protected abstract Result CloseConnection();
        /// <summary>
        /// Close the connection with target user.
        /// </summary>
        /// <param name="targetId"></param>
        /// <returns></returns>
        public abstract Result CloseConnection(UserId targetId);
#endregion

        protected internal abstract Result ResetConnections();
        /// <summary>
        /// Update SyncedInfo, then Invoke SyncedSynic event.
        /// </summary>
        /// <param name="id">target id</param>
        /// <param name="phase">Synic phase</param>
        /// <summary>
        void INetworkCore.UpdateSynicStatus(string id, byte phase){
            p2pInfo.Instance.SyncSnyicNotifier.UpdateSynicStatus(id, phase);
        }
        public async UniTask AutoRefreshPings(CancellationToken token){
            await UniTask.Delay(p2pConfig.Instance.PingAutoRefreshRateSec * 1000, cancellationToken: token);
            if(!IsConnected){ return; }

            await p2pInfo.Instance.pings.RefreshPings(token);
            if(!IsConnected){ return; }
            
            AutoRefreshPings(token).Forget();
        }
        /// <summary>
        /// To check disconencted user's conenction state after p2p.
        /// </summary>
        /// <param name="disconenctedUserIndex"> UserIndex. For second Heart beat, +100</param>
        protected void HeartBeatToLobby(int disconenctedUserIndex){
            MatchMakeManager.Instance.HeartBeatToLobby(disconenctedUserIndex);
        }
        internal void CancelRTTToken(){
            if(rttTokenSource == null){
                return;
            }
            rttTokenSource.Cancel();
            rttTokenSource.Dispose();
            rttTokenSource = null;
        }
        /// <summary>
        /// Change AcceptHostsSynic to false. Call from ConnectHub
        /// </summary>
        void INetworkCore.StopOverwritingLocalUserData(){
            p2pInfo.Instance.userIds.ReceivedLocalUserSynic();
        }
        /// <summary>
        /// Return pong to calculate RTT. Call from ConnectHub
        /// </summary>
        void INetworkCore.GetPong(string id, ArraySegment<byte> utc){
            p2pInfo.Instance.pings.GetPong(id, utc);
        }

        /// <summary>
        /// Get the packet queue information.
        /// </summary>
        /// <param name="Info">The object to return the result.</param>
        /// <returns></returns>
        public abstract Result GetPacketQueueInfo(out PacketQueueInformation Info);
    }
}