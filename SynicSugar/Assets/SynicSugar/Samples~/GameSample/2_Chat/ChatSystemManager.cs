using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SynicSugar.P2P;
using SynicSugar.RTC;
using UnityEngine;
using UnityEngine.UI;

namespace SynicSugar.Samples.Chat
{
    public class ChatSystemManager : MonoBehaviour 
    {
        [SerializeField] private GameObject matchmakeCanvas, chatCanvas;
        public Text chatText, inputCount;
        public InputField contentField, nameField;
        public GameObject chatPlayerPrefab, uiSetsPrefabs;
        private Dictionary<string, Text> vcStates = new Dictionary<string, Text>(); //Key is UserId.ToString().
        [SerializeField] private Transform vcStatesContentParent;
        [SerializeField] private Text vcStatePrefab;
        public RawImage ForLargePacket;

        private async UniTaskVoid Start() 
        {
            if(p2pInfo.Instance.AllUserIds.Count > 1) //=　p2pInfo.Instance.SessionType == SessionType.OnlineSession
            { 
                //Regsiter notify for Online mode.
                p2pInfo.Instance.ConnectionNotifier.OnTargetDisconnected += OnDisconect;
                p2pInfo.Instance.ConnectionNotifier.OnTargetConnected += OnConnected;
                p2pInfo.Instance.ConnectionNotifier.OnLobbyClosed += OnClosed;
                p2pInfo.Instance.SyncSnyicNotifier.OnSyncedSynic += OnSyncedSynic;
            }
            //At first, instantiate network objects.
            //It are registered to ConnectHub automatically.
            SynicObject.AllSpawn(chatPlayerPrefab);
            //For reconencter
            //IsReconnecter means that this local user is a reconnector and they has not received Synic data about themself.
            if(p2pInfo.Instance.IsReconnecter)
            {
                Debug.Log("Start SynicReceiver");
                //To get SynicPacket.
                ConnectHub.Instance.StartSynicReceiver(5);
                //This flag(HasReceivedAllSyncSynic) cannot be used at the same time. Once it returns True, it returns False again.
                await UniTask.WaitUntil(() => p2pInfo.Instance.HasReceivedAllSyncSynic, cancellationToken: this.GetCancellationTokenOnDestroy());
                Debug.Log("Finish SynicReceiver");
                UpdateChatCount();
            }
            
            //To get AllPacket.
            ConnectHub.Instance.StartPacketReceiver(PacketReceiveTiming.FixedUpdate, 5);
            if(p2pInfo.Instance.AllUserIds.Count > 1)//VC setting for Online mode.
            { 
                await RTCManager.Instance.StartVoiceSending();
                // VC actions with No args
                // RTCManager.Instance.ParticipantUpdatedNotifier.Register(() => OnStartSpeaking(), t => OnStopSpeaking());
                RTCManager.Instance.ParticipantUpdatedNotifier.Register(t => OnStartSpeaking(t), t => OnStopSpeaking(t));
            }
        }
        void Oestroy()
        {
            #if SYNICSUGAR_PACKETINFO
                PacketMonitor.Instance.SetUpdateTiming(PacketMonitor.UpdateTiming.None);
            #endif
        }
#if SYNICSUGAR_FPSTEST
        private void Update()
        {
            float fps = 1f / Time.deltaTime;
            Debug.Log("--UPDATE-- fps:" + fps);
        }
#endif
        public void SwitchPanelContent()
        {
            matchmakeCanvas.SetActive(false);
            chatCanvas.SetActive(true);
        }
        public void GenerateVCStateObject(UserId userId)
        {
            Text stateText = Instantiate(vcStatePrefab, vcStatesContentParent);
            vcStates.Add(userId.ToString(), stateText);
            string tmpName = p2pInfo.Instance.IsLoaclUser(userId) ? "LocalPlayer" : "RemotePlayer";
            stateText.text = $"{tmpName}: Not-Speaking";
        }
        
        public void UpdateChatCount()
        {
            if(p2pInfo.Instance.AllUserIds.Count > 1)
            {
                inputCount.text = $"ChatCount: {ConnectHub.Instance.GetUserInstance<ChatPlayer>(p2pInfo.Instance.LocalUserId).submitCount} / {ConnectHub.Instance.GetUserInstance<ChatPlayer>(p2pInfo.Instance.CurrentRemoteUserIds[0]).submitCount}";
            }
            else
            {
                inputCount.text = $"ChatCount: {ConnectHub.Instance.GetUserInstance<ChatPlayer>(p2pInfo.Instance.LocalUserId).submitCount} / --";
            }
        }
        private void OnDisconect(UserId id)
        {
            chatText.text += $"{id} is Disconnected / {p2pInfo.Instance.LastDisconnectedUsersReason}{System.Environment.NewLine}";
        }
        private void OnConnected(UserId id)
        {
            chatText.text += $"{id} Join {System.Environment.NewLine}";
            //Send local data
            ConnectHub.Instance.SyncSynic(p2pInfo.Instance.LastConnectedUsersId, SynicType.WithOthers, 0, false);
        }
        
