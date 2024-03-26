using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBP_I_PROJEKAT.Models
{
    public class Poruka
    {

        public Poruka(int od,string korisnickoIme, string sadrzaj)
        {
            this.Od = od;
            this.KorisnickoIme = korisnickoIme;
            this.Sadrzaj = sadrzaj;
        }

        public int Od { get; set; }

        public string KorisnickoIme;
        public string Sadrzaj { get; set; }
    }
}
