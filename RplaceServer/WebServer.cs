using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RplaceServer;

internal class WebServer
{
    private const string backuplistTemplate =
    @"
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

    private const string indexTemplate =
    @"
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
        <p>©Zekiah-A - rplace.tk ❤️</p>
    ";

    private readonly WebApplication app;
    private readonly WebApplicationBuilder builder;
    private readonly GameData gameData;

    public WebServer(GameData data, string certPath, string keyPath, string origin, bool ssl, int port)
    {
        gameData = data;

        builder = WebApplication.CreateBuilder();
        builder.Configuration["Kestrel:Certificates:Default:Path"] = certPath;
        builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = keyPath;
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => { policy.WithOrigins(origin, "*"); });
        });

        app = builder.Build();
        app.Urls.Add($"{(ssl ? "https" : "http")}://*:{port}");
        app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(_ => true).AllowCredentials());
    }

    public async Task Start()
    {
        app.MapGet("/", () => Results.Content(indexTemplate, "text/html", Encoding.Unicode));

        //Serve absolute latest board from memory.
        app.MapGet("/place", () => Results.Bytes(gameData.Board));

        // Lists all available backups. 
        app.MapGet("/backups", () =>
        {
            Task.FromResult(Results.Content(backuplistTemplate, "text/html"));
        });

        // To download a specific backup.
        app.MapGet("/backups/{placeFile}", (string placeFile) =>
        {
            var stream = new FileStream(Path.Join(gameData.CanvasFolder, placeFile), FileMode.Open);
            return Results.File(stream);
        });

        app.MapGet("/backuplist", async () =>
            await File.ReadAllTextAsync(Path.Join(gameData.CanvasFolder, "backuplist.txt"))
        );

        //TODO: Implement StarlkYT's timelapse generator
        /*app.MapPost("/timelapse", async (TimelapseInformation timelapseInfo) =>
        {
            var stream = await TimelapseGenerator.GenerateTimelapseAsync(timelapseInfo.BackupStart, timelapseInfo.BackupEnd, timelapseInfo.Fps, 750, timelapseInfo.StartX, timelapseInfo.StartY, timelapseInfo.EndX, timelapseInfo.EndY, timelapseInfo.Reverse);
            return Results.File(stream);
        });*/

        await app.RunAsync();
        await HandleBoardBackups();
    }

    public async Task HandleBoardBackups()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(gameData.BackupFrequency));

        while (await timer.WaitForNextTickAsync())
        {
            var backupName = "place." + DateTime.Now.ToString("dd.MM.yyyy.HH:mm:ss");
            await using var file = new StreamWriter(Path.Join(gameData.CanvasFolder, "backuplist.txt"), append: true);
            await file.WriteLineAsync(backupName);

            await using var backupStream = File.Open(backupName, FileMode.OpenOrCreate);
            backupStream.Seek(0, SeekOrigin.End);
            await backupStream.WriteAsync(gameData.Board);
        }
    }
}