using System.Text.Json.Serialization;
using CodeModernizer.Api;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;
using CodeModernizer.Infrastructure.Diff;
using CodeModernizer.Infrastructure.Providers;
using CodeModernizer.Infrastructure.Services;
using CodeModernizer.Infrastructure.Sessions;
using CodeModernizer.Infrastructure.Skills;

var builder = WebApplication.CreateBuilder(args);

// Optional local config for secrets (gitignored); overrides appsettings.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Key resolution: appsettings(.Local).json → ANTHROPIC_API_KEY env var (SDK default).
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"]?.Trim();
var hasApiKey = !string.IsNullOrEmpty(anthropicApiKey)
    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<ISkillRegistry>(_ =>
    new FileSkillRegistry(ResolveSkillsDirectory(builder)));
builder.Services.AddSingleton<IAiProvider>(_ => new ClaudeProvider(anthropicApiKey));
builder.Services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
builder.Services.AddSingleton<IDiffService, DiffService>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<ModernizationService>();

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/config", (ISkillRegistry skills, IAiProviderRegistry providers) =>
    new ConfigDto(
        skills.Skills.Select(s => new SkillDto(s.Id, s.DisplayName, s.Language, s.TargetVersion, s.FileExtensions)).ToList(),
        providers.Providers.Select(p => new AiProviderInfo(p.Id, p.DisplayName, p.Models)).ToList(),
        hasApiKey));

// Server-side directory listing backing the frontend's folder picker (a browser
// cannot read absolute paths from the native file dialog).
api.MapGet("/browse", (string? path) =>
{
    var target = string.IsNullOrWhiteSpace(path)
        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        : path;
    try
    {
        var full = Path.GetFullPath(target);
        if (!Directory.Exists(full))
            return Results.BadRequest(new { error = $"Directory not found: {full}" });

        var directories = new DirectoryInfo(full).EnumerateDirectories()
            .Where(d => !d.Name.StartsWith('.'))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new DirectoryEntryDto(d.Name, d.FullName))
            .ToList();
        return Results.Ok(new BrowseDto(full, Path.GetDirectoryName(full), directories));
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapPost("/sessions", (StartSessionRequest request, ModernizationService service) =>
{
    try
    {
        var session = service.Start(
            request.ProjectPath, request.SkillId, request.ProviderId,
            request.AgentModelId, request.ReviewModelId);
        return Results.Ok(session.ToDto());
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or KeyNotFoundException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/sessions/{sessionId}", (string sessionId, ISessionStore store) =>
    store.Get(sessionId) is { } session ? Results.Ok(session.ToDto()) : Results.NotFound());

api.MapGet("/sessions/{sessionId}/files/{fileId}", (string sessionId, string fileId, ISessionStore store) =>
    FindFile(store, sessionId, fileId) is { } file ? Results.Ok(file.ToDetailDto()) : Results.NotFound());

api.MapPost("/sessions/{sessionId}/files/{fileId}/hunks/{hunkId:int}",
    (string sessionId, string fileId, int hunkId, HunkDecisionRequest request, ISessionStore store) =>
{
    if (FindFile(store, sessionId, fileId) is not { } file) return Results.NotFound();
    lock (file.SyncRoot)
    {
        if (file.Hunks.FirstOrDefault(h => h.Id == hunkId) is not { } hunk) return Results.NotFound();
        hunk.Decision = request.Decision;
    }
    return Results.Ok(file.ToDetailDto());
});

api.MapPost("/sessions/{sessionId}/files/{fileId}/accept", (string sessionId, string fileId, ISessionStore store) =>
    SetAllHunks(store, sessionId, fileId, HunkDecision.Accepted));

api.MapPost("/sessions/{sessionId}/files/{fileId}/revert", (string sessionId, string fileId, ISessionStore store) =>
    SetAllHunks(store, sessionId, fileId, HunkDecision.Rejected));

api.MapPost("/sessions/{sessionId}/files/{fileId}/adjust",
    (string sessionId, string fileId, AdjustRequest request, ISessionStore store, ModernizationService service) =>
{
    if (store.Get(sessionId) is not { } session) return Results.NotFound();
    if (session.Files.FirstOrDefault(f => f.Id == fileId) is not { } file) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Instructions))
        return Results.BadRequest(new { error = "Instructions are required." });

    // Runs in the background; the client polls the file until Status leaves Modernizing.
    _ = service.AdjustFileAsync(session, file, request.Instructions);
    return Results.Accepted();
});

api.MapPost("/sessions/{sessionId}/review", (string sessionId, ISessionStore store, ModernizationService service) =>
{
    if (store.Get(sessionId) is not { } session) return Results.NotFound();
    if (session.Status is SessionStatus.Scanning or SessionStatus.Running)
        return Results.BadRequest(new { error = "Session is still running." });

    // Runs in the background; the client polls the session for Review + status.
    _ = service.ReviewAsync(session);
    return Results.Accepted();
});

api.MapPost("/sessions/{sessionId}/apply", (string sessionId, ISessionStore store, ModernizationService service) =>
{
    if (store.Get(sessionId) is not { } session) return Results.NotFound();
    var written = service.Apply(session);
    return Results.Ok(new { written });
});

// SPA fallback for the React frontend served out of wwwroot.
app.MapFallbackToFile("index.html");

app.Run();

static FileChange? FindFile(ISessionStore store, string sessionId, string fileId) =>
    store.Get(sessionId)?.Files.FirstOrDefault(f => f.Id == fileId);

static IResult SetAllHunks(ISessionStore store, string sessionId, string fileId, HunkDecision decision)
{
    if (FindFile(store, sessionId, fileId) is not { } file) return Results.NotFound();
    lock (file.SyncRoot)
    {
        foreach (var hunk in file.Hunks) hunk.Decision = decision;
    }
    return Results.Ok(file.ToDetailDto());
}

static string ResolveSkillsDirectory(WebApplicationBuilder builder)
{
    var configured = builder.Configuration["SkillsDirectory"] ?? "skills";
    if (Path.IsPathRooted(configured)) return configured;

    // Probe the content root and its ancestors so the app works whether it is
    // launched via `dotnet run`, from the repo root, or from the build output.
    var dir = builder.Environment.ContentRootPath;
    for (var i = 0; i < 6 && dir is not null; i++)
    {
        var candidate = Path.Combine(dir, configured);
        if (Directory.Exists(candidate)) return candidate;
        dir = Path.GetDirectoryName(dir);
    }

    return Path.Combine(builder.Environment.ContentRootPath, configured);
}
