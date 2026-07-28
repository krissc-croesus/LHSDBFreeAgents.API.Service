using LHSDBFreeAgentsAPI.Mappers;
using LHSDBFreeAgentsAPI.Models;
using LHSDBFreeAgentsAPI.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LHSDBFreeAgentsAPI.Services
{
    public class OfferService : IOfferService
    {
        private readonly IMapper _mapper;
        private readonly IOfferRepository _offerRepository;

        public OfferService(IOfferRepository repository, IMapper mapper)
        {
            this._offerRepository = repository;
            this._mapper = mapper;
        }

        public async Task CreateNewOffer(OfferModel model)
        {
            OfferDb newOffer = this._mapper.ToOfferDbModel(model);

            await this._offerRepository.CreateNewOffer(newOffer);
        }

        public async Task DeleteOffer(string username, int offerId)
        {
            await this._offerRepository.DeleteOffer(username, offerId);
        }

        public async Task<IEnumerable<OfferModel>> GetAllOffersByTeam(int teamId)
        {
            var response = await this._offerRepository.GetAllOffersByTeam(teamId);

            return _mapper.ToOfferResponse(response);
        }

        public async Task<IEnumerable<OfferModel>> GetAllOffersToPlayer(int playerId)
        {
            var response = await this._offerRepository.GetAllOffersToPlayer(playerId);
            return _mapper.ToOfferResponse(response);
        }
    }
}
