using PSP.Mock.HamrahAval.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockHamrahAvalOptions>(builder.Configuration.GetSection(MockHamrahAvalOptions.SectionName));
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
