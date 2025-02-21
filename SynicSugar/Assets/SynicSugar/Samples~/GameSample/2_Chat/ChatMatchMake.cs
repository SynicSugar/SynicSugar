using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SynicSugar.MatchMake;
using SynicSugar.P2P;
/// <summary>
/// Awake: Prep for matchmaking (For basis of Events. This sample dosen't have condition for matchmaking)
/// Start: Try to reconnect
/// UserInputs: Matchmaking or OfflineMode
/// </summary>
namespace SynicSugar.Samples.Chat 
{
    public class ChatMatchMake : MonoBehaviour 
    {
        private enum MatchmakingState 
        { 
            NoneAndAfterStart, Standby, InMatchmaking, ReadyToStartGame
        }

        /// <summary>
        /// Display matchmaking state on GUI.
        /// </summary>
        [SerializeField] private Text matchmakeState;
        [SerializeField] private Button startMatchMake, startOfflineMode, 
                        cancelMatchMake, backtoMenu, startGame;
        [SerializeField] private Text buttonText;
        [SerializeField] private MatchMakeConditions matchConditions;
        private void Awake()
        {
            SetGUIEvents();
        }
    #region Init and Reconnection
        //Second,　check whether this player is a reconnector.
        //In fact, you had better check id like this on the Title screen after user Login to EOS.
        private void Start()
        {   
            PacketMonitor.Instance.SetUpdateTiming(PacketMonitor.UpdateTiming.Update);
            Debug.Log("ChatMatchmake");
            //Prep matchmaking
            // SetGUIEvents();
            SwitchButtonsActive(MatchmakingState.NoneAndAfterStart);
            //Try recconect
            //Sample projects use LobbyID save API of SynicSugar to save into Playerprefs for recconection.
            string LobbyID = MatchMakeManager.Instance.GetReconnectLobbyID();
            TryToreconnect(LobbyID).Forget();
        }
        /// <summary>
        /// Register tests and button events for in-matchmaking.
        /// </summary>
        private void SetGUIEvents()
        {
            MatchMakeManager.Instance.MatchMakingGUIEvents = MatchMakeConfig.SetMatchingText(MatchMakeConfig.Langugage.EN);
            MatchMakeManager.Instance.MatchMakingGUIEvents.stateText = matchmakeState;

            MatchMakeManager.Instance.MatchMakingGUIEvents.DisableStart += OnDisableStart;
            MatchMakeManager.Instance.MatchMakingGUIEvents.EnableCancelKick += OnEnableCancel;
        }
        /// <summary>
        /// Just after starting matchmaking
        /// </summary>
        private void OnDisableStart()
        {
            //Cancel action need be disable when creating or joining lobby.
            SwitchButtonsActive(MatchmakingState.NoneAndAfterStart);
        }
        /// <summary>
        /// Finish to create or join a lobby, and accept canceling process.
        /// </summary>
        private void OnEnableCancel()
        {
            SwitchButtonsActive(MatchmakingState.InMatchmaking);
        }
        private async UniTask TryToreconnect(string LobbyID)
        {
            //On the default way, return Empty when there is no lobby data in local.
            if(string.IsNullOrEmpty(LobbyID))
            {
                SynicSugarDebug.Instance.Log($"This user is not Reconnecter.");
                SwitchButtonsActive(MatchmakingState.Standby);
                return;
            }

            Result result = await MatchMakeManager.Instance.ReconnectLobby(LobbyID);

            if(result != Result.Success)
            {
                SynicSugarDebug.Instance.Log("Fail to Re-Connect Lobby.", result);
                SwitchButtonsActive(MatchmakingState.Standby);
                return;
            }
            SynicSugarDebug.Instance.Log($"Success Recconect! LobbyID:{MatchMakeManager.Instance.GetCurrentLobbyID()}");
            SwitchButtonsActive(MatchmakingState.ReadyToStartGame);
        }
    #endregion
        /// <summary>
        /// Switch button active
        /// </summary>
        /// <param name="state"></param>
        private void SwitchButtonsActive(MatchmakingState state)
        {
            //To start matchmake
            startMatchMake.gameObject.SetActive(state == MatchmakingState.Standby);
            startOfflineMode.gameObject.SetActive(state == MatchmakingState.Standby);
            //To cancel matchmake
            cancelMatchMake.gameObject.SetActive(state == MatchmakingState.InMatchmaking);
            backtoMenu.gameObject.SetActive(state == MatchmakingState.InMatchmaking);
            //To start game
            startGame.gameObject.SetActive(state == MatchmakingState.ReadyToStartGame);
        }
    #region Matchmaking and Offlinemode
        /// <summary>
        /// Basis way to start matchmaking.
        /// </summary>
        public void StartMatchMake()
        {
            SynicSugarDebug.Instance.Log("Start MatchMake.");
            StartMatchMakeEntity().Forget();
        }
        /// <summary>
        /// SynicSugar has some async process, so implement only async part separately.
        /// </summary>
        /// <returns></returns>
        internal async UniTask StartMatchMakeEntity(){
            SwitchButtonsActive(MatchmakingState.NoneAndAfterStart);
            Result result = await MatchMakeManager.Instance.SearchAndCreateLobby(matchConditions.GetLobbyCondition(2));
                
            if(result != Result.Success)
            {
                SynicSugarDebug.Instance.Log("MatchMaking Failed.", result);
                SwitchButtonsActive(MatchmakingState.Standby);
                return;
            }

            SynicSugarDebug.Instance.Log($"Success Matching! LobbyID:{MatchMakeManager.Instance.GetCurrentLobbyID()}");
            SynicSugarDebug.Instance.Log($"CurrentSessionStartUTC :{p2pInfo.Instance.CurrentSessionStartUTC}");
            SwitchButtonsActive(MatchmakingState.ReadyToStartGame);
        }
        /// <summary>
        /// You can register NON-UniTask async process to Unity button.
        /// I implement everything the same way as StartMatchMake in my-own project for the performance, though.
        /// </summary>
        public async void StartOfflineMode()
        {
            SynicSugarDebug.Instance.Log("Start Offline Mode.");
            //To　simulate matchmaking
            // OfflineMatchmakingDelay delay = new OfflineMatchmakingDelay(2000, 1000, 1000, 1000);
            OfflineMatchmakingDelay delay = OfflineMatchmakingDelay.NoDelay;
            //This is always true.
            Result result = await MatchMakeManager.Instance.CreateOfflineLobby(matchConditions.GetLobbyCondition(2), delay);

            //CreateOfflineLobby always returns Success. So this is no point.
            if(result != Result.Success) return; 

            SynicSugarDebug.Instance.Log($"Success Matching! LobbyID:{MatchMakeManager.Instance.GetCurrentLobbyID()}");
            SynicSugarDebug.Instance.Log($"CurrentSessionStartUTC :{p2pInfo.Instance.CurrentSessionStartUTC}");
            SwitchButtonsActive(MatchmakingState.ReadyToStartGame);
        }
        /// <summary>
        /// For button. Just cancel matchmaking.
        /// </summary>
        public void CancelMatchMaking()
        {
            CancelMatchMaking(isOfflineMode()).Forget();
        }
        public async UniTask CancelMatchMaking(bool isOfflineMode)
        {
            if(isOfflineMode)
            {
                await ConnectHub.Instance.DestoryOfflineLobby(false);
            }
            else
            {
                await MatchMakeManager.Instance.ExitCurrentMatchMake(false);
            }
        }
        /// <summary>
        /// For button. Cancel matchmaking, and then return to Menu.
        /// </summary>
        public void CanelMatchMakingAndReturnToMenu()
        {
            CanelMatchMakingAndReturnToMenu(isOfflineMode()).Forget();
        }
        public async UniTask CanelMatchMakingAndReturnToMenu(bool isOfflineMode)
        {
            if(isOfflineMode)
            {
                await ConnectHub.Instance.DestoryOfflineLobby();
            }
            else
            {
                await MatchMakeManager.Instance.ExitCurrentMatchMake(true);
            }
            SceneChanger.ChangeGameScene(Scene.MainMenu);
        }
        /// <summary>
        /// Offline lobby id is "OFFLINEMODE"
        /// </summary>
        /// <returns></returns>
        private bool isOfflineMode()
        {
            return MatchMakeManager.Instance.GetCurrentLobbyID() == "OFFLINEMODE";
        }
    #endregion
    }
}