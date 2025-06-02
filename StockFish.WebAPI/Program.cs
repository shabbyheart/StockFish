using StockFish.WebAPI.Services.Stockfish;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<StockfishEnginePool>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var path = config["Stockfish:Path"];
    var count = int.Parse(config["Stockfish:InstanceCount"]);
    return new StockfishEnginePool(path, count);
});
builder.Services.AddScoped<IStockfishService, StockfishService>();
builder.Services.AddSingleton<IStockfishRequestQueue, StockfishRequestQueue>();
builder.Services.AddHostedService<StockfishRequestProcessor>();
//builder.Services.AddHostedService(sp => sp.GetRequiredService<StockfishRequestProcessor>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
