using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using System.Collections.Generic;
using SynicSugar.Base;

namespace SynicSugar.P2P {
    [DefaultExecutionOrder(-50)]
    public class p2pInfo : MonoBehaviour {
#region Singleton
        public static p2pInfo Instance { get; private set; }
        void Awake() {
            Logger.Log("p2pInfo", "Start initialization of p2pInfo.");
            if( Instance != null ) {
                Destroy( this );
                Logger.Log("p2pInfo", "Discard this instance since p2pInfo already exists.");
                return;
            }
            Instance = this;
            userIds = new ();
            pings = new();
            lastRpcInfo = new();
            lastTargetRPCInfo = new();
            ConnectionNotifier = new ConnectionNotifier();
            SyncSnyicNotifier = new SyncSnyicNotifier();
        }
        void OnDestroy() {
            if( Instance == this ) {
                UserId.CacheClear();
                ConnectionNotifier.Reset();
                SyncSnyicNotifier.Clear();

                Instance = null;
            }
        }
        /// <summary>
        /// For the case not to destroy and play next match.
        /// </summary>
        internal void Reset(){
            userIds = new ();
            pings = new();
            lastRpcInfo = new();
            lastTargetRPCInfo = new();
            CurrentSessionStartUTC = DateTime.MinValue;
            UserId.CacheClear();
            ConnectionNotifier.Reset();
            SyncSnyicNotifier.Clear();
        }
        /// <summary>
        /// Set reference of some manager classes.
        /// </summary>
        /// <param name="sessionInstance"></param>
        /// <param name="natrelayInstance"></param>
        internal void SetDependency(SessionCore sessionInstance, NatRelayManager natrelayInstance){
            sessionCore = sessionInstance;
            natRelayManager = natrelayInstance;
        }
#endregion

        /// <summary>
        /// Indicates whether the matchmaking process has finished and the system is in a state where P2P communication is possible.<br />
        /// After get Success by matchmaking APIs include for Offline, this value becomes true　until finish Session by ExitSession or CloseSession.
        /// </summary>
        public bool IsInSession => SynicSugarManger.Instance.State.IsInSession;

        /// <summary>
        /// Indicates whether the connection is currently active.<br />
        /// After matchmaking completes and transitions to the session state, the connection automatically starts, and this value returns true.<br />
        /// If the value is false and you want to restart the connection, call ConnectHub.Instance.RestartConnections(). (*This method is only valid when IsInSession is true.)
        /// </summary>
        public bool IsConnected => sessionCore.IsConnected;
        SessionCore sessionCore;
        NatRelayManager natRelayManager;
        internal UserIds userIds;
        internal p2pPing pings;
        internal RPCInformation lastRpcInfo;
        internal TargetRPCInformation lastTargetRPCInfo;

        /// <summary>
        /// The type of current session.
        /// </summary>
        public SessionType SessionType { get; internal set; }
        /// <summary>
        /// Date time when this LOCAL user starts current session.
        /// </summary>
        /// <value></value>
        public DateTime CurrentSessionStartUTC { get; internal set; }
        /// <summary>
        /// Get sec since start current session.
        /// </summary>
        /// <returns></returns>
        public uint GetSessionTimestamp() {
            return (uint)DateTime.UtcNow.Subtract(CurrentSessionStartUTC).TotalSeconds;
        }
        /// <summary>
        /// Get micro sec since start current session for the case need precision.
        /// </summary>
        /// <returns></returns>
        public double GetSessionTimestampInMs() {
            return DateTime.UtcNow.Subtract(CurrentSessionStartUTC).TotalMilliseconds;
        }
    #region UserId basic info
        /// <summary>
        /// This lobby's Host UserId.
        /// </summary>
        public UserId HostUserId => userIds.HostUserId;
        /// <summary>
        /// This local UserId.
        /// </summary>
        public UserId LocalUserId => userIds.LocalUserId;
        /// <summary>
        /// Remote UserIds currently connected.
        /// </summary>
        public List<UserId> CurrentRemoteUserIds => userIds.RemoteUserIds;
        /// <summary>
        /// All Connected UserIds　（Excluding disconnected and left users)
        /// </summary>
        public List<UserId> CurrentConnectedUserIds => userIds.CurrentConnectedUserIds;
        /// <summary>
        /// AllUserIds - Left Users.
        /// </summary>
        public List<UserId> CurrentAllUserIds => userIds.CurrentAllUserIds;
        /// <summary>
        /// All UserIds throughout this session include Local and Left Users.<br />
        /// This value is the same value and same order with all locals through the whole game.
        /// </summary>
        public List<UserId> AllUserIds => userIds.AllUserIds;


