using System;
namespace SynicSugar.MatchMake {
    /// <summary>
    /// Events to manage LobbyID for reconenction as Action.
    /// </summary>
    public class LobbyIDMethod {
        public event Action Save, Delete;
        /// <summary>
        /// Register functions to save and delete lobby Id to re-connect.<br />
        /// Can be registerd in the same way as a normal event. In that case, change MatchMakeManager.Instance.lobbyIdSaveType to CustomMethod.<br />
        /// We can use cloud and save assets for this, but these place to be saved and deleted must be in the same. 
        /// </summary>
        /// <param name="save">void function</param>
        /// <param name="delete">void function</param>
        /// <param name="changeType">If true, change SaveType in this method</param>
        public void Register(Action save, Action delete, bool changeType = true){
            if(changeType){
                MatchMakeManager.Instance.lobbyIdSaveType = MatchMakeManager.RecconectLobbyIdSaveType.CustomMethod;
                Logger.Log("LobbyIDMethod.Register", $"SaveType changed to CustomMethod.");
            }
            Save += save;
            Delete += delete;
        }
        /// <summary>
        /// Remove all events from the actions. <br />
        /// Events are automatically cleared when they are no longer needed.
        /// </summary>
        public void Clear(){
            Save = null;
            Delete = null;
        }
        internal void OnSave(){
            Save?.Invoke();
        }
        internal void OnDelete(){
            Delete?.Invoke();
        }   
    }
}