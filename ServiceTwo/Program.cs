var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddOpenApi("v1");
var app = builder.Build();
app.MapOpenApi();


app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();