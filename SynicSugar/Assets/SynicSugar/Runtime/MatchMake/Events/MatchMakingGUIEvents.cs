#pragma warning disable CS0414 //The field is assigned but its value is never used
using System;
#if SYNICSUGAR_TMP
using TMPro;
#else
using UnityEngine.UI;
#endif

namespace SynicSugar.MatchMake {
    /// <summary>
    /// To register and manage events that occur during matchmaking.
    /// </summary>
    [Serializable]
    public class MatchMakingGUIEvents {
        public MatchMakingGUIEvents(){}
    #if SYNICSUGAR_TMP
        public MatchMakingGUIEvents(TMP_Text displayedStateText){
            stateText = displayedStateText;
        }
    #else
        public MatchMakingGUIEvents(Text displayedStateText){
            stateText = displayedStateText;
        }
    #endif

        /// <summary>
        /// To display text state.
        /// </summary>
    #if SYNICSUGAR_TMP
        public TMP_Text stateText = null;
    #else
        public Text stateText = null;
    #endif
        bool enabledManualConclude = false;
        // public TextP
        public bool canKick { get; internal set; }
    #region Event
        /// <summary>
        /// After push start button, prevent to be re-pressed matchmaking button.
        /// </summary>
        public event Action DisableStart;
        /// <summary>
        /// After join and create lobby, accept cancel matchmake and host's kick member.
        /// </summary>
        public event Action EnableCancelKick;
        /// <summary>
        /// This is enabled when the required members are passed to the Matchmaking API.<br />
        /// After filled in the minimum number of members, can manually close Lobby to start p2p.
        /// </summary>
        public event Action EnableHostConclude;
        /// <summary>
        /// This is enabled when the required members are passed to the Matchmaking API.<br />
        /// Invoked when members leave and condition is no longer met.
        /// </summary>
        public event Action DisableHostConclude;
        /// <summary>
        /// After complete or cancel matchmaking, prevent to change lobby state.
        /// </summary>
        public event Action DisableCancelKickConclude;
        /// <summary>
        /// Invoked when the number of participants in the lobby changes.
        /// </summary>
        public event Action<UserId, bool> OnLobbyMemberCountChanged;
    #endregion
    #region Text
        /// <summary>
        /// Wait for starting(pushing) the matchmaking process (button).<br />
        /// </summary>
        public string Standby;
        /// <summary>
        /// Until join or create lobby.<br />
        /// Invoke DisableStart().
        /// </summary>
        public string StartMatchmaking;
        /// <summary>
        /// Wait for opponents in Lobby until finish Matchmake.<br />
        /// Invoke EnableCancelKick(). canKick becomes true.
        /// </summary>
        public string WaitForOpponents;
        /// <summary>
        /// After Filled lobby or close the matchmaking in manually, start to exchange data for p2p.<br />
        /// Invoke DisableCancelKickFinish().
        /// </summary>
        public string FinishMatchmaking;
        /// <summary>
        /// Can start p2p and game.<br />
        /// (Move to GameScene or that UI mode.)
        /// </summary>
        public string ReadyForConnection;
        /// <summary>
        /// Leave or destroy lobby.<br />
        /// Invoke DisableCancelKickFinish().
        /// </summary>
        public string TryToCancel;
        /// <summary>
        /// Until join lobby. Same StartMatchmaking<br />
        /// Invoke DisableStart().
        /// </summary>
        public string StartReconnection;
    #endregion
        public enum State {
            /// <summary>
            /// Matchmaking is idle and ready to be started by user interaction (e.g., pressing a button).
            /// No matchmaking process is currently active.
            /// </summary>
            Standby,
            /// <summary>
            /// Matchmaking process has been initiated, attempting to join or create a lobby.
            /// </summary>
            Start,
            /// <summary>
            /// Waiting for opponents in the lobby to join until matchmaking is complete.
            /// </summary>
            Wait,
            /// <summary>
            /// Lobby is filled or matchmaking has been manually closed. 
            /// Begins exchanging data for peer-to-peer (P2P) connection setup.
            /// </summary>
            SetupP2P,
            /// <summary>
            /// P2P connection is ready and the game can start.
            /// Usually transitions to the game scene or closing matchmaking GUI.
            /// </summary>
            Ready,
            /// <summary>
            /// Cancels matchmaking by leaving or destroying the lobby.
            /// </summary>
            Cancel,
            /// <summary>
            /// Reconnection process begins after disconnection, attempting to rejoin a lobby.
            /// </summary>
            Reconnect
        }
        /// <summary>
        /// Remove all event and clear string data.
        /// </summary>
        internal void Reset(){
            Clear();
            
        #if SYNICSUGAR_TMP
            stateText = null;
        #else
            stateText = null;
        #endif

            Standby = string.Empty;
            StartMatchmaking = string.Empty;
            WaitForOpponents = string.Empty;
            FinishMatchmaking = string.Empty;
            ReadyForConnection = string.Empty;
            TryToCancel = string.Empty;
            StartReconnection = string.Empty;
        }
        /// <summary>
        /// Remove all events from the actions. <br />
        /// Events are automatically cleared when they are no longer needed.
        /// </summary>
        public void Clear(){
            DisableStart = null;
            EnableCancelKick = null;
            EnableHostConclude = null;
            DisableHostConclude = null;
            DisableCancelKickConclude = null;
            OnLobbyMemberCountChanged = null;
        }
        
