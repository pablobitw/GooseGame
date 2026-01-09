using GameServer.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameServer.Repositories.Interfaces;

namespace GameServer.Helpers
{
    public interface IFriendshipRepositoryFactory
    {
        IFriendshipRepository Create();
    }

    public class FriendshipRepositoryFactory : IFriendshipRepositoryFactory
    {
        public IFriendshipRepository Create()
        {
            return new FriendshipRepository();
        }
    }
}