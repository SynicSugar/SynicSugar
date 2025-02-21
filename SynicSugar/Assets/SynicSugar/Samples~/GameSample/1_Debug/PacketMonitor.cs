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
        private bool incomingIsZero, outgoingIsZero ;
        public void SetUpdateTiming(UpdateTiming updateTiming)
        {
            incomingIsZero = false;
            outgoingIsZero = false;
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
                if(packetQueueInformation.IncomingPacketQueueCurrentPacketCount != 0 || !incomingIsZero)
                {
                    incomingIsZero = packetQueueInformation.IncomingPacketQueueCurrentPacketCount == 0;
                    Debug.Log($"PacketQueueInfo(Incoming): IncomingPacketQueueMaxSizeBytes: {packetQueueInformation.IncomingPacketQueueMaxSizeBytes} / IncomingPacketQueueCurrentSizeBytes: {packetQueueInformation.IncomingPacketQueueCurrentSizeBytes} / IncomingPacketQueueCurrentPacketCount: {packetQueueInformation.IncomingPacketQueueCurrentPacketCount}");   
                }
                // Outgoing Packets
                if(packetQueueInformation.OutgoingPacketQueueCurrentPacketCount != 0 || !outgoingIsZero)
                {
                    outgoingIsZero = packetQueueInformation.OutgoingPacketQueueCurrentPacketCount == 0;
                    Debug.Log($"PacketQueueInfo(Outgoing): OutgoingPacketQueueMaxSizeBytes: {packetQueueInformation.OutgoingPacketQueueMaxSizeBytes} / OutgoingPacketQueueCurrentSizeBytes: {packetQueueInformation.OutgoingPacketQueueCurrentSizeBytes} / OutgoingPacketQueueCurrentPacketCount: {packetQueueInformation.OutgoingPacketQueueCurrentPacketCount}");
                }
            }
        }
    }
}
