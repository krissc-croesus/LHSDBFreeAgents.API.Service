using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LHSDBFreeAgentsAPI.Models;
using LHSDBFreeAgentsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LHSDBFreeAgentsAPI.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class OffersController : ControllerBase
    {
        private readonly IOfferService _offerService;
        private readonly ILogger _logger;

        public OffersController(IOfferService offerService, ILogger<OffersController> logger)
        {
            _offerService = offerService;
            _logger = logger;
        }

        // POST /offers
        [HttpPost]
        public async Task<IActionResult> CreateOffer(OfferModel value)
        {
            if (value == null)
            {
                return BadRequest("Missing offer model");
            }

            string username = this.User.FindFirst("username").Value;
            if (!value.OfferedBy.Equals(username))
            {
                return Unauthorized();
            }

            try
            {
                await _offerService.CreateNewOffer(value);
            }
            catch (Exception e)
            {
                return BadRequest(e);
            }

            return Ok("Offer created correctly");
        }

        [HttpDelete]
        [Route("{offerId}")]
        public async Task<IActionResult> DeleteOffer(int offerId)
        {
            string username = this.User.FindFirst("username").Value;
            try
            {
                await _offerService.DeleteOffer(username, offerId);
            }
            catch (Exception e)
            {
                return NotFound(e);
            }

            return Ok();

        }

        [HttpGet]
        [Route("teams/{teamId}")]
        public async Task<IActionResult> GetAllOffersByTeam(int teamId)
        {
            var currentUser = HttpContext.User;

            // TODO block for other teams
            try
            {
                if (teamId <= 0)
                {
                    return BadRequest("Missing team ID");
                }

                var results = await _offerService.GetAllOffersByTeam(teamId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("players/{playerId}")]
        public async Task<IActionResult> GetAllOffersToPlayer(int playerId)
        {
            try
            {
                if (playerId <= 0)
                {
                    return BadRequest("Missing valid player ID");
                }

                var results = await _offerService.GetAllOffersToPlayer(playerId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
        }
    }
}
