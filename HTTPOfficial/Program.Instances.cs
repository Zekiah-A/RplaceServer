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
            InstanceUserResponse? instanceUser = null;
            try
            {
                instanceUser = await httpClient.GetFromJsonAsync<InstanceUserResponse>(endpointLocation, defaultJsonOptions);
            }
            catch(Exception exception)
            {
                logger.LogError("Failed to request user info from instance {endpointLocation}, {exception}", endpointLocation, exception);
            }
            if (instanceUser is null)
            {
                return Results.NotFound(new ErrorResponse("Specified user does not exist on their host instance",
                    "instances.users.instanceUserNotFound"));
            }

            var instanceUserLastJoined = DateTimeOffset.FromUnixTimeMilliseconds(instanceUser.LastJoined).DateTime;
            var canvasUserResponse = new CanvasUserResponse(canvasUser.Id, canvasUser.UserIntId, canvasUser.InstanceId,
                canvasUser.AccountId, instanceUser.ChatName, instanceUserLastJoined, instanceUser.PixelsPlaced, instanceUser.PlayTimeSeconds);
            return Results.Ok(canvasUserResponse);
        });
    }
}
