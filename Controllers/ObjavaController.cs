using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using StackExchange.Redis;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Neo4jClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Neo4j.Driver;
using NBP_I_PROJEKAT.Session;
using NBP_I_PROJEKAT.Hubs;
using NBP_I_PROJEKAT.Extensions;
using NBP_I_PROJEKAT.Models;
using Microsoft.Extensions.Logging;

namespace NBP_I_PROJEKAT.Controllers
{
   
    public class ObjavaController:Controller
    {
        private readonly IDriver _driver;
        private readonly IConnectionMultiplexer _redis;
        private readonly IHubContext<ObjavaHub> _hub;
        public readonly ILogger<ObjavaController> _logger;


        private static readonly object _lock = new();
        private static bool _firstTimeRun = true;
        public (UserInfo, int)[] Leaderboard => RedisManager<UserInfo>.GetSortedSet("leaderboard", 10);
        public ObjavaController(IDriver driver, IHubContext<ObjavaHub> hub, IConnectionMultiplexer redis, ILogger<ObjavaController> logger)
        {
            _driver = driver;
            _logger = logger;
            _hub = hub;
            _redis = redis;

            
        }
        public IActionResult Index()
        {
            return View();

        }
        [HttpPost]
        [Route("DodajObjavu")]
        public async Task<IActionResult> DodajObjavu(string Naziv, string Sadrzaj, string Tag)//ovo radi xd
        {

            if (!HttpContext.Session.IsLoggedIn())
            {

                return StatusCode(401, "Niste logovani!");
            }
            IResultCursor result;

            var session = _driver.AsyncSession();
            try
            {
                var res = await (await session.RunAsync($"MATCH (t:Tag {{ naziv: '{Tag}' }}) RETURN id(t)")).ToListAsync();
                int tagID = -1;
                if (res.Count == 0)
                {
                    result = await session.RunAsync($"CREATE (tag:Tag {{ naziv: '{Tag}' }}) return id(tag)");
                    tagID = await result.SingleAsync(record => record["id(tag)"].As<int>());
                }
                else
                {
                    tagID = res[0]["id(t)"].As<int>();
                }
                var idUser = HttpContext.Session.GetUserId();
                result = await session.RunAsync($"CREATE (o:Objava {{ naziv: '{Naziv}', sadrzaj: '{Sadrzaj}', tag: '{Tag}', datumkreiranja: '{DateTime.Now}',likes:'{null}',poruke:'{null}'}}) return id(o)");
                var objavaID = await result.SingleAsync(record => record["id(o)"].As<int>());
                if (objavaID == -1)
                    return RedirectToAction("Index", "Home");
                //return BadRequest("Nije kreirana objava");

                session = _driver.AsyncSession();
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(@$"MATCH(t:Tag) WHERE id(t)={tagID} 
                                    MATCH (o:Objava) WHERE id(o)={objavaID} 
                                    CREATE (t)-[:HAS]->(o)"));

                var userId = HttpContext.Session.GetUserId();
                session = _driver.AsyncSession();
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(@$"MATCH(k:Korisnik) WHERE id(k)={userId} 
                                    MATCH (o:Objava) WHERE id(o)={objavaID} 
                                    CREATE (k)-[:POSTED]->(o)"));

                try //to je pubsub mehanizam to cemo posle
                {
                    result = await session.RunAsync($"MATCH p=(k1:Korisnik)-[r:PRATI]->(k2:Korisnik) WHERE id(k2)={userId} RETURN id(k1)");
                    var idList = await result.ToListAsync();
                    var ids = idList?.Select(x => x["id(k1)"].As<string>()).ToArray() ?? Array.Empty<string>();

                    var userName = HttpContext.Session.GetUsername();
                    RedisManager<Notifikacija>.Publish("PostavljenaObjava", new Notifikacija(ids, objavaID, Naziv, userId, userName));
                }
                finally { await session.CloseAsync(); }
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Index", "Home");
        }


     
        [HttpDelete]
        [Route("ObrisiNotifikaciju")]
        public IActionResult ObrisiNotifikaciju(string path, string item)
               => RedisManager<string>.DeleteItem(path, item) ? Ok() : BadRequest();

       
        [HttpGet]
        [Route("PromeniObjavu")]
        public async Task<IActionResult> PromeniObjavu(string id, string naziv, string sadrzaj, string tag)//radi ovo
        {
            if (!HttpContext.Session.IsLoggedIn())
            {
              
                return RedirectToAction("Login", "Home");
            }
            var objavaId = int.Parse(id);
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                _ = await session.RunAsync($"MATCH (o:Objava) WHERE id(o)={objavaId} SET o = {{ naziv: '{naziv}', sadrzaj: '{sadrzaj}', tag: '{tag}' }} ");


            }
            finally
            {
                await session.CloseAsync();
            }
            
            return RedirectToAction("MojeObjave", "Home");
           
        }

       
        [HttpGet]
        [Route("ObrisiObjavu")]
        public async Task<IActionResult> ObrisiObjavu(int objavaID)
        {

            if (!HttpContext.Session.IsLoggedIn())
            {


                return RedirectToAction("Login", "Home");
            }
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                _ = await session.RunAsync($"MATCH (n) WHERE id(n) = {objavaID} DETACH DELETE n");
            }
            finally
            {
                await session.CloseAsync();
            }
            return RedirectToAction("MojeObjave", "Home");
            
        }
        [HttpGet]
        [Route("Profil")]
        public async Task<IActionResult> Profil()
        {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");
            

            var userId = HttpContext.Session.GetUserId();
            var session = _driver.AsyncSession();
            try
            {
                var result = await session.RunAsync($"MATCH (k:Korisnik) WHERE id(k) = {userId} RETURN k");
                var user = (await result.SingleAsync())["k"].As<INode>();
                string str_godinaStudija = user.Properties["godinaStudija"].ToString();

                return View(new Korisnik
                {
                    ID = userId,
                    KorisnickoIme = user.Properties["korisnickoIme"].ToString(),
                    Sifra = user.Properties["sifra"].ToString(),
                    Email = user.Properties["email"].ToString(),
                    Fakultet = user.Properties["fakultet"].ToString(),
                    Smer = user.Properties["smer"].ToString(),
                    GodinaStudije = Int32.Parse(str_godinaStudija)

                });
            }
            finally
            {
                await session.CloseAsync();
            }
           
        }
        
        }
}
