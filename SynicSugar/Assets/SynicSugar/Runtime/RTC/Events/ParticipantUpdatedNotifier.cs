#pragma warning disable CS0414 //The field is assigned but its value is never used
using System;
using SynicSugar.P2P;

namespace SynicSugar.RTC {
    public class ParticipantUpdatedNotifier {
        public event Action OnStartSpeaking;
        public event Action OnStopSpeaking;
        public event Action<UserId> OnStartTargetSpeaking;
        public event Action<UserId> OnStopTargetSpeaking;
        internal UserId TargetId { get; private set; }
        public void Register(Action startSpeaking, Action stopSpeaking){
            OnStartSpeaking += startSpeaking;
            OnStopSpeaking += stopSpeaking;
        }
        public void Register(Action<UserId> startSpeaking, Action<UserId> stopSpeaking){
            OnStartTargetSpeaking += startSpeaking;
            OnStopTargetSpeaking += stopSpeaking;
        }

        /// <summary>
        /// Remove all events from the actions. <br />
        /// Events are automatically cleared when they are no longer needed
        /// </summary>
        public void Clear(){
            OnStartSpeaking = null;
            OnStopSpeaking = null;
            OnStartTargetSpeaking = null;
            OnStopTargetSpeaking = null;
        }
        internal void StartSpeaking(UserId targetId){
            TargetId = targetId;
            OnStartTargetSpeaking?.Invoke(targetId);
            OnStartSpeaking?.Invoke();
        }
        internal void StopSpeaking(UserId targetId){
            TargetId = targetId;
            OnStopTargetSpeaking?.Invoke(targetId);
            OnStopSpeaking?.Invoke();
        }
    }
}