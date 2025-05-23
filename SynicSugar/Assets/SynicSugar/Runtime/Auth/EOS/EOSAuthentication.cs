using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using SynicSugar.Base;

namespace SynicSugar.Auth {
    public class EOSAuthentication : AuthenticationCore {
        ulong ExpirationNotifyId;
        /// <summary>
        /// Login with DeviceID. If success, return true. <br />
        /// For Success: EOS tokens expire every hour, but EOSAuthentication automatically refresh access tokens via Expiration notify.
        /// In case of failure, an error log is called and the token is invalidated 10 minutes after the notification.<br />
        /// We can't use DeviceId directly for security. This id is saved secure pos like as Keystore.
        /// </summary>
        /// <param name="displayName"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async UniTask<Result> Login(string displayName, CancellationToken token = default(CancellationToken)){
            bool finishCallback = false;
            Result result = Result.Canceled;
            //DeviceID
            var connectInterface = EOSManager.Instance.GetEOSPlatformInterface().GetConnectInterface();
            var createDeviceIdOptions = new CreateDeviceIdOptions() { DeviceModel = SystemInfo.deviceModel };

            connectInterface.CreateDeviceId(ref createDeviceIdOptions, null, OnCreateDeviceIdCallback);
                
            try{
                await UniTask.WaitUntil(() => finishCallback, cancellationToken: token);
            }catch(OperationCanceledException){  
                Logger.LogError("Login", "CreateDeviceId is canceled.", Result.Canceled);
                return Result.Canceled;
            }

            if(result != Result.Success){
                Logger.LogError("Login", "can't get device id", result);
                return result;
            }
            //Login
            finishCallback = false;
            result = Result.Canceled;
            //Pass UserID on each Game.
            EOSManager.Instance.StartConnectLoginWithDeviceToken(displayName, info => {
                    result = (Result)info.ResultCode;
                    finishCallback = true;
                });

            try{
                await UniTask.WaitUntil(() => finishCallback, cancellationToken: token);
            }catch(OperationCanceledException){
                Logger.LogError("Login", "StartConnectLoginWithDeviceToken is canceled.", Result.Canceled);
                return result;
            }

            if(result == Result.Success){   
                SynicSugarManger.Instance.SetLocalUserId(UserId.GetUserId(EOSManager.Instance.GetProductUserId()));
                SynicSugarManger.Instance.State.IsLoggedIn = true;
                AddNotifyAuthExpiration(displayName);
            }else{
                SynicSugarManger.Instance.State.IsLoggedIn = false;
                RemoveNotifyAuthExpiration();
            }
            return result;

            void OnCreateDeviceIdCallback(ref CreateDeviceIdCallbackInfo data){
                result = (Result)data.ResultCode;
                if (result is Result.Success or Result.DuplicateNotAllowed) {
                    Logger.Log("Login", result is Result.Success ? "Create new DeviceId" : "Already have DeviceID in local", result);

                    result = Result.Success;
                }
                finishCallback = true;
            }
        }
        /// <summary>
        /// To receive upcoming authentication expiration notifications.
        /// </summary>
        /// <param name="userName"></param>
        private void AddNotifyAuthExpiration(string userName){
            if (ExpirationNotifyId != 0)
            {
                Logger.LogWarning("AddNotifyAuthExpiration", "AuthExpirationNotify is already registered.");
                return;
            }
            var connectInterface = EOSManager.Instance.GetEOSConnectInterface();
            var addNotifyAuthExpirationOptions = new AddNotifyAuthExpirationOptions();

            ExpirationNotifyId = connectInterface.AddNotifyAuthExpiration(ref addNotifyAuthExpirationOptions, null, OnAuthExpirationCallback);

            if(ExpirationNotifyId != 0){
                Logger.Log("AddNotifyAuthExpiration", "AuthExpiration notification registration succeeded.");
            
            #if UNITY_EDITOR
                SynicSugarManger.Instance.CleanupForEditor += RemoveNotifyAuthExpiration;
            #endif
            }

            void OnAuthExpirationCallback(ref AuthExpirationCallbackInfo data){
                if(data.LocalUserId != SynicSugarManger.Instance.LocalUserId.AsEpic){
                    Logger.LogError("AuthExpirationCallback", "This notify is not for this local user.");
                    return;
                }
                ReLoginWithDeviceToken(userName);
            }
        }
        /// <summary>
        /// After login eos, SynicSugar manages access credentials with this.<br />
        /// *Just for current plugin ver. These ver don't have relogin process.
        /// </summary>
        /// <param name="displayName"></param>
        private void ReLoginWithDeviceToken(string displayName){
            var connectInterface = EOSManager.Instance.GetEOSConnectInterface();

            var loginOptions = new LoginOptions()
            {
                UserLoginInfo = new UserLoginInfo { DisplayName = displayName },
                Credentials = new Credentials
                {
                    Token = null,
                    Type = ExternalCredentialType.DeviceidAccessToken,
                }
            };

            connectInterface.Login(ref loginOptions, null, OnLogin);

            void OnLogin(ref LoginCallbackInfo info){
                Result result = (Result)info.ResultCode;
                if(result != Result.Success){
                    Logger.LogError("ReLoginWithDeviceToken", "Login failed. This user access credentials will become invalid in 10 minutes.");
                    return;
                }
                Logger.Log("ReLoginWithDeviceToken", "Login success!. The access credential has refreshed.");
            }
        }
        /// <summary>
        /// When stop playing mode in editor, remove the notify via SynicSugarManager's OnDestory.
        /// </summary>
        private void RemoveNotifyAuthExpiration(){
            if(ExpirationNotifyId == 0){
                return;
            }
            var connectInterface = EOSManager.Instance.GetEOSConnectInterface();
            connectInterface.RemoveNotifyAuthExpiration(ExpirationNotifyId);
            ExpirationNotifyId = 0;
        }
        /// <summary>
        /// Delete any existing Device ID access credentials for the current user profile on the local device. <br />
        /// On Android and iOS devices, uninstalling the application will automatically delete any local Device ID credentials.<br />
        /// This doesn't means delete User on EOS. We can't delete users from EOS.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async UniTask<Result> DeleteAccount(CancellationToken token = default(CancellationToken)){
            var connectInterface = EOSManager.Instance.GetEOSPlatformInterface().GetConnectInterface();
            DeleteDeviceIdOptions options = new DeleteDeviceIdOptions();
            Result result = Result.Canceled;
            bool finishCallback = false;

            connectInterface.DeleteDeviceId(ref options, null, OnDeleteDeviceId);

            try{
                await UniTask.WaitUntil(() => finishCallback, cancellationToken: token);
            }catch(OperationCanceledException){
                Logger.Log("DeleteAccount", "The delete process was canceled using a cancellation token. ");
                return Result.Canceled;
            }
            
            return result;

            void OnDeleteDeviceId(ref DeleteDeviceIdCallbackInfo data){
                result = (Result)data.ResultCode;
                finishCallback = true;
            }
        }
    }
}