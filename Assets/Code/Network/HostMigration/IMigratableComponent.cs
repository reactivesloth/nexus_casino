using FishNet.Object;
using Newtonsoft.Json;

namespace Code.Network.HostMigration
{
    public interface IMigratableComponentComponent<TMigrateDataType> : IMigratableComponentBase where TMigrateDataType : struct
    {
        void OnMigrateDataReceived(TMigrateDataType data);
        new TMigrateDataType GetMigrateData();

        string GetJson(TMigrateDataType data) => JsonConvert.SerializeObject(data, Formatting.Indented);

        //Base implementations
        void IMigratableComponentBase.OnMigrateDataReceived(string jsonData) =>
            OnMigrateDataReceived(JsonConvert.DeserializeObject<TMigrateDataType>(jsonData));
        object IMigratableComponentBase.GetMigrateData() => GetMigrateData();
        string IMigratableComponentBase.GetJson(object data) => GetJson((TMigrateDataType) data);
    }

    public interface IMigratableComponentBase
    {
        void OnMigrateDataReceived(string jsonData);
        object GetMigrateData();
        string GetJson(object data);
    }
}