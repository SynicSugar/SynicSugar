using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using MemoryPack;
namespace SynicSugar.P2P {
    public class p2pPing {
        internal Dictionary<string, PingInformation> pingInfo = new Dictionary<string, PingInformation>();
        int refreshMembers;
        bool isRefreshing;
        enum CHANNELLIST{
            ObtainPing = 253, ReturnPong = 254, Synic = 255
        }
        internal void Init(){
            foreach(var id in p2pInfo.Instance.userIds.AllUserIds){
                if(!pingInfo.ContainsKey(id.ToString())){
                    pingInfo.Add(id.ToString(), new PingInformation());
                }
            }
        }
        /// <summary>
        /// Reflesh Ping with Target<br />
        /// Use also this for heartbeat.
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        internal async UniTask<Result> RefreshPing(UserId targetId, CancellationToken token){
            if(!p2pConfig.Instance.sessionCore.IsConnected){
                Logger.LogWarning("RefreshPings", "The connection is invalid.");
                return Result.InvalidAPICall;
            }
            if(isRefreshing){
                Logger.LogWarning("RefreshPing", "Currently being refreshed.");
                return Result.RequestInProgress;
            }
            isRefreshing = true;
            refreshMembers = 0;
            for(int i = 0; i < p2pConfig.Instance.SamplesPerPing; i++){
                DateTime utc = DateTime.UtcNow;
                byte[] utc_b = MemoryPackSerializer.Serialize(utc);
                EOSp2p.SendPacket((byte)CHANNELLIST.ObtainPing, utc_b, targetId);
            }

            await UniTask.WhenAny(UniTask.WaitUntil(() => refreshMembers == 1, cancellationToken: token), UniTask.Delay(5000, cancellationToken: token));

            isRefreshing = false;
            return refreshMembers == 1 ? Result.Success : Result.TimedOut;
        }
        /// <summary>
        /// Send 0 + Utc. Measure ping at the time of return 1 + UTC.
        /// </summary> 
        // MEMO: Replace SendPacketToAll when it can be made more efficient.
        internal async UniTask<Result> RefreshPings(CancellationToken token){
            if(!p2pConfig.Instance.sessionCore.IsConnected){
                Logger.LogWarning("RefreshPings", "The connection is invalid.");
                return Result.InvalidAPICall;
            }
            if(isRefreshing){
                Logger.LogWarning("RefreshPing", "Currently being refreshed.");
                return Result.RequestInProgress;
            }
            isRefreshing = true;
            refreshMembers = 0;
            for(int i = 0; i < p2pConfig.Instance.SamplesPerPing; i++){
                foreach(var id in p2pInfo.Instance.userIds.RemoteUserIds){
                    DateTime utc = DateTime.UtcNow;
                    byte[] utc_b = MemoryPackSerializer.Serialize(utc);

                    EOSp2p.SendPacket((byte)CHANNELLIST.ObtainPing, utc_b, id);
                }
                await UniTask.Yield();
            }
            await UniTask.WhenAny(UniTask.WaitUntil(() => refreshMembers == p2pInfo.Instance.userIds.RemoteUserIds.Count, cancellationToken: token),
            UniTask.Delay(10000, cancellationToken: token));

            isRefreshing = false;
            return refreshMembers == p2pInfo.Instance.userIds.RemoteUserIds.Count ? Result.Success : Result.TimedOut;
        }
        //Get Pong and calc
        internal void GetPong(string id, ArraySegment<byte> utc){
            DateTime current = DateTime.UtcNow;

            TimeSpan delta = current - MemoryPackSerializer.Deserialize<DateTime>(utc);
            pingInfo[id].tmpPings.Add(delta.TotalMilliseconds);
            Logger.Log("GetPong", $"{id.ToMaskedString()} sent pong at {MemoryPackSerializer.Deserialize<DateTime>(utc)}");

            if(pingInfo[id].tmpPings.Count == p2pConfig.Instance.SamplesPerPing){
                pingInfo[id].Ping = (int)(pingInfo[id].tmpPings.Sum() / pingInfo[id].tmpPings.Count);
                pingInfo[id].LastUpdatedLocalUTC = current;
                pingInfo[id].tmpPings.Clear();
                refreshMembers++;
            }
        }
    }
}