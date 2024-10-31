namespace RplaceServer;

public interface IGameDataBuilder<out T>  : ICanvasConfiguration, IStorageConfiguration, IModerationConfiguration, IServiceConfiguration where T : IGameDataBuilder<T>, new()
{
    // Services
    public TurnstileService? TurnstileService { get; set; }
    
    // Builder
    public static IGameDataBuilder<T> CreateBuilder()
    {
        return new T();
    }
    
    public T Build()
    {
        return (T) this;
    }

    public IGameDataBuilder<T> ConfigureCanvas()
    {
        return ConfigureCanvas(_ => {});
    }

    public IGameDataBuilder<T> ConfigureCanvas(Action<ConfigureCanvasOptions> optionsAction)
    {
        var options = new ConfigureCanvasOptions();
        optionsAction(options);
        CooldownMs = options.CooldownMs;
        BoardWidth = options.BoardWidth;
        BoardHeight = options.BoardHeight;
        Palette = options.Palette;
        return this;
    }
    
    public IGameDataBuilder<T> ConfigureStorage()
    {
        return ConfigureStorage(_ => {});
    }

    public IGameDataBuilder<T> ConfigureStorage(Action<IStorageConfiguration> optionsAction)
    {
        var options = new ConfigureStorageOptions();
        optionsAction(options);
        BackupFrequencyS = options.BackupFrequencyS;
        StaticResourcesFolder = options.StaticResourcesFolder;
        SaveDataFolder = options.SaveDataFolder;
        CanvasFolder = options.CanvasFolder;
        TimelapseLimitPeriodS = options.TimelapseLimitPeriodS;
        TimelapseEnabled = options.TimelapseEnabled;
        CreateBackups = options.CreateBackups;
        return this;
    }
    
    public IGameDataBuilder<T> ConfigureModeration()
    {
        return ConfigureModeration(_ => {});
    }
    
    public IGameDataBuilder<T> ConfigureModeration(Action<IModerationConfiguration> optionsAction)
    {
        var options = new ConfigureModerationOptions();
        optionsAction(options);
        UseCloudflare = options.UseCloudflare;
        ChatCooldownMs = options.ChatCooldownMs;
        CaptchaEnabled = options.CaptchaEnabled;
        CensorChatMessages = options.CensorChatMessages;
        ChatCensorRegexes = options.ChatCensorRegexes;
        ChatAllowedDomainsRegexes = options.ChatAllowedDomainsRegexes;
        return this;
    }
    
    public IGameDataBuilder<T> ConfigureServices()
    {
        return ConfigureServices(_ => {});
    }

    public IGameDataBuilder<T> ConfigureServices(Action<IServiceConfiguration> optionsAction)
    {
        var options = new ConfigureServiceOptions();
        optionsAction(options);
        WebhookService = options.WebhookService;
        TurnstileService = options.TurnstileService;
        return this;
    }
}