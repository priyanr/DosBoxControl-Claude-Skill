using DosBoxControl;
using DosBoxControl.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<DosBoxSessionManager>();
builder.Services.AddSingleton<WindowCaptureService>();
builder.Services.AddSingleton<KeyboardInputService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/open", async (OpenRequest req, DosBoxSessionManager sessions, CancellationToken ct) =>
{
    var session = await sessions.OpenAsync(req.ExePath, req.Args, req.Fullscreen, ct);
    return Results.Ok(new OpenResponse(session.Process.Id, (long)session.WindowHandle, session.WindowTitle));
})
.WithName("OpenExe")
.WithOpenApi();

app.MapPost("/api/close", async (CloseRequest? req, DosBoxSessionManager sessions, CancellationToken ct) =>
{
    await sessions.CloseAsync(req?.Force ?? false, ct);
    return Results.Ok();
})
.WithName("Close")
.WithOpenApi();

app.MapGet("/api/status", (DosBoxSessionManager sessions) =>
{
    var s = sessions.Current;
    return Results.Ok(new StatusResponse(
        Running: s is not null,
        Pid: s?.Process.Id,
        ExePath: s?.ExePath,
        WindowTitle: s?.WindowTitle,
        WindowHandle: s is null ? null : (long)s.WindowHandle));
})
.WithName("Status")
.WithOpenApi();

app.MapGet("/api/screenshot", (DosBoxSessionManager sessions, WindowCaptureService capture) =>
{
    var session = sessions.Current;
    if (session is null)
    {
        return Results.NotFound(new { error = "No active DOSBox session. Call /api/open first." });
    }

    var png = capture.CaptureAsPng(session.WindowHandle);
    return Results.File(png, "image/png");
})
.WithName("Screenshot")
.WithOpenApi();

app.MapPost("/api/keys", (KeysRequest req, DosBoxSessionManager sessions, KeyboardInputService keyboard) =>
{
    var session = sessions.Current;
    if (session is null)
    {
        return Results.NotFound(new { error = "No active DOSBox session. Call /api/open first." });
    }

    if (!string.IsNullOrEmpty(req.Text))
    {
        keyboard.SendText(session.WindowHandle, req.Text, req.DelayMs);
    }

    if (req.Keys is { Length: > 0 })
    {
        keyboard.SendKeySequence(session.WindowHandle, req.Keys, req.DelayMs);
    }

    return Results.Ok();
})
.WithName("SendKeys")
.WithOpenApi();

app.Run();
