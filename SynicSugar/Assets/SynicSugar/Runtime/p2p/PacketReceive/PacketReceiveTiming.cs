namespace SynicSugar.P2P{
    /// <summary>
    /// This is the same timing with Unity each Update.
    /// </summary>
    public enum PacketReceiveTiming {
        /// <summary>
        /// Process in FixedUpdate (Physics updates)
        /// </summary>
        FixedUpdate,
        /// <summary>
        /// Process in Update (Frame-based updates)
        /// </summary>
        Update,
        /// <summary>
        /// Process in LateUpdate (After all Update calls)
        /// </summary>
        LateUpdate
    }
}