using System.Runtime.CompilerServices;
using System.Text;
using System;
using System.Net.Mime;

namespace Server;

public class WebServer
{
    private const string backuplistTemplate = @"
        <h1>rPlace canvas place file/backup list.</h1>
        <p>See [domain-url]/backuplist.txt for cleanly formatted list of backups saved here.</p>
        <span style=""color: red;"">(Do not try to iterate directly through this directory with code, for the sake of your own sanity, please instead use the plaintext list at /backuplist instead.)</span>
        <br> <br>
        <input type=""text"" placeholder=""Search.."" onkeyup=""search(this.value)"">
        <br> <br>
        <script>
        function search(val) {
            let str = val.toLowerCase().trim();
            let links = document.getElementsByTagName('a');
            for (let link of links) {
                    let text = link.innerText.toLowerCase();
                    if (text == '..') return;
                    if (str.length && text.indexOf(str) || !str) link.classList.remove('highlight');
                    else  link.classList.add('highlight');
            }
        }
        async function getList() {
            let blist = (await (await fetch('./backuplist')).text()).split('\n')
            for (let backup of blist) {
                  let a = document.createElement('a')
                  a.innerText = backup
                  a.href = './backups/' + backup
                      document.body.appendChild(a)
                      document.body.appendChild(document.createElement('br'))
            }
        }
        getList()
        </script>
        <style>
            .highlight {
                border-radius: 4px;
                background-color: yellow;
                box-shadow: -2px -2px 4px darkkhaki inset;
            }
        </style>
    ";

    private const string indexTemplate = @"
        <h1>rPlace canvas file server is running.</h1>
        <p>Visit /place in order to fetch the active place file, /backuplist to view a list of all backups, and fetch from /backups/<span style=""background-color: lightgray; border-radius: 4px;"">place file name</span> to obtain a backup by it's filename (in backuplist).</p>
        <pre>
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠓⠒⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣀⣀⠀⠀⠀⠀⠀⢠⢤⣤⣤⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⡠⠔⠒⠒⠲⠎⠀⠀⢹⡃⢀⣀⠀⠑⠃⠀⠈⢀⠔⠒⢢⠀⠀⠀⡖⠉⠉⠉⠒⢤⡀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⠔⠚⠙⠒⠒⠒⠤⡎⠀⠀⠀⠀⢀⣠⣴⣦⠀⠈⠘⣦⠑⠢⡀⠀⢰⠁⠀⠀⠀⠑⠰⠋⠁⠀⠀⠀⠀⠀⠈⢦⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⣸⠁⠀⠀⠀⠀⠀⠀⢰⠃⠀⣀⣀⡠⣞⣉⡀⡜⡟⣷⢟⠟⡀⣀⡸⠀⡎⠀⠀⠀⠀⠀⡇⠀⠀⠀⠀⠀⠀⠀⠀⣻⠀⠀⠀⠀
        ⢰⠂  ⠀⠀⣗⠀⠀⢀⣀⣀⣀⣀⣀⣓⡞⢽⡚⣑⣛⡇⢸⣷⠓⢻⣟⡿⠻⣝⢢⠀⢇⣀⡀⠀⠀⠀⢈⠗⠒⢶⣶⣶⡾⠋⠉⠀⠀⠀⠀⠀
        ⠈⠉⠀⠀⢀⠀⠈⠒⠊⠻⣷⣿⣚⡽⠃⠉⠀⠀⠙⠿⣌⠳⣼⡇⠀⣸⣟⡑⢄⠘⢸⢀⣾⠾⠥⣀⠤⠖⠁⠀⠀⠀⢸⡇⠀⠀⠀⠀⠀⢀⠀⠀
    ⠀⠀⠀⢰⢆⠀⢀⠏⡇⠀⡀⠀⠀⠀⣿⠉⠀⠀⠀⠀⠀⠀⠀⠈⢧⣸⡇⢐⡟⠀⠙⢎⢣⣿⣾⡷⠊⠉⠙⠢⠀⠀⠀⠀⠀⢸⡇⢀⠀⠀⠀⠀⠈⠣⡀
    ⠀⠀⠀⠘⡌⢣⣸⠀⣧⢺⢃⡤⢶⠆⣿⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⣟⠋⢀⠔⣒⣚⡋⠉⣡⠔⠋⠉⢰⡤⣇⠀⠀⠀⠀⢸⡇⡇⠀⠀⠀⠀⠀⠀⠸
    ⠀⠀⠀⠀⠑⢄⢹⡆⠁⠛⣁⠔⠁⠀⣿⠀⠀⠀⠀⢸⡇⠀⠀⠀⠀⠀⣿⢠⡷⠋⠁⠀⠈⣿⡇⠀⠀⠀⠈⡇⠉⠀⠀⠀⠀⢸⡇⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠑⣦⡔⠋⠁⠀⠀⠀⣿⠀⠀⢠⡀⢰⣼⡇⠀⡀⠀⠀⣿⠀⠁⠀⠀⠀⠀⣿⣷⠀⠀⠀⠀⡇⠀⠀⢴⣤⠀⢸⡇⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⢰⣿⡇⠀⠀⠀⠀⠀⣿⡀⠀⢨⣧⡿⠋⠀⠘⠛⠀⠀⣿⠀⠀⢀⠀⠀⠀⣿⣿⠀⠀⠀⠀⢲⠀⠀⠀⠀⠀⢸⡇⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⢸⣿⡇⠀⠀⠀⠀⠀⢸⡧⡄⠀⠹⣇⡆⠀⠀⠀⠀⠀⣿⠀⢰⣏⠀⣿⣸⣿⣿⠀⠀⠀⠀⣼⠀⠀⠰⠗⠀⢸⡇⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⢸⣿⡇⠀⠀⠀⠀⠀⢸⡇⣷⣛⣦⣿⢀⠈⠑⠀⢠⡆⣿⠐⢠⣟⠁⢸⠸⣿⣿⢱⣤⢀⠀⣼⠀⠀⢀⠀⠀⢸⡇⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⢸⣿⡇⠀⢀⠀⠀⠀⢸⡇⠘⠫⣟⡇⠊⣣⠘⠛⣾⡆⢿⠀⠙⣿⢀⣘⡃⣿⣿⡏⠉⠒⠂⡿⠀⠰⣾⡄⠀⢸⡟⣽⣀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠸⣿⡇⠀⠘⣾⠀⠀⢸⡇⢸⣇⡙⠣⠀⣹⣇⠀⠈⠧⢀⣀⣀⡏⣸⣿⣇⢹⣿⡇⢴⣴⣄⣀⡀⢰⣿⡇⠀⢸⣇⢿⡿⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠓⠁⠈⠻⢷⠾⠦⠤⠬⣅⣹⣿⣖⣶⣲⣈⡥⠤⠶⡖⠛⠒⠛⠁⠉⠛⠮⠐⢛⡓⠒⢛⠚⠒⠒⠒⠛⣚⣫⡼⠿⠿⣯⠛⠤⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠉⠉⠉⠉⠉⡉⠉⠁⠀⠀⠘⠓⠀⠀⠀⠀⠀⣀⣞⡿⡉⠉⠉⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠹⣶⠏⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
        </pre>
        <h4 style=""margin: 0px;"">Other pages:</h4>    <a href=""place"">place</a>
        <a href=""backups"">backups</a>
        <a href=""backuplist"">backuplist</a>
        <br>
        <p>©Zekiah-A, BlobKat - rplace.tk ❤️</p>
    ";

