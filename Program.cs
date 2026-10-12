using CharlesSchwabAssessment.Components;
using CharlesSchwabAssessment.Models;
using CharlesSchwabAssessment.Orchestration;
using CharlesSchwabAssessment.Security;
using CharlesSchwabAssessment.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Register shortener and dependencies
builder.Services.AddSingleton<IUrlRepository, InMemoryUrlRepository>();
builder.Services.AddSingleton<ShortCodeGenerator>();
builder.Services.AddSingleton<IUrlShortenerService, UrlShortenerService>();
builder.Services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();
builder.Services.AddSingleton<IApprovalService, InMemoryApprovalService>();
builder.Services.AddSingleton<TimeProvider>(_ => TimeProvider.System);
// Program.cs
// Register factory used by WorkflowOrchestrator
builder.Services.AddSingleton<CharlesSchwabAssessment.Orchestration.WorkflowTemplateFactory>(); 

// Register policy guardrail implementation
builder.Services.AddSingleton<IWorkflowPolicyGuardrail, DefaultWorkflowPolicyGuardrail>();

// Register agent decision engine used for judgment-based orchestration steps.
builder.Services.AddSingleton<IAgentDecisionEngine, HeuristicAgentDecisionEngine>();

// Secure the orchestration/approval API surface with an API key scheme.
// Production deployments should replace this with a proper identity provider.
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme,
        options =>
        {
            options.ApiKey = builder.Configuration["Security:OrchestrationApiKey"] ?? string.Empty;
        });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrchestrationAccess", policy =>
        policy.RequireAuthenticatedUser()
              .AddAuthenticationSchemes(ApiKeyAuthenticationOptions.DefaultScheme));
});

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
 

app.MapPost("/api/urls", async (
    ShortenUrlRequest request,
    HttpContext context,
    IUrlShortenerService shortener,
    CancellationToken cancellationToken) =>
{
    try
    {
        var baseUri = new Uri($"{context.Request.Scheme}://{context.Request.Host}/");
        var response = await shortener.ShortenAsync(request, baseUri, cancellationToken);
        return Results.Ok(response);
    }
    catch (UrlShorteningException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/u/{code}", async Task<IResult> (
    string code,
    IUrlShortenerService shortener,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var record = await shortener.GetByCodeAsync(code, cancellationToken);
    if (record is null)
    {
        return Results.NotFound(new { error = "Short code not found." });
    }

    if (record.IsExpired(timeProvider.GetUtcNow()))
    {
        return Results.StatusCode(StatusCodes.Status410Gone);
    }

    await shortener.RegisterRedirectAsync(code, timeProvider.GetUtcNow(), cancellationToken);
    return Results.Redirect(record.OriginalUrl.ToString(), permanent: false);
});

app.MapGet("/api/urls/{code}/analytics", async (
    string code,
    IUrlShortenerService shortener,
    CancellationToken cancellationToken) =>
{
    var analytics = await shortener.GetAnalyticsAsync(code, cancellationToken);
    return analytics is null ? Results.NotFound() : Results.Ok(analytics);
});

var orchestrationGroup = app.MapGroup("/api/orchestration")
    .RequireAuthorization("OrchestrationAccess");

orchestrationGroup.MapPost("/runs", async (
    WorkflowRunRequest request,
    IWorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var result = await orchestrator.StartAsync(request, cancellationToken);
    return Results.Ok(result);
});

orchestrationGroup.MapPost("/runs/{workflowId}/resume", async (
    string workflowId,
    IWorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var result = await orchestrator.ResumeAsync(workflowId, cancellationToken);
    return Results.Ok(result);
});

orchestrationGroup.MapPost("/runs/{workflowId}/safe-stop", async (
    string workflowId,
    IWorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var stopped = await orchestrator.RequestSafeStopAsync(workflowId, cancellationToken);
    return stopped ? Results.Accepted() : Results.NotFound();
});

orchestrationGroup.MapPost("/runs/{workflowId}/replan", async (
    string workflowId,
    ReplanRequest request,
    IWorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var updatedArtifacts = request.UpdatedArtifacts.ToDictionary(
        pair => pair.Key,
        pair => (object?)pair.Value,
        StringComparer.OrdinalIgnoreCase);

    var result = await orchestrator.ReplanFromNodeAsync(workflowId, request.ChangedNodeId, updatedArtifacts, cancellationToken);
    return Results.Ok(result);
});

orchestrationGroup.MapGet("/runs", async (IWorkflowOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var snapshots = await orchestrator.GetAllSnapshotsAsync(cancellationToken);
    return Results.Ok(snapshots);
});

orchestrationGroup.MapGet("/runs/{workflowId}", async (
    string workflowId,
    IWorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var snapshot = await orchestrator.GetSnapshotAsync(workflowId, cancellationToken);
    return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
});

orchestrationGroup.MapGet("/approvals", async (IApprovalService approvalService, CancellationToken cancellationToken) =>
{
    var pending = await approvalService.GetPendingAsync(cancellationToken);
    return Results.Ok(pending);
});

orchestrationGroup.MapPost("/approvals/approve", async (
    ApproveRequest request,
    IApprovalService approvalService,
    CancellationToken cancellationToken) =>
{
    var approved = await approvalService.ApproveAsync(request.ApprovalId, cancellationToken);
    return approved ? Results.Accepted() : Results.NotFound();
});

app.Run();

/// <summary>
/// Exposes the implicit Program entry point so integration tests can host the app via WebApplicationFactory.
/// </summary>
public partial class Program;

