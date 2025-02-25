using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SynicSugar.P2P;
using SynicSugar.RTC;
using SynicSugar.Base;
using ResultE = Epic.OnlineServices.Result;

namespace SynicSugar.MatchMake {
    internal class EOSMatchmaking : MatchmakingCore {
        readonly Lobby CurrentLobby = new Lobby();
        //User config
        bool useManualFinishMatchMake;
        uint requiredMembers;
        //Notification
        /// <summary>
        /// Join, Leave, Kicked, Promote or so on...
        /// </summary>
        ulong LobbyMemberStatusNotifyId;
        /// <summary>
        /// Lobby attributes
        /// </summary>
        ulong LobbyUpdateNotififyId;
        /// <summary>
        /// Member attributes.
        /// </summary>
        ulong LobbyMemberUpdateNotifyId;

        /// <summary>
        /// For WaitForMatchingEstablishment(). Change this in notify.
        /// </summary>
        bool isMatchmakingCompleted;
        /// <summary>
        /// For WaitForMatchingEstablishment(). Change this in notify.
        /// </summary>
        Result matchingResult;
        UserId localUserId;

        public override bool isHost(){
            return localUserId == UserId.GetUserId(CurrentLobby.LobbyOwner);
        }
        public override bool isLocalUser(UserId userId){
            return userId == localUserId;
        }

        public EOSMatchmaking(uint maxSearch) : base(maxSearch) {
            localUserId = SynicSugarManger.Instance.LocalUserId;
            RTCManager.Instance.SetLobbyReference(CurrentLobby);
        }

        /// <summary>
        /// Search for lobbies in backend and join in one to meet conditions.<br />
        /// When player could not join, they create lobby as host and wait for other player.
        /// </summary>
        /// <param name="lobbyCondition">Create and search condition. <c>MUST NOT</c> add the data not to open public.</param>
        /// <param name="token"></param>
        /// <param name="userAttributes"></param>
        /// <param name="minLobbyMember"></param>
        /// <returns></returns>
        public override async UniTask<Result> StartMatching(Lobby lobbyCondition, List<AttributeData> userAttributes, uint minLobbyMember, CancellationToken token){
            useManualFinishMatchMake = minLobbyMember > 0;
            requiredMembers = minLobbyMember;
            //Serach
            Result joinResult = await JoinExistingLobby(lobbyCondition, userAttributes, token);

            if(joinResult == Result.Success){
                // Wait for establised matchmaking and to get SocketName to be used in p2p connection.
                Result matchingResult = await WaitForMatchingEstablishment(token);
                
                return matchingResult;
            }
            //If player cannot join lobby as a guest, creates a lobby as a host and waits for other player.
            //Create
            Result createResult = await CreateLobby(lobbyCondition, userAttributes, token);

            if(createResult == Result.Success){
                // Wait for establised matchmaking and to get SocketName to be used in p2p connection.
                Result matchingResult = await WaitForMatchingEstablishment(token);

                return matchingResult;
            }
            //Failed due to no-playing-user or server problems.
            return createResult;
        }
        /// <summary>
        /// Just search Lobby<br />
        /// Recommend: StartMatching()
        /// </summary>
        /// <param name="lobbyCondition">Search condition. <c>MUST NOT</c> add the data not to open public.</param>
        /// <param name="token"></param>
        /// <param name="userAttributes"></param>
        /// <param name="minLobbyMember"></param>
        /// <returns>True on success. If false, EOS backend have something problem. So, when you call this process again, should wait for some time.</returns>
        public override async UniTask<Result> StartJustSearch(Lobby lobbyCondition, List<AttributeData> userAttributes, uint minLobbyMember, CancellationToken token){
            //For host migration
            useManualFinishMatchMake = minLobbyMember > 0;
            requiredMembers = minLobbyMember;
            //Serach
            Result joinResult = await JoinExistingLobby(lobbyCondition, userAttributes, token);

            if(joinResult == Result.Success){
                // Wait for establised matchmaking and to get SocketName to be used in p2p connection.
                Result matchingResult = await WaitForMatchingEstablishment(token);
                
                return matchingResult;
            }
            //This is NOT Success. Failed due to no-playing-user or server problems.
            return joinResult;
        }
        /// <summary>
        /// Create lobby as host<br />
        /// Recommend: StartMatching()
        /// </summary>
        /// <param name="lobbyCondition">Create and search condition. <c>MUST NOT</c> add the data not to open public.</param>
        /// <param name="token"></param>
        /// <param name="userAttributes"></param>
        /// <param name="minLobbyMember"></param>
        /// <returns>True on success. If false, EOS backend have something problem. So, when you call this process again, should wait for some time.</returns>
        public override async UniTask<Result> StartJustCreate(Lobby lobbyCondition, List<AttributeData> userAttributes, uint minLobbyMember, CancellationToken token){
            useManualFinishMatchMake = minLobbyMember > 0;
            requiredMembers = minLobbyMember;

            Result createResult = await CreateLobby(lobbyCondition, userAttributes, token);

            if(createResult == Result.Success){
                // Wait for establised matchmaking and to get SocketName to be used in p2p connection.
                Result matchingResult = await WaitForMatchingEstablishment(token);
                
                return matchingResult;
            }
            //This is NOT Success. Failed due to no-playing-user or server problems.
            return createResult;
        }
        /// <summary>
        /// Join the Lobby with specific id to that lobby. <br />
        /// To return to a disconnected lobby.
        /// </summary>
        /// <param name="LobbyID">Lobby ID to <c>re</c>-connect</param>
        /// <param name="token"></param>
        public override async UniTask<Result> JoinLobbyBySavedLobbyId(string LobbyID, CancellationToken token){
            //Search
            var retrieveResult = await RetriveLobbyByLobbyId(LobbyID, token);

            if(retrieveResult.result != Result.Success){
                await MatchMakeManager.Instance.OnDeleteLobbyID();
                ReleaseLobbySearch(retrieveResult.lobbySerach);
                return retrieveResult.result; //This is NOT Success. Can't retrive Lobby data from EOS.
            }

            //Join when lobby has members than more one.
            Result joinResult = await TryJoinSearchResults(retrieveResult.lobbySerach, null, true, token);

            if(joinResult != Result.Success){
                await MatchMakeManager.Instance.OnDeleteLobbyID();
                return joinResult; //This is NOT Success. The lobby was already closed.
            }

            return Result.Success;
        }
//Host
#region Create
        /// <summary>
        /// Create a Lobby. <br />
        /// *<c>StartMatching()</c> can search for an existing lobby and create one if there isn't.
        /// </summary>
        /// <param name="lobbyCondition"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<Result> CreateLobby(Lobby lobbyCondition, List<AttributeData> userAttributes, CancellationToken token){
            // Check if there is current session. Leave it.
            if (CurrentLobby.isValid()){
                Logger.Log("Create Lobby", $"Leaving Current Lobby. LobbyId: {CurrentLobby.LobbyId.ToMaskedString()}");
                await LeaveLobby(token);
            }

            //Lobby Option
            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                MaxLobbyMembers = lobbyCondition.MaxLobbyMembers,
                PermissionLevel = LobbyPermissionLevel.Inviteonly,
                BucketId = lobbyCondition.BucketId,
                PresenceEnabled = false,
                RejoinAfterKickRequiresInvite = lobbyCondition.RejoinAfterKickRequiresInvite,
                AllowInvites = false,
                EnableRTCRoom = lobbyCondition.bEnableRTCRoom
            };