        /// <summary>
        /// When the host closes the lobby, the session will be terminated. The session will be closed and the MatchMake state will be reset. However, the state of NetworkManager will not be destroyed and MatchMaking GUI not clean up in this process.
        /// If you no longer need this manager for scene transitions, you need to destory or reset them manually.
        /// This event is only called when the host calls ConnectHub.Instance.CloseSession(); to close the lobby. If you want to continue the session after the host leaves, you should call ConnectHub.Instance.ExitSession(); to leave.
        /// If the guest calls ConnectHub.Instance.CloseSession();, ConnectHub.Instance.ExitSession(); will be called internally. Only the host can destroy the lobby.
        /// </summary>
        private void OnClosed(Reason reason)
        {
            chatText.text += $"This lobby is closed. Return to the lobby in 5 seconds. : {reason}";
            
            ReturnToMenu(reason).Forget();
        }

        private async UniTask ReturnToMenu(Reason reason){
            Debug.Log($"ReturnToMenu: Reason {reason} / IsMatchmaking {SynicSugarManger.Instance.State.IsMatchmaking} / IsInSession {SynicSugarManger.Instance.State.IsInSession} / SessionType {p2pInfo.Instance.SessionType}");
            await UniTask.Delay(5000);

            //To reset ConnectHub, we must call ExitSession or CloseSession.
            //This use always returns Success and is done synchronously, so it is called also possible as Forget().
            Result result = await ConnectHub.Instance.ExitSession();
            if (result != Result.Success) 
            {
                //This probably won't be called.
                Debug.LogError("Failed to exit session.");
                return;
            }

            SceneChanger.ChangeGameScene(Scene.MainMenu);
        }
        //Called each time a SyncSynic packet is received.
        //Use when Synic is used as just a large packet.
        //With this usage method, we can receive instance data about yourself, but data about others is discarded when packets are got from buffer. 
        //By setting syncSinglePhase to true and using p2pInfo.Instance.SyncedSynicPhase, we can also batch sync specific phases only.
        private void OnSyncedSynic()
        {
            if(p2pInfo.Instance.SyncedSynicPhase == 1)
            {  
                SynicSugarDebug.Instance.Log("GetLargePacket");
                chatText.text = ConnectHub.Instance.GetUserInstance<ChatPlayer>(p2pInfo.Instance.CurrentRemoteUserIds[0]).LargePacket;
            }
        }
        //VC actions with No args
        // private void OnStartSpeaking(){
        //     if(!vcStates.ContainsKey(RTCManager.Instance.LastStateUpdatedUserId.ToString())){
        //         return;
        //     }
        //     string name = ConnectHub.Instance.GetUserInstance<ChatPlayer>(RTCManager.Instance.LastStateUpdatedUserId).Name;
        //     vcStates[RTCManager.Instance.LastStateUpdatedUserId.ToString()].text = $"{name}: Speaking";
        // }
        // void OnStopSpeaking(){
        //     if(!vcStates.ContainsKey(RTCManager.Instance.LastStateUpdatedUserId.ToString())){
        //         return;
        //     }
        //     string name = ConnectHub.Instance.GetUserInstance<ChatPlayer>(RTCManager.Instance.LastStateUpdatedUserId).Name;
        //     vcStates[RTCManager.Instance.LastStateUpdatedUserId.ToString()].text = $"{name}: Not-Speaking";
        // }

        private void OnStartSpeaking(UserId target)
        {
            if(!vcStates.ContainsKey(target.ToString())) return;
            
            string name = ConnectHub.Instance.GetUserInstance<ChatPlayer>(target).Name;
            vcStates[target.ToString()].text = $"{name}: Speaking";
        }
        private void OnStopSpeaking(UserId target)
        {
            if(!vcStates.ContainsKey(target.ToString())) return;
            
            string name = ConnectHub.Instance.GetUserInstance<ChatPlayer>(target).Name;
            vcStates[target.ToString()].text = $"{name}: Not-Speaking";
        }
    }
}