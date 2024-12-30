using AuthOfficial.ApiModel;
using Microsoft.EntityFrameworkCore;

namespace AuthOfficial;

internal static partial class Program
{
    private static void MapInstanceEndpoints(this WebApplication app)
    {
        app.MapGet("/instances/vanity/{vanityName}", async (string vanityName, DatabaseContext database) =>
        {
            var instance = await database.Instances.FirstOrDefaultAsync(instance => instance.VanityName == vanityName);
            if (instance is null)
            {
                return Results.NotFound(new ErrorResponse("Specified canvas instance does not exist",
                    "instances.vanity.notFound"));
            }

            return Results.Ok(instance);
        });
        
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

            var protocol = instance.UsesHttps ? "https://" : "http://";
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
        
        app.MapGet("/instances/overlays/{id:int}", async (int id, DatabaseContext database) =>
        {
            // TODO: Query canvas server for their list of overlays
            throw new NotImplementedException();
        });

        app.MapGet("/instances/{instanceId:int}/overlays", async (int instanceId, int id, DatabaseContext database) =>
        {
            // TODO: Query canvas server for their list of overlays
            throw new NotImplementedException();
        });
    }
}
