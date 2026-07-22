using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using LHSDBFreeAgentsAPI.Models;
using Microsoft.AspNetCore.Authorization;

namespace LHSDBFreeAgentsAPI.Repositories.DynamoDBImpl
{
    public class OfferRepository : IOfferRepository
    {
        private readonly IDynamoDBContext _context;

        public OfferRepository(IAmazonDynamoDB dynamoDbClient)
        {
            _context = new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => dynamoDbClient)
                .Build();
        }

        public async Task CreateNewOffer(OfferDb newOffer)
        {
            try
            {
                await this._context.SaveAsync<OfferDb>(newOffer);
            }
            catch(Exception ex)
            {
                throw new Exception($"Amazon error in Write operation! Error: {ex}");
            }
        }

        public async Task<IEnumerable<OfferDb>> GetAllOffersByTeam(int teamId)
        {
            var config = new QueryConfig
            {
                IndexName = "TeamID-index"
            };

            return await _context.QueryAsync<OfferDb>(teamId, config).GetRemainingAsync();
        }

        public async Task<IEnumerable<OfferDb>> GetAllOffersToPlayer(int playerId)
        {
            return await this._context.QueryAsync<OfferDb>(playerId).GetRemainingAsync();
        }

        public async Task DeleteOffer(string username, int offerHashKey)
        {
            var config = new QueryConfig
            {
                IndexName = "PlayerID-OfferedBy-index",
                QueryFilter = new List<ScanCondition>()
            };
            config.QueryFilter.Add(new ScanCondition("OfferedBy", ScanOperator.Equal, username));

            var searchResults = await _context.QueryAsync<OfferDb>(offerHashKey, config).GetRemainingAsync();

            if (searchResults != null && searchResults.Count == 1)
            {
                try
                {
                    await this._context.DeleteAsync<OfferDb>(searchResults.ElementAt(0));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Amazon error in delete operation! Error: {ex}");
                }
            }
            else
            {
                throw new Exception("Offer not found");
            }
        }
    }
}
