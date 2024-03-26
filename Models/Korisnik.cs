using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBP_I_PROJEKAT.Models
{
    public class Korisnik
    {
        public int ID { get; set; }
        public string KorisnickoIme { get; set; }
        public string Sifra { get; set; }
        public string Email { get; set; }
        public string Fakultet { get; set; }
        public string Smer { get; set; }
        public int GodinaStudije { get; set; }
        public List<Objava> Objave { get; set; }
        public List<int> Pratioci { get; set; }
    }
}
