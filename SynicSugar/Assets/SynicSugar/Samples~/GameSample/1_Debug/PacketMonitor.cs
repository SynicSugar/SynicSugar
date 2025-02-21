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
        public UpdateTiming updateTiming { get; private set; }  = UpdateTiming.None;
        public void SetUpdateTiming(UpdateTiming updateTiming)
        {
            this.updateTiming = updateTiming;
        }
        void Update()
        {
            if(updateTiming != UpdateTiming.Update){ return; }
            GetPacketQueueInfo();
        }
        void FixedUpdate()
        {
            if(updateTiming != UpdateTiming.FixedUpdate){ return; }
            GetPacketQueueInfo();
        }
        void LateUpdate()
        {
            if(updateTiming != UpdateTiming.LateUpdate){ return; }
            GetPacketQueueInfo();
        }
        void GetPacketQueueInfo(){
            Result result = p2pInfo.Instance.GetPacketQueueInfo(out PacketQueueInformation packetQueueInformation);
            if(result == Result.Success){
                Debug.Log($"PacketQueueInfo(Incoming): IncomingPacketQueueMaxSizeBytes: {packetQueueInformation.IncomingPacketQueueMaxSizeBytes} / IncomingPacketQueueCurrentSizeBytes: {packetQueueInformation.IncomingPacketQueueCurrentSizeBytes} / IncomingPacketQueueCurrentPacketCount: {packetQueueInformation.IncomingPacketQueueCurrentPacketCount}");
                Debug.Log($"PacketQueueInfo(Outgoing): OutgoingPacketQueueMaxSizeBytes: {packetQueueInformation.OutgoingPacketQueueMaxSizeBytes} / OutgoingPacketQueueCurrentSizeBytes: {packetQueueInformation.OutgoingPacketQueueCurrentSizeBytes} / OutgoingPacketQueueCurrentPacketCount: {packetQueueInformation.OutgoingPacketQueueCurrentPacketCount}");
            }
        }
    }
}
