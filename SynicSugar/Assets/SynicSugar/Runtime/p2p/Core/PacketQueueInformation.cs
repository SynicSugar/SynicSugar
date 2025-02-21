namespace SynicSugar.P2P {
    public class PacketQueueInformation
    {
        /// <summary>
        /// The maximum size in bytes of the incoming packet queue
        /// </summary>
        public ulong IncomingPacketQueueMaxSizeBytes { get; internal set; }
        /// <summary>
        /// The current size in bytes of the incoming packet queue
        /// </summary>
        public ulong IncomingPacketQueueCurrentSizeBytes { get; internal set; }
        /// <summary>
        /// The current number of queued packets in the incoming packet queue
        /// </summary>
        public ulong IncomingPacketQueueCurrentPacketCount { get; internal set; }
        /// <summary>
        /// The maximum size in bytes of the outgoing packet queue
        /// </summary>
        public ulong OutgoingPacketQueueMaxSizeBytes { get; internal set; }
        /// <summary>
        /// The current size in bytes of the outgoing packet queue
        /// </summary>
        public ulong OutgoingPacketQueueCurrentSizeBytes { get; internal set; }
        /// <summary>
        /// The current amount of queued packets in the outgoing packet queue
        /// </summary>
        public ulong OutgoingPacketQueueCurrentPacketCount { get; internal set; }
    }
}