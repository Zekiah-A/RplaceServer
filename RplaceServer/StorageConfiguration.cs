namespace RplaceServer;

public interface IStorageConfiguration
{
    public int BackupFrequencyS { get; set; }
    // Directory where resources, such as Pages and Captcha Generation assets will be stored,
    // multiple instances can technically share a resources directory as their content is static.
    public string StaticResourcesFolder { get; set; }
    // Will contain save data produced by this instance, excluding canvas data, such as log records, bans, mutes and other
    // such instance data.
    public string SaveDataFolder { get; set;  }
    // Seconds
    public int TimelapseLimitPeriodS { get; set; }
    public bool TimelapseEnabled { get; set; }
    // Will contain live canvas save and canvas backups
    public string CanvasFolder { get; set; }
    public bool CreateBackups { get; set; }

}

public class ConfigureStorageOptions : IStorageConfiguration
{
    // Every 15 Minutes
    public int BackupFrequencyS { get; set; } = 900;
    public string StaticResourcesFolder { get; set; } = "StaticData";
    public string SaveDataFolder { get; set; } = "SaveData";
    public string CanvasFolder { get; set; } = "Canvases";
    public int TimelapseLimitPeriodS { get; set; } = 900;
    public bool TimelapseEnabled { get; set; } = false;
    public bool CreateBackups { get; set; } = true;
}