            // Init for async
            Result result = Result.None;
            bool finishCreated = false;
            CurrentLobby.Clear();

            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            //Set lobby data
            CurrentLobby._BeingCreated = true;
            CurrentLobby.LobbyOwner = EOSManager.Instance.GetProductUserId();

            lobbyInterface.CreateLobby(ref createLobbyOptions, null, OnCreateLobbyCompleted);

            await UniTask.WaitUntil(() => finishCreated, cancellationToken: token);

            if(result != Result.Success){
                Logger.LogError("Create Lobby", "Can't create new lobby.", result);
                return result;
            }
            //Need add condition for serach
            Result modifyAttribute = await AddSerachLobbyAttribute(lobbyCondition, userAttributes, token);

            if(modifyAttribute != Result.Success){
                Logger.LogError("Create Lobby", "Can't add lobby search attributes.");
                return modifyAttribute;
            }
            return modifyAttribute;

            void OnCreateLobbyCompleted(ref CreateLobbyCallbackInfo info){
                result = (Result)info.ResultCode;
                if (info.ResultCode != ResultE.Success){
                    Logger.LogError("Created Lobby", "Request failed.", (Result)info.ResultCode);
                    finishCreated = true;
                    return;
                }
                
                if (string.IsNullOrEmpty(info.LobbyId) || !CurrentLobby._BeingCreated){
                    result = Result.InvalidParameters;
                    Logger.LogError("Created Lobby", "Lobby initialization failed.", (Result)info.ResultCode);
                    finishCreated = true;
                    return;
                }
                CurrentLobby.InitFromLobbyHandle(info.LobbyId);

                // For the host migration
                AddNotifyLobbyMemberStatusReceived();
                // To get Member attributes
                AddNotifyLobbyMemberUpdateReceived();
                //For self
                MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(UserId.GetUserId(EOSManager.Instance.GetProductUserId()), true);
                //RTC
                RTCManager.Instance.AddNotifyParticipantStatusChanged();

                finishCreated = true;
            }
        }
        /// <summary>
        /// Set attribute for search and Host attributes. This process is only for Host player.
        /// </summary>
        /// <param name="lobbyCondition"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<Result> AddSerachLobbyAttribute(Lobby lobbyCondition, List<AttributeData> userAttributes,CancellationToken token){
            if (!CurrentLobby.isHost()){
                Logger.LogError("Change Lobby", "This user is not lobby owner.");
                return Result.InvalidAPICall;
            }

            UpdateLobbyModificationOptions options = new UpdateLobbyModificationOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };
            //Create modify handle
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            ResultE result = lobbyInterface.UpdateLobbyModification(ref options, out LobbyModification lobbyHandle);
            if (result != ResultE.Success){
                Logger.LogError("AddSerachLobbyAttribute", "Could not create lobby modification.", (Result)result);
                return (Result)result;
            }

            //Switch precense
            LobbyModificationSetPermissionLevelOptions permissionOptions = new LobbyModificationSetPermissionLevelOptions(){
                PermissionLevel = LobbyPermissionLevel.Publicadvertised
            };
            result = lobbyHandle.SetPermissionLevel(ref permissionOptions);

            if (result != ResultE.Success){
                Logger.LogError("AddSerachLobbyAttribute", "Could not switch permission level.", (Result)result);
                lobbyHandle.Release();
                return (Result)result;
            }
            //Set Backet ID
            LobbyModificationSetBucketIdOptions  bucketIdOptions = new(){
                BucketId = lobbyCondition.BucketId
            };
            result = lobbyHandle.SetBucketId(ref bucketIdOptions);

            if (result != ResultE.Success){
                Logger.LogError("AddSerachLobbyAttribute", "Could not set bucket id.", (Result)result);
                lobbyHandle.Release();
                return (Result)result;
            }

            // Set attribute to handle in local
            foreach(var attribute in lobbyCondition.Attributes){
                LobbyModificationAddAttributeOptions attributeOptions = new LobbyModificationAddAttributeOptions(){
                    Attribute = attribute.AsLobbyAttribute(),
                    Visibility = attribute.Visibility
                };

                result = lobbyHandle.AddAttribute(ref attributeOptions);
                if (result != ResultE.Success){
                    Logger.LogError("AddSerachLobbyAttributey", "Could not add attribute.", (Result)result);
                    lobbyHandle.Release();
                    return (Result)result;
                }
            }
            // Add host's user attributes
            foreach(var attr in userAttributes){
                var attrOptions = new LobbyModificationAddMemberAttributeOptions(){
                    Attribute = attr.AsLobbyAttribute(),
                    Visibility = LobbyAttributeVisibility.Public
                };
                result = lobbyHandle.AddMemberAttribute(ref attrOptions);

                if (result != ResultE.Success){
                    Logger.LogError("AddSerachLobbyAttribute", "Could not add Host's member attribute.", (Result)result);
                    lobbyHandle.Release();
                    return (Result)result;
                }
            }
            //Add attribute with handle
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions(){
                LobbyModificationHandle = lobbyHandle
            };
            // Init for async
            Result updateResult = Result.None;
            bool finishUpdate = false;
            
            lobbyInterface.UpdateLobby(ref updateOptions, null, OnAddSearchAttribute);

            await UniTask.WaitUntil(() => finishUpdate, cancellationToken: token);
            lobbyHandle.Release();

            return updateResult;

            void OnAddSearchAttribute(ref UpdateLobbyCallbackInfo info){
                updateResult = (Result)info.ResultCode;
                if (info.ResultCode != ResultE.Success){
                    finishUpdate = true;
                    Logger.LogError("OnAddSearchAttribute", (Result)info.ResultCode);
                    return;
                }
                OnLobbyUpdated(info.LobbyId);
                CurrentLobby._BeingCreated = false;

                finishUpdate = true;
            }
        }
