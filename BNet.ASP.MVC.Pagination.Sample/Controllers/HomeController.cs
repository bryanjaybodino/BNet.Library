using BNet.ASP.MVC.Pagination.Sample.Models;
using BNet.ASP.MVC.Pagination.Sample.Repositories;
using Microsoft.AspNetCore.Mvc;
using static BNet.ASP.MVC.Pagination.GridView;

namespace BNet.ASP.MVC.Pagination.Sample.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUsersServices usersServices;
        public HomeController(ILogger<HomeController> logger, IUsersServices usersServices)
        {
            _logger = logger;
            this.usersServices = usersServices;
        }

        BNet.ASP.MVC.Pagination.GridView gridView = new GridView();
        public async Task<IActionResult> IndexAsync(string? search,string? test)
        {
            search = search ?? string.Empty;
            HttpContext.Session.SetString("Search", search); // CALL THE 
            List<users_table.dataDTO> users_table = await usersServices.GetAllAsync(search);

            gridView.FirstAndLast = true; //OPTIONAL
            gridView.SetGridView(this.HttpContext, users_table, "Home/Pagination", "htmlTableUsers", 8, 5);
            gridView.PaginationChanged += GridView_PaginationChanged; 
            return View("~/Views/Home/Index.cshtml", gridView);
        }

        //You can modify the route name but make sure it is similar to your SetGridView()
        [Route("Home/Pagination")] //THIS IS THE ROUTE NAME -> PUT THIS TO YOUR SetGridView() 
        [HttpPost]//POST REQUIRED
        public async Task<IActionResult> GridView_PaginationChanged(PaginationChangedEventArgs e)
        {
            string Search = HttpContext.Session.GetString("Search"); // You must use session here to maintain your search filter while paginated
            List<users_table.dataDTO> users_table = await usersServices.GetAllAsync(Search); // Call again your services here
            gridView.NewPagination(this.HttpContext, users_table, e);
            return PartialView("~/Views/Home/Index.cshtml", gridView);
        }
    }
}


// FOR Version 1.0.1 
//call this codes to your View.cshtml For SEARCH FILTER
//htmlTableUsers_SearchEvent({parameterName:'your filter'});

//htmlTableUsers <<--- This is the id of your HTML Tabe in your view.cshtml