using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBP_I_PROJEKAT.Models;
namespace NBP_I_PROJEKAT.Models
{
    public class Konverzacija
    {
        public string Id;
        public string NazivObjave;
        public Konverzacija(string id, string nazivObjave)
        {
            this.Id = id;
            this.NazivObjave = nazivObjave;
        }

        public Poruka[] VratiPoruke() => RedisManager<Poruka>.GetAll($"poruke:{Id}");
        
        public void SaljiPoruku(Poruka poruka)
        {
            RedisManager<Poruka>.Push($"poruke:{Id}", poruka);
        }
    }
}
