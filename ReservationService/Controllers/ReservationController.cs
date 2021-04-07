﻿using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ReservationService.DBContexts;
using ReservationService.Models;
using ReservationService.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReservationService.Controllers
{
    /// <summary>
    /// Reservation controller this controller is used for the calls between API and frontend for managing the reservations in the ACI Rental system
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class ReservationController : ControllerBase
    {
        /// <summary>
        /// Database context for the reservation service, this is used to make calls to the reservation table
        /// </summary>
        private readonly ReservationServiceDatabaseContext _dbContext;

        /// <summary>
        /// Constructor is used for receiving the database context at the creation of the image controller
        /// </summary>
        /// <param name="dbContext">Database context param used for calls to the reservation table</param>
        public ReservationController(ReservationServiceDatabaseContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Reservation>>> GetReservations()
        {
            return await _dbContext.Reservations.ToListAsync();
        }

        [HttpPost("reserveproducts")]
        public async Task<IActionResult> ReserveProducts(ReserveProductModel reserveProductModel)
        {
            List<Reservation> revervations = new List<Reservation>();
            List<KeyValuePair<ProductModel, string>> productModelsErrorList = new List<KeyValuePair<ProductModel, string>>();
            if (reserveProductModel.ProductModels == null || reserveProductModel.ProductModels.Count <= 0)
            {
                return BadRequest("PRODUCT.RESERVE.NO_PRODUCTS");
            }


            foreach (ProductModel product in reserveProductModel.ProductModels)
            {
                if (product.Id <= 0)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_NO_ID"));
                }
                if (product.StartDate < DateTime.Now)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_INVALID_STARTDATE"));
                }
                if (product.EndDate < DateTime.Now)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_INVALID_ENDDATE"));
                }
                if (product.EndDate < product.StartDate)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_INVALID_ENDDATE"));
                }
                int weekenddays = await amountOfWeekendDays(product.StartDate, product.EndDate);
                double totalamountofdays = (product.EndDate - product.StartDate).TotalDays - weekenddays;
                if (totalamountofdays > 5)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.RESERVATION_TIME_TO_LONG"));
                }
                Reservation foundReservation = await _dbContext.Reservations.Where(x => x.StartDate >= product.StartDate && x.StartDate <= product.EndDate || x.EndDate >= product.StartDate && x.EndDate <= product.EndDate).FirstOrDefaultAsync();
                if (foundReservation != null)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_ALREADY_RESERVED_IN_PERIOD"));
                }
                var result =  await $"https://localhost:44372/api/product/{product.Id}".AllowAnyHttpStatus().GetStringAsync();
                Product foundProduct = JsonConvert.DeserializeObject<Product>(result);
                if (foundProduct == null)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_NOT_FOUND"));
                }
                if (foundProduct.ProductState != ProductState.AVAILABLE)
                {
                    productModelsErrorList.Add(new KeyValuePair<ProductModel, string>(product, "PRODUCT.RESERVE.PRODUCT_NOT_AVAILABLE"));
                }
                else
                {
                    //TODO: Add RenterID with JWT claims
                    Reservation reservation = new Reservation()
                    {
                        ProductId = product.Id,
                        StartDate = product.StartDate,
                        EndDate = product.EndDate,
                        IsApproved = foundProduct.RequiresApproval ? false : null,
                    };
                    revervations.Add(reservation);
                }
            }
            if (productModelsErrorList.Count > 0)
            {
                return BadRequest(productModelsErrorList);
            }
            return Ok();
        }

        private async Task<int> amountOfWeekendDays(DateTime startDate, DateTime endDate)
        {
            int amountOfWeekendDays = 0;
            for (DateTime i = startDate; i < endDate; i = i.AddDays(1))
            {
                if (i.DayOfWeek == DayOfWeek.Saturday || i.DayOfWeek == DayOfWeek.Sunday)
                {
                    amountOfWeekendDays = amountOfWeekendDays + 1;
                }
            }
            return amountOfWeekendDays;
        }
    }
}
