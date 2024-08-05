using System.ComponentModel.DataAnnotations;

namespace BNet.ASP.MVC.Pagination.Sample.Models
{
    public class users_table
    {
        [Key]
        public int DBID { get; set; }
        public string? DBFirstName { get; set; }
        public string? DBLastName { get; set; }
        public string? DBAge { get; set; }
        public string? DBDateCreated { get; set; }

        public class dataDTO
        {
            public int id { get; set; }
            public string? firstname { get; set; }
            public string? lastname { get; set; }
            public string? age { get; set; }
            public string? datecreated { get; set; }
        }
    }
}