#endregion
//Guest
#region Search
        /// <summary>
        /// Find and join the lobby. <br />
        /// If the lobby fills up or is closed by the host, the lobby becomes unsearch-lobby and has SocketName for p2p instead of search attributes.<br />
        /// *<c>StartMatching()</c> can search for an existing lobby and create one if there isn't. <br />
        /// <c>ReconnectParticipatingLobby()</c> can re-join by lobby-ID.
        /// </summary>
        /// <param name="lobbyCondition"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<Result> JoinExistingLobby(Lobby lobbyCondition, List<AttributeData> userAttributes, CancellationToken token){
            //Serach
            var retrieveResult = await RetriveLobbyByAttribute(lobbyCondition, token);
            if(retrieveResult.result != Result.Success){
                ReleaseLobbySearch(retrieveResult.lobbySerach);
                return retrieveResult.result; //Need to create own session
            }
            //Join
            Result joinLobby = await TryJoinSearchResults(retrieveResult.lobbySerach, userAttributes, false, token);
            if(joinLobby != Result.Success){
                return joinLobby;  //Need to create own session
            }
            return joinLobby;
        }
        /// <summary>
        /// Wait for a match to be made or cancelled.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>Waiting result. Just returne value when matchmaking is established, canceled, kicked, or otherwise unable to wait.</returns>
        async UniTask<Result> WaitForMatchingEstablishment(CancellationToken token){
            //　Wait for the matchmaking result.
            //These value are changed by MamberStatusUpdate notification.
            isMatchmakingCompleted = false;
            matchingResult = Result.None;
            MatchMakeManager.Instance.MatchMakingGUIEvents.ChangeState(MatchMakingGUIEvents.State.Wait);
            //Wait for closing lobby or timeout
            try{
                await UniTask.WaitUntil(() => isMatchmakingCompleted, cancellationToken: token);

                return matchingResult;
            }
            catch (OperationCanceledException){
                MatchMakeManager.Instance.MatchMakingGUIEvents.ChangeState(MatchMakingGUIEvents.State.Cancel);
                //User cancels Matchmaking process from cancel APIs.
                if(!MatchMakeManager.Instance.isLooking){
                    return Result.Canceled;
                }
                
                //Timeout or User cancels Matchmaking process from token.
                if(MatchMakeManager.Instance.enableHostmigrationInMatchmaking){
                    await CancelMatchMaking(MatchMakeManager.Instance.GetCancellationTokenOnDestroy());
                }else{
                    await CloseMatchMaking(MatchMakeManager.Instance.GetCancellationTokenOnDestroy());
                }
                //Need to clear GUI state for Timeout.
                foreach(var member in CurrentLobby.Members){
                    MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(UserId.GetUserId(member.Key), false);
                }
                return Result.TimedOut;
            }
        }
        /// <summary>
        /// Create object to connection via p2p, then open conenction. 
        /// </summary>
        /// <param name="setupTimeoutSec">the time from starting to establishing the connections.</param>
        /// <param name="token">Token not related to timeout　token</param>
        /// <returns></returns>
        public override async UniTask<Result> SetupP2PConnection(ushort setupTimeoutSec, CancellationToken token){
            Result result = InitConnectConfig(ref p2pInfo.Instance.userIds);
            if(result != Result.Success){
                Logger.LogError("InitConnectConfig", "Not enough data to make the connection.", result);
                return result;
            }

            result = await OpenConnection(setupTimeoutSec, token);
            if(result != Result.Success){
                Logger.LogError("OpenConnection", "Failure to make connection.", result);
                return result;
            }
            
            await MatchMakeManager.Instance.OnSaveLobbyID();
            return result;
        }
        /// <summary>
        /// For use in normal matching. Retrive Lobby by Attributes. 
        /// Retrun true on getting Lobby data.
        /// </summary>
        /// <param name="lobbyCondition"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<(Result result, LobbySearch lobbySerach)> RetriveLobbyByAttribute(Lobby lobbyCondition, CancellationToken token){
            //Create Search handle on local
            // Create new handle 
            CreateLobbySearchOptions searchOptions = new CreateLobbySearchOptions(){
                MaxResults = MAX_SEARCH_RESULT
            };

            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            ResultE result = lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch lobbySearchHandle);

            if (result != ResultE.Success){
                Logger.LogError("Search Lobby", "could not create SearchByAttribute.", (Result)result);
                return ((Result)result, null);
            }

            //Set Backet ID
            Epic.OnlineServices.Lobby.AttributeData bucketAttribute = new (){
                Key = "bucket",
                Value = new AttributeDataValue(){ AsUtf8 = lobbyCondition.BucketId }
            };
            LobbySearchSetParameterOptions paramOptions = new LobbySearchSetParameterOptions(){
                Parameter = bucketAttribute,
                ComparisonOp = ComparisonOp.Equal.AsEpic()
            };

            result = lobbySearchHandle.SetParameter(ref paramOptions);
            
            if (result != ResultE.Success){
                Logger.LogError("Retrieve Lobby", "failed to add bucket.", (Result)result);
                return ((Result)result, null);
            }
            
            // Set other attributes
            foreach (var attribute in lobbyCondition.Attributes){
                Epic.OnlineServices.Lobby.AttributeData data = attribute.AsLobbyAttribute();
                paramOptions.Parameter = data;
                paramOptions.ComparisonOp = attribute.ComparisonOperator.AsEpic(); 

                result = lobbySearchHandle.SetParameter(ref paramOptions);

                if (result != ResultE.Success){
                    Logger.LogError("Retrieve Lobby", "failed to add option attribute.", (Result)result);
                    return ((Result)result, null);
                }
            }
            //Search with handle
            Result findResult = await FindLobby(lobbySearchHandle, token);

            //Can retrive data
            return (findResult, lobbySearchHandle);
        }
        /// <summary>
        /// Get Lobby from EOS by LobbyID<br />
        /// Can search closed-lobby.
        /// </summary>
        /// <param name="lobbyId">Id to get</param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<(Result result, LobbySearch lobbySerach)> RetriveLobbyByLobbyId(string lobbyId, CancellationToken token){
            //Create Search handle on local
            // Create new handle 
            CreateLobbySearchOptions searchOptions = new CreateLobbySearchOptions(){ 
                MaxResults = 1 
            };

            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            ResultE result = lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch lobbySearchHandle);

            if (result != ResultE.Success){
                Logger.LogError("RetriveLobbyByLobbyId", "could not create LobbySearch Object for SearchByLobbyId.", (Result)result);
                return ((Result)result, null);
            }

            // Set Lobby ID
            LobbySearchSetLobbyIdOptions setLobbyOptions = new LobbySearchSetLobbyIdOptions(){
                LobbyId = lobbyId
            };
            result = lobbySearchHandle.SetLobbyId(ref setLobbyOptions);

            if (result != ResultE.Success){
                Logger.LogError("RetriveLobbyByLobbyId", "failed to update LobbySearch Object with lobby id.", (Result)result);
                return ((Result)result, null);
            }

            //Search with handle
            Result findResult = await FindLobby(lobbySearchHandle, token);

            //Can retrive data
            return (findResult, lobbySearchHandle);
        }
        /// <summary>
        /// Find lobby Wity searchhandle.
        /// </summary>
        /// <param name="lobbySearchHandle">lobbySearch that set search options.</param>
        /// <param name="token">cancel token</param>
        /// <returns>Api result</returns>
        async UniTask<Result> FindLobby(LobbySearch lobbySearchHandle, CancellationToken token){
            LobbySearchFindOptions findOptions = new LobbySearchFindOptions() {
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };
            Result result = Result.None;
            bool finishFound = false;
            lobbySearchHandle.Find(ref findOptions, null, OnLobbySearchCompleted);

            await UniTask.WaitUntil(() => finishFound, cancellationToken: token);

            return result;

            void OnLobbySearchCompleted(ref LobbySearchFindCallbackInfo info){
                result = (Result)info.ResultCode;
            #if SYNICSUGAR_LOG
                if (info.ResultCode != ResultE.Success) {
                    Logger.Log("Search Lobby", (Result)info.ResultCode);
                }
            #endif
                finishFound = true;
            }
        }
        void ReleaseLobbySearch(LobbySearch lobbySearch){
            if(lobbySearch == null){ return; }
            lobbySearch.Release();
        }
