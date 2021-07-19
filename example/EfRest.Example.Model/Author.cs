using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfRest.Example.Model
{
    public class Author
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public ICollection<Book>? Books { get; set; }
    }
}
