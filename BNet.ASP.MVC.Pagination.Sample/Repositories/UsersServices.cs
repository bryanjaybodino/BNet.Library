using BNet.ASP.MVC.Pagination.Sample.Models;
using Microsoft.EntityFrameworkCore;

namespace BNet.ASP.MVC.Pagination.Sample.Repositories
{
    public class UsersServices : IUsersServices
    {

        private readonly MyDBContext context;
        public UsersServices(MyDBContext context)
        {
            this.context = context;
        }
        public async Task<List<users_table.dataDTO>> GetAllAsync(string search)
        {
            var users = new List<users_table>();
            var data = new List<users_table.dataDTO>();
            if (search == null || search == "")
            {
                users = await context.users_table.ToListAsync();
            }
            else
            {
                users = await context.users_table.Where(x => x.DBFirstName.Contains(search) || x.DBLastName.Contains(search)).ToListAsync();
            }
            foreach (var user in users)
            {
                data.Add(new users_table.dataDTO
                {
                    id = user.DBID,
                    firstname = user.DBFirstName,
                    lastname = user.DBLastName,
                    age = user.DBAge,
                    datecreated = user.DBDateCreated
                });
            }
            return data;
        }

    }
}
