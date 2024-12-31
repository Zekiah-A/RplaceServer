using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AuthOfficial.ApiModel;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using AuthOfficial.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthOfficial;

internal static partial class Program
{    
    private static void MapForumEndpoints(this WebApplication app)
    {
        app.MapGet("/forums", () =>
        {
            throw new NotImplementedException();
        });

        app.MapGet("/forums/{id:int}", () =>
        {
            throw new NotImplementedException();
        });

        app.MapPatch("/forums/{id:int}", () =>
        {
            throw new NotImplementedException();
        });

        app.MapGet("/forums/{id:int}/posts", () =>
        {
            throw new NotImplementedException();
        });
    }
}
