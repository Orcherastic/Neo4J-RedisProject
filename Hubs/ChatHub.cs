using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;
using NBP_I_PROJEKAT.Models;
using NBP_I_PROJEKAT;


namespace NBP_I_PROJEKAT.Hubs
{
    public class ChatHub : BaseHub
    {
        public async Task SaljiPoruku(string idObjave, string sadrzaj)
        {
            Poruka poruka = new Poruka(UserId.Value, UserName, sadrzaj);
            await Clients.Group(idObjave).SendAsync("MessageReceived", JsonConvert.SerializeObject(poruka), idObjave);
            RedisManager<Poruka>.Push($"poruke:{idObjave}", poruka);
        }

        public async Task Subscribe(string objava) => await Groups.AddToGroupAsync(Context.ConnectionId, objava);
    }
}