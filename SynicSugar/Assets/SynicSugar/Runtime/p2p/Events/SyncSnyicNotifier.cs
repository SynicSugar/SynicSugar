#pragma warning disable CS0414 //The field is assigned but its value is never used
using System;
using System.Collections.Generic;

namespace SynicSugar.P2P {
    public class SyncSnyicNotifier {
        /// <summary>
        /// Invoke when Synic variables is synced.
        /// </summary>
        public event Action OnSyncedSynic;
        
        public void Register(Action syncedSynic){
            OnSyncedSynic += syncedSynic;
        }
        /// <summary>
        /// Remove all events and init all variables. <br />
        /// NetworkObject can be used without being initialized, so called this on the last of the session.
        /// </summary>
        internal void Reset(){
            LastSyncedUserId = SynicSugarManger.Instance.LocalUserId;
            LastSyncedPhase = 0;
            ReceivedUsers.Clear();
            _receivedAllSyncSynic = false;
            includeDisconnectedData = false;
        }
        /// <summary>
        /// Remove all events from the actions.
        /// </summary>
        public void Clear(){
            OnSyncedSynic = null;
        }

        internal UserId LastSyncedUserId { get; private set; }
        internal byte LastSyncedPhase { get; private set; }
        bool _receivedAllSyncSynic;
        List<string> ReceivedUsers = new List<string>();
        bool includeDisconnectedData;
        internal bool ReceivedAllSyncSynic(){
            if(_receivedAllSyncSynic){
                //Init
                ReceivedUsers.Clear();
                _receivedAllSyncSynic = false;
                includeDisconnectedData = false;
                Logger.Log("ReceivedAllSyncSynic", "Initialize Synicflags and returne True.");

                return true;
            }
            return false;
        }
        /// <summary>
        /// Manage information about users who have received data and whether or not all of it has been received
        /// </summary>
        /// <param name="id"></param>
        /// <param name="phase"></param>
        internal void UpdateSynicStatus(string id, byte phase){
            Logger.Log("UpdateSynicStatus", $"Received Synic packet from {id.ToMaskedString()}.");
            if (!ReceivedUsers.Contains(id)){
                ReceivedUsers.Add(id);
                LastSyncedUserId = UserId.GetUserId(id);
                LastSyncedPhase = phase;
                //If the id of data owner is in disconnecter list, Host has send the data of disconnecter.
                //So, local user need extend the waiting condition.
                if(!includeDisconnectedData && p2pInfo.Instance.DisconnectedUserIds.Contains(UserId.GetUserId(id))){
                    includeDisconnectedData = true;
                }
            }

            OnSyncedSynic?.Invoke();

            if(ReceivedUsers.Count == (includeDisconnectedData ? p2pInfo.Instance.CurrentAllUserIds.Count : p2pInfo.Instance.CurrentConnectedUserIds.Count)){
                Logger.Log("UpdateSynicStatus", "Initialize because Synic flag returned True.Receive all Synic packets and reset flags.");
                _receivedAllSyncSynic = true;
            }
        }
    }
}