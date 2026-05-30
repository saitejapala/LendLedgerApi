using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace LendLedgerApi.WebApi.Controllers
{
    public class ApiControllerBase : ControllerBase
    {
        protected Guid LenderId
        {
            get
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(idClaim) || !Guid.TryParse(idClaim, out var guid))
                {
                    throw new InvalidOperationException("User context name identifier claim is missing or invalid.");
                }
                return guid;
            }
        }
    }
}
