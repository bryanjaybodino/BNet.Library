using BNet.ASP.MVC.Pagination.Sample.Repositories;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<MyDBContext>();
builder.Services.AddScoped<IUsersServices, UsersServices>();
builder.Services.AddSession();  // SESSION IS REQUIRED TO ADD SEARCH FILTER
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseSession(); // SESSION IS REQUIRED TO ADD SEARCH FILTER

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();