#endregion
#region Join
        /// <summary>
        /// Check result amounts, then if it has 1 or more, try join the lobby.
        /// </summary>
        /// <param name="lobbySearch"></param>
        /// <param name="userAttributes"></param>
        /// <param name="isReconnecter">For Reconenction process. If member is 0, it means ClosedLobby.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<Result> TryJoinSearchResults(LobbySearch lobbySearch, List<AttributeData> userAttributes, bool isReconnecter = false, CancellationToken token = default(CancellationToken)){ 
            if (lobbySearch == null){
                Logger.LogError("TryJoinSearchResults", "There is no LobbySearch.");
                return Result.NotFound;
            }

            var resultCountOptions = new LobbySearchGetSearchResultCountOptions(); 
            uint searchResultCount = lobbySearch.GetSearchResultCount(ref resultCountOptions);
            
            if(searchResultCount == 0){
                return Result.NotFound;
            }
            //For reconnecter
            if(isReconnecter){
                Result result = HasMembers(lobbySearch);
                if(result != Result.Success){   
                    return Result.LobbyClosed;
                }
            }

            LobbySearchCopySearchResultByIndexOptions indexOptions = new LobbySearchCopySearchResultByIndexOptions();
            Result joinResult = Result.None;
            
            for (uint i = 0; i < searchResultCount; i++){
                indexOptions.LobbyIndex = i;

                ResultE searchResult = lobbySearch.CopySearchResultByIndex(ref indexOptions, out LobbyDetails lobbyHandle);

                if (searchResult == ResultE.Success && lobbyHandle != null){

                    joinResult = await JoinLobby(lobbyHandle, userAttributes, isReconnecter, token);
                    
                    if(joinResult == Result.Success){
                        break;
                    }
                }
            }
            ReleaseLobbySearch(lobbySearch);
            return joinResult;
        }
        /// <summary>
        /// For Reconnection. <br />
        /// The lobby to try to reconnect has members? <br />
        /// Can't go back to the empty lobby.
        /// </summary>
        /// <returns></returns>
        Result HasMembers(LobbySearch lobbySearch){
            LobbySearchCopySearchResultByIndexOptions indexOptions = new LobbySearchCopySearchResultByIndexOptions(){ LobbyIndex = 0 };
            ResultE result = lobbySearch.CopySearchResultByIndex(ref indexOptions, out LobbyDetails lobbyDetails);

            if (result != ResultE.Success){
                lobbyDetails.Release();
                Logger.LogError("TryJoinSearchResults", "Reconnecter can't create lobby handle to check member count.");
                return (Result)result;
            }
            LobbyDetailsGetMemberCountOptions countOptions = new LobbyDetailsGetMemberCountOptions();
            uint MemberCount = lobbyDetails.GetMemberCount(ref countOptions);
            
            lobbyDetails.Release();
            if(MemberCount == 0){
                Logger.Log("TryJoinSearchResults", "The lobby had been closed. There is no one.");
                return Result.LobbyClosed;
            }

            return Result.Success;
        }
        
        /// <summary>
        /// Call this from TryJoinSearchResults.<br />
        /// If can join a lobby, then update local lobby infomation in callback.
        /// </summary>
        async UniTask<Result> JoinLobby(LobbyDetails lobbyDetails, List<AttributeData> userAttributes, bool isReconencter, CancellationToken token){
            JoinLobbyOptions joinOptions = new JoinLobbyOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                LobbyDetailsHandle = lobbyDetails,
                PresenceEnabled = false
            };
            Result result = Result.None;
            bool finishJoined = false;

            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSPlatformInterface().GetLobbyInterface();
            lobbyInterface.JoinLobby(ref joinOptions, null, OnJoinLobbyCompleted);
            
            await UniTask.WaitUntil(() => finishJoined, cancellationToken: token);

            lobbyDetails.Release();
            return result;

            void OnJoinLobbyCompleted(ref JoinLobbyCallbackInfo info){
                result = (Result)info.ResultCode;
                if (result != Result.Success){
                    Logger.LogError("Join Lobby", "can't join Lobby.", (Result)info.ResultCode);
                    finishJoined = true;
                    return;
                }

                AddOptions(info.LobbyId).Forget();
            }

            async UniTask AddOptions(string lobbyId){
                // If has joined in other lobby
                if (CurrentLobby.isValid() && !string.Equals(CurrentLobby.LobbyId, lobbyId)){
                    await LeaveLobby(token);
                }

                CurrentLobby.InitFromLobbyHandle(lobbyId);

                if(!isReconencter){
                    foreach(var member in CurrentLobby.Members){
                        UserId thisUserId = UserId.GetUserId(member.Key);
                        MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(thisUserId, true);
                        //About user attribute of localuser, add these after this.
                        if(MatchMakeManager.Instance.isLocalUserId(thisUserId)){
                            continue;
                        }
                        MatchMakeManager.Instance.MemberUpdatedNotifier.MemberAttributesUpdated(thisUserId);
                    }
                    
                    // To get SocketName safety
                    AddNotifyLobbyUpdateReceived();
                    // To get Member attributes
                    AddNotifyLobbyMemberUpdateReceived();
                }
                // For the host migration
                AddNotifyLobbyMemberStatusReceived();
                //RTC
                RTCManager.Instance.AddNotifyParticipantStatusChanged();
                //Member Attribute
                result = await AddUserAttributes(userAttributes, token);

                if(result != Result.Success){
                    finishJoined = true;
                    return;
                }
                
                string LocalId = EOSManager.Instance.GetProductUserId().ToString();
                finishJoined = true;
            }
        }
#endregion
#region Kick
        /// <summary>
        /// Currently preventing duplicate calls.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public override async UniTask<Result> KickTargetMember(UserId target, CancellationToken token){
            if(!CurrentLobby.isHost()){
                Logger.LogError("KickTargetMember", "This user is not Host.");
                return Result.InvalidAPICall;
            }
            var lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            var options = new KickMemberOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = CurrentLobby.LobbyOwner,
                TargetUserId = target.AsEpic
            };
            bool finishKicked = false;
            Result result = Result.None;

            lobbyInterface.KickMember(ref options, null, OnKickMember);
            await UniTask.WaitUntil(() => finishKicked, cancellationToken: token);
            return result;

            void OnKickMember(ref KickMemberCallbackInfo info){
                result = (Result)info.ResultCode;
                if(result != Result.Success){
                    Logger.LogError("KickTarget", "Can't kick target.", result);
                }
                finishKicked = true;
            }
        }
