using BNet.ASP.MVC.Pagination.Sample.Models;
using BNet.ASP.MVC.Pagination.Sample.Repositories;
using Microsoft.AspNetCore.Mvc;

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


        #region THIS IS THE DEFAULT PATTERN
        BNet.ASP.MVC.Pagination.GridView gridView = new GridView();
        private void setPagination(List<users_table.dataDTO> users_table, string NewPageIndex = "0")
        {
            //htmlTable_Users <<--- THIS IS THE ID OF YOUR HTML TABLE IN YOUR VIEW
            gridView.FirstAndLast = true; //OPTIONAL
            gridView.SetGriView(users_table, "Home/Pagination", "htmlTable_Users", 5, 5, NewPageIndex);
            gridView.PaginationChanged += GridView_PaginationChanged;
        }

        //YOU CAN MODIFY THE ROUTE NAME
        [Route("Home/Pagination")] //THIS IS THE ROUTE NAME -> PUT THIS TO YOUR SetGridView() 
        [HttpPost]
        public async Task<IActionResult> GridView_PaginationChanged(string NewPageIndex)
        {
            // IN MY EXAMPLE I HAVE 1 SEARCH FILTER. YOU CAN ADD MORE ARGUMENTS IN YOUR SERVICES
            string Search = HttpContext.Session.GetString("Search"); // YOU MUST USE SESSION FOR SEARCH 
            List<users_table.dataDTO> users_table = await usersServices.GetAllAsync(Search);
            setPagination(users_table, NewPageIndex);
            return PartialView("~/Views/Home/Index.cshtml", gridView);
        }
        #endregion



        #region YOUR INDEX MUST LOOK LIKE THIS
        public async Task<IActionResult> IndexAsync(string? data = "")
        {
            HttpContext.Session.SetString("Search", data); // CALL THE 
            List<users_table.dataDTO> users_table = await usersServices.GetAllAsync(data);
            setPagination(users_table);
            return View("~/Views/Home/Index.cshtml", gridView);
        }
        #endregion
    }
}
