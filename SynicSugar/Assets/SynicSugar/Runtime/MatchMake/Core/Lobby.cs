using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using System.Collections.Generic;
using SynicSugar.RTC;
using ResultE = Epic.OnlineServices.Result;

namespace SynicSugar.MatchMake {
    public class Lobby {
        public Lobby (){}
        internal string LobbyId;
        internal ProductUserId LobbyOwner = new ProductUserId();
        //Basis
        uint _maxLobbyMembers;
        public uint MaxLobbyMembers { //2-64
            get { return _maxLobbyMembers;}
            set {
                if(value < 2){
                    _maxLobbyMembers = 2;
                }else if(value > 64){
                    _maxLobbyMembers = 64;
                }else{
                    _maxLobbyMembers = value;
                }
            }
        }
        internal LobbyPermissionLevel PermissionLevel = LobbyPermissionLevel.Publicadvertised;
        public string BucketId = string.Empty;
        internal bool bAllowInvites = false;
        internal bool bDisableHostMigration = true;
        internal bool bEnableRTCRoom = false;
        /// <summary>
        /// If true, the pleyer who was kicked need Invite to join the same Lobby.
        /// </summary>
        public bool RejoinAfterKickRequiresInvite = false;
        public List<AttributeData> Attributes = new List<AttributeData>();
        internal void SetBucketID(string[] conditions){
            BucketId = string.Empty;
            if(conditions.Length == 0){
                BucketId = "NONE";
                return;
            }
            for(int i = 0; i < conditions.Length; i++){
                BucketId += i == 0 ? conditions[i] : (":" + conditions[i]);
            }
        }
 
        internal Dictionary<string, MemberState> Members = new();

        // Return True only when create Lobby. Otherwise, return False because this is re-created by constructor.
        internal bool _BeingCreated = false;

        #region RTC
        internal string RTCRoomName = string.Empty;
        internal bool hasConnectedRTCRoom = false;
        //for joing or leaving
        internal NotifyEventHandle RTCParticipantStatusChanged; 
        //for speaking or non-speaking
        internal NotifyEventHandle RTCParticipantUpdated;
        #endregion

        /// <summary>
        /// Copies the Lobby's permission and settings attributes.
        /// </summary>
        public void CopySettings(Lobby otherLobby) {
            MaxLobbyMembers = otherLobby.MaxLobbyMembers;
            PermissionLevel = otherLobby.PermissionLevel;
            BucketId = otherLobby.BucketId;
            bAllowInvites = otherLobby.bAllowInvites;
            bDisableHostMigration = otherLobby.bDisableHostMigration;
            bEnableRTCRoom = otherLobby.bEnableRTCRoom;
            RejoinAfterKickRequiresInvite = otherLobby.RejoinAfterKickRequiresInvite;
            Attributes = new List<AttributeData>(otherLobby.Attributes);
        }
        /// <summary>
        /// Copies the Lobby's permission and settings attributes from LobbyCondition for OfflineMode.
        /// </summary>
        /// <param name="otherLobby"></param>
        /// <param name="userAttributes"></param>
        internal void InitializeOfflineLobby(Lobby otherLobby, List<AttributeData> userAttributes){
            BucketId = otherLobby.BucketId;
            Attributes = new List<AttributeData>(otherLobby.Attributes);
            
            LobbyId = "OFFLINEMODE";
            LobbyOwner = EOSManager.Instance.GetProductUserId();

            MaxLobbyMembers = 1;
            PermissionLevel = LobbyPermissionLevel.Inviteonly;
            bAllowInvites = false;
            bDisableHostMigration = false;
            bEnableRTCRoom = false;
            RejoinAfterKickRequiresInvite = false;
            hasConnectedRTCRoom = false;

            Members.Add(SynicSugarManger.Instance.LocalUserId.ToString(), new MemberState() { Attributes = userAttributes });
        }

        /// <summary>
        /// Checks if Lobby Id is valid
        /// </summary>
        /// <returns>True if valid</returns>
        internal bool isValid(){
            return !string.IsNullOrEmpty(LobbyId);
        }

