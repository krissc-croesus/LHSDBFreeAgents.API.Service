using LHSDBFreeAgentsAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LHSDBFreeAgentsAPI.Services
{
    public interface IOfferService
    {
        public Task CreateNewOffer(OfferModel model);
        public Task DeleteOffer(string username, int offerId);
        public Task<IEnumerable<OfferModel>> GetAllOffersByTeam(int teamId);
        public Task<IEnumerable<OfferModel>> GetAllOffersToPlayer(int playerId);
    }
}
