using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMoniesGame
{
    public class Board
    {
        public ConcurrentDictionary<int, Space> Spaces;
    }
}
