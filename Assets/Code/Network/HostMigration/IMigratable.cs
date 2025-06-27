using Newtonsoft.Json;

namespace Code.Network.HostMigration
{
    public interface IMigratable<TMigrateDataType> : IMigratableBase where TMigrateDataType : class
    {
        void SetMigrateData(TMigrateDataType data);
        new TMigrateDataType GetMigrateData();

        string GetJson(TMigrateDataType data) => JsonConvert.SerializeObject(data, Formatting.Indented);

        //Base implementations
        void IMigratableBase.SetMigrateData(string jsonData) =>
            SetMigrateData(JsonConvert.DeserializeObject<TMigrateDataType>(jsonData));
        object IMigratableBase.GetMigrateData() => GetMigrateData();
        string IMigratableBase.GetJson(object data) => GetJson(data as TMigrateDataType);
    }

    public interface IMigratableBase
    {
        void SetMigrateData(string jsonData);
        object GetMigrateData();
        string GetJson(object data);
    }
}