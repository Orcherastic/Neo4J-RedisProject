using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBP_I_PROJEKAT.Models;
using Neo4j.Driver;
using NBP_I_PROJEKAT.Extensions;
using NBP_I_PROJEKAT.Hubs;
using NBP_I_PROJEKAT.Session;
namespace NBP_I_PROJEKAT.Controllers
{
    public class KonverzacijaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        private readonly IDriver _driver;

        public KonverzacijaController(IDriver dirver)
        {
            _driver = dirver;
        }

        [HttpGet]
        [Route("VratiPoruke")]
        public IActionResult VratiPoruke(string idObjave)
        {
            //Konverzacija k = RedisManager<Konverzacija>.GetString($"objave:{idKonverzacije}:konverzacija");
            Poruka[] poruke = RedisManager<Poruka>.GetAll($"poruke:{idObjave}");

            return Ok(poruke);
        }

        [HttpGet]
        [Route("SaljiPoruku")]
        public IActionResult SaljiPoruku(string idObjave, string korisnickoIme ,string sadrzaj)
        {
            int userId = HttpContext.Session.GetUserId();
            if (!HttpContext.Session.IsLoggedIn())
            {
                return BadRequest("Niste logovani");
            }
            //Konverzacija k = RedisManager<Konverzacija>.GetString($"objave:{idKonverzacije}:konverzacija");
            //k.SaljiPoruku(poruka);
            Poruka poruka = new Poruka(userId, korisnickoIme, sadrzaj); 
            RedisManager<Poruka>.Push($"poruke:{idObjave}", poruka);

            return Ok(poruka);
        }

        [HttpPost]
        [Route("NapraviKonverzaciju")]
        public IActionResult NapraviKonverzaciju(string idObjave, string nazivObjave)
        {
            if (!HttpContext.Session.IsLoggedIn())
            {
                return BadRequest("Niste logovani");
            }
            var k = new Konverzacija(idObjave, nazivObjave);
            RedisManager<Konverzacija>.SetString($"objave:{idObjave}:konverzacija", k);
            return Ok(k);
        }

    }
}
