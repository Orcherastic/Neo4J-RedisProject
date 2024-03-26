using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBP_I_PROJEKAT.Models
{
    public class Objave
    {
        public List<Tag> TagList { get; set; }
        public List<Objava> ObjavaList { get; set; }
        public List<Objava> ObjavaRecomendList { get; set; }
        public (UserInfo userInfo, int score)[] Leaderboard { get; set; }
    }
}
