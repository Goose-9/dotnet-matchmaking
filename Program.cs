using Matchmaking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

// Strategy + engine
builder.Services.AddSingleton<IMatchmakingStrategy, FifoQueueStrategy>();
builder.Services.AddSingleton<MatchmakingEngine>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MatchmakingEngine>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();