        /// <summary>
        /// Checks the local player is the lobby host or not.
        /// </summary>
        /// <returns>If true, the local user is lobby's Host</returns>
        internal bool isHost(){
            return EOSManager.Instance.GetProductUserId() == LobbyOwner;
        }

        /// <summary>
        /// Clears local cache of Lobby Id, owner, attributes and members
        /// </summary>
        internal void Clear(){
            //Need to remove RTC events before reset bEnableRTCRoom.
            ResetRTCData();

            LobbyId = string.Empty;
            LobbyOwner = new ProductUserId();
            Members.Clear();
            _maxLobbyMembers = 0;
            
            _BeingCreated = false;
            PermissionLevel = LobbyPermissionLevel.Publicadvertised;
            BucketId = string.Empty;
            bAllowInvites = false;
            bDisableHostMigration = true;
            bEnableRTCRoom = false; //This is also a variable related to RTC, but this is not a variable related to RTCRoom, but a variable related to Lobby.
            RejoinAfterKickRequiresInvite = false;
            
            Attributes.Clear();
        }

        void ResetRTCData(){
            RTCManager.Instance.RemoveRTCEvents();
            RTCRoomName = string.Empty;
            hasConnectedRTCRoom = false;
        }

        /// <summary>
        /// Initializing the given Lobby Id and caches all relevant attributes
        /// </summary>
        /// <param name="lobbyId">Specified Lobby Id</param>
        internal void InitFromLobbyHandle(string lobbyId){
            if (string.IsNullOrEmpty(lobbyId)){
                return;
            }

            LobbyId = lobbyId;

            CopyLobbyDetailsHandleOptions options = new CopyLobbyDetailsHandleOptions(){
                LobbyId = LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };

            ResultE result = EOSManager.Instance.GetEOSLobbyInterface().CopyLobbyDetailsHandle(ref options, out LobbyDetails outLobbyDetailsHandle);
            if (result != ResultE.Success){
                Logger.LogError("InitFromLobbyHandle", "Ican't get lobby info handle.", (Result)result);
                return;
            }
            if (outLobbyDetailsHandle == null){
                Logger.LogError("InitFromLobbyHandle", "can't get lobby info handle. outLobbyDetailsHandle is null");
                return;
            }

            UpdateLobbyWithLatestLobbyDetails(outLobbyDetailsHandle);
            outLobbyDetailsHandle.Release();
        }
        /// <summary>
        /// Initializing the given LobbyDetails handle and caches all relevant attributes
        /// </summary>
        /// <param name="lobbyDetailsHandle">Specified LobbyDetails handle</param>
        void UpdateLobbyWithLatestLobbyDetails(LobbyDetails lobbyDetailsHandle){
            // Get owner
            var lobbyDetailsGetLobbyOwnerOptions = new LobbyDetailsGetLobbyOwnerOptions();
            ProductUserId newLobbyOwner = lobbyDetailsHandle.GetLobbyOwner(ref lobbyDetailsGetLobbyOwnerOptions);
            if (newLobbyOwner != LobbyOwner){
                LobbyOwner = newLobbyOwner;
            }

            // Copy lobby info
            var lobbyDetailsCopyInfoOptions = new LobbyDetailsCopyInfoOptions();
            ResultE infoResult = lobbyDetailsHandle.CopyInfo(ref lobbyDetailsCopyInfoOptions, out LobbyDetailsInfo? outLobbyDetailsInfo);
            if (infoResult != ResultE.Success){
                Logger.LogError("UpdateLobbyWithLatestLobbyDetails", "Can't copy lobby info.", (Result)infoResult);
                return;
            }
            if (outLobbyDetailsInfo == null){
                Logger.LogError("UpdateLobbyWithLatestLobbyDetails", "Could not copy info: outLobbyDetailsInfo is null.");
                return;
            }

            MaxLobbyMembers = (uint)(outLobbyDetailsInfo?.MaxMembers);
            PermissionLevel = (LobbyPermissionLevel)(outLobbyDetailsInfo?.PermissionLevel);
            BucketId = outLobbyDetailsInfo?.BucketId;
            bAllowInvites = (bool)(outLobbyDetailsInfo?.AllowInvites);
            bEnableRTCRoom = (bool)(outLobbyDetailsInfo?.RTCRoomEnabled);
            bDisableHostMigration = (bool)(outLobbyDetailsInfo?.AllowHostMigration);
            RejoinAfterKickRequiresInvite = (bool)(outLobbyDetailsInfo?.RejoinAfterKickRequiresInvite);

            Logger.Log("UpdateLobbyWithLatestLobbyDetails", $"Update Lobby Data. {System.Environment.NewLine} MaxLobbyMembers {MaxLobbyMembers} / PermissionLevel {PermissionLevel} / BucketId {BucketId} / AllowInvites {bAllowInvites} / RTCRoomEnabled {bEnableRTCRoom} / AllowHostMigration {bDisableHostMigration} / RejoinAfterKickRequiresInvite {RejoinAfterKickRequiresInvite}");

            // Get attributes
            Attributes.Clear();
            var lobbyDetailsGetAttributeCountOptions = new LobbyDetailsGetAttributeCountOptions();
            uint attrCount = lobbyDetailsHandle.GetAttributeCount(ref lobbyDetailsGetAttributeCountOptions);
            for (uint i = 0; i < attrCount; i++){
                LobbyDetailsCopyAttributeByIndexOptions attrOptions = new LobbyDetailsCopyAttributeByIndexOptions(){ AttrIndex = i };
                ResultE copyAttrResult = lobbyDetailsHandle.CopyAttributeByIndex(ref attrOptions, out Attribute? outAttribute);
                if (copyAttrResult == ResultE.Success && outAttribute != null && outAttribute?.Data != null){
                    Attributes.Add(EOSLobbyExtensions.GenerateLobbyAttribute(outAttribute));
                }
            }

            // Get members
            Members.Clear();
            var lobbyDetailsGetMemberCountOptions = new LobbyDetailsGetMemberCountOptions();
            uint memberCount = lobbyDetailsHandle.GetMemberCount(ref lobbyDetailsGetMemberCountOptions);
            for (int i = 0; i < memberCount; i++){
                var lobbyDetailsGetMemberByIndexOptions = new LobbyDetailsGetMemberByIndexOptions() { MemberIndex = (uint)i };
                ProductUserId memberId = lobbyDetailsHandle.GetMemberByIndex(ref lobbyDetailsGetMemberByIndexOptions);
                Members.Add(UserId.GetUserId(memberId).ToString(), new MemberState(){});

                // Add member attributes
                var lobbyDetailsGetMemberAttributeCountOptions = new LobbyDetailsGetMemberAttributeCountOptions() { TargetUserId = memberId };
                int memberAttributeCount = (int)lobbyDetailsHandle.GetMemberAttributeCount(ref lobbyDetailsGetMemberAttributeCountOptions);

                for (int attributeIndex = 0; attributeIndex < memberAttributeCount; attributeIndex++){
                    var lobbyDetailsCopyMemberAttributeByIndexOptions = new LobbyDetailsCopyMemberAttributeByIndexOptions() { AttrIndex = (uint)attributeIndex, TargetUserId = memberId };
                    ResultE memberAttributeResult = lobbyDetailsHandle.CopyMemberAttributeByIndex(ref lobbyDetailsCopyMemberAttributeByIndexOptions, out Attribute? outAttribute);

                    if (memberAttributeResult != ResultE.Success){
                        Logger.Log("UpdateLobbyWithLatestLobbyDetails", "Can't copy member attribute.", (Result)memberAttributeResult);
                        continue;
                    }
 
                    Members[UserId.GetUserId(memberId).ToString()].Attributes.Add(EOSLobbyExtensions.GenerateLobbyAttribute(outAttribute));
                }
            }
        }
    }
}