        internal void ChangeState(State state){
            Logger.Log("ChangeState", $"Matchmaking state has changed. Current state: {state}.");
            switch(state){
                case State.Standby:
                    canKick = false;
                    enabledManualConclude = false;
                    if(stateText != null){
                        SetText(Standby);
                    }
                break;
                case State.Start:
                    DisableStart?.Invoke();
                    canKick = false;
                    enabledManualConclude = false;
                    if(stateText != null){
                        SetText(StartMatchmaking);
                    }
                break;
                case State.Wait:
                    EnableCancelKick?.Invoke();
                    canKick = true;
                    if(stateText != null){
                        SetText(WaitForOpponents);
                    }
                break;
                case State.SetupP2P:
                    DisableCancelKickConclude?.Invoke();
                    canKick = false;
                    if(stateText != null){
                        SetText(FinishMatchmaking);
                    }
                break;
                case State.Ready:
                    if(stateText != null){
                        SetText(ReadyForConnection);
                    }
                break;
                case State.Cancel:
                    DisableCancelKickConclude?.Invoke();
                    canKick = false;
                    if(stateText != null){
                        SetText(TryToCancel);
                    }
                break;
                case State.Reconnect:
                    DisableStart?.Invoke();
                    canKick = false;
                    enabledManualConclude = false;
                    if(stateText != null){
                        SetText(StartReconnection);
                    }
                break;
            }
        }
        /// <summary>
        /// Set text to TMP or Legacy text.
        /// </summary>
        /// <param name="text"></param> <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        void SetText(string text){
        #if SYNICSUGAR_TMP
            stateText.SetText(text);
        #else
            stateText.text = text;
        #endif
        }

        /// <summary>
        /// To display Member count. <br />
        /// After meet lobby min member counts.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="isParticipated"></param>
        /// <param name="meetMinCondition"></param>
        internal void LobbyMemberCountChanged(UserId target, bool isParticipated, bool meetMinCondition){
            OnLobbyMemberCountChanged?.Invoke(target, isParticipated);
            if(!MatchMakeManager.Instance.isHost){
                return;
            }
            if(enabledManualConclude != meetMinCondition){
                enabledManualConclude = meetMinCondition;
                
                if(meetMinCondition){
                    EnableHostConclude?.Invoke();
                    Logger.Log("LobbyMemberCountChanged", "Matchmaking meets min member conditions. Host can close Lobby from now.");
                }else{
                    DisableHostConclude?.Invoke();
                    Logger.Log("LobbyMemberCountChanged", "The member conditions are no longer met.");
                }
            }
        }
        internal void LocalUserIsPromoted(bool meetMinCondition){
            if(!MatchMakeManager.Instance.isHost){
                return;
            }
            if(enabledManualConclude != meetMinCondition){
                enabledManualConclude = meetMinCondition;
                
                if(meetMinCondition){
                    EnableHostConclude?.Invoke();
                    Logger.Log("LocalUserIsPromoted", "Matchmaking meets min member conditions. Host can close Lobby from now.");
                }else{
                    DisableHostConclude?.Invoke();
                    Logger.Log("LocalUserIsPromoted", "The member conditions are no longer met.");
                }
            }
        }
        /// <summary>
        /// To display Member count.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="isParticipated"></param>
        internal void LobbyMemberCountChanged(UserId target, bool isParticipated){
            Logger.Log("LobbyMemberCountChanged", $"UserId({target.ToMaskedString()}) is participated: {isParticipated}");
            OnLobbyMemberCountChanged?.Invoke(target, isParticipated);
        }
    }
}