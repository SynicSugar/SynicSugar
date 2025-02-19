#pragma warning disable CS0414 //The field is assigned but its value is never used
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SynicSugar.RTC {
    public class AudioDeviceChangedNotifier {
        public event Action OnDeviceChanged;
        
        public void Register(Action deviceChanged){
            OnDeviceChanged += deviceChanged;
        }
        /// <summary>
        /// Remove all events from the actions. <br />
        /// Events are automatically cleared when they are no longer needed.
        /// </summary>
        public void Clear(){
            OnDeviceChanged = null;
        }
        internal void DeviceChanged(){
            OnDeviceChanged?.Invoke();
        }
        internal async UniTask MonitorGameToUnsubscribe(string sceneName){
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name != sceneName);
            RTCConfig.Instance.RemoveNotifyAudioDevicesChanged();
        }
        internal async UniTask MonitorGameToUnsubscribe(GameObject targetObject){
            await UniTask.WaitUntil(() => !targetObject.activeSelf, cancellationToken: targetObject.GetCancellationTokenOnDestroy());
            RTCConfig.Instance.RemoveNotifyAudioDevicesChanged();
        }
    }
}