        /// <summary>
        /// Disconnected user ids. (May come back)
        /// </summary>
        public List<UserId> DisconnectedUserIds => userIds.DisconnectedUserIds;

        /// <summary>
        /// Get LocalUser' UserIndex in AllUserIds<br />
        /// This is the same　value in all locals.
        /// </summary>
        /// <returns>(0, max lobby members count -1)</returns>
        public int GetUserIndex(){
            return userIds.AllUserIds.IndexOf(userIds.LocalUserId);
        }
        /// <summary>
        /// Get UserIndex in AllUserIds<br />
        /// This is the same　value in all locals.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>(0, max lobby members count -1)</returns>
        public int GetUserIndex(UserId id){
            return userIds.AllUserIds.IndexOf(id);
        }
    #endregion


        /// <summary>
        /// The notify events for connection and disconection on current session.
        /// </summary>
        public ConnectionNotifier ConnectionNotifier;
        /// <summary>
        /// Reason of the user disconnected from p2p.
        /// </summary>
        public Reason LastDisconnectedUsersReason { get { return ConnectionNotifier.ClosedReason;} }
        /// <summary>
        /// UserId of the user disconnected from p2p.
        /// </summary>
        public UserId LastDisconnectedUsersId { get { return ConnectionNotifier.CloseUserId;} } 
        /// <summary>
        /// UserId of the reconnecter disconnected from p2p.
        /// </summary>
        public UserId LastConnectedUsersId { get { return ConnectionNotifier.ConnectUserId;} } 
        /// <summary>
        /// The notify events for SyncSynic for recconecter and large packet.
        /// </summary>
        public SyncSnyicNotifier SyncSnyicNotifier;
        /// <summary>
        /// Return True only once when this local user is received SyncSync from every other peers of the current session. <br />
        /// EVERY here means p2pInfo.Instance.CurrentAllUserIds.Count if the host is also sending data for others who are disconnected, 
        /// or p2pInfo.Instance. CurrentConnectedUserIds.Count.<br />
        /// After return true, all variable about this flag is initialized and become returning False again.
        /// </summary>
        /// <returns></returns>
        public bool HasReceivedAllSyncSynic => SyncSnyicNotifier.ReceivedAllSyncSynic();
        /// <summary>
        /// Phase of the last SyncSynic to receive to this local.
        /// </summary>
        public byte SyncedSynicPhase { get { return SyncSnyicNotifier.LastSyncedPhase; } } 
        /// <summary>
        /// UserId of the last SyncSynic to receive to this local.
        /// </summary>
        public UserId LastSyncedUserId { get { return SyncSnyicNotifier.LastSyncedUserId;} } 
        /// <summary>
        /// Always return false. Just on reconnect, returns true until getting SyncSynic about SELF data from Host.
        /// </summary>
        public bool IsReconnecter => userIds.isJustReconnected;
        
