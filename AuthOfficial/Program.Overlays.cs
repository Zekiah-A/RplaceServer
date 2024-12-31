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
    private static void MapOverlayEndpoints(this WebApplication app)
    {
        app.MapGet("/overlays", () =>
        {
            throw new NotImplementedException();
        });

        app.MapPost("/overlays", () =>
        {
            throw new NotImplementedException();
        });

        app.MapGet("/overlays/{id:int}", () =>
        {
            throw new NotImplementedException();
        });

        app.MapDelete("/overlays/{id:int}", () =>
        {
            throw new NotImplementedException();
        });
    }
}
