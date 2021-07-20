using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EfRest.Example.Model
{
    public class Book
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = "";

        public string? Description { get; set; }

        [NotMapped]
        public string? AuthorNames { get; set; }

        public ICollection<Author>? Authors { get; set; }
        public ICollection<Genre>? Genres { get; set; }
        public BookDetail? BookDetail { get; set; }
    }
}
