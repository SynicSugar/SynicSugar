using System;
using UnityEngine;

namespace SynicSugar.P2P{
    public abstract class PacketReceiver : MonoBehaviour
    {
        protected byte ch_r;
        protected UserId id_r;
        protected ArraySegment<byte> payload_r;
        protected uint maxBatchSize;
        protected IPacketConvert hub;
        protected Base.SessionCore getPacket;
        void Awake(){
            enabled = false;
        }
        public void SetGetPacket(Base.SessionCore instance){
            getPacket = instance;
        }
        /// <summary>
        /// Must manage this object active from here.
        /// </summary>
        /// <param name="ReceivingBatchSize"></param>
        public virtual void StartPacketReceiver(IPacketConvert hubInstance, uint ReceivingBatchSize = 1){
            Logger.Log("StartPacketReceiver", $"Activate {this.GetType()}.");
            hub = hubInstance;
            maxBatchSize = ReceivingBatchSize;
            this.enabled = true;
        }
        public virtual void StopPacketReceiver(){
            Logger.Log("StartPacketReceiver", $"Stop {this.GetType()}.");
            this.enabled = false;
            maxBatchSize = 0;
        }
    }
}