using System.Text;
using Code.API;
using FishNet.Managing.Scened;
using Proyecto26;
using UnityEngine;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Code.Utility
{
    public class ChatacterCastomizationApplyer : MonoBehaviour
    {
        public void ApplyChatacterCastomization()
        {
            var loadForm = new WWWForm();
            loadForm.AddBinaryData("file", Encoding.UTF8.GetBytes(PlayerPrefs.GetString("PlayerModelType")),
                $"PlayerModelType_{ClientDataStorage.UserData.id}.txt");

            var loadModelTypeRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetLoadFileUrl(),
                FormData = loadForm,
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Post(loadModelTypeRequest).Finally(TryLoadSpawnPondAndPlay);
        }

        private void TryLoadSpawnPondAndPlay()
        {
            var getSpawnRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetFileUrl($"SpawnPoint_{ClientDataStorage.UserData.id}.txt"),
                Headers = ClientDataStorage.GetJwtHeader(), Timeout = 5
            };

            RestClient.Get(getSpawnRequest).Then(getSpawnResponse =>
            {
                if (getSpawnResponse.StatusCode != 200)
                    return;

                var responseParts = getSpawnResponse.Text.Split(' ');
                var posX = float.Parse(responseParts[0]);
                var posY = float.Parse(responseParts[1]);
                var posZ = float.Parse(responseParts[2]);
                var rotX = float.Parse(responseParts[3]);
                var rotY = float.Parse(responseParts[4]);
                var rotZ = float.Parse(responseParts[5]);

                var p = new Vector3(posX, posY, posZ);
                var r = Quaternion.Euler(rotX, rotY, rotZ);

                PlayerPrefs.SetFloat("SavedSpawnPositionX", p.x);
                PlayerPrefs.SetFloat("SavedSpawnPositionY", p.y);
                PlayerPrefs.SetFloat("SavedSpawnPositionZ", p.z);
                PlayerPrefs.SetFloat("SavedSpawnRotationX", r.x);
                PlayerPrefs.SetFloat("SavedSpawnRotationY", r.y);
                PlayerPrefs.SetFloat("SavedSpawnRotationZ", r.z);
                PlayerPrefs.SetInt("SavedSpawnPosition", 1);
                PlayerPrefs.Save();

            }).Finally(() => SceneManager.LoadSceneAsync("Main"));
        }
    }
}