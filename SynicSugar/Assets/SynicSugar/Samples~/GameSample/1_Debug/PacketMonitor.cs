using SynicSugar.P2P;
using UnityEngine;

namespace SynicSugar.Samples {
    public class PacketMonitor : MonoBehaviour
    {
    #region Singleton Instance
        public static PacketMonitor Instance { get; private set; }
        private void Awake() 
        {
            if( Instance != null ) 
            {
                Destroy( this.gameObject );
                return;
            }
            Instance = this;
        }
        private void OnDestroy() 
        {
            if( Instance == this )
            {
                Instance = null;
            }
        }
    #endregion
        public enum UpdateTiming
        {
            None,
            Update,
            FixedUpdate,
            LateUpdate
        }
        public UpdateTiming updateTiming { get; private set; } = UpdateTiming.None;
        private ulong prevIncomingPacketSize, prevOutgoingPacketSize;
        public void SetUpdateTiming(UpdateTiming updateTiming)
        {
            prevIncomingPacketSize = ulong.MaxValue;
            prevOutgoingPacketSize = ulong.MaxValue;
            this.updateTiming = updateTiming;
        }
        private void Update()
        {
            if(updateTiming != UpdateTiming.Update){ return; }
            GetPacketQueueInfo();
        }
        private void FixedUpdate()
        {
            if(updateTiming != UpdateTiming.FixedUpdate){ return; }
            GetPacketQueueInfo();
        }
        private void LateUpdate()
        {
            if(updateTiming != UpdateTiming.LateUpdate){ return; }
            GetPacketQueueInfo();
        }
        private void GetPacketQueueInfo(){
            Result result = p2pInfo.Instance.GetPacketQueueInfo(out PacketQueueInformation packetQueueInformation);
            if(result == Result.Success){
                //Incoming Packets
                if(packetQueueInformation.IncomingPacketQueueCurrentPacketCount != prevIncomingPacketSize)
                {
                    prevIncomingPacketSize = packetQueueInformation.IncomingPacketQueueCurrentPacketCount;
                    Debug.Log($"PacketQueueInfo(Incoming): IncomingPacketQueueCurrentSizeBytes: {packetQueueInformation.IncomingPacketQueueCurrentSizeBytes} / IncomingPacketQueueCurrentPacketCount: {packetQueueInformation.IncomingPacketQueueCurrentPacketCount} / IncomingPacketQueueMaxSizeBytes: {packetQueueInformation.IncomingPacketQueueMaxSizeBytes}");   
                }
                // Outgoing Packets
                if(packetQueueInformation.OutgoingPacketQueueCurrentPacketCount != prevOutgoingPacketSize)
                {
                    prevOutgoingPacketSize = packetQueueInformation.OutgoingPacketQueueCurrentPacketCount;
                    Debug.Log($"PacketQueueInfo(Outgoing): OutgoingPacketQueueCurrentSizeBytes: {packetQueueInformation.OutgoingPacketQueueCurrentSizeBytes} / OutgoingPacketQueueCurrentPacketCount: {packetQueueInformation.OutgoingPacketQueueCurrentPacketCount} / OutgoingPacketQueueMaxSizeBytes: {packetQueueInformation.OutgoingPacketQueueMaxSizeBytes}");
                }
            }
        }
    }
}
