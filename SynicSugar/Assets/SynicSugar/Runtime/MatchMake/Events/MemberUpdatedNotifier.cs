using System;
using SynicSugar.P2P;

namespace SynicSugar.MatchMake {
    /// <summary>
    /// Notify when member attributes are changed.
    /// </summary>
    public class MemberUpdatedNotifier {
        /// <summary>
        /// Invoke when a user attributes is updated in current lobby.</ br>
        /// </summary>
        public event Action<UserId> OnAttributesUpdated;

        public void Register(Action<UserId> attributesUpdated){
            OnAttributesUpdated += attributesUpdated;
        }
        /// <summary>
        /// Remove all events from the actions. <br />
        /// Events are automatically cleared when they are no longer needed.
        /// </summary>
        public void Clear(){
            OnAttributesUpdated = null;
        }
        internal void MemberAttributesUpdated(UserId target){
            Logger.Log("MemberAttributesUpdated", $"UserId: {target.ToMaskedString()}");
            OnAttributesUpdated?.Invoke(target);
        }
    }
}