#endregion
//Common
#region Notification
        /// <summary>
        /// For Host and Guest. To get the notification for a user to join and disconnect.
        /// </summary>
        /// <returns></returns>
        void AddNotifyLobbyMemberStatusReceived(){
            if(LobbyMemberStatusNotifyId == 0){
                var options = new AddNotifyLobbyMemberStatusReceivedOptions();
                LobbyMemberStatusNotifyId = EOSManager.Instance.GetEOSLobbyInterface().AddNotifyLobbyMemberStatusReceived(ref options, null, OnLobbyMemberStatusReceived);

                if (LobbyMemberStatusNotifyId == 0){
                    Logger.LogError("LobbyMemberStatusReceived", "could not subscribe, bad notification id returned.");
                }
            }
        }
        void RemoveNotifyLobbyMemberStatusReceived(){
            if(LobbyMemberStatusNotifyId == 0){
                return;
            }
            EOSManager.Instance.GetEOSLobbyInterface().RemoveNotifyLobbyMemberStatusReceived(LobbyMemberStatusNotifyId);
            LobbyMemberStatusNotifyId = 0;
        }
        void OnLobbyMemberStatusReceived(ref LobbyMemberStatusReceivedCallbackInfo info){
            if (!info.TargetUserId.IsValid()){
                Logger.LogError("Lobby Notification", "This target player is invalid.");
                return;
            }
            ProductUserId productUserId = EOSManager.Instance.GetProductUserId();
            // When a local player is kicked out of the Lobby
            if (info.TargetUserId == productUserId){
                if (info.CurrentStatus == LobbyMemberStatus.Closed ||
                    info.CurrentStatus == LobbyMemberStatus.Kicked ||
                    info.CurrentStatus == LobbyMemberStatus.Disconnected){
                    CurrentLobby.Clear();
                    Reason reason = Reason.Unknown;
                    switch(info.CurrentStatus){
                        case LobbyMemberStatus.Closed:
                            matchingResult = Result.LobbyClosed;
                            reason = Reason.LobbyClosed;
                        break;
                        case LobbyMemberStatus.Kicked:
                            matchingResult = Result.UserKicked;
                            reason = Reason.Kicked;
                        break;
                        case LobbyMemberStatus.Disconnected:
                            matchingResult = Result.NetworkDisconnected;
                            reason = Reason.Disconnected;
                        break;
                    }
                    RemoveAllNotifyEvents();

                    if(SynicSugarManger.Instance.State.IsInSession){
                        CloseSessionOnLobbyClosure(reason).Forget();
                        return;
                    }

                    isMatchmakingCompleted = true;
                    return;
                }
            }

            OnLobbyUpdated(info.LobbyId);
            
            //For MatchMaking
            if(SynicSugarManger.Instance.State.IsMatchmaking){
                // The notify about promote dosen't change member amount.
                if(info.TargetUserId == productUserId){
                    if(info.CurrentStatus == LobbyMemberStatus.Promoted){
                        // This local player manage lobby from this time.
                        // So, this user dosen't need to get update notify.
                        if(info.TargetUserId == productUserId){
                            Logger.Log("OnLobbyMemberStatusReceived", "This local user becomes new Host.");
                            RemoveNotifyLobbyUpdateReceived();

                            // Change the UI state when local user is promoted.
                            if(useManualFinishMatchMake){
                                MatchMakeManager.Instance.MatchMakingGUIEvents.LocalUserIsPromoted(CurrentLobby.Members.Count >= requiredMembers);
                            }
                        }
                        return;
                    }
                }
                //Meet member condition?
                if(useManualFinishMatchMake){
                    MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(UserId.GetUserId(info.TargetUserId), info.CurrentStatus == LobbyMemberStatus.Joined, CurrentLobby.Members.Count >= requiredMembers);
                }else{
                    //Lobby is full. Stop additional member and change search attributes to SoketName.
                    MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(UserId.GetUserId(info.TargetUserId), info.CurrentStatus == LobbyMemberStatus.Joined);
                    //Auto comlete match
                    if(CurrentLobby.isHost() && CurrentLobby.Members.Count == CurrentLobby.MaxLobbyMembers){
                        SwitchLobbyAttribute();
                    }
                }
                return;
            }

            //In game
            // Hosts changed?
            if (info.CurrentStatus == LobbyMemberStatus.Promoted){
                p2pInfo.Instance.userIds.HostUserId = UserId.GetUserId(info.TargetUserId);
                Logger.Log($"MemberStatusNotyfy", $"{UserId.GetUserId(info.TargetUserId).ToMaskedString()} is promoted to host.");

            }else if(info.CurrentStatus == LobbyMemberStatus.Left) {
                Logger.Log($"MemberStatusNotyfy", $"{UserId.GetUserId(info.TargetUserId).ToMaskedString()} left from lobby.");
                p2pInfo.Instance.userIds.RemoveUserId(info.TargetUserId);
                p2pInfo.Instance.ConnectionNotifier.Leaved(UserId.GetUserId(info.TargetUserId), Reason.Left);
            }else if(info.CurrentStatus == LobbyMemberStatus.Disconnected){
                Logger.Log($"MemberStatusNotyfy", $"{UserId.GetUserId(info.TargetUserId).ToMaskedString()} diconnect from lobby.");
                p2pInfo.Instance.userIds.MoveTargetUserIdToLefts(info.TargetUserId);
                p2pInfo.Instance.ConnectionNotifier.Disconnected(UserId.GetUserId(info.TargetUserId), Reason.Disconnected);
            }else if(info.CurrentStatus == LobbyMemberStatus.Joined){
                p2pInfo.Instance.userIds.MoveTargetUserIdToRemoteUsersFromLeft(info.TargetUserId);
                // Send Id list.
                if(p2pInfo.Instance.IsHost()){
                    ConnectionSetupHandler setupHandler = new();
                    setupHandler.SendUserList(UserId.GetUserId(info.TargetUserId));
                    Logger.Log($"MemberStatusNotyfy", $"Send user list to {UserId.GetUserId(info.TargetUserId).ToMaskedString()}.");
                }
                p2pInfo.Instance.ConnectionNotifier.Connected(UserId.GetUserId(info.TargetUserId));
            }
        }
        /// <summary>
        /// For Guest to retrive the SoketName after joining lobby.
        /// On getting the ScoketName, discard this event.
        /// </summary>
        /// <returns></returns>
        void AddNotifyLobbyUpdateReceived(){
            if(LobbyUpdateNotififyId == 0){
                var options = new AddNotifyLobbyUpdateReceivedOptions();
                LobbyUpdateNotififyId = EOSManager.Instance.GetEOSLobbyInterface().AddNotifyLobbyUpdateReceived(ref options, null, OnLobbyUpdateReceived);
            
                if (LobbyUpdateNotififyId == 0){
                    Logger.LogError("LobbyUpdateReceived", "could not subscribe, bad notification id returned.");
                }
            }
        }
        void RemoveNotifyLobbyUpdateReceived(){
            if(LobbyUpdateNotififyId == 0){
                return;
            }
            EOSManager.Instance.GetEOSLobbyInterface().RemoveNotifyLobbyUpdateReceived(LobbyUpdateNotififyId);
            LobbyUpdateNotififyId = 0;
        }
        /// <summary>
        /// To finish matchmaking for guest.
        /// </summary>
        /// <param name="info"></param>
        void OnLobbyUpdateReceived(ref LobbyUpdateReceivedCallbackInfo info){
            if(info.LobbyId != CurrentLobby.LobbyId){
                Logger.LogError("Lobby Updated", "this notify is for the other lobby.");
                return;
            }

            OnLobbyUpdated(info.LobbyId);

            if(CurrentLobby.PermissionLevel == LobbyPermissionLevel.Joinviapresence){
                // No need to get lobby update info (to get socket name)
                RemoveNotifyLobbyUpdateReceived();
                
                matchingResult = Result.Success;
                isMatchmakingCompleted = true;
            }
        }
        /// <summary>
        /// To get info to be updated on Member attributes.
        /// </summary>
        /// <returns></returns>
        void AddNotifyLobbyMemberUpdateReceived(){
            if(LobbyMemberUpdateNotifyId == 0){
                var options = new AddNotifyLobbyMemberUpdateReceivedOptions();
                LobbyMemberUpdateNotifyId = EOSManager.Instance.GetEOSLobbyInterface().AddNotifyLobbyMemberUpdateReceived(ref options, null, OnLobbyMemberUpdate);
            
                if (LobbyMemberUpdateNotifyId == 0){
                    Logger.LogError("LobbyMemberUpdateReceived", "could not subscribe, bad notification id returned.");
                }
            }
        }
        void RemoveNotifyLobbyMemberUpdateReceived(){
            if(LobbyMemberUpdateNotifyId == 0){
                return;
            }
            EOSManager.Instance.GetEOSLobbyInterface().RemoveNotifyLobbyMemberUpdateReceived(LobbyMemberUpdateNotifyId);
            LobbyMemberUpdateNotifyId = 0;
        }
        void OnLobbyMemberUpdate(ref LobbyMemberUpdateReceivedCallbackInfo info){
            if(info.LobbyId != CurrentLobby.LobbyId){
                Logger.LogError("OnLobbyMemberUpdate", "this is other lobby data.");
                return;
            }

            OnLobbyUpdated(info.LobbyId);
            MatchMakeManager.Instance.MemberUpdatedNotifier.MemberAttributesUpdated(UserId.GetUserId(info.TargetUserId));
        }
        /// <summary>
        /// Delete all notification events if the result is not Success and the API is completed.
        /// </summary> 
        void RemoveAllNotifyEvents(){
            RemoveNotifyLobbyMemberStatusReceived();
            RemoveNotifyLobbyMemberUpdateReceived();
            RemoveNotifyLobbyUpdateReceived();
        }
