﻿using HotelManagement.Enums;

namespace HotelManagement.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; }

        public PaymentMethod Method { get; set; }
        public DateTime PaidAt { get; set; }
        public decimal Amount { get; set; }
    }

}
