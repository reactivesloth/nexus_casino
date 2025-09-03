using Newtonsoft.Json;

namespace Code.Network.HostMigration
{
    public interface IMigratable<TMigrateDataType> : IMigratableBase
    {
        /// <summary>
        /// Migration receive handler. Server method.
        /// </summary>
        /// <param name="data"></param>
        void OnMigrateDataReceived_Server(TMigrateDataType data);
        
        /// <summary>
        /// Get migration data for client migration
        /// </summary>
        /// <returns></returns>
        new TMigrateDataType GetMigrateData_Client();

        string GetJson(TMigrateDataType data) => JsonConvert.SerializeObject(data, Formatting.Indented);

        // Base implementations
        void IMigratableBase.OnMigrateDataReceived_Server(string jsonData) =>
            OnMigrateDataReceived_Server(JsonConvert.DeserializeObject<TMigrateDataType>(jsonData));
        object IMigratableBase.GetMigrateData_Client() => GetMigrateData_Client();
        string IMigratableBase.GetJson(object data) => GetJson((TMigrateDataType)data);
    }

    public interface IMigratableBase
    {
        void OnMigrateDataReceived_Server(string jsonData);
        object GetMigrateData_Client();
        string GetJson(object data);
    }
}