#endregion
#region Modify
        /// <summary>
        /// Add scoketName and remove search attributes.
        /// This process is the preparation for p2p connect and re-Connect. <br />
        /// Use lobbyID to connect on the problem, so save lobbyID in local somewhere.
        /// </summary>
        /// <returns></returns>
        public override void SwitchLobbyAttribute(){
            if (!CurrentLobby.isHost()){
                Logger.LogError("SwitchLobbyAttribute", "This user isn't lobby owner.");
                matchingResult = Result.InvalidAPICall;
                return;
            }
            if(MatchMakeManager.Instance.isLooking && CurrentLobby.Members.Count < requiredMembers){
                Logger.LogError("SwitchLobbyAttribute", "This lobby doesn't meet required member condition.");
                matchingResult = Result.InvalidAPICall;
                return;
            }

            UpdateLobbyModificationOptions options = new UpdateLobbyModificationOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };

            //Create modify handle
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            ResultE result = lobbyInterface.UpdateLobbyModification(ref options, out LobbyModification lobbyHandle);

            if (result != ResultE.Success){
                Logger.LogError("SwitchLobbyAttribute", "Could not create lobby modification.", (Result)result);
                matchingResult = (Result)result;
                isMatchmakingCompleted = true;
                return;
            }
            //Change permission level
            LobbyModificationSetPermissionLevelOptions permissionOptions = new LobbyModificationSetPermissionLevelOptions(){
                PermissionLevel = LobbyPermissionLevel.Joinviapresence
            };
            result = lobbyHandle.SetPermissionLevel(ref permissionOptions);
            if (result != ResultE.Success){
                Logger.LogError("SwitchLobbyAttribute", "can't change permission level.", (Result)result);
                matchingResult = (Result)result;
                isMatchmakingCompleted = true;
                return;
            }

            // Set SocketName
            string socket = EOSp2pExtenstions.GenerateRandomSocketName();
            Epic.OnlineServices.Lobby.AttributeData socketAttribute = new(){
                Key = "socket",
                Value = new AttributeDataValue(){ AsUtf8 = socket }
            };
            
            LobbyModificationAddAttributeOptions addAttributeOptions = new LobbyModificationAddAttributeOptions(){
                Attribute = socketAttribute,
                Visibility = LobbyAttributeVisibility.Public
            };

            result = lobbyHandle.AddAttribute(ref addAttributeOptions);
            if (result != ResultE.Success){
                Logger.LogError("SwitchLobbyAttribute", "could not add socket name.", (Result)result);
                matchingResult = (Result)result;
                isMatchmakingCompleted = true;
                return;
            }
            // Set attribute to handle in local
            foreach(var attribute in CurrentLobby.Attributes){
                LobbyModificationRemoveAttributeOptions removeAttributeOptions = new LobbyModificationRemoveAttributeOptions(){
                    Key = attribute.Key
                };

                result = lobbyHandle.RemoveAttribute(ref removeAttributeOptions);
                if (result != ResultE.Success){
                    Logger.LogError("SwitchLobbyAttribute", "could not remove attribute.", (Result)result);

                    matchingResult = (Result)result;
                    isMatchmakingCompleted = true;
                    return;
                }
            }
            //Change lobby attributes with handle
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions(){
                LobbyModificationHandle = lobbyHandle
            };

            lobbyInterface.UpdateLobby(ref updateOptions, null, OnSwitchLobbyAttribute);
            lobbyHandle.Release();

            void OnSwitchLobbyAttribute(ref UpdateLobbyCallbackInfo info){
                matchingResult = (Result)info.ResultCode;
                if (info.ResultCode != ResultE.Success){
                    Logger.LogError("OnSwitchLobbyAttribute", (Result)info.ResultCode);
                    isMatchmakingCompleted = true;
                    return;
                }

                OnLobbyUpdated(info.LobbyId);
                isMatchmakingCompleted = true;
            }
        }
        /// <summary>
        /// For join. Host add self attributes on adding serach attribute.
        /// </summary>
        async UniTask<Result> AddUserAttributes(List<AttributeData> userAttributes, CancellationToken token){
            if(userAttributes == null || userAttributes.Count == 0){
                return Result.Success;
            }
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            UpdateLobbyModificationOptions options = new UpdateLobbyModificationOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };
            ResultE result = lobbyInterface.UpdateLobbyModification(ref options, out LobbyModification lobbyHandle);
            if(result != ResultE.Success){
                Logger.LogError("AddUserAttributes", "can't get modify handle.", (Result)result);
                return (Result)result;
            }

            foreach(var attr in userAttributes){
                var attrOptions = new LobbyModificationAddMemberAttributeOptions(){
                    Attribute = attr.AsLobbyAttribute(),
                    Visibility = LobbyAttributeVisibility.Public
                };
                result = lobbyHandle.AddMemberAttribute(ref attrOptions);

                if (result != ResultE.Success){
                    Logger.LogError("AddMemberAttribute", "could not add member attribute.", (Result)result);
                    return (Result)result;
                }
            }
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions(){
                LobbyModificationHandle = lobbyHandle
            };

            Result addResult = Result.None;
            bool finshAddedResult = false;
            lobbyInterface.UpdateLobby(ref updateOptions, null, OnAddedUserAttributes);

            await UniTask.WaitUntil(() => finshAddedResult, cancellationToken: token);
            lobbyHandle.Release();

            return addResult;

            void OnAddedUserAttributes(ref UpdateLobbyCallbackInfo info){
                addResult = (Result)info.ResultCode;
                if (info.ResultCode != ResultE.Success){
                    Logger.LogError("OnAddedUserAttributes", (Result)info.ResultCode);
                    finshAddedResult = true;
                    return;
                }
                Logger.Log("OnAddedUserAttributes", "added User attributes.");

                finshAddedResult = true;
            }
        }
        void OnLobbyUpdated(string lobbyId){
            if (CurrentLobby.LobbyId == lobbyId){
                CurrentLobby.InitFromLobbyHandle(lobbyId);
            }
        }
        /// <summary>
        /// To check disconencted user's conenction state after p2p.
        /// </summary>
        /// <param name="index">User Index in AllUserIds</param>
        public override void UpdateMemberAttributeAsHeartBeat(int index){
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            UpdateLobbyModificationOptions options = new UpdateLobbyModificationOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };
            ResultE result = lobbyInterface.UpdateLobbyModification(ref options, out LobbyModification lobbyHandle);
            if(result != ResultE.Success){
                Logger.LogError("UpdateMemberAttributeAsHeartBeat", "can't get modify handle.");
                return;
            }
            var attrOptions = new LobbyModificationAddMemberAttributeOptions(){
                Attribute = new Epic.OnlineServices.Lobby.AttributeData() { Key = "HeartBeat", Value = index },
                Visibility = LobbyAttributeVisibility.Public
            };
            result = lobbyHandle.AddMemberAttribute(ref attrOptions);

            if (result != ResultE.Success){
                Logger.LogError("UpdateMemberAttributeAsHeartBeat", "Could not add member attribute.", (Result)result);
                return;
            }
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions(){
                LobbyModificationHandle = lobbyHandle
            };

            lobbyInterface.UpdateLobby(ref updateOptions, null, OnAddedUserAttributes);
             
            void OnAddedUserAttributes(ref UpdateLobbyCallbackInfo info){
                lobbyHandle.Release();
                if (info.ResultCode != ResultE.Success){
                    Logger.LogError("OnAddedUserAttributes", "The changing lobby for heart beat failed.", (Result)info.ResultCode);
                    return;
                }
                Logger.Log("OnAddedUserAttributes", $"Heart beat is success. this user index is {index}");
            }
        }