        /// <summary>
        /// Update local user's NATType to the latest state.
        /// </summary>
        public async UniTask QueryNATType() => await natRelayManager.QueryNATType();
        /// <summary>
        /// Get last-queried NAT-type, if it has been successfully queried.
        /// </summary>
        /// <returns>Open means being able connect with direct p2p. Otherwise, the connection may be via Epic relay.</returns>
        public NATType GetNATType() => natRelayManager.GetNATType();
    #region IsHost
        /// <summary>
        /// Is this local user Game Host?
        /// </summary>
        /// <returns></returns>
        public bool IsHost (){
            return userIds.LocalUserId == userIds.HostUserId;
        }
        /// <summary>
        /// Is this user Game Host?
        /// </summary>
        /// <returns></returns>
        public bool IsHost (UserId targetId){
            return targetId == userIds.HostUserId;
        }
        /// <summary>
        /// Is this user Game Host?
        /// </summary>
        /// <returns></returns>
        public bool IsHost (string targetId){
            return UserId.GetUserId(targetId) == userIds.HostUserId;
        }
    #endregion
    #region IsLocalUser
        /// <summary>
        /// Is this user local user?
        /// </summary>
        /// <returns></returns>
        public bool IsLoaclUser (UserId targetId){
            return targetId == userIds.LocalUserId;
        }
        /// <summary>
        /// Is this user local user?
        /// </summary>
        /// <returns></returns>
        public bool IsLoaclUser (string targetId){
            return UserId.GetUserId(targetId) == userIds.LocalUserId;
        }
    #endregion
    #region Ping
        /// <summary>
        /// Get last Ping of specific user from local data.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Returns the last ping obtained. <br />
        /// If the target user is disconnected, -1 is returned. If the key cannot be found because the local user is offline, etc., -2 is returned.</returns>
        public int GetPing(UserId id){
            if (pings.pingInfo.TryGetValue(id.ToString(), out var pingInfo)){
                return pingInfo.Ping;
            }

            return -1;
        }

        /// <summary>
        /// Manually update Ping data with Target to latest.
        /// </summary>
        /// <returns></returns>
        public async UniTask<Result> RefreshPing(UserId target){
            if(!IsInSession || SessionType == SessionType.OfflineSession){
                Logger.LogWarning("RefreshPing", "This local user is not in online session.");
                return Result.InvalidAPICall;
            }
            return await pings.RefreshPing(target, sessionCore.rttTokenSource.Token);
        }
        /// <summary>
        /// Manually update Pings data to latest.
        /// </summary>
        /// <returns></returns>
        public async UniTask<Result> RefreshPings(){
            if(!IsInSession || SessionType == SessionType.OfflineSession){
                Logger.LogWarning("RefreshPing", "This local user is not in online session.");
                return Result.InvalidAPICall;
            }
            return await pings.RefreshPings(sessionCore.rttTokenSource.Token);
        }
    #endregion
        /// <summary>
        /// the last byte array sent with RPC that record data.
        /// </summary>
        public byte[] LastRPCPayload => lastRpcInfo.payload;
        /// <summary>
        /// the last byte array sent with TargetRPC that record data.
        /// </summary>
        public byte[] LastTargetRPCPayload => lastTargetRPCInfo.payload;
        /// <summary>
        /// the last ch sent with RPC that record data.
        /// </summary>
        public byte LastRPCch => lastRpcInfo.ch;
        /// <summary>
        /// the last ch sent with TargetRPC that record data.
        /// </summary>
        public byte LastTargetRPCch => lastTargetRPCInfo.ch;
        /// <summary>
        /// the last UserId sent with TargetRPC that record data.
        /// </summary>
        public UserId LastTargetRPCUserId => lastTargetRPCInfo.target;
        public bool LastRPCIsLargePacket => lastRpcInfo.isLargePacket;
        public bool LastTargetRPCIsLargePacket => lastTargetRPCInfo.isLargePacket;
        /// <summary>
        /// Checks if the connection has been enabled by the library or user.
        /// This does not necessarily mean that an actual connection has been established.
        /// The IsConnected flag becomes true after the user or library initiates the connection.
        /// </summary>
        /// <returns>True if the connection is open, false otherwise.</returns>
        [Obsolete("This is old. p2pInfo.Instance.IsConnected is new one.")]
        public bool ConnectionIsValid(){
            return sessionCore.IsConnected;
        }
        /// <summary>
        /// Gets the currently valid (active) packet receiver type.
        /// </summary>
        /// <returns>The active ReceiverType enum value</returns>
        public ReceiverType GetActiveReceiverType(){
            return sessionCore.validReceiverType;
        }
    }
}