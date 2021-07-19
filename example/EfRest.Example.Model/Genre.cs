using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EfRest.Example.Model
{
    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentGenreId { get; set; }
        public Genre? ParentGenre { get; set; }

        [JsonPropertyName("child_genres")]
        public ICollection<Genre>? ChildGenres { get; set; }

        public ICollection<Book>? Books { get; set; }
    }
}