    private readonly WebApplication app;

    private readonly WebApplicationBuilder builder;
    private readonly ProgramConfig programConfig;
    private readonly WebServerConfig serverConfig;

    private byte[]? board;
    private int lastBackup;
    
    public WebServer(ProgramConfig programConfig, WebServerConfig serverConfig)
    {
        this.programConfig = programConfig;
        this.serverConfig = serverConfig;
        lastBackup = new DateTimeOffset().Millisecond;

        builder = WebApplication.CreateBuilder();
        builder.Configuration["Kestrel:Certificates:Default:Path"] = programConfig.CertPath;
        builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = programConfig.KeyPath;
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => { policy.WithOrigins(programConfig.Origin, "*"); });
        });

        app = builder.Build();
        app.Urls.Add($"{(programConfig.Ssl ? "https" : "http")}://*:{programConfig.HttpPort}");
        app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(_ => true).AllowCredentials());
    }

    public async Task Start()
    {
        app.MapGet("/", () => Results.Content(indexTemplate, "text/html", Encoding.Unicode));

        //Serve absolute latest board from memory.
        app.MapGet("/place", () => Results.Bytes(board ?? Array.Empty<byte>()));

        // Lists all available backups. 
        app.MapGet("/backups", () =>
        {
            Task.FromResult(Results.Content(backuplistTemplate, "text/html"));
        });

        // To download a specific backup.
        app.MapGet("/backups/{placeFile}", (string placeFile) =>
        {
            var stream = new FileStream(Path.Join(programConfig.CanvasFolder, placeFile), FileMode.Open);
            return Results.File(stream);
        });

        app.MapGet("/backuplist", async () =>
            await File.ReadAllTextAsync(Path.Join(programConfig.CanvasFolder, "backuplist.txt"))
        );

        /*app.MapPost("/timelapse", async (TimelapseInformation timelapseInfo) =>
        {
            var stream = await TimelapseGenerator.GenerateTimelapseAsync(timelapseInfo.BackupStart, timelapseInfo.BackupEnd, timelapseInfo.Fps, 750, timelapseInfo.StartX, timelapseInfo.StartY, timelapseInfo.EndX, timelapseInfo.EndY, timelapseInfo.Reverse);
            return Results.File(stream);
        });*/

        await app.RunAsync();
    }

    public async Task IncomingBoard(byte[] canvas)
    {
        //If it has been more than backup time, create a new backup
        if (new DateTimeOffset().Millisecond - lastBackup > serverConfig.BackupFrequency)
        {
            var backupName = "place." + DateTime.Now.ToString("dd.MM.yyyy.HH:mm:ss");
            await using var file = new StreamWriter(Path.Join(programConfig.CanvasFolder, "backuplist.txt"), append: true);
            await file.WriteLineAsync(backupName);

            await using var backupStream = File.Open(backupName, FileMode.OpenOrCreate);
            backupStream.Seek(0, SeekOrigin.End);
            await backupStream.WriteAsync(board);
        }
        
        board = canvas;
    }
}