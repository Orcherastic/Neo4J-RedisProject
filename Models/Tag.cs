using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBP_I_PROJEKAT.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Naziv { get; set; }
        public List<Objava> Objave { get; set; }

        public Tag()
        {
            Objave = new List<Objava>();
        }
    }
}
