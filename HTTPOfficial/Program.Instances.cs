using HTTPOfficial.ApiModel;
using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial;

internal static partial class Program
{
    private static void ConfigureInstanceEndpoints()
    {
        app.MapGet("/instances/users/{id:int}", async (int id, DatabaseContext database) =>
        {
            var canvasUser = await database.CanvasUsers.FindAsync(id);
            if (canvasUser is null)
            {
                return Results.NotFound(new ErrorResponse("Specified canvas user does not exist",
                    "instances.users.notFound"));
            }
            var instance = await database.Instances.FindAsync(canvasUser.InstanceId);
            if (instance is null)
            {
                return Results.NotFound(new ErrorResponse("This user's host instance could not be located",
                    "instances.users.instanceNotFound"));
            }

            var protocol = instance.UsesHttps ? "http://" : "https://";
            var endpointLocation = $"{protocol}{instance.ServerLocation}/users/{canvasUser.UserIntId}";
            var instanceUser = await httpClient.GetFromJsonAsync<InstanceUserResponse>(endpointLocation, defaultJsonOptions);
            if (instanceUser is null)
            {
                return Results.NotFound(new ErrorResponse("Error when communicating with this users host instance",
                    "instances.users.instanceError"));
            }

            var instanceUserLastJoined = DateTimeOffset.FromUnixTimeMilliseconds(instanceUser.LastJoined).DateTime;
            var canvasUserResponse = new CanvasUserResponse(canvasUser.Id, canvasUser.UserIntId, canvasUser.InstanceId,
                canvasUser.AccountId, instanceUser.ChatName, instanceUserLastJoined, instanceUser.PixelsPlaced, instanceUser.PlayTimeSeconds);
            return Results.Ok(canvasUserResponse);
        });
    }
}