#endregion
#region Cancel MatchMake
    /// <summary>
    /// Host close matchmaking. Guest Cancel matchmaking.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public override async UniTask<Result> CloseMatchMaking(CancellationToken token = default(CancellationToken)){
        if(!CurrentLobby.isValid()){
            Logger.LogError($"CloseMatchMaking", "This user has not participated a lobby.");
            return Result.InvalidAPICall;
        }
        //Destroy or Leave the current lobby.
        if(CurrentLobby.isHost()){
            Result destroyResult = await DestroyLobby(token);
            
            if(destroyResult != Result.Success){
                Logger.LogError($"CloseMatchMaking", "There was a problem when destroying the lobby.");
            }

            return destroyResult;
        }

        Result leaveResult = await LeaveLobby(token);
        
        if(leaveResult != Result.Success){
            Logger.LogError($"CloseMatchMaking", "There was a problem when leaving the lobby.");
        }
        return leaveResult;
    }
    /// <summary>
    /// Cancel MatcgMaking and leave the lobby.
    /// </summary>
    /// <param name="token"></param>
    /// <returns>If true, user can leave or destroy the lobby. </returns>
    public override async UniTask<Result> CancelMatchMaking(CancellationToken token = default(CancellationToken)){
        if(!CurrentLobby.isValid()){
            Logger.LogError($"CancelMatchMaking", "This local user has not participated a lobby.");
            return Result.InvalidAPICall;
        }
        //Destroy or Leave the current lobby.
        if(CurrentLobby.isHost()){
            if(CurrentLobby.Members.Count == 1){
                Result destroyResult = await DestroyLobby(token);
                
                if(destroyResult != Result.Success){
                    Logger.LogError($"CancelMatchMaking", "There was a problem when destroying the lobby.");
                }

                return destroyResult;
            }
        }

        Result leaveResult = await LeaveLobby(token);
        
        if(leaveResult != Result.Success){
            Logger.LogError($"CancelMatchMaking", "There was a problem when leaving the lobby.");
        }
        return leaveResult;
    }
#endregion
#region Leave
        //SynicSugar does not expect user to be in more than one Lobby at the same time.
        //So when joining in a new Lobby, the user needs to exit an old one.
        //It is not necessary to synchronize in most situations and can add Forget().
        //About Guests, when the lobby is Destroyed by Host, they Leave the lobby automatically.
        /// <summary>
        /// Leave the Participating Lobby.<br />
        /// When a game is over, call DestroyLobby() instead of this.
        /// </summary>
        /// <param name="token"></param>
        public override async UniTask<Result> LeaveLobby(CancellationToken token){
            if (CurrentLobby == null || string.IsNullOrEmpty(CurrentLobby.LobbyId) || !EOSManager.Instance.GetProductUserId().IsValid()){
                Logger.LogWarning("LeaveLobby", "This local user is not in an online lobby.");
                return Result.InvalidAPICall;
            }

            LeaveLobbyOptions options = new LeaveLobbyOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };

            Result result = Result.None;
            bool finishLeave = false;

            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            lobbyInterface.LeaveLobby(ref options, null, OnLeaveLobbyCompleted);

            await UniTask.WaitUntil(() => finishLeave, cancellationToken: token);

            if(result != Result.Success){
                return result;
            }

            RemoveAllNotifyEvents();
            if(SynicSugarManger.Instance.State.IsMatchmaking){
                matchingResult = Result.LobbyClosed;
                isMatchmakingCompleted = true;
            }else if(SynicSugarManger.Instance.State.IsInSession){
                await MatchMakeManager.Instance.OnDeleteLobbyID();
            }

            return result;

            void OnLeaveLobbyCompleted(ref LeaveLobbyCallbackInfo info){
                result = (Result)info.ResultCode;
                if (info.ResultCode != ResultE.Success){
                    Logger.LogError("LeaveLobby", "Failed to leave lobby.", (Result)info.ResultCode);
                    finishLeave = true;
                    return;
                }
                CurrentLobby.Clear();
                finishLeave = true;
            }
        }
#endregion
#region Destroy
        /// <summary>
        /// When a game is over, call this. Guest leaves Lobby by update notify.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>On destroy success, return true.</returns>
        public override async UniTask<Result> DestroyLobby(CancellationToken token){
            if(!CurrentLobby.isHost()){
                Logger.LogError("Destroy Lobby", "This user is not the Host.");
                return Result.InvalidAPICall;
            }
            DestroyLobbyOptions options = new DestroyLobbyOptions(){
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                LobbyId = CurrentLobby.LobbyId
            };

            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();

            Result result = Result.None;
            bool finishDestory = false;
            lobbyInterface.DestroyLobby(ref options, null, OnDestroyLobbyCompleted);
            
            await UniTask.WaitUntil(() => finishDestory, cancellationToken: token);

            if(result != Result.Success){
                return result;
            }
            RemoveAllNotifyEvents();
            if(SynicSugarManger.Instance.State.IsMatchmaking){
                matchingResult = Result.LobbyClosed;
                isMatchmakingCompleted = true;
            }else if(SynicSugarManger.Instance.State.IsInSession){
                await MatchMakeManager.Instance.OnDeleteLobbyID();
            }

            return result;

            void OnDestroyLobbyCompleted(ref DestroyLobbyCallbackInfo info){
                result = (Result)info.ResultCode;
                if (info.ResultCode != ResultE.Success){
                    Logger.LogError("Destroy Lobby", (Result)info.ResultCode);
                    finishDestory = true;
                    return;
                }
                
                CurrentLobby.Clear();
                finishDestory = true;
            }
        }
