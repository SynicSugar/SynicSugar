using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.RTC;
using Epic.OnlineServices.RTCAudio;
using UnityEngine;
using SynicSugar.P2P;
using SynicSugar.MatchMake;
using System.Threading;
using Cysharp.Threading.Tasks;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SynicSugar.RTC {
    public class RTCManager : MonoBehaviour {
        public static RTCManager Instance { get; private set; }
        void Awake() {
            Logger.Log("RTCManager", "Start initialization of RTCManager.");
            if( Instance != null ) {
                Destroy( this );
                Logger.Log("RTCManager", "Discard this instance since RTCManager already exists.");
                return;
            }
            Instance = this;
            ParticipantUpdatedNotifier = new();
        }
        void OnDestroy() {
            if( Instance == this ) {
                ParticipantUpdatedNotifier.Clear();

                Instance = null;
                DisposePTTToken();
            }
        }
        /// <summary>
        /// Set from Matchmaking core.
        /// </summary>
        /// <param name="currentLobbyInstance"></param>
        internal void SetLobbyReference(Lobby currentLobbyInstance){
            CurrentLobby = currentLobbyInstance;
        }
        Lobby CurrentLobby;
        RTCInterface rtcInterface;
        RTCAudioInterface audioInterface;
        CancellationTokenSource pttToken;
        ulong ParticipantStatusId, ParticipantUpdatedId = 0;
        public ParticipantUpdatedNotifier ParticipantUpdatedNotifier;
        /// <summary>
        /// Indicates whether the current Lobby has VC enabled.
        /// </summary>
        public bool IsVoiceChatEnabled { get { return CurrentLobby != null && CurrentLobby.bEnableRTCRoom; } }
        /// <summary>
        /// Indicates whether local sending is enabled.
        /// </summary>
        public bool IsLocalSendingEnabled { get; private set; }
        /// <summary>
        /// Indicates whether local receiving is enabled.
        /// </summary>
        public bool IsLocalReceivingEnabled { get; private set; }
        /// <summary>
        /// This is valid only before matching. If we want to switch OpenVC and PTT after matching, call ToggleLocalUserSending() ourself.
        /// </summary>
        [Header("*This is old. VCMode is new. <br>If true, all audio is transmitted. If false, push to take."), System.Obsolete("This is old. VCMode is new one.")]
        public bool UseOpenVC;

        [SerializeField] VoiceChatMode _voiceChatMode;
        /// <summary>
        /// Change VC mode of this Manager.
        /// </summary>
        //Implemented as a property in case internal processing needs to be adjusted ｗhen changed via script in the future.
        public VoiceChatMode VoiceChatMode { 
            get { return _voiceChatMode; } 
            set { _voiceChatMode = value; }
        }
#if ENABLE_INPUT_SYSTEM
        public Key KeyToPushToTalk = Key.Space;
#else
        public KeyCode KeyToPushToTalk = KeyCode.Space;
#endif
        /// <summary>
        /// When using RTC, call OnComplete of Create and Join.
        /// </summary>
        internal void AddNotifyParticipantStatusChanged(){      
            if(!IsVoiceChatEnabled){
                return;
            }
            Logger.Log("AddNotifyParticipantStatusChanged", "Add Notify for RTC Room.");
            
            rtcInterface = EOSManager.Instance.GetEOSRTCInterface();
            audioInterface = rtcInterface.GetAudioInterface();

            //Validation of RTC room
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();

            IsRTCRoomConnectedOptions isConnectedOptions = new IsRTCRoomConnectedOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };
            Result result = (Result)lobbyInterface.IsRTCRoomConnected(ref isConnectedOptions, out bool isConnected);
            if (result != Result.Success){
                Logger.LogError("AddNotifyParticipantStatusChanged", "This user is not participating in the RTC room.", result);
                return;
            }
            //IsRTCRoomConnected maybe have bug. isConnected is not true.
            CurrentLobby.hasConnectedRTCRoom = true;
            CurrentLobby.RTCRoomName = GetRTCRoomName();

            //Check audio devices
            var audioOptions = new GetAudioInputDevicesCountOptions();
            audioInterface.GetAudioInputDevicesCount(ref audioOptions);

            var audioOutputOptions = new GetAudioOutputDevicesCountOptions();
            audioInterface.GetAudioOutputDevicesCount(ref audioOutputOptions);
            //Stop sending VC
            ToggleLocalUserSending(false).Forget();

            if(ParticipantStatusId == 0){
                // Notify to get a user's joining and leaving
                AddNotifyParticipantStatusChangedOptions StatusChangedOptions = new AddNotifyParticipantStatusChangedOptions(){
                    LocalUserId = EOSManager.Instance.GetProductUserId(),
                    RoomName = CurrentLobby.RTCRoomName
                };
                ParticipantStatusId = rtcInterface.AddNotifyParticipantStatusChanged(ref StatusChangedOptions, null, OnParticipantStatusChanged);

                if(ParticipantStatusId == 0){
                    Logger.LogError("AddNotifyParticipantStatusChanged", "RTCRoomParticipantUpdate can not be regisered.");
                }
            }
            string GetRTCRoomName(){
                GetRTCRoomNameOptions options = new GetRTCRoomNameOptions(){
                    LobbyId = CurrentLobby.LobbyId,
                    LocalUserId = EOSManager.Instance.GetProductUserId()
                };
                var lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
                Result result = (Result)lobbyInterface.GetRTCRoomName(ref options, out Utf8String roomName);

                if(result != Result.Success){
                    Logger.LogError("GetRTCRoomName", "Could not get Room Name.", result);
                    return string.Empty;
                }
                return roomName;
            }
        }
        /// <summary>
        /// Notifications when a participant's status changes (oin or leave the room), or when the participant is added or removed from an applicable block list.
        /// </summary>
        /// <param name="data"></param>
        void OnParticipantStatusChanged(ref ParticipantStatusChangedCallbackInfo data){
            if (CurrentLobby.RTCRoomName != data.RoomName){
                Logger.LogError("OnParticipantStatusChanged", "This data is not current RTC room data.");
                return;
            }
            if(data.ParticipantStatus == RTCParticipantStatus.Left){
                return;
            }
            MemberState member = CurrentLobby.Members[UserId.GetUserId(data.ParticipantId).ToString()];

            if (data.ParticipantStatus == RTCParticipantStatus.Joined){
                member.RTCState.IsInRTCRoom = true;
            }else{
                member.RTCState.IsInRTCRoom = false;
                member.RTCState.IsSpeakinging = false;
            }
        }
        /// <summary>
        /// Register event to get VC status, then start VC.<br />
        /// Must call this to use after Created ot Join Lobby.
        /// </summary>
        internal void AddNotifyParticipantUpdated(){
            if(!IsVoiceChatEnabled){
                return;
            }
            if(!CurrentLobby.hasConnectedRTCRoom){
                Logger.LogError("AddNotifyParticipantUpdated", "This user has failed to join RTCRoom.");
                return;
            }
            
            Logger.Log("AddNotifyParticipantUpdated", "Add Notifiy for RTC Room.");
            //Notify to get user Speaking status
            if(ParticipantUpdatedId == 0){
                AddNotifyParticipantUpdatedOptions addNotifyParticipantUpdatedOptions = new AddNotifyParticipantUpdatedOptions(){
                    LocalUserId = EOSManager.Instance.GetProductUserId(),
                    RoomName = CurrentLobby.RTCRoomName
                };
                ParticipantUpdatedId = audioInterface.AddNotifyParticipantUpdated(ref addNotifyParticipantUpdatedOptions, null, OnParticipantUpdated);
                if(ParticipantUpdatedId == 0){
                    Logger.LogError("AddNotifyParticipantUpdated", "can not be regisered.");
                    return;
                }
            }
        }
        /// <summary>
        /// Notifications when a room participant audio status is updated.
        /// </summary>
        /// <param name="info"></param>
        void OnParticipantUpdated(ref ParticipantUpdatedCallbackInfo info){
            if (CurrentLobby.RTCRoomName != info.RoomName){
                Logger.LogError("OnParticipantUpdate", "This data is not current RTC room data.");
                return;
            }
            Logger.Log("OnParticipantUpdate", $"Change Paticipant State. UserId: {UserId.GetUserId(info.ParticipantId).ToMaskedString()} IsSpeaking: {info.Speaking}");

            MemberState member = CurrentLobby.Members[UserId.GetUserId(info.ParticipantId).ToString()];
            member.RTCState.IsSpeakinging = info.Speaking;
            member.RTCState.IsAudioOutputEnabled = info.AudioStatus == RTCAudioStatus.Enabled;

            if(info.Speaking){
                ParticipantUpdatedNotifier.StartSpeaking(UserId.GetUserId(info.ParticipantId));
            }else{
                ParticipantUpdatedNotifier.StopSpeaking(UserId.GetUserId(info.ParticipantId));
            }
        }
        /// <summary>
        /// Call this close or leave lobby.
        /// </summary>
        internal void RemoveRTCEvents(){
            if(!CurrentLobby.bEnableRTCRoom){
                return;
            }
            if(ParticipantStatusId != 0){
                rtcInterface.RemoveNotifyParticipantStatusChanged(ParticipantStatusId);
                Logger.Log("RemoveRTCEvents", "Remove ParticipantStatusChanged.");
            }
            if(ParticipantUpdatedId != 0){
                rtcInterface.GetAudioInterface().RemoveNotifyParticipantUpdated(ParticipantUpdatedId);
                Logger.Log("RemoveRTCEvents", "Remove NotifyParticipantUpdated");
            }

            IsLocalSendingEnabled = false;
            IsLocalSendingEnabled = false;
        }
    #region Audio Send and Receive
        /// <summary>
        /// Starts local user sending voice chat. 
        /// </summary>
        public async UniTask<Result> StartVoiceSending(){
            if(!IsVoiceChatEnabled){
                Logger.LogWarning("StartVoiceSending", "This lobby doesn't have RTC room.");
                return Result.InvalidAPICall;
            }
            Logger.Log("StartVoiceSending", $"Start VoiceChat. VCMode: {VoiceChatMode}");
            //When PushToTalk is enabled, the VC is not turned on unless the button is pressed.
            if(VoiceChatMode == VoiceChatMode.PushToTalk){
                StartAcceptingToPushToTalk();
                return Result.Success;
            }
            return await ToggleLocalUserSending(true);
        }
        /// <summary>
        /// Stop local user sending voice chat. (= Mute) <br />
        /// </summary>
        public async UniTask<Result> StopVoiceSending(){
            if(!IsVoiceChatEnabled){
                Logger.LogWarning("StopVoiceSending", "This lobby doesn't have RTC room.");
                return Result.InvalidAPICall;
            }
            Logger.Log("StopVoiceSending", "Stop VoiceChat.");
            if(VoiceChatMode == VoiceChatMode.PushToTalk){
                StopAcceptingToPushToTalk();
            }
            return await ToggleLocalUserSending(false);
        }
        /// <summary>
        /// Switch Input setting of Local user sending on this Session.<br />
        /// We should StartVoiceSending() and StopVoiceSending() instead of this. This is Low API. 
        /// </summary>
        /// <param name="isEnable">If true, send VC. If false, stop VC.</param>
        public async UniTask<Result> ToggleLocalUserSending(bool isEnable){
            if(!IsVoiceChatEnabled){
                Logger.LogWarning("ToggleLocalUserSending", "This local does not currently have an RTCRoom.");
                return Result.InvalidAPICall;
            }
            var sendingOptions = new UpdateSendingOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                RoomName = CurrentLobby.RTCRoomName,
                AudioStatus = isEnable ? RTCAudioStatus.Enabled : RTCAudioStatus.Disabled
            };

            Result result = Result.None;
            audioInterface.UpdateSending(ref sendingOptions, null, OnUpdateSending);

            await UniTask.WaitUntil(() => result != Result.None, cancellationToken: this.GetCancellationTokenOnDestroy());
            return result;

            void OnUpdateSending(ref UpdateSendingCallbackInfo info){
                if((Result)info.ResultCode != Result.Success){
                    Logger.LogError("OnUpdateSending", "could not toggle mute setting of local user sending.", (Result)info.ResultCode);
                    result = (Result)info.ResultCode;
                    return;
                }
                if(info.AudioStatus != RTCAudioStatus.Enabled){
                    ParticipantUpdatedNotifier.StopSpeaking(p2pInfo.Instance.userIds.LocalUserId);
                }

                IsLocalSendingEnabled = info.AudioStatus == RTCAudioStatus.Enabled;
                Logger.Log("OnUpdateSending", $"the toggle is successful. Status: {info.AudioStatus}");
                result = (Result)info.ResultCode;
            }
        }
        /// <summary>
        /// Switch Output setting(Enable or Mute) of receiving from target user on this Session.
        /// </summary>
        /// <param name="targetId">If null, effect to all remote users</param>
        /// <param name="isEnable">If true, receive vc from target. If false, mute target.</param>
        public async UniTask<Result> ToggleReceiveingFromTarget(UserId targetId, bool isEnable){
            if(!IsVoiceChatEnabled){
                Logger.LogWarning("ToggleReceiveingFromTargetUser", "This local does not currently have an RTCRoom.");
                return Result.InvalidAPICall;
            }
            var receiveOptions = new UpdateReceivingOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                RoomName = CurrentLobby.RTCRoomName,
                ParticipantId = targetId == null ? null : targetId.AsEpic,
                AudioEnabled = isEnable
            };

            Result result = Result.None;
            audioInterface.UpdateReceiving(ref receiveOptions, null, OnUpdateReceiving);

            await UniTask.WaitUntil(() => result != Result.None, cancellationToken: this.GetCancellationTokenOnDestroy());
            return result;

            void OnUpdateReceiving(ref UpdateReceivingCallbackInfo info){
                if((Result)info.ResultCode != Result.Success){
                    Logger.LogError("OnUpdateReceiving" ,"could not toggle setting.", (Result)info.ResultCode);
                    result = (Result)info.ResultCode;
                    return;
                }
                if(info.ParticipantId == null){
                    foreach(var member in CurrentLobby.Members){
                        member.Value.RTCState.IsLocalMute = !info.AudioEnabled;
                    }
                    IsLocalReceivingEnabled = info.AudioEnabled;
                }else{
                    CurrentLobby.Members[UserId.GetUserId(info.ParticipantId).ToString()].RTCState.IsLocalMute = !info.AudioEnabled;
                }
            #if SYNICSUGAR_LOG
                string target = info.ParticipantId == null ? "All remote users" : UserId.GetUserId(info.ParticipantId).ToMaskedString();
                Logger.Log("OnUpdateReceiving", $"the toggle is successful. CurrentStatus: {info.AudioEnabled} / Target: {target}");
            #endif
                result = (Result)info.ResultCode;
            }
        }
        /// <summary>
        /// Change the receiving volume on this Session.
        /// </summary>
        /// <param name="targetId">If null, effect to all remote users</param>
        /// <param name="volume">Range 0.0 - 100. 50 means that the audio volume is not modified and stays in its source value.</param>
        public async UniTask<Result> UpdateReceiveingVolumeFromTarget(UserId targetId, float volume){
            if(!IsVoiceChatEnabled){
                Logger.LogWarning("ToggleReceiveingFromTargetUser", "This local does not currently have an RTCRoom.");
                return Result.InvalidAPICall;
            }
            volume = System.Math.Clamp(volume, 0f, 100f);
            var receiveOptions = new UpdateParticipantVolumeOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                RoomName = CurrentLobby.RTCRoomName,
                ParticipantId = targetId == null ? null : targetId.AsEpic,
                Volume = volume
            };
            
            Result result = Result.None;
            audioInterface.UpdateParticipantVolume(ref receiveOptions, null, OnUpdateParticipantVolume);

            await UniTask.WaitUntil(() => result != Result.None, cancellationToken: this.GetCancellationTokenOnDestroy());
            return result;

            void OnUpdateParticipantVolume(ref UpdateParticipantVolumeCallbackInfo info){
                if((Result)info.ResultCode != Result.Success){
                    Logger.LogError("OnUpdateParticipantVolume", "could not toggle setting.", (Result)info.ResultCode);
                    result = (Result)info.ResultCode;
                    return;
                }
                if(info.ParticipantId == null){
                    foreach(var member in CurrentLobby.Members){
                        member.Value.RTCState.LocalOutputedVolume = info.Volume;
                    }
                }else{
                    CurrentLobby.Members[UserId.GetUserId(info.ParticipantId).ToString()].RTCState.LocalOutputedVolume =  info.Volume;
                }
            #if SYNICSUGAR_LOG
                string target = info.ParticipantId == null ? "All remote users" : UserId.GetUserId(info.ParticipantId).ToMaskedString();
                Logger.Log("OnUpdateParticipantVolume", $"volume change is successful. Target: {target} / Volume:{info.Volume}");
            #endif
                result = (Result)info.ResultCode;
            }
        }
        /// <summary>
        /// Host user mutes target user('s input). The target can't speak but can hear other members of the lobby.
        /// </summary>
        /// <param name="target">Muted Target</param>
        public async UniTask<Result> HardMuteTargetUser(UserId target, bool isMuted){
            if(!IsVoiceChatEnabled){
                Logger.LogWarning("HardMuteTargetUser", "This local does not currently have an RTCRoom.");
                return Result.InvalidAPICall;
            }
            if(!CurrentLobby.isHost()){
                Logger.LogWarning("HardMuteTargetUser", "This user is not Host and cannot hard-mute other users.");
                return Result.InvalidAPICall;
            }
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            var muteOptions = new HardMuteMemberOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                TargetUserId = target.AsEpic,
                HardMute = isMuted
            };

            Result result = Result.None;
            lobbyInterface.HardMuteMember(ref muteOptions, null, OnHardMuteMember);

            await UniTask.WaitUntil(() => result != Result.None, cancellationToken: this.GetCancellationTokenOnDestroy());
            return result;

            void OnHardMuteMember(ref HardMuteMemberCallbackInfo info){         
                if((Result)info.ResultCode != Result.Success){
                    Logger.LogError("OnHardMuteMember", "could not mute target.", (Result)info.ResultCode);
                    result = (Result)info.ResultCode;
                    return;
                }

                CurrentLobby.Members[target.ToString()].RTCState.IsHardMuted = isMuted;
                Logger.Log("OnHardMuteMember", $"hard mute is successful. target: {UserId.GetUserId(info.TargetUserId).ToMaskedString()}");
                result = (Result)info.ResultCode;
            }
        }
        /// <summary>
        /// Start to accept PushToTalk key. <br />
        /// This switches the sending state with ToggleLocalUserSending() on user's input automatically.
        /// </summary>
        void StartAcceptingToPushToTalk(){
            CancelPTTToken();
            pttToken = new();
            PushToTalkLoop(pttToken.Token).Forget();
        }
        /// <summary>
        /// Stop to accept PushToTalk key.
        /// </summary>
        void StopAcceptingToPushToTalk(){
            CancelPTTToken();
            DisposePTTToken();
        }
        /// <summary>
        /// Switch sending state by UniTask. 
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask PushToTalkLoop(CancellationToken token){
            while(!token.IsCancellationRequested){
#if ENABLE_INPUT_SYSTEM
                await UniTask.WaitUntil(() => Keyboard.current[KeyToPushToTalk].wasPressedThisFrame, cancellationToken: token);
                await ToggleLocalUserSending(true);
                await UniTask.WaitUntil(() => Keyboard.current[KeyToPushToTalk].wasReleasedThisFrame, cancellationToken: token);
                await ToggleLocalUserSending(false);
#else
                await UniTask.WaitUntil(() => Input.GetKeyDown(KeyToPushToTalk), cancellationToken: token);
                await ToggleLocalUserSending(true);
                await UniTask.WaitUntil(() => Input.GetKeyUp(KeyToPushToTalk), cancellationToken: token);
                await ToggleLocalUserSending(false);
#endif
            }
        }
    #endregion
        public UserId LastStateUpdatedUserId { get { return ParticipantUpdatedNotifier.TargetId;} } 
        /// <summary>
        /// This is outputed volume on this local. We don't know target local setting.
        /// </summary>
        /// <param name="target"></param>
        /// <returns>0-100. 50 means source volume.</returns>
        public float TargetOutputedVolumeOnLocal(UserId target) { 
            return CurrentLobby.Members[target.ToString()].RTCState.LocalOutputedVolume;
        }
        /// <summary>
        /// This is outputed volume on this local. We don't know target local setting.
        /// </summary>
        /// <param name="target"></param>
        /// <returns>0-100. 50 means source volume.</returns>
        public bool TryGetTargetOutputedVolumeOnLocal(UserId target, out float volume){
            if (CurrentLobby.Members.TryGetValue(target.ToString(), out var member)){
                volume = member.RTCState.LocalOutputedVolume;
                return true;
            }
            volume = 0f;
            return false;
        }
        /// <summary>
        /// Has this local muted target user?
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool IsTargetMutedOnLocal(UserId target) { 
            return CurrentLobby.Members[target.ToString()].RTCState.IsLocalMute;
        }
        /// <summary>
        /// Has this local muted target user?
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool TryGetIsTargetMutedOnLocal(UserId target, out bool isMuted){
            if (CurrentLobby.Members.TryGetValue(target.ToString(), out var member)){
                isMuted = member.RTCState.IsLocalMute;
                return true;
            }
            isMuted = false;
            return false;
        }

        /// <summary>
        /// Has this hard muted target user by this local Host?<br />
        /// This is valid only for Lobby Host.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool IsTargetHardMuted(UserId target) { 
            return CurrentLobby.Members[target.ToString()].RTCState.IsHardMuted;
        }
        /// <summary>
        /// Has this hard muted target user by this local Host?<br />
        /// This is valid only for Lobby Host.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool TryGetIsTargetHardMuted(UserId target, out bool isMuted){
            if (CurrentLobby.Members.TryGetValue(target.ToString(), out var member)){
                isMuted = member.RTCState.IsHardMuted;
                return true;
            }
            isMuted = false;
            return false;
        }

    #region PTTToken
        void DisposePTTToken(){
            if (pttToken == null) { return; }
            pttToken.Dispose();
            pttToken = null;
        }
        void CancelPTTToken(){
            if (pttToken == null) { return; }
            pttToken.Cancel();
        }
    #endregion
    }
}

