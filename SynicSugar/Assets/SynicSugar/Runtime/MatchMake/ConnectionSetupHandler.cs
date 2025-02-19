using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SynicSugar.P2P;
using MemoryPack;
using MemoryPack.Compression;
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using ResultE = Epic.OnlineServices.Result;

namespace SynicSugar.MatchMake {
    internal class ConnectionSetupHandler {
        /// <summary>
        /// To open and request initial connection.
        /// </summary>
        /// <returns>Return true, after end the conenction. If pass time before finish prepartion, return false/</returns>
        internal async UniTask<Result> WaitConnectPreparation(CancellationToken token, int timeoutMS){
            await UniTask.WhenAny(UniTask.WaitUntil(() => p2pInfo.Instance.ConnectionNotifier.completeConnectPreparetion, cancellationToken: token), UniTask.Delay(timeoutMS, cancellationToken: token));

            Logger.Log("WaitConnectPreparation", "Connection setup is ready. Proceed to user list synchronization.");

            if(!p2pConfig.Instance.UseDisconnectedEarlyNotify){
                ((EOSSessionManager)p2pConfig.Instance.sessionCore).RemoveNotifyPeerConnectionnEstablished();
            }
            if(!p2pInfo.Instance.ConnectionNotifier.completeConnectPreparetion){
                await p2pConfig.Instance.GetNetworkCore().CloseSession(false, true, token);
                return Result.TimedOut;
            }
            return Result.Success;
        }
        /// <summary>
        /// Different Assembly can have same CH, but not sorted when receive packet. <br />
        /// So must not use the same ch for what SynicSugar may receive at the same time.
        /// </summary>
        const byte BASICINFO_CH = 252;
        const int SEND_BATCH_SIZE = 8;
        SocketId ReferenceSocketId;
        // Allocate memory at maximum packet size in advance.
        byte[] buffer = new byte[1170];
        #region Send
        /// <summary>
        /// For Host to send List after re-connecter has came.
        /// </summary>
        internal void SendUserList(UserId target){
            BasicInfo basicInfo = new BasicInfo();
            basicInfo.userIds = p2pInfo.Instance.AllUserIds.ConvertAll(id => id.ToString());
            
            if(p2pInfo.Instance.DisconnectedUserIds.Count > 0){
                for(int i = 0; i < p2pInfo.Instance.DisconnectedUserIds.Count; i++){
                    basicInfo.disconnectedUserIndexes.Add((byte)p2pInfo.Instance.AllUserIds.IndexOf(p2pInfo.Instance.DisconnectedUserIds[i]));
                }
            }
            basicInfo.ElapsedSecSinceStart = p2pInfo.Instance.GetSessionTimestamp();

            using var compressor  = new BrotliCompressor(MatchMakeManager.Instance.BasicInfoPacketCompressionLevel);
            MemoryPackSerializer.Serialize(compressor, basicInfo);
            SendPacket(BASICINFO_CH, compressor.ToArray(), target);
        }
        /// <summary>
        /// For Host to send AllUserList after connection.
        /// </summary>
        internal async UniTask SendUserListToAll(CancellationToken token){
            BasicInfo basicInfo = new BasicInfo();
            basicInfo.userIds = p2pInfo.Instance.AllUserIds.ConvertAll(id => id.ToString());
            
            using var compressor  = new BrotliCompressor(MatchMakeManager.Instance.BasicInfoPacketCompressionLevel);
            MemoryPackSerializer.Serialize(compressor, basicInfo);
            
            int count = SEND_BATCH_SIZE;
            var compressorArray = compressor.ToArray();
            foreach(var id in p2pInfo.Instance.userIds.RemoteUserIds){
                SendPacket(BASICINFO_CH, compressorArray, id);

                count--;
                if(count <= 0){
                await UniTask.Yield(cancellationToken: token);
                    if(token.IsCancellationRequested){
                        Logger.LogWarning("SendUserListToAll", "Get out of the loop by Cancel");
                        break;
                    }
                    count = SEND_BATCH_SIZE;
                }
            }
        }
        
        static void SendPacket(byte ch, byte[] value, UserId targetId){
            SendPacketOptions options = new SendPacketOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                RemoteUserId = targetId.AsEpic,
                SocketId = p2pConfig.Instance.sessionCore.SocketId,
                Channel = ch,
                AllowDelayedDelivery = true,
                Reliability = PacketReliability.ReliableUnordered,
                Data = new ArraySegment<byte>(value)
            };

            P2PInterface P2PHandle = EOSManager.Instance.GetEOSPlatformInterface().GetP2PInterface();
            ResultE result = P2PHandle.SendPacket(ref options);

            if(result != ResultE.Success){
                Logger.LogError("SendUserLists", "Can't send packet.", (Result)result);
                return;
            }
        }
        #endregion
        #region Receive
        internal async UniTask ReciveUserIdsPacket(CancellationToken token){
            P2PInterface p2pInterface = EOSManager.Instance.GetEOSPlatformInterface().GetP2PInterface();
            //Next packet size
            var getNextReceivedPacketSizeOptions = new GetNextReceivedPacketSizeOptions {
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                RequestedChannel = BASICINFO_CH
            };
            byte ch = BASICINFO_CH;
            ProductUserId id = new();
            ArraySegment<byte> payload = new();
            while(!token.IsCancellationRequested){
                bool recivePacket = GetPacketFromBuffer(ref p2pInterface, ref getNextReceivedPacketSizeOptions, ref ch, ref id, ref payload);

                if(recivePacket){
                    UnityEngine.Debug.Log("ReciveUserIdsPacket");
                    ConvertFromPacket(in ch, in payload);
                    return;
                }
                await UniTask.Yield(cancellationToken: token);
            }
        }
        /// <summary>
        /// To get basicInfo packet about a user list.
        /// </summary>
        /// <param name="ch"></param>
        /// <param name="id"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        bool GetPacketFromBuffer(ref P2PInterface p2pInterface,ref GetNextReceivedPacketSizeOptions sizeOptions, ref byte ch, ref ProductUserId id, ref ArraySegment<byte> payload){
            ResultE result = p2pInterface.GetNextReceivedPacketSize(ref sizeOptions, out uint nextPacketSizeBytes);
            if(result != ResultE.Success){
                return false; //No packet
            }

            //Set options
            ReceivePacketOptions options = new ReceivePacketOptions(){
                LocalUserId = p2pInfo.Instance.userIds.LocalUserId.AsEpic,
                MaxDataSizeBytes = nextPacketSizeBytes,
                RequestedChannel = BASICINFO_CH
            };

            payload = new ArraySegment<byte>(buffer, 0, (int)nextPacketSizeBytes);
            result = p2pInterface.ReceivePacket(ref options, ref id, ref ReferenceSocketId, out byte outChannel, payload, out uint bytesWritten);
            
            if (result != ResultE.Success){
                return false; //No packet
            }
            ch = outChannel;

            return true;
        }
        
        void ConvertFromPacket(in byte ch, in ArraySegment<byte> payload){
            if(ch != BASICINFO_CH){
                Logger.LogError("ConvertFromPacket", "Could not get packets about UserList because the packets were on different channels.");
                return;
            }

            using var decompressor = new BrotliDecompressor();
            var decompressed = decompressor.Decompress(payload);

            BasicInfo data = MemoryPackSerializer.Deserialize<BasicInfo>(decompressed);
            p2pInfo.Instance.userIds.OverwriteAllUserIdsWithOrdered(data);
        }
        #endregion
    }
}