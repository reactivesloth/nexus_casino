using FishNet.Object;
using Newtonsoft.Json;

namespace Code.Network.HostMigration
{
    public interface IMigratable<TMigrateDataType> : IMigratableBase
    {
        void OnMigrateDataReceived(TMigrateDataType data);
        new TMigrateDataType GetMigrateData();

        string GetJson(TMigrateDataType data) => JsonConvert.SerializeObject(data, Formatting.Indented);

        //Base implementations
        void IMigratableBase.OnMigrateDataReceived(string jsonData) =>
            OnMigrateDataReceived(JsonConvert.DeserializeObject<TMigrateDataType>(jsonData));
        object IMigratableBase.GetMigrateData() => GetMigrateData();
        string IMigratableBase.GetJson(object data) => GetJson((TMigrateDataType) data);
    }

    public interface IMigratableBase
    {
        void OnMigrateDataReceived(string jsonData);
        object GetMigrateData();
        string GetJson(object data);
    }
}