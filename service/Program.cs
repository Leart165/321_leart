var builder = WebApplication.CreateBuilder(args);



var app = builder.Build();


app.UseHttpsRedirection();


app.MapGet("/world", () =>
{

    return "world!";
})
.WithName("world")
.WithOpenApi();

app.Run();

