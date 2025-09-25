using Matchmaking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Strategy registration: swap here later for Latency/Elo strategies
builder.Services.AddSingleton<IMatchmakingStrategy, FifoQueueStrategy>();

// Engine is a hosted background service
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
