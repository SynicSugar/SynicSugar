using System;
using Cysharp.Threading.Tasks;

namespace SynicSugar.MatchMake {
    /// <summary>
    /// Events to manage LobbyID for reconenction as async Func.
    /// </summary>
    public class AsyncLobbyIDMethod {
        public event Func<UniTask> Save, Delete;
        /// <summary>
        /// Register functions that returns UniTask to save and delete lobby Id to re-connect.<br />
        /// Can be registerd in the same way as a normal event. In that case, change MatchMakeManager.Instance.lobbyIdSaveType to AsyncCustomMethod.<br />
        /// We can use cloud and save assets for this, but these place to be saved and deleted must be in the same. 
        /// </summary>
        /// <param name="save">UniTask function</param>
        /// <param name="delete">UniTask function</param>
        /// <param name="changeType">If true, change SaveType in this method</param>
        public void Register(Func<UniTask> save, Func<UniTask> delete, bool changeType = true){
            if(changeType){
                MatchMakeManager.Instance.lobbyIdSaveType = MatchMakeManager.RecconectLobbyIdSaveType.AsyncCustomMethod;
                Logger.Log("AsyncLobbyIDMethod.Register", $"SaveType changed to AsyncCustomMethod.");
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
        internal async UniTask OnSave(){
            if(Save == null){
                return;
            }
            await Save().AsAsyncUnitUniTask();
        }
        internal async UniTask  OnDelete(){
            if(Delete == null){
                return;
            }
            await Delete().AsAsyncUnitUniTask();
        }
    }
}