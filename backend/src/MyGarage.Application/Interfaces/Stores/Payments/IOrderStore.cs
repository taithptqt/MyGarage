using System.Collections.Generic;
using MyGarage.Application.Payments.DTOs;

namespace MyGarage.Interfaces
{
    public interface IOrderStore
    {
        List<Order> GetAllOrders();
        Order? GetOrderById(int id);
        Order? GetOrderByOrderCode(long orderCode);
        Order? GetOrderByPaymentLinkId(string paymentLinkId);
        void CreateOrder(Order order);
        bool UpdateOrder(int id, Order updatedOrder);
        bool DeleteOrder(int id);
    }
}
