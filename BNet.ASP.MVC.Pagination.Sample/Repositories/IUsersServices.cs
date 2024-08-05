using BNet.ASP.MVC.Pagination.Sample.Models;

namespace BNet.ASP.MVC.Pagination.Sample.Repositories
{
    public interface IUsersServices
    {
        public Task<List<users_table.dataDTO>> GetAllAsync(string search);
    }
}
