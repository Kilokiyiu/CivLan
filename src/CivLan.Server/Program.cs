using CivLan.Application.Options;
using CivLan.Application.Services;
using CivLan.Domain.Exceptions;
using CivLan.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CivLanOptions>(builder.Configuration.GetSection(CivLanOptions.SectionName));
builder.Services.Configure<WireGuardOptions>(builder.Configuration.GetSection(WireGuardOptions.SectionName));
builder.Services.AddInfrastructure();

builder.WebHost.ConfigureKestrel(options =>
{
    var port = builder.Configuration.GetValue("Server:Port", 5199);
    options.ListenAnyIP(port);
});

var app = builder.Build();

app.UseStaticFiles();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/health"))
    {
        var configuredKey = app.Configuration.GetValue<string>($"{CivLanOptions.SectionName}:ServerApiKey");
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
                provided != configuredKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
                return;
            }
        }
    }

    await next();
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "CivLan" }));

app.MapGet("/api/rooms", async (RoomAppService service, CancellationToken ct) =>
{
    var rooms = await service.ListOpenRoomsAsync(ct);
    return Results.Ok(rooms);
});

app.MapGet("/api/rooms/{code}", async (string code, RoomAppService service, CancellationToken ct) =>
{
    try
    {
        var room = await service.GetRoomAsync(code, ct);
        return Results.Ok(room);
    }
    catch (DomainException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/rooms", async ([FromBody] CreateRoomRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        var result = await service.CreateRoomAsync(request.RoomName, request.PlayerName, ct);
        return Results.Ok(result);
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/rooms/{code}/join", async (string code, [FromBody] JoinRoomRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        var result = await service.JoinRoomAsync(code, request.PlayerName, request.AccessToken, ct);
        return Results.Ok(result);
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/rooms/{code}/config", async (string code, [FromBody] TokenRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        var config = await service.GetClientConfigAsync(code, request.AccessToken, ct);
        return Results.Ok(config);
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/rooms/{code}/host", async (string code, [FromBody] SetHostRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        var room = await service.SetHostAsync(code, request.AccessToken, request.HostPeerId, ct);
        return Results.Ok(room);
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/rooms/{code}/leave", async (string code, [FromBody] TokenRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        await service.LeaveRoomAsync(code, request.AccessToken, ct);
        return Results.Ok(new { message = "Left room." });
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/rooms/{code}/heartbeat", async (string code, [FromBody] TokenRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        await service.HeartbeatAsync(code, request.AccessToken, ct);
        return Results.Ok(new { message = "ok" });
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/rooms/{code}", async (string code, [FromBody] TokenRequest request, RoomAppService service, CancellationToken ct) =>{
    try
    {
        await service.CloseRoomAsync(code, request.AccessToken, ct);
        return Results.Ok(new { message = "Room closed." });
    }
    catch (DomainException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

internal sealed record CreateRoomRequest(string RoomName, string PlayerName);
internal sealed record JoinRoomRequest(string PlayerName, string? AccessToken = null);
internal sealed record TokenRequest(string AccessToken);
internal sealed record SetHostRequest(string AccessToken, Guid HostPeerId);
