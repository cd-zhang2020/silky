using System.Threading.Tasks;
using Lms.Order.Application.Contracts.Orders;
using Lms.Order.Application.Contracts.Orders.Dtos;
using Lms.Order.Domain.Orders;
using Lms.Order.Domain.Shared.Orders;
using Silky.Lms.AutoMapper;
using Silky.Lms.Core.Extensions;
using Silky.Lms.Rpc.Transport;
using Silky.Lms.Transaction.Tcc;

namespace Lms.Order.Application.Orders
{
    public class OrderAppService : IOrderAppService
    {
        private readonly IOrderDomainService _orderDomainService;

        public OrderAppService(IOrderDomainService orderDomainService)
        {
            _orderDomainService = orderDomainService;

        }

        [TccTransaction(ConfirmMethod = "OrderCreateConfirm", CancelMethod = "OrderCreateCancel")]
        public async Task<GetOrderOutput> Create(CreateOrderInput input)
        {
            return await _orderDomainService.Create(input);
        }

        public async Task<GetOrderOutput> OrderCreateConfirm(CreateOrderInput input)
        {
            var orderId = RpcContext.GetContext().GetAttachment("orderId");
            var order = await _orderDomainService.GetById(orderId.To<long>());
            order.Status = OrderStatus.Payed;
            order = await _orderDomainService.Update(order);
            return order.MapTo<GetOrderOutput>();
        }

        public async Task OrderCreateCancel(CreateOrderInput input)
        {
            var orderId = RpcContext.GetContext().GetAttachment("orderId");
            if (orderId != null)
            {
                // await _orderDomainService.Delete(orderId.To<long>());
                var order = await _orderDomainService.GetById(orderId.To<long>());
                order.Status = OrderStatus.UnPay;
                await _orderDomainService.Update(order);
            }
        }
    }
}