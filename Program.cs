using Matchmaking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.Run();
