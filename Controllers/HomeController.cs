using Microsoft.AspNetCore.Mvc;
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using StackExchange.Redis;
using Neo4jClient;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Neo4j.Driver;
using NBP_I_PROJEKAT.Session;
using NBP_I_PROJEKAT.Hubs;
using NBP_I_PROJEKAT.Extensions;
using NBP_I_PROJEKAT.Models;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using NBP_I_PROJEKAT;
using System.Diagnostics;

namespace NBP_I.Controllers
{

    public class HomeController : Controller
    {

        public readonly IDriver _driver;
        public readonly IConnectionMultiplexer _redis;
        public readonly IHubContext<ObjavaHub> _hub;
        public readonly ILogger<HomeController> _logger;
        public (UserInfo, int)[] Leaderboard => RedisManager<UserInfo>.GetSortedSet("leaderboard", 10);

        public static readonly object _lock = new();
        public static bool _firstTimeRun = true;
        
        public HomeController(IDriver driver, IHubContext<ObjavaHub> hub, IConnectionMultiplexer redis, ILogger<HomeController> logger)
        {
            _driver = driver;
            _hub = hub;
            _logger = logger;
            _redis = redis;

            lock (_lock)
            {
                if (!_firstTimeRun)
                    return;
                _firstTimeRun = false;

                RedisManager<Notifikacija>.Subscribe("PostavljenaObjava", async (channel, data) =>
                {
                    data.Pratioci.ToList().ForEach(x => RedisManager<string>
                        .Push($"korisnici:{x}:notifikacije", $"{data.KorisnickoIme} kog pratite je postavio novu objavu - {data.ObjavaName} - | {data.ObjavaId}"));
                    await hub.Clients.All.SendAsync("PrimljenaNotifikacija", data);
                });
            }
        }
        public async Task<IActionResult> Index()
        {

            List<Tag> tagList;
            List<Objava> objavaList = null;
            List<Objava> objavaPreporukeList = null;



            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                // Pokupi sve tagove
                result = await session.RunAsync($"MATCH (t:Tag) RETURN t");



                tagList = (await result.ToListAsync()).Select(t =>
                {
                    INode tag = t["t"].As<INode>();
                    return new Tag
                    {
                        Id = (int)tag.Id,
                        Naziv = tag.Properties["naziv"].ToString()
                    };
                }).ToList();



                // Nadji sve tagove koji imaju objave i dodaj te objave u njihovu listu objava
                await Task.WhenAll(tagList.Select(async x =>
                {
                    session = _driver.AsyncSession();
                    result = await session.RunAsync($"MATCH (t:Tag {{naziv: '{x.Naziv}'}})-[r]-(o:Objava) return o");
                    var veze = await result.ToListAsync();
                    veze.ForEach(v =>
                    {
                        INode v1 = v["o"].As<INode>();
                        var o = new Objava
                        {
                            Id = (int)v1.Id,
                            Naziv = v1.Properties["naziv"].ToString(),
                            Sadrzaj = v1.Properties["sadrzaj"].ToString(),
                            Tag = v1.Properties["tag"].ToString()
                            
                        };
                        x.Objave.Add(o);
                    });
                }));



                var userId = HttpContext.Session.GetUserId();
                if (userId >= 0)
                {
                    
                    session = _driver.AsyncSession();
                    result = await session.RunAsync(@$"MATCH (k:Korisnik)-[r:VISITED]-(o:Objava) 
                                    WHERE id(k)={userId} 
                                    RETURN o, r, k");
                    objavaList = (await result.ToListAsync()).Select(o =>
                    {
                        INode oo = o["o"].As<INode>();
                        return new Objava
                        {
                            Id = (int)oo.Id,
                            Naziv = oo.Properties["naziv"].ToString(),
                            Sadrzaj = oo.Properties["sadrzaj"].ToString(),
                            Tag = oo.Properties["tag"].ToString()    
                        };
                    }).ToList();


                    session = _driver.AsyncSession();
                    result = await session.RunAsync(@$"MATCH (k:Korisnik)-[r:LIKED]-(o:Objava) 
                                    WHERE id(k)={userId} 
                                    RETURN o.tag
                                    LIMIT 5");
                    var fav = await result.ToListAsync();
                    if (fav.Count > 0)
                    {
                        
                        var favTag = fav[0].Values.Values.FirstOrDefault().ToString();
                        //var favTag = fav[0].Values.FirstOrDefault().ToString();

                        session = _driver.AsyncSession();
                     
                        result = await session.RunAsync(@$"MATCH (o:Objava) 
                                    WHERE o.tag = '{favTag}' 
                                    RETURN o");

                        objavaPreporukeList = (await result.ToListAsync()).Select(o =>
                        {
                            INode oo = o["o"].As<INode>();
                            Objava obj = new()
                            {
                                Id = (int)oo.Id,
                                Naziv = oo.Properties["naziv"].ToString(),
                                Sadrzaj = oo.Properties["sadrzaj"].ToString(),
                                Tag = oo.Properties["tag"].ToString()
                                
                            };
                            return obj;
                        }).Where(x => objavaList.Find(y => x.Id == y.Id) == null).ToList();
                    }
                }
            }
            finally
            {
                await session.CloseAsync();
            }
            return View(new Objave { TagList = tagList, ObjavaList = objavaList, ObjavaRecomendList = objavaPreporukeList, Leaderboard = Leaderboard });
        }
        public IActionResult Login()
        {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index","Home");

            return View();
        }
        public IActionResult Register()
        {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index");

            return View();
        }
        public IActionResult Odjava()
        {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        [Route("PrikaziSveObjaveKorisnika")]
        public async Task<IActionResult> PrikaziSveObjaveKorisnika(int userId)//radi
        {
            List<Objava> objavaList = null;
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (k:Korisnik)-[r:POSTED]->(o:Objava) WHERE id(k) = {userId} RETURN o");
                var list = await result.ToListAsync();



                if (list.Count > 0)
                {
                    Korisnik k = new Korisnik { ID = userId };
                    objavaList = new List<Objava>();
                    list.ForEach(x =>
                    {
                        INode objava = x["o"].As<INode>();
                        objavaList.Add(new Objava
                        {
                            Id = (int)objava.Id,
                            Naziv = objava.Properties["naziv"].ToString(),
                            Sadrzaj = objava.Properties["sadrzaj"].ToString(),
                            Tag = objava.Properties["tag"].ToString(),
                            Korisnik = k
                            
                        });
                    });
                }
            }
            finally
            {
                await session.CloseAsync();
            }
            return View(objavaList);
        }

        [HttpPost]
        [Route("Registracija")]
        public async Task<IActionResult> Registracija(string email, string korisnickoIme, string sifra, string fakultet, string smer, int godinaStudija)
        {

            if (HttpContext.Session.IsLoggedIn())
            {
                

                return RedirectToAction("Index", "Home");
            }

            var session = _driver.AsyncSession();
            try
            {
                var result = await session.RunAsync($"CREATE (k:Korisnik {{email: '{email}', korisnickoIme: '{korisnickoIme}', sifra:  '{sifra}', fakultet: '{fakultet}', smer:  '{smer}', godinaStudija: {godinaStudija} }}) return id(k)");
                var userId = await result.SingleAsync(record => record["id(k)"].As<int>());
                if (userId != -1)
                {
                    HttpContext.Session.SetString(SessionKeys.Username, korisnickoIme);
                    HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
                    return RedirectToAction("Login", "Home");
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Register", "Home");
        }

        [HttpPost]
        [Route("Prijava")]
        public async Task<IActionResult> Prijava(string KorisnickoIme, string Sifra)
        {
            if (HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");
            


            var session = _driver.AsyncSession();
            try
            {
                var result = await session.RunAsync($"MATCH (k:Korisnik {{korisnickoIme: '{KorisnickoIme}', sifra: '{Sifra}'}}) RETURN id(k)");

                var res = await result.ToListAsync();
                if (res.Count == 0)
                    return RedirectToAction("Login", "Home");

                var userId = res[0]["id(k)"].As<int>();

                if (userId != -1)
                {
                    HttpContext.Session.SetString(SessionKeys.Username, KorisnickoIme);
                    HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
                    return RedirectToAction("Index","Home");
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Login", "Home");
        }

        [HttpGet]
        [Route("PromeniPodatke")]
        public async Task<IActionResult> PromeniPodatke(string id, string KorisnickoIme, string Email, string GodinaStudije, string Smer, string Fakultet, /*string password1,*/ string Sifra, string repassword)
        {
            
            var newPass = " ";
            if (!string.IsNullOrEmpty(Sifra) && Sifra.CompareTo(repassword) == 0)
                newPass = Sifra;
            else
            {
                return BadRequest("Sifre nisu iste!");
            }
            var userId = int.Parse(id);
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                _ = await session.RunAsync(@$"MATCH (k:Korisnik) 
                                    WHERE id(k)={userId}
                                    SET k = {{ email: '{Email}',fakultet: '{Fakultet}', godinaStudija: '{GodinaStudije}' ,korisnickoIme: '{KorisnickoIme}', sifra: '{newPass}',  smer: '{Smer}'}} ");
            }
            finally
            {
                await session.CloseAsync();
            }

            
            return RedirectToAction("Profil", "Objava");

        }
        [HttpGet]
        [Route("PreuzmiPodatke")]
        public async Task<IActionResult> PreuzmiPodatke()
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
                return Ok(new Korisnik
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

        [HttpGet]
        [Route("ZapratiKorisnika")]
        public async Task<IActionResult> ZapratiKorisnika(int uId)
        {
            var userId = HttpContext.Session.GetUserId();
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync(@$"MATCH (k1:Korisnik)-[r:PRATI]->(k2:Korisnik)
                                        WHERE id(k1) = {userId} AND id(k2) = {uId}
                                        RETURN k1, r, k2");//k1 zapracuje k2 i vraca k1 k2 i vezu izmedju(prati)
                var checkIfFollow = await result.ToListAsync();

                if (checkIfFollow.Count == 0)//da ga vec ne pratis
                {
                    session = _driver.AsyncSession();
                    _ = await session.RunAsync(@$"MATCH(k1:Korisnik) WHERE id(k1)={userId} 
                                    MATCH (k2:Korisnik) WHERE id(k2)={uId} 
                                    CREATE (k1)-[:PRATI]->(k2)"); 

                    RedisManager<int>.Push($"korisnici:{uId}:pratioci", userId);
                }
                else
                {
                    return BadRequest("Vec pratite ovog korisnika!!!");
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Index", "Home");
           
        }
        [HttpGet]
        [Route("MojeObjave")]
        public async Task<IActionResult> MojeObjave()
        {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");
            //return BadRequest("Niste ulogovani!");

            var userId = HttpContext.Session.GetUserId();
            List<Objava> objavaList = new();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (k:Korisnik)-[r:POSTED]-(o:Objava) WHERE id(k) = {userId} RETURN o");
                var objave = await result.ToListAsync();
                objave.ForEach(a => {
                    INode o1 = a["o"].As<INode>();
                    objavaList.Add(new()
                    {
                        Id = (int)o1.Id,
                        Naziv = o1.Properties["naziv"].ToString(),
                        Sadrzaj = o1.Properties["sadrzaj"].ToString(),
                        Tag = o1.Properties["tag"].ToString(),
                       
                    });
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(objavaList);
        }
        [HttpGet]
        [Route("OtpratiKorisnika")]
        public async Task<IActionResult> OtpratiKorisnika(int uId)
        {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var userId = HttpContext.Session.GetUserId();
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                _ = await session.RunAsync(@$"MATCH (k1:Korisnik)-[r:PRATI]->(k2:Korisnik)
                                        WHERE id(k1) = {userId} AND id(k2) = {uId}
                                        DELETE r");
                    }
            finally
            {
                await session.CloseAsync();
            }
            //return View();
            return RedirectToAction("Index", "Home");
           


        }
        [HttpGet]
        [Route("PrikaziPojedinacneObjaveKorisnika")]
        public async Task<IActionResult> PrikaziPojedinacneObjaveKorisnika(int objavaId)//radi i ovo
        {
            Poruka[] poruke = RedisManager<Poruka>.GetAll($"poruke:{objavaId}");
            ;
            if (!HttpContext.Session.IsLoggedIn())
            {
                return RedirectToAction("Login", "Home");
            }



            var userId = HttpContext.Session.GetUserId();
            Objava objava;
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                //Sta treba ovde?
                result = await session.RunAsync($"MATCH (k:Korisnik)-[r:POSTED]-(o:Objava) WHERE id(o) = {objavaId} RETURN k, o");
                var objava1 = await result.SingleAsync();

                //vraca krosnika koji je objavio tu objavu

                INode objava2 = objava1["o"].As<INode>();
                INode k = objava1["k"].As<INode>();



                if (userId == (int)k.Id)//ako si ti objavio,kreria se nova objava
                {
                    objava = new()
                    {
                        Id = (int)objava2.Id,
                        Naziv = objava2.Properties["naziv"].ToString(),
                        Sadrzaj = objava2.Properties["sadrzaj"].ToString(),
                        Tag = objava2.Properties["tag"].ToString(),
                        Poruke = poruke,
                        Korisnik = null,
                      
                        
                    };
                }
                else
                {
                    session = _driver.AsyncSession();
                    result = await session.RunAsync($"MATCH (k1:Korisnik)<-[r:PRATI]-(k2:Korisnik) WHERE id(k1) = {(int)k.Id} RETURN id(k2)");
                    var pratiociId = await result.ToListAsync();


                    Korisnik kor = new()
                    {
                        ID = (int)k.Id,
                        KorisnickoIme = k.Properties["korisnickoIme"].ToString(),
                        Pratioci = pratiociId.Select(x => x["id(k2)"].As<int>()).ToList()
                    };



                    objava = new()
                    {
                        Id = (int)objava2.Id,
                        Naziv = objava2.Properties["naziv"].ToString(),
                        Sadrzaj = objava2.Properties["sadrzaj"].ToString(),
                        Tag = objava2.Properties["tag"].ToString(),
                        Korisnik = kor,
                        Poruke = poruke
                        
                    };
                }
                
            }
            finally
            {
                await session.CloseAsync();
            }


            
            return View(objava);
          
        }
        [HttpGet]
        [Route("LajkujObjavuKorisnika")]
        public async Task<IActionResult> LajkujObjavuKorisnika(int objavaId)
        {
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                var mojId = HttpContext.Session.GetUserId();


                var korisnik = await session.RunAsync($"MATCH (k:Korisnik) -[:POSTED]->(o:Objava) WHERE id(o) = {objavaId} RETURN k");
                var user = (await korisnik.SingleAsync())["k"].As<INode>();
                var userId = user.Id;
                var userName = user.Properties["korisnickoIme"].ToString();

                session = _driver.AsyncSession();

                        var rez = await session.RunAsync(@$"MATCH(k:Korisnik) WHERE id(k)={mojId} 
                                                MATCH (o:Objava) WHERE id(o)={objavaId} 
                                                CREATE (k)-[:LIKED]->(o)");//da se napravi poteg sa lajkovanom objavom
                RedisManager<UserInfo>.IncrementSortedSet($"leaderboard", new((int)userId, userName));

                return RedirectToAction("Index", "Home");
                //return View("Index", "Home");
            }
            catch (Exception e)
            {
                return Json(new { success = false, error = e.Message });
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}
