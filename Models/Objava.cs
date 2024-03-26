using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBP_I_PROJEKAT.Models
{
    public class Objava
    {
        public int Id { get; set; }
        public string Naziv { get; set; }
        public string Sadrzaj { get; set; }
        public string Tag { get; set; } 
        public DateTime DatumKreiranja { get; set; }
        public Korisnik Korisnik { get; set; }
        public Poruka [] Poruke { get; set; }
       
    }
}
