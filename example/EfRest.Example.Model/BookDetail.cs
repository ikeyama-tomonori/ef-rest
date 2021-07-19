using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfRest.Example.Model
{
    public class BookDetail
    {
        public int Id { get; set; }

        public int? TotalPages { get; set; }
        public decimal? Rating { get; set; }
        public string? Isbn { get; set; }
        public DateTime? PublisedDate { get; set; }

        public int? PublisherId { get; set; }
        public Publisher? Publisher { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }
    }
}