#endregion
#region Offline
        /// <summary>
        /// Search for lobbies in backend and join in one to meet conditions.<br />
        /// When player could not join, they create lobby as host and wait for other player.
        /// </summary>
        /// <param name="lobbyCondition">Create and search condition. <c>MUST NOT</c> add the data not to open public.</param>
        /// <param name="delay"></param>
        /// <param name="userAttributes"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async UniTask<Result> CreateOfflineLobby(Lobby lobbyCondition, OfflineMatchmakingDelay delay, List<AttributeData> userAttributes, CancellationToken token){
            CurrentLobby.InitializeOfflineLobby(lobbyCondition, userAttributes);

            MatchMakeManager.Instance.MatchMakingGUIEvents.LobbyMemberCountChanged(localUserId, true);
            MatchMakeManager.Instance.MemberUpdatedNotifier.MemberAttributesUpdated(localUserId);

            if(delay.WaitForOpponentsDelay > 0){
                MatchMakeManager.Instance.MatchMakingGUIEvents.ChangeState(MatchMakingGUIEvents.State.Wait);
                await UniTask.Delay((int)delay.WaitForOpponentsDelay, cancellationToken: token);
            }
            if(delay.FinishMatchmakingDelay > 0){
                MatchMakeManager.Instance.MatchMakingGUIEvents.ChangeState(MatchMakingGUIEvents.State.SetupP2P);
                await UniTask.Delay((int)delay.FinishMatchmakingDelay, cancellationToken: token);
            }
            //Set User info
            p2pConfig.Instance.sessionCore.ScoketName = "OFFLINEMODE";
            p2pInfo.Instance.userIds.HostUserId = localUserId;
            p2pInfo.Instance.userIds.AllUserIds.Add(localUserId);
            p2pInfo.Instance.userIds.CurrentAllUserIds.Add(localUserId);
            p2pInfo.Instance.userIds.CurrentConnectedUserIds.Add(localUserId);
            
            await MatchMakeManager.Instance.OnSaveLobbyID();
            if(delay.ReadyForConnectionDelay > 0){
                MatchMakeManager.Instance.MatchMakingGUIEvents.ChangeState(MatchMakingGUIEvents.State.Ready);
                await UniTask.Delay((int)delay.ReadyForConnectionDelay, cancellationToken: token);
            }

            return Result.Success;
        }
        public override async UniTask<Result> DestroyOfflineLobby(CancellationToken token){
            await MatchMakeManager.Instance.OnDeleteLobbyID();

            CurrentLobby.Clear();

            return Result.Success;
        }
#endregion
        /// <summary>
        /// Init p2pManager's room info with new lobby data.
        /// </summary>
        /// <param name="userIds"></param>
        /// <returns></returns>
        /// *UserIDs contains Internal, so other method must be need for scalability.
        Result InitConnectConfig(ref UserIds userIds){
            //Prep RTC(Voice Chat)
            RTCManager.Instance.AddNotifyParticipantUpdated();
            //Crate copy handle
            LobbyInterface lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
            CopyLobbyDetailsHandleOptions options = new CopyLobbyDetailsHandleOptions(){
                LobbyId = CurrentLobby.LobbyId,
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };

            ResultE result = lobbyInterface.CopyLobbyDetailsHandle(ref options, out LobbyDetails lobbyHandle);

            if(result != ResultE.Success){
                Logger.LogError("InitConnectConfig", "can't get lobby detail handle.", (Result)result);
                return (Result)result;
            }
            //Copy details from local handle
            LobbyDetailsCopyInfoOptions dataOptions = new LobbyDetailsCopyInfoOptions();
            result = lobbyHandle.CopyInfo(ref dataOptions, out LobbyDetailsInfo? dataInfo);

            if(result != ResultE.Success){
                Logger.LogError("InitConnectConfig", "can't copy lobby details on local.", (Result)result);
                lobbyHandle.Release();
                return (Result)result;
            }
            //Get member count
            LobbyDetailsGetMemberCountOptions countOptions = new LobbyDetailsGetMemberCountOptions();
            uint memberCount = lobbyHandle.GetMemberCount(ref countOptions);
            //Get other use's id
            LobbyDetailsGetMemberByIndexOptions memberOptions = new LobbyDetailsGetMemberByIndexOptions();
            for(uint i = 0; i < memberCount; i++){
                memberOptions.MemberIndex = i;
                UserId targetId = UserId.GetUserId(lobbyHandle.GetMemberByIndex(ref memberOptions));

                userIds.AllUserIds.Add(targetId);

                if(targetId != userIds.LocalUserId){
                    userIds.RemoteUserIds.Add(targetId);
                }
            }
            userIds.CurrentAllUserIds = new List<UserId>(userIds.AllUserIds);
            userIds.CurrentConnectedUserIds = new List<UserId>(userIds.AllUserIds);
            //Get lobby's attribute count
            LobbyDetailsCopyAttributeByKeyOptions attrOptions = new LobbyDetailsCopyAttributeByKeyOptions();
            attrOptions.AttrKey = "socket";
            result = lobbyHandle.CopyAttributeByKey(ref attrOptions, out Epic.OnlineServices.Lobby.Attribute? socket);
            if(result != ResultE.Success || socket?.Data == null){
                Logger.LogError("InitConnectConfig", "can't copy lobby attribute on local.", (Result)result);
                lobbyHandle.Release();
                return (Result)result;
            }
            p2pConfig.Instance.sessionCore.ScoketName = EOSLobbyExtensions.GenerateLobbyAttribute(socket).STRING;
            //For options
            userIds.HostUserId = UserId.GetUserId(CurrentLobby.LobbyOwner);
            lobbyHandle.Release();
            return (Result)result;
        }
        /// <summary>
        /// Open conenction and init some objects
        /// </summary>
        /// <param name="setupTimeoutSec">the time from starting to establishing the connections.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        async UniTask<Result> OpenConnection(ushort setupTimeoutSec, CancellationToken token){
            if(p2pConfig.Instance.relayControl != RelayControl.AllowRelays){
                p2pConfig.Instance.SetRelayControl(p2pConfig.Instance.relayControl);
            }
            await p2pConfig.Instance.natRelayManager.Init();
            RemoveNotifyLobbyMemberUpdateReceived();
            p2pConfig.Instance.sessionCore.OpenConnection(true);
            Logger.Log("OpenConnection", "Sending a connection request to other peers.");
            
            ConnectionSetupHandler setupHandler = new();
            Result canConnect = await setupHandler.WaitConnectPreparation(token, setupTimeoutSec * 1000); //Pass time as ms.
            if(canConnect != Result.Success){
                return Result.ConnectEstablishFailed;
            }
            //Host sends AllUserIds list, Guest Receives AllUserIds.
            if(p2pInfo.Instance.IsHost()){
                Logger.Log("OpenConnection", "Sending UserList to other peers as the Host.");
                await setupHandler.SendUserListToAll(token);
            }else{
                Logger.Log("OpenConnection", "Waiting for UserList from the Host.");
                await setupHandler.ReciveUserIdsPacket(token);
            }
            p2pInfo.Instance.pings.Init();
            return Result.Success;
        }
        /// <summary>
        /// For library user to save ID.
        /// </summary>
        /// <returns></returns>
        public override string GetCurrentLobbyID(){
            return CurrentLobby.LobbyId;
        }
        
        public override int GetCurrentLobbyMemberCount(){
            //When Host create lobby, they can't count self. This is called before adding member attributes.
           return CurrentLobby._BeingCreated ? 1 : CurrentLobby.Members.Count;
        }

        public override int GetLobbyMemberLimit(){
           return (int)CurrentLobby.MaxLobbyMembers;
        }

        public override AttributeData GetTargetAttributeData(UserId target, string Key){
            return CurrentLobby.Members[target.ToString()]?.GetAttributeData(Key);
        }

        /// <summary>
        /// Get target all attributes.
        /// </summary>
        /// <param name="target">target user id</param>
        /// <returns>If no data, return null.</returns>
        public override List<AttributeData> GetTargetAttributeData(UserId target){
            return CurrentLobby.Members[target.ToString()]?.Attributes;
        }
    }
}