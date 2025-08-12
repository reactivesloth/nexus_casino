using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class ConnectCreateDeviceId
    {
        public CreateDeviceIdCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(int timeout, out ConnectCreateDeviceId connectCreateDeviceId)
        {
            connectCreateDeviceId = new ConnectCreateDeviceId();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[ConnectCreateDeviceId] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(connectCreateDeviceId.CreateDeviceIdCoroutine(timeout));
        }

        private IEnumerator CreateDeviceIdCoroutine(int timeout)
        {
            var connect = EOS.GetCachedConnectInterface();
            if (connect == null)
            {
                CallbackInfo = new CreateDeviceIdCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var createDeviceIdOptions = new CreateDeviceIdOptions
            {
                DeviceModel = $"{SystemInfo.deviceModel} {SystemInfo.deviceName} {SystemInfo.deviceType} {SystemInfo.operatingSystem}",
            };

            connect.CreateDeviceId(ref createDeviceIdOptions, null, (ref CreateDeviceIdCallbackInfo data) => { CallbackInfo = data; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new CreateDeviceIdCallbackInfo { ResultCode = Result.TimedOut }
            );
        }
    }
}