using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using NBP_I_PROJEKAT.Session;

namespace NBP_I_PROJEKAT.Hubs
{
    public class BaseHub : Hub
    {
        public int? UserId => Context.GetHttpContext().Session.GetInt32(SessionKeys.UserId);
        public string UserName => Context.GetHttpContext().Session.GetString(SessionKeys.Username